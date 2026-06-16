using System.Data;
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

public partial class CrossDbCopyAppFormForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;
    private System.Data.DataTable? _searchResults;

    public CrossDbCopyAppFormForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
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

        PopulateList("");
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
            MessageBox.Show("请输入要复制的APP表单OBJECTGUID！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        lblProgress.Text = "正在复制APP表单...";
        progressBar.Value = 50;

        var success = await CopyAppFormsAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            txtObjectGuids.Text.Trim(), chkDeleteFirst.Checked);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            MessageBox.Show("APP表单复制完成！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Debug.WriteLine("连接测试失败: " + ex.Message);
                return false;
            }
        });
    }

    private async Task<bool> CopyAppFormsAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        string objectGuids, bool deleteFirst)
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

                // 解析OBJECTGUID列表（支持多个，用逗号或分号分隔）
                var guidList = objectGuids.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
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

                    // 复制S_APP_OBJECT表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_APP_OBJECT", "GUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制S_APP_DATA表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_APP_DATA", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制S_APP_CONTROL表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_APP_CONTROL", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制S_APP_FILTER表
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_APP_FILTER", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制S_OBJECTBAR表   扫码定义
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_OBJECTBAR", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制S_APP_OBJECT_BACKGROUD表 颜色设置
                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_APP_OBJECT_BACKGROUD", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                    // 复制关联的编码规则和标准查询
                    CopyAppFormCodeRules(srcConn, tgtConn, objectGuid);
                    CopyAppFormStandardQueries(srcConn, tgtConn, objectGuid);

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
SELECT GUID AS OBJECTGUID,
       CODE AS 代码,
       NAME AS APP表单名称,
       DESCRIPTION AS 备注
FROM S_APP_OBJECT
WHERE NAME LIKE '%{keyword}%' OR CODE LIKE '%{keyword}%'
ORDER BY NAME";

                using var conn = new SqlConnection(connString);
                using var cmd = new SqlCommand(sql, conn);
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new System.Data.DataTable();
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
            MessageBox.Show("请先选择要添加的APP表单！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("选中的APP表单已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var addedText = string.Join(";", newGuids);
        txtObjectGuids.Text = currentText + separator + addedText;

        lblSearchProgress.Text = $"已添加 {newGuids.Count} 个APP表单到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        // 清空ObjectGuids
        txtObjectGuids.Text = "";
        // 清空选中状态
        dgvSearchResults.ClearSelection();
        // 同步checkbox
        if (dgvSearchResults.Columns.Contains("chk"))
        {
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null) checkCell.Value = false;
            }
        }
        lblSearchProgress.Text = "已清空选项";
        lblSearchProgress.ForeColor = Color.Gray;
    }

    // ==================== APP表单编码规则 & 标准查询复制 ====================

    /// <summary>
    /// 复制APP编码规则：从S_APP_CONTROL中DATANAME=BILLNO/CODE的行取DEFAULTVALUE作为编码规则CODE，
    /// 若目标库不存在对应规则则从源库复制S_BILLCODERULE和S_BILLCODERULEDETAIL
    /// </summary>
    private void CopyAppFormCodeRules(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid)
    {
        try
        {
            var sql = @"SELECT DEFAULTVALUE FROM dbo.S_APP_CONTROL
                        WHERE OBJECTGUID = @guid AND (DATANAME = 'BILLNO' OR DATANAME = 'CODE')";
            using var cmd = new SqlCommand(sql, srcConn);
            cmd.Parameters.AddWithValue("@guid", objectGuid);
            using var reader = cmd.ExecuteReader();
            var codeRuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var defaultValue = reader.GetString(0)?.Trim();
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                    {
                        codeRuleCodes.Add(defaultValue);
                    }
                }
            }
            reader.Close();

            foreach (var ruleCode in codeRuleCodes)
            {
                CopyOneAppFormCodeRule(srcConn, tgtConn, ruleCode);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[APP表单编码规则] 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 复制单条编码规则（S_BILLCODERULE + S_BILLCODERULEDETAIL）
    /// </summary>
    private void CopyOneAppFormCodeRule(SqlConnection srcConn, SqlConnection tgtConn, string ruleCode)
    {
        try
        {
            if (AppFormCodeRuleExistsInTarget(tgtConn, ruleCode))
            {
                System.Diagnostics.Debug.WriteLine($"[APP表单编码规则] {ruleCode} 目标库已存在，跳过");
                return;
            }

            var rule = GetAppFormCodeRuleFromSource(srcConn, ruleCode);
            if (rule == null)
            {
                System.Diagnostics.Debug.WriteLine($"[APP表单编码规则] {ruleCode} 在源库中未找到");
                return;
            }

            TableCopyService.CopyTableData(srcConn, tgtConn, "S_BILLCODERULE", "CODE", ruleCode, false, "[APP表单编码规则]");
            TableCopyService.CopyTableData(srcConn, tgtConn, "S_BILLCODERULEDETAIL", "BILLCODERULEGUID", rule.Item1, false, "[APP表单编码规则]");

            System.Diagnostics.Debug.WriteLine($"[APP表单编码规则] {ruleCode} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[APP表单编码规则] {ruleCode} 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 检查目标库中编码规则是否存在
    /// </summary>
    private bool AppFormCodeRuleExistsInTarget(SqlConnection tgtConn, string code)
    {
        var sql = @"SELECT COUNT(*) FROM dbo.S_BILLCODERULE WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, tgtConn);
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 从源库获取编码规则的GUID
    /// </summary>
    private Tuple<string, DataTable>? GetAppFormCodeRuleFromSource(SqlConnection srcConn, string code)
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
    /// 复制APP标准查询：从S_APP_CONTROL中DATASELECTCODE不为空的行取值，
    /// 若目标库不存在对应标准查询则从源库复制S_DATASELECT
    /// </summary>
    private void CopyAppFormStandardQueries(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid)
    {
        try
        {
            var sql = @"SELECT DATASELECTCODE FROM dbo.S_APP_CONTROL
                        WHERE OBJECTGUID = @guid AND DATASELECTCODE IS NOT NULL AND DATASELECTCODE <> ''";
            using var cmd = new SqlCommand(sql, srcConn);
            cmd.Parameters.AddWithValue("@guid", objectGuid);
            using var reader = cmd.ExecuteReader();
            var dataSelectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var code = reader.GetString(0)?.Trim();
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        dataSelectCodes.Add(code);
                    }
                }
            }
            reader.Close();

            foreach (var code in dataSelectCodes)
            {
                CopyOneAppFormStandardQuery(srcConn, tgtConn, code);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[APP表单标准查询] 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 复制单条标准查询（S_DATASELECT）
    /// </summary>
    private void CopyOneAppFormStandardQuery(SqlConnection srcConn, SqlConnection tgtConn, string code)
    {
        try
        {
            if (AppFormStandardQueryExistsInTarget(tgtConn, code))
            {
                System.Diagnostics.Debug.WriteLine($"[APP表单标准查询] {code} 目标库已存在，跳过");
                return;
            }

            var dt = GetAppFormStandardQueryFromSource(srcConn, code);
            if (dt == null || dt.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[APP表单标准查询] {code} 在源库中未找到");
                return;
            }

            TableCopyService.CopyTableData(srcConn, tgtConn, "S_DATASELECT", "CODE", code, false, "[APP表单标准查询]");

            System.Diagnostics.Debug.WriteLine($"[APP表单标准查询] {code} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[APP表单标准查询] {code} 复制失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 检查目标库中标准查询是否存在
    /// </summary>
    private bool AppFormStandardQueryExistsInTarget(SqlConnection tgtConn, string code)
    {
        var sql = @"SELECT COUNT(*) FROM dbo.S_DATASELECT WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, tgtConn);
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 从源库获取标准查询数据
    /// </summary>
    private DataTable? GetAppFormStandardQueryFromSource(SqlConnection srcConn, string code)
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
