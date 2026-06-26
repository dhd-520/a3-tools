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

public partial class CrossDbCopyWebObjectForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;
    private System.Data.DataTable? _searchResults;

    public CrossDbCopyWebObjectForm(IToolContext context, Account? currentAccount)
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
                {
                    listBox.Items.Add(item);
                }
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
        if (string.IsNullOrWhiteSpace(txtWebObjectCode.Text))
        {
            MessageBox.Show("请输入WEB看板CODE！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var codes = txtWebObjectCode.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray();
        if (codes.Length == 0)
        {
            MessageBox.Show("请输入有效的WEB看板CODE！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        lblProgress.Text = "正在复制WEB看板...";
        progressBar.Value = 50;

        var success = await CopyWebObjectsAsync(
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

    private async Task<bool> CopyWebObjectsAsync(
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

                    var webObjectGuid = GetWebObjectGuid(srcConn, code);
                    if (string.IsNullOrEmpty(webObjectGuid))
                    {
                        failCodes.Add(code + "(未找到)");
                        continue;
                    }

                    TableCopyService.CopyTableData(srcConn, tgtConn, "S_WEBOBJECT", "GUID", webObjectGuid, deleteFirst, "[Web看板]");
                    TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, "S_WEBCONTROL", "WEBOBJECTGUID", webObjectGuid, deleteFirst, "[Web看板]");
                    TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, "S_WEBDATA", "WEBOBJECTGUID", webObjectGuid, deleteFirst, "[Web看板]");
                    TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, "S_WEBSTYLE", "WEBOBJECTGUID", webObjectGuid, deleteFirst, "[Web看板]");
                    TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, "S_WEBCMD", "WEBOBJECTGUID", webObjectGuid, deleteFirst, "[Web看板]");
                    successCount++;
                }

                progressBar.Invoke(new Action(() => progressBar.Value = 100));
                this.Invoke(new Action(() =>
                {
                    if (failCodes.Count > 0)
                    {
                        MessageBox.Show("完成！成功 " + successCount + " 个，失败 " + failCodes.Count + " 个：\n" + string.Join("\n", failCodes), "部分成功", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("WEB看板复制完成！共 " + successCount + " 个。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    // 不自动关闭，方便继续操作
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

    private string? GetWebObjectGuid(SqlConnection conn, string code)
    {
        var sql = "SELECT GUID FROM dbo.S_WEBOBJECT WHERE CODE = @code";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@code", code);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
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
SELECT GUID AS WEBOBJECTGUID,
       CODE AS 代码,
       NAME AS 名称,
       DESCRIPTION AS 备注
FROM S_WEBOBJECT
WHERE CODE LIKE '%{keyword}%' OR NAME LIKE '%{keyword}%'
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
                    // 隐藏WEBOBJECTGUID列
                    if (dgvSearchResults.Columns.Contains("WEBOBJECTGUID"))
                    {
                        dgvSearchResults.Columns["WEBOBJECTGUID"].Visible = false;
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
            MessageBox.Show("请先选择要添加的WEB看板！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedCodes = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            var code = row.Cells["代码"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(code))
            {
                selectedCodes.Add(code);
            }
        }

        if (selectedCodes.Count == 0) return;

        // 追加到现有内容
        var currentText = txtWebObjectCode.Text.Trim();
        var separator = string.IsNullOrEmpty(currentText) ? "" : ";";

        var existingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
        {
            currentText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                .ForEach(c => existingCodes.Add(c.Trim()));
        }

        var newCodes = selectedCodes.Where(c => !existingCodes.Contains(c)).ToList();
        if (newCodes.Count == 0)
        {
            MessageBox.Show("选中的WEB看板已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var addedText = string.Join(";", newCodes);
        txtWebObjectCode.Text = currentText + separator + addedText;

        lblSearchProgress.Text = $"已添加 {newCodes.Count} 个WEB看板到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        // 清空WebObjectCode
        txtWebObjectCode.Text = "";
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
}
