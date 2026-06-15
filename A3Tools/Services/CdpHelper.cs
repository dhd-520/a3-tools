using System.Collections.Concurrent;
using System.Diagnostics;
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

    public event Action<string, JsonElement>? OnEvent;

    public bool IsOpen => _ws.State == WebSocketState.Open;

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
                HandleMessage(bytes);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"CDP read loop error: {ex.Message}");
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
                var method = methodEl.GetString() ?? "";
                var @params = root.TryGetProperty("params", out var p) ? p.Clone() : default;
                try { OnEvent?.Invoke(method, @params); }
                catch (Exception ex) { Debug.WriteLine($"CDP event handler error: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CDP message parse error: {ex.Message}");
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
    /// 查找本地空闲端口（CDP 远程调试用）
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
    /// 写日志到文件 + Debug 输出（双写）
    /// 文件位置：A3Tools 同目录的 cdp.log（方便不开 VS 也能排查问题）
    /// </summary>
    public static void CdpLog(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [CDP] {msg}";
        Debug.WriteLine(line);
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "cdp.log");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>
    /// 从 /json/version 拿 websocketUrl
    /// 1. 先用 TcpClient 探活（连接被拒绝时不会抛 HttpRequestException）
    /// 2. 端口活着才走 HttpClient 拿 wsUrl
    /// Chrome 默认绑 127.0.0.1（IPv4），优先试 127.0.0.1 和 localhost
    /// </summary>
    public static async Task<string?> GetWebSocketUrlAsync(int port)
    {
        // 优先 127.0.0.1（Edge/Chrome 默认绑 IPv4），其次 localhost
        var targets = new (string host, System.Net.IPAddress[] addrs)[]
        {
            ("127.0.0.1", new[] { System.Net.IPAddress.Parse("127.0.0.1") }),
            ("localhost", new[] { System.Net.IPAddress.Loopback }),
        };
        foreach (var (host, addrs) in targets)
        {
            System.Net.IPAddress? connectedAddr = null;
            foreach (var addr in addrs)
            {
                try
                {
                    using var tcp = new System.Net.Sockets.TcpClient();
                    var connectTask = tcp.ConnectAsync(addr, port);
                    var timeoutTask = Task.Delay(1500);
                    var done = await Task.WhenAny(connectTask, timeoutTask);
                    if (done == connectTask && tcp.Connected)
                    {
                        connectedAddr = addr;
                        break;
                    }
                }
                catch
                {
                    // 忽略，尝试下一个地址
                }
            }
            if (connectedAddr == null)
            {
                CdpLog($"端口 {port} 不可达 (尝试 {host})");
                continue;
            }

            // 端口活着，走 HTTP 拿 wsUrl
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var url = $"http://{connectedAddr}:{port}/json/version";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsEl))
                {
                    CdpLog($"✓ 拿到 WebSocket URL (host={connectedAddr})");
                    return wsEl.GetString();
                }
            }
            catch (Exception ex)
            {
                CdpLog($"http://{connectedAddr}:{port}/json/version 拿不到: {ex.GetType().Name}: {ex.Message}");
            }
        }
        CdpLog($"✗ 所有 host 都连不上，请检查：1) Edge/Chrome 是否启动 2) 防火墙是否拦截端口 {port} 3) --remote-debugging-port 是否生效");
        return null;
    }

    /// <summary>
    /// 从 /json/list 拿一个 page target 的 WebSocket URL
    /// /json/version 返回的是 browser-level 连接（只能调 Browser/Target 域）
    /// /json/list 返回的是页面级连接（能调 Page 域，这才是填表要的）
    /// 优先选 type=page 且有 URL 的（不是 about:blank 或 chrome://newtab）
    /// </summary>
    public static async Task<string?> GetPageWebSocketUrlAsync(int port)
    {
        var targets = new (string host, System.Net.IPAddress[] addrs)[]
        {
            ("127.0.0.1", new[] { System.Net.IPAddress.Parse("127.0.0.1") }),
            ("localhost", new[] { System.Net.IPAddress.Loopback }),
        };
        foreach (var (host, addrs) in targets)
        {
            // 先 TcpClient 探活
            System.Net.IPAddress? connectedAddr = null;
            foreach (var addr in addrs)
            {
                try
                {
                    using var tcp = new System.Net.Sockets.TcpClient();
                    var connectTask = tcp.ConnectAsync(addr, port);
                    var timeoutTask = Task.Delay(1500);
                    var done = await Task.WhenAny(connectTask, timeoutTask);
                    if (done == connectTask && tcp.Connected)
                    {
                        connectedAddr = addr;
                        break;
                    }
                }
                catch { }
            }
            if (connectedAddr == null) continue;

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var url = $"http://{connectedAddr}:{port}/json/list";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                string? firstPageWs = null;
                string? anyPageWs = null;
                foreach (var target in doc.RootElement.EnumerateArray())
                {
                    if (!target.TryGetProperty("type", out var typeEl)) continue;
                    var type = typeEl.GetString();
                    if (type != "page") continue;
                    if (!target.TryGetProperty("webSocketDebuggerUrl", out var wsEl)) continue;
                    var ws = wsEl.GetString();
                    if (string.IsNullOrEmpty(ws)) continue;

                    // 有 URL 的 page 优先（不是 about:blank）
                    if (target.TryGetProperty("url", out var urlEl))
                    {
                        var pageUrl = urlEl.GetString() ?? "";
                        if (!string.IsNullOrEmpty(pageUrl) &&
                            !pageUrl.StartsWith("about:") &&
                            !pageUrl.StartsWith("chrome://") &&
                            !pageUrl.StartsWith("edge://"))
                        {
                            firstPageWs = ws;
                            break;
                        }
                    }
                    anyPageWs ??= ws;
                }
                var result = firstPageWs ?? anyPageWs;
                if (!string.IsNullOrEmpty(result))
                {
                    CdpLog($"✓ 拿到 page WebSocket URL (host={connectedAddr})");
                    return result;
                }
            }
            catch (Exception ex)
            {
                CdpLog($"http://{connectedAddr}:{port}/json/list 拿不到: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>
    /// 自动登录：导航 + 轮询填表 + 提交
    /// 适合 SPA 登录页（Angular/Vue/React/antd-mobile），会等表单元素渲染后再填
    /// </summary>
    public static async Task<bool> AutoLoginAsync(
        CdpSession session,
        string url,
        string usernameSel,
        string passwordSel,
        string submitSel,
        string username,
        string password,
        int timeoutMs = 15000)
    {
        // 启用 Page + Runtime domain
        await session.SendCommandAsync("Page.enable");
        await session.SendCommandAsync("Runtime.enable");

        // 1. 导航前先订阅 Page.loadEventFired 事件（页面真的加载完了才触发表单轮询）
        // 2. SPA 页面需要下载 JS bundle + bootstrap framework + 渲染表单，仅靠 readyState 不够，
        //    还要等表单元素实际出现在 DOM 中
        var loadEventTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoadEvent(string method, JsonElement p)
        {
            if (method == "Page.loadEventFired")
            {
                CdpLog("✓ 页面 loadEventFired（页面主资源加载完成）");
                loadEventTcs.TrySetResult(true);
            }
        }
        session.OnEvent += OnLoadEvent;

        try
        {
            // 导航到目标 URL
            CdpLog($"导航到：{url}");
            await session.SendCommandAsync("Page.navigate", new { url });

            // 3. 等 loadEventFired 事件（页面主资源加载完）
            //    超时 10 秒（避诼 SPA 卡死），超时也继续填表（页面可能从缓存加载）
            var loadTask = loadEventTcs.Task;
            var loadTimeoutTask = Task.Delay(10000);
            var loadDone = await Task.WhenAny(loadTask, loadTimeoutTask);
            if (loadDone == loadTimeoutTask)
            {
                CdpLog("⚠️ loadEventFired 超时（10s），强制继续填表");
            }
        }
        finally
        {
            session.OnEvent -= OnLoadEvent;
        }

        // 构造填表 JS：用 querySelector 拿元素，挨个赋值并触发 input 事件
        // 关键：React/antd-mobile 框架用了 Object.getOwnPropertyDescriptor 拦截 value 的设置，
        //      必须用原生 HTMLInputElement 的 value setter 才能触发框架的 onChange
        var uJs = JsonSerializer.Serialize(username);
        var pJs = JsonSerializer.Serialize(password);
        var uSelJs = JsonSerializer.Serialize(usernameSel);
        var pSelJs = JsonSerializer.Serialize(passwordSel);
        var bSelJs = JsonSerializer.Serialize(submitSel);

        // 诊断 + 填表脚本
        // 返回值约定：{{ ok: bool, missing: [string], count: {{u:int, p:int, b:int}}, url: string }}
        // 1. 在主 frame 和所有 iframe 里查找元素
        // 2. 返回每个选择器在所有 frame 中找到的元素总数（便于诊断）
        // 3. 填表成功后返回 ok=true
        string script = $@"
(function() {{
  function findInAllFrames(sel) {{
    // 在当前 document 和所有 iframe 里查找，返回所有匹配元素数组
    // 同时穿透 shadow DOM（Angular Material 等组件库会用到）
    var results = [];
    function deepQuerySelectorAll(root, selector) {{
      var out = [];
      try {{
        var direct = root.querySelectorAll(selector);
        for (var i = 0; i < direct.length; i++) out.push(direct[i]);
      }} catch (e) {{}}
      // 穿透 shadow root
      var all = root.querySelectorAll('*');
      for (var k = 0; k < all.length; k++) {{
        if (all[k].shadowRoot) {{
          var shadowResults = deepQuerySelectorAll(all[k].shadowRoot, selector);
          for (var m = 0; m < shadowResults.length; m++) out.push(shadowResults[m]);
        }}
      }}
      return out;
    }}
    function search(doc) {{
      var els = deepQuerySelectorAll(doc, sel);
      for (var i = 0; i < els.length; i++) results.push(els[i]);
      // 递归子 iframe
      var iframes = doc.querySelectorAll('iframe');
      for (var j = 0; j < iframes.length; j++) {{
        try {{
          var idoc = iframes[j].contentDocument || iframes[j].contentWindow?.document;
          if (idoc) search(idoc);
        }} catch (e) {{}}
      }}
    }}
    search(document);
    return results;
  }}
  function setVal(el, val) {{
    var proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
    var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, val);
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }}
  function diagStatus() {{
    var uMatches = findInAllFrames({uSelJs});
    var pMatches = findInAllFrames({pSelJs});
    var bMatches = findInAllFrames({bSelJs});
    var status = {{
      ok: false,
      url: window.location.href,
      count: {{ u: uMatches.length, p: pMatches.length, b: bMatches.length }},
      missing: []
    }};
    if (uMatches.length === 0) status.missing.push('username');
    if (pMatches.length === 0) status.missing.push('password');
    if (bMatches.length === 0) status.missing.push('button');
    return status;
  }}
  // 第一道闸门：页面必须 readyState=complete
  if (document.readyState !== 'complete') {{
    return {{ ok: false, reason: 'loading', readyState: document.readyState, diag: diagStatus() }};
  }}
  var uMatches = findInAllFrames({uSelJs});
  var pMatches = findInAllFrames({pSelJs});
  var bMatches = findInAllFrames({bSelJs});
  if (uMatches.length > 0 && pMatches.length > 0 && bMatches.length > 0) {{
    setVal(uMatches[0], {uJs});
    setVal(pMatches[0], {pJs});
    bMatches[0].click();
    return {{ ok: true, count: {{ u: uMatches.length, p: pMatches.length, b: bMatches.length }} }};
  }}
  return {{ ok: false, reason: 'not_found', diag: diagStatus() }};
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
                if (result.TryGetProperty("result", out var resultEl) &&
                    resultEl.TryGetProperty("value", out var valueEl) &&
                    valueEl.ValueKind == JsonValueKind.Object)
                {
                    // 读取结构化的诊断信息
                    string? reason = null;
                    string? missing = null;
                    string? diagUrl = null;
                    string? countInfo = null;
                    if (valueEl.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True)
                    {
                        if (valueEl.TryGetProperty("count", out var cntEl))
                        {
                            countInfo = $"u={cntEl.GetProperty("u").GetInt32()} p={cntEl.GetProperty("p").GetInt32()} b={cntEl.GetProperty("b").GetInt32()}";
                        }
                        CdpLog($"✓ 第 {attempt} 次轮询填表成功 ({countInfo})");
                        return true;
                    }
                    if (valueEl.TryGetProperty("reason", out var reasonEl))
                    {
                        reason = reasonEl.GetString();
                    }
                    if (valueEl.TryGetProperty("diag", out var diagEl))
                    {
                        if (diagEl.TryGetProperty("missing", out var mEl) && mEl.ValueKind == JsonValueKind.Array)
                        {
                            var missList = new List<string>();
                            foreach (var m in mEl.EnumerateArray())
                            {
                                var s = m.GetString();
                                if (!string.IsNullOrEmpty(s)) missList.Add(s);
                            }
                            missing = string.Join(",", missList);
                        }
                        if (diagEl.TryGetProperty("url", out var uEl))
                        {
                            diagUrl = uEl.GetString();
                        }
                        if (diagEl.TryGetProperty("count", out var cEl))
                        {
                            countInfo = $"u={cEl.GetProperty("u").GetInt32()} p={cEl.GetProperty("p").GetInt32()} b={cEl.GetProperty("b").GetInt32()}";
                        }
                    }
                    // 第一次或每次 missing 变化时打日志
                    if (reason == "not_found" && !string.IsNullOrEmpty(missing))
                    {
                        CdpLog($"  第 {attempt} 次：未找到 [{missing}] (找到数量：{countInfo}) URL={diagUrl}");
                    }
                    else if (reason == "loading")
                    {
                        // 页面还在 loading，不刷屏
                    }
                }
            }
            catch (Exception ex)
            {
                // 页面可能还没 ready，吞掉异常继续重试
                if (attempt == 1) CdpLog($"  脚本执行异常: {ex.GetType().Name}: {ex.Message}");
            }
            await Task.Delay(200);
        }
        CdpLog($"✗ {timeoutMs}ms 内未填表成功（试了 {attempt} 次）");
        return false;
    }
}
