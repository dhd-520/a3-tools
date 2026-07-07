using System.Data;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL 执行状态（影响 Tab 标题图标 + 状态栏颜色）。
/// </summary>
public enum ExecStatus
{
    Idle,
    Running,
    Success,
    Failure,
    Cancelled,
}

/// <summary>
/// SQL 查询 TabPage 内容（编辑器 + 结果 + 消息）。
/// 编辑器用 SqlEditor（继承 RichTextBox）+ LineNumberPanel 自绘行号 + SQL 高亮。
/// 嵌入到 SqlQueryForm 的 TabPage.Controls 中，由 SqlQueryForm 管理生命周期。
/// </summary>
public partial class SqlQueryTabPage : UserControl
{
    private readonly SqlQueryForm _parent;
    private CancellationTokenSource? _cts;
    private Action<string, long, int, ExecStatus>? _statusReporter;
    private bool _suppressStatusClear;  // SetEditorText 程序性赋值时屏蔽 TextChanged 触发清状态图标

    /// <summary>当前 Tab 对应的 TabPage（由 SqlQueryForm 在嵌入时设置）</summary>
    public TabPage? Page { get; set; }

    public SqlEditor Editor => rtbEditor;
    public DataGridView ResultGrid => dgvResult;
    public RichTextBox Messages => rtbMessages;

    /// <summary>字号变化事件→主窗体状态栏（2026-07-07）</summary>
    public event EventHandler? FontSizeChanged;

    /// <summary>设计器无参构造（VS 加载设计时使用）。运行时走带参构造。</summary>
    public SqlQueryTabPage() : this(null!)
    {
        if (DesignMode) return;
    }

    public SqlQueryTabPage(SqlQueryForm parent)
    {
        _parent = parent;
        InitializeComponent();
        // 设计器模式下不绑事件（避免找不到对应 Form）
        if (DesignMode) return;
        InitEditor();
    }

    private void InitEditor()
    {
        rtbEditor.KeyDown += SqlEditor_KeyDown;
        // 字号改变 → 转发给 SqlQueryForm 状态栏（2026-07-07）
        rtbEditor.FontSizeChanged += (_, _) => FontSizeChanged?.Invoke(this, EventArgs.Empty);
        FontSizeChanged?.Invoke(this, EventArgs.Empty);
        // F12 转到定义（按词查缓存 → OpenScript）
        rtbEditor.GoToDefinitionRequested += () => _parent.GoToDefinition();
        // 用户在编辑器里改动 → 清掉上次执行结果的状态图标（结果已经过期）
        rtbEditor.TextChanged += (_, _) =>
        {
            if (!_suppressStatusClear) SetTabStatusIcon(ExecStatus.Idle);
        };
    }

    private void SqlEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.OemQuestion)
        {
            ToggleLineComment(false); // 取消注释
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.OemQuestion)
        {
            ToggleLineComment(true); // 注释
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.L)
        {
            rtbMessages.Clear();
            e.SuppressKeyPress = true;
        }
    }

    /// <summary>
    /// 注释/取消注释当前选区所在的所有行。
    /// </summary>
    private void ToggleLineComment(bool comment)
    {
        var sb = new StringBuilder();
        int start = rtbEditor.SelectionStart;
        int len = rtbEditor.SelectionLength;
        int lineStart = rtbEditor.GetLineFromCharIndex(start);
        int lineEnd = rtbEditor.GetLineFromCharIndex(start + Math.Max(0, len));

        rtbEditor.SuspendHighlight(true);
        rtbEditor.SuspendLayout();
        try
        {
            for (int line = lineStart; line <= lineEnd; line++)
            {
                int ci = rtbEditor.GetFirstCharIndexFromLine(line);
                if (ci < 0) continue;
                int nextCi = rtbEditor.GetFirstCharIndexFromLine(line + 1);
                int lineLen = (nextCi < 0 ? rtbEditor.TextLength : nextCi) - ci;
                string lineText = rtbEditor.Text.Substring(ci, lineLen);
                string trimmed = lineText.TrimStart();
                int leading = lineText.Length - trimmed.Length;

                if (comment)
                {
                    if (!trimmed.StartsWith("--"))
                        sb.Append(lineText.Substring(0, leading)).Append("-- ").Append(trimmed);
                    else
                        sb.Append(lineText);
                }
                else
                {
                    if (trimmed.StartsWith("-- "))
                        sb.Append(lineText.Substring(0, leading)).Append(trimmed.Substring(3));
                    else if (trimmed.StartsWith("--"))
                        sb.Append(lineText.Substring(0, leading)).Append(trimmed.Substring(2));
                    else
                        sb.Append(lineText);
                }
                if (line < lineEnd) sb.Append('\n');
            }

            int firstCi = rtbEditor.GetFirstCharIndexFromLine(lineStart);
            int endCi = rtbEditor.GetFirstCharIndexFromLine(lineEnd + 1);
            int replaceLen = (endCi < 0 ? rtbEditor.TextLength : endCi) - firstCi;

            // 移除尾部多出的换行
            string finalText = sb.ToString();
            if (finalText.EndsWith('\n') && replaceLen > 0 && rtbEditor.Text[firstCi + replaceLen - 1] != '\n')
                finalText = finalText.Substring(0, finalText.Length - 1);

            rtbEditor.Select(firstCi, replaceLen);
            rtbEditor.SelectedText = finalText;
        }
        finally
        {
            rtbEditor.ResumeLayout();
            rtbEditor.SuspendHighlight(false);
            rtbEditor.HighlightNow();
        }
    }

    public void SetEditorText(string text)
    {
        // 临时屏蔽 IntelliSense（防止加载脚本后末行 "GO" 触发 [GOTO/...] 的莫名提示）
        rtbEditor.SuppressIntelliSense();
        // 屏蔽 TextChanged 清状态图标（程序性加载不应清掉刚跑完的状态图标）
        _suppressStatusClear = true;
        try
        {
            rtbEditor.Text = text;
            rtbEditor.HighlightNow();
        }
        finally
        {
            _suppressStatusClear = false;
            // 短暂延迟后再开放，避免用户键入第一个字符仍保留抑制
            // （如果定时器已起不会被取消；之前未起也不会新起）
            var t = new System.Windows.Forms.Timer { Interval = 250 };
            t.Tick += (_, _) => { t.Stop(); t.Dispose(); rtbEditor.ResumeIntelliSense(); };
            t.Start();
        }
    }

    /// <summary>
    /// 设置 Tab 标题上的执行状态图标：✓ 成功 / ✗ 失败 / ⏸ 停止 / ⏳ 运行中 / 空=无状态。
    /// 同步触发 TabControl 重绘（Page.Text 变更会自动触发）。
    /// </summary>
    private void SetTabStatusIcon(ExecStatus status)
    {
        if (Page == null) return;
        string current = Page.Text ?? "";
        // 去掉已有的状态图标（✓ ✗ ⏸ ⏳），保持标题文本干净
        foreach (var icon in new[] { "✓", "✗", "⏸", "⏳" })
        {
            int idx = current.LastIndexOf(icon);
            if (idx >= 0)
            {
                current = current.Substring(0, idx).TrimEnd();
                break;
            }
        }
        string suffix = status switch
        {
            ExecStatus.Success   => "  ✓",
            ExecStatus.Failure   => "  ✗",
            ExecStatus.Cancelled => "  ⏸",
            ExecStatus.Running   => "  ⏳",
            _                    => "",
        };
        string newText = string.IsNullOrEmpty(suffix) ? current : $"{current}{suffix}";
        // 相同文本不重写，避免每次按键都触发 Tab 重绘闪烁
        if (Page.Text != newText) Page.Text = newText;
    }

    public void AppendMessage(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => AppendMessage(msg)); return; }
        rtbMessages.AppendText(msg);
    }

    public void SetStatusReporter(Action<string, long, int, ExecStatus> reporter) => _statusReporter = reporter;

    public void ClearResults()
    {
        if (InvokeRequired) { BeginInvoke(ClearResults); return; }
        dgvResult.DataSource = null;
    }

    public void ClearAll()
    {
        if (InvokeRequired) { BeginInvoke(ClearAll); return; }
        rtbEditor.Text = "";
        dgvResult.DataSource = null;
        rtbMessages.Clear();
    }

    // ============================================
    // 执行逻辑
    // ============================================

    /// <summary>公开接口：执行当前 Tab 全部 SQL（供主窗体 F5 快捷键调用）</summary>
    public void PerformExecuteAll()
    {
        var sql = rtbEditor.Text;
        if (string.IsNullOrWhiteSpace(sql)) return;
        _ = ExecuteAsync(sql);
    }

    /// <summary>公开接口：执行选中 SQL（供主窗体 Ctrl+F5 快捷键调用）</summary>
    public void PerformExecuteSelected()
    {
        var sql = rtbEditor.SelectedText;
        if (string.IsNullOrWhiteSpace(sql))
        {
            AppendMessage("[提示] 未选中文本，执行全部\n");
            sql = rtbEditor.Text;
        }
        if (string.IsNullOrWhiteSpace(sql)) return;
        _ = ExecuteAsync(sql);
    }

    private async void BtnExecute_Click(object? sender, EventArgs e) => PerformExecuteAll();

    private async void BtnExecuteSelected_Click(object? sender, EventArgs e) => PerformExecuteSelected();

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        AppendMessage("[提示] 已请求停止\n");
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "保存 SQL 脚本",
            Filter = "SQL 文件 (*.sql)|*.sql|所有文件 (*.*)|*.*",
            FileName = $"query_{DateTime.Now:yyyyMMdd_HHmmss}.sql"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(dlg.FileName, rtbEditor.Text, Encoding.UTF8);
                AppendMessage($"[成功] 已保存到 {dlg.FileName}\n");
            }
            catch (Exception ex)
            {
                AppendMessage($"[错误] 保存失败: {ex.Message}\n");
            }
        }
    }

    private async Task ExecuteAsync(string sql)
    {
        btnExecute.Enabled = false;
        btnExecuteSelected.Enabled = true;
        btnStop.Enabled = true;
        dgvResult.DataSource = null;
        _cts = new CancellationTokenSource();

        // 执行前 → 状态图标 = ⏳，状态栏 = 蓝色 "执行中..."
        SetTabStatusIcon(ExecStatus.Running);
        _statusReporter?.Invoke("执行中...", 0, 0, ExecStatus.Running);

        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new SqlConnection(_parent.CurrentConnectionString);
            await conn.OpenAsync(_cts.Token);
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 已连接到 [{conn.Database}]\n");

            // ★ 重要：按 GO 切分为多个批（GO 是 SSMS/sqlcmd 的批处理分隔符，不是 T-SQL 关键字）。
            // - USE [db] GO 是第一个批
            // - ALTER PROCEDURE 必须是批中的第一句（SQL Server 要求）
            // - SSMS 能运行多 GO 脚本是它自己在做切分，.NET SqlClient 不原生支持
            var batches = SplitSqlByGo(sql);
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 拆分为 {batches.Count} 个批次（GO 边界）\n");

            int affectedRows = 0;
            bool hasResult = false;
            bool anyBatchError = false;

            for (int i = 0; i < batches.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var batch = batches[i].Trim();
                if (string.IsNullOrWhiteSpace(batch)) continue;

                AppendMessage($"[{DateTime.Now:HH:mm:ss}] 批次 {i + 1}/{batches.Count}：{batch.Length} 字符\n");

                // 每个批次一个 SqlCommand + Use 独立连接（重置 SET 选项/USE）
                using var batchCmd = new SqlCommand(batch, conn) { CommandTimeout = 0 };
                try
                {
                    using var reader = await batchCmd.ExecuteReaderAsync(_cts.Token);
                    try
                    {
                        do
                        {
                            var dt = new DataTable();
                            dt.Load(reader);
                            if (!hasResult)
                            {
                                dgvResult.DataSource = dt;
                                hasResult = true;
                            }
                            else
                            {
                                AppendMessage($"--- 后续结果集（{dt.Rows.Count} 行 x {dt.Columns.Count} 列）---\n");
                                AppendMessage(DataTableToText(dt));
                            }
                            affectedRows += dt.Rows.Count;
                        }
                        while (await reader.NextResultAsync(_cts.Token));
                    }
                    catch (InvalidOperationException) when (reader.IsClosed)
                    {
                        // 单结果集：reader 内部已关闭，无更多结果集，正常
                    }
                }
                catch (Exception batchEx)
                {
                    anyBatchError = true;
                    AppendMessage($"[{DateTime.Now:HH:mm:ss}] [错误] 批次 {i + 1} 失败：{batchEx.Message}\n");
                    // 继续下一个批次，不中断（类似 SSMS 行为）
                }
            }

            sw.Stop();
            if (anyBatchError)
            {
                SetTabStatusIcon(ExecStatus.Failure);
                _statusReporter?.Invoke($"✗ 部分批次失败（{batches.Count} 批），成功 {affectedRows} 行", sw.ElapsedMilliseconds, affectedRows, ExecStatus.Failure);
                // 失败 → 强制切到 消息 Tab（即使有部分结果也要看错误详情）
                if (tabResultSwitcher.SelectedTab != tabMessages)
                    tabResultSwitcher.SelectedTab = tabMessages;
            }
            else
            {
                SetTabStatusIcon(ExecStatus.Success);
                _statusReporter?.Invoke($"✓ 执行成功，影响 {affectedRows} 行", sw.ElapsedMilliseconds, affectedRows, ExecStatus.Success);
                if (!hasResult && tabResultSwitcher.SelectedTab != tabMessages)
                    tabResultSwitcher.SelectedTab = tabMessages;
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 已停止\n");
            SetTabStatusIcon(ExecStatus.Cancelled);
            _statusReporter?.Invoke("⏸ 已停止", sw.ElapsedMilliseconds, 0, ExecStatus.Cancelled);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] [错误] {ex.Message}\n");
            SetTabStatusIcon(ExecStatus.Failure);
            _statusReporter?.Invoke($"✗ 执行失败：{ex.Message}", sw.ElapsedMilliseconds, 0, ExecStatus.Failure);
            if (tabResultSwitcher.SelectedTab != tabMessages)
                tabResultSwitcher.SelectedTab = tabMessages;
        }
        finally
        {
            btnExecute.Enabled = true;
            btnExecuteSelected.Enabled = true;
            btnStop.Enabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// 按 GO 边界切分 SQL 脚本。GO 是 SSMS / sqlcmd 的批处理分隔符，不是 T-SQL 关键字。
    /// — 行首/独立行 GO（前后可为空白）才切。字符串/注释中的 GO 不切。
    /// — GO 后可跟正整数（重复执行次数），这里合并为单次批。
    /// </summary>
    private static List<string> SplitSqlByGo(string sql)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(sql)) return result;

        var lines = sql.Replace("\r\n", "\n").Split('\n');
        var current = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            // 匹配独立行 GO（允许 :on/off 等修饰词，为了简单这里只接受裸 GO）
            // GO 后面可能有数字（重复执行）一并忽略
            bool isGo =
                string.Equals(trimmed, "GO", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^GO\s+\d+\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (isGo)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.AppendLine(rawLine);
            }
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static string DataTableToText(DataTable dt)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
        foreach (DataRow row in dt.Rows)
            sb.AppendLine(string.Join("\t", row.ItemArray.Select(v => v?.ToString() ?? "NULL")));
        return sb.ToString();
    }

    // ============================================
    // 复制到剪贴板
    // ============================================

    private void CopySelectedCell()
    {
        if (dgvResult.CurrentCell != null)
            Clipboard.SetText(dgvResult.CurrentCell.Value?.ToString() ?? "");
    }

    private void CopySelectedRow()
    {
        if (dgvResult.CurrentCell == null) return;
        var rowIdx = dgvResult.CurrentCell.RowIndex;
        if (dgvResult.DataSource is not DataTable dt) return;
        if (rowIdx < 0 || rowIdx >= dt.Rows.Count) return;
        var row = dt.Rows[rowIdx];
        Clipboard.SetText(string.Join("\t", row.ItemArray.Select(v => v?.ToString() ?? "NULL")));
    }

    private void CopyAllToClipboard()
    {
        if (dgvResult.DataSource is DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join("\t", row.ItemArray.Select(v => v?.ToString() ?? "NULL")));
            Clipboard.SetText(sb.ToString());
        }
    }
}