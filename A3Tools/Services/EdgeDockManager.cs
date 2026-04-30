using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Services;

/// <summary>
/// 窗体贴顶部自动隐藏到托盘
/// 原理：一个 Timer 轮询鼠标位置，窗体在顶部时鼠标离开就隐藏到托盘，鼠标在托盘图标就显示
/// </summary>
public class EdgeDockManager : IDisposable
{
    private readonly Form _form;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _disposed;
    private DateTime? _mouseLeftTime;
    private bool _isHiding;
    private DateTime _lastShowTime = DateTime.MinValue;

    private const int HIDE_DELAY = 400;
    private const int SHOW_GRACE_PERIOD = 500; // 显示后500毫秒内不自动隐藏

    /// <summary>
    /// 隐藏到托盘时调用
    /// </summary>
    public Action? OnHideToTray { get; set; }

    /// <summary>
    /// 从托盘显示时调用
    /// </summary>
    public Action? OnShowFromTray { get; set; }

    /// <summary>
    /// 显示窗体时调用（鼠标移到边缘触发）
    /// </summary>
    public Action? OnShowFromEdge { get; set; }

    /// <summary>
    /// 是否正在隐藏流程中（防止重复触发）
    /// </summary>
    public bool IsHiding => _isHiding;

    /// <summary>
    /// 记录窗体显示时间，用于防止显示后立即被自动隐藏
    /// </summary>
    public void RecordShowTime()
    {
        _lastShowTime = DateTime.Now;
    }

    public EdgeDockManager(Form form)
    {
        _form = form;
        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_form.WindowState == FormWindowState.Maximized) return;
        
        // 用户正在拖动窗体，不干预
        if (Control.MouseButtons.HasFlag(MouseButtons.Left)) return;

        // 如果正在隐藏流程中，不检测
        if (_isHiding) return;

        var workArea = Screen.FromControl(_form).WorkingArea;
        var mousePos = Cursor.Position;

        // 检查是否在托盘隐藏模式
        bool isHiddenToTray = OnHideToTray != null && !_form.Visible;

        if (isHiddenToTray)
        {
            // 托盘隐藏模式：检测鼠标是否在屏幕顶部边缘
            // 顶部边缘区域：整个屏幕宽度，高度约8像素
            var topEdgeRect = new Rectangle(0, workArea.Top, workArea.Width, 8);
            if (topEdgeRect.Contains(mousePos))
            {
                // 鼠标在顶部边缘，触发显示
                _lastShowTime = DateTime.Now;
                OnShowFromEdge?.Invoke();
            }
            return;
        }

        // 正常模式：窗体可见
        bool isAtTop = _form.Top <= workArea.Top;

        // 窗体不在顶部，无需处理
        if (!isAtTop) 
        {
            _mouseLeftTime = null;
            return;
        }

        // 检测鼠标是否在窗体内
        bool mouseInForm = _form.Bounds.Contains(mousePos);

        // 如果有子窗体（如对话框）正在显示，不隐藏
        if (!mouseInForm && isAtTop)
        {
            if (HasActiveChildWindow())
            {
                _mouseLeftTime = null;
                return;
            }

            // 如果刚刚显示过（在Grace Period内），不自动隐藏
            if ((DateTime.Now - _lastShowTime).TotalMilliseconds < SHOW_GRACE_PERIOD)
            {
                return;
            }

            if (_mouseLeftTime == null)
            {
                _mouseLeftTime = DateTime.Now;
            }
            else if ((DateTime.Now - _mouseLeftTime.Value).TotalMilliseconds >= HIDE_DELAY)
            {
                _isHiding = true;
                _mouseLeftTime = null;
                
                // 直接隐藏到托盘
                OnHideToTray?.Invoke();
                
                _isHiding = false;
            }
        }
        else if (mouseInForm)
        {
            _mouseLeftTime = null;
        }
    }

    /// <summary>
    /// 检查是否有子窗体（如对话框）在主窗体范围内活动
    /// </summary>
    private bool HasActiveChildWindow()
    {
        // 检查是否有 OwnedForms 且其中任何一个在可见状态
        if (_form.OwnedForms != null)
        {
            foreach (var owned in _form.OwnedForms)
            {
                if (owned.Visible && owned.WindowState != FormWindowState.Minimized)
                {
                    return true;
                }
            }
        }

        // 检查 Application.OpenForms 中是否有主窗体的子窗体
        foreach (Form form in Application.OpenForms)
        {
            if (form == _form) continue;
            if (form.Owner == _form && form.Visible && form.WindowState != FormWindowState.Minimized)
            {
                return true;
            }
            // 检查是否是主窗体的子控件（通过 Owner 链判断）
            var owner = form.Owner;
            while (owner != null)
            {
                if (owner == _form && form.Visible && form.WindowState != FormWindowState.Minimized)
                {
                    return true;
                }
                owner = owner.Owner;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _timer.Dispose();
    }
}
