using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace A3Tools.Services;

/// <summary>
/// 全局快捷键接收器 - 使用独立的 NativeWindow 接收快捷键消息
/// </summary>
public class HotkeyReceiver : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private bool _disposed;

    public event System.EventHandler<int>? HotkeyPressed;

    public HotkeyReceiver()
    {
        var cp = new CreateParams { ExStyle = 0x08000000 };
        CreateHandle(cp);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            HotkeyPressed?.Invoke(this, id);
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
/// 全局快捷键管理器 - 支持注册多个命名的快捷键
/// </summary>
public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private IntPtr _hWnd;
    private readonly Dictionary<int, string> _registeredIds = new();
    private int _nextId = 1;
    private bool _disposed;
    private HotkeyReceiver? _receiver;

    public event System.EventHandler<int>? HotkeyPressed
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

    public void EnsureReceiver()
    {
        if (_receiver == null)
        {
            _receiver = new HotkeyReceiver();
            _hWnd = _receiver.Handle;
        }
    }

    /// <summary>
    /// 注册快捷键，返回分配的 ID；空字符串则取消注册
    /// </summary>
    public int RegisterHotkey(string hotkeyString)
    {
        if (string.IsNullOrEmpty(hotkeyString))
            return -1;

        EnsureReceiver();
        ParseHotkey(hotkeyString, out uint modifiers, out uint key);
        if (key == 0) return -1;

        int id = _nextId++;
        if (RegisterHotKey(_hWnd, id, modifiers, key))
        {
            _registeredIds[id] = hotkeyString;
            return id;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeyManager] RegisterHotKey failed for ID {id}, hotkey='{hotkeyString}', modifiers={modifiers}, key={key}");
            return -1;
        }
    }

    /// <summary>
    /// 根据已有 ID 重新注册快捷键
    /// </summary>
    public bool ReregisterHotkey(int existingId, string hotkeyString)
    {
        if (string.IsNullOrEmpty(hotkeyString))
        {
            UnregisterHotkey(existingId);
            return true;
        }

        EnsureReceiver();
        UnregisterHotKey(_hWnd, existingId);
        ParseHotkey(hotkeyString, out uint modifiers, out uint key);
        if (key == 0) return false;

        if (RegisterHotKey(_hWnd, existingId, modifiers, key))
        {
            _registeredIds[existingId] = hotkeyString;
            return true;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeyManager] ReregisterHotKey failed for ID {existingId}, hotkey='{hotkeyString}', modifiers={modifiers}, key={key}");
            return false;
        }
    }

    /// <summary>
    /// 取消注册指定 ID 的快捷键
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        if (id <= 0 || !_registeredIds.ContainsKey(id)) return;
        if (_hWnd != IntPtr.Zero)
            UnregisterHotKey(_hWnd, id);
        _registeredIds.Remove(id);
    }

    /// <summary>
    /// 取消所有快捷键
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredIds.Keys.ToList())
            UnregisterHotkey(id);
        _registeredIds.Clear();
    }

    /// <summary>
    /// 调试用：获取当前接收器 Handle
    /// </summary>
    public string GetDebugHandle()
    {
        EnsureReceiver();
        return _hWnd.ToString();
    }

    private void ParseHotkey(string hotkeyString, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
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
                    if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
                        key = (uint)char.ToUpper(trimmed[0]);
                    else if (Enum.TryParse<Keys>(part.Trim(), true, out var keys))
                        key = (uint)keys;
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _receiver?.Dispose();
    }
}