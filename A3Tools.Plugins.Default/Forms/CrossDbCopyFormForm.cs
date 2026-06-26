using System.Data;
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

    // 用于存储搜索到的表单数据
    private DataTable? _searchResults;

    public CrossDbCopyFormForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        LoadPresetAccounts();
        FormHotkeyHelper.Setup(this, () => BtnConfirm_Click(this, EventArgs.Empty));
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control) { BtnSelectSource_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D && e.Modifiers == Keys.Control) { BtnSelectTarget_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };

        // 数据网格视图支持多选
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.MultiSelect = true;

        // 选中状态变化时，同步checkbox勾选状态
        dgvSearchResults.SelectionChanged += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk")) return;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null)
                {
                    checkCell.Value = row.Selected;
                }
            }
        };

        // 点击表头处理：点击checkbox列全选/取消全选
        dgvSearchResults.ColumnHeaderMouseClick += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk") || e.ColumnIndex != 0) return;
            var allChecked = true;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell == null || checkCell.Value == null || !(bool)checkCell.Value)
                {
                    allChecked = false;
                    break;
                }
            }
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null)
                {
                    checkCell.Value = !allChecked;
                    row.Selected = !allChecked;
                }
            }
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

    /// <summary>
    /// 根据主窗体工具箱 Tab 中的源/目标预选账套自动带入连接信息。
    /// 预选为空时，源库回退到原选中账套（保留原行为），目标库保持空。
    /// 带入后用户仍可在工具内自行修改或重新选择。
    /// </summary>
    private void LoadPresetAccounts()
    {
        var preset = _context.GetToolDatabasePreset();
        ApplyAccountToDatabaseFields(preset.SourceAccount ?? _currentAccount, true);
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
        dialog.KeyDown += (s, e) =>
        {
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

    private void BtnSearch_Click(object? sender, EventArgs e)
    {
        // 验证源数据库连接信息
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text))
        {
            MessageBox.Show("请填写源数据库地址！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtSourceDbName.Text))
        {
            MessageBox.Show("请填写源数据库名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var keyword = txtSearchKeyword.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show("请输入搜索关键字！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSearchKeyword.Focus();
            return;
        }

        lblSearchProgress.Text = "查询中...";
        lblSearchProgress.ForeColor = Color.Blue;
        dgvSearchResults.DataSource = null;
        btnSearch.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var server = txtSourceServer.Text.Trim();
                var dbName = txtSourceDbName.Text.Trim();
                var user = txtSourceUser.Text.Trim();
                var password = txtSourcePassword.Text;

                var connString = string.IsNullOrEmpty(user)
                    ? $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={server};Database={dbName};User Id={user};Password={EncryptionService.Decrypt(password)};TrustServerCertificate=True;";

                var sql = $@"
SELECT A.GUID AS OBJECTGUID,
       A.CODE AS 代码,
       A.NAME AS 名称,
       F.CODE AS 解决方案代码,
       F.NAME AS 解决方案,
       B.NAME AS 业务分组
FROM S_OBJECT A
LEFT JOIN S_SUBSYSTEM B ON A.SUBSYSTEMGUID = B.GUID
LEFT JOIN S_BUSINESSTYPE F ON B.BUSINESSTYPEGUID = F.GUID
WHERE A.NAME LIKE '%{keyword}%' OR A.CODE LIKE '%{keyword}%'
ORDER BY A.NAME";

                using var conn = new SqlConnection(connString);
                using var cmd = new SqlCommand(sql, conn);
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                this.Invoke(new Action(() =>
                {
                    _searchResults = dt;
                    // 先移除旧的选择列（如果存在）
                    if (dgvSearchResults.Columns.Contains("chk"))
                    {
                        dgvSearchResults.Columns.Remove("chk");
                    }
                    // 先设置数据源
                    dgvSearchResults.DataSource = dt;
                    // 再插入checkbox列作为第一列
                    var checkCol = new DataGridViewCheckBoxColumn();
                    checkCol.HeaderText = "选择";
                    checkCol.Width = 50;
                    checkCol.Name = "chk";
                    dgvSearchResults.Columns.Insert(0, checkCol);
                    dgvSearchResults.AutoResizeColumns();
                    // 隐藏代码列
                    if (dgvSearchResults.Columns.Contains("代码"))
                    {
                        dgvSearchResults.Columns["代码"].Visible = false;
                    }
                    // 默认选中第一行并同步checkbox
                    if (dgvSearchResults.Rows.Count > 0)
                    {
                        dgvSearchResults.Rows[0].Selected = true;
                    }
                    // 同步所有选中行的checkbox状态
                    foreach (DataGridViewRow row in dgvSearchResults.Rows)
                    {
                        var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                        if (checkCell != null) checkCell.Value = row.Selected;
                    }
                    // 将状态信息移到DataGridView下方
                    lblSearchProgress.Location = new Point(dgvSearchResults.Left, dgvSearchResults.Bottom + 5);
                    lblSearchProgress.Text = $"查询完成，共 {dt.Rows.Count} 条记录";
                    lblSearchProgress.ForeColor = Color.Green;
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblSearchProgress.Text = "查询失败";
                    lblSearchProgress.ForeColor = Color.Red;
                    MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                this.Invoke(new Action(() =>
                {
                    btnSearch.Enabled = true;
                }));
            }
        });
    }

    private void BtnAddSelected_Click(object? sender, EventArgs e)
    {
        if (dgvSearchResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先选择要添加的表单！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedGuids = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            var guid = row.Cells["OBJECTGUID"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                selectedGuids.Add(guid);
            }
        }

        if (selectedGuids.Count == 0) return;

        // 追加到现有内容
        var currentText = txtObjectGuids.Text.Trim();
        var separator = string.IsNullOrEmpty(currentText) ? "" : ";";

        var existingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
        {
            currentText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                .ForEach(g => existingGuids.Add(g.Trim()));
        }

        var newGuids = selectedGuids.Where(g => !existingGuids.Contains(g)).ToList();
        if (newGuids.Count == 0)
        {
            MessageBox.Show("选中的表单已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var addedText = string.Join(";", newGuids);
        txtObjectGuids.Text = currentText + separator + addedText;

        lblSearchProgress.Text = $"已添加 {newGuids.Count} 个表单到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        txtObjectGuids.Clear();
        dgvSearchResults.ClearSelection();
        lblSearchProgress.Text = "已清空选项";
        lblSearchProgress.ForeColor = Color.Gray;
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
            // 不自动关闭，方便继续操作
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
                System.Diagnostics.Debug.WriteLine("连接测试失败: " + ex.Message);
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
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_OBJECT", "GUID", objectGuid, deleteFirst, "[Win表单]");

                    // 复制S_CONTROL表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_CONTROL", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

                    // 复制S_DATA表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_DATA", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

                    // 复制样式表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_OBJECTSTYLE", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

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
            System.Diagnostics.Debug.WriteLine($"[GetStoredProcNames] {fn} -> {pn}");
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
        System.Diagnostics.Debug.WriteLine($"[GetProcDefinition] procName={procName}, definition length={text.Length}");
        return text;
    }

    /// <summary>
    /// 在目标库创建存储过程
    /// </summary>
    private void CreateProcInTarget(SqlConnection tgtConn, string procName, string definition)
    {
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTarget] procName={procName}, definition length={definition?.Length ?? -1}");

        // 先删除已存在的同名存储过程
        if (ProcExistsInTarget(tgtConn, procName))
        {
            using var dropCmd = new SqlCommand("DROP PROCEDURE [" + procName + "]", tgtConn);
            dropCmd.ExecuteNonQuery();
        }

        // 创建存储过程（移除原库的USE语句和创建语句头部）
        var createSql = NormalizeProcDefinition(definition, procName);
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTarget] createSql length={createSql.Length}");
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTarget] createSql preview: {createSql.Substring(0, Math.Min(200, createSql.Length))}");
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
                        System.Diagnostics.Debug.WriteLine($"[存储过程] {procName} 无法获取定义（可能加密或不存在）");
                        this.Invoke(new Action(() =>
                        {
                            lblProgress.Text = $"⚠ {procName} 无法获取定义（加密或不存在）";
                            lblProgress.ForeColor = Color.Orange;
                        }));
                        continue;
                    }
                    CreateProcInTarget(tgtConn, procName, definition);
                    System.Diagnostics.Debug.WriteLine($"[存储过程] {procName} 复制成功（字段：{fieldName}）");
                    this.Invoke(new Action(() =>
                    {
                        lblProgress.Text = $"✓ {procName} 复制成功";
                        lblProgress.ForeColor = Color.Green;
                    }));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[存储过程] {procName} 目标库已存在，跳过");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[存储过程] {procName} 复制失败：{ex.Message}");
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = $"✗ {procName} 复制失败：{ex.Message}";
                    lblProgress.ForeColor = Color.Red;
                }));
            }
        }
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
            System.Diagnostics.Debug.WriteLine($"[编码规则] 复制失败：" + ex.Message);
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
                System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 目标库已存在，跳过");
                return;
            }

            // 从源库读取编码规则主表
            var rule = GetCodeRuleFromSource(srcConn, ruleCode);
            if (rule == null)
            {
                System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 在源库中未找到");
                return;
            }

            // 复制主表
            TableCopyService.CopyTableData(srcConn, tgtConn, "S_BILLCODERULE", "GUID", rule.Item1, false, "[编码规则]");

            // 复制明细表
            TableCopyService.CopyTableData(srcConn, tgtConn, "S_BILLCODERULEDETAIL", "BILLCODERULEGUID", rule.Item1, false, "[编码规则]");

            System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 复制失败：" + ex.Message);
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
    private Tuple<string, DataTable>? GetCodeRuleFromSource(SqlConnection srcConn, string code)
    {
        var sql = "SELECT * FROM dbo.S_BILLCODERULE WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@code", code);
        var dt = new DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(dt);
        if (dt.Rows.Count == 0) return null;
        var guid = dt.Rows[0]["GUID"].ToString()!;
        return Tuple.Create(guid, dt);
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
            System.Diagnostics.Debug.WriteLine($"[标准查询] 复制失败：" + ex.Message);
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
                System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 目标库已存在，跳过");
                return;
            }

            // 从源库读取标准查询
            var dt = GetStandardQueryFromSource(srcConn, code);
            if (dt == null || dt.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 在源库中未找到");
                return;
            }

            // 复制数据
            TableCopyService.CopyTableData(srcConn, tgtConn, "S_DATASELECT", "CODE", code, false, "[标准查询]");

            System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 复制失败：" + ex.Message);
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
    private DataTable? GetStandardQueryFromSource(SqlConnection srcConn, string code)
    {
        var sql = "SELECT * FROM dbo.S_DATASELECT WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, srcConn);
        cmd.Parameters.AddWithValue("@code", code);
        var dt = new DataTable();
        using var adapter = new SqlDataAdapter(cmd);
        adapter.Fill(dt);
        return dt.Rows.Count > 0 ? dt : null;
    }

}