using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyTableForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    // 对象类型常量
    private static readonly Dictionary<string, string> ObjectTypeMap = new()
    {
        { "U",   "表结构" },
        { "V",   "视图" },
        { "TF",  "表值函数" },
        { "FN",  "标量值函数" },
        { "P",   "存储过程" }
    };

    public CrossDbCopyTableForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        InitObjectTypeCombo();
    }

    private void InitObjectTypeCombo()
    {
        cboObjectType.Items.Clear();
        foreach (var kv in ObjectTypeMap)
        {
            cboObjectType.Items.Add(new ObjectTypeItem { Value = kv.Key, Display = kv.Value });
        }
        cboObjectType.DisplayMember = "Display";
        cboObjectType.ValueMember = "Value";
        cboObjectType.SelectedIndex = 0;
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
            txtSourceDbName.Text = _currentAccount.DatabaseName ?? "";
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
                {
                    listBox.Items.Add(item);
                }
            }
        }

        PopulateList("");
        txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);

        var btnOk = new Button { Text = "确定", Left = 170, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };
        var btnCancelDialog = new Button { Text = "取消", Left = 310, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };

        btnOk.Click += (s, e) =>
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
        if (string.IsNullOrWhiteSpace(txtObjects.Text))
        {
            MessageBox.Show("请输入对象名称！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请选择对象类型！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var objectNames = txtObjects.Text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();

        var objectType = ((ObjectTypeItem)cboObjectType.SelectedItem).Value;
        var deleteIfExists = chkDeleteIfExists.Checked;

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

        lblProgress.Text = "正在复制对象...";
        progressBar.Value = 50;

        var success = await CopyObjectsAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            objectNames, objectType, deleteIfExists);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            MessageBox.Show("成功复制 " + objectNames.Count + " 个对象！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
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

    private async Task<bool> CopyObjectsAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        List<string> objectNames, string objectType, bool deleteIfExists)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";Database=" + srcDbName + ";User Id=" + srcUser + ";Password=" + EncryptionService.Decrypt(srcPassword) + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";Database=" + tgtDbName + ";User Id=" + tgtUser + ";Password=" + EncryptionService.Decrypt(tgtPassword) + ";TrustServerCertificate=True;";

                using var srcConn = new Microsoft.Data.SqlClient.SqlConnection(srcConnStr);
                using var tgtConn = new Microsoft.Data.SqlClient.SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                int total = objectNames.Count;
                int current = 0;

                foreach (var objectName in objectNames)
                {
                    current++;
                    var progress = 50 + (current * 50 / total);
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progress;
                        lblProgress.Text = "正在复制：" + objectName + " (" + current + "/" + total + ")";
                    }));

                    string? script = objectType switch
                    {
                        "U" => GenerateCreateTableScript(srcConn, objectName),
                        "V" => GenerateCreateViewScript(srcConn, objectName),
                        "TF" => GenerateCreateFunctionScript(srcConn, objectName),
                        "FN" => GenerateCreateFunctionScript(srcConn, objectName),
                        "P" => GenerateCreateProcScript(srcConn, objectName),
                        _ => null
                    };

                    if (string.IsNullOrEmpty(script))
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show("无法获取 " + objectName + " 的定义（对象不存在或无权访问）。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                        continue;
                    }

                    // 删除目标已存在对象
                    if (deleteIfExists)
                    {
                        var dropScript = GetDropScript(objectName, objectType);
                        using var dropCmd = new Microsoft.Data.SqlClient.SqlCommand(dropScript, tgtConn);
                        dropCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        // 检查是否存在
                        if (ObjectExists(tgtConn, objectName, objectType))
                        {
                            Debug.WriteLine($"跳过已存在的对象: {objectName}");
                            continue;
                        }
                    }

                    // 创建目标对象
                    using var createCmd = new Microsoft.Data.SqlClient.SqlCommand(script, tgtConn);
                    createCmd.ExecuteNonQuery();
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

    private bool ObjectExists(Microsoft.Data.SqlClient.SqlConnection conn, string objectName, string type)
    {
        var sql = "SELECT 1 FROM sys.objects WHERE name = @name AND type = @type";
        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", objectName);
        cmd.Parameters.AddWithValue("@type", type);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    private string GetDropScript(string objectName, string type)
    {
        return type switch
        {
            "U" => $"IF OBJECT_ID('{objectName}', 'U') IS NOT NULL DROP TABLE [{objectName}]",
            "V" => $"IF OBJECT_ID('{objectName}', 'V') IS NOT NULL DROP VIEW [{objectName}]",
            "TF" => $"IF OBJECT_ID('{objectName}', 'TF') IS NOT NULL DROP FUNCTION [{objectName}]",
            "FN" => $"IF OBJECT_ID('{objectName}', 'FN') IS NOT NULL DROP FUNCTION [{objectName}]",
            "P" => $"IF OBJECT_ID('{objectName}', 'P') IS NOT NULL DROP PROCEDURE [{objectName}]",
            _ => $"IF OBJECT_ID('{objectName}', 'U') IS NOT NULL DROP TABLE [{objectName}]"
        };
    }

    // ========== 表结构 ==========
    private string? GenerateCreateTableScript(Microsoft.Data.SqlClient.SqlConnection conn, string tableName)
    {
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

            if (columnDefs.Count == 0) return null;

            return "CREATE TABLE [" + tableName + "] (" + string.Join(", ", columnDefs) + ")";
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成建表脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 视图 ==========
    private string? GenerateCreateViewScript(Microsoft.Data.SqlClient.SqlConnection conn, string viewName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", viewName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成视图脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 函数（表值/标量值） ==========
    private string? GenerateCreateFunctionScript(Microsoft.Data.SqlClient.SqlConnection conn, string funcName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", funcName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成函数脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 存储过程 ==========
    private string? GenerateCreateProcScript(Microsoft.Data.SqlClient.SqlConnection conn, string procName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", procName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成存储过程脚本失败: " + ex.Message);
            return null;
        }
    }

    private string GetSqlDataType(string dataType, int maxLen, int precision, int scale)
    {
        return dataType.ToLower() switch
        {
            "varchar" => maxLen == -1 ? "VARCHAR(MAX)" : "VARCHAR(" + (maxLen) + ")",
            "nvarchar" => maxLen == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(" + (maxLen) + ")",
            "char" => "CHAR(" + maxLen + ")",
            "nchar" => "NCHAR(" + (maxLen) + ")",
            "decimal" => "DECIMAL(" + precision + ", " + scale + ")",
            "numeric" => "NUMERIC(" + precision + ", " + scale + ")",
            _ => dataType
        };
    }

    private class ObjectTypeItem
    {
        public string Value { get; set; } = "";
        public string Display { get; set; } = "";
    }
}