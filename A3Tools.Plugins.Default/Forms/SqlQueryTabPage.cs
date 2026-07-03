using System.Data;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL 查询 TabPage 内容（编辑器 + 结果 + 消息）。
/// 编辑器用 SqlEditor（继承 RichTextBox）+ LineNumberPanel 自绘行号 + SQL 高亮。
/// 嵌入到 SqlQueryForm 的 TabPage.Controls 中，由 SqlQueryForm 管理生命周期。
/// </summary>
public partial class SqlQueryTabPage : UserControl
{
    private readonly SqlQueryForm _parent;
    private CancellationTokenSource? _cts;
    private Action<string, long, int>? _statusReporter;

    /// <summary>当前 Tab 对应的 TabPage（由 SqlQueryForm 在嵌入时设置）</summary>
    public TabPage? Page { get; set; }

    public SqlEditor Editor => rtbEditor;
    public DataGridView ResultGrid => dgvResult;
    public RichTextBox Messages => rtbMessages;

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
        rtbEditor.Text = text;
        rtbEditor.HighlightNow();
    }

    public void AppendMessage(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => AppendMessage(msg)); return; }
        rtbMessages.AppendText(msg);
    }

    public void SetStatusReporter(Action<string, long, int> reporter) => _statusReporter = reporter;

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
        btnExecuteSelected.Enabled = false;
        btnStop.Enabled = true;
        dgvResult.DataSource = null;
        _cts = new CancellationTokenSource();

        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new SqlConnection(_parent.CurrentConnectionString);
            await conn.OpenAsync(_cts.Token);
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 已连接到 [{conn.Database}]\n");

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
            int affectedRows = 0;
            bool hasResult = false;

            using var reader = await cmd.ExecuteReaderAsync(_cts.Token);
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
                // SqlDataReader 已被关闭（单结果集查询在 dt.Load 后 reader 内部自动关闭）
                // 这表示没有更多结果集，是正常情况，忽略。
            }

            sw.Stop();
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 执行完成\n");
            _statusReporter?.Invoke($"执行成功，影响 {affectedRows} 行", sw.ElapsedMilliseconds, affectedRows);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] 已停止\n");
            _statusReporter?.Invoke("已停止", sw.ElapsedMilliseconds, 0);
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppendMessage($"[{DateTime.Now:HH:mm:ss}] [错误] {ex.Message}\n");
            _statusReporter?.Invoke("执行失败", sw.ElapsedMilliseconds, 0);
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