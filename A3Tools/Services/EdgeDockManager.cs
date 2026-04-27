using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Services;

/// <summary>
/// 窗体贴顶部自动隐藏（极简实现）
/// 原理：一个 Timer 轮询鼠标位置，窗体在顶部时鼠标离开就藏，鼠标回来就显示
/// </summary>
public class EdgeDockManager : IDisposable
{
    private readonly Form _form;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _isHidden;
    private bool _disposed;
    private DateTime? _mouseLeftTime;

    private const int VISIBLE_STRIP = 2;
    private const int HIDE_DELAY = 400;

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
        if (!_form.Visible) return;
        // 用户正在拖动窗体，不干预
        if (Control.MouseButtons.HasFlag(MouseButtons.Left)) return;

        var workArea = Screen.FromControl(_form).WorkingArea;
        bool isAtTop = _form.Top <= workArea.Top;

        // 窗体不在顶部且未处于隐藏状态，无需处理
        if (!isAtTop && !_isHidden) return;

        var mousePos = Cursor.Position;
        bool mouseInForm;

        if (_isHidden)
        {
            // 隐藏状态：检测鼠标是否在顶部边缘条区域
            var stripRect = new Rectangle(_form.Left, workArea.Top, _form.Width, VISIBLE_STRIP + 6);
            mouseInForm = stripRect.Contains(mousePos);
        }
        else
        {
            // 显示状态：检测鼠标是否在窗体内
            mouseInForm = _form.Bounds.Contains(mousePos);
        }

        if (_isHidden && mouseInForm)
        {
            // 鼠标进入 → 显示窗体并置顶激活
            _form.Top = workArea.Top;
            _form.TopMost = true;
            _form.Activate();
            _isHidden = false;
            _mouseLeftTime = null;
        }
        else if (!_isHidden && !mouseInForm && isAtTop)
        {
            // 鼠标离开 → 延迟后隐藏窗体
            if (_mouseLeftTime == null)
            {
                _mouseLeftTime = DateTime.Now;
            }
            else if ((DateTime.Now - _mouseLeftTime.Value).TotalMilliseconds >= HIDE_DELAY)
            {
                _form.TopMost = false;
                _form.Top = workArea.Top - _form.Height + VISIBLE_STRIP;
                _isHidden = true;
                _mouseLeftTime = null;
            }
        }
        else if (mouseInForm)
        {
            _mouseLeftTime = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _timer.Dispose();

        // 恢复窗体位置
        if (_isHidden)
        {
            try
            {
                var workArea = Screen.FromControl(_form).WorkingArea;
                _form.Top = workArea.Top;
            }
            catch { }
        }
    }
}
