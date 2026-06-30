using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 自定义通用复制工具窗体（由「自定义工具」配置驱动）。
/// 布局完全对齐 <see cref="CrossDbCopyReportForm"/>：仅复制主表/复制关键字/关联表/关联字段由配置驱动。
/// </summary>
public partial class GenericCopyToolForm : Form
{
    private readonly IToolContext _context;
    private readonly CustomToolConfig _config;
    private System.Data.DataTable? _searchResults;

    public GenericCopyToolForm(IToolContext context, CustomToolConfig config)
    {
        _context = context;
        _config = config;
        InitializeComponent();
        LoadPresetAccounts();
        FormHotkeyHelper.Setup(this, () => BtnConfirm_Click(this, EventArgs.Empty));
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control) { BtnSelectSource_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D && e.Modifiers == Keys.Control) { BtnSelectTarget_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };

        // 顶部信息
        Text = string.IsNullOrEmpty(config.Name) ? "自定义工具" : $"自定义工具 — {config.Name}";
        lblTitleHint.Text = $"复制关键字（{_config.MainTable}.{_config.PrimaryKey}，多个用英文分号 ; 隔开）：";
        txtKeyValues.PlaceholderText = $"输入{_config.PrimaryKey}，可通过下方搜索添加";
        lblSearchHint.Text = BuildSearchHintText();
        lblConfigInfo.Text = BuildConfigInfoText();
        btnConfirm.Text = "确认复制";

        // 数据网格视图支持多选
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.MultiSelect = true;

        dgvSearchResults.SelectionChanged += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk")) return;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                if (row.Cells["chk"] is DataGridViewCheckBoxCell checkCell)
                    checkCell.Value = row.Selected;
            }
        };

        dgvSearchResults.ColumnHeaderMouseClick += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk") || e.ColumnIndex != 0) return;
            var allChecked = true;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                if (row.Cells["chk"] is not DataGridViewCheckBoxCell checkCell || checkCell.Value is not bool b || !b)
                {
                    allChecked = false;
                    break;
                }
            }
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                if (row.Cells["chk"] is DataGridViewCheckBoxCell checkCell)
                {
                    checkCell.Value = !allChecked;
                    row.Selected = !allChecked;
                }
            }
        };
    }

    private string BuildConfigInfoText()
    {
        var related = _config.RelatedTableList;
        var relatedDesc = related.Count == 0 ? "（无）" : string.Join(" / ", related);
        var foreignKey = string.IsNullOrEmpty(_config.ForeignKey) ? "（无）" : _config.ForeignKey;
        var searchCols = _config.SearchColumnList;
        var searchDesc = searchCols.Count == 0 ? "全部列" : string.Join(",", searchCols);
        var hiddenCols = _config.HiddenColumnSet;
        var hiddenDesc = hiddenCols.Count == 0 ? string.Empty : $"    隐藏：{string.Join(",", hiddenCols)}";
        return $"主表：{_config.MainTable}    主键：{_config.PrimaryKey}    关联表：{relatedDesc}    关联字段：{foreignKey}    搜索列：{searchDesc}{hiddenDesc}";
    }

    private string BuildSearchHintText()
    {
        var searchCols = _config.SearchColumnList;
        if (searchCols.Count == 0)
            return $"提示：输入{_config.MainTable}表的{_config.PrimaryKey}进行搜索";
        return $"提示：按以下列模糊搜索 — {string.Join(",", searchCols)}";
    }

    private void BtnSelectSource_Click(object? sender, EventArgs e) => SelectAccount(true);
    private void BtnSelectTarget_Click(object? sender, EventArgs e) => SelectAccount(false);
    private void BtnCancel_Click(object? sender, EventArgs e) => Close();

    /// <summary>
    /// 根据主窗体工具箱 Tab 中的源/目标预选账套自动带入连接信息。
    /// </summary>
    private void LoadPresetAccounts()
    {
        var preset = _context.GetToolDatabasePreset();
        ApplyAccountToDatabaseFields(preset.SourceAccount, true);
        ApplyAccountToDatabaseFields(preset.TargetAccount, false);
    }

    private void ApplyAccountToDatabaseFields(Account? account, bool isSource)
    {
        if (account == null) return;
        if (isSource)
        {
            txtSourceServer.Text = account.Database ?? "";
            txtSourceDbName.Text = account.DatabaseName ?? "";
            txtSourceUser.Text = account.DbUser ?? "";
            txtSourcePassword.Text = account.DbPassword ?? "";
        }
        else
        {
            txtTargetServer.Text = account.Database ?? "";
            txtTargetDbName.Text = account.DatabaseName ?? "";
            txtTargetUser.Text = account.DbUser ?? "";
            txtTargetPassword.Text = account.DbPassword ?? "";
        }
    }

    private void SelectAccount(bool isSource)
    {
        var accounts = _context.GetAllAccounts();
        if (accounts.Count == 0)
        {
            MessageBox.Show("没有可用的账套！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = "选择账套",
            Size = new Size(600, 600),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White
        };

        var lbl = new Label { Text = "请选择账套（支持搜索）", Left = 20, Top = 15, Width = 540, Height = 25, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(lbl);

        var txtSearch = new TextBox
        {
            Left = 20,
            Top = 45,
            Width = 540,
            Height = 30,
            Font = new Font("微软雅黑", 11F),
            PlaceholderText = "输入账套编码或名称搜索..."
        };
        dialog.Controls.Add(txtSearch);

        var listBox = new ListBox { Left = 20, Top = 85, Width = 540, Height = 380, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(listBox);

        void PopulateList(string filter)
        {
            listBox.Items.Clear();
            foreach (var acc in accounts)
            {
                var item = acc.Code + " - " + acc.Name;
                bool matchCode = (acc.Code ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchName = (acc.Name ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchPinyin = (acc.Pinyin ?? "").Contains(filter.ToLower(), StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(filter) || matchCode || matchName || matchPinyin)
                    listBox.Items.Add(item);
            }
        }

        PopulateList("");
        txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);
        dialog.KeyPreview = true;
        bool justFocused = false;
        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Oemtilde) { txtSearch.Focus(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { dialog.Close(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { if (listBox.SelectedIndex >= 0) ApplyAndClose(); e.SuppressKeyPress = true; }
            else if ((e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && !listBox.Focused && listBox.Items.Count > 0) { listBox.Focus(); listBox.SelectedIndex = 0; justFocused = true; e.SuppressKeyPress = true; }
            else if (justFocused && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) { justFocused = false; e.SuppressKeyPress = true; }
        };
        txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Oemtilde) { txtSearch.SelectionStart = 0; txtSearch.SelectionLength = txtSearch.Text.Length; e.SuppressKeyPress = true; } };

        void ApplyAndClose()
        {
            if (listBox.SelectedIndex < 0) return;
            var selectedText = listBox.SelectedItem?.ToString() ?? "";
            var selectedAcc = accounts.FirstOrDefault(a => (a.Code + " - " + a.Name) == selectedText);
            if (selectedAcc != null)
            {
                ApplyAccountToDatabaseFields(selectedAcc, isSource);
                dialog.Close();
            }
        }

        var btnOk = new Button { Text = "确定", Left = 170, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };
        var btnCancelDialog = new Button { Text = "取消", Left = 310, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };
        btnOk.Click += (s, e) => ApplyAndClose();
        btnCancelDialog.Click += (s, e) => dialog.Close();
        listBox.DoubleClick += (s, e) => ApplyAndClose();

        dialog.Controls.Add(btnOk);
        dialog.Controls.Add(btnCancelDialog);
        dialog.ShowDialog(this);
    }

    private string BuildConnStr(string server, string dbName, string user, string password)
    {
        if (string.IsNullOrEmpty(user))
            return $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        var pwd = string.IsNullOrEmpty(password) ? "" : EncryptionService.Decrypt(password);
        return $"Server={server};Database={dbName};User Id={user};Password={pwd};TrustServerCertificate=True;";
    }

    private bool TestConn(string connStr)
    {
        try
        {
            using var conn = new SqlConnection(connStr);
            conn.Open();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("连接测试失败: " + ex.Message);
            return false;
        }
    }

    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        if (!ValidateTableName(_config.MainTable) || !ValidateFieldName(_config.PrimaryKey))
        {
            MessageBox.Show("配置里的主表或复制关键字不合法。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var keyword = txtSearchKeyword.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show("请输入搜索关键字！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSearchKeyword.Focus();
            return;
        }

        var srcConnStr = BuildConnStr(txtSourceServer.Text.Trim(), txtSourceDbName.Text.Trim(), txtSourceUser.Text.Trim(), txtSourcePassword.Text);
        if (!TestConn(srcConnStr))
        {
            MessageBox.Show("源数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        lblSearchProgress.Text = "查询中...";
        lblSearchProgress.ForeColor = Color.Blue;
        dgvSearchResults.DataSource = null;
        btnSearch.Enabled = false;

        try
        {
            var validSearchCols = new List<string>();
            _searchResults = await Task.Run(() =>
            {
                using var conn = new SqlConnection(srcConnStr);
                conn.Open();
                var dbColumns = GetAllColumns(conn, _config.MainTable);
                var dbSet = new HashSet<string>(dbColumns, StringComparer.OrdinalIgnoreCase);
                var pk = _config.PrimaryKey;
                var configuredCols = _config.SearchColumnList;
                if (configuredCols.Count > 0)
                {
                    // 新行为：用配置的列，过滤出数据库中真实存在的列
                    foreach (var c in configuredCols)
                        if (dbSet.Contains(c)) validSearchCols.Add(c);
                    // 保证 PrimaryKey 总是参与搜索（即使没有写在 SearchColumns 中）
                    if (!string.IsNullOrEmpty(pk) && !validSearchCols.Any(c => string.Equals(c, pk, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (dbSet.Contains(pk)) validSearchCols.Insert(0, pk);
                    }
                }
                else
                {
                    // 旧行为：主键 + 可选的 NAME
                    if (!string.IsNullOrEmpty(pk) && dbSet.Contains(pk)) validSearchCols.Add(pk);
                    if (dbSet.Contains("NAME")) validSearchCols.Add("NAME");
                }

                if (validSearchCols.Count == 0)
                    throw new InvalidOperationException("未能找到任何可用于搜索的列，请检查搜索列配置。");

                var sql = BuildSearchSql(validSearchCols);
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new System.Data.DataTable();
                adapter.Fill(dt);
                return dt;
            });

            BindSearchResults(_searchResults);
            lblSearchProgress.Text = $"查询完成，共 {_searchResults.Rows.Count} 条。";
            lblSearchProgress.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblSearchProgress.Text = "查询失败";
            lblSearchProgress.ForeColor = Color.Red;
        }
        finally
        {
            btnSearch.Enabled = true;
        }
    }

    /// <summary>
    /// 根据已经通过数据库校验的搜索列集合拼 SQL。
    /// 会再按大小写不敏感去重一次，保持参数顺序。
    /// </summary>
    private string BuildSearchSql(List<string> validSearchColumns)
    {
        var table = _config.MainTable;
        var pk = _config.PrimaryKey;

        // 去重（按大小写不敏感）保持顺序
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueCols = new List<string>();
        foreach (var c in validSearchColumns)
            if (!string.IsNullOrWhiteSpace(c) && seen.Add(c)) uniqueCols.Add(c);

        var whereClauses = uniqueCols
            .Select(c => $"CONVERT(NVARCHAR(4000), [{c}]) LIKE @keyword")
            .ToList();
        var where = string.Join(" OR ", whereClauses);

        return $@"
SELECT TOP 5000 *
FROM dbo.[{table}]
WHERE {where}
ORDER BY [{pk}]";
    }

    private List<string> GetAllColumns(SqlConnection conn, string tableName)
    {
        const string sql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME=@tableName";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    private void BindSearchResults(System.Data.DataTable dt)
    {
        if (dgvSearchResults.Columns.Contains("chk"))
            dgvSearchResults.Columns.Remove("chk");

        dgvSearchResults.DataSource = dt;
        ApplySearchColumnLayout();
        dgvSearchResults.AutoResizeColumns();
    }

    /// <summary>
    /// 根据配置的 SearchColumns / ColumnDisplayNames / HiddenColumns 过滤可见列、设置列标题。
    /// PrimaryKey 始终可见（即使被 HiddenColumns 标记）。SearchColumns 为空时保留旧行为（所有列都显示）。
    /// </summary>
    private void ApplySearchColumnLayout()
    {
        var searchCols = _config.SearchColumnList;
        var displayNames = _config.ColumnDisplayNameList;
        var hiddenSet = _config.HiddenColumnSet;
        var pk = _config.PrimaryKey;

        if (searchCols.Count == 0)
        {
            // 旧行为：仅主键列重命名为 PrimaryKey，其它保持原名
            foreach (DataGridViewColumn col in dgvSearchResults.Columns)
            {
                if (!string.IsNullOrEmpty(pk) && string.Equals(col.Name, pk, StringComparison.OrdinalIgnoreCase))
                    col.HeaderText = pk;
            }
            return;
        }

        // 按配置顺序重排、隐藏不在列表中或在 HiddenColumns 中的列。
        var displayOrdered = searchCols
            .Select((name, i) => new { Name = name, Display = i < displayNames.Count ? displayNames[i] : name })
            .ToList();

        // 确保 PrimaryKey 始终显示
        var visibleNames = new List<string>();
        var headerByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in displayOrdered)
        {
            var exists = dgvSearchResults.Columns.Contains(item.Name);
            if (!exists) continue;
            if (hiddenSet.Contains(item.Name) && !string.Equals(item.Name, pk, StringComparison.OrdinalIgnoreCase)) continue;
            visibleNames.Add(item.Name);
            headerByName[item.Name] = item.Display;
        }
        // PrimaryKey 补位
        if (!string.IsNullOrEmpty(pk)
            && dgvSearchResults.Columns.Contains(pk)
            && !visibleNames.Any(n => string.Equals(n, pk, StringComparison.OrdinalIgnoreCase)))
        {
            visibleNames.Insert(0, pk);
            headerByName[pk] = pk;
        }

        var ordered = new List<DataGridViewColumn>();
        foreach (var name in visibleNames)
            ordered.Add(dgvSearchResults.Columns[name]);

        foreach (DataGridViewColumn col in dgvSearchResults.Columns)
        {
            if (col.Name == "chk") continue;
            if (!ordered.Contains(col))
                col.Visible = false;
        }

        // 按 visibleNames 顺序重新设置 DisplayIndex
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].DisplayIndex = i + 1;  // 0 位留给 chk

        // 设置 HeaderText
        foreach (var col in ordered)
        {
            if (headerByName.TryGetValue(col.Name, out var display))
                col.HeaderText = display;
        }
    }

    private void BtnAddSelected_Click(object? sender, EventArgs e)
    {
        if (dgvSearchResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先选择要添加的数据！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedKeys = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            var key = row.Cells[_config.PrimaryKey].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(key)) selectedKeys.Add(key);
        }

        var currentText = txtKeyValues.Text.Trim();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
            currentText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList().ForEach(x => existing.Add(x));

        var newKeys = selectedKeys.Where(k => !existing.Contains(k)).ToList();
        if (newKeys.Count == 0)
        {
            MessageBox.Show("选中的数据已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        txtKeyValues.Text = currentText + (string.IsNullOrEmpty(currentText) ? "" : ";") + string.Join(";", newKeys);
        lblSearchProgress.Text = $"已添加 {newKeys.Count} 条到复制列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        txtKeyValues.Text = "";
        dgvSearchResults.ClearSelection();
        if (dgvSearchResults.Columns.Contains("chk"))
        {
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                if (row.Cells["chk"] is DataGridViewCheckBoxCell checkCell) checkCell.Value = false;
            }
        }
        lblSearchProgress.Text = "已清空选项";
        lblSearchProgress.ForeColor = Color.Gray;
    }

    private async void BtnConfirm_Click(object? sender, EventArgs e)
    {
        var keyValues = txtKeyValues.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (keyValues.Length == 0)
        {
            MessageBox.Show("请先填写或添加复制关键字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var srcConnStr = BuildConnStr(txtSourceServer.Text.Trim(), txtSourceDbName.Text.Trim(), txtSourceUser.Text.Trim(), txtSourcePassword.Text);
        var tgtConnStr = BuildConnStr(txtTargetServer.Text.Trim(), txtTargetDbName.Text.Trim(), txtTargetUser.Text.Trim(), txtTargetPassword.Text);
        if (!TestConn(srcConnStr)) { MessageBox.Show("源数据库连接失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        if (!TestConn(tgtConnStr)) { MessageBox.Show("目标数据库连接失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        var relatedTables = _config.RelatedTableList;
        var tag = $"[{_config.Name}]";
        var errors = new List<string>();
        var success = 0;

        btnConfirm.Enabled = false;
        progressBar.Value = 0;
        lblProgress.Text = $"开始复制 {keyValues.Length} 条...";

        try
        {
            await Task.Run(() =>
            {
                using var srcConn = new SqlConnection(srcConnStr);
                using var tgtConn = new SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                for (int i = 0; i < keyValues.Length; i++)
                {
                    var key = keyValues[i];
                    try
                    {
                        var parentGuid = GetMainRowGuid(srcConn, _config.MainTable, _config.PrimaryKey, key);
                        TableCopyService.CopyTableData(srcConn, tgtConn, _config.MainTable, _config.PrimaryKey, key, chkDeleteFirst.Checked, tag);

                        if (!string.IsNullOrEmpty(parentGuid) && !string.IsNullOrWhiteSpace(_config.ForeignKey))
                        {
                            foreach (var related in relatedTables)
                                TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, related, _config.ForeignKey, parentGuid, chkDeleteFirst.Checked, tag);
                        }
                        else if (relatedTables.Count > 0)
                        {
                            errors.Add($"{key}: 未找到主表 GUID/OBJECTGUID，关联表未复制");
                        }

                        success++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{key}: {ex.Message}");
                    }

                    var done = i + 1;
                    Invoke(new Action(() =>
                    {
                        progressBar.Value = done * 100 / keyValues.Length;
                        lblProgress.Text = $"已处理 {done}/{keyValues.Length}（成功 {success}）";
                    }));
                }
            });

            progressBar.Value = 100;
            lblProgress.Text = $"完成：成功 {success}/{keyValues.Length}";
            if (errors.Count > 0)
            {
                var preview = string.Join("\n", errors.Take(10));
                if (errors.Count > 10) preview += $"\n... 还有 {errors.Count - 10} 条错误";
                MessageBox.Show($"复制完成，但有 {errors.Count} 条错误：\n\n{preview}", "部分失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show($"复制完成！成功 {success} 条。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制过程出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = $"失败：{ex.Message}";
        }
        finally
        {
            btnConfirm.Enabled = true;
        }
    }

    private static string? GetMainRowGuid(SqlConnection conn, string tableName, string primaryKey, string keyValue)
    {
        var hasGuidSql = @"
SELECT TOP 1 COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME=@tableName AND COLUMN_NAME IN ('GUID','OBJECTGUID')
ORDER BY CASE COLUMN_NAME WHEN 'GUID' THEN 0 ELSE 1 END";
        using var colCmd = new SqlCommand(hasGuidSql, conn);
        colCmd.Parameters.AddWithValue("@tableName", tableName);
        var guidColumn = colCmd.ExecuteScalar()?.ToString();
        if (string.IsNullOrEmpty(guidColumn)) return null;

        var sql = $"SELECT TOP 1 [{guidColumn}] FROM dbo.[{tableName}] WHERE [{primaryKey}] = @value";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@value", keyValue);
        return cmd.ExecuteScalar()?.ToString();
    }

    private static bool ValidateTableName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static bool ValidateFieldName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');
}