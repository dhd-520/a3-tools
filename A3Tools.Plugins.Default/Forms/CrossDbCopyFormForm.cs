using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyFormForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    public CrossDbCopyFormForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
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

    private void LoadCurrentAccount()
    {
        if (_currentAccount != null)
        {
            txtSourceServer.Text = _currentAccount.Database ?? "";
            txtSourceUser.Text = _currentAccount.DbUser ?? "";
            txtSourcePassword.Text = _currentAccount.DbPassword ?? "";
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
            Left = 20, Top = 45, Width = 540, Height = 30,
            Font = new Font("微软雅黑", 11F),
            PlaceholderText = "输入账套编码或名称搜索..."
        };
        dialog.Controls.Add(txtSearch);

        var listBox = new ListBox { Left = 20, Top = 85, Width = 540, Height = 380, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(listBox);

        // 添加账套到列表
        void PopulateList(string filter)
        {
            listBox.Items.Clear();
            foreach (var acc in accounts)
            {
                var item = acc.Code + " - " + acc.Name;
                // 支持编码、名称、拼音首字母搜索
                bool matchCode = (acc.Code ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchName = (acc.Name ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchPinyin = (acc.Pinyin ?? "").Contains(filter.ToLower(), StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(filter) || matchCode || matchName || matchPinyin)
                {
                    listBox.Items.Add(item);
                }
            }
        }

        // 初始填充
        PopulateList("");

        // 搜索事件
        txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);

        var btnOk = new Button { Text = "确定", Left = 170, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };
        var btnCancelDialog = new Button { Text = "取消", Left = 310, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };

        btnOk.Click += (s, e) =>
        {
            if (listBox.SelectedIndex >= 0)
            {
                // 从listBox选中项获取对应的账套（通过显示文本匹配）
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
        };
        btnCancelDialog.Click += (s, e) => dialog.Close();
        listBox.DoubleClick += (s, e) => btnOk.PerformClick();

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
        if (string.IsNullOrWhiteSpace(txtObjectGuids.Text))
        {
            MessageBox.Show("请输入要复制的表单OBJECTGUID！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        lblProgress.Text = "正在连接源数据库...";
        progressBar.Value = 10;

        if (!await TestConnectionAsync(txtSourceServer.Text, txtSourceUser.Text, txtSourcePassword.Text))
        {
            MessageBox.Show("源数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在连接目标数据库...";
        progressBar.Value = 30;

        if (!await TestConnectionAsync(txtTargetServer.Text, txtTargetUser.Text, txtTargetPassword.Text))
        {
            MessageBox.Show("目标数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在复制表单...";
        progressBar.Value = 50;

        var success = await CopyFormsAsync(
            txtSourceServer.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetUser.Text, txtTargetPassword.Text,
            txtObjectGuids.Text.Trim(), chkDeleteFirst.Checked);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            MessageBox.Show("表单复制完成！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
        else
        {
            progressBar.Value = 0;
            lblProgress.Text = "";
        }
    }

    private async Task<bool> TestConnectionAsync(string server, string user, string password)
    {
        return await Task.Run(() =>
        {
            try
            {
                var connStr = "Server=" + server + ";User Id=" + user + ";Password=" + password + ";TrustServerCertificate=True;";
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

    private async Task<bool> CopyFormsAsync(
        string srcServer, string srcUser, string srcPassword,
        string tgtServer, string tgtUser, string tgtPassword,
        string objectGuids, bool deleteFirst)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";User Id=" + srcUser + ";Password=" + srcPassword + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";User Id=" + tgtUser + ";Password=" + tgtPassword + ";TrustServerCertificate=True;";

                using var srcConn = new SqlConnection(srcConnStr);
                using var tgtConn = new SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                // 解析OBJECTGUID列表
                var guidList = objectGuids.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim()).ToList();

                int total = guidList.Count;
                int current = 0;

                foreach (var objectGuid in guidList)
                {
                    current++;
                    var progress = 30 + (current * 70 / total);
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progress;
                        lblProgress.Text = "正在复制：" + objectGuid + " (" + current + "/" + total + ")";
                    }));

                    // 复制S_OBJECT表
                    CopyTableData(srcConn, tgtConn, "S_OBJECT", "GUID", objectGuid, deleteFirst);

                    // 复制S_CONTROL表
                    CopyTableData(srcConn, tgtConn, "S_CONTROL", "OBJECTGUID", objectGuid, deleteFirst);

                    // 复制S_DATA表
                    CopyTableData(srcConn, tgtConn, "S_DATA", "OBJECTGUID", objectGuid, deleteFirst);
                }

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

    private void CopyTableData(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string whereField, string whereValue, bool deleteFirst)
    {
        try
        {
            // 获取目标表的列信息
            var tgtColumns = GetTableColumns(tgtConn, tableName);
            if (tgtColumns.Count == 0)
            {
                Debug.WriteLine($"目标表{tableName}不存在或没有列");
                return;
            }

            // 如果需要先删除
            if (deleteFirst)
            {
                var deleteSql = $"DELETE FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var deleteCmd = new SqlCommand(deleteSql, tgtConn);
                deleteCmd.Parameters.AddWithValue("@value", whereValue);
                deleteCmd.ExecuteNonQuery();
            }

            // 从源数据库读取数据 - 只选择目标表存在的列
            var commonColumns = tgtColumns.ToHashSet();
            var selectColumns = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
            var selectSql = $"SELECT {selectColumns} FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
            using var selectCmd = new SqlCommand(selectSql, srcConn);
            selectCmd.Parameters.AddWithValue("@value", whereValue);
            using var reader = selectCmd.ExecuteReader();

            var rows = new List<Dictionary<string, object?>>();

            // 获取列信息
            if (reader.Read())
            {
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                do
                {
                    var row = new Dictionary<string, object?>();
                    foreach (var col in columns)
                    {
                        row[col] = reader[col];
                    }
                    rows.Add(row);
                } while (reader.Read());
            }
            reader.Close();

            if (rows.Count == 0) return;

            // 检查是否已存在
            if (!deleteFirst)
            {
                var checkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var checkCmd = new SqlCommand(checkSql, tgtConn);
                checkCmd.Parameters.AddWithValue("@value", whereValue);
                var count = Convert.ToInt32(checkCmd.ExecuteScalar());
                if (count > 0)
                {
                    Debug.WriteLine($"表{tableName}中{whereField}={whereValue}已存在，跳过");
                    return;
                }
            }

            // 插入数据 - 使用目标表的列
            foreach (var row in rows)
            {
                var columnNames = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
                var paramNames = string.Join(", ", tgtColumns.Select(c => "@" + c));
                var insertSql = $"INSERT INTO dbo.[{tableName}] ({columnNames}) VALUES ({paramNames})";

                using var insertCmd = new SqlCommand(insertSql, tgtConn);
                foreach (var col in tgtColumns)
                {
                    var value = row.ContainsKey(col) ? row[col] : null;
                    insertCmd.Parameters.AddWithValue("@" + col, value ?? DBNull.Value);
                }
                insertCmd.ExecuteNonQuery();
            }
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
