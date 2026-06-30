using System.Data;
using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

public partial class CompareTablesForm : Form
{
    private readonly string _srcServer;
    private readonly string _srcDbName;
    private readonly string _srcUser;
    private readonly string _srcPassword;
    private readonly string _tgtServer;
    private readonly string _tgtDbName;
    private readonly string _tgtUser;
    private readonly string _tgtPassword;
    private readonly List<string> _tables;

    public CompareTablesForm(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        List<string> tables)
    {
        _srcServer = srcServer;
        _srcDbName = srcDbName;
        _srcUser = srcUser;
        _srcPassword = srcPassword;
        _tgtServer = tgtServer;
        _tgtDbName = tgtDbName;
        _tgtUser = tgtUser;
        _tgtPassword = tgtPassword;
        _tables = tables;
        InitializeComponent();

        // ESC 关闭 + Ctrl+C 复制脚本
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { this.Close(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.C) { BtnCopyScript_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };

        // DataGridView 多选 + 复选框联动
        dgvDifferences.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvDifferences.MultiSelect = true;

        dgvDifferences.SelectionChanged += (s, e) =>
        {
            if (!dgvDifferences.Columns.Contains("chk")) return;
            foreach (DataGridViewRow row in dgvDifferences.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null) checkCell.Value = row.Selected;
            }
        };

        // 表头点击全选/取消全选
        dgvDifferences.ColumnHeaderMouseClick += (s, e) =>
        {
            if (!dgvDifferences.Columns.Contains("chk") || e.ColumnIndex != 0) return;
            var allChecked = true;
            foreach (DataGridViewRow row in dgvDifferences.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell == null || checkCell.Value == null || !(bool)checkCell.Value)
                {
                    allChecked = false;
                    break;
                }
            }
            foreach (DataGridViewRow row in dgvDifferences.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null)
                {
                    checkCell.Value = !allChecked;
                    row.Selected = !allChecked;
                }
            }
        };

        // 启动时自动执行一次对比
        this.Shown += (s, e) => StartCompare();

        // 二次筛选：差异类型多选 + 字段名模糊 + 清空
        chkMissingTable.CheckedChanged += (s, e) => ApplyFilter();
        chkMissingCol.CheckedChanged += (s, e) => ApplyFilter();
        chkTypeDiff.CheckedChanged += (s, e) => ApplyFilter();
        txtColumnFilter.TextChanged += (s, e) => ApplyFilter();
        btnClearFilter.Click += (s, e) => ClearFilter();
    }

    private void BtnClose_Click(object? sender, EventArgs e) => this.Close();
    private void BtnRefresh_Click(object? sender, EventArgs e) => StartCompare();

    /// <summary>
    /// 异步开始对比
    /// </summary>
    private void StartCompare()
    {
        dgvDifferences.DataSource = null;
        dgvDifferences.Rows.Clear();
        dgvDifferences.Columns.Clear();
        lblSummary.Text = "正在对比表结构...";
        lblSummary.ForeColor = Color.FromArgb(24, 145, 176);
        progressBar.Value = 0;
        lblProgress.Text = "准备中...";
        btnExecute.Enabled = false;
        btnCopyScript.Enabled = false;
        btnRefresh.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var srcConnStr = BuildConnString(_srcServer, _srcDbName, _srcUser, _srcPassword);
                var tgtConnStr = BuildConnString(_tgtServer, _tgtDbName, _tgtUser, _tgtPassword);

                using var srcConn = new SqlConnection(srcConnStr);
                using var tgtConn = new SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                var differences = new List<DifferenceRow>();
                int total = _tables.Count;
                int current = 0;

                foreach (var tableName in _tables)
                {
                    current++;
                    var percent = (int)((double)current / total * 100);
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = percent;
                        lblProgress.Text = $"对比中：{tableName} ({current}/{total})";
                    }));

                    try
                    {
                        var srcCols = GetTableColumns(srcConn, tableName);
                        var tgtCols = GetTableColumns(tgtConn, tableName);

                        if (srcCols == null || srcCols.Count == 0)
                        {
                            // 源库表不存在，跳过
                            continue;
                        }

                        if (tgtCols == null || tgtCols.Count == 0)
                        {
                            // 目标库表不存在 → 整表 CREATE
                            var createScript = GenerateCreateTableScript(srcCols, tableName);
                            differences.Add(new DifferenceRow
                            {
                                TableName = tableName,
                                DiffType = "缺表",
                                ColumnName = "",
                                SrcType = "",
                                TgtType = "",
                                Script = createScript
                            });
                        }
                        else
                        {
                            // 两边都存在，对比字段
                            CompareColumns(srcCols, tgtCols, tableName, differences);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"对比表 {tableName} 失败: {ex.Message}");
                    }
                }

                this.Invoke(new Action(() =>
                {
                    RenderDifferences(differences);
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblSummary.Text = "对比失败";
                    lblSummary.ForeColor = Color.Red;
                    lblProgress.Text = ex.Message;
                    MessageBox.Show($"对比失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                this.Invoke(new Action(() =>
                {
                    btnRefresh.Enabled = true;
                }));
            }
        });
    }

    /// <summary>
    /// <summary>
    /// 差异全量数据，供筛选使用。
    /// </summary>
    private List<DifferenceRow> _allDifferences = new();

    /// <summary>
    /// 渲染差异入口：保存全量 + 更新摘要/按钮状态，最后调 ApplyFilter 渲染行。
    /// </summary>
    private void RenderDifferences(List<DifferenceRow> differences)
    {
        _allDifferences = differences ?? new List<DifferenceRow>();

        // 摘要（始终基于全量，筛选不影响统计）
        var missingTableCount = _allDifferences.Count(d => d.DiffType == "缺表");
        var missingColCount = _allDifferences.Count(d => d.DiffType == "缺字段");
        var typeDiffCount = _allDifferences.Count(d => d.DiffType == "类型差异");
        lblSummary.Text = $"对比完成：共 {_allDifferences.Count} 项差异（缺表 {missingTableCount} / 缺字段 {missingColCount} / 类型差异 {typeDiffCount}）";
        lblSummary.ForeColor = _allDifferences.Count > 0 ? Color.FromArgb(228, 88, 38) : Color.FromArgb(57, 181, 74);
        lblProgress.Text = _allDifferences.Count == 0 ? "源库和目标库表结构完全一致，无需同步" : "请勾选要同步的差异项，点击执行/复制脚本";
        progressBar.Value = 100;

        btnExecute.Enabled = _allDifferences.Count > 0;
        btnCopyScript.Enabled = _allDifferences.Count > 0;

        ApplyFilter();
    }

    /// <summary>
    /// 根据筛选控件状态过滤全量数据，重新渲染 DataGridView 行。
    /// - 差异类型：多选 CheckBox（任一不勾 = 隐藏该类型）
    /// - 字段名：文本模糊匹配（ColumnName 为空的行不受该筛选影响）
    /// </summary>
    private void ApplyFilter()
    {
        if (_allDifferences == null) return;

        var filtered = _allDifferences.AsEnumerable();

        // 差异类型筛选：全不勾 = 不过滤（全部显示）
        var anyChecked = chkMissingTable.Checked || chkMissingCol.Checked || chkTypeDiff.Checked;
        if (anyChecked)
        {
            filtered = filtered.Where(d =>
                (d.DiffType == "缺表" && chkMissingTable.Checked) ||
                (d.DiffType == "缺字段" && chkMissingCol.Checked) ||
                (d.DiffType == "类型差异" && chkTypeDiff.Checked));
        }

        // 字段名筛选：忽略大小写子串匹配；ColumnName 为空（缺表行）会被过滤掉，需保留以免意外丢表
        var colKeyword = txtColumnFilter.Text.Trim();
        if (!string.IsNullOrEmpty(colKeyword))
        {
            filtered = filtered.Where(d =>
                !string.IsNullOrEmpty(d.ColumnName) &&
                d.ColumnName.Contains(colKeyword, StringComparison.OrdinalIgnoreCase));
        }

        RenderRows(filtered.ToList());
    }

    /// <summary>
    /// 重置所有筛选控件（全不勾 + 清空输入框），事件会自动触发 ApplyFilter。
    /// </summary>
    private void ClearFilter()
    {
        chkMissingTable.Checked = false;
        chkMissingCol.Checked = false;
        chkTypeDiff.Checked = false;
        txtColumnFilter.Text = "";
    }

    /// <summary>
    /// 实际把行渲染到 DataGridView（每次筛选后重画）。
    /// </summary>
    private void RenderRows(List<DifferenceRow> rows)
    {
        dgvDifferences.DataSource = null;
        dgvDifferences.Rows.Clear();
        dgvDifferences.Columns.Clear();

        // 复选框列
        var checkCol = new DataGridViewCheckBoxColumn
        {
            HeaderText = "选择",
            Width = 50,
            Name = "chk"
        };
        dgvDifferences.Columns.Add(checkCol);

        // 数据列
        dgvDifferences.Columns.Add("TableName", "表名");
        dgvDifferences.Columns.Add("DiffType", "差异类型");
        dgvDifferences.Columns.Add("ColumnName", "字段名");
        dgvDifferences.Columns.Add("SrcType", "源库字段类型");
        dgvDifferences.Columns.Add("TgtType", "目标库字段类型");
        dgvDifferences.Columns.Add("Script", "变更脚本");

        foreach (var diff in rows)
        {
            var rowIndex = dgvDifferences.Rows.Add(
                false,
                diff.TableName,
                diff.DiffType,
                diff.ColumnName,
                diff.SrcType,
                diff.TgtType,
                diff.Script);

            // 缺表行整行标橙
            if (diff.DiffType == "缺表")
            {
                dgvDifferences.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 244, 220);
            }
            else if (diff.DiffType == "缺字段")
            {
                dgvDifferences.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 230);
            }
        }

        // 其他列根据内容自适应，脚本列固定宽度（能横向滚动查看完整脚本）
        foreach (DataGridViewColumn col in dgvDifferences.Columns)
        {
            if (col.Name == "Script")
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                col.Width = 300;
            }
            else
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
        }
    }

    /// <summary>
    /// 对比两边的列差异，追加到 differences
    /// </summary>
    private void CompareColumns(
        List<ColumnInfo> srcCols,
        List<ColumnInfo> tgtCols,
        string tableName,
        List<DifferenceRow> differences)
    {
        var tgtDict = tgtCols.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // 缺字段 + 类型差异
        foreach (var srcCol in srcCols)
        {
            if (!tgtDict.TryGetValue(srcCol.Name, out var tgtCol))
            {
                // 缺字段 → ALTER TABLE ADD
                var script = $"ALTER TABLE [{tableName}] ADD [{srcCol.Name}] {srcCol.TypeDecl} {(srcCol.IsNullable ? "NULL" : "NOT NULL")}";
                if (!string.IsNullOrEmpty(srcCol.DefaultValue))
                {
                    script += $" DEFAULT {srcCol.DefaultValue}";
                }
                differences.Add(new DifferenceRow
                {
                    TableName = tableName,
                    DiffType = "缺字段",
                    ColumnName = srcCol.Name,
                    SrcType = srcCol.TypeDecl + (srcCol.IsNullable ? " NULL" : " NOT NULL"),
                    TgtType = "",
                    Script = script
                });
            }
            else if (!IsTypeEqual(srcCol, tgtCol))
            {
                // 类型/可空性不一致 → ALTER TABLE ALTER COLUMN
                var script = $"ALTER TABLE [{tableName}] ALTER COLUMN [{srcCol.Name}] {srcCol.TypeDecl} {(srcCol.IsNullable ? "NULL" : "NOT NULL")}";
                differences.Add(new DifferenceRow
                {
                    TableName = tableName,
                    DiffType = "类型差异",
                    ColumnName = srcCol.Name,
                    SrcType = srcCol.TypeDecl + (srcCol.IsNullable ? " NULL" : " NOT NULL"),
                    TgtType = tgtCol.TypeDecl + (tgtCol.IsNullable ? " NULL" : " NOT NULL"),
                    Script = script
                });
            }
        }
    }

    /// <summary>
    /// 比较两个列是否类型和可空性都一致
    /// </summary>
    private bool IsTypeEqual(ColumnInfo a, ColumnInfo b)
    {
        if (!string.Equals(a.TypeDecl, b.TypeDecl, StringComparison.OrdinalIgnoreCase))
            return false;
        if (a.IsNullable != b.IsNullable)
            return false;
        return true;
    }

    /// <summary>
    /// 获取表的列信息（不存在返回 null）
    /// </summary>
    private List<ColumnInfo>? GetTableColumns(SqlConnection conn, string tableName)
    {
        try
        {
            // 先检查表是否存在
            var existsSql = "SELECT 1 FROM sys.objects WHERE name = @name AND type = 'U'";
            using (var existsCmd = new SqlCommand(existsSql, conn))
            {
                existsCmd.Parameters.AddWithValue("@name", tableName);
                using var reader = existsCmd.ExecuteReader();
                if (!reader.Read()) return null;
            }

            // 查询列信息
            var sql = @"
SELECT c.name,
       t.name AS data_type,
       c.max_length,
       c.precision,
       c.scale,
       c.is_nullable,
       ISNULL(dc.definition, '') AS default_value
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID(@tableName)
ORDER BY c.column_id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var colReader = cmd.ExecuteReader();

            var cols = new List<ColumnInfo>();
            while (colReader.Read())
            {
                var colName = colReader["name"]?.ToString() ?? "";
                var dataType = colReader["data_type"]?.ToString() ?? "";
                var maxLen = Convert.ToInt32(colReader["max_length"]);
                var precision = Convert.ToInt32(colReader["precision"]);
                var scale = Convert.ToInt32(colReader["scale"]);
                var isNullable = Convert.ToBoolean(colReader["is_nullable"]);
                var defaultValue = colReader["default_value"]?.ToString() ?? "";

                var typeDecl = SqlDataTypeFormatter.Format(dataType, maxLen, precision, scale);

                cols.Add(new ColumnInfo
                {
                    Name = colName,
                    DataType = dataType,
                    MaxLength = maxLen,
                    Precision = precision,
                    Scale = scale,
                    IsNullable = isNullable,
                    TypeDecl = typeDecl,
                    DefaultValue = defaultValue
                });
            }
            return cols;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetTableColumns {tableName} 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 生成 CREATE TABLE 脚本（含列定义）
    /// </summary>
    private string GenerateCreateTableScript(List<ColumnInfo> cols, string tableName)
    {
        var columnDefs = new List<string>();
        foreach (var col in cols)
        {
            var def = $"[{col.Name}] {col.TypeDecl} {(col.IsNullable ? "NULL" : "NOT NULL")}";
            if (!string.IsNullOrEmpty(col.DefaultValue))
            {
                def += $" DEFAULT {col.DefaultValue}";
            }
            columnDefs.Add(def);
        }
        return $"CREATE TABLE [{tableName}] ({string.Join(", ", columnDefs)})";
    }

    /// <summary>
    /// 执行脚本：在目标库中执行勾选行的变更脚本
    /// </summary>
    private async void BtnExecute_Click(object? sender, EventArgs e)
    {
        var selectedScripts = GetSelectedScripts();
        if (selectedScripts.Count == 0)
        {
            MessageBox.Show("请先勾选要执行的差异项！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"即将在目标库执行 {selectedScripts.Count} 条脚本，确定吗？\n\n目标库：{_tgtServer} / {_tgtDbName}",
            "确认执行", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        btnExecute.Enabled = false;
        btnCopyScript.Enabled = false;
        btnRefresh.Enabled = false;
        progressBar.Value = 0;
        lblProgress.Text = "开始执行脚本...";

        var (successCount, failCount, errorMsg) = await Task.Run(() => ExecuteScripts(selectedScripts));

        progressBar.Value = 100;
        if (failCount == 0)
        {
            lblProgress.Text = $"执行完成：成功 {successCount} 条，失败 0 条";
            lblProgress.ForeColor = Color.FromArgb(57, 181, 74);
            MessageBox.Show($"执行完成！成功 {successCount} 条。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            lblProgress.Text = $"执行完成：成功 {successCount} 条，失败 {failCount} 条";
            lblProgress.ForeColor = Color.Red;
            MessageBox.Show($"执行完成！成功 {successCount} 条，失败 {failCount} 条。\n\n最后错误：{errorMsg}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        btnExecute.Enabled = true;
        btnCopyScript.Enabled = true;
        btnRefresh.Enabled = true;
    }

    /// <summary>
    /// 同步执行多条脚本，返回 (成功数, 失败数, 错误信息)
    /// </summary>
    private (int success, int fail, string error) ExecuteScripts(List<string> scripts)
    {
        int success = 0, fail = 0;
        string lastError = "";

        try
        {
            var connStr = BuildConnString(_tgtServer, _tgtDbName, _tgtUser, _tgtPassword);
            using var conn = new SqlConnection(connStr);
            conn.Open();

            int total = scripts.Count;
            for (int i = 0; i < scripts.Count; i++)
            {
                var script = scripts[i];
                this.Invoke(new Action(() =>
                {
                    var percent = (int)((double)(i + 1) / total * 100);
                    progressBar.Value = percent;
                    lblProgress.Text = $"执行中：{i + 1}/{total}";
                }));

                try
                {
                    using var cmd = new SqlCommand(script, conn);
                    cmd.ExecuteNonQuery();
                    success++;
                }
                catch (Exception ex)
                {
                    fail++;
                    lastError = ex.Message;
                    Debug.WriteLine($"执行脚本失败：{script}\n{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            return (success, fail + (scripts.Count - success), lastError);
        }

        return (success, fail, lastError);
    }

    /// <summary>
    /// 复制脚本到剪贴板
    /// </summary>
    private void BtnCopyScript_Click(object? sender, EventArgs e)
    {
        var scripts = GetSelectedScripts();
        string textToCopy;

        if (scripts.Count > 0)
        {
            // 有勾选：复制勾选行的脚本
            textToCopy = string.Join("\r\n\r\n", scripts);
        }
        else
        {
            // 无勾选：复制所有差异脚本
            var allScripts = new List<string>();
            foreach (DataGridViewRow row in dgvDifferences.Rows)
            {
                var script = row.Cells["Script"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(script)) allScripts.Add(script);
            }
            if (allScripts.Count == 0)
            {
                MessageBox.Show("暂无差异可复制！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            textToCopy = string.Join("\r\n\r\n", allScripts);
        }

        try
        {
            Clipboard.SetText(textToCopy);
            var msg = scripts.Count > 0
                ? $"已复制 {scripts.Count} 条脚本到剪贴板"
                : $"已复制全部 {textToCopy.Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length} 条脚本到剪贴板";
            lblProgress.Text = msg;
            lblProgress.ForeColor = Color.FromArgb(57, 181, 74);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 提取勾选行的脚本（按表格行顺序）
    /// </summary>
    private List<string> GetSelectedScripts()
    {
        var scripts = new List<string>();
        foreach (DataGridViewRow row in dgvDifferences.Rows)
        {
            var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
            var isChecked = checkCell?.Value != null && (bool)checkCell.Value;
            if (isChecked || row.Selected)
            {
                var script = row.Cells["Script"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(script))
                {
                    scripts.Add(script);
                }
            }
        }
        return scripts;
    }

    private string BuildConnString(string server, string dbName, string user, string password)
    {
        if (string.IsNullOrEmpty(user))
        {
            return $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        }
        var decrypted = EncryptionService.Decrypt(password);
        return $"Server={server};Database={dbName};User Id={user};Password={decrypted};TrustServerCertificate=True;";
    }

    /// <summary>
    /// 差异行数据结构
    /// </summary>
    private class DifferenceRow
    {
        public string TableName { get; set; } = "";
        public string DiffType { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string SrcType { get; set; } = "";
        public string TgtType { get; set; } = "";
        public string Script { get; set; } = "";
    }

    /// <summary>
    /// 列信息数据结构
    /// </summary>
    private class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int MaxLength { get; set; }
        public int Precision { get; set; }
        public int Scale { get; set; }
        public bool IsNullable { get; set; }
        public string TypeDecl { get; set; } = "";
        public string DefaultValue { get; set; } = "";
    }
}
