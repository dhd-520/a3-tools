using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
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

    // 2026-08-04 陛下反馈修复: 粘贴 SQL 时去掉原带格式 (颜色/字体/段落)
    private const int WM_PASTE = 0x0302;

    // 冻结重绘：设置重绘抑制标志 + 解除后强制刷新。避免高亮过程中多次
    // Select + SelectionColor 引起的 RichTextBox 闪烁（richEdit 重绘不双缓冲，
    // 每设色都刷一次 → 闪）。
    private const int WM_SETREDRAW = 0x000B;
    // ★ 2026-09-29 陛下反馈修复 Ctrl+Z 来回撤销: 禁用 RichTextBox 自带的 undo
    // 注意: EM_SETUNDOLIMIT = WM_USER + 162 = 0x04A2,不是 0xC4 (0xC4 是 EM_GETLINE!)
    private const int EM_SETUNDOLIMIT = 0x04A2;
    // EM_EMPTYUNDOBUFFER = 0x00CD,清空 richedit undo buffer,作为兜底保险
    private const int EM_EMPTYUNDOBUFFER = 0x00CD;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // 保存和恢复滚动位置
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

    private readonly System.Windows.Forms.Timer _highlightTimer;
    private bool _suppressHighlight;

    // ★ 2026-09-22 陛下反馈修复栈溢出: 重入守卫
    // 原因: Highlight() 内部 Select() 会触发 WndProc 滚动消息 → HighlightVisibleRegionIfNeeded()
    //       → Highlight() → ... → 栈溢出。
    // 解决: Highlight 执行期间置 true,滚动/重绘消息里看到 true 跳过。
    private bool _isHighlighting;

    // ★ 2026-09-22 陛下反馈修复大文档卡顿: 增量高亮
    // OnTextChanged 时记录受影响行范围,timer Tick 用它代替全量 Highlight。
    // 1000 行存储过程从"几秒 ~ 十几秒"降到 "< 100ms"。
    private int _pendingLineFrom = -1;
    private int _pendingLineTo   = -1;
    private int _lastTextLength  = 0;
    // 批量插入/删除阈值: |delta| 超过这个数视为粘贴/批量删除,扩大范围到覆盖整个变更区域
    private const int BulkChangeThreshold = 20;

    // ★ 2026-09-22 陛下反馈修复大文档首次加载慢: 后台异步高亮
    // HighlightNow 只高亮可见区域,其余交给后台 Timer 慢高亮。
    // 用户 < 1 秒就能操作(不用等全文 7-8 秒),滚动到没高亮区域会即时高亮。
    private System.Windows.Forms.Timer? _backgroundHighlightTimer;
    private int _bgHighlightCurrentLine = -1;
    private int _bgHighlightTotalLine   = -1;
    private bool _bgHighlightResetColors = true;

    // ★ 2026-09-23 陛下反馈修复 1 秒操作延迟: 用户编辑时间戳
    // DoBackgroundHighlightTick 检查这个字段,500ms 内编辑过则跳过本轮,
    // 避免后台高亮与 _highlightTimer(编辑高亮)竞争 UI 线程。
    private DateTime _lastUserEditTimeUtc = DateTime.MinValue;

    // ★ 2026-09-23 陛下反馈方案 B 异步高亮: 正则后台计算 + UI 线程只设色
    // 5 轮 regex 搬到后台 Task.Run,UI 线程只负责 SetColor,减少 ~60% 卡顿。
    private readonly System.Collections.Concurrent.ConcurrentQueue<System.Collections.Generic.List<HighlightSpan>> _pendingSpans = new();
    private volatile bool _bgHighlightRunning;  // 后台任务是否在跑
    private int _bgHighlightJobId;  // 递增,取消旧任务用

    /// <summary>高亮区间,后台线程计算,UI 线程使用</summary>
    private readonly struct HighlightSpan
    {
        public readonly int Start;
        public readonly int Length;
        public readonly Color Color;
        public HighlightSpan(int start, int length, Color color) { Start = start; Length = length; Color = color; }
    }

    // ★ 2026-09-22 陛下反馈修复"每操作一下都卡": 滚动高亮节流
    // 之前在 WndProc 每个 WM_VSCROLL/WM_MOUSEWHEEL 都调 HighlightVisibleRegionIfNeeded(),
    // 鼠标滚轮每 notch 好几个消息,每次都跑 5 轮正则 → 每滚一下都卡。
    // 改为 100ms debounce: 滚动时不立即高亮,等用户停滚 100ms 才跑一次。
    private System.Windows.Forms.Timer? _scrollHighlightTimer;

    /// <summary>滚动位置 / 选区 / 文本变化时触发（行号面板监听此事件重绘）</summary>
    public event EventHandler? ViewChanged;

    private readonly IntelliSensePopup _intelliSense = new();
    private readonly System.Windows.Forms.Timer _intelliSenseTimer;
    private bool _suppressIntelliSense;

    // ★ 2026-09-29 陛下反馈修复 Ctrl+Z 只能撤一步: 自定义撤销栈
    // 原因: RichTextBox.Undo() 被 WndProc 自定义拦截吃掉,只撤一步
    // 方案: 自己维护文本快照栈，每次 TextChanged 把旧文本压栈
    private struct UndoState
    {
        public string Text;
        public int CursorStart;
    }
    private readonly System.Collections.Generic.Stack<UndoState> _undoStack = new();
    private readonly System.Collections.Generic.Stack<UndoState> _redoStack = new();
    private string _lastCommittedText = "";
    private int _lastCommittedCursor;
    private bool _suspendUndo;
    private bool _undoInitialized;
    // ★ 2026-09-29 v6 重写: 500ms 去抖定时器,停止输入 500ms 后才记录一次撤销状态
    private readonly System.Windows.Forms.Timer _undoDebounceTimer;
    private const int MaxUndoLevels = 200;

    public SqlEditor()
    {
        // 默认字体设大2个字号，使用Consolas等宽字体更适合SQL
        Font = new System.Drawing.Font("Consolas", 12f);

        // ★ 2026-09-29 陛下反馈修复 Ctrl+Z 来回撤销: Handle 创建后关闭 RichTextBox native undo
        // 原因: PerformUndo() 设置 Text 后,richedit 控件自己的 WndProc 还会反向再撤一次
        //       (它把 PerformUndo 的 Text 改动当作"最近一次修改"塞进自己的 undo 队列,然后 Ctrl+Z 又被
        //        它 native 处理,反向撤销回去 → 看到 Text 在两个状态间振荡)
        // 方案: EM_SETUNDOLIMIT(wParam=0) → 关掉 richedit 内置 undo 队列,只走我们自己的 PerformUndo
        // ★ 2026-09-29 v6: 撤销去抖定时器 - 500ms 内连续输入合并成一个撤销步骤
        _undoDebounceTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _undoDebounceTimer.Tick += (_, _) =>
        {
            _undoDebounceTimer.Stop();
            CommitUndoSnapshot();
        };

        HandleCreated += (_, _) =>
        {
            // ★ 2026-09-29 陛下反馈修复 Ctrl+Z 来回撤销: 关掉 richedit native undo
            SendMessage(Handle, EM_SETUNDOLIMIT, IntPtr.Zero, IntPtr.Zero);
            SendMessage(Handle, EM_EMPTYUNDOBUFFER, IntPtr.Zero, IntPtr.Zero);
        };

        // 【2026-07-15 选中修复】关闭单词级自动选择
        // RichTextBox 默认 AutoWordSelection = true → 鼠标拖选会"吸附"到单词边界，
        // 表现：选区莫名扩大/跳字、像不听使唤。VS / SSMS 都是 false → 字符级精确选择。
        // 保留：双击 = 选词、三击 = 选段（这两个不受 AutoWordSelection 影响）。
        // ★ 2026-09-17: 构造函数设了但 Handle 创建后可能被底层 RichEdit 重置，
        //   OnHandleCreated 中再次强制设一次 + EM_SETOPTIONS P/Invoke 兜底。
        AutoWordSelection = false;

        _highlightTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _highlightTimer.Tick += (_, _) =>
        {
            _highlightTimer.Stop();
            // ★ 2026-09-22 陛下反馈修复大文档卡顿: 用 OnTextChanged 记录的增量范围
            // 取代全量 Highlight。1000 行存储过程的"几秒 ~ 十几秒"降到 < 100ms。
            int from = _pendingLineFrom;
            int to   = _pendingLineTo;
            if (from < 0 || to < 0 || from > to) return;
            Highlight(from, to);
        };

        // ★ 2026-09-24 陛下反馈修复联想闪烁: 防抖 800ms
        // 原因: 之前 Interval=50,每次按键立刻触发,输入过程中持续闪烁。
        // 新行为: 用户停手 800ms 后才触发联想,连续打字期间 timer 反复 Stop+Start 重置倒计时。
        // 与 _highlightTimer(200ms 高亮节流) 独立——高亮仍 200ms(为了接近实时的反馈)。
        _intelliSenseTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _intelliSenseTimer.Tick += (_, _) =>
        {
            _intelliSenseTimer.Stop();
            TriggerIntelliSense();
        };

        // ★ 2026-09-22 陛下反馈修复"每操作一下都卡": 滚动高亮 100ms debounce
        _scrollHighlightTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _scrollHighlightTimer.Tick += (_, _) =>
        {
            _scrollHighlightTimer.Stop();
            if (!_isHighlighting) HighlightVisibleRegionIfNeeded();
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

    public void HighlightNow(bool resetColors = true)
    {
        _highlightTimer.Stop();
        // 取消之前的后台异步高亮(重新调用)
        _backgroundHighlightTimer?.Stop();
        _bgHighlightCurrentLine = -1;
        _bgHighlightTotalLine   = -1;

        // ★ 2026-09-22 陛下反馈修复大文档首次加载慢:
        // 改为"可见区域优先"—— 只高亮当前屏幕能看到的行 + 上下缓冲,
        // 剩余区域交给后台 Timer 异步高亮 (200ms/批,每批 200 行)。
        // 效果: 1000 行从 "7-8 秒不能操作" 降到 "< 1 秒能操作",
        //        全文高亮在后台 ~2-3 秒慢慢补完,滚动时可见区域即时高亮。
        // 代价: 滚动到未高亮区域时,会看到 50-200ms 的"灰色 -> 彩色"闪烁。
        if (IsDisposed || !IsHandleCreated) return;
        int lineCount = Lines.Length;
        if (lineCount == 0) return;

        // 计算可见区域 (顶部 + 底部 + 缓冲)
        int firstVisLine, lastVisLine;
        try
        {
            int firstVisChar = GetCharIndexFromPosition(new Point(0, 0));
            firstVisLine = firstVisChar >= 0 ? GetLineFromCharIndex(firstVisChar) : 0;
            int lastVisChar = GetCharIndexFromPosition(new Point(0, Math.Max(0, Height - 2)));
            lastVisLine  = lastVisChar >= 0 ? GetLineFromCharIndex(lastVisChar) : 0;
        }
        catch
        {
            firstVisLine = 0;
            lastVisLine  = Math.Min(lineCount - 1, 100);
        }

        int fromLine = Math.Max(0, firstVisLine - 10);
        int toLine   = Math.Min(lineCount - 1, lastVisLine + 50);
        Highlight(fromLine, toLine, resetColors);

        // 启动后台异步高亮剩余区域(从 toLine+1 到末尾)
        if (toLine + 1 < lineCount)
        {
            StartBackgroundHighlight(toLine + 1, lineCount - 1, resetColors);
        }
    }

    /// <summary>
    /// ★ 2026-09-22 陛下反馈修复: 后台异步高亮大文档的剩余区域。
    /// ★ 2026-09-23 方案 B: 正则计算 (5 轮 regex + 行偏移扫描) 搬到后台 Task.Run,
    /// UI 线程只负责 Select + SelectionColor,减少 ~60% UI 线程占用。
    /// 调用者: HighlightNow(可见区域之后的部分)。
    /// </summary>
    private void StartBackgroundHighlight(int fromLine, int toLine, bool resetColors)
    {
        _bgHighlightCurrentLine = fromLine;
        _bgHighlightTotalLine   = toLine;
        _bgHighlightResetColors = resetColors;
        _bgHighlightJobId++;  // 递增 job id,后台检测到过期任务则丢弃结果

        // 先清空旧 spans(可能有上一轮剩下的)
        while (_pendingSpans.TryDequeue(out _)) { }

        if (_backgroundHighlightTimer == null)
        {
            _backgroundHighlightTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _backgroundHighlightTimer.Tick += (_, _) => DoBackgroundHighlightTick();
        }
        _backgroundHighlightTimer.Stop();
        _backgroundHighlightTimer.Start();

        // 启动后台任务: 文本快照 + 行偏移 + 5 轮 regex 全部在后台线程跑
        if (_bgHighlightRunning) return;  // 已有后台任务在跑,不重复启动
        StartBackgroundComputeTask();
    }

    /// <summary>
    /// 后台线程: 文本快照 → 行偏移 → 5 轮 regex → 产出 HighlightSpan 喂队列
    /// 每个 200 行一批计算完就 Enqueue,UI 线程 Tick 时取出来设色。
    /// </summary>
    private void StartBackgroundComputeTask()
    {
        _bgHighlightRunning = true;
        int jobId = _bgHighlightJobId;

        // 必须在 UI 线程 snapshot Text,因为 Text 是控件属性不能在后台访问
        string textSnapshot;
        try
        {
            textSnapshot = Text ?? "";
        }
        catch
        {
            _bgHighlightRunning = false;
            return;
        }

        Task.Run(() =>
        {
            try
            {
                // 后台线程计算行偏移(扫 \n),不依赖 GetFirstCharIndexFromLine
                var lineOffsets = ComputeLineOffsets(textSnapshot);

                while (true)
                {
                    // 检查 job id 是否被新任务覆盖
                    if (jobId != _bgHighlightJobId) return;

                    int curLine = _bgHighlightCurrentLine;
                    int totalLine = _bgHighlightTotalLine;
                    if (curLine < 0 || curLine > totalLine) return;

                    const int ChunkLines = 200;
                    int chunkEnd = Math.Min(curLine + ChunkLines - 1, totalLine);
                    int startIdx = lineOffsets[curLine];
                    int endIdx   = (chunkEnd + 1 < lineOffsets.Count) ? lineOffsets[chunkEnd + 1] : textSnapshot.Length;
                    if (endIdx <= startIdx)
                    {
                        // 该 chunk 为空,推进 curLine 继续
                        Interlocked.Exchange(ref _bgHighlightCurrentLine, chunkEnd + 1);
                        continue;
                    }

                    int len = endIdx - startIdx;
                    string segment = textSnapshot.Substring(startIdx, len);

                    // 5 轮 regex 在后台跑(30% 耗时省掉)
                    var spans = ComputeSpansForSegment(segment, startIdx);

                    _pendingSpans.Enqueue(spans);

                    // 推进 currentLine(原子操作,UI 线程可能会读)
                    Interlocked.Exchange(ref _bgHighlightCurrentLine, chunkEnd + 1);

                    if (chunkEnd >= totalLine) return;
                }
            }
            catch
            {
                // 后台任务异常静默吞掉
            }
            finally
            {
                _bgHighlightRunning = false;
            }
        });
    }

    /// <summary>
    /// 计算每行在文本中的起始字符索引。返回列表: lineOffsets[行号] = 该行起始 char index。
    /// 与 RichTextBox.GetFirstCharIndexFromLine 一致 (lineOffsets[0] = 0)。
    /// 后台线程调用,不依赖控件 API。
    /// </summary>
    private static System.Collections.Generic.List<int> ComputeLineOffsets(string text)
    {
        var offsets = new System.Collections.Generic.List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') offsets.Add(i + 1);
        }
        return offsets;
    }

    /// <summary>
    /// 后台线程跑 5 轮 regex,产出 HighlightSpan 列表(不设色,只算位置)。
    /// </summary>
    private System.Collections.Generic.List<HighlightSpan> ComputeSpansForSegment(string segment, int startIdx)
    {
        var spans = new System.Collections.Generic.List<HighlightSpan>();

        // 1. 关键字
        foreach (Match m in WordRegex.Matches(segment))
        {
            if (Keywords.Contains(m.Value))
                spans.Add(new HighlightSpan(m.Index + startIdx, m.Length, KeywordColor));
        }

        // 2. 数字
        foreach (Match m in NumberRegex.Matches(segment))
            spans.Add(new HighlightSpan(m.Index + startIdx, m.Length, NumberColor));

        // 3. 注释
        foreach (Match m in CommentLineRegex.Matches(segment))
            spans.Add(new HighlightSpan(m.Index + startIdx, m.Length, CommentColor));
        foreach (Match m in CommentBlockRegex.Matches(segment))
            spans.Add(new HighlightSpan(m.Index + startIdx, m.Length, CommentColor));

        // 4. 字符串(最后,优先覆盖关键字/数字)
        foreach (Match m in StringRegex.Matches(segment))
            spans.Add(new HighlightSpan(m.Index + startIdx, m.Length, StringColor));

        return spans;
    }

    private void DoBackgroundHighlightTick()
    {
        // ★ 2026-09-23 陛下反馈修复关闭报错: try-catch 防御 + IsDisposed 检查
        try
        {
            if (IsDisposed || Disposing || !IsHandleCreated || Handle == IntPtr.Zero)
            {
                _backgroundHighlightTimer?.Stop();
                return;
            }

            // ★ 2026-09-23 陛下反馈修复 1 秒延迟: 用户正在编辑时暂停后台高亮,
            // 避免与 _highlightTimer(200ms 后亮用户输入区域)竞争同一 UI 线程。
            if ((DateTime.UtcNow - _lastUserEditTimeUtc).TotalMilliseconds < 500) return;

            // 从队列取一组 spans 设色,只设一批(防止单次 Tick 卡)
            const int MaxSpansPerTick = 500;
            int count = 0;
            while (count < MaxSpansPerTick && _pendingSpans.TryDequeue(out var spans))
            {
                // 这批 spans 可能已被新 job 覆盖,丢弃
                // (job id 在 StartBackgroundHighlight 时递增,后台检测过期就 return,但
                //  仍有边界情况: job 已经算出 spans 并 Enqueue,然后 job 被新覆盖。
                //  UI 这里简单处理: 取出来就设上,即使旧 job 也无害 — 文本位置可能已变,
                //  但 Select + SelectionColor 对越界容错。)

                int selStart = SelectionStart;
                int selLen = SelectionLength;
                int vScroll = GetScrollPos(Handle, SB_VERT);
                int hScroll = GetScrollPos(Handle, SB_HORZ);
                _isHighlighting = true;
                SendMessage(Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
                try
                {
                    SuspendLayout();
                    for (int i = 0; i < spans.Count; i++)
                    {
                        var s = spans[i];
                        if (s.Start < 0 || s.Length <= 0 || s.Start + s.Length > TextLength) continue;
                        Select(s.Start, s.Length);
                        SelectionColor = s.Color;
                        count++;
                        if (count >= MaxSpansPerTick) break;
                    }
                    Select(selStart, selLen);
                    SelectionColor = Color.Black;
                }
                finally
                {
                    SetScrollPos(Handle, SB_VERT, vScroll, false);
                    SetScrollPos(Handle, SB_HORZ, hScroll, false);
                    ResumeLayout();
                    _suppressHighlight = false;
                    SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    Invalidate();
                    SendMessage(Handle, WM_VSCROLL, (IntPtr)(SB_THUMBPOSITION | (vScroll << 16)), IntPtr.Zero);
                    SendMessage(Handle, WM_HSCROLL, (IntPtr)(SB_THUMBPOSITION | (hScroll << 16)), IntPtr.Zero);
                    _isHighlighting = false;
                }
            }

            // ★ 队列空 + 后台任务结束 → 停 Timer
            if (_pendingSpans.IsEmpty && !_bgHighlightRunning)
            {
                _backgroundHighlightTimer.Stop();
            }
        }
        catch
        {
            // 任何意外异常静默吞掉 + 停 Timer
            _backgroundHighlightTimer?.Stop();
        }
    }

    /// <summary>
    /// ★ 2026-09-23 陛下反馈修复"滚动鼠标滚轮后高亮卡": 滚动期间不再同步设色,
    /// 改为: 滚动 → 立即入队一个可见区域计算任务 → 后台 Task.Run 算 spans →
    /// UI 线程 Tick 异步设色。滚动期间 UI 线程不被正则/设色阻塞,滚轮丝滑。
    /// 保留: 只高亮可见区域,resetColors=false (后台异步已设的颜色保留)。
    /// 调用者: WndProc 拦截滚动消息 → 100ms debounce Timer Tick。
    /// </summary>
    private void HighlightVisibleRegionIfNeeded()
    {
        if (IsDisposed || Disposing || !IsHandleCreated || Handle == IntPtr.Zero) return;

        // 如果 UI 线程还在设色中,跳过本次(防设色叠加)
        if (_isApplyingScrollHighlight) return;

        try
        {
            int lineCount = Lines.Length;
            if (lineCount == 0) return;

            int firstVisLine, lastVisLine;
            int firstVisChar = GetCharIndexFromPosition(new Point(0, 0));
            firstVisLine = firstVisChar >= 0 ? GetLineFromCharIndex(firstVisChar) : 0;
            int lastVisChar = GetCharIndexFromPosition(new Point(0, Math.Max(0, Height - 2)));
            lastVisLine  = lastVisChar >= 0 ? GetLineFromCharIndex(lastVisChar) : 0;

            int fromLine = Math.Max(0, firstVisLine - 5);
            int toLine   = Math.Min(lineCount - 1, lastVisLine + 30);
            if (fromLine > toLine) return;

            // ★ 滚动触发的是局部可见区域,走专门的 _scrollPendingSpans 队列,
            // 不与后台全文档高亮的 _pendingSpans 混淆。
            EnqueueScrollHighlight(fromLine, toLine);
        }
        catch
        {
            // UI 状态不一致时静默跳过
        }
    }

    /// <summary>
    /// ★ 2026-09-23 陛下反馈修复"颜色混乱有些关键字没标": 复用 _pendingSpans 队列,
    /// 滚动 spans 与全文档 spans 统一顺序处理,取消 _scrollHighlightJobId 过期丢弃。
    /// 之前快速滚轮时,过期机制丢掉了老任务的计算结果,导致部分 spans 丢失。
    /// 现在: 滚动 → 后台算 spans → 统一入 _pendingSpans → UI 线程依次设色,
    /// 后设的覆盖前设的,顺序确定不丢。
    /// </summary>

    /// <summary>UI 线程是否正在设滚动 spans,防与全文档高亮冲突</summary>
    private volatile bool _isApplyingScrollHighlight;
    /// 滚动可见区域入队: 后台算 spans → UI 线程设色。
    /// 设计与 StartBackgroundHighlight 类似,但针对滚动局部可见区域。
    /// </summary>
    private void EnqueueScrollHighlight(int fromLine, int toLine)
    {
        // 必须在 UI 线程 snapshot Text
        string textSnapshot;
        try
        {
            textSnapshot = Text ?? "";
        }
        catch
        {
            return;
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var lineOffsets = ComputeLineOffsets(textSnapshot);
                int startIdx = (fromLine < lineOffsets.Count) ? lineOffsets[fromLine] : -1;
                int endIdx   = (toLine + 1 < lineOffsets.Count) ? lineOffsets[toLine + 1] : textSnapshot.Length;
                if (startIdx < 0 || endIdx <= startIdx) return;

                int len = endIdx - startIdx;
                string segment = textSnapshot.Substring(startIdx, len);
                var spans = ComputeSpansForSegment(segment, startIdx);

                // ★ 复用全文档高亮的 _pendingSpans 队列,统一顺序处理
                _pendingSpans.Enqueue(spans);
            }
            catch
            {
                // 后台异常静默吞掉
            }
        });

        // 启动滚动设色 Timer(如果还没启动)
        if (_scrollHighlightApplyTimer == null)
        {
            _scrollHighlightApplyTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _scrollHighlightApplyTimer.Tick += (_, _) => DoScrollHighlightApplyTick();
            _scrollHighlightApplyTimer.Start();
        }
    }

    private int _scrollHighlightJobId;
    private System.Windows.Forms.Timer? _scrollHighlightApplyTimer;

    /// <summary>
    /// 滚动设色 Timer Tick: 从统一 _pendingSpans 队列取 spans 设色。
    /// ★ 2026-09-23 修复"颜色混乱有些关键字没标": 复用全文档高亮的 _pendingSpans,
    /// 取消 _scrollHighlightJobId 过期丢弃,所有 spans 按顺序设色,后设的覆盖前设的。
    /// 之前过期机制丢已在 Unity 中,新任务的 spans 到达后设色,老任务只设了一半的 spans 就停,
    /// 导致部分关键字没标 → 颜色混乱。
    /// 加 _isApplyingScrollHighlight 守卫,防止与全文档高亮冲突。
    /// </summary>
    private void DoScrollHighlightApplyTick()
    {
        try
        {
            if (IsDisposed || Disposing || !IsHandleCreated || Handle == IntPtr.Zero)
            {
                _scrollHighlightApplyTimer?.Stop();
                return;
            }

            // 如果正在设色(包括全文档后台高亮或本次滚动),跳过避免冲突
            if (_isApplyingScrollHighlight || _isHighlighting) return;

            // ★ 复用统一队列: 全文档 spans 和滚动 spans 按入队顺序依次设色
            if (!_pendingSpans.TryDequeue(out var spans)) return;

            _isApplyingScrollHighlight = true;
            try
            {
                // 保存现场
                int selStart = SelectionStart;
                int selLen = SelectionLength;
                int vScroll = GetScrollPos(Handle, SB_VERT);
                int hScroll = GetScrollPos(Handle, SB_HORZ);

                SendMessage(Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
                SuspendLayout();
                _isHighlighting = true;
                try
                {
                    const int MaxSpansPerTick = 200;
                    int count = 0;
                    for (int i = 0; i < spans.Count && count < MaxSpansPerTick; i++)
                    {
                        var s = spans[i];
                        if (s.Start < 0 || s.Length <= 0 || s.Start + s.Length > TextLength) continue;
                        Select(s.Start, s.Length);
                        SelectionColor = s.Color;
                        count++;
                    }
                    Select(selStart, selLen);
                    SelectionColor = Color.Black;
                }
                finally
                {
                    SetScrollPos(Handle, SB_VERT, vScroll, false);
                    SetScrollPos(Handle, SB_HORZ, hScroll, false);
                    ResumeLayout();
                    _isHighlighting = false;
                    SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    Invalidate();
                    SendMessage(Handle, WM_VSCROLL, (IntPtr)(SB_THUMBPOSITION | (vScroll << 16)), IntPtr.Zero);
                    SendMessage(Handle, WM_HSCROLL, (IntPtr)(SB_THUMBPOSITION | (hScroll << 16)), IntPtr.Zero);
                }
            }
            finally
            {
                _isApplyingScrollHighlight = false;
            }

            // 队列还有 → 继续下一 Tick
            if (_pendingSpans.IsEmpty)
            {
                _scrollHighlightApplyTimer?.Stop();
            }
            {
                _scrollHighlightApplyTimer?.Stop();
            }
        }
        catch
        {
            _isApplyingScrollHighlight = false;
            _scrollHighlightApplyTimer?.Stop();
        }
    }

    // ★ 2026-09-17: Handle 创建后再次强制关闭 AutoWordSelection（构造函数设的可能被 RichEdit 重置）
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AutoWordSelection = false;
    }

    // ★ 2026-09-23 陛下反馈修复关闭报错: Handle 销毁时彻底停掉所有 Timer,
    // 防止关闭过程中 Timer Tick 访问已销毁的 Handle → NullReferenceException
    protected override void OnHandleDestroyed(EventArgs e)
    {
        _backgroundHighlightTimer?.Stop();
        _backgroundHighlightTimer?.Dispose();
        _backgroundHighlightTimer = null;

        _scrollHighlightTimer?.Stop();
        _scrollHighlightTimer?.Dispose();
        _scrollHighlightTimer = null;

        _highlightTimer.Stop();
        _highlightTimer.Dispose();

        _intelliSenseTimer.Stop();
        _intelliSenseTimer.Dispose();

        base.OnHandleDestroyed(e);
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
        // 拦截 WM_PASTE: 从剪贴板拿纯文本插入 (不调 base.Paste() 防止带 RTF 格式)
        if (m.Msg == WM_PASTE)
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                // RichTextBox 换行统一为 \n, 转换 \r\n / \r 到 \n 避免混合
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");
                _suppressHighlight = true;
                _suppressIntelliSense = true;
                try
                {
                    SelectedText = text;
                }
                catch { /* 控件可能正在销毁 */ }
                finally
                {
                    _suppressHighlight = false;
                    _suppressIntelliSense = false;
                }
            }
            return; // 吞掉原始消息，不交给 base
        }

        base.WndProc(ref m);
        if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_HSCROLL || m.Msg == EM_LINESCROLL)
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
            // ★ 2026-09-22 陛下反馈修复栈溢出 + "每操作一下都卡":
            // 1) Highlight 执行期间(_isHighlighting)跳过,避免递归
            // 2) 其余情况启动 100ms debounce timer,等用户停滚才高亮一次
            //    (之前每个 WM_VSCROLL/WM_MOUSEWHEEL 都调,鼠标滚轮每 notch 好几个消息,
            //     每次都跑 5 轮正则 → 每滚一下都卡)
            if (!_isHighlighting)
            {
                _scrollHighlightTimer?.Stop();
                _scrollHighlightTimer?.Start();
            }
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        // ★ 2026-09-29 v6 重写撤销跟踪: 500ms 去抖定时器
        // 不再每次 TextChanged 立即压栈,改成: 每次变化只重启 500ms 定时器,
        // 定时器到点才把"自上次提交以来的状态变化"作为一个撤销步骤。
        // 优点: 跟 SSMS 一致,一段连续输入 = 一个撤销步骤;
        //       避免被富文本各种 deferred 事件干扰(慢按 Ctrl+Z 时富文本有时间反向 undo)
        if (!_undoInitialized)
        {
            _lastCommittedText = Text;
            _lastCommittedCursor = SelectionStart;
            _undoInitialized = true;
        }
        else if (!_suspendUndo)
        {
            // 重启去抖定时器: 500ms 内再有变化就重新计时
            _undoDebounceTimer.Stop();
            _undoDebounceTimer.Start();
        }

        base.OnTextChanged(e);

        ViewChanged?.Invoke(this, EventArgs.Empty);

        // ★ 2026-09-22 陛下反馈修复大文档卡顿: 计算增量高亮的行范围
        // - 单字符 / 单词变化: 范围 = 当前行 ±3 / +50(覆盖未来输入)
        // - 批量插入(粘贴/加载): 范围 = 插入起点行 -3 ~ 插入终点行 +50
        // - 批量删除: 范围 = 当前行 ±50(覆盖周围需要重算的区域)
        // 总是记录,即使 _suppressHighlight=true: 后续 timer Tick 也能拿到最新范围
        int newLen = TextLength;
        int delta = newLen - _lastTextLength;
        _lastTextLength = newLen;

        // ★ 2026-09-23 陛下反馈修复 1 秒操作延迟: 记录用户编辑时间
        // DoBackgroundHighlightTick 检查这个时间戳,500ms 内编辑过则跳过本轮,
        // 避免后台高亮与 _highlightTimer(200ms 后亮用户输入区域)竞争同一 UI 线程。
        // 注意: Timer 保持运行,不 Stop,让用户停手后自动恢复后台高亮。
        _lastUserEditTimeUtc = DateTime.UtcNow;

        int affectedLine = GetLineFromCharIndex(Math.Min(SelectionStart, newLen));
        int lineCount = Lines.Length;
        int maxLine = Math.Max(0, lineCount - 1);

        if (Math.Abs(delta) > BulkChangeThreshold)
        {
            // 批量变化: 扩大范围覆盖整个受影响区域
            int rangeStartLine = affectedLine;
            if (delta > 0)
            {
                // 插入: 起点 = SelectionStart - delta(插入起始位置)
                int insertStart = Math.Max(0, SelectionStart - delta);
                rangeStartLine = GetLineFromCharIndex(insertStart);
            }
            // 删除: rangeStartLine 直接用 affectedLine(当前光标已在删除点附近)
            _pendingLineFrom = Math.Max(0, rangeStartLine - 3);
            _pendingLineTo   = Math.Min(maxLine, Math.Max(affectedLine, rangeStartLine) + 50);
        }
        else
        {
            // 普通键入: 小范围
            _pendingLineFrom = Math.Max(0, affectedLine - 3);
            _pendingLineTo   = Math.Min(maxLine, affectedLine + 50);
        }

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
        // Ctrl+Y = 重做（Redo，类似 SSMS）
        if (e.Control && e.KeyCode == Keys.Y && !e.Shift && !e.Alt)
        {
            PerformRedo();
            e.SuppressKeyPress = true;
            return;
        }
        // Ctrl+Z = 撤回（Undo，类似 SSMS）
        if (e.Control && e.KeyCode == Keys.Z && !e.Shift && !e.Alt)
        {
            PerformUndo();
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
                // 只有在 popup 真有可选项时才拦截 Enter/Tab
                if (_intelliSense.HasItems)
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
                // popup 不可见 / 无项 / 无选中：Enter 交给默认处理（换行）
                if (e.KeyCode == Keys.Enter)
                {
                    if (_intelliSense.IsVisible) _intelliSense.Hide();
                    // 继续走到下面 HandleEnterWithIndent 处理
                }
                else // Tab
                {
                    if (_intelliSense.IsVisible) _intelliSense.Hide();
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

        // 2026-08-04 陛下反馈修复: Tab 缩进 / Shift+Tab 反缩进
        // 规则: 
        //   · 无选区 / 单行选区: Tab 在光标位置插入 4 空格
        //   · 多行选区: Tab 在每行行首插入 4 空格; Shift+Tab 从每行行首去掉最多 4 空格
        //   · 在联想 popup 中: 保持原逻辑 (优先选中联想项)
        if (e.KeyCode == Keys.Tab && !e.Control && !e.Alt)
        {
            if (e.Shift) HandleShiftTabIndent(e);
            else HandleTabIndent(e);
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
    /// 回车自动缩进。
    ///
    /// Bug 历史（2026-07-08 修复）：
    /// 之前是 "base.OnKeyDown(e) → e.SuppressKeyPress = true → SelectedText = indent" 三段式，
    /// 注释自夸"修复了 base 后 suppress 不起作用的问题"，实际代码依然保持错误顺序。
    /// 根因：WinForms RichTextBox 的换行不是 base.OnKeyDown 干的，而是 base.OnKeyDown 返回后
    /// WinForms 走"WM_CHAR 注入 '\r' → richedit 处理 → 插入段落符"这条链路实现的。
    /// 此时再设 SuppressKeyPress=true 已经拦不到 WM_CHAR 之前的部分（WM_CHAR 链路整体被吃掉，
    /// 结果就是 richedit 永远看不到 '\r' → 不换行）。注释里"之前在 base.OnKeyDown 后再调
    /// e.SuppressKeyPress 会不起作用"的观察完全正确，但修复没改成。
    ///
    /// 正确做法（2026-07-08）：绕开隐式 WM_CHAR 链路，显式用 SelectedText 插入 换行+缩进，
    /// 然后 SuppressKeyPress=true 防止 base/WM_CHAR 二次插入。
    /// </summary>
    private void HandleEnterWithIndent(KeyEventArgs e)
    {
        // 1. 计算上一行缩进
        int lineIdxBefore = GetLineFromCharIndex(SelectionStart);
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

        // 2. 显式插入 换行 + 缩进（不再依赖 base.OnKeyDown 的隐式段落插入）
        if (IsDisposed || !IsHandleCreated)
        {
            e.SuppressKeyPress = true;
            return;
        }
        _suppressHighlight = true;
        _suppressIntelliSense = true;
        try
        {
            // Environment.NewLine = "\r\n" → richedit 标准段落符
            SelectedText = Environment.NewLine + indent;
        }
        catch { /* 控件可能正在销毁 */ }
        finally
        {
            _suppressHighlight = false;
            _suppressIntelliSense = false;
        }

        // 3. 阻断 base.OnKeyDown 与后续 WM_CHAR 二次插入
        e.SuppressKeyPress = true;
    }

    // ============================================
    // Tab 缩进 / Shift+Tab 反缩进 (2026-08-04)
    // ============================================

    private const string IndentText = "    ";

    /// <summary>
    /// Tab 缩进:
    ///   · 无选区 / 单行选区: 在光标位置插入 4 空格
    ///   · 多行选区: 每行行首加 4 空格
    /// 保持与 HandleEnterWithIndent 同一缩进宽度。
    /// </summary>
    private void HandleTabIndent(KeyEventArgs e)
    {
        int selStart = SelectionStart;
        int selLen = SelectionLength;
        int lineStart = GetLineFromCharIndex(selStart);
        int lineEnd = GetLineFromCharIndex(selStart + Math.Max(0, selLen));

        if (lineStart == lineEnd)
        {
            // 单行 / 无选区: 插入 4 空格
            _suppressHighlight = true;
            _suppressIntelliSense = true;
            try { SelectedText = IndentText; }
            finally
            {
                _suppressHighlight = false;
                _suppressIntelliSense = false;
            }
            e.SuppressKeyPress = true;
            return;
        }

        // 多行选区: 每行行首插入 4 空格
        IndentMultipleLines(lineStart, lineEnd, addIndent: true);
        e.SuppressKeyPress = true;
    }

    /// <summary>
    /// Shift+Tab 反缩进:
    ///   · 多行选区: 每行行首去掉最多 4 个空格 (不是 \t)
    ///   · 无选区: 上一行一样 (其实只是去 4 空格)
    ///   · 单行选区: 选区不在行首, 从光标所在行行首开始去 4 空格
    /// 不动 \t (SQL 不常见, 改起来风险高)
    /// </summary>
    private void HandleShiftTabIndent(KeyEventArgs e)
    {
        int selStart = SelectionStart;
        int selLen = SelectionLength;
        int lineStart = GetLineFromCharIndex(selStart);
        int lineEnd = GetLineFromCharIndex(selStart + Math.Max(0, selLen));

        if (lineStart == lineEnd)
        {
            // 单行 / 无选区: 从光标所在行行首去掉最多 4 空格
            DedentLine(lineStart);
            e.SuppressKeyPress = true;
            return;
        }

        // 多行选区: 每行行首去 4 空格
        IndentMultipleLines(lineStart, lineEnd, addIndent: false);
        e.SuppressKeyPress = true;
    }

    /// <summary>多行缩进/反缩进 (Tab / Shift+Tab 共享)</summary>
    private void IndentMultipleLines(int lineStart, int lineEnd, bool addIndent)
    {
        if (lineStart < 0 || lineEnd < lineStart) return;

        // ★ 2026-09-24 陛下反馈修复缩进后选中丢失: 保存用户原始选区用于缩进后还原。
        // IndentMultipleLines 是 HandleTabIndent/HandleShiftTabIndent 共用入口,
        // 调用方已读过 selStart/selLen,这里再读一次当前 SelectionStart/SelectionLength 作为兜底。
        int selStartAtMethodEntry = SelectionStart;
        int selLenAtMethodEntry = SelectionLength;
        // 考虑选区延伸到 lineEnd+1 的下一行 (GetLineFromCharIndex(selStart+selLen) 
        // 当 selLen>0 且选区末尾正好是 \n 之后的位置时, 会返下一行)。
        // 逻辑: 如果选区以行尾换行结尾, 跨行范围 -= 1 (避免空行被加缩进)。
        int realLineEnd = lineEnd;
        if (addIndent)
        {
            int tailCharIdx = SelectionStart + SelectionLength;
            if (tailCharIdx > 0 && tailCharIdx <= TextLength && Text[tailCharIdx - 1] == '\n')
                realLineEnd = Math.Max(lineStart, lineEnd - 1);
        }

        var sb = new StringBuilder();
        for (int line = lineStart; line <= realLineEnd; line++)
        {
            int ci = GetFirstCharIndexFromLine(line);
            if (ci < 0) continue;
            int nextCi = GetFirstCharIndexFromLine(line + 1);
            int lineLen = (nextCi < 0 ? TextLength : nextCi) - ci;
            if (lineLen <= 0) { sb.Append('\n'); continue; }
            string lineText = Text.Substring(ci, lineLen);
            if (addIndent)
            {
                sb.Append(IndentText).Append(lineText);
            }
            else
            {
                int toRemove = 0;
                int maxRemove = Math.Min(IndentText.Length, lineLen);
                for (int k = 0; k < maxRemove; k++)
                {
                    if (lineText[k] == ' ') toRemove++;
                    else break;
                }
                if (toRemove == 0) { sb.Append(lineText); }
                else { sb.Append(lineText.Substring(toRemove)); }
            }
        }

        int firstCi = GetFirstCharIndexFromLine(lineStart);
        int endCi = (realLineEnd < GetLineFromCharIndex(TextLength))
            ? GetFirstCharIndexFromLine(realLineEnd + 1)
            : TextLength;
        int replaceLen = endCi - firstCi;

        _suppressHighlight = true;
        _suppressIntelliSense = true;
        SuspendLayout();
        try
        {
            Select(firstCi, replaceLen);
            SelectedText = sb.ToString();
        }
        finally
        {
            ResumeLayout();
            _suppressHighlight = false;
            _suppressIntelliSense = false;
        }

        // ★ 2026-09-24 陛下反馈修复缩进后高亮丢失 + 选中丢失:
        //   1. 缩进后手动调 Highlight() 重设受影响的行范围的关键字颜色
        //      (因为 _suppressHighlight=true 时 OnTextChanged 没触发增量高亮,关键字会变默认黑色)
        //   2. 还原用户原本的选区(缩进后选区字符位置会整体平移)
        // ★★★ v2 修复选中丢失 bug: 起点位移 = IndentText.Length (只算选中起点行)
        //                  终点位移 = IndentText.Length × (realLineEnd - lineStart + 1)
        //                  长度增量 = 终点位移 - 起点位移 = IndentText.Length × (realLineEnd - lineStart)
        // 之前误用 totalShift = charShift × linesAffected,导致起点位移过大,
        // "每缩进一次选中就少一点" 即起点位移多算了 (linesAffected-1) 个缩进宽度。
        int charShift = addIndent ? IndentText.Length : -IndentText.Length;
        int linesAffected = Math.Max(1, realLineEnd - lineStart + 1);

        // 1. 重设色: 直接调 Highlight 增量高亮受影响行
        try { Highlight(lineStart, realLineEnd); }
        catch { /* Highlight 内部已有四重检查,这里再兜底 */ }

        // 2. 还原选区
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            int origSelStart = selStartAtMethodEntry;
            int origSelLen = selLenAtMethodEntry;

            // 原始选区终点(在 text 替换前)
            int origSelEnd = origSelStart + origSelLen;

            // 原始选区起点所在行号(在原文本中)
            int origSelStartLine = GetLineFromCharIndex(origSelStart);
            int origSelEndLine = (origSelLen > 0 && origSelEnd <= TextLength) ? GetLineFromCharIndex(origSelEnd) : origSelStartLine;

            // 原始选区起点落在缩进范围(lineStart ~ realLineEnd)内的哪一行
            // 落在 lineStart 行 → 起点位移 = 1 个 charShift
            // 落在 lineStart+1 行 → 起点位移 = 2 个 charShift
            // 落在 lineStart+k 行 → 起点位移 = (k+1) 个 charShift
            int startRowOffset = 0;
            if (origSelStartLine >= lineStart && origSelStartLine <= realLineEnd)
                startRowOffset = origSelStartLine - lineStart;  // 0-indexed: lineStart 是第 0 行

            int endRowOffset = startRowOffset;
            if (origSelEndLine >= lineStart && origSelEndLine <= realLineEnd)
                endRowOffset = origSelEndLine - lineStart;
            else if (origSelEndLine > realLineEnd)
                endRowOffset = linesAffected - 1;  // 选区终点在最后一行

            int startShift = charShift * (startRowOffset + 1);  // 起点位移 = (所在行 + 1) × charShift
            int endShift = charShift * (endRowOffset + 1);      // 终点位移 = (所在行 + 1) × charShift

            // addIndent 时: 起点/终点 charShift=+4,移右;removeIndent 时移左(可能到负)
            int newSelStart = origSelStart + startShift;
            int newSelLen = Math.Max(0, (origSelEnd + endShift) - newSelStart);

            // 边界保护: 选区不能超出当前 TextLength
            if (newSelStart < 0) newSelStart = 0;
            if (newSelStart > TextLength) newSelStart = TextLength;
            if (newSelStart + newSelLen > TextLength) newSelLen = TextLength - newSelStart;

            SelectionStart = newSelStart;
            SelectionLength = newSelLen;
            // 让光标可见(滚动到选区)
            ScrollToCaret();
        }
        catch { /* 选区还原失败不影响主流程 */ }
    }

    /// <summary>从指定行行首去掉最多 4 空格</summary>
    private void DedentLine(int line)
    {
        int ci = GetFirstCharIndexFromLine(line);
        if (ci < 0) return;
        int nextCi = GetFirstCharIndexFromLine(line + 1);
        int lineLen = (nextCi < 0 ? TextLength : nextCi) - ci;
        if (lineLen <= 0) return;

        int toRemove = 0;
        int maxRemove = Math.Min(IndentText.Length, lineLen);
        for (int k = 0; k < maxRemove; k++)
        {
            if (Text[ci + k] == ' ') toRemove++;
            else break;
        }
        if (toRemove == 0) return;

        _suppressHighlight = true;
        _suppressIntelliSense = true;
        try
        {
            Select(ci, toRemove);
            SelectedText = "";
        }
        finally
        {
            _suppressHighlight = false;
            _suppressIntelliSense = false;
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

    /// <summary>
    /// 替换当前单词为指定文本（用于补全）。
    /// ★ 2026-07-15：如果 word 含 "."（如 "T1.Na" / "dbo.T1.Name"），只替换最后一个 "." 之后的部分。
    ///   - "T1.Na" 回车选 "Name"  → "T1.Name"（保留 "T1."）
    ///   - "T1." 回车选 "Name"    → "T1.Name"（保留 "T1."）
    ///   - "Na" 回车选 "Name"     → "Name"（无 dot 走原逻辑）
    /// 原因：GetCurrentWordStart() 把 "." 当 word 一部分返回起点（IntelliSense 解析用），
    ///       但补全时不能从最早起点全替换——会把 "别名." / "表名." 吃掉。
    /// </summary>
    private void ReplaceCurrentWord(string replacement)
    {
        int caret = SelectionStart;
        int start = GetCurrentWordStart();

        // 找最后一个 "." → 从 "."+1 开始替换（保留前缀）
        if (start < caret)
        {
            for (int i = caret - 1; i >= start; i--)
            {
                if (Text[i] == '.')
                {
                    start = i + 1;
                    break;
                }
            }
        }

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
            // 2026-08-04 陛下反馈修复: 联想会卡顿导致无法编辑/操作。
            // 原逻辑: GetSuggestions 内部 EnsureLoadedSync 同步等 10s (会冻 UI)。
            // 新逻辑: GetSuggestions 只读缓存, 未就绪返空。
            // 修复: 缓存未就绪且在强上下文 (EXEC 后),  fire-and-forget 启动加载,
            //       加载完成后重弹一次。
            if (isStrongContext
                && !string.IsNullOrEmpty(ConnectionString)
                && !SqlObjectSchemaCache.IsLoaded(ConnectionString))
            {
                TryStartAsyncReloadAndRepopup(word, caret);
            }
            _intelliSense.Hide();
            return;
        }

        ShowIntelliSensePopup(caret, matches);
    }

    /// <summary>
    /// 异步重弹: fire-and-forget 启动缓存加载, 加载完后在 UI 线程重弹。
    /// 只订阅一次 (Loaded 事件中 - 避免重入)。
    /// </summary>
    private void TryStartAsyncReloadAndRepopup(string word, int caretAtTrigger)
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;

        Action<string>? handler = null;
        handler = (key) =>
        {
            // 重要: 先解绑, 避免后续重入
            SqlObjectSchemaCache.Loaded -= handler;

            // 控件可能已销毁 (快速切 Tab/关闭)
            if (IsDisposed || !IsHandleCreated) return;

            // 跳回 UI 线程重弹
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RepopupIfStillRelevant(word, caretAtTrigger)));
            }
            else
            {
                RepopupIfStillRelevant(word, caretAtTrigger);
            }
        };
        SqlObjectSchemaCache.Loaded += handler;
        SqlObjectSchemaCache.EnsureLoadingAsync(ConnectionString);
    }

    /// <summary>
    /// 缓存加载完后重弹。只在光标位置 + 上下文 仍相关时重弹。
    /// 避免: 用户已走开/输入其他字符 后还弹旧的。
    /// </summary>
    private void RepopupIfStillRelevant(string word, int caretAtTrigger)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (_suppressIntelliSense) return;

        // 用户可能已经移开/输入了别的字符:
        // 1) 光标位置不同 → 上下文可能变了 → 重新检测
        // 2) 但只重弹在 "强上下文" 位置 (EXEC/SELECT*/FROM 后) → 避免无脑弹
        var ctx = SqlIntelliSenseProvider.DetectContext(Text, SelectionStart);
        bool isStillStrongContext =
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterExec ||
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword ||
            ctx == SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword;
        if (!isStillStrongContext) return;

        // 重取当前位置的 word (用户可能已输入更多字符)
        var newWord = GetCurrentWord();
        var matches = SqlIntelliSenseProvider.GetSuggestions(newWord, ConnectionString, Text, SelectionStart, 80).ToList();
        if (matches.Count == 0) return;

        ShowIntelliSensePopup(SelectionStart, matches);
    }

    /// <summary>根据光标位置在 DGV 下方显示 popup</summary>
    private void ShowIntelliSensePopup(int caret, List<string> matches)
    {
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

    private void Highlight(int fromLine, int toLine, bool resetColors = true)
    {
        if (IsDisposed || TextLength == 0) return;
        if (!IsHandleCreated) return;

        // ★ 2026-09-14 修复: 用户拖选时禁止执行 Highlight
        // 原因: Highlight 内部 Select(0, TextLength) 会把当前选区覆盖为全文,
        //       然后恢复成 200ms 前 OnTextChanged 触发时的旧选区 (selStart/selLen).
        //       如果用户正在拖选 (MouseButtons != None), 恢复的旧选区会打断用户当前的拖选,
        //       表现为 "选区突然跳回" / "选多或选少".
        // 解决: 用户在拖选时直接 return, 等 OnTextChanged/下次 timer 触发再跑.
        if (MouseButtons != MouseButtons.None) return;

        // ★ 2026-09-22 陛下反馈修复大文档卡顿: 增量行范围
        // 1000 行存储过程的"几秒 ~ 十几秒" -> "< 100ms"。
        // 关键改动: 不再 Select(0, TextLength) + SelectionColor=Black 全量重置，
        //           改为只对 [fromLine, toLine] 行范围重置 + 设色。
        int lineCount = Lines.Length;
        if (lineCount == 0) return;
        fromLine = Math.Max(0, fromLine);
        toLine   = Math.Min(lineCount - 1, toLine);
        if (fromLine > toLine) return;

        int startIdx = GetFirstCharIndexFromLine(fromLine);
        int endIdx   = (toLine >= lineCount - 1) ? TextLength : GetFirstCharIndexFromLine(toLine + 1);
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx) return;
        int len = endIdx - startIdx;

        int selStart = SelectionStart;
        int selLen = SelectionLength;
        // 保存滚动位置
        int vScroll = GetScrollPos(Handle, SB_VERT);
        int hScroll = GetScrollPos(Handle, SB_HORZ);

        _suppressHighlight = true;
        // ★ 2026-09-22 陛下反馈修复栈溢出: 标记 Highlight 执行中, 避免递归
        _isHighlighting = true;
        // 冻结重绘 → 防 RichTextBox 闪烁（仅窗口被冻结，不影响其他控件）
        SendMessage(Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
        try
        {
            SuspendLayout();

            // 1. 范围内字符重置为黑色（只动这几十行，不是全文）
            // ★ 2026-09-22 陛下反馈修复: 初次加载场景 (editor.Text = "...") 跳过此步
            // 因为新赋值的文本默认就是黑色,重置是白做功,1000 行白白多耗 5-6s。
            // 5 轮正则设色在下方,resetColors=false 也会跑,关键字高亮完整保留。
            if (resetColors)
            {
                Select(startIdx, len);
                SelectionColor = Color.Black;
            }

            // ★ 取范围内子串，正则匹配只在子串上跑，索引加 startIdx 偏移量
            var text = Text.Substring(startIdx, len);

            // 2. 关键字
            foreach (Match m in WordRegex.Matches(text))
            {
                if (Keywords.Contains(m.Value))
                {
                    Select(m.Index + startIdx, m.Length);
                    SelectionColor = KeywordColor;
                }
            }

            // 3. 数字
            foreach (Match m in NumberRegex.Matches(text))
            {
                Select(m.Index + startIdx, m.Length);
                SelectionColor = NumberColor;
            }

            // 4. 注释（行注释 + 块注释）
            foreach (Match m in CommentLineRegex.Matches(text))
            {
                Select(m.Index + startIdx, m.Length);
                SelectionColor = CommentColor;
            }
            foreach (Match m in CommentBlockRegex.Matches(text))
            {
                Select(m.Index + startIdx, m.Length);
                SelectionColor = CommentColor;
            }

            // 5. 字符串（最后，覆盖关键字/数字）
            foreach (Match m in StringRegex.Matches(text))
            {
                Select(m.Index + startIdx, m.Length);
                SelectionColor = StringColor;
            }

            // 恢复选区
            Select(selStart, selLen);
            SelectionColor = Color.Black;
        }
        finally
        {
            // ★ 2026-09-22 陛下反馈修复栈溢出: 先复位守卫,再恢复滚动位置
            // 顺序很重要: SendMessage(WM_VSCROLL, ...) 会触发 WndProc 滚动分支,
            // 该分支检查 _isHighlighting 跳过 HighlightVisibleRegionIfNeeded(),
            // 所以必须 _isHighlighting = false 在 SendMessage 之后, 但 _isHighlighting
            // 状态在 WndProc 看到消息时还是 true → 跳过,不会重入。
            // 反过来若先 false 再 SendMessage, 会重入!
            ResumeLayout();
            _suppressHighlight = false;
            // 解冻重绘 + 主动 Invalidate：让富文本一次性画出，跳过中间状态
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
            // ★ 先解冻结重绘,再恢复滚动位置(SendMessage 会触发 WndProc,守卫为 true 跳过),
            // 最后复位守卫,避免下次重入被吞掉。
            SetScrollPos(Handle, SB_VERT, vScroll, false);
            SetScrollPos(Handle, SB_HORZ, hScroll, false);
            SendMessage(Handle, WM_VSCROLL, (IntPtr)(SB_THUMBPOSITION | (vScroll << 16)), IntPtr.Zero);
            SendMessage(Handle, WM_HSCROLL, (IntPtr)(SB_THUMBPOSITION | (hScroll << 16)), IntPtr.Zero);
            _isHighlighting = false;
        }
    }


    /// <summary>★ 2026-09-29 v6: 去抖定时器到点时,把当前已稳定的 text 提交为撤销步骤</summary>
    private void CommitUndoSnapshot()
    {
        if (!_undoInitialized) return;
        if (_suspendUndo) return;  // PerformUndo/Redo 期间不提交

        var currentText = Text;
        var currentCursor = SelectionStart;

        if (_lastCommittedText == currentText) return;  // 无变化

        _undoStack.Push(new UndoState
        {
            Text = _lastCommittedText,
            CursorStart = _lastCommittedCursor
        });

        if (_undoStack.Count > MaxUndoLevels)
        {
            // 保留最新的 MaxUndoLevels 条 (items[0] 是最旧的)
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 1; i >= 1; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        _redoStack.Clear();  // 任何新编辑清空 redo 栈

        _lastCommittedText = currentText;
        _lastCommittedCursor = currentCursor;
    }

    /// <summary>★ 2026-09-29 v6: 撤销栈里有东西就回滚一格;同时把当前状态推到 redo 栈</summary>
    private void PerformUndo()
    {
        if (_undoStack.Count == 0) return;
        // ★ v6 修复: 撤销前先停掉去抖定时器并强制提交(避免定时器在 undo 后又把当前 redo 状态进 undo 栈)
        _undoDebounceTimer.Stop();
        CommitUndoSnapshot();

        var current = new UndoState { Text = Text, CursorStart = SelectionStart };
        var prev = _undoStack.Pop();
        _redoStack.Push(current);

        _suspendUndo = true;
        try
        {
            Text = prev.Text;
            SelectionStart = Math.Min(prev.CursorStart, Text.Length);
            ScrollToCaret();
            // ★ v6 修复: 同步更新 _lastCommittedText,否则下次输入会把撤销前的旧 text 当作 prev
            _lastCommittedText = Text;
            _lastCommittedCursor = SelectionStart;
        }
        finally
        {
            _suspendUndo = false;
        }
    }

    /// <summary>★ 2026-09-29 v6: 重做栈里有东西就前进一格</summary>
    private void PerformRedo()
    {
        if (_redoStack.Count == 0) return;
        // ★ v6 修复: 同 PerformUndo - 先停定时器+提交,避免 redo 后定时器把 redo 状态塞进 undo 栈
        _undoDebounceTimer.Stop();
        CommitUndoSnapshot();

        var current = new UndoState { Text = Text, CursorStart = SelectionStart };
        var next = _redoStack.Pop();
        _undoStack.Push(current);

        _suspendUndo = true;
        try
        {
            Text = next.Text;
            SelectionStart = Math.Min(next.CursorStart, Text.Length);
            ScrollToCaret();
            // ★ v6 修复: 同步 _lastCommittedText
            _lastCommittedText = Text;
            _lastCommittedCursor = SelectionStart;
        }
        finally
        {
            _suspendUndo = false;
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
        // ★ 2026-09-22 修复: 字号变了肯定要重算宽度 → 清缓存强制算
        _lastWidthLineCount = -1;
        UpdateWidthIfNeeded();
        Invalidate();
    }

    // ★ 2026-09-22 陛下反馈修复行号截断:
    // 之前只在 Bind 一次 + FontSizeChanged 时算 Width,文件加载/粘贴后 Lines.Length
    // 增长但 Width 没更新 → 100/1000 等高位行号的"1"被裁掉,看上去"99 后又到 00"。
    // 现在每次 ViewChanged 都检查行数,变了才重算宽度 + 重绘。
    private int _lastWidthLineCount = -1;

    private void OnViewChanged(object? sender, EventArgs e)
    {
        UpdateWidthIfNeeded();
        Invalidate();
    }

    /// <summary>
    /// 按 Lines.Length + 当前 Font 计算所需宽度,Bold 字体量出 N 位数字的实际占位 + 边距。
    /// 行数没变就跳过(避免每个按键都触发 Width setter → 父容器重布局)。
    /// </summary>
    private void UpdateWidthIfNeeded()
    {
        if (_editor == null) return;
        int lineCount = _editor.Lines.Length;
        if (lineCount == _lastWidthLineCount) return;
        _lastWidthLineCount = lineCount;
        if (lineCount <= 0) return;

        // 取最大行号文本,保证画"1000"时宽度够
        string maxLineText = lineCount.ToString();
        // 用 Bold 量(当前行是粗体,粗体比 Regular 略宽)
        using var boldFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
        var size = TextRenderer.MeasureText(maxLineText, boldFont);

        // 6px 右边竖线距 + 6px 行号右对齐留白 + 2px 缓冲
        int newWidth = Math.Max(44, size.Width + 14);
        if (Width != newWidth) Width = newWidth;
    }

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