using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyTableForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    public CrossDbCopyTableForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        LoadCurrentAccount();
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
            Size = new Size(500, 400),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White
        };

        var lbl = new Label { Text = "请选择账套", Left = 20, Top = 15, Width = 440, Height = 25, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(lbl);

        var listBox = new ListBox { Left = 20, Top = 45, Width = 440, Height = 260, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(listBox);

        foreach (var acc in accounts)
        {
            listBox.Items.Add(acc.Code + " - " + acc.Name);
        }

        var btnOk = new Button { Text = "确定", Left = 120, Top = 320, Width = 100, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };
        var btnCancelDialog = new Button { Text = "取消", Left = 240, Top = 320, Width = 100, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };

        btnOk.Click += (s, e) =>
        {
            if (listBox.SelectedIndex >= 0)
            {
                var selectedAcc = accounts[listBox.SelectedIndex];
                if (isSource)
                {
                    txtSourceServer.Text = selectedAcc.Database ?? "";
                    txtSourceUser.Text = selectedAcc.DbUser ?? "";
                    txtSourcePassword.Text = selectedAcc.DbPassword ?? "";
                }
                else
                {
                    txtTargetServer.Text = selectedAcc.Database ?? "";
                    txtTargetUser.Text = selectedAcc.DbUser ?? "";
                    txtTargetPassword.Text = selectedAcc.DbPassword ?? "";
                }
                dialog.Close();
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
        if (string.IsNullOrWhiteSpace(txtTables.Text))
        {
            MessageBox.Show("请输入要复制的表名称！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var tableNames = txtTables.Text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();

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

        lblProgress.Text = "正在复制表结构...";
        progressBar.Value = 50;

        var success = await CopyTablesAsync(
            txtSourceServer.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetUser.Text, txtTargetPassword.Text,
            tableNames);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            MessageBox.Show("成功复制 " + tableNames.Count + " 个表的结构！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
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

    private async Task<bool> CopyTablesAsync(
        string srcServer, string srcUser, string srcPassword,
        string tgtServer, string tgtUser, string tgtPassword,
        List<string> tableNames)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";User Id=" + srcUser + ";Password=" + srcPassword + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";User Id=" + tgtUser + ";Password=" + tgtPassword + ";TrustServerCertificate=True;";

                using var srcConn = new Microsoft.Data.SqlClient.SqlConnection(srcConnStr);
                using var tgtConn = new Microsoft.Data.SqlClient.SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                int total = tableNames.Count;
                int current = 0;

                foreach (var tableName in tableNames)
                {
                    current++;
                    var progress = 50 + (current * 50 / total);
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progress;
                        lblProgress.Text = "正在复制：" + tableName + " (" + current + "/" + total + ")";
                    }));

                    var scripts = GenerateCreateTableScripts(srcConn, tableName);
                    foreach (var script in scripts)
                    {
                        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(script, tgtConn);
                        cmd.ExecuteNonQuery();
                    }
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

    private List<string> GenerateCreateTableScripts(Microsoft.Data.SqlClient.SqlConnection conn, string tableName)
    {
        var scripts = new List<string>();
        try
        {
            var columnsSql = @"
                SELECT c.name, t.name AS data_type, c.max_length, c.precision, c.scale, c.is_nullable,
                       CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
                       dc.definition AS default_value
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN (SELECT ic.column_id, ic.object_id FROM sys.index_columns ic JOIN sys.indexes i ON ic.index_id = i.index_id AND ic.object_id = i.object_id WHERE i.is_primary_key = 1) pk
                       ON c.column_id = pk.column_id AND c.object_id = pk.object_id
                LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
                WHERE c.object_id = OBJECT_ID(@tableName)
                ORDER BY c.column_id";

            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(columnsSql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var reader = cmd.ExecuteReader();

            var columnDefs = new List<string>();
            while (reader.Read())
            {
                var colName = reader["name"]?.ToString() ?? "";
                var dataType = reader["data_type"]?.ToString() ?? "";
                var maxLen = Convert.ToInt32(reader["max_length"]);
                var precision = Convert.ToInt32(reader["precision"]);
                var scale = Convert.ToInt32(reader["scale"]);
                var isNullable = Convert.ToBoolean(reader["is_nullable"]);
                var isPk = Convert.ToInt32(reader["is_primary_key"]) == 1;
                var defaultValue = reader["default_value"]?.ToString();

                var colDef = "[" + colName + "] " + GetSqlDataType(dataType, maxLen, precision, scale);
                if (!isNullable) colDef += " NOT NULL";
                else colDef += " NULL";
                if (!string.IsNullOrEmpty(defaultValue)) colDef += " DEFAULT " + defaultValue;
                if (isPk) colDef += " PRIMARY KEY";
                columnDefs.Add(colDef);
            }
            reader.Close();

            if (columnDefs.Count == 0) return scripts;

            scripts.Add("IF OBJECT_ID('" + tableName + "', 'U') IS NOT NULL DROP TABLE [" + tableName + "]");
            scripts.Add("CREATE TABLE [" + tableName + "] (" + string.Join(", ", columnDefs) + ")");
            return scripts;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成建表脚本失败: " + ex.Message);
            return scripts;
        }
    }

    private string GetSqlDataType(string dataType, int maxLen, int precision, int scale)
    {
        return dataType.ToLower() switch
        {
            "varchar" => maxLen == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(" + (maxLen / 2) + ")",
            "nvarchar" => maxLen == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(" + (maxLen / 2) + ")",
            "char" => "CHAR(" + maxLen + ")",
            "nchar" => "NCHAR(" + (maxLen / 2) + ")",
            "decimal" => "DECIMAL(" + precision + ", " + scale + ")",
            "numeric" => "NUMERIC(" + precision + ", " + scale + ")",
            _ => dataType
        };
    }
}