using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace A3Tools.Services;

/// <summary>
/// Win32 窗体自动登录助手
/// 通过 FindWindow + EnumChildWindows 定位控件 + WM_SETTEXT 填值 + BM_CLICK 触发按钮
/// 用于 A3 客户端（君则A3.exe）和集成开发工具（君则A3集成开发工具.exe）的自动登录
/// </summary>
public static class Win32AutoLoginHelper
{
    // Win32 自动登录会抢前台窗口 + 发送键盘事件；三端同时启动时必须串行化，避免互相抢焦点导致客户端/开发工具卡住
    private static readonly SemaphoreSlim LoginSemaphore = new(1, 1);

    #region Win32 API 声明

    private const int WM_SETTEXT = 0x000C;
    private const int WM_GETTEXT = 0x000D;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int BM_CLICK = 0x00F5;
    private const int CB_SELECTSTRING = 0x014D;
    private const int CB_GETCOUNT = 0x0146;
    private const int CB_GETLBTEXT = 0x0148;
    private const int CB_SETCURSEL = 0x014E;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int GWL_STYLE = -16;
    private const int BS_DEFPUSHBUTTON = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const byte VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // ShowWindow 命令
    private const int SW_SHOW = 5;

    #endregion

    #region 进程运行检测 + 拉到前台

    /// <summary>
    /// 判断指定进程名是否已在运行（进程名不带 .exe 后缀）
    /// </summary>
    public static bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        var processes = Process.GetProcessesByName(processName);
        return processes.Length > 0;
    }

    /// <summary>
    /// 按 PID 查找进程的主窗口句柄（优先 MainWindowHandle，拿不到时 EnumWindows 兜底）
    /// </summary>
    public static bool TryGetProcessWindow(int processId, out IntPtr hWnd)
    {
        hWnd = IntPtr.Zero;
        try
        {
            var proc = Process.GetProcessById(processId);
            if (proc == null || proc.HasExited) return false;

            IntPtr found = proc.MainWindowHandle;

            // 拿不到主窗口句柄时，枚举所有顶层窗口找该进程的窗口
            if (found == IntPtr.Zero)
            {
                EnumWindows((h, _) =>
                {
                    GetWindowThreadProcessId(h, out uint pid);
                    if (pid == (uint)processId)
                    {
                        found = h;
                        return false; // 停止枚举
                    }
                    return true;
                }, IntPtr.Zero);
            }

            hWnd = found;
            return hWnd != IntPtr.Zero;
        }
        catch
        {
            hWnd = IntPtr.Zero;
            return false;
        }
    }

    /// <summary>
    /// 按 PID 把进程的主窗口拉到前台。流程：找主窗口 → 最小化恢复 → AllowSetForegroundWindow → SetForegroundWindow。
    /// 用于「按账套切前台」场景（不同账套 A3 客户端进程名相同，必须按 PID 区分）。
    /// </summary>
    /// <param name="processId">目标进程 ID</param>
    /// <returns>是否成功拉到前台</returns>
    public static bool BringProcessByIdToFront(int processId)
    {
        if (!TryGetProcessWindow(processId, out IntPtr hWnd)) return false;
        return BringWindowToFront(hWnd);
    }

    /// <summary>
    /// 把指定窗口句柄拉到前台（最小化恢复 + 抢焦点三件套）。
    /// </summary>
    private static bool BringWindowToFront(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);
            else
                ShowWindow(hWnd, SW_SHOW);

            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                AllowSetForegroundWindow((int)fgPid);
            }
            SetForegroundWindow(hWnd);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将指定进程名的首个运行实例的主窗口拉到前台。
    /// 流程：找主窗口句柄 → 最小化则恢复 → AllowSetForegroundWindow → SetForegroundWindow。
    /// </summary>
    /// <param name="processName">进程名（不带 .exe）</param>
    /// <param name="processId">输出找到的进程 ID</param>
    /// <returns>是否成功拉到前台</returns>
    public static bool BringProcessToFront(string processName, out int processId)
    {
        processId = 0;
        if (string.IsNullOrEmpty(processName)) return false;

        Process?[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return false;
        }

        if (processes.Length == 0) return false;
        var proc = processes[0];
        if (proc == null) return false;

        processId = proc.Id;

        // 1. 先尝试 MainWindowHandle
        IntPtr hWnd = proc.MainWindowHandle;

        // 2. 拿不到主窗口句柄时，枚举所有顶层窗口找该进程的窗口
        if (hWnd == IntPtr.Zero)
        {
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == proc.Id)
                {
                    hWnd = h;
                    return false; // 停止枚举
                }
                return true;
            }, IntPtr.Zero);
        }

        if (hWnd == IntPtr.Zero) return false;
        return BringWindowToFront(hWnd);
    }

    #endregion

    /// <summary>
    /// Win32 自动登录调试日志：仅输出到 VS Debug 窗口，不再写入 win32-login.log 文件。
    /// </summary>
    public static void WinLog(string msg)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
        Debug.WriteLine(line);
    }

    /// <summary>
    /// 异步启动 + 自动登录
    /// 启动后等待登录窗出现，定位控件填表，点确定按钮
    /// </summary>
    /// <param name="exePath">要启动的 exe 完整路径</param>
    /// <param name="windowTitleContains">登录窗标题中必须包含的文本（如 "君则A3" / "A3IDE"）</param>
    /// <param name="accountName">账套名称（账套下拉框要选中的项）</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="timeoutMs">等待登录窗的超时时间（毫秒）</param>
    /// <returns>启动的进程，启动失败返回 null</returns>
    public static Process? LaunchAndAutoLogin(
        string exePath,
        string windowTitleContains,
        string? accountName,
        string username,
        string password,
        int timeoutMs = 30000)
    {
        if (!File.Exists(exePath))
        {
            WinLog($"✗ exe 不存在：{exePath}");
            return null;
        }

        // 启动进程
        Process? proc;
        try
        {
            string workDir = Path.GetDirectoryName(exePath) ?? "";
            proc = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WinLog($"✗ 启动 {exePath} 失败：{ex.Message}");
            return null;
        }

        if (proc == null)
        {
            WinLog($"✗ 启动 {exePath} 失败：Process.Start 返回 null");
            return null;
        }

        WinLog($"▶ 启动 {Path.GetFileName(exePath)} PID={proc.Id}");

        // 异步等待 + 填表（不阻塞调用方）
        _ = Task.Run(async () =>
        {
            bool lockTaken = false;
            try
            {
                await LoginSemaphore.WaitAsync();
                lockTaken = true;

                bool ok = await TryAutoLoginAsync(proc, new[] { windowTitleContains }, accountName, username, password, timeoutMs);
                if (ok)
                    WinLog($"✓ 自动登录完成：{Path.GetFileName(exePath)}");
                else
                    WinLog($"⚠ 自动登录超时/失败，已交由用户手动操作：{Path.GetFileName(exePath)}");
            }
            catch (Exception ex)
            {
                WinLog($"✗ 自动登录异常：{ex.Message}");
            }
            finally
            {
                if (lockTaken) LoginSemaphore.Release();
            }
        });

        return proc;
    }

    /// <summary>
    /// 启动集成开发工具并两步自动登录：
    /// 步骤 1：等客户端登录窗 → 填 ServerUsername + ServerPassword → 等消失
    /// 步骤 2：等开发工具登录窗 → 仅填 DevToolsPassword（开发账号默认记住）→ 等消失
    /// </summary>
    /// <param name="exePath">集成开发工具 exe 完整路径</param>
    /// <param name="windowTitleContains">登录窗标题包含文本（客户端和开发工具登录页同标题，如 "君则A3"）</param>
    /// <param name="accountName">账套名称（步骤 1 用，null=不选账套）</param>
    /// <param name="clientUsername">客户端用户名（步骤 1 用）</param>
    /// <param name="clientPassword">客户端密码（步骤 1 用）</param>
    /// <param name="devPassword">开发工具密码（步骤 2 用，开发账号默认记住不需要填）</param>
    /// <param name="stepTimeoutMs">每步等待登录窗的超时（毫秒）</param>
    /// <param name="transitionDelayMs">步骤 1 完成后等多久看步骤 2 窗体（毫秒）</param>
    public static Process? LaunchAndAutoLoginDevTools(
        string exePath,
        string windowTitleContains,
        string? accountName,
        string clientUsername,
        string clientPassword,
        string devPassword,
        int stepTimeoutMs = 30000,
        int transitionDelayMs = 1000)
    {
        if (!File.Exists(exePath))
        {
            WinLog($"✗ exe 不存在：{exePath}");
            return null;
        }

        string workDir = Path.GetDirectoryName(exePath) ?? "";
        Process? proc;
        try
        {
            proc = Process.Start(new ProcessStartInfo { FileName = exePath, WorkingDirectory = workDir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WinLog($"✗ 启动 {exePath} 失败：{ex.Message}");
            return null;
        }

        if (proc == null)
        {
            WinLog($"✗ 启动 {exePath} 失败：Process.Start 返回 null");
            return null;
        }

        WinLog($"▶ 启动 {Path.GetFileName(exePath)} PID={proc.Id}（两步登录）");

        _ = Task.Run(async () =>
        {
            bool lockTaken = false;
            try
            {
                await LoginSemaphore.WaitAsync();
                lockTaken = true;

                // 步骤 1 用 「君则A3」匹配客户端登录窗
                // 步骤 2 用 「IDE授权登录」匹配开发工具 IDE 授权窗
                var step1Keywords = new[] { "君则A3" };
                var step2Keywords = new[] { "IDE授权登录", "IDE授权" };

                // ===== 步骤 1：客户端登录 =====
                WinLog($"  ▶ 步骤 1/2 客户端登录");
                bool step1 = await TryAutoLoginAsync(proc, step1Keywords, accountName, clientUsername, clientPassword, stepTimeoutMs);
                if (!step1)
                {
                    WinLog($"  ✗ 步骤 1 失败，中止");
                    return;
                }
                WinLog($"  ✓ 步骤 1 完成");

                // ===== 过渡：等开发工具登录页出现 =====
                await Task.Delay(transitionDelayMs);

                // ===== 步骤 2：开发工具登录（只填密码，用户名默认记住）=====
                // 开发工具需要「三重确认」：发 3 次 Enter，间隔 500ms
                WinLog($"  ▶ 步骤 2/2 开发工具登录（Enter 键发 3 次×间隔 500ms）");
                bool step2 = await TryAutoLoginAsync(proc, step2Keywords, null, null, devPassword, stepTimeoutMs,
                    enterRepeatCount: 3, enterRepeatDelayMs: 500);
                if (!step2)
                {
                    WinLog($"  ✗ 步骤 2 失败");
                    return;
                }
                WinLog($"  ✓ 步骤 2 完成");
                WinLog($"✓ 两步登录全部完成：{Path.GetFileName(exePath)}");
            }
            catch (Exception ex)
            {
                WinLog($"✗ 两步登录异常：{ex.Message}");
            }
            finally
            {
                if (lockTaken) LoginSemaphore.Release();
            }
        });

        return proc;
    }

    /// <summary>
    /// 尝试自动登录：轮询找登录窗 → 定位控件 → 填表 → 点确定
    /// </summary>
    /// <param name="titleKeywords">title 多关键字，任一匹配即作为候选窗</param>
    /// <param name="enterRepeatCount">Enter 键发送次数（默认 1；开发工具 IDE 授权需 2 次「双重确认」）</param>
    /// <param name="enterRepeatDelayMs">两次 Enter 之间的间隔毫秒数（默认 0）</param>
    private static async Task<bool> TryAutoLoginAsync(
        Process proc,
        string[] titleKeywords,
        string? accountName,
        string username,
        string password,
        int timeoutMs,
        int enterRepeatCount = 1,
        int enterRepeatDelayMs = 0)
    {
        var sw = Stopwatch.StartNew();
        IntPtr loginHwnd = IntPtr.Zero;

        // 阶段 1：等登录窗出现
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (proc.HasExited)
                {
                    WinLog($"  ✗ 进程已退出（PID={proc.Id}）");
                    return false;
                }
            }
            catch { }

            loginHwnd = FindLoginWindow(proc.Id, titleKeywords);
            if (loginHwnd != IntPtr.Zero)
            {
                WinLog($"  ✓ 找到登录窗（耗时 {sw.ElapsedMilliseconds}ms）");
                break;
            }

            await Task.Delay(300);
        }

        if (loginHwnd == IntPtr.Zero)
        {
            string desc = string.Join(" | ", titleKeywords);
            WinLog($"  ✗ 等待登录窗超时（{timeoutMs}ms，关键词：{desc}）");
            return false;
        }

        // 阶段 2：等控件加载完（再等 800ms 让窗体完全就绪）
        await Task.Delay(500);

        // 阶段 3：定位 + 填表
        return await FillLoginFormAsync(loginHwnd, accountName, username, password, clickSubmit: true, enterRepeatCount: enterRepeatCount, enterRepeatDelayMs: enterRepeatDelayMs);
    }

    /// <summary>
    /// 在指定进程的窗口中找登录窗
    /// 匹配规则：进程ID 一致 + 标题包含 windowTitleContains + 顶层可见窗口
    /// 关键优化：候选窗按 EDIT 子控件数量降序（登录窗必须有输入框，主窗没有）
    /// </summary>
    private static IntPtr FindLoginWindow(int processId, string titleContains)
    {
        // 候选窗列表（记录每个候选的子控件统计）
        var candidates = new List<(IntPtr hWnd, string title, int editCount, int buttonCount, int comboCount)>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != (uint)processId) return true;

            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            // 标题包含指定关键字
            if (title.Contains(titleContains))
            {
                // 统计子窗体中的 EDIT / BUTTON / COMBO 数量
                int edits = 0, buttons = 0, combos = 0;
                EnumChildWindows(hWnd, (child, _) =>
                {
                    var cls = GetClass(child);
                    if (cls.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0) edits++;
                    else if (cls.IndexOf("BUTTON", StringComparison.OrdinalIgnoreCase) >= 0) buttons++;
                    else if (cls.IndexOf("COMBO", StringComparison.OrdinalIgnoreCase) >= 0) combos++;
                    return true;
                }, IntPtr.Zero);

                candidates.Add((hWnd, title, edits, buttons, combos));
            }

            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            // 没找到任何候选，留给外层继续轮询
            return IntPtr.Zero;
        }

        // 优先选有 EDIT 控件的窗（登录窗特征：必须有用户名/密码输入框）
        // 按 EDIT 数量降序，相同 EDIT 时按 COMBO 数量降序（账套下拉也是登录窗特征）
        var withEdits = candidates
            .Where(c => c.editCount > 0)
            .OrderByDescending(c => c.editCount + c.comboCount)
            .ToList();

        if (withEdits.Count > 0)
        {
            var best = withEdits.First();
            if (candidates.Count > 1)
                WinLog($"  → {candidates.Count} 个候选中选中：'{best.title}'（EDIT={best.editCount} COMBO={best.comboCount}）");
            return best.hWnd;
        }

        WinLog($"  ⚠ {candidates.Count} 个候选窗均无 EDIT，继续等待...");
        return IntPtr.Zero;
    }

    /// <summary>
    /// 多关键字重载：title 任一匹配即加入候选
    /// </summary>
    private static IntPtr FindLoginWindow(int processId, string[] titleContainsList)
    {
        if (titleContainsList == null || titleContainsList.Length == 0)
            return IntPtr.Zero;

        // 候选窗列表
        var candidates = new List<(IntPtr hWnd, string title, int editCount, int buttonCount, int comboCount)>();
        string titleFilterDesc = string.Join(" | ", titleContainsList);

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != (uint)processId) return true;

            int len = GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            // 任一关键字命中
            bool matched = false;
            foreach (var kw in titleContainsList)
            {
                if (!string.IsNullOrEmpty(kw) && title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched) return true;

            int edits = 0, buttons = 0, combos = 0;
            EnumChildWindows(hWnd, (child, _) =>
            {
                var cls = GetClass(child);
                if (cls.IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0) edits++;
                else if (cls.IndexOf("BUTTON", StringComparison.OrdinalIgnoreCase) >= 0) buttons++;
                else if (cls.IndexOf("COMBO", StringComparison.OrdinalIgnoreCase) >= 0) combos++;
                return true;
            }, IntPtr.Zero);

            candidates.Add((hWnd, title, edits, buttons, combos));
            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
            return IntPtr.Zero;

        // 按 EDIT + COMBO 数量降序选最优
        var withEdits = candidates
            .Where(c => c.editCount > 0)
            .OrderByDescending(c => c.editCount + c.comboCount)
            .ToList();

        if (withEdits.Count > 0)
        {
            var best = withEdits.First();
            if (candidates.Count > 1)
                WinLog($"  → {candidates.Count} 个候选中选中：'{best.title}'（EDIT={best.editCount} COMBO={best.comboCount}）");
            return best.hWnd;
        }

        WinLog($"  ⚠ {candidates.Count} 个候选窗均无 EDIT，继续等待...");
        return IntPtr.Zero;
    }

    /// <summary>
    /// 填表核心逻辑
    /// 1. 枚举所有子控件
    /// 2. 按"用户名"/"密码"/"确定"/"帐套选择" Label 找对应 Edit
    /// 3. WM_SETTEXT 填值
    /// 4. 找"确定"按钮 BM_CLICK
    /// </summary>
    private static async Task<bool> FillLoginFormAsync(IntPtr loginHwnd, string? accountName, string username, string password)
    {
        return await FillLoginFormAsync(loginHwnd, accountName, username, password, clickSubmit: true);
    }

    /// <summary>
    /// <summary>
    /// 填表核心逻辑（可选择是否点确定，用于调试）
    /// </summary>
    /// <param name="enterRepeatCount">Enter 键发送次数（默认 1；开发工具 IDE 授权需 2 次「双重确认」）</param>
    /// <param name="enterRepeatDelayMs">两次 Enter 之间的间隔毫秒数（默认 0）</param>
    public static async Task<bool> FillLoginFormAsync(IntPtr loginHwnd, string? accountName, string username, string password, bool clickSubmit, int enterRepeatCount = 1, int enterRepeatDelayMs = 0)
    {
        // 先把登录窗拉到前台（避免后续真实鼠标点击路由到其他窗体）
        SetForegroundWindow(loginHwnd);
        await Task.Delay(200);

        // 递归枚举整个控件树（含嵌套层级），解决 GroupBox/Panel 容器导致同父匹配失败的问题
        var allControls = EnumerateAllControls(loginHwnd);
        int editCount = allControls.Count(c => c.ClassName.Contains("EDIT"));
        int buttonCount = allControls.Count(c => IsButtonClass(c.ClassName));
        int comboCount = allControls.Count(c => c.ClassName.Contains("COMBO"));
        WinLog($"  控件：{allControls.Count} 个（EDIT={editCount} BUTTON={buttonCount} COMBO={comboCount}）");
        // 详细控件树（调试时取消注释启用）
        // LogControlTree(allControls);

        // 1) 选账套下拉
        if (!string.IsNullOrEmpty(accountName))
        {
            // 找包含 "帐套" / "账套" 的 Label
            var comboLabel = allControls.FirstOrDefault(c =>
                c.ClassName.Contains("STATIC") &&
                (c.Text.Contains("帐套") || c.Text.Contains("账套")));

            IntPtr comboHwnd = IntPtr.Zero;
            if (comboLabel.Hwnd != IntPtr.Zero)
            {
                // 沿父链向上找同辈 COMBO（Label 和下拉可能隔一层 GroupBox）
                comboHwnd = FindCompanionControl(comboLabel, allControls, "COMBO");
            }

            if (comboHwnd != IntPtr.Zero)
            {
                SelectComboByText(comboHwnd, accountName);
                await Task.Delay(200);
            }
            else
            {
                // Fallback：账套下拉可能没用 COMBO 类名（BUTTON/DROPDOWN/其他自定义类）
                // 兑底取第一个 COMBOX 控件
                var firstCombo = allControls.FirstOrDefault(c => c.ClassName.Contains("COMBO"));
                if (firstCombo.Hwnd != IntPtr.Zero)
                {
                    SelectComboByText(firstCombo.Hwnd, accountName);
                    WinLog($"  → 兑底：用了第一个 COMBO 当账套");
                    await Task.Delay(200);
                }
                else
                {
                    WinLog($"  ⚠ 未找到任何 COMBO 控件");
                }
            }
        }
        else
        {
            WinLog($"  → 跳过账套选择（accountName 为空）");
        }

        // 2) 填用户名（仅当 username 非空时才操作，避免覆盖窗体已记住的开发账号）
        if (!string.IsNullOrEmpty(username))
        {
            var userLabel = allControls.FirstOrDefault(c =>
                c.ClassName.Contains("STATIC") && c.Text == "用户名");
            IntPtr userEditHwnd = IntPtr.Zero;
            if (userLabel.Hwnd != IntPtr.Zero)
            {
                userEditHwnd = FindCompanionControl(userLabel, allControls, "EDIT");
            }

            if (userEditHwnd != IntPtr.Zero)
            {
                SetEditText(userEditHwnd, username);
                WinLog($"  ✓ 已填用户名：{username}");
            }
            else
            {
                WinLog($"  ⚠ 未找到【用户名】Label 或对应的 EDIT 控件（username 已提供）");
            }
        }
        else
        {
            WinLog($"  → 跳过用户名（username 为空，保留窗体已记住的开发账号）");
        }

        await Task.Delay(200);

        // 3) 填密码（仅当 password 非空时才操作）
        if (!string.IsNullOrEmpty(password))
        {
            var pwdLabel = allControls.FirstOrDefault(c =>
                c.ClassName.Contains("STATIC") && c.Text == "密码");
            IntPtr pwdEditHwnd = IntPtr.Zero;
            if (pwdLabel.Hwnd != IntPtr.Zero)
            {
                pwdEditHwnd = FindCompanionControl(pwdLabel, allControls, "EDIT");
            }

            if (pwdEditHwnd != IntPtr.Zero)
            {
                SetEditText(pwdEditHwnd, password);
                WinLog($"  ✓ 已填密码（长度={password.Length}）");
            }
            else
            {
                WinLog($"  ⚠ 未找到【密码】Label 或对应的 EDIT 控件");
            }
        }
        else
        {
            WinLog($"  → 跳过密码（password 为空）");
        }

        await Task.Delay(200);

        // 4) 点"确定"按钮 - 多策略降级匹配（解决按钮文字不是"确定"的问题）
        if (clickSubmit)
        {
            ControlInfo? okBtn = null;
            string matchStrategy = "";

            // 策略 A：Text 精确匹配"确定"
            okBtn = allControls.FirstOrDefault(c =>
                IsButtonClass(c.ClassName) && c.Text == "确定");
            if (okBtn != null) matchStrategy = "精确";

            // 策略 B：Text 包含关键字（确定/登录/进入/提交/OK/Login/Sign 等）
            if (okBtn == null)
            {
                string[] keywords = { "确定", "登录", "进入", "提交", "登 录", "OK", "Login", "Sign In", "Sign in", "Sign" };
                okBtn = allControls.FirstOrDefault(c =>
                    IsButtonClass(c.ClassName) &&
                    keywords.Any(k => c.Text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                if (okBtn != null) matchStrategy = "关键字";
            }

            // 策略 C：默认按钮（BS_DEFPUSHBUTTON 样式，回车自动触发的那个）
            if (okBtn == null)
            {
                okBtn = allControls.FirstOrDefault(c =>
                {
                    if (!IsButtonClass(c.ClassName)) return false;
                    int style = GetWindowLong(c.Hwnd, GWL_STYLE);
                    return (style & BS_DEFPUSHBUTTON) == BS_DEFPUSHBUTTON;
                });
                if (okBtn != null) matchStrategy = "默认按钮";
            }

            // 策略 D：Tab Order 最后一个按钮（兜底，主操作按钮一般 Tab Order 末尾）
            if (okBtn == null)
            {
                okBtn = allControls.LastOrDefault(c => IsButtonClass(c.ClassName));
                if (okBtn != null) matchStrategy = "末尾兜底";
            }

            if (okBtn != null && okBtn.Hwnd != IntPtr.Zero)
            {
                // 阶段 1：优先 SendInput Enter 键（不抢鼠标、不怕遮挡）
                // WinForms 默认按钮 (BS_DEFPUSHBUTTON) 按 Enter 自动触发 Click 事件
                await SendEnterKeyAsync(loginHwnd, enterRepeatCount, enterRepeatDelayMs);

                // 阶段 2：每秒检测一次登录窗是否还在，最多 8 次（共 8 秒等待 Enter 键生效）
                bool disappeared = false;
                const int detectCount = 8;
                for (int i = 1; i <= detectCount; i++)
                {
                    await Task.Delay(1000);
                    bool stillThere = IsWindow(loginHwnd) && IsWindowVisible(loginHwnd);
                    if (!stillThere) { disappeared = true; break; }
                }
                if (disappeared)
                {
                    WinLog($"  ✓ Enter 键生效，登录窗已消失（按钮：'{okBtn.Text}'）");
                    return true;
                }

                // 阶段 3：8 次检测后登录窗仍在，真实鼠标兜底（抢鼠标换稳定）
                WinLog($"  → Enter 键超时，真实鼠标点击兜底");
                ClickButton(okBtn.Hwnd);
                WinLog($"  ✓ 已点击按钮 '{okBtn.Text}' [{matchStrategy}]");
                return true;
            }
            else
            {
                WinLog($"  ⚠ 4 种策略都没找到可点击的 BUTTON");
                return false;
            }
        }
        else
        {
            WinLog($"  → 跳过点击【确定】（clickSubmit=false）");
            return true;
        }
    }

    /// <summary>
    /// 递归枚举窗体内所有子控件（含嵌套层级），同时记录深度
    /// 解决 EnumChildWindows 只枚举一级、控件嵌在 GroupBox/Panel 里时匹配不到的问题
    /// </summary>
    private static List<ControlInfo> EnumerateAllControls(IntPtr rootHwnd)
    {
        var all = new List<ControlInfo>();
        Recurse(rootHwnd, 0);
        return all;

        void Recurse(IntPtr parent, int level)
        {
            EnumChildWindows(parent, (hWnd, _) =>
            {
                var info = new ControlInfo
                {
                    Hwnd = hWnd,
                    ClassName = GetClass(hWnd),
                    Text = GetText(hWnd),
                    Parent = parent,
                    Level = level
                };
                all.Add(info);
                // 递归枚举子窗体的子窗体（深度 +1）
                Recurse(hWnd, level + 1);
                return true;
            }, IntPtr.Zero);
        }
    }

    /// <summary>
    /// 把完整控件树按层级打印到日志，方便排查窗体结构
    /// </summary>
    private static void LogControlTree(List<ControlInfo> controls)
    {
        // 按 Level 排序后打印（虽然枚举顺序天然按 BFS，递归后是 DFS，但已经够看）
        foreach (var c in controls)
        {
            string indent = new string(' ', Math.Min(c.Level, 8) * 2);
            string textPreview = string.IsNullOrEmpty(c.Text) ? "(空)" : c.Text;
            // 文本太长截断
            if (textPreview.Length > 40) textPreview = textPreview.Substring(0, 40) + "...";
            WinLog($"    {indent}[L{c.Level}] 0x{c.Hwnd.ToInt64():X8} class='{c.ClassName}' text='{textPreview}'");
        }
    }

    /// <summary>
    /// 从给定 Label 控件出发，沿父链向上找同辈的指定类名控件
    /// 优先同父（最常见），找不到就上一级（祖父）、再找不到就曾祖父...最多 4 层
    /// 解决"Label 在外层、输入框在 GroupBox 内"导致同父匹配失败的问题
    /// </summary>
    private static IntPtr FindCompanionControl(ControlInfo label, List<ControlInfo> all, string classFragment)
    {
        IntPtr currentAncestor = label.Parent;
        IntPtr lastAncestor = IntPtr.Zero;

        // 最多向上找 4 层（Label → 同父 → 父 → 祖父 → 曾祖父 → 曾曾祖父）
        for (int depth = 0; depth <= 4; depth++)
        {
            if (currentAncestor == IntPtr.Zero || currentAncestor == lastAncestor) break;
            lastAncestor = currentAncestor;

            var found = all.FirstOrDefault(c =>
                c.Parent == currentAncestor &&
                c.ClassName.IndexOf(classFragment, StringComparison.OrdinalIgnoreCase) >= 0);

            if (found != null && found.Hwnd != IntPtr.Zero && found.Hwnd != label.Hwnd)
            {
                if (depth > 0)
                {
                    WinLog($"  → '{classFragment}' 沿父链上溯 {depth} 级找到");
                }
                return found.Hwnd;
            }

            // 找 currentAncestor 的父
            var parentInfo = all.FirstOrDefault(c => c.Hwnd == currentAncestor);
            if (parentInfo == null || parentInfo.Parent == IntPtr.Zero) break;
            currentAncestor = parentInfo.Parent;
        }

        return IntPtr.Zero;
    }

    private static string GetClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetText(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len == 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// 判断控件类名是否为按钮（兼容 Win32 标准按钮、WinForms Button、Delphi TButton）
    /// WinForms Button 实际类名是 "WindowsForms10.Window.b.app.0.xxx"，含 "Window.b" 不含 "BUTTON"
    /// </summary>
    private static bool IsButtonClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;

        // Win32 标准按钮 (Button class) - 大小写不敏感
        if (className.IndexOf("BUTTON", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // WinForms Button：类名包含 "Window.b"（注意是 Window.b 不是 Window.b.button）
        // 例: WindowsForms10.Window.b.app.0.1ca0192_r8_ad1
        if (className.IndexOf("Window.b", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // Delphi TButton
        if (className.IndexOf("TButton", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
    }

    /// <summary>
    /// 去除字符串中所有空白字符（半角空格/全角空格/Tab/换行）
    /// 用于 ComboBox 项匹配前规范化（如 "标准账套8088" vs "标准账套 8088"）
    /// </summary>
    private static string StripWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace(" ", "").Replace("\t", "").Replace("\u3000", "").Replace("\n", "").Replace("\r", "");
    }

    /// <summary>
    /// 通过 WM_SETTEXT 设置 Edit 文本
    /// </summary>
    private static void SetEditText(IntPtr hWnd, string text)
    {
        if (hWnd == IntPtr.Zero) return;
        // 先全选 + 清空
        SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, string.Empty);
        // 再设值
        SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, text ?? string.Empty);
    }

    /// <summary>
    /// 在 ComboBox 中按文本选中（多策略匹配，返回成功标志）
    /// </summary>
    private static bool SelectComboByText(IntPtr hWnd, string text)
    {
        if (hWnd == IntPtr.Zero) return false;
        if (string.IsNullOrEmpty(text)) return false;

        int count = (int)SendMessage(hWnd, CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0)
        {
            WinLog($"  ⚠ ComboBox 是空的（0 项）");
            return false;
        }

        // 收集所有项
        const int BUF_SIZE = 512;
        IntPtr buffer = Marshal.AllocHGlobal(BUF_SIZE * 2);
        var items = new List<string>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                IntPtr len = SendMessage(hWnd, CB_GETLBTEXT, new IntPtr(i), buffer);
                if (len.ToInt64() <= 0) continue;
                string itemText = Marshal.PtrToStringUni(buffer) ?? string.Empty;
                items.Add(itemText);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // 多种匹配策略
        string textNoSpace = StripWhitespace(text);
        for (int i = 0; i < items.Count; i++)
        {
            string itemText = items[i];
            string itemNoSpace = StripWhitespace(itemText);

            // 策略 A：精确匹配
            if (itemText == text) return SelectComboIndex(hWnd, i, itemText, "精确");

            // 策略 B：双向 Contains（处理包含子串的情况）
            if (itemText.Contains(text) || text.Contains(itemText)) return SelectComboIndex(hWnd, i, itemText, "子串");

            // 策略 C：去空格后精确匹配（处理"标准账套8088" vs "标准账套 8088"）
            if (itemNoSpace == textNoSpace) return SelectComboIndex(hWnd, i, itemText, "去空格");

            // 策略 D：去空格后双向 Contains
            if (itemNoSpace.Contains(textNoSpace) || textNoSpace.Contains(itemNoSpace)) return SelectComboIndex(hWnd, i, itemText, "去空格子串");
        }

        // 匹配失败时列出所有项（仅调试场景需要）
        string itemsPreview = items.Count <= 10
            ? string.Join(" | ", items.Select(s => $"'{s}'"))
            : string.Join(" | ", items.Take(10).Select(s => $"'{s}'")) + $" ... (共 {items.Count} 项)";
        WinLog($"  ⚠ ComboBox 未匹配 '{text}'，项列表：{itemsPreview}");
        return false;
    }

    private static bool SelectComboIndex(IntPtr hWnd, int index, string itemText, string strategy)
    {
        SendMessage(hWnd, CB_SETCURSEL, new IntPtr(index), IntPtr.Zero);
        WinLog($"  ✓ 账套选中：'{itemText}' [{strategy}]");
        return true;
    }

    /// <summary>
    /// 模拟点击按钮：优先真实鼠标坐标点击（解决 BM_CLICK 在 WinForms 自定义按钮上不生效的问题）
    /// </summary>
    private static void ClickButton(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        // 优先真实鼠标点击（GetWindowRect + SetCursorPos + mouse_event）
        RECT rect;
        if (GetWindowRect(hWnd, out rect))
        {
            int x = (rect.Left + rect.Right) / 2;
            int y = (rect.Top + rect.Bottom) / 2;

            // 先把鼠标移到按钮上（让按钮获得 hover 状态）
            SetCursorPos(x, y);
            Thread.Sleep(50);

            // 鼠标按下 + 抬起（中间留 80ms 模拟真实点击节奏）
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }
        else
        {
            // Fallback: GetWindowRect 失败时用 BM_CLICK
            SendMessage(hWnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            WinLog($"  ⚠ GetWindowRect 失败，BM_CLICK 兑底");
        }
    }

    /// <summary>
    /// <summary>
    /// 发送 Enter 键（用于触发 WinForms 默认按钮 BS_DEFPUSHBUTTON 的 Click 事件）
    /// 要求焦点在登录窗上 - 提前 SetForegroundWindow + SetFocus 确保路由到登录窗
    /// 优点：不抢鼠标、不移动光标、不怕窗口被遮挡
    /// </summary>
    /// <param name="loginHwnd">登录窗句柄</param>
    /// <param name="repeatCount">发送次数（默认 1；开发工具 IDE 授权需发 2 次「双重确认」）</param>
    /// <param name="repeatDelayMs">两次发送之间的间隔（毫秒，默认 0）</param>
    private static async Task SendEnterKeyAsync(IntPtr loginHwnd, int repeatCount = 1, int repeatDelayMs = 0)
    {
        // 把登录窗拉到前台 + 设焦点（避免 Enter 路由到其他窗体）
        SetForegroundWindow(loginHwnd);
        await Task.Delay(100);
        SetFocus(loginHwnd);
        await Task.Delay(50);

        for (int i = 1; i <= repeatCount; i++)
        {
            // 发送 Enter（key down + key up）
            keybd_event(VK_RETURN, 0, 0, IntPtr.Zero);
            await Task.Delay(50);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, IntPtr.Zero);

            if (i < repeatCount && repeatDelayMs > 0)
                await Task.Delay(repeatDelayMs);
        }
        WinLog($"  → Enter 键 ×{repeatCount}（间隔 {repeatDelayMs}ms）");
    }

    private class ControlInfo
    {
        public IntPtr Hwnd { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public IntPtr Parent { get; set; }
        public int Level { get; set; }  // 嵌套深度（0 = loginHwnd 的直接子控件）
    }
}
