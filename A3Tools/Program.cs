using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using A3Tools.Forms;

namespace A3Tools;

static class Program
{
    // 全局互斥量名字：整个系统范围内唯一，用来判断 A3工具箱 是否已在运行
    // 名字带版本后缀，避免以后重装/升级后造成遗留互斥量误判
    private const string SingleInstanceMutexName = "A3Tools_SingleInstance_Mutex_v1";
    private const string MainWindowTitle = "A3工具箱";

    // 「已在运行」提示框的自动关闭时间（毫秒）
    private const int AlreadyRunningToastDurationMs = 3000;

    private static Mutex? _singleInstanceMutex;

    [STAThread]
    static void Main()
    {
        // 单实例检测：创建命名 Mutex，createdNew=false 说明已有实例在跑
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out bool createdNew);

        if (!createdNew)
        {
            // 已有 A3工具箱 正在运行 → 把那个窗口拉到前台 + 提示后退出
            BringExistingInstanceToFront();
            ShowAlreadyRunningHint();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        finally
        {
            // 正常退出时释放互斥量（异常崩溃 GC 也会释放）
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { /* abandoned */ }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }

    /// <summary>
    /// 查找并激活已运行的 A3工具箱 主窗口。
    /// 使用窗口标题查找（简单可靠；A3工具箱 主窗 Text = "A3工具箱"，托盘隐藏时不变）
    /// </summary>
    private static void BringExistingInstanceToFront()
    {
        try
        {
            IntPtr hWnd = FindWindow(lpClassName: null, lpWindowName: MainWindowTitle);
            if (hWnd == IntPtr.Zero)
            {
                // 主窗可能隐藏到托盘了，标题仍在但需要恢复
                // 改用枚举所有顶级窗口 + 进程名匹配的方式
                hWnd = FindByProcessName("A3Tools");
            }
            if (hWnd == IntPtr.Zero) return;

            // 突破 Vista+ 抢焦点限制
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                AllowSetForegroundWindow((int)fgPid);
            }
            // 最小化则恢复
            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
        catch
        {
            // 任何失败都不影响退出流程
        }
    }

    /// <summary>
    /// 枚举所有顶级窗口，按进程名匹配 A3Tools
    /// 托盘隐藏后窗口标题可能没变（仍然是 "A3工具箱"），但有些场景下需要走进程匹配
    /// </summary>
    private static IntPtr FindByProcessName(string processName)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                if (proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    // 优先选可见的主窗
                    if (IsWindowVisible(hWnd) || result == IntPtr.Zero)
                        result = hWnd;
                }
            }
            catch { /* 进程可能在枚举过程中退出，忽略 */ }
            return true; // 继续枚举
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// 显示一个 3 秒后自动消失的 Toast 提示，不抢主窗焦点，不阻塞进程退出
    /// </summary>
    private static void ShowAlreadyRunningHint()
    {
        try
        {
            var toast = new AlreadyRunningToastForm
            {
                DurationMs = AlreadyRunningToastDurationMs,
                Message = "A3工具箱 已在运行，已切到前台",
            };

            // Application.Run(form) 启动以 toast 为唯一窗体的消息循环
            // toast.Close() 会自动退出该消息循环，进程随之结束
            Application.Run(toast);
            toast.Dispose();
        }
        catch
        {
            // Toast 显示失败也不重要
        }
    }

    // ===== Win32 imports =====

    private const int SW_RESTORE = 9;
    // WS_EX_NOACTIVATE = 0x08000000：创建/显示窗口时不抢焦点
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    // WS_EX_TOOLWINDOW = 0x00000080：不在任务栏/Alt+Tab 列表显示
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}

/// <summary>
/// 重复启动 A3工具箱 时显示的轻量 Toast 窗体
/// 特性：
/// 1. 无边框 + 暗色背景 + 圆角 + 居中文本
/// 2. WS_EX_NOACTIVATE 样式：不抢主窗焦点
/// 3. WS_EX_TOOLWINDOW 样式：不在任务栏/Alt+Tab 出现
/// 4. Timer 自动关闭：默认 3 秒
/// 5. 右下角定位（距离任务栏 20px）
/// </summary>
internal sealed class AlreadyRunningToastForm : Form
{
    private readonly System.Windows.Forms.Timer _closeTimer;
    private int _durationMs = 3000;
    private string _message = "A3工具箱 已在运行，已切到前台";
    private const int ToastWidth = 340;
    private const int ToastHeight = 64;
    private const int MarginFromTaskbar = 20;
    private const int CornerRadius = 8;
    private readonly Label labelMessage;

    public int DurationMs
    {
        get => _durationMs;
        set => _durationMs = value > 0 ? value : 3000;
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value ?? string.Empty;
            if (labelMessage != null) labelMessage.Text = _message;
        }
    }

    public AlreadyRunningToastForm()
    {
        labelMessage = new Label();

        SuspendLayout();

        // 窗体基础设置
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(45, 45, 45);
        Opacity = 0.92;
        Size = new Size(ToastWidth, ToastHeight);
        Cursor = Cursors.Default;

        // 文本 Label
        labelMessage.AutoSize = false;
        labelMessage.Dock = DockStyle.Fill;
        labelMessage.TextAlign = ContentAlignment.MiddleCenter;
        labelMessage.ForeColor = Color.White;
        labelMessage.BackColor = Color.Transparent;
        labelMessage.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
        labelMessage.Text = _message;
        labelMessage.Padding = new Padding(16, 0, 16, 0);
        labelMessage.UseCompatibleTextRendering = false;
        Controls.Add(labelMessage);

        // 关闭 Timer
        _closeTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _closeTimer.Tick += CloseTimer_Tick;

        Name = "AlreadyRunningToast";
        Text = string.Empty;

        ResumeLayout(false);

        Load += AlreadyRunningToastForm_Load;
    }

    /// <summary>
    /// 关键样式：WS_EX_NOACTIVATE（不抢焦点）+ WS_EX_TOOLWINDOW（不在任务栏/Alt+Tab）
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Program.WS_EX_NOACTIVATE | Program.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private void AlreadyRunningToastForm_Load(object? sender, EventArgs e)
    {
        // 设置圆角 Region（避免锯齿边）
        using var path = new GraphicsPath();
        path.AddArc(0, 0, CornerRadius * 2, CornerRadius * 2, 180, 90);
        path.AddArc(Width - CornerRadius * 2, 0, CornerRadius * 2, CornerRadius * 2, 270, 90);
        path.AddArc(Width - CornerRadius * 2, Height - CornerRadius * 2, CornerRadius * 2, CornerRadius * 2, 0, 90);
        path.AddArc(0, Height - CornerRadius * 2, CornerRadius * 2, CornerRadius * 2, 90, 90);
        path.CloseAllFigures();
        Region = new Region(path);

        // 右下角定位（任务栏上方 20px）
        var screen = Screen.PrimaryScreen?.WorkingArea
                     ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            screen.Right - Width - MarginFromTaskbar,
            screen.Bottom - Height - MarginFromTaskbar);

        // 启动倒计时关闭
        _closeTimer.Interval = DurationMs;
        _closeTimer.Start();

        // 鼠标移入停止计时（用户想看清），移出后再给 1.5 秒
        MouseEnter += (s, e2) => _closeTimer.Stop();
        MouseLeave += (s, e2) =>
        {
            _closeTimer.Interval = 1500;
            _closeTimer.Start();
        };
    }

    private void CloseTimer_Tick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        // 用 BeginInvoke 避免在 Tick 里直接 Close 触发异常
        BeginInvoke(new Action(() =>
        {
            // 淡出效果：200ms 渐变
            var fadeOut = new System.Windows.Forms.Timer { Interval = 15 };
            int totalTicks = 200 / 15; // 约 13 帧
            int tickCount = 0;
            double startOpacity = Opacity;
            fadeOut.Tick += (s2, e2) =>
            {
                tickCount++;
                Opacity = startOpacity * (1.0 - (double)tickCount / totalTicks);
                if (tickCount >= totalTicks)
                {
                    fadeOut.Stop();
                    Close();
                }
            };
            fadeOut.Start();
        }));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 画一个 1px 的浅色边框（让暗色背景在白底桌面上有边界感）
        using var pen = new Pen(Color.FromArgb(80, 80, 80), 1f);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(pen, rect);
    }
}
