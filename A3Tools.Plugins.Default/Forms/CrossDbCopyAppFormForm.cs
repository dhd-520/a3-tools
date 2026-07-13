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
using A3Tools.Common.DataAccess;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyAppFormForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;
    private Account? _srcAccount;
    private Account? _tgtAccount;
    private System.Data.DataTable? _searchResults;

    public CrossDbCopyAppFormForm(IToolContext context, Account? currentAccount)
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
    /// 预选为空时，源库和目标库均保持空白。
    /// 带入后用户仍可在工具内自行修改或重新选择。
    /// </summary>
    private void LoadPresetAccounts()
    {
        var preset = _context.GetToolDatabasePreset();
        _srcAccount = preset.SourceAccount;
        _tgtAccount = preset.TargetAccount;
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
        var tempAccount = BuildTempAccount(server, dbName, user, password);
        var da = ProxyHelper.CreateDataAccess(tempAccount);
        return await ProxyHelper.TestConnectionAsync(da);
    }

    private Account BuildTempAccount(string server, string dbName, string user, string password)
    {
        var account = new Account
        {
            Database = server,
            DatabaseName = dbName,
            DbUser = user,
            DbPassword = password,
            ConnectionMode = DataAccessMode.Direct
        };
        if (_srcAccount != null && _srcAccount.ConnectionMode == DataAccessMode.Http &&
            server == _srcAccount.Database && dbName == _srcAccount.DatabaseName)
        {
            account.ConnectionMode = DataAccessMode.Http;
            account.HttpEndpoint = _srcAccount.HttpEndpoint;
            account.HttpSecretKey = _srcAccount.HttpSecretKey;
            account.HttpServerPublicKey = _srcAccount.HttpServerPublicKey;
        }
        else if (_tgtAccount != null && _tgtAccount.ConnectionMode == DataAccessMode.Http &&
                 server == _tgtAccount.Database && dbName == _tgtAccount.DatabaseName)
        {
            account.ConnectionMode = DataAccessMode.Http;
            account.HttpEndpoint = _tgtAccount.HttpEndpoint;
            account.HttpSecretKey = _tgtAccount.HttpSecretKey;
            account.HttpServerPublicKey = _tgtAccount.HttpServerPublicKey;
        }
        return account;
    }

    private async Task<bool> CopyAppFormsAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        string objectGuids, bool deleteFirst)
    {
        try
        {
            var srcAccount = BuildTempAccount(srcServer, srcDbName, srcUser, srcPassword);
            var tgtAccount = BuildTempAccount(tgtServer, tgtDbName, tgtUser, tgtPassword);
            var srcDA = ProxyHelper.CreateDataAccess(srcAccount);
            var tgtDA = ProxyHelper.CreateDataAccess(tgtAccount);
            if (srcDA == null || tgtDA == null)
            {
                MessageBox.Show("创建数据访问失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var guidList = objectGuids.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToList();

            int total = guidList.Count;
            int current = 0;

            foreach (var objectGuid in guidList)
            {
                current++;
                var progress = 30 + (current * 70 / total);
                progressBar.Value = progress;
                lblProgress.Text = "正在复制：" + objectGuid + " (" + current + "/" + total + ")";
                Application.DoEvents();

                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_APP_OBJECT", "GUID", objectGuid, deleteFirst, "[APP表单]");
                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_APP_DATA", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");
                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_APP_CONTROL", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");
                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_APP_FILTER", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");
                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_OBJECTBAR", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");
                await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_APP_OBJECT_BACKGROUD", "OBJECTGUID", objectGuid, deleteFirst, "[APP表单]");

                await CopyAppFormCodeRulesAsync(srcDA, tgtDA, objectGuid);
                await CopyAppFormStandardQueriesAsync(srcDA, tgtDA, objectGuid);
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("复制失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    // ==================== APP表单编码规则 & 标准查询复制 ====================

    private async Task CopyAppFormCodeRulesAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid)
    {
        try
        {
            var sql = @"SELECT DEFAULTVALUE FROM dbo.S_APP_CONTROL
                        WHERE OBJECTGUID = '{ProxyHelper.EscapeSql(objectGuid)}' AND (DATANAME = 'BILLNO' OR DATANAME = 'CODE')";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);
            var codeRuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                var defaultValue = row[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(defaultValue))
                    codeRuleCodes.Add(defaultValue);
            }

            foreach (var ruleCode in codeRuleCodes)
            {
                await CopyOneAppFormCodeRuleAsync(srcDA, tgtDA, ruleCode);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP表单编码规则] 复制失败：" + ex.Message);
        }
    }

    private async Task CopyOneAppFormCodeRuleAsync(IDataAccess srcDA, IDataAccess tgtDA, string ruleCode)
    {
        try
        {
            var existsObj = await ProxyHelper.ExecuteScalarAsync(tgtDA, $"SELECT COUNT(*) FROM dbo.S_BILLCODERULE WHERE CODE = '{ProxyHelper.EscapeSql(ruleCode)}'");
            if (Convert.ToInt32(existsObj ?? 0) > 0)
            {
                Debug.WriteLine($"[APP表单编码规则] {ruleCode} 目标库已存在，跳过");
                return;
            }

            var guidObj = await ProxyHelper.ExecuteScalarAsync(srcDA, $"SELECT GUID FROM dbo.S_BILLCODERULE WHERE CODE = '{ProxyHelper.EscapeSql(ruleCode)}'");
            var guid = guidObj?.ToString();
            if (string.IsNullOrEmpty(guid))
            {
                Debug.WriteLine($"[APP表单编码规则] {ruleCode} 在源库中未找到");
                return;
            }

            await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_BILLCODERULE", "CODE", ruleCode, false, "[APP表单编码规则]");
            await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_BILLCODERULEDETAIL", "BILLCODERULEGUID", guid, false, "[APP表单编码规则]");
            Debug.WriteLine($"[APP表单编码规则] {ruleCode} 复制成功");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP表单编码规则] {ruleCode} 复制失败：" + ex.Message);
        }
    }

    private async Task CopyAppFormStandardQueriesAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid)
    {
        try
        {
            var sql = @"SELECT DATASELECTCODE FROM dbo.S_APP_CONTROL
                        WHERE OBJECTGUID = '{ProxyHelper.EscapeSql(objectGuid)}' AND DATASELECTCODE IS NOT NULL AND DATASELECTCODE <> ''";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);
            var dataSelectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                var code = row[0]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(code))
                    dataSelectCodes.Add(code);
            }

            foreach (var code in dataSelectCodes)
            {
                await CopyOneAppFormStandardQueryAsync(srcDA, tgtDA, code);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP表单标准查询] 复制失败：" + ex.Message);
        }
    }

    private async Task CopyOneAppFormStandardQueryAsync(IDataAccess srcDA, IDataAccess tgtDA, string code)
    {
        try
        {
            var existsObj = await ProxyHelper.ExecuteScalarAsync(tgtDA, $"SELECT COUNT(*) FROM dbo.S_DATASELECT WHERE CODE = '{ProxyHelper.EscapeSql(code)}'");
            if (Convert.ToInt32(existsObj ?? 0) > 0)
            {
                Debug.WriteLine($"[APP表单标准查询] {code} 目标库已存在，跳过");
                return;
            }

            await ProxyHelper.CopyTableDataAsync(srcDA, tgtDA, "S_DATASELECT", "CODE", code, false, "[APP表单标准查询]");
            Debug.WriteLine($"[APP表单标准查询] {code} 复制成功");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APP表单标准查询] {code} 复制失败：" + ex.Message);
        }
    }

    private void BtnSearch_Click(object? sender, EventArgs e)
    {
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

        Task.Run(async () =>
        {
            try
            {
                var srcAccount = BuildTempAccount(txtSourceServer.Text.Trim(), txtSourceDbName.Text.Trim(), txtSourceUser.Text.Trim(), txtSourcePassword.Text);
                var srcDA = ProxyHelper.CreateDataAccess(srcAccount);

                var sql = $@"
SELECT GUID AS OBJECTGUID,
       CODE AS 代码,
       NAME AS APP表单名称,
       DESCRIPTION AS 备注
FROM S_APP_OBJECT
WHERE NAME LIKE '%{ProxyHelper.EscapeSql(keyword)}%' OR CODE LIKE '%{ProxyHelper.EscapeSql(keyword)}%'
ORDER BY NAME";

                var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);

                this.Invoke(new Action(() =>
                {
                    if (dgvSearchResults.Columns.Contains("chk"))
                        dgvSearchResults.Columns.Remove("chk");
                    dgvSearchResults.DataSource = dt;
                    var checkCol = new DataGridViewCheckBoxColumn { HeaderText = "选择", Width = 50, Name = "chk" };
                    dgvSearchResults.Columns.Insert(0, checkCol);
                    dgvSearchResults.AutoResizeColumns();
                    if (dgvSearchResults.Columns.Contains("OBJECTGUID"))
                        dgvSearchResults.Columns["OBJECTGUID"].Visible = false;
                    if (dgvSearchResults.Rows.Count > 0)
                        dgvSearchResults.Rows[0].Selected = true;
                    foreach (DataGridViewRow row in dgvSearchResults.Rows)
                    {
                        var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                        if (checkCell != null) checkCell.Value = row.Selected;
                    }
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
                this.Invoke(new Action(() => btnSearch.Enabled = true));
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
                selectedGuids.Add(guid);
        }

        if (selectedGuids.Count == 0) return;

        var currentText = txtObjectGuids.Text.Trim();
        var separator = string.IsNullOrEmpty(currentText) ? "" : ";";

        var existingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
        {
            currentText.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList().ForEach(g => existingGuids.Add(g.Trim()));
        }

        var newGuids = selectedGuids.Where(g => !existingGuids.Contains(g)).ToList();
        if (newGuids.Count == 0)
        {
            MessageBox.Show("选中的APP表单已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        txtObjectGuids.Text = currentText + separator + string.Join(";", newGuids);
        lblSearchProgress.Text = $"已添加 {newGuids.Count} 个APP表单到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        txtObjectGuids.Text = "";
        dgvSearchResults.ClearSelection();
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
}

