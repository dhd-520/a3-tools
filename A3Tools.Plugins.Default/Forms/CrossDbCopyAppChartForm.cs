using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyAppChartForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    public CrossDbCopyAppChartForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        FormHotkeyHelper.Setup(this, () => BtnConfirm_Click(this, EventArgs.Empty));
        this.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control) { BtnSelectSource_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D && e.Modifiers == Keys.Control) { BtnSelectTarget_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };
    }

    private void BtnSelectSource_Click(object? sender, EventArgs e)
    {
        SelectAccount(true);
    }

    private void BtnSelectTarget_Click(object? sender, EventArgs e)
    {
        SelectAccount(false);
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.Close();
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
            Left = 20, Top = 45, Width = 540, Height = 30,
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
                {
                    listBox.Items.Add(item);
                }
            }
        }

        PopulateList("");
        txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);
        dialog.KeyPreview = true;
        bool justFocused = false;
        dialog.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Oemtilde) { txtSearch.Focus(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { dialog.Close(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { if (listBox.SelectedIndex >= 0) btnOkClick(); e.SuppressKeyPress = true; }
            else if ((e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && !listBox.Focused && listBox.Items.Count > 0) { listBox.Focus(); listBox.SelectedIndex = 0; justFocused = true; e.SuppressKeyPress = true; }
            else if (justFocused && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) { justFocused = false; e.SuppressKeyPress = true; }
        };
        txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Oemtilde) { txtSearch.SelectionStart = 0; txtSearch.SelectionLength = txtSearch.Text.Length; e.SuppressKeyPress = true; } };
        var btnOk = new Button { Text = "确定", Left = 170, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };

        void btnOkClick()
        {
            if (listBox.SelectedIndex >= 0)
            {
                var selectedText = listBox.SelectedItem?.ToString() ?? "";
                var selectedAcc = accounts.FirstOrDefault(a => (a.Code + " - " + a.Name) == selectedText);
                if (selectedAcc != null)
                {
                    if (isSource)
                    {
                        txtSourceServer.Text = selectedAcc.Database ?? "";
                        txtSourceDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtSourceUser.Text = selectedAcc.DbUser ?? "";
                        txtSourcePassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    else
                    {
                        txtTargetServer.Text = selectedAcc.Database ?? "";
                        txtTargetDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtTargetUser.Text = selectedAcc.DbUser ?? "";
                        txtTargetPassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    dialog.Close();
                }
            }
        }
        btnOk.Click += (s, e) => btnOkClick();
        var btnCancelDialog = new Button { Text = "取消", Left = 310, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };
        btnCancelDialog.Click += (s, e) => dialog.Close();
        listBox.DoubleClick += (s, e) => btnOkClick();

        dialog.Controls.Add(btnOk);
        dialog.Controls.Add(btnCancelDialog);
        dialog.ShowDialog();
    }

    private async void BtnConfirm_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text))
        {
            MessageBox.Show("请填写源数据库地址！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtTargetServer.Text))
        {
            MessageBox.Show("请填写目标数据库地址！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtCode.Text))
        {
            MessageBox.Show("请输入看板CODE！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var codes = txtCode.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray();
        if (codes.Length == 0)
        {
            MessageBox.Show("请输入有效的看板CODE！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        lblProgress.Text = "正在连接源数据库...";
        progressBar.Value = 10;

        if (!await TestConnectionAsync(txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text))
        {
            MessageBox.Show("源数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在连接目标数据库...";
        progressBar.Value = 30;

        if (!await TestConnectionAsync(txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text))
        {
            MessageBox.Show("目标数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在复制看板...";
        progressBar.Value = 50;

        var success = await CopyAppChartAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            codes, chkDeleteFirst.Checked);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
        }
        else
        {
            progressBar.Value = 0;
            lblProgress.Text = "";
        }
    }

    private async Task<bool> TestConnectionAsync(string server, string dbName, string user, string password)
    {
        return await Task.Run(() =>
        {
            try
            {
                var connStr = "Server=" + server + ";Database=" + dbName + ";User Id=" + user + ";Password=" + EncryptionService.Decrypt(password) + ";TrustServerCertificate=True;";
                using var conn = new SqlConnection(connStr);
                conn.Open();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("连接测试失败: " + ex.Message);
                return false;
            }
        });
    }

    private async Task<bool> CopyAppChartAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        string[] codes, bool deleteFirst)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";Database=" + srcDbName + ";User Id=" + srcUser + ";Password=" + EncryptionService.Decrypt(srcPassword) + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";Database=" + tgtDbName + ";User Id=" + tgtUser + ";Password=" + EncryptionService.Decrypt(tgtPassword) + ";TrustServerCertificate=True;";

                using var srcConn = new SqlConnection(srcConnStr);
                using var tgtConn = new SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                var total = codes.Length;
                var successCount = 0;
                var failCodes = new List<string>();

                for (int i = 0; i < codes.Length; i++)
                {
                    var code = codes[i];
                    var current = i + 1;
                    lblProgress.Invoke(new Action(() => lblProgress.Text = $"正在处理：{code} ({current}/{total})"));
                    progressBar.Invoke(new Action(() => progressBar.Value = current * 100 / total));

                    var guid = GetAppChartGuid(srcConn, code);
                    if (string.IsNullOrEmpty(guid))
                    {
                        failCodes.Add(code + "(未找到)");
                        continue;
                    }

                    CopyTableData(srcConn, tgtConn, "S_APP_CHART", "GUID", guid, deleteFirst);
                    CopyTableDataByParentGuid(srcConn, tgtConn, "S_APP_CHARTDETAIL", "CHARTGUID", guid, deleteFirst);
                    CopyTableDataByParentGuid(srcConn, tgtConn, "S_APP_CHARTDATASOURCE", "CHARTGUID", guid, deleteFirst);
                    CopyTableDataByParentGuid(srcConn, tgtConn, "S_APP_CHARTROLE", "CHARTGUID", guid, deleteFirst);
                    CopyTableDataByParentGuid(srcConn, tgtConn, "S_APP_CHARTCMD", "CHARTGUID", guid, deleteFirst);
                    successCount++;
                }

                progressBar.Invoke(new Action(() => progressBar.Value = 100));
                this.Invoke(new Action(() =>
                {
                    if (failCodes.Count > 0)
                    {
                        MessageBox.Show($"完成！成功 {successCount} 个，失败 {failCodes.Count} 个：\n{string.Join("\n", failCodes)}", "部分成功", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"移动看板复制完成！共 {successCount} 个。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    this.Close();
                }));
                return true;
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("复制失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
                return false;
            }
        });
    }

    private string? GetAppChartGuid(SqlConnection conn, string code)
    {
        var sql = "SELECT GUID FROM dbo.S_APP_CHART WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@code", code);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    private void CopyTableData(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string whereField, string whereValue, bool deleteFirst)
    {
        try
        {
            var tgtColumns = GetTableColumns(tgtConn, tableName);
            if (tgtColumns.Count == 0) return;

            if (deleteFirst)
            {
                var deleteSql = $"DELETE FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var deleteCmd = new SqlCommand(deleteSql, tgtConn);
                deleteCmd.Parameters.AddWithValue("@value", whereValue);
                deleteCmd.ExecuteNonQuery();
            }
            else
            {
                var checkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var checkCmd = new SqlCommand(checkSql, tgtConn);
                checkCmd.Parameters.AddWithValue("@value", whereValue);
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                {
                    Debug.WriteLine($"表{tableName}中{whereField}={whereValue}已存在，跳过");
                    return;
                }
            }

            var selectColumns = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
            var selectSql = $"SELECT {selectColumns} FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
            using var selectCmd = new SqlCommand(selectSql, srcConn);
            selectCmd.Parameters.AddWithValue("@value", whereValue);

            var dataTable = new System.Data.DataTable();
            using (var adapter = new SqlDataAdapter(selectCmd))
            {
                adapter.Fill(dataTable);
            }

            if (dataTable.Rows.Count == 0) return;

            using var bulkCopy = new SqlBulkCopy(tgtConn);
            bulkCopy.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in tgtColumns)
            {
                bulkCopy.ColumnMappings.Add(col, col);
            }
            bulkCopy.WriteToServer(dataTable);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"复制表{tableName}失败: {ex.Message}");
            throw;
        }
    }

    private void CopyTableDataByParentGuid(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string parentField, string parentGuid, bool deleteFirst)
    {
        try
        {
            var tgtColumns = GetTableColumns(tgtConn, tableName);
            if (tgtColumns.Count == 0) return;

            if (deleteFirst)
            {
                var deleteSql = $"DELETE FROM dbo.[{tableName}] WHERE [{parentField}] = @parentGuid";
                using var deleteCmd = new SqlCommand(deleteSql, tgtConn);
                deleteCmd.Parameters.AddWithValue("@parentGuid", parentGuid);
                deleteCmd.ExecuteNonQuery();
            }

            var selectColumns = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
            var selectSql = $"SELECT {selectColumns} FROM dbo.[{tableName}] WHERE [{parentField}] = @parentGuid";
            using var selectCmd = new SqlCommand(selectSql, srcConn);
            selectCmd.Parameters.AddWithValue("@parentGuid", parentGuid);

            var dataTable = new System.Data.DataTable();
            using (var adapter = new SqlDataAdapter(selectCmd))
            {
                adapter.Fill(dataTable);
            }

            if (dataTable.Rows.Count == 0) return;

            using var bulkCopy = new SqlBulkCopy(tgtConn);
            bulkCopy.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in tgtColumns)
            {
                bulkCopy.ColumnMappings.Add(col, col);
            }
            bulkCopy.WriteToServer(dataTable);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"复制表{tableName}失败: {ex.Message}");
            throw;
        }
    }

    private List<string> GetTableColumns(SqlConnection conn, string tableName)
    {
        var columns = new List<string>();
        var sql = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = 'dbo'
            ORDER BY ORDINAL_POSITION";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        reader.Close();
        return columns;
    }
}