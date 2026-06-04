using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;
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
        // 快捷键：键定位搜索框，上/下键快速进入列表选择，ESC关闭，Enter确认
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
        if (string.IsNullOrWhiteSpace(txtObjectGuids.Text))
        {
            MessageBox.Show("请输入要复制的表单OBJECTGUID！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        lblProgress.Text = "正在复制表单...";
        progressBar.Value = 50;

        var success = await CopyFormsAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            txtObjectGuids.Text.Trim(), chkDeleteFirst.Checked, chkCopyStoredProcs.Checked);

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

    private async Task<bool> CopyFormsAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        string objectGuids, bool deleteFirst, bool copyStoredProcs)
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

                    // 复制样式表
                    CopyTableData(srcConn, tgtConn, "S_OBJECTSTYLE", "OBJECTGUID", objectGuid, deleteFirst);

                    // 复制编码规则（S_CONTROL中DATANAME=CODE/BILLNO的EXTENDS）
                    CopyCodeRulesForObject(srcConn, tgtConn, objectGuid);

                    // 复制标准查询（S_CONTROL中CONTROLTYPE=A3Text/GridColumn的EXTENDS）
                    CopyStandardQueriesForObject(srcConn, tgtConn, objectGuid);

                    // 复制关联存储过程（仅当勾选时）
                    if (copyStoredProcs)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblProgress.Text = "正在复制存储过程：" + objectGuid + " (" + current + "/" + total + ")";
                        }));
                        CopyStoredProcsForObject(srcConn, tgtConn, objectGuid, deleteFirst);
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

    /// <summary>
    /// 获取S_OBJECT记录中的三个存储过程名称
    /// </summary>
    private List<(string FieldName, string ProcName)> GetStoredProcNames(SqlConnection srcConn, string objectGuid)
    {
        var result = new List<(string, string)>();
        var sql = @"SELECT AUDITINGPROCNAME, DELETEPROCNAME, UNAUDITINGPROCNAME
                     FROM dbo.S_OBJECT WHERE GUID = @guid";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@guid", objectGuid);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.IsDBNull(i) ? null : reader.GetString(i);
                if (!string.IsNullOrWhiteSpace(val))
                    result.Add((reader.GetName(i), val));
            }
        }
        reader.Close();

        // 调试输出
        foreach (var (fn, pn) in result)
        {
            Debug.WriteLine($"[GetStoredProcNames] {fn} -> {pn}");
        }

        return result;
    }

    /// <summary>
    /// 检查目标库中存储过程是否存在
    /// </summary>
    private bool ProcExistsInTarget(SqlConnection tgtConn, string procName)
    {
        var sql = @"SELECT COUNT(*) FROM sys.objects
                    WHERE type = 'P' AND name = @procName AND is_ms_shipped = 0";
        using var cmd = new SqlCommand(sql, tgtConn);
        cmd.Parameters.AddWithValue("@procName", procName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 获取存储过程的完整定义文本
    /// </summary>
    private string GetProcDefinition(SqlConnection srcConn, string procName)
    {
        // 先尝试直接查询 sys.sql_modules（更可靠，支持含特殊字符的名称）
        var sql = @"SELECT definition FROM sys.sql_modules
                    WHERE object_id = OBJECT_ID(@procName, 'P')";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@procName", procName);
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
        {
            // 兼容旧方式（加密的存储过程OBJECT_DEFINITION也可能返回null）
            var sql2 = @"SELECT OBJECT_DEFINITION(OBJECT_ID(@procName, 'P'))";
            using var cmd2 = new SqlCommand(sql2, srcConn);
            cmd2.Parameters.AddWithValue("@procName", procName);
            result = cmd2.ExecuteScalar();
        }
        var text = result as string ?? "";
        Debug.WriteLine($"[GetProcDefinition] procName={procName}, definition length={text.Length}");
        return text;
    }

    /// <summary>
    /// 在目标库创建存储过程
    /// </summary>
    private void CreateProcInTarget(SqlConnection tgtConn, string procName, string definition)
    {
        Debug.WriteLine($"[CreateProcInTarget] procName={procName}, definition length={definition?.Length ?? -1}");

        // 先删除已存在的同名存储过程
        if (ProcExistsInTarget(tgtConn, procName))
        {
            using var dropCmd = new SqlCommand("DROP PROCEDURE [" + procName + "]", tgtConn);
            dropCmd.ExecuteNonQuery();
        }

        // 创建存储过程（移除原库的USE语句和创建语句头部）
        var createSql = NormalizeProcDefinition(definition, procName);
        Debug.WriteLine($"[CreateProcInTarget] createSql length={createSql.Length}");
        Debug.WriteLine($"[CreateProcInTarget] createSql preview: {createSql.Substring(0, Math.Min(200, createSql.Length))}");
        using var createCmd = new SqlCommand(createSql, tgtConn);
        createCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 清理存储过程定义：去掉头部的USE和SET语句，保留CREATE PROCEDURE行（含参数）
    /// 替换为ALTER PROCEDURE以适配目标库
    /// </summary>
    private string NormalizeProcDefinition(string definition, string procName)
    {
        if (string.IsNullOrWhiteSpace(definition)) return "";

        var lines = definition.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var sb = new System.Text.StringBuilder();
        bool headerDone = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (!headerDone)
            {
                // 跳过 USE 语句
                if (trimmed.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
                    continue;
                // 跳过 SET 语句（ANSI_NULLS / QUOTED_IDENTIFIER）
                if (trimmed.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 找到 CREATE/ALTER PROCEDURE 行（含参数签名），替换为 ALTER
                if (trimmed.StartsWith("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase))
                {
                    // 替换 CREATE PROCEDURE 为 ALTER PROCEDURE，保留后面的参数和AS
                    var normalizedLine = System.Text.RegularExpressions.Regex.Replace(
                        trimmed, @"^\s*(CREATE|ALTER)\s+PROCEDURE\s+",
                        "ALTER PROCEDURE ",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    sb.AppendLine(normalizedLine);
                    headerDone = true;
                    continue;
                }
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 为单个表单复制其关联的三个存储过程（如果存在且目标库没有）
    /// </summary>
    private void CopyStoredProcsForObject(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid, bool deleteFirst)
    {
        var procNames = GetStoredProcNames(srcConn, objectGuid);
        foreach (var (fieldName, procName) in procNames)
        {
            try
            {
                if (!ProcExistsInTarget(tgtConn, procName))
                {
                    var definition = GetProcDefinition(srcConn, procName);
                    if (string.IsNullOrWhiteSpace(definition))
                    {
                        Debug.WriteLine($"[存储过程] {procName} 无法获取定义（可能加密或不存在）");
                        this.Invoke(new Action(() =>
                        {
                            lblProgress.Text = $"⚠ {procName} 无法获取定义（加密或不存在）";
                            lblProgress.ForeColor = Color.Orange;
                        }));
                        continue;
                    }
                    CreateProcInTarget(tgtConn, procName, definition);
                    Debug.WriteLine($"[存储过程] {procName} 复制成功（字段：{fieldName}）");
                    this.Invoke(new Action(() =>
                    {
                        lblProgress.Text = $"✓ {procName} 复制成功";
                        lblProgress.ForeColor = Color.Green;
                    }));
                }
                else
                {
                    Debug.WriteLine($"[存储过程] {procName} 目标库已存在，跳过");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[存储过程] {procName} 复制失败：{ex.Message}");
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = $"✗ {procName} 复制失败：{ex.Message}";
                    lblProgress.ForeColor = Color.Red;
                }));
            }
        }
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

            // 从源数据库读取数据到DataTable
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

            // 使用SqlBulkCopy批量写入，自动处理所有列类型
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

    // ==================== 编码规则 & 标准查询复制 ====================

    /// <summary>
    /// 解析EXTENDS字段，用'|!'分割多项，'|@'分割KEY和VALUE
    /// </summary>
    public static Dictionary<string, string> ParseExtendsField(string? extends)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(extends)) return dict;

        var items = extends.Split(new[] { "|!" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var kv = item.Split(new[] { "|@" }, 2, StringSplitOptions.None);
            if (kv.Length == 2)
                dict[kv[0].Trim()] = kv[1].Trim();
        }
        return dict;
    }

    /// <summary>
    /// 复制编码规则：根据S_CONTROL中DATANAME=CODE/BILLNO的EXTENDS找到CodeRuleGuid，
    /// 若目标库不存在对应规则则从源库复制S_BILLCODERULE和S_BILLCODERULEDETAIL
    /// </summary>
    private void CopyCodeRulesForObject(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid)
    {
        try
        {
            // 查找S_CONTROL中DATANAME为CODE或BILLNO的记录，取EXTENDS字段
            var sql = @"SELECT EXTENDS FROM dbo.S_CONTROL
                        WHERE OBJECTGUID = @guid AND (DATANAME = 'CODE' OR DATANAME = 'BILLNO')";
            using var cmd = new SqlCommand(sql, srcConn);
            cmd.Parameters.AddWithValue("@guid", objectGuid);
            using var reader = cmd.ExecuteReader();
            var codeRuleGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var extends = reader.GetString(0);
                    var dict = ParseExtendsField(extends);
                    if (dict.TryGetValue("CodeRuleGuid", out var ruleGuid) &&
                        !string.IsNullOrWhiteSpace(ruleGuid))
                    {
                        codeRuleGuids.Add(ruleGuid);
                    }
                }
            }
            reader.Close();

            foreach (var ruleGuid in codeRuleGuids)
            {
                CopyOneCodeRule(srcConn, tgtConn, ruleGuid);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[编码规则] 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 复制单条编码规则（S_BILLCODERULE + S_BILLCODERULEDETAIL）
    /// </summary>
    private void CopyOneCodeRule(SqlConnection srcConn, SqlConnection tgtConn, string ruleCode)
    {
        try
        {
            // 检查目标库是否已存在
            if (CodeRuleExistsInTarget(tgtConn, ruleCode))
            {
                Debug.WriteLine($"[编码规则] {ruleCode} 目标库已存在，跳过");
                return;
            }

            // 从源库读取编码规则主表
            var rule = GetCodeRuleFromSource(srcConn, ruleCode);
            if (rule == null)
            {
                Debug.WriteLine($"[编码规则] {ruleCode} 在源库中未找到");
                return;
            }

            // 复制主表
            CopyTableDataByGuid(srcConn, tgtConn, "S_BILLCODERULE", "GUID", rule.Item1, false);

            // 复制明细表
            CopyTableDataByGuid(srcConn, tgtConn, "S_BILLCODERULEDETAIL", "BILLCODERULEGUID", rule.Item1, false);

            Debug.WriteLine($"[编码规则] {ruleCode} 复制成功");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[编码规则] {ruleCode} 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 检查目标库中编码规则是否存在
    /// </summary>
    private bool CodeRuleExistsInTarget(SqlConnection tgtConn, string code)
    {
        var sql = @"SELECT COUNT(*) FROM dbo.S_BILLCODERULE WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, tgtConn);
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 从源库获取编码规则的GUID
    /// </summary>
    private Tuple<string, System.Data.DataTable>? GetCodeRuleFromSource(SqlConnection srcConn, string code)
    {
        var sql = "SELECT * FROM dbo.S_BILLCODERULE WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@code", code);
        var dt = new System.Data.DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(dt);
        if (dt.Rows.Count == 0) return null;
        var guid = dt.Rows[0]["GUID"].ToString()!;
        return Tuple.Create(guid, dt);
    }

    /// <summary>
    /// 根据GUID复制整张表数据（用于规则、明细表等无主键或按GUID定位的表）
    /// </summary>
    private void CopyTableDataByGuid(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string guidField, string guidValue, bool deleteFirst)
    {
        try
        {
            var tgtColumns = GetTableColumns(tgtConn, tableName);
            if (tgtColumns.Count == 0) return;

            if (deleteFirst)
            {
                var deleteSql = $"DELETE FROM dbo.[{tableName}] WHERE [{guidField}] = @guid";
                using var delCmd = new SqlCommand(deleteSql, tgtConn);
                delCmd.Parameters.AddWithValue("@guid", guidValue);
                delCmd.ExecuteNonQuery();
            }
            else
            {
                var checkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{guidField}] = @guid";
                using var chkCmd = new SqlCommand(checkSql, tgtConn);
                chkCmd.Parameters.AddWithValue("@guid", guidValue);
                if (Convert.ToInt32(chkCmd.ExecuteScalar()) > 0) return;
            }

            var cols = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
            var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{guidField}] = @guid";
            using var selCmd = new SqlCommand(selSql, srcConn);
            selCmd.Parameters.AddWithValue("@guid", guidValue);
            var dt = new System.Data.DataTable();
            using var adapter = new SqlDataAdapter(selCmd);
            adapter.Fill(dt);
            if (dt.Rows.Count == 0) return;

            using var bulk = new SqlBulkCopy(tgtConn);
            bulk.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in tgtColumns)
                bulk.ColumnMappings.Add(col, col);
            bulk.WriteToServer(dt);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[编码规则] 复制表{tableName}失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 复制标准查询：根据S_CONTROL中CONTROLTYPE=A3Text/GridColumn的EXTENDS找到DataSelectCode，
    /// 若目标库不存在对应标准查询则从源库复制S_DATASELECT
    /// </summary>
    private void CopyStandardQueriesForObject(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid)
    {
        try
        {
            // 查找S_CONTROL中CONTROLTYPE为A3Text或GridColumn的记录
            var sql = @"SELECT EXTENDS FROM dbo.S_CONTROL
                        WHERE OBJECTGUID = @guid AND (CONTROLTYPE = 'A3Text' OR CONTROLTYPE = 'GridColumn')";
            using var cmd = new SqlCommand(sql, srcConn);
            cmd.Parameters.AddWithValue("@guid", objectGuid);
            using var reader = cmd.ExecuteReader();
            var dataSelectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var extends = reader.GetString(0);
                    var dict = ParseExtendsField(extends);
                    if (dict.TryGetValue("DataSelectCode", out var dataSelectCode) &&
                        !string.IsNullOrWhiteSpace(dataSelectCode))
                    {
                        dataSelectCodes.Add(dataSelectCode);
                    }
                }
            }
            reader.Close();

            foreach (var code in dataSelectCodes)
            {
                CopyOneStandardQuery(srcConn, tgtConn, code);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[标准查询] 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 复制单条标准查询（S_DATASELECT）
    /// </summary>
    private void CopyOneStandardQuery(SqlConnection srcConn, SqlConnection tgtConn, string code)
    {
        try
        {
            if (StandardQueryExistsInTarget(tgtConn, code))
            {
                Debug.WriteLine($"[标准查询] {code} 目标库已存在，跳过");
                return;
            }

            // 从源库读取标准查询
            var dt = GetStandardQueryFromSource(srcConn, code);
            if (dt == null || dt.Rows.Count == 0)
            {
                Debug.WriteLine($"[标准查询] {code} 在源库中未找到");
                return;
            }

            // 复制数据
            CopyTableDataByCode(srcConn, tgtConn, "S_DATASELECT", "CODE", code);

            Debug.WriteLine($"[标准查询] {code} 复制成功");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[标准查询] {code} 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 检查目标库中标准查询是否存在
    /// </summary>
    private bool StandardQueryExistsInTarget(SqlConnection tgtConn, string code)
    {
        var sql = @"SELECT COUNT(*) FROM dbo.S_DATASELECT WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, tgtConn);
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 从源库获取标准查询数据
    /// </summary>
    private System.Data.DataTable? GetStandardQueryFromSource(SqlConnection srcConn, string code)
    {
        var sql = "SELECT * FROM dbo.S_DATASELECT WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@code", code);
        var dt = new System.Data.DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(dt);
        return dt.Rows.Count > 0 ? dt : null;
    }

    /// <summary>
    /// 根据CODE字段复制表数据
    /// </summary>
    private void CopyTableDataByCode(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string codeField, string codeValue)
    {
        try
        {
            var tgtColumns = GetTableColumns(tgtConn, tableName);
            if (tgtColumns.Count == 0) return;

            var checkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{codeField}] = @code";
            using var chkCmd = new SqlCommand(checkSql, tgtConn);
            chkCmd.Parameters.AddWithValue("@code", codeValue);
            if (Convert.ToInt32(chkCmd.ExecuteScalar()) > 0) return;

            var cols = string.Join(", ", tgtColumns.Select(c => "[" + c + "]"));
            var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{codeField}] = @code";
            using var selCmd = new SqlCommand(selSql, srcConn);
            selCmd.Parameters.AddWithValue("@code", codeValue);
            var dt = new System.Data.DataTable();
            using var adapter = new SqlDataAdapter(selCmd);
            adapter.Fill(dt);
            if (dt.Rows.Count == 0) return;

            using var bulk = new SqlBulkCopy(tgtConn);
            bulk.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in tgtColumns)
                bulk.ColumnMappings.Add(col, col);
            bulk.WriteToServer(dt);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[标准查询] 复制表{tableName}失败：" + ex.Message);
        }
    }
}