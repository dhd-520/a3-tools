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

    // 【2026-07-09 多结果集】所有结果集 DataGridView 共享的右键菜单（随 components 自动释放）
    private ContextMenuStrip ctxResultMenu = null!;

    // 【2026-07-09 大数据流式读】每读 N 行让出一次 UI 线程，让用户能点“停止”+ 状态栏实时刷新
    private const int _streamBatchSize = 1000;
    // 【2026-07-09 列宽自适应阈值】超过 N 行不开 AutoSizeColumnsMode（AllCells 扫所有行算列宽，100万行 + 50列量级会卡几秒）
    //   ≤ 列宽自适应限制：保留 AllCells，视觉好看
    //   >：列宽 = None，用户手动拉或双击列头自适应（DataGridView 内置支持）
    private const int _autoSizeColumnLimit = 100;

    // 【2026-07-09 性能】保存当前正在执行的 batchCmd，让“停止”按钮可以调 SqlCommand.Cancel()
    //   立即中断卡住的 reader.Read()，避免用户取消后还要等当前 Read 返回（最坏 1 秒）
    private SqlCommand? _currentBatchCmd;

    /// <summary>当前 Tab 对应的 TabPage（由 SqlQueryForm 在嵌入时设置）</summary>
    public TabPage? Page { get; set; }

    public SqlEditor Editor => rtbEditor;
    /// <summary>【2026-07-09 重构】返回第一个结果集的 DataGridView（多结果集时取首集）；没有结果集返 null</summary>
    public DataGridView? ResultGrid =>
        tcResults.TabPages.Count > 0
            ? tcResults.TabPages[0].Controls.OfType<DataGridView>().FirstOrDefault()
            : null;
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
        // 【2026-07-09 多结果集】所有结果集 DataGridView 共享一个右键菜单
        ctxResultMenu = new ContextMenuStrip(components);
        var miCopyCell = new ToolStripMenuItem("复制单元格");
        miCopyCell.Click += (_, _) => CopySelectedCell();
        var miCopyRow = new ToolStripMenuItem("复制整行（TSV）");
        miCopyRow.Click += (_, _) => CopySelectedRow();
        var miCopyAll = new ToolStripMenuItem("复制全部（TSV）");
        miCopyAll.Click += (_, _) => CopyAllToClipboard();
        ctxResultMenu.Items.AddRange(new ToolStripItem[] { miCopyCell, miCopyRow, new ToolStripSeparator(), miCopyAll });

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
        // 【2026-07-09 多结果集】清空所有结果集 sub-Tab + 释放里面的 DataGridView
        while (tcResults.TabPages.Count > 0)
        {
            var page = tcResults.TabPages[0];
            tcResults.TabPages.RemoveAt(0);
            foreach (Control c in page.Controls) c.Dispose();
            page.Dispose();
        }
    }

    public void ClearAll()
    {
        if (InvokeRequired) { BeginInvoke(ClearAll); return; }
        rtbEditor.Text = "";
        ClearResults();
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
        // 【2026-07-09】立即中断卡住的 reader.Read()，避免等当前 IO 返回（最坏 1 秒）
        try { _currentBatchCmd?.Cancel(); } catch { /* ignore */ }
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
        ClearResults();
        _cts = new CancellationTokenSource();

        // 执行前 → 状态图标 = ⏳，状态栏 = 蓝色 "执行中..."
        SetTabStatusIcon(ExecStatus.Running);
        _statusReporter?.Invoke("执行中...", 0, 0, ExecStatus.Running);

        var sw = Stopwatch.StartNew();

        // 2026-07-09 混合连接：Http 模式走 IDataAccess 路径（同步返回，丢失流式读优化但验证可行性）
        if (_parent.CurrentDataAccess.Mode == A3Tools.Common.DataAccess.DataAccessMode.Http)
        {
            await ExecuteViaDataAccessAsync(sql, sw);
            return;
        }

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
                    _currentBatchCmd = batchCmd;
                    try
                    {
                        int resultInBatch = 0;
                        do
                        {
                            // 【2026-07-09 多结果集 + 大数据】分阶段读
                            //   - CreateResultTab：先建空 DataGridView（**不绑 DataTable**，性能关键）
                            //   - 同步 Read 填 dt（不用 await ReadAsync，每次 1ms 调度开销叠加 = 几秒）
                            //   - 每 1000 行 Application.DoEvents 让出 UI 线程（同步 Read 会卡 UI）
                            //     + 检查 _cts.Token 让用户能点“停止”
                            //   - 读完 dgv.DataSource = dt 一次性绑（不走 5万单元格×N行 通知）
                            //   - FinalizeResultTab：改 tab 标题（带行数 + 取消标记 ⏸） + 按阈值决定列宽自适应
                            var (page, dgv) = CreateResultTab(i + 1, ++resultInBatch);
                            var dt = new DataTable();
                            // 列定义（从 reader 拿）
                            // 处理 SELECT BILLNO,* 这类重复列名（SSMS 允许，DataTable 不允许）
                            for (int c = 0; c < reader.FieldCount; c++)
                            {
                                var colName = reader.GetName(c);
                                if (string.IsNullOrEmpty(colName)) colName = $"Column{c + 1}";
                                Type colType = reader.GetFieldType(c) ?? typeof(object);
                                // 重复列名：SSMS 允许 `SELECT BILLNO, *` 展开后两列都叫 BILLNO，
                                // DataTable.Columns.Add 会抛 DuplicateNameException，兜底处理
                                try { dt.Columns.Add(colName, colType); }
                                catch (DuplicateNameException)
                                {
                                    var safeName = colName;
                                    int suffix = 2;
                                    while (dt.Columns.Contains(safeName)) safeName = $"{colName}_{suffix++}";
                                    dt.Columns.Add(safeName, colType);
                                }
                            }
                            // 分批读行（同步 Read，不用 await，避免 N×1ms 调度开销叠加）
                            int totalRead = 0;
                            bool cancelled = false;
                            int batchRead = 0;
                            try
                            {
                                while (reader.Read())  // 同步读：比 ReadAsync 快 N 倍
                                {
                                    var row = dt.NewRow();
                                    for (int c = 0; c < dt.Columns.Count; c++)
                                        row[c] = reader.IsDBNull(c) ? DBNull.Value : reader.GetValue(c);
                                    dt.Rows.Add(row);
                                    totalRead++;
                                    if (++batchRead >= _streamBatchSize)
                                    {
                                        batchRead = 0;
                                        // 临时标题让用户看到进度
                                        page.Text = $"结果 {tcResults.TabPages.IndexOf(page) + 1}  ·  {totalRead:N0} 行…";
                                        _statusReporter?.Invoke(
                                            $"读取中… {totalRead:N0} 行  ({sw.Elapsed:mm\\:ss})",
                                            sw.ElapsedMilliseconds, totalRead, ExecStatus.Running);
                                        // 让出 UI 线程让用户能点“停止”（同步 Read 会阻塞 UI）
                                        Application.DoEvents();
                                        // 检查取消（用户点停止后 _cts.Cancel()，这里 break）
                                        if (_cts.IsCancellationRequested)
                                        {
                                            cancelled = true;
                                            // 立即中断卡住的 reader.Read()（最坏 1 秒延迟）
                                            try { batchCmd.Cancel(); } catch { /* ignore */ }
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) when (_cts.IsCancellationRequested)
                            {
                                // batchCmd.Cancel() 会让 Read 抛 SqlException，这里静默吃掉
                                cancelled = true;
                            }
                            if (cancelled)
                            {
                                AppendMessage($"[{DateTime.Now:HH:mm:ss}] [提示] 结果集 {resultInBatch} 已被用户取消（已读 {totalRead:N0} 行）\n");
                            }
                            // 关键性能点：读完后一次性绑 DGV（DataGridView 接收 DataTable 是最快路径）
                            dgv.DataSource = dt;
                            // 收尾：改 tab 标题 + 按阈值决定列宽自适应
                            FinalizeResultTab(page, dgv, dt, totalRead, cancelled);
                            hasResult = true;
                            affectedRows += totalRead;
                        }
                        while (reader.NextResult());
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
                int resultSetCount = tcResults.TabPages.Count;
                _statusReporter?.Invoke(
                    $"✗ 部分批次失败（{batches.Count} 批 / {resultSetCount} 个结果集），成功 {affectedRows} 行",
                    sw.ElapsedMilliseconds, affectedRows, ExecStatus.Failure);
                // 失败 → 强制切到 消息 Tab（即使有部分结果也要看错误详情）
                if (tabResultSwitcher.SelectedTab != tabMessages)
                    tabResultSwitcher.SelectedTab = tabMessages;
            }
            else
            {
                SetTabStatusIcon(ExecStatus.Success);
                int resultSetCount = tcResults.TabPages.Count;
                _statusReporter?.Invoke(
                    resultSetCount > 1
                        ? $"✓ 执行成功，{resultSetCount} 个结果集 / {affectedRows} 行"
                        : $"✓ 执行成功，影响 {affectedRows} 行",
                    sw.ElapsedMilliseconds, affectedRows, ExecStatus.Success);
                // 有结果集 → 切到结果 Tab；没结果集 → 切到消息 Tab（看 PRINT）
                if (hasResult && tabResultSwitcher.SelectedTab != tabResult)
                    tabResultSwitcher.SelectedTab = tabResult;
                else if (!hasResult && tabResultSwitcher.SelectedTab != tabMessages)
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
    /// 2026-07-09 混合连接 Http 模式：走 IDataAccess.ExecuteBatchAsync 同步返回结果。
    /// 丢失流式读 + 取消能力，但验证混合连接可行性。
    /// </summary>
    private async Task ExecuteViaDataAccessAsync(string sql, Stopwatch sw)
    {
        try
        {
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 已连接（Http 代理：{_parent.CurrentDataAccess.DisplayName}）\n");

            var result = await _parent.CurrentDataAccess.ExecuteBatchAsync(sql, _cts.Token);

            if (!result.Success)
            {
                AppendMessage($"[{DateTime.Now:HH:mm:ss}] {result.Message}\n");
                _statusReporter?.Invoke($"✗ {result.Message}", sw.ElapsedMilliseconds, 0, ExecStatus.Failure);
                SetTabStatusIcon(ExecStatus.Failure);
                tabResultSwitcher.SelectedTab = tabMessages;
                return;
            }

            // 把 QueryResult.Tables 渲染到 sub-Tab（复用原 DGV + dt 创建逻辑）
            int resultIdx = 0;
            foreach (var table in result.Tables)
            {
                resultIdx++;
                var dt = new DataTable();
                foreach (var col in table.Columns)
                    dt.Columns.Add(col.Name, Type.GetType(col.TypeName) ?? typeof(object));

                foreach (var row in table.Rows)
                {
                    var dr = dt.NewRow();
                    for (int i = 0; i < row.Length && i < dt.Columns.Count; i++)
                    {
                        var val = row[i];
                        if (val == null)
                        {
                            dr[i] = DBNull.Value;
                            continue;
                        }

                        var colType = dt.Columns[i].DataType;
                        if (val.GetType() == colType)
                        {
                            dr[i] = val;
                        }
                        else if (val is string s)
                        {
                            // Newtonsoft.Json 反序列化 object?[] 时，DateTime/ Guid/ decimal 等都会变成 string
                            if (colType == typeof(DateTime))
                                dr[i] = DateTime.TryParse(s, out var dt2) ? dt2 : DBNull.Value;
                            else if (colType == typeof(decimal))
                                dr[i] = decimal.TryParse(s, out var dec2) ? dec2 : DBNull.Value;
                            else if (colType == typeof(long) || colType == typeof(int))
                                dr[i] = long.TryParse(s, out var lng2) ? lng2 : (object)DBNull.Value;
                            else if (colType == typeof(bool))
                                dr[i] = bool.TryParse(s, out var b2) ? b2 : DBNull.Value;
                            else if (colType == typeof(Guid))
                                dr[i] = Guid.TryParse(s, out var g2) ? g2 : DBNull.Value;
                            else if (colType == typeof(TimeSpan))
                                dr[i] = TimeSpan.TryParse(s, out var ts2) ? ts2 : DBNull.Value;
                            else
                                dr[i] = val;
                        }
                        else if (val is System.Text.Json.JsonElement je)
                        {
                            // System.Text.Json 反序列化 object?[] 时所有值都是 JsonElement
                            dr[i] = je.ValueKind switch
                            {
                                System.Text.Json.JsonValueKind.Null => DBNull.Value,
                                System.Text.Json.JsonValueKind.String => ConvertFromJsonElement(je, colType),
                                System.Text.Json.JsonValueKind.Number => ConvertFromJsonElement(je, colType),
                                System.Text.Json.JsonValueKind.True => true,
                                System.Text.Json.JsonValueKind.False => false,
                                _ => je.ToString()
                            };
                        }
                        else
                        {
                            dr[i] = val;
                        }
                    }
                    dt.Rows.Add(dr);
                }

                var (page, dgv) = CreateResultTab(0, resultIdx);
                dgv.DataSource = dt;
                FinalizeResultTab(page, dgv, dt, dt.Rows.Count, false);
            }

            AppendMessage($"[{DateTime.Now:HH:mm:ss}] {result.Message}（耗时 {sw.ElapsedMilliseconds}ms）\n");

            if (result.Tables.Count == 1)
            {
                _statusReporter?.Invoke($"✓ Http 执行成功，影响 {result.TotalRows} 行", sw.ElapsedMilliseconds, result.TotalRows, ExecStatus.Success);
            }
            else
            {
                _statusReporter?.Invoke($"✓ Http 执行成功，{result.Tables.Count} 个结果集 / {result.TotalRows} 行", sw.ElapsedMilliseconds, result.TotalRows, ExecStatus.Success);
            }
            SetTabStatusIcon(ExecStatus.Success);
            tabResultSwitcher.SelectedTab = tabResult;
        }
        catch (OperationCanceledException)
        {
            _statusReporter?.Invoke("⏸ 已停止", sw.ElapsedMilliseconds, 0, ExecStatus.Failure);
            SetTabStatusIcon(ExecStatus.Failure);
        }
        catch (Exception ex)
        {
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] Http 代理错误：{ex.Message}\n");
            _statusReporter?.Invoke($"✗ {ex.Message}", sw.ElapsedMilliseconds, 0, ExecStatus.Failure);
            SetTabStatusIcon(ExecStatus.Failure);
            tabResultSwitcher.SelectedTab = tabMessages;
        }
        finally
        {
            btnExecute.Enabled = true;
            btnExecuteSelected.Enabled = true;
            btnStop.Enabled = false;
        }
    }

    /// <summary>
    /// 把 JsonElement 转换为目标列类型（DateTime/decimal/int/bool/Guid/TimeSpan 等）。
    /// HttpDataAccess 用 System.Text.Json 反序列化时，所有值都是 JsonElement，
    /// 直接赋值给 DataTable 的强类型列会报 InvalidCastException。
    /// </summary>
    private static object ConvertFromJsonElement(System.Text.Json.JsonElement je, Type colType)
    {
        if (colType == typeof(DateTime))
            return je.TryGetDateTime(out var dt) ? dt : DateTime.TryParse(je.GetString(), out var dt2) ? dt2 : (object)DBNull.Value;
        if (colType == typeof(decimal))
            return je.TryGetDecimal(out var d) ? d : DBNull.Value;
        if (colType == typeof(long))
            return je.TryGetInt64(out var l) ? l : DBNull.Value;
        if (colType == typeof(int))
            return je.TryGetInt32(out var i) ? i : DBNull.Value;
        if (colType == typeof(bool))
            return je.ValueKind == System.Text.Json.JsonValueKind.True;
        if (colType == typeof(Guid))
            return Guid.TryParse(je.GetString(), out var g) ? g : DBNull.Value;
        if (colType == typeof(TimeSpan))
            return TimeSpan.TryParse(je.GetString(), out var ts) ? ts : DBNull.Value;
        if (colType == typeof(string))
            return je.GetString() ?? (object)DBNull.Value;
        // 兜底
        return je.ValueKind == System.Text.Json.JsonValueKind.String ? (object?)je.GetString() ?? DBNull.Value : je.ToString();
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
    // 多结果集支持（2026-07-09）
    // ============================================

    /// <summary>当前 tcResults 中选中的 sub-Tab 里的 DataGridView（多结果集时取当前可见那个）</summary>
    private DataGridView? CurrentResultGrid =>
        tcResults.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();

    /// <summary>
    /// 【2026-07-09 流式读】为单个结果集创建空 sub-Tab + 空 DataGridView（**不绑 DataTable**）。
    /// 返回的容器由调用方填 dt，读完手动 dgv.DataSource = dt 一次性绑。
    /// 性能关键：**读过程中 DGV 不绑 dt → DataTable.Rows.Add 不触发 DGV 通知**
    ///   （DGV 每行通知 = N×M 单元格创建 + 布局，1000×50=5万单元格 = 几秒）
    ///   读完后一次性绑 → DGV 一次性接收所有行 = 最快路径（与 dt.Load + 一次绑类似）
    /// 性能要点：DGV 创建时 AutoSizeColumnsMode=None，读完后 FinalizeResultTab 按阈值决定是否 AllCells。
    /// </summary>
    private (TabPage page, DataGridView dgv) CreateResultTab(int batchIndex, int resultInBatch)
    {
        var page = new TabPage($"结果 {tcResults.TabPages.Count + 1}  ·  读取中…");
        var dgv = CreateResultDataGridView();
        // 性能优化：读阶段关闭自动列宽（让 DGV 走默认列宽，读完再算）
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        page.Controls.Add(dgv);
        tcResults.TabPages.Add(page);
        // 第一个结果集自动选中 + 切到结果 Tab
        if (tcResults.TabPages.Count == 1)
        {
            tcResults.SelectedTab = page;
            if (tabResultSwitcher.SelectedTab != tabResult)
                tabResultSwitcher.SelectedTab = tabResult;
        }
        return (page, dgv);
    }

    /// <summary>
    /// 【2026-07-09 流式读 + 阈值列宽】收尾：改 tab 标题（带行数 + 取消标记 ⏸）+ 按阈值决定是否开自适应列宽。
    /// ·  ≤ _autoSizeColumnLimit (100)：开 AllCells 一次性算列宽（几行～几十行不卡）
    /// ·  >：保持 None（不升）。AllCells 拖 N 行 × M 列扫描 = 几秒，陛下达超过 100 行说“很卡”才加这个阈值
    /// ·  想看自适应变双击列头右边阶（DataGridView 内置习惯）→ →手 拉 → Left then AllCells
    /// </summary>
    private void FinalizeResultTab(TabPage page, DataGridView dgv, DataTable dt, int totalRows, bool cancelled)
    {
        int idx = tcResults.TabPages.IndexOf(page);
        if (idx < 0) idx = tcResults.TabPages.Count;  // fallback
        string suffix = cancelled ? "  ⏸" : "";
        page.Text = $"结果 {idx + 1}  ·  {totalRows:N0} 行{suffix}";
        // 阈值：超过 _autoSizeColumnLimit 行不开自适应列宽，避免扫 N 行算列宽时卡住
        if (totalRows <= _autoSizeColumnLimit)
        {
            // 小数据集：一次性算列宽（AllCells 扫描当前 N 行 ≤ 100 很快）
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }
        else
        {
            // 大数据集：保持 None，列宽 = 列名字段串长度 * 像素（默认估算，不扫描）
            // 用户想看真实列宽可以双击列头右边阶手工适配（DGV 内置交互习惯）
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }
    }

    /// <summary>创建一个结果集 DataGridView（与原 dgvResult 同款样式，共享右键菜单）</summary>
    private DataGridView CreateResultDataGridView()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 242, 245),
                SelectionBackColor = Color.FromArgb(220, 230, 245)
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 252)
            },
            ContextMenuStrip = ctxResultMenu
        };
    }

    // ============================================
    // 复制到剪贴板（2026-07-09：多结果集改用 CurrentResultGrid 而非固定 dgvResult）
    // ============================================

    private void CopySelectedCell()
    {
        var dgv = CurrentResultGrid;
        if (dgv?.CurrentCell != null)
            Clipboard.SetText(dgv.CurrentCell.Value?.ToString() ?? "");
    }

    private void CopySelectedRow()
    {
        var dgv = CurrentResultGrid;
        if (dgv?.CurrentCell == null) return;
        var rowIdx = dgv.CurrentCell.RowIndex;
        if (dgv.DataSource is not DataTable dt) return;
        if (rowIdx < 0 || rowIdx >= dt.Rows.Count) return;
        var row = dt.Rows[rowIdx];
        Clipboard.SetText(string.Join("\t", row.ItemArray.Select(v => v?.ToString() ?? "NULL")));
    }

    private void CopyAllToClipboard()
    {
        var dgv = CurrentResultGrid;
        if (dgv?.DataSource is DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join("\t", row.ItemArray.Select(v => v?.ToString() ?? "NULL")));
            Clipboard.SetText(sb.ToString());
        }
    }
}