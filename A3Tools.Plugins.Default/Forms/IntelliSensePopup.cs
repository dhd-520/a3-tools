using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// IntelliSense 浮层提示框（仿 SSMS）。
/// 内部用 ToolStripDropDown + ListBox 实现，自动定位在光标下方。
/// - ShowNearCaret: 在屏幕坐标处显示
/// - Filter: 按关键字过滤
/// - MoveSelection: ↑↓ 选择项
/// - Hide: 关闭
/// - 事件 ItemActivated: 双击 / Enter 触发补全
/// </summary>
public class IntelliSensePopup
{
    private readonly ToolStripDropDown _dropDown;
    private readonly ListBox _listBox;
    private readonly ToolStripControlHost _host;
    private readonly List<string> _allItems = new();

    /// <summary>用户双击 / Enter 确认选中</summary>
    public event EventHandler<string>? ItemActivated;

    public IntelliSensePopup()
    {
        _listBox = new ListBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10F),
            IntegralHeight = false,
            ItemHeight = 20,
            DrawMode = DrawMode.OwnerDrawVariable,
            BackColor = Color.FromArgb(250, 251, 253),
            ForeColor = Color.FromArgb(40, 50, 70)
        };
        _listBox.MeasureItem += (_, e) => e.ItemHeight = 20;
        _listBox.DrawItem += ListBox_DrawItem;
        _listBox.KeyDown += ListBox_KeyDown;
        _listBox.DoubleClick += (_, _) => ActivateSelected();
        _listBox.MouseMove += ListBox_MouseMove;
        // 不监听 ListBox.Resize：避免 "ListBox.Resize → host.Size → 重新布局 → ListBox.Resize" 死循环
        // 尺寸在 ShowNearCaret 里一次性设好

        _host = new ToolStripControlHost(_listBox)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _dropDown = new ToolStripDropDown
        {
            AutoClose = false,
            BackColor = Color.FromArgb(220, 225, 235),
            Opacity = 0.98
        };
        _dropDown.Items.Add(_host);
    }

    public bool IsVisible => _dropDown.Visible;

    public void ShowNearCaret(Control owner, Point screenLocation, int width, int maxHeight, IEnumerable<string> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items);

        _listBox.Items.Clear();
        _listBox.Items.AddRange(items.ToArray());
        _listBox.Width = width;
        // 高度：按行数，最多 maxHeight，每行 20
        int showCount = Math.Min(_listBox.Items.Count, maxHeight / 20);
        if (showCount < 1) showCount = 1;
        if (showCount > _listBox.Items.Count) showCount = _listBox.Items.Count;
        _listBox.Height = showCount * 20 + 4;
        _host.Size = new Size(width + 2, _listBox.Height + 2);

        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;

        _dropDown.Show(owner, screenLocation);
        // 不转移焦点：让 SqlEditor 保留键盘输入
    }

    /// <summary>按关键字过滤（已经显示的 popup），更新可见项；空列表自动隐藏</summary>
    public void Filter(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            // 不过滤
            _listBox.Items.Clear();
            _listBox.Items.AddRange(_allItems.ToArray());
        }
        else
        {
            var matches = _allItems
                .Where(k => k.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .Take(50)
                .ToArray();
            _listBox.Items.Clear();
            _listBox.Items.AddRange(matches);
        }

        if (_listBox.Items.Count == 0)
        {
            Hide();
            return;
        }

        int showCount = Math.Min(_listBox.Items.Count, 12);
        _listBox.Height = showCount * 20 + 4;
        _host.Size = new Size(_listBox.Width + 2, _listBox.Height + 2);
        _listBox.SelectedIndex = 0;
    }

    public void MoveSelection(int delta)
    {
        if (!_dropDown.Visible || _listBox.Items.Count == 0) return;
        int newIdx = _listBox.SelectedIndex + delta;
        if (newIdx < 0) newIdx = 0;
        if (newIdx >= _listBox.Items.Count) newIdx = _listBox.Items.Count - 1;
        _listBox.SelectedIndex = newIdx;
    }

    public string? GetSelectedText()
    {
        return _listBox.SelectedItem?.ToString();
    }

    public void Hide()
    {
        if (_dropDown.Visible) _dropDown.Close();
    }

    private void ActivateSelected()
    {
        var text = GetSelectedText();
        if (!string.IsNullOrEmpty(text))
        {
            Hide();
            ItemActivated?.Invoke(this, text);
        }
    }

    private void ListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
        {
            ActivateSelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Hide();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            MoveSelection(1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ListBox_MouseMove(object? sender, MouseEventArgs e)
    {
        int idx = _listBox.IndexFromPoint(e.Location);
        if (idx >= 0 && idx != _listBox.SelectedIndex)
            _listBox.SelectedIndex = idx;
    }

    private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Color.FromArgb(24, 144, 255) : _listBox.BackColor);
        e.Graphics.FillRectangle(bg, e.Bounds);

        var text = _listBox.Items[e.Index]?.ToString() ?? "";
        using var fg = new SolidBrush(selected ? Color.White : Color.FromArgb(40, 50, 70));
        var font = new Font(_listBox.Font, selected ? FontStyle.Bold : FontStyle.Regular);
        e.Graphics.DrawString(text, font, fg, e.Bounds.X + 6, e.Bounds.Y + 2);
        e.DrawFocusRectangle();
    }
}