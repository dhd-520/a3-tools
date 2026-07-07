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
    private const int SB_HORZ = 0;
    private const int SB_VERT = 1;
    private const int SB_THUMBPOSITION = 4;

    // 冻结重绘：设置重绘抑制标志 + 解除后强制刷新。避免高亮过程中多次
    // Select + SelectionColor 引起的 RichTextBox 闪烁（richEdit 重绘不双缓冲，
    // 每设色都刷一次 → 闪）。
    private const int WM_SETREDRAW = 0x000B;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // 保存和恢复滚动位置
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

    private readonly System.Windows.Forms.Timer _highlightTimer;
    private bool _suppressHighlight;

    /// <summary>滚动位置 / 选区 / 文本变化时触发（行号面板监听此事件重绘）</summary>
    public event EventHandler? ViewChanged;

    private readonly IntelliSensePopup _intelliSense = new();
    private readonly System.Windows.Forms.Timer _intelliSenseTimer;
    private bool _suppressIntelliSense;

    public SqlEditor()
    {
        // 默认字体设大2个字号，使用Consolas等宽字体更适合SQL
        Font = new System.Drawing.Font("Consolas", 12f);

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

    /// <summary>公开 API：暂时屏蔽 IntelliSense（脚本加载防“加载完自动弹候选”之场景）</summary>
    public void SuspendIntelliSense(bool suspend) => _suppressIntelliSense = suspend;

    /// <summary>便捷：隐藏并立刻屏蔽</summary>
    public void SuppressIntelliSense()
    {
        _intelliSenseTimer.Stop();
        _suppressIntelliSense = true;
        _intelliSense.Hide();
    }

    /// <summary>便捷：恢复 IntelliSense</summary>
    public void ResumeIntelliSense() => _suppressIntelliSense = false;

    /// <summary>显示查找/替换对话框</summary>
    public void ShowSearchReplace(bool replaceMode)
    {
        var dlg = new SearchReplaceDialog(this, replaceMode);
        dlg.Show(this);
    }

    public void HighlightNow()
    {
        _highlightTimer.Stop();
        Highlight();
    }

    /// <summary>字体大小改变时触发（行号面板/状态栏监听）</summary>
    public event EventHandler? FontSizeChanged;

    /// <summary>当前字号（供外部读取 / 重设）</summary>
    public float CurrentFontSize => Font.Size;

    /// <summary>F12 转到定义事件。由 SqlQueryTabPage 订阅。</summary>
    public event Action? GoToDefinitionRequested;

    /// <summary>
    /// 获取光标位置的词（含 schema. / [] 转义）。
    /// 从 caret 同时向左、右找边界（照顾"光标在词中间"的情况）。
    /// 返回 schema.name / schema.[name] / [schema].[name] / 纯 name 都 OK。
    /// </summary>
    public string GetWordAtCursor()
    {
        int caret = SelectionStart;
        if (caret < 0 || caret > Text.Length) return "";

        int start = caret;
        while (start > 0)
        {
            char c = Text[start - 1];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '.' || c == '[' || c == ']'))
                break;
            start--;
        }

        int end = caret;
        while (end < Text.Length)
        {
            char c = Text[end];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '.' || c == '[' || c == ']'))
                break;
            end++;
        }

        if (start == end) return "";
        return Text.Substring(start, end - start);
    }

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
        
        // 光标位置改变：如果新位置不在当前正在输入的单词范围内 → 隐藏联想框
        if (_intelliSense.IsVisible)
        {
            int caret = SelectionStart;
            string word = GetCurrentWord();
            int wordStart = GetCurrentWordStart();
            
            // 判断：新位置是否在 [wordStart, wordStart+word.Length] 范围内
            // 如果不在 → 隐藏
            if (word.Length == 0 || caret < wordStart || caret > wordStart + word.Length)
            {
                _intelliSense.Hide();
            }
        }
    }

    protected override void OnVScroll(EventArgs e)
    {
        base.OnVScroll(e);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Ctrl+F = 查找
        if (e.Control && e.KeyCode == Keys.F && !e.Shift && !e.Alt)
        {
            ShowSearchReplace(false);
            e.SuppressKeyPress = true;
            return;
        }
        // Ctrl+H = 替换
        if (e.Control && e.KeyCode == Keys.H && !e.Shift && !e.Alt)
        {
            ShowSearchReplace(true);
            e.SuppressKeyPress = true;
            return;
        }
        // F12 = 转到定义
        if (e.KeyCode == Keys.F12 && !e.Control && !e.Shift && !e.Alt)
        {
            GoToDefinitionRequested?.Invoke();
            e.SuppressKeyPress = true;
            return;
        }

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
            HandleEnterWithIndent(e);
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>
    /// 回车自动缩进：1) 先记住上一行的缩进文本，2) base.OnKeyDown 处理换行，
    /// 3) 插入缩进文本。
    /// 修复：之前在 base.OnKeyDown 后再调 e.SuppressKeyPress 会不起作用，且 base.OnKeyDown
    /// 返回后状态可能错乱。改为一个干净的实现（2026-07-07）。
    /// </summary>
    private void HandleEnterWithIndent(KeyEventArgs e)
    {
        // 1. 计算上一行缩进
        int caretBefore = SelectionStart;
        int lineIdxBefore = GetLineFromCharIndex(caretBefore);
        string indent = "";
        bool needExtraIndent = false;
        if (lineIdxBefore > 0)
        {
            int prevLineStart = GetFirstCharIndexFromLine(lineIdxBefore - 1);
            int curLineStart = GetFirstCharIndexFromLine(lineIdxBefore);
            int prevLineLen = curLineStart - prevLineStart;
            if (prevLineLen > 0)
            {
                string prevLineText = Text.Substring(prevLineStart, prevLineLen);
                foreach (char c in prevLineText)
                {
                    if (c == ' ' || c == '\t') indent += c;
                    else break;
                }
                var trimmed = prevLineText.TrimEnd();
                needExtraIndent =
                    trimmed.EndsWith("BEGIN", StringComparison.OrdinalIgnoreCase)
                    || trimmed.EndsWith("IF ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.EndsWith("WHILE ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.EndsWith("CASE ", StringComparison.OrdinalIgnoreCase)
                    || trimmed.EndsWith("(", StringComparison.Ordinal);
                if (needExtraIndent) indent += "    ";
            }
        }

        // 2. 默认行为（处理换行）
        base.OnKeyDown(e);
        // 让 base.OnKeyDown 吃掉这个 key，后续不再处理
        e.SuppressKeyPress = true;

        // 3. 插入缩进
        if (!string.IsNullOrEmpty(indent) && !IsDisposed && IsHandleCreated)
        {
            try
            {
                _suppressHighlight = true;
                _suppressIntelliSense = true;
                SelectedText = indent;
            }
            catch { /* 控件可能正在销毁 */ }
            finally
            {
                _suppressHighlight = false;
                _suppressIntelliSense = false;
            }
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
        int caret = SelectionStart;

        // 上下文检测：EXEC / SELECT / FROM 等关键字后空格 → 即使 word="" 也要弹
        // 陛下反馈：“EXEC 空格后没联想”、“SELECT * 后不输表名.也没联想” 都是这个原因。
        // 原逻辑 word.Length<1 直接 Hide，导致空白后不会弹。
        var ctx = SqlIntelliSenseProvider.DetectContext(Text, caret);
        bool isStrongContext =
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterExec ||
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword ||
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword;

        // 不弹的条件：word 空 且 不在强上下文
        if (word.Length < 1 && !isStrongContext)
        {
            _intelliSense.Hide();
            return;
        }

        var matches = SqlIntelliSenseProvider.GetSuggestions(word, ConnectionString, Text, caret, 80).ToList();
        if (matches.Count == 0)
        {
            _intelliSense.Hide();
            return;
        }

        // 计算 popup 屏幕位置：在光标所在行的"下一行"顶部，X坐标对准当前光标位置！
        // ────────────────────────────────────────────────────────────────
        Point screenPos;
        Point caretPos = GetPositionFromCharIndex(caret);
        int lineIdx = GetLineFromCharIndex(caret);
        int nextLineCharIdx = GetFirstCharIndexFromLine(lineIdx + 1);
        if (nextLineCharIdx >= 0)
        {
            // 有下一行：用下一行首字符Y + 当前光标X
            Point nextLinePos = GetPositionFromCharIndex(nextLineCharIdx);
            screenPos = PointToScreen(new Point(caretPos.X, nextLinePos.Y));
        }
        else
        {
            // 已经在最后一行：用当前光标位置X + 该行行高（Font.Height 是真正的行高）
            int lineHeight = Font.Height;
            screenPos = PointToScreen(new Point(caretPos.X, caretPos.Y + lineHeight + 2));
        }

        // 陛下反馈：popup 宽应按内容自适应。传 width=0 → ShowNearCaret 内部自动量。
        _intelliSense.ShowNearCaret(this, screenPos, 0, 280, matches);
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
        // 保存滚动位置
        int vScroll = GetScrollPos(Handle, SB_VERT);
        int hScroll = GetScrollPos(Handle, SB_HORZ);

        _suppressHighlight = true;
        // 冻结重绘 → 防 RichTextBox 闪烁（仅窗口被冻结，不影响其他控件）
        SendMessage(Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
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
            // 恢复滚动位置
            SetScrollPos(Handle, SB_VERT, vScroll, false);
            SetScrollPos(Handle, SB_HORZ, hScroll, false);
            SendMessage(Handle, WM_VSCROLL, (IntPtr)(SB_THUMBPOSITION | (vScroll << 16)), IntPtr.Zero);
            SendMessage(Handle, WM_HSCROLL, (IntPtr)(SB_THUMBPOSITION | (hScroll << 16)), IntPtr.Zero);

            ResumeLayout();
            _suppressHighlight = false;
            // 解冻重绘 + 主动 Invalidate：让富文本一次性画出，跳过中间状态
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
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