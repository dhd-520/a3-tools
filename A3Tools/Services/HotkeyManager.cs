using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace A3Tools.Services;

/// <summary>
/// 全局快捷键接收器 - 使用独立的 NativeWindow 接收快捷键消息
/// 即使主窗体隐藏也能响应快捷键
/// </summary>
public class HotkeyReceiver : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public HotkeyReceiver()
    {
        // 创建一个隐藏的消息窗口
        var cp = new CreateParams();
        cp.ExStyle = 0x08000000; // WS_EX_TOOLWINDOW - 不显示在任务栏
        CreateHandle(cp);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DestroyHandle();
            _disposed = true;
        }
    }
}

/// <summary>
/// 全局快捷键管理器
/// 使用 Win32 API 注册系统级热键
/// </summary>
public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifiers
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private IntPtr _hWnd;
    private int _currentId = 0;
    private bool _disposed = false;
    private HotkeyReceiver? _receiver;

    /// <summary>
    /// 当快捷键被按下时触发
    /// </summary>
    public event EventHandler? HotkeyPressed
    {
        add
        {
            if (_receiver != null)
                _receiver.HotkeyPressed += value;
        }
        remove
        {
            if (_receiver != null)
                _receiver.HotkeyPressed -= value;
        }
    }

    /// <summary>
    /// 注册快捷键
    /// </summary>
    /// <param name="hotkeyString">快捷键字符串，如 "Ctrl+Shift+Z"</param>
    /// <returns>是否注册成功</returns>
    public bool RegisterHotkey(string hotkeyString)
    {
        if (string.IsNullOrEmpty(hotkeyString))
        {
            UnregisterCurrentHotkey();
            return false;
        }

        // 创建独立的消息接收窗口
        if (_receiver == null)
        {
            _receiver = new HotkeyReceiver();
        }

        _hWnd = _receiver.Handle;

        // 解析快捷键字符串
        uint modifiers = 0;
        uint key = 0;

        var parts = hotkeyString.Split('+');
        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLower();
            switch (trimmed)
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    // 应该是按键
                    if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
                    {
                        key = (uint)char.ToUpper(trimmed[0]);
                    }
                    else if (Enum.TryParse<Keys>(part.Trim(), true, out var keys))
                    {
                        key = (uint)keys;
                    }
                    break;
            }
        }

        if (key == 0)
            return false;

        // 先取消之前的注册
        UnregisterCurrentHotkey();

        // 注册新的快捷键
        _currentId = 1;
        if (RegisterHotKey(_hWnd, _currentId, modifiers, key))
        {
            return true;
        }

        _currentId = 0;
        return false;
    }

    /// <summary>
    /// 取消当前注册的快捷键
    /// </summary>
    public void UnregisterCurrentHotkey()
    {
        if (_currentId != 0 && _hWnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hWnd, _currentId);
            _currentId = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterCurrentHotkey();
        _receiver?.Dispose();
    }
}
