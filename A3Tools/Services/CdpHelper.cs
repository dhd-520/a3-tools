using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace A3Tools.Services;

/// <summary>
/// Chrome DevTools Protocol (CDP) 会话封装
/// 用于在外部 Chrome/Edge 浏览器中自动填写并提交网页表单
/// 协议说明：https://chromedevtools.github.io/devtools-protocol/
/// </summary>
public class CdpSession : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    private int _nextId = 0;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentQueue<byte> _rxBuffer = new();

    public event Action<string, JsonElement>? OnEvent;

    public bool IsOpen => _ws.State == WebSocketState.Open;

    /// <summary>
    /// 从 /json/version 拿 websocketUrl
    /// </summary>
    public static async Task<string?> GetWebSocketUrlAsync(int port)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json/version");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 连接到指定 wsUrl
    /// </summary>
    public static async Task<CdpSession> ConnectAsync(string wsUrl)
    {
        var session = new CdpSession();
        await session._ws.ConnectAsync(new Uri(wsUrl), session._cts.Token);
        _ = Task.Run(session.ReadLoopAsync);
        return session;
    }

    /// <summary>
    /// 发送 CDP 命令并等待响应
    /// </summary>
    public async Task<JsonElement> SendCommandAsync(string method, object? parameters = null, int timeoutMs = 10000)
    {
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var message = parameters == null
            ? (object)new { id, method }
            : new { id, method, @params = parameters };
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);

        using var cts = new CancellationTokenSource(timeoutMs);
        using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            return await tcs.Task;
        }
    }

    /// <summary>
    /// 执行 JS 表达式并返回结果
    /// </summary>
    public async Task<JsonElement> EvaluateAsync(string expression, int timeoutMs = 10000)
    {
        return await SendCommandAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = true,
            awaitPromise = false
        }, timeoutMs);
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buffer, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var bytes = new byte[result.Count];
                Buffer.BlockCopy(buffer, 0, bytes, 0, result.Count);

                // CDP 消息以 0x00 长度前缀（Binary 模式）或纯文本（Text 模式）
                // 我们用 Text 模式发送，对端也用 Text 模式回复
                HandleMessage(bytes);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CDP read loop error: {ex.Message}");
        }
    }

    private void HandleMessage(byte[] bytes)
    {
        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idEl))
            {
                // 响应消息
                int id = idEl.GetInt32();
                if (_pending.TryRemove(id, out var tcs))
                {
                    if (root.TryGetProperty("error", out var errEl))
                    {
                        tcs.TrySetException(new Exception($"CDP error: {errEl.GetRawText()}"));
                    }
                    else
                    {
                        var result = root.TryGetProperty("result", out var r) ? r.Clone() : default;
                        tcs.TrySetResult(result);
                    }
                }
            }
            else if (root.TryGetProperty("method", out var methodEl))
            {
                // 事件消息
                var method = methodEl.GetString() ?? "";
                var @params = root.TryGetProperty("params", out var p) ? p.Clone() : default;
                try { OnEvent?.Invoke(method, @params); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CDP event handler error: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CDP message parse error: {ex.Message}");
        }
    }

    public async Task CloseAsync()
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", closeCts.Token);
            }
        }
        catch { }
        _cts.Cancel();
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _ws.Dispose();
            _cts.Dispose();
        }
        catch { }
    }
}

/// <summary>
/// CDP 辅助工具：启动浏览器 + 自动登录
/// </summary>
public static class CdpHelper
{
    /// <summary>
    /// 检查浏览器是否支持 CDP（Chrome/Edge）
    /// </summary>
    public static bool IsCdpSupported(string browser)
    {
        return browser == "chrome" || browser == "msedge";
    }

    /// <summary>
    /// 找一个空闲端口（CDP 远程调试用）
    /// </summary>
    public static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 生成临时 user-data-dir 路径（避免与用户自己的 Chrome 配置文件冲突）
    /// </summary>
    public static string GetTempUserDataDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"A3Tools_CdpProfile_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 自动登录：导航 + 轮询填表 + 提交
    /// 适合 SPA 登录页（Angular/Vue/React），会等表单元素渲染后再填
    /// </summary>
    /// <param name="session">已连接的 CDP 会话</param>
    /// <param name="url">目标 URL</param>
    /// <param name="usernameSel">用户名输入框 CSS 选择器</param>
    /// <param name="passwordSel">密码输入框 CSS 选择器</param>
    /// <param name="submitSel">登录按钮 CSS 选择器</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="timeoutMs">总超时（默认 8 秒）</param>
    /// <returns>true=登录成功，false=超时失败</returns>
    public static async Task<bool> AutoLoginAsync(
        CdpSession session,
        string url,
        string usernameSel,
        string passwordSel,
        string submitSel,
        string username,
        string password,
        int timeoutMs = 8000)
    {
        // 启用 Page + Runtime domain
        await session.SendCommandAsync("Page.enable");
        await session.SendCommandAsync("Runtime.enable");

        // 导航到目标 URL
        await session.SendCommandAsync("Page.navigate", new { url });

        // 构造填表 JS：用 querySelectorAll('sel') 拿所有匹配，挨个赋值并触发 input 事件
        // （Angular 这种双向绑定框架必须 dispatch input 事件）
        var uJs = JsonSerializer.Serialize(username);
        var pJs = JsonSerializer.Serialize(password);
        var uSelJs = JsonSerializer.Serialize(usernameSel);
        var pSelJs = JsonSerializer.Serialize(passwordSel);
        var bSelJs = JsonSerializer.Serialize(submitSel);

        string script = $@"
(function() {{
  function findFirst(sel) {{
    try {{
      var el = document.querySelector(sel);
      if (el) return el;
    }} catch (e) {{}}
    return null;
  }}
  function setVal(el, val) {{
    var last = el.value;
    el.value = val;
    if (el.value !== val) {{
      el.value = val;  // 某些 input 需要 set + dispatch 配合
    }}
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }}
  var u = findFirst({uSelJs});
  var p = findFirst({pSelJs});
  var b = findFirst({bSelJs});
  if (u && p && b) {{
    setVal(u, {uJs});
    setVal(p, {pJs});
    b.click();
    return true;
  }}
  return false;
}})()
";

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                var result = await session.EvaluateAsync(script);
                // 解析 result.result.value
                if (result.TryGetProperty("result", out var resultEl) &&
                    resultEl.TryGetProperty("value", out var valueEl) &&
                    valueEl.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }
            catch
            {
                // 页面可能还没 ready，吞掉异常继续重试
            }
            await Task.Delay(200);
        }
        return false;
    }
}
