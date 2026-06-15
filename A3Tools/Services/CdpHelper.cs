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
    /// 扫描现有浏览器进程，找到带 --remote-debugging-port=XXXX 的进程
    /// 【Tab 模式用】复用现有浏览器实例，在现有窗口开新 Tab
    /// </summary>
    /// <returns>调试端口（如 9222），如果没找到返回 0</returns>
    public static int FindExistingBrowserDebugPort(string browser)
    {
        string procName = browser == "msedge" ? "msedge" : (browser == "chrome" ? "chrome" : "");
        if (string.IsNullOrEmpty(procName)) return 0;
        try
        {
            var procs = Process.GetProcessesByName(procName);
            foreach (var p in procs)
            {
                try
                {
                    var searcher = new System.Management.ManagementObjectSearcher(
                        $"SELECT CommandLine FROM Win32_Process WHERE ProcessId={p.Id}");
                    foreach (var obj in searcher.Get())
                    {
                        var cmd = obj["CommandLine"]?.ToString() ?? "";
                        var match = System.Text.RegularExpressions.Regex.Match(
                            cmd, @"--remote-debugging-port=(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
                        {
                            CdpLog($"发现现有 {procName} 带调试端口: PID={p.Id} port={port}");
                            return port;
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            CdpLog($"扫描现有 {procName} 进程失败: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// 在现有浏览器中通过 CDP 创建新 Tab，返回新 Tab 的 WebSocket URL
    /// 【Tab 模式用】需先调用 FindExistingBrowserDebugPort 拿到端口
    /// </summary>
    public static async Task<string?> CreateNewTabInExistingBrowserAsync(int port, string url)
    {
        try
        {
            // 1. TcpClient 探活
            using var tcp = new System.Net.Sockets.TcpClient();
            var connectTask = tcp.ConnectAsync(System.Net.IPAddress.Parse("127.0.0.1"), port);
            var timeoutTask = Task.Delay(2000);
            var done = await Task.WhenAny(connectTask, timeoutTask);
            if (done != connectTask || !tcp.Connected)
            {
                CdpLog($"端口 {port} 不可达");
                return null;
            }
            // 2. /json/new 端点创建新 Tab
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var newTabJson = await http.GetStringAsync($"http://127.0.0.1:{port}/json/new?{Uri.EscapeDataString(url)}");
            using var doc = JsonDocument.Parse(newTabJson);
            if (doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsEl))
            {
                var ws = wsEl.GetString();
                if (!string.IsNullOrEmpty(ws))
                {
                    CdpLog($"✓ 现有浏览器创建新 Tab: {ws}");
                    return ws;
                }
            }
            CdpLog($"现有浏览器 /json/new 返回无效: {newTabJson}");
        }
        catch (Exception ex)
        {
            CdpLog($"在现有浏览器开新 Tab 失败: {ex.GetType().Name}: {ex.Message}");
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
        // 启用 Page + Runtime + Input domain
        // Input domain 提供 Input.insertText（模拟真实键盘输入，触发 React 受控组件 onChange）
        await session.SendCommandAsync("Page.enable");
        await session.SendCommandAsync("Runtime.enable");
        await session.SendCommandAsync("Input.setIgnoreInputEvents", new { ignore = false });

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
            else
            {
                // 4. 页面主资源加载完了，但还要等 500ms 让 SPA 加载账套信息（接口、初始化数据）
                //    loadEventFired 只表示 HTML+子资源加载完，不代表框架 bootstrap 完
                CdpLog("等待 500ms 让页面初始化完成（加载账套信息）");
                await Task.Delay(500);
            }
        }
        finally
        {
            session.OnEvent -= OnLoadEvent;
        }

        // 构造填表 JS：用 querySelector 拿元素，挨个赋值并触发 input 事件
        // 关键：React/antd-mobile 框架用了 Object.getOwnPropertyDescriptor 拦截 value 的设置，
        //      必须用原生 HTMLInputElement 的 value setter 才能触发框架的 onChange
        // 调用 1：查找元素（仅为诊断）
        var uSelJs = JsonSerializer.Serialize(usernameSel);
        var pSelJs = JsonSerializer.Serialize(passwordSel);
        var bSelJs = JsonSerializer.Serialize(submitSel);
        var uJs = JsonSerializer.Serialize(username);
        var pJs = JsonSerializer.Serialize(password);

        // 调用 2：使用 Input.insertText 模拟真实键盘输入（React 受控组件可唯一样响应）
        // 思路：focus input → Input.insertText（CDP 原生，会触发完整 input/keyboard 事件链）
        //       返表诊断信息：哪个选择器没找到、点击了什么
        string script = $@"
(function() {{
  function findInAllFrames(sel) {{
    var results = [];
    function deepQuerySelectorAll(root, selector) {{
      var out = [];
      try {{
        var direct = root.querySelectorAll(selector);
        for (var i = 0; i < direct.length; i++) out.push(direct[i]);
      }} catch (e) {{}}
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
  // 透明标记脚本默认不填表，返诊断信息
  if (window.__cdp_skip_fill) {{
    var u = findInAllFrames({uSelJs});
    var p = findInAllFrames({pSelJs});
    var b = findInAllFrames({bSelJs});
    return {{
      url: window.location.href,
      readyState: document.readyState,
      count: {{ u: u.length, p: p.length, b: b.length }}
    }};
  }}
  // 真正填表脚本：focus + native value setter + 返按钮坐标 + 调用 React onClick handler
  // 重点：React 内部不依赖 onClick 事件，而是从 props 里取。
  // 为了避免 React 事件委托拦截 mouse/click，我们直接从 React fiber 里拿 onClick 直接调用。
  function setVal(el, val) {{
    el.focus();
    el.setSelectionRange(0, el.value?.length || 0);
    var proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
    var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, val);
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }}
  // 重要补充：Ant Design Mobile / React 17+ 还可能从 native input setter 里读 value，
  // 上面 setVal 可能还不够。需要额外发 keyboardEvent（keydown/keypress/keyup/input）。
  function simulateTyping(el, val) {{
    el.focus();
    el.setSelectionRange(0, el.value?.length || 0);
    // 退格删除现有内容
    for (var i = 0; i < (el.value?.length || 0); i++) {{
      el.dispatchEvent(new KeyboardEvent('keydown', {{ key: 'Backspace', code: 'Backspace', keyCode: 8, bubbles: true }}));
    }}
    el.value = '';
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    // 逐字发送 keydown + keypress + input + keyup
    for (var i = 0; i < val.length; i++) {{
      var ch = val.charAt(i);
      el.dispatchEvent(new KeyboardEvent('keydown', {{ key: ch, code: 'Key' + ch.toUpperCase(), keyCode: ch.charCodeAt(0), bubbles: true }}));
      el.dispatchEvent(new KeyboardEvent('keypress', {{ key: ch, keyCode: ch.charCodeAt(0), bubbles: true }}));
      var proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
      var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
      var current = el.value;
      setter.call(el, current + ch);
      el.dispatchEvent(new Event('input', {{ bubbles: true }}));
      el.dispatchEvent(new KeyboardEvent('keyup', {{ key: ch, code: 'Key' + ch.toUpperCase(), keyCode: ch.charCodeAt(0), bubbles: true }}));
    }}
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }}
  // 从 React fiber 拿 onClick props key（react 16+ 是 memoizedProps，17+ 也是 props）
  function getReactProps(el) {{
    var key = Object.keys(el).find(function(k) {{
      return k.startsWith('__reactProps$') || k.startsWith('__reactInternalInstance$') || k.startsWith('__reactFiber$');
    }});
    return key ? el[key] : null;
  }}
  var uMatches = findInAllFrames({uSelJs});
  var pMatches = findInAllFrames({pSelJs});
  var bMatches = findInAllFrames({bSelJs});
  var missing = [];
  if (uMatches.length === 0) missing.push('username');
  if (pMatches.length === 0) missing.push('password');
  if (bMatches.length === 0) missing.push('button');
  if (uMatches.length > 0 && pMatches.length > 0 && bMatches.length > 0) {{
    // 重点：JS 事件置入 React state 不一定生效（提交时拿到空值），
    // 后面 C# 侧会用 Input.insertText（CDP 原生）重发一次，那才是唯一可靠的方式。
    setVal(uMatches[0], {uJs});
    setVal(pMatches[0], {pJs});
    var btn = bMatches[0];
    var rect = btn.getBoundingClientRect();
    // 检查 React props 里有没有 onClick（React 16+ 都在 __reactProps$xxx 上）
    var props = getReactProps(btn);
    var hasReactOnClick = !!(props && typeof props.onClick === 'function');
    return {{
      ok: true,
      uVal: uMatches[0].value,
      pVal: pMatches[0].value,
      count: {{ u: uMatches.length, p: pMatches.length, b: bMatches.length }},
      // 按钮中心坐标（视口坐标）
      btnX: rect.left + rect.width / 2,
      btnY: rect.top + rect.height / 2,
      // 按钮外层文本
      btnText: (btn.innerText || btn.textContent || '').trim().substring(0, 20),
      // 按钮是否在视口内
      inViewport: rect.top >= 0 && rect.bottom <= window.innerHeight && rect.left >= 0 && rect.right <= window.innerWidth,
      // React props 检测
      hasReactOnClick: hasReactOnClick,
      btnTag: btn.tagName,
      btnClass: btn.className
    }};
  }}
  return {{
    ok: false,
    reason: 'not_found',
    missing: missing,
    count: {{ u: uMatches.length, p: pMatches.length, b: bMatches.length }},
    url: window.location.href
  }};
}})()
";

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int attempt = 0;
        bool filled = false;
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
                        // 拿按钮中心坐标，用 Input.dispatchMouseEvent 真实鼠标点击
                        // （React 17+ 事件委托需要真实鼠标事件才能触发 onClick）
                        if (valueEl.TryGetProperty("btnX", out var btnXEl) &&
                            valueEl.TryGetProperty("btnY", out var btnYEl))
                        {
                            double btnX = btnXEl.GetDouble();
                            double btnY = btnYEl.GetDouble();
                            string btnText = valueEl.TryGetProperty("btnText", out var btEl) ? btEl.GetString() ?? "" : "";
                            bool inViewport = valueEl.TryGetProperty("inViewport", out var ivEl) && ivEl.ValueKind == JsonValueKind.True;
                            bool hasReactOnClick = valueEl.TryGetProperty("hasReactOnClick", out var hocEl) && hocEl.ValueKind == JsonValueKind.True;
                            string btnTag = valueEl.TryGetProperty("btnTag", out var tagEl) ? tagEl.GetString() ?? "" : "";
                            string btnClass = valueEl.TryGetProperty("btnClass", out var clsEl) ? clsEl.GetString() ?? "" : "";
                            CdpLog($"按钮: 位置({btnX:F0},{btnY:F0}) tag={btnTag} class={btnClass} 文本=\"{btnText}\"");
                            CdpLog($"      在视口内={inViewport} 有React onClick={hasReactOnClick}");
                            // 0. 【重要】先用 CDP Input.insertText 重填（JS 填的不一定生效）
                            //    思路：JS focus input → C# 发 Input.insertText → 触发真实 input 事件 → React state 同步
                            CdpLog("=== 阶段 0：用 Input.insertText 重填账号（CDP 原生键盘输入） ===");
                            await session.SendCommandAsync("Runtime.evaluate", new { expression = $"document.querySelector({uSelJs})?.focus(); document.querySelector({uSelJs})?.select();" });
                            await Task.Delay(100);
                            await session.SendCommandAsync("Input.insertText", new { text = username });
                            await Task.Delay(200);
                            CdpLog($"=== 阶段 0：用 Input.insertText 重填密码 ===");
                            await session.SendCommandAsync("Runtime.evaluate", new { expression = $"document.querySelector({pSelJs})?.focus(); document.querySelector({pSelJs})?.select();" });
                            await Task.Delay(100);
                            await session.SendCommandAsync("Input.insertText", new { text = password });
                            await Task.Delay(300);
                            // 验证：拿一下当前 uVal
                            var verifyResult = await session.EvaluateAsync($@"JSON.stringify({{u: document.querySelector({uSelJs})?.value, p: document.querySelector({pSelJs})?.value}})");
                            CdpLog($"填表后验证: {verifyResult}");
                            // 1. 如果不在视口内，先滚动到可见
                            if (!inViewport)
                            {
                                CdpLog("按钮不在视口内，滚动到可见");
                                await session.SendCommandAsync("Runtime.evaluate", new { expression = $"document.querySelector({bSelJs})?.scrollIntoView({{block:'center'}});" });
                                await Task.Delay(200);
                            }
                            // 2. 送完整点击事件链：touch + mouse + pointer（antd-mobile 是移动端 UI 可能听 touch）
                            // touchStart
                            await session.SendCommandAsync("Input.dispatchTouchEvent", new
                            {
                                type = "touchStart",
                                touchPoints = new[] { new { x = btnX, y = btnY, id = 1 } }
                            });
                            await Task.Delay(50);
                            // touchEnd
                            await session.SendCommandAsync("Input.dispatchTouchEvent", new
                            {
                                type = "touchEnd",
                                touchPoints = new object[] { }
                            });
                            await Task.Delay(50);
                            // 鼠标移动
                            await session.SendCommandAsync("Input.dispatchMouseEvent", new
                            {
                                type = "mouseMoved",
                                x = btnX,
                                y = btnY
                            });
                            // 鼠标按下
                            await session.SendCommandAsync("Input.dispatchMouseEvent", new
                            {
                                type = "mousePressed",
                                x = btnX,
                                y = btnY,
                                button = "left",
                                clickCount = 1
                            });
                            // 鼠标抬起
                            await session.SendCommandAsync("Input.dispatchMouseEvent", new
                            {
                                type = "mouseReleased",
                                x = btnX,
                                y = btnY,
                                button = "left",
                                clickCount = 1
                            });
                            CdpLog("✓ 已发送 touch + mouse + pointer 完整事件链");
                            // 3. 靠底：直接调用 React onClick handler
                            if (hasReactOnClick)
                            {
                                CdpLog("React onClick handler 存在，直接调用");
                                var jsCallOnClick = $@"
(function() {{
  var btn = document.querySelector({bSelJs});
  if (!btn) return 'no btn';
  var key = Object.keys(btn).find(function(k) {{ return k.startsWith('__reactProps$') || k.startsWith('__reactInternalInstance$'); }});
  if (!key) return 'no react key';
  var props = btn[key];
  if (props && typeof props.onClick === 'function') {{
    try {{ props.onClick({{ preventDefault: function(){{}}, stopPropagation: function(){{}} }}); return 'onClick called'; }}
    catch (e) {{ return 'onClick err: ' + e.message; }}
  }}
  return 'no onClick';
}})()";
                                var callResult = await session.EvaluateAsync(jsCallOnClick);
                                CdpLog($"React onClick 调用结果: {callResult}");
                            }
                            await Task.Delay(1500);
                            // 验证：检查 URL 是否变了
                            var urlResult = await session.EvaluateAsync("window.location.href");
                            string afterUrl = urlResult.GetProperty("result").GetProperty("value").GetString() ?? "";
                            CdpLog($"点击后 URL: {afterUrl}");
                        }
                        else
                        {
                            CdpLog("⚠️ 未拿到按钮坐标，回退到 element.click()");
                        }
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
