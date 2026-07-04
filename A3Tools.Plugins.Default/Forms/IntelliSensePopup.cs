using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// IntelliSense 浮层提示框（仿 SSMS）。
/// 用普通 Form 实现（不用 ToolStripDropDown）—— 避免抢焦点导致按键丢失。
/// - ShowNearCaret: 在屏幕坐标处显示；显示后立刻 owner.Focus() 还焦点
/// - Filter: 按关键字过滤（已经显示的 popup）
/// - MoveSelection: ↑↓ 选择项
/// - Hide: 关闭
/// - 事件 ItemActivated: 双击 / Enter 触发补全
///
/// 设计器兼容性：
/// - 构造函数里立刻检查 LicenseManager.UsageMode == Designtime 直接 return，
///   避免在 VS 设计器加载自定义控件时创建 Form Window Handle 触发
///   "ITypeIdentityResolutionService 开始处理排队程序集之前无法解析类型" 异常
/// - Opacity 不在 ctor 设，延后到 ShowNearCaret 设（设计时不会触发分层窗口创建）
/// </summary>
public class IntelliSensePopup : Form
{
    private ListBox? _listBox;       // 设计时跳过 ctor 时 _listBox 为 null，所有方法都用 null-safe 访问
    private readonly List<string> _allItems = new();
    private bool _opacityApplied;    // 第一次 ShowNearCaret 才设 Opacity（避免 ctor 阶段触发分层窗口）

    /// <summary>用户双击 / Enter 确认选中</summary>
    public event EventHandler<string>? ItemActivated;

    public IntelliSensePopup()
    {
        // ===== 设计器兼容：跳过 Form 初始化 =====
        // VS 设计器加载自定义控件时会调用字段初始化 new IntelliSensePopup()，
        // 不应该创建 Form Window Handle（会触发 ITypeIdentityResolutionService 异常）
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(220, 225, 235);
        Padding = Padding.Empty;
        Margin = Padding.Empty;
        // 注意：这里不设 Opacity —— 延后到 ShowNearCaret，避免 ctor 阶段触发分层窗口创建
        // （Form.Opacity setter 在没有 Window Handle 时只是存值，但有些 .NET 7 + Designer 组合
        //  会强制触发 Window Handle 创建，导致设计时异常）

        _listBox = new ListBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10F),
            IntegralHeight = false,
            // 行高 = 实际文字高度（10pt Consolas ≈ 13-14px）+ 上下各 4px padding = 22。
            // 之前 ItemHeight = 20 + DrawItem 用 Graphics.DrawString 描字，文字 ascent + descent ≈ 16-18px
            // 实际超出 20px 范围，文字底部被切掉，看起来"下行挡住"。
            // 这里改用 TextRenderer.DrawText + 足够 ItemHeight，全部可见。
            ItemHeight = 22,
            DrawMode = DrawMode.OwnerDrawFixed,  // 改 Fixed，强制严格按 ItemHeight 切行
            BackColor = Color.FromArgb(250, 251, 253),
            ForeColor = Color.FromArgb(40, 50, 70)
        };
        _listBox.DrawItem += ListBox_DrawItem;
        _listBox.KeyDown += ListBox_KeyDown;
        _listBox.DoubleClick += (_, _) => ActivateSelected();
        _listBox.MouseMove += ListBox_MouseMove;

        Controls.Add(_listBox);
    }

    /// <summary>popup 是否可见（editor 用此判断是否拦截 ↑↓EnterTabEsc）</summary>
    public bool IsVisible => !IsDisposed && _listBox != null && Visible;

    /// <summary>
    /// 在屏幕坐标处显示（不抢焦点）。
    /// owner = 编辑器控件（用于 Show 时归还焦点 + 关闭时跟随 owner 卸载）。
    /// </summary>
    public void ShowNearCaret(Control owner, Point screenLocation, int width, int maxHeight, IEnumerable<string> items)
    {
        // 设计器模式防御性检查（万一）
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
        if (IsDisposed || _listBox == null) return;
        if (owner == null || !owner.IsHandleCreated) return;

        _allItems.Clear();
        _allItems.AddRange(items);

        var listBox = _listBox;
        listBox.Items.Clear();
        listBox.Items.AddRange(items.ToArray());

        int popupWidth = Math.Max(width, 120);
        listBox.Width = popupWidth;
        // 行高统一 22（构造函数里定的 ItemHeight）
        const int rowHeight = 22;
        int showCount = Math.Min(listBox.Items.Count, maxHeight / rowHeight);
        if (showCount < 1) showCount = 1;
        if (showCount > listBox.Items.Count) showCount = listBox.Items.Count;
        int popupHeight = showCount * rowHeight + 4;
        listBox.Height = popupHeight;

        ClientSize = new Size(popupWidth + 2, popupHeight + 2);

        // 屏幕边界检查：若超出屏幕下方，改为向上显示
        var screen = Screen.FromControl(owner);
        var work = screen.WorkingArea;
        if (screenLocation.Y + Height > work.Bottom)
        {
            int newY = screenLocation.Y - popupHeight - (owner is Control o ? o.Font.Height + 4 : 20);
            if (newY < work.Top) newY = work.Top;
            screenLocation = new Point(screenLocation.X, newY);
        }
        // 屏幕右边超出检查
        if (screenLocation.X + Width > work.Right)
            screenLocation = new Point(work.Right - Width - 4, screenLocation.Y);

        Location = screenLocation;

        if (!_opacityApplied)
        {
            try { Opacity = 0.98f; _opacityApplied = true; }
            catch { /* 不让 Opacity 失败把整个弹窗拖死 */ }
        }

        if (listBox.Items.Count > 0)
            listBox.SelectedIndex = 0;

        if (!Visible)
        {
            // TopMost=true 让 popup 总在最上层，避免被 SplitContainer/TabControl 遮挡
            TopMost = true;
            Show(owner);  // 关联 owner（owner 关闭时 popup 也跟着关）
        }
        else
        {
            Refresh();
        }

        // ===== 关键：立即把焦点还回 editor =====
        // Form.Show(owner) 默认会激活并抢焦点；这一步把焦点拉回 editor
        // 下一次按键会进 editor → OnTextChanged → 重新触发 IntelliSense 更新
        owner.BeginInvoke(new Action(() =>
        {
            try { owner.Focus(); } catch { /* 控件可能正在销毁 */ }
        }));
    }

    /// <summary>按关键字过滤（已经显示的 popup），更新可见项；空列表自动隐藏</summary>
    public void Filter(string text)
    {
        if (IsDisposed || _listBox == null) return;
        var listBox = _listBox;

        if (string.IsNullOrEmpty(text))
        {
            listBox.Items.Clear();
            listBox.Items.AddRange(_allItems.ToArray());
        }
        else
        {
            var matches = _allItems
                .Where(k => k.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .Take(50)
                .ToArray();
            listBox.Items.Clear();
            listBox.Items.AddRange(matches);
        }

        if (listBox.Items.Count == 0)
        {
            Hide();
            return;
        }

        const int rowHeight = 22;
        int showCount = Math.Min(listBox.Items.Count, 12);
        int popupHeight = showCount * rowHeight + 4;
        listBox.Height = popupHeight;
        ClientSize = new Size(listBox.Width + 2, popupHeight + 2);

        listBox.SelectedIndex = 0;
    }

    public void MoveSelection(int delta)
    {
        if (IsDisposed || _listBox == null) return;
        if (!Visible || _listBox.Items.Count == 0) return;
        int newIdx = _listBox.SelectedIndex + delta;
        if (newIdx < 0) newIdx = 0;
        if (newIdx >= _listBox.Items.Count) newIdx = _listBox.Items.Count - 1;
        _listBox.SelectedIndex = newIdx;
    }

    public string? GetSelectedText()
    {
        if (_listBox == null) return null;
        return _listBox.SelectedItem?.ToString();
    }

    public new void Hide()
    {
        if (Visible)
        {
            TopMost = false;
            base.Hide();
        }
    }

    /// <summary>关闭且不弹任何事件（owner 关闭时调用）</summary>
    public void ClosePopup()
    {
        if (!IsDisposed) Close();
    }

    private void ActivateSelected()
    {
        if (_listBox == null) return;
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
            // 把焦点拉回 owner
            if (Owner is Control c) c.Focus();
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
        if (_listBox == null) return;
        int idx = _listBox.IndexFromPoint(e.Location);
        if (idx >= 0 && idx != _listBox.SelectedIndex)
            _listBox.SelectedIndex = idx;
    }

    private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (_listBox == null) return;
        if (e.Index < 0) return;
        e.DrawBackground();
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Color.FromArgb(24, 144, 255) : _listBox.BackColor);
        e.Graphics.FillRectangle(bg, e.Bounds);

        var text = _listBox.Items[e.Index]?.ToString() ?? "";
        using var fg = new SolidBrush(selected ? Color.White : Color.FromArgb(40, 50, 70));
        var font = new Font(_listBox.Font, selected ? FontStyle.Bold : FontStyle.Regular);

        // 用 TextRenderer.DrawText（与 GetRectangle 一致）+ 居中布局。
        // Graphics.DrawString + Y+2 会在 22px ItemHeight 里把 Consolas 10pt 文字底部切掉。
        TextRenderer.DrawText(e.Graphics, text, font,
            new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
            fg.Color, bg.Color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        e.DrawFocusRectangle();
    }

    /// <summary>
    /// 重写避免 Show 时激活窗口（虽然因为 BeginInvoke(owner.Focus()) 会把焦点拉回，
    /// 但这个标志让 Show 阶段不抢激活，行为更稳）。
    /// </summary>
    protected override bool ShowWithoutActivation => true;
}
