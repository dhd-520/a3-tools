using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 增强版 RichTextBox（SQL 编辑器）：
/// - 拦截滚动消息（WM_VSCROLL/WM_MOUSEWHEEL/WM_HSCROLL/EM_LINESCROLL）触发 ViewChanged
/// - TextChanged 节流（200ms）后调用 SQL 高亮，避免每按一键都全量重算
/// - SelectionChanged 也触发 ViewChanged，让行号面板能高亮当前行
/// </summary>
public class SqlEditor : RichTextBox
{
    private const int WM_VSCROLL = 0x115;
    private const int WM_MOUSEWHEEL = 0x20A;
    private const int WM_HSCROLL = 0x114;
    private const int EM_LINESCROLL = 0xB6;

    private readonly System.Windows.Forms.Timer _highlightTimer;
    private bool _suppressHighlight;

    /// <summary>滚动位置 / 选区 / 文本变化时触发（行号面板监听此事件重绘）</summary>
    public event EventHandler? ViewChanged;

    private readonly IntelliSensePopup _intelliSense = new();
    private readonly System.Windows.Forms.Timer _intelliSenseTimer;
    private bool _suppressIntelliSense;

    public SqlEditor()
    {
        _highlightTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _highlightTimer.Tick += (_, _) =>
        {
            _highlightTimer.Stop();
            Highlight();
        };

        _intelliSenseTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _intelliSenseTimer.Tick += (_, _) =>
        {
            _intelliSenseTimer.Stop();
            TriggerIntelliSense();
        };

        _intelliSense.ItemActivated += (_, text) => ReplaceCurrentWord(text);
    }

    public void SuspendHighlight(bool suspend) => _suppressHighlight = suspend;

    public void HighlightNow()
    {
        _highlightTimer.Stop();
        Highlight();
    }

    /// <summary>字体大小改变时触发（行号面板/状态栏监听）</summary>
    public event EventHandler? FontSizeChanged;

    /// <summary>当前字号（供外部读取 / 重设）</summary>
    public float CurrentFontSize => Font.Size;

    private const float MinFontSize = 8F;
    private const float MaxFontSize = 32F;
    private const float FontSizeStep = 1F;

    /// <summary>手动设置字体大小（超过范围自动限制）</summary>
    public void SetFontSize(float size)
    {
        var newSize = Math.Clamp(size, MinFontSize, MaxFontSize);
        if (Math.Abs(newSize - Font.Size) < 0.01F) return;
        Font = new Font(Font.FontFamily, newSize, Font.Style);
        FontSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ctrl+滚轮缩放字体（仿 SSMS）。返回 true 表示事件被吞掉（不应再触发滚动）</summary>
    public bool HandleCtrlMouseWheel(MouseEventArgs e)
    {
        if (ModifierKeys != Keys.Control) return false;
        var delta = e.Delta > 0 ? FontSizeStep : -FontSizeStep;
        SetFontSize(Font.Size + delta);
        return true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (HandleCtrlMouseWheel(e))
        {
            // Ctrl+滚轮只缩放字体，不滚动视图
            return;
        }
        base.OnMouseWheel(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_HSCROLL || m.Msg == EM_LINESCROLL)
            ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ViewChanged?.Invoke(this, EventArgs.Empty);

        // 高亮节流（与 IntelliSense 独立 —— 之前共用 _suppressHighlight，
        // 导致外部 ReplaceCurrentWord 时整个 OnTextChanged 直接 return，IntelliSense 也不再触发）
        if (!_suppressHighlight)
        {
            _highlightTimer.Stop();
            _highlightTimer.Start();
        }

        // IntelliSense 节流（独立判断 _suppressIntelliSense）
        // 50ms 是经过权衡：太快会卡，太慢让用户感到"按了不弹"
        if (!_suppressIntelliSense)
        {
            _intelliSenseTimer.Stop();
            _intelliSenseTimer.Start();
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        // 失焦时不关闭 popup（用户在选 popup 项时焦点转移）
        // popup 由其点击外部 / Esc / 选中项 关闭
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnVScroll(EventArgs e)
    {
        base.OnVScroll(e);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // IntelliSense 拦截（仅在 popup visible 时）
        if (_intelliSense.IsVisible)
        {
            if (e.KeyCode == Keys.Up)
            {
                _intelliSense.MoveSelection(-1);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                _intelliSense.MoveSelection(1);
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                var sel = _intelliSense.GetSelectedText();
                if (!string.IsNullOrEmpty(sel))
                {
                    ReplaceCurrentWord(sel);
                    _intelliSense.Hide();
                    e.SuppressKeyPress = true;
                    return;
                }
            }
            if (e.KeyCode == Keys.Escape)
            {
                _intelliSense.Hide();
                e.SuppressKeyPress = true;
                return;
            }
        }

        // Ctrl+Space 手动触发提示（即使 popup 已隐藏）
        if (e.Control && e.KeyCode == Keys.Space)
        {
            TriggerIntelliSense();
            e.SuppressKeyPress = true;
            return;
        }

        // F5 = 执行（由 SqlQueryForm 主窗体拦截，这里不重复处理）
        // Ctrl+F5 = 执行选中（同上）
        // 只处理 Enter 自动缩进
        if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
        {
            // 暂存原始行为，先让 RichTextBox 处理换行
            base.OnKeyDown(e);
            // 然后追加当前行的缩进
            int curLineIdx = GetLineFromCharIndex(SelectionStart);
            if (curLineIdx <= 0) return;
            int prevLineStart = GetFirstCharIndexFromLine(curLineIdx - 1);
            int curLineStart = GetFirstCharIndexFromLine(curLineIdx);
            int prevLineLen = curLineStart - prevLineStart;
            string prevLineText = Text.Substring(prevLineStart, prevLineLen);
            string indent = "";
            foreach (char c in prevLineText)
            {
                if (c == ' ' || c == '\t') indent += c;
                else break;
            }
            // 如果上一行以 BEGIN/IF/WHILE/CASE/( 结尾，再加一级缩进
            var trimmed = prevLineText.TrimEnd();
            if (trimmed.EndsWith("BEGIN", StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("IF ", StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("WHILE ", StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("CASE ", StringComparison.OrdinalIgnoreCase)
                || trimmed.EndsWith("(", StringComparison.Ordinal)
                || trimmed.EndsWith("(", StringComparison.Ordinal))
            {
                indent += "    ";
            }
            if (indent.Length > 0)
            {
                _suppressHighlight = true;
                SelectedText = indent;
                _suppressHighlight = false;
            }
            e.SuppressKeyPress = true;
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    // ============================================
    // IntelliSense
    // ============================================

    /// <summary>
    /// 当前连接串（由 SqlQueryForm 在初始化 / 切库时同步设置）。
    /// 用来让 IntelliSense 能拉当前库对象。
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 获取当前光标位置之前的"候选 token"。
    /// 比 GetCurrentWord 更宽：
    ///   "Sel" -> "Sel"
    ///   "dbo.Sel" -> "dbo.Sel"        （含 schema. 限定，一起拉）
    ///   "Sales." -> "Sales."          （schema 限定，名字前缀为空 -> 弹该 schema 全对象）
    ///   "[Sel" -> "[Sel"               （SSMS 风格的方括号转义，先不展开）
    ///   "[dbo].[Sel" -> "[dbo].[Sel"
    /// "." 视为单词一部分；其他标点（空格、`,`、`(`、`;`）视为分隔符。
    /// </summary>
    private string GetCurrentWord()
    {
        int caret = SelectionStart;
        if (caret == 0) return "";
        int start = caret;
        while (start > 0)
        {
            char c = Text[start - 1];
            // 标识符 / . / [] 内的内容 视为单词一部分
            if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '.')
                start--;
            else if (c == ']' && start >= 2 && Text[start - 2] == '[')
            {
                // 跳过 `]` 后的内容直到 `[`
                start--;
                while (start > 0 && Text[start - 1] != '[')
                    start--;
                if (start > 0) start--; // 跳过 `[`
            }
            else
                break;
        }
        if (start == caret) return "";
        return Text.Substring(start, caret - start);
    }

    /// <summary>获取当前光标位置之前的完整 token（含 schema. / [] 转义），与 GetCurrentWord 一致</summary>
    private int GetCurrentWordStart()
    {
        int caret = SelectionStart;
        int start = caret;
        while (start > 0)
        {
            char c = Text[start - 1];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '.')
                start--;
            else if (c == ']' && start >= 2 && Text[start - 2] == '[')
            {
                start--;
                while (start > 0 && Text[start - 1] != '[')
                    start--;
                if (start > 0) start--;
            }
            else
                break;
        }
        return start;
    }

    /// <summary>替换当前单词为指定文本（用于补全）</summary>
    private void ReplaceCurrentWord(string replacement)
    {
        int caret = SelectionStart;
        int start = GetCurrentWordStart();
        int len = caret - start;
        if (len <= 0)
        {
            // 没有部分单词，直接插入
            _suppressHighlight = true;
            _suppressIntelliSense = true;
            SelectionStart = caret;
            SelectedText = replacement;
            _suppressHighlight = false;
            _suppressIntelliSense = false;
            return;
        }
        _suppressHighlight = true;
        _suppressIntelliSense = true;
        Select(start, len);
        SelectedText = replacement;
        SelectionStart = start + replacement.Length;
        _suppressHighlight = false;
        _suppressIntelliSense = false;
    }

    /// <summary>触发 IntelliSense 提示（节流后调用）</summary>
    private void TriggerIntelliSense()
    {
        if (_suppressIntelliSense) return;

        string word = GetCurrentWord();

        // 空单词 或 处于不合适的上下文（光标前刚输入了非标识符字符）→ 关闭
        if (word.Length < 1)
        {
            _intelliSense.Hide();
            return;
        }

        // 光标位置：用于 1) popup 锚点 2) 给 SqlAliasResolver 留扩展点
        int caret = SelectionStart;

        var matches = SqlIntelliSenseProvider.GetSuggestions(word, ConnectionString, Text, caret, 80).ToList();
        if (matches.Count == 0)
        {
            _intelliSense.Hide();
            return;
        }

        // 计算 popup 屏幕位置：在光标所在行的"下一行"顶部（不要用 Font.Size 凑数，
        // 那是字号不是行高，会让 popup 顶部落在文字中间。按下一行首字符的位置才是准的）
        // ────────────────────────────────────────────────────────────────
        Point screenPos;
        int lineIdx = GetLineFromCharIndex(caret);
        int nextLineCharIdx = GetFirstCharIndexFromLine(lineIdx + 1);
        if (nextLineCharIdx >= 0)
        {
            // 有下一行：用下一行首字符位置作为 popup 锚点
            Point nextLinePos = GetPositionFromCharIndex(nextLineCharIdx);
            screenPos = PointToScreen(nextLinePos);
        }
        else
        {
            // 已经在最后一行：用当前字符位置 + 该行行高（Font.Height 是真正的行高，含行间距）
            Point charPos = GetPositionFromCharIndex(caret);
            int lineHeight = Font.Height; // 真行高（1.2x Font.Size），比 Font.Size 准
            screenPos = PointToScreen(new Point(charPos.X, charPos.Y + lineHeight + 2));
        }

        _intelliSense.ShowNearCaret(this, screenPos, 320, 280, matches);
    }

    // ============================================
    // SQL 高亮
    // ============================================

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select","from","where","and","or","not","in","is","null","like",
        "between","exists","case","when","then","else","end","as","on",
        "join","left","right","inner","outer","full","cross","apply",
        "group","by","having","order","asc","desc","union","all","into",
        "insert","values","update","set","delete",
        "create","alter","drop","truncate","table","view","procedure",
        "function","trigger","index","database","schema",
        "declare","begin","commit","rollback","transaction","try","catch",
        "if","while","return","returns","go","use","exec","execute",
        "sp_executesql","with","over","partition","row_number",
        "primary","foreign","key","references","default","check","unique",
        "print","raiserror","throw","output","identity","sequence",
        "distinct","top","offset","fetch","next","rows","only",
        "match","pivot","unpivot","merge"
    };

    private static readonly Regex WordRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);
    private static readonly Regex StringRegex = new(@"'(?:''|[^'])*'", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
    private static readonly Regex CommentLineRegex = new(@"--[^\r\n]*", RegexOptions.Compiled);
    private static readonly Regex CommentBlockRegex = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);

    private static readonly Color KeywordColor = Color.FromArgb(0, 0, 255);
    private static readonly Color StringColor = Color.FromArgb(163, 21, 21);
    private static readonly Color NumberColor = Color.FromArgb(0, 128, 0);
    private static readonly Color CommentColor = Color.FromArgb(0, 128, 0);

    private void Highlight()
    {
        if (IsDisposed || TextLength == 0) return;
        if (!IsHandleCreated) return;

        int selStart = SelectionStart;
        int selLen = SelectionLength;

        _suppressHighlight = true;
        try
        {
            SuspendLayout();

            // 1. 全部默认黑色
            Select(0, TextLength);
            SelectionColor = Color.Black;

            var text = Text;

            // 2. 关键字
            foreach (Match m in WordRegex.Matches(text))
            {
                if (Keywords.Contains(m.Value))
                {
                    Select(m.Index, m.Length);
                    SelectionColor = KeywordColor;
                }
            }

            // 3. 数字
            foreach (Match m in NumberRegex.Matches(text))
            {
                Select(m.Index, m.Length);
                SelectionColor = NumberColor;
            }

            // 4. 注释（行注释 + 块注释）
            foreach (Match m in CommentLineRegex.Matches(text))
            {
                Select(m.Index, m.Length);
                SelectionColor = CommentColor;
            }
            foreach (Match m in CommentBlockRegex.Matches(text))
            {
                Select(m.Index, m.Length);
                SelectionColor = CommentColor;
            }

            // 5. 字符串（最后，覆盖关键字/数字）
            foreach (Match m in StringRegex.Matches(text))
            {
                Select(m.Index, m.Length);
                SelectionColor = StringColor;
            }

            // 恢复选区
            Select(selStart, selLen);
            SelectionColor = Color.Black;
        }
        finally
        {
            ResumeLayout();
            _suppressHighlight = false;
        }
    }
}

/// <summary>
/// 行号面板，自绘 RichTextBox 左侧的行号。
/// Bind(editor) 后监听 editor.ViewChanged 重绘。
/// </summary>
public class LineNumberPanel : Control
{
    private SqlEditor? _editor;

    public LineNumberPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(240, 242, 245);
        ForeColor = Color.FromArgb(160, 160, 160);
        Font = new Font("Consolas", 9.5F);
        Width = 44;
        Cursor = Cursors.Default;
    }

    public void Bind(SqlEditor editor)
    {
        if (_editor != null)
        {
            _editor.ViewChanged -= OnViewChanged;
            _editor.FontSizeChanged -= SyncFontSize;
        }
        _editor = editor;
        _editor.ViewChanged += OnViewChanged;
        _editor.FontSizeChanged += SyncFontSize;
        SyncFontSize(editor, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>同步编辑器字体（字号 + 字体族）</summary>
    private void SyncFontSize(object? sender, EventArgs e)
    {
        if (_editor == null) return;
        Font = new Font(_editor.Font.FontFamily, _editor.Font.Size, Font.Style);
        // 1000 行以上时加宽面板
        int lineCount = _editor.Lines.Length;
        int newWidth = lineCount >= 1000 ? 60 : lineCount >= 100 ? 50 : 44;
        if (Width != newWidth) Width = newWidth;
        Invalidate();
    }

    private void OnViewChanged(object? sender, EventArgs e) => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_editor == null || _editor.TextLength == 0)
        {
            // 空编辑器也画一条竖线
            using var borderPen = new Pen(Color.FromArgb(225, 230, 235));
            e.Graphics.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
            return;
        }

        var g = e.Graphics;
        g.Clear(BackColor);

        // 计算可见行范围
        int firstChar = _editor.GetCharIndexFromPosition(new Point(0, 0));
        int firstLine = Math.Max(0, _editor.GetLineFromCharIndex(firstChar));
        int lastChar = _editor.GetCharIndexFromPosition(new Point(0, _editor.Height - 2));
        int lastLine = _editor.GetLineFromCharIndex(lastChar);

        int currentLine = _editor.GetLineFromCharIndex(_editor.SelectionStart);

        for (int i = firstLine; i <= lastLine + 1; i++)
        {
            int charIdx = _editor.GetFirstCharIndexFromLine(i);
            if (charIdx < 0) break;
            var pos = _editor.GetPositionFromCharIndex(charIdx);
            if (pos.Y < 0 || pos.Y > _editor.Height) continue;

            bool isCurrent = (i == currentLine);
            using var brush = new SolidBrush(isCurrent ? Color.FromArgb(24, 144, 255) : ForeColor);
            using var font = new Font(Font.FontFamily, Font.Size, isCurrent ? FontStyle.Bold : FontStyle.Regular);

            var text = (i + 1).ToString();
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, Width - size.Width - 6, pos.Y);
        }

        // 右边竖线
        using var borderPen2 = new Pen(Color.FromArgb(225, 230, 235));
        g.DrawLine(borderPen2, Width - 1, 0, Width - 1, Height);
    }
}