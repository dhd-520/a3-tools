using System.Data;
using System.Windows.Forms;
using A3Tools.Common.DataAccess;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyFormForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;
    private Account? _srcAccount;
    private Account? _tgtAccount;

    // 用于存储搜索到的表单数据
    private DataTable? _searchResults;
    private DataView? _dataView;

    public CrossDbCopyFormForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        // Http 代理模式已支持，不再拦截
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

        // 快速过滤行
        txtFilterName.TextChanged += (s, e) => ApplyFilter();
        txtFilterSolution.TextChanged += (s, e) => ApplyFilter();
        txtFilterBizGroup.TextChanged += (s, e) => ApplyFilter();
        txtFilterGroup.TextChanged += (s, e) => ApplyFilter();

        dgvSearchResults.ColumnWidthChanged += (s, e) => SyncFilterRowPositions();
        dgvSearchResults.ColumnAdded += (s, e) => SyncFilterRowPositions();
        dgvSearchResults.DataSourceChanged += (s, e) => SyncFilterRowPositions();

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

    // ==================== Http 代理模式辅助 ====================

    private IDataAccess? GetSourceDA() => ProxyHelper.CreateDataAccess(_srcAccount);
    private IDataAccess? GetTargetDA() => ProxyHelper.CreateDataAccess(_tgtAccount);
    private bool IsSourceHttp => ProxyHelper.IsHttp(_srcAccount);
    private bool IsTargetHttp => ProxyHelper.IsHttp(_tgtAccount);
    private bool IsHttpMode => IsSourceHttp || IsTargetHttp;

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
                        _srcAccount = selectedAcc;
                        txtSourceServer.Text = selectedAcc.Database ?? "";
                        txtSourceDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtSourceUser.Text = selectedAcc.DbUser ?? "";
                        txtSourcePassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    else
                    {
                        _tgtAccount = selectedAcc;
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
                        _srcAccount = selectedAcc;
                        txtSourceServer.Text = selectedAcc.Database ?? "";
                        txtSourceDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtSourceUser.Text = selectedAcc.DbUser ?? "";
                        txtSourcePassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    else
                    {
                        _tgtAccount = selectedAcc;
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

        Task.Run(async () =>
        {
            try
            {
                var escapedKeyword = ProxyHelper.EscapeSql(keyword);
                var sql = $@"
SELECT A.GUID AS OBJECTGUID,
       A.CODE AS 代码,
       A.NAME AS 名称,
       F.CODE AS 解决方案代码,
       F.NAME AS 解决方案,
       B.NAME AS 业务分组,
	   ISNULL(G3.NAME+'/','')+ISNULL(G2.NAME+'/','')+ISNULL(G1.NAME,'') 分组
FROM S_OBJECT A
LEFT JOIN S_SUBSYSTEM B ON A.SUBSYSTEMGUID = B.GUID
LEFT JOIN S_BUSINESSTYPE F ON B.BUSINESSTYPEGUID = F.GUID
LEFT JOIN S_OBJECTGROUP G1 ON A.OBJECTGROUPGUID=G1.GUID
LEFT JOIN S_OBJECTGROUP G2 ON G1.PARENTGUID=G2.GUID
LEFT JOIN S_OBJECTGROUP G3 ON G2.PARENTGUID=G3.GUID
WHERE A.NAME LIKE '%{escapedKeyword}%' OR F.NAME LIKE '%{escapedKeyword}%' OR G1.NAME LIKE '%{escapedKeyword}%' OR G2.NAME LIKE '%{escapedKeyword}%' OR G3.NAME LIKE '%{escapedKeyword}%'
ORDER BY F.NAME,B.NAME,ISNULL(G3.NAME+'/','')+ISNULL(G2.NAME+'/','')+ISNULL(G1.NAME,''),A.NAME";

                DataTable dt;
                if (IsSourceHttp)
                {
                    var da = GetSourceDA();
                    if (da == null) throw new Exception("无法创建源数据访问对象，请确认源账套已选择");
                    dt = await ProxyHelper.ExecuteQueryToDataTableAsync(da, sql);
                }
                else
                {
                    var server = txtSourceServer.Text.Trim();
                    var dbName = txtSourceDbName.Text.Trim();
                    var user = txtSourceUser.Text.Trim();
                    var password = txtSourcePassword.Text;

                    var connString = string.IsNullOrEmpty(user)
                        ? $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;"
                        : $"Server={server};Database={dbName};User Id={user};Password={EncryptionService.Decrypt(password)};TrustServerCertificate=True;";

                    using var conn = new SqlConnection(connString);
                    using var cmd = new SqlCommand(sql, conn);
                    using var adapter = new SqlDataAdapter(cmd);
                    dt = new DataTable();
                    adapter.Fill(dt);
                }

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
                    _dataView = dt.DefaultView;
                    ApplyFilter();
                    SyncFilterRowPositions();
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

        if (!await TestConnectionAsync(txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text, _srcAccount))
        {
            MessageBox.Show("源数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在连接目标数据库...";
        progressBar.Value = 30;

        if (!await TestConnectionAsync(txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text, _tgtAccount))
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
            txtObjectGuids.Text.Trim(), chkDeleteFirst.Checked, chkCopyStoredProcs.Checked, chkCopyTableStructure.Checked);

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

    private async Task<bool> TestConnectionAsync(string server, string dbName, string user, string password, Account? account = null)
    {
        // Http 模式：通过 IDataAccess 测试连接
        if (ProxyHelper.IsHttp(account))
        {
            var da = ProxyHelper.CreateDataAccess(account);
            if (da == null) return false;
            return await ProxyHelper.TestConnectionAsync(da);
        }

        // 直连模式：保持原逻辑
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
        string objectGuids, bool deleteFirst, bool copyStoredProcs, bool copyTableStructures)
    {
        // Http 代理模式
        if (IsHttpMode)
        {
            return await CopyFormsHttpAsync(objectGuids, deleteFirst, copyStoredProcs, copyTableStructures);
        }

        // 直连模式：保持原逻辑
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

                    // 复制表结构（仅当勾选时, 且 OBJECTTYPE IN ('1','5') 录入表单）
                    if (copyTableStructures)
                    {
                        var objType = GetObjectType(srcConn, objectGuid);
                        if (objType == "1" || objType == "5")
                        {
                            this.Invoke(new Action(() =>
                            {
                                lblProgress.Text = "正在复制表结构：" + objectGuid + " (" + current + "/" + total + ")";
                            }));
                            CopyTableStructuresForObject(srcConn, tgtConn, objectGuid);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[表结构] {objectGuid} OBJECTTYPE={objType ?? "(null)"}, 不是录入表单 (1/5), 跳过表结构复制");
                        }
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

    // ==================== Http 代理模式核心复制 ====================

    private async Task<bool> CopyFormsHttpAsync(string objectGuids, bool deleteFirst, bool copyStoredProcs, bool copyTableStructures)
    {
        try
        {
            var srcDA = GetSourceDA();
            var tgtDA = GetTargetDA();
            if (srcDA == null || tgtDA == null)
                throw new Exception("无法创建数据访问对象，请确认源/目标账套已选择");

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
                await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_OBJECT", "GUID", objectGuid, deleteFirst, "[Win表单]");

                // 复制S_CONTROL表
                await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_CONTROL", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

                // 复制S_DATA表
                await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_DATA", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

                // 复制样式表
                await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_OBJECTSTYLE", "OBJECTGUID", objectGuid, deleteFirst, "[Win表单]");

                // 复制编码规则
                await CopyCodeRulesForObjectHttpAsync(srcDA, tgtDA, objectGuid);

                // 复制标准查询
                await CopyStandardQueriesForObjectHttpAsync(srcDA, tgtDA, objectGuid);

                // 复制关联存储过程（仅当勾选时）
                if (copyStoredProcs)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblProgress.Text = "正在复制存储过程：" + objectGuid + " (" + current + "/" + total + ")";
                    }));
                    await CopyStoredProcsForObjectHttpAsync(srcDA, tgtDA, objectGuid, deleteFirst);
                }

                // 复制表结构（仅当勾选时, 且 OBJECTTYPE IN ('1','5') 录入表单）
                if (copyTableStructures)
                {
                    var objType = await GetObjectTypeHttpAsync(srcDA, objectGuid);
                    if (objType == "1" || objType == "5")
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblProgress.Text = "正在复制表结构：" + objectGuid + " (" + current + "/" + total + ")";
                        }));
                        await CopyTableStructuresForObjectHttpAsync(srcDA, tgtDA, objectGuid);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[表结构] {objectGuid} OBJECTTYPE={objType ?? "(null)"}, 不是录入表单 (1/5), 跳过表结构复制");
                    }
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
    }

    // ==================== 直连模式辅助方法（原逻辑不变） ====================

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
    /// 应用过滤：根据 4 个 txtFilter* 动态过滤 DataView (AND 子串匹配)
    /// </summary>
    private void ApplyFilter()
    {
        if (_dataView == null) return;
        var filters = new List<string>();
        var name = txtFilterName.Text.Trim();
        if (!string.IsNullOrEmpty(name))
            filters.Add($"[名称] LIKE '%{EscapeLike(name)}%'");
        var solution = txtFilterSolution.Text.Trim();
        if (!string.IsNullOrEmpty(solution))
            filters.Add($"[解决方案] LIKE '%{EscapeLike(solution)}%'");
        var bizGroup = txtFilterBizGroup.Text.Trim();
        if (!string.IsNullOrEmpty(bizGroup))
            filters.Add($"[业务分组] LIKE '%{EscapeLike(bizGroup)}%'");
        var group = txtFilterGroup.Text.Trim();
        if (!string.IsNullOrEmpty(group))
            filters.Add($"[分组] LIKE '%{EscapeLike(group)}%'");
        _dataView.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
    }

    /// <summary>
    /// 同步过滤行 TextBox 位置/宽度与 dgvSearchResults 列对齐
    /// </summary>
    private void SyncFilterRowPositions()
    {
        if (dgvSearchResults.Columns.Count == 0) return;
        int x = dgvSearchResults.Left - pnlFilterRow.Left;
        if (dgvSearchResults.RowHeadersVisible)
            x += dgvSearchResults.RowHeadersWidth;
        if (dgvSearchResults.Columns.Contains("chk") && dgvSearchResults.Columns["chk"].Visible)
            x += dgvSearchResults.Columns["chk"].Width;
        int y = (pnlFilterRow.ClientSize.Height - txtFilterName.PreferredHeight) / 2;
        if (y < 0) y = 0;

        if (dgvSearchResults.Columns.Contains("名称") && dgvSearchResults.Columns["名称"].Visible)
        {
            txtFilterName.Visible = true;
            txtFilterName.Location = new Point(x, y);
            txtFilterName.Width = dgvSearchResults.Columns["名称"].Width;
            x += txtFilterName.Width;
        }
        else { txtFilterName.Visible = false; }

        if (dgvSearchResults.Columns.Contains("解决方案") && dgvSearchResults.Columns["解决方案"].Visible)
        {
            txtFilterSolution.Visible = true;
            txtFilterSolution.Location = new Point(x, y);
            txtFilterSolution.Width = dgvSearchResults.Columns["解决方案"].Width;
            x += txtFilterSolution.Width;
        }
        else { txtFilterSolution.Visible = false; }

        if (dgvSearchResults.Columns.Contains("业务分组") && dgvSearchResults.Columns["业务分组"].Visible)
        {
            txtFilterBizGroup.Visible = true;
            txtFilterBizGroup.Location = new Point(x, y);
            txtFilterBizGroup.Width = dgvSearchResults.Columns["业务分组"].Width;
            x += txtFilterBizGroup.Width;
        }
        else { txtFilterBizGroup.Visible = false; }

        if (dgvSearchResults.Columns.Contains("分组") && dgvSearchResults.Columns["分组"].Visible)
        {
            txtFilterGroup.Visible = true;
            txtFilterGroup.Location = new Point(x, y);
            txtFilterGroup.Width = dgvSearchResults.Columns["分组"].Width;
        }
        else { txtFilterGroup.Visible = false; }
    }

    /// <summary>
    /// 转义 DataView.RowFilter LIKE 模式中的单引号
    /// </summary>
    private static string EscapeLike(string s) => s.Replace("'", "''");

    /// <summary>
    /// 查 S_OBJECT.OBJECTTYPE（直连模式），返回 null 表示表单不存在
    /// ★ 2026-09-30 陛下要求：只有 OBJECTTYPE IN ('1','5') 的录入表单才复制表结构
    /// </summary>
    private string? GetObjectType(SqlConnection conn, string objectGuid)
    {
        var sql = "SELECT OBJECTTYPE FROM S_OBJECT WHERE GUID = @guid";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@guid", objectGuid);
        var result = cmd.ExecuteScalar();
        return result?.ToString()?.Trim();
    }

    /// <summary>
    /// 复制表结构（直连模式）：查 S_DATA 去重 VIEWNAME, 对比源/目标库
    ///   - 源无 -> 跳过
    ///   - 目标无 -> 生成 CREATE TABLE 脚本并执行
    ///   - 都有 -> 加缺失列
    /// </summary>
    private void CopyTableStructuresForObject(SqlConnection srcConn, SqlConnection tgtConn, string objectGuid)
    {
        try
        {
            var viewNameSql = @"SELECT DISTINCT VIEWNAME FROM dbo.S_DATA
                                WHERE OBJECTGUID = @guid AND ISNULL(VIEWNAME, '') <> ''";
            var viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand(viewNameSql, srcConn))
            {
                cmd.Parameters.AddWithValue("@guid", objectGuid);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var vn = reader.GetString(0).Trim();
                    if (!string.IsNullOrEmpty(vn))
                        viewNames.Add(vn);
                }
            }

            foreach (var viewName in viewNames)
            {
                CopyOneTableStructure(srcConn, tgtConn, viewName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[表结构] 复制失败：{ex.Message}");
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✗ 表结构复制失败：{ex.Message}";
                lblProgress.ForeColor = Color.Red;
            }));
        }
    }

    /// <summary>
    /// 复制单张表的结构（直连模式，CREATE TABLE 生成 + 缺失列添加）
    /// </summary>
    private void CopyOneTableStructure(SqlConnection srcConn, SqlConnection tgtConn, string viewName)
    {
        try
        {
            if (!TableExistsInDb(srcConn, viewName))
            {
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 源库不存在，跳过");
                return;
            }

            // 拉一次源列 + 自增列, 后面两个分支都用到
            var srcCols = GetColumnsFromDb(srcConn, viewName);
            var identityCols = GetIdentityColumnNames(srcConn, viewName);

            if (!TableExistsInDb(tgtConn, viewName))
            {
                // 目标不存在 -> 生成 CREATE TABLE 脚本, 在目标库执行
                //   不需要源库访问目标库 (跨库权限/HTTP 模式都适用)
                var createSql = BuildCreateTableSql(viewName, srcCols, identityCols);
                using var cmd = new SqlCommand(createSql, tgtConn);
                cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 已在目标库创建");
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = $"✓ {viewName} 表结构已创建";
                    lblProgress.ForeColor = Color.Green;
                }));
                return;
            }

            // 双方都存在 -> 加缺失列
            var tgtCols = GetColumnsFromDb(tgtConn, viewName);
            var missing = srcCols.Where(sc => !tgtCols.Any(tc => string.Equals(tc.Name, sc.Name, StringComparison.OrdinalIgnoreCase))).ToList();

            if (missing.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 双方一致, 无需补列");
                return;
            }

            foreach (var col in missing)
            {
                var alterSql = $"ALTER TABLE [dbo].[{viewName}] ADD [{col.Name}] {col.Type}";
                using var cmd = new SqlCommand(alterSql, tgtConn);
                cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 已加列 {col.Name} {col.Type}");
            }
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✓ {viewName} 已补 {missing.Count} 个缺失列";
                lblProgress.ForeColor = Color.Green;
            }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 复制失败：{ex.Message}");
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✗ {viewName} 复制失败：{ex.Message}";
                lblProgress.ForeColor = Color.Red;
            }));
        }
    }

    /// <summary>
    /// 检查表是否存在 (INFORMATION_SCHEMA.TABLES)
    /// </summary>
    private bool TableExistsInDb(SqlConnection conn, string tableName)
    {
        var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    /// <summary>
    /// 获取表的自增列名集合
    /// </summary>
    private HashSet<string> GetIdentityColumnNames(SqlConnection conn, string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sql = "SELECT name FROM sys.identity_columns WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", tableName);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// 获取表的列定义 (名称 + 完整类型字符串 + 是否自增)
    /// </summary>
    private List<(string Name, string Type)> GetColumnsFromDb(SqlConnection conn, string tableName)
    {
        var result = new List<(string, string)>();
        var sql = @"SELECT COLUMN_NAME, DATA_TYPE,
                    ISNULL(CHARACTER_MAXIMUM_LENGTH, 0),
                    ISNULL(NUMERIC_PRECISION, 0),
                    ISNULL(NUMERIC_SCALE, 0),
                    IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @name
                    ORDER BY ORDINAL_POSITION";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", tableName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1).ToUpper();
            var charMax = reader.GetInt32(2);
            var numPrec = reader.GetInt32(3);
            var numScale = reader.GetInt32(4);
            var isNullable = reader.GetString(5) == "YES";

            string typeStr;
            if (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR" || dataType == "BINARY" || dataType == "VARBINARY")
                typeStr = charMax == -1 ? $"{dataType}(MAX)" : $"{dataType}({charMax})";
            else if (dataType == "DECIMAL" || dataType == "NUMERIC")
                typeStr = $"{dataType}({numPrec},{numScale})";
            else
                typeStr = dataType;

            if (!isNullable)
                typeStr += " NOT NULL";

            result.Add((colName, typeStr));
        }
        return result;
    }

    /// <summary>
    /// 生成 CREATE TABLE 脚本 (含 IDENTITY 自增列)
    /// </summary>
    private string BuildCreateTableSql(string tableName, List<(string Name, string Type)> columns, HashSet<string> identityCols)
    {
        if (columns.Count == 0)
            return $"CREATE TABLE [dbo].[{tableName}] ([_placeholder_] INT NULL)";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"CREATE TABLE [dbo].[{tableName}] (");
        var parts = new List<string>();
        foreach (var col in columns)
        {
            var typeStr = col.Type;
            if (identityCols.Contains(col.Name))
            {
                typeStr = typeStr.Replace(" NOT NULL", "");
                typeStr += " IDENTITY(1,1)";
            }
            parts.Add($"    [{col.Name}] {typeStr}");
        }
        sb.AppendLine(string.Join("," + Environment.NewLine, parts));
        sb.AppendLine(")");
        return sb.ToString();
    }

    /// <summary>
    /// 查 S_OBJECT.OBJECTTYPE（Http 模式），返回 null 表示表单不存在
    /// ★ 2026-09-30 陛下要求：只有 OBJECTTYPE IN ('1','5') 的录入表单才复制表结构
    /// </summary>
    private async Task<string?> GetObjectTypeHttpAsync(IDataAccess da, string objectGuid)
    {
        var sql = $"SELECT OBJECTTYPE FROM S_OBJECT WHERE GUID = '{ProxyHelper.EscapeSql(objectGuid)}'";
        var result = await ProxyHelper.ExecuteScalarAsync(da, sql);
        return result?.ToString()?.Trim();
    }

    /// <summary>
    /// 复制表结构 (Http 代理模式)
    /// </summary>
    private async Task CopyTableStructuresForObjectHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid)
    {
        try
        {
            var viewNameSql = $@"SELECT DISTINCT VIEWNAME FROM dbo.S_DATA
                                 WHERE OBJECTGUID = '{ProxyHelper.EscapeSql(objectGuid)}' AND ISNULL(VIEWNAME, '') <> ''";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, viewNameSql);
            var viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                var vn = row[0]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(vn))
                    viewNames.Add(vn);
            }

            foreach (var viewName in viewNames)
            {
                await CopyOneTableStructureHttpAsync(srcDA, tgtDA, viewName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[表结构] 复制失败：{ex.Message}");
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✗ 表结构复制失败：{ex.Message}";
                lblProgress.ForeColor = Color.Red;
            }));
        }
    }

    /// <summary>
    /// Http 模式: 复制单张表结构 (CREATE TABLE + 缺失列)
    /// </summary>
    private async Task CopyOneTableStructureHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string viewName)
    {
        try
        {
            var escaped = ProxyHelper.EscapeSql(viewName);

            // 源是否存在
            var srcExistSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{escaped}'";
            var srcExists = Convert.ToInt32(await ProxyHelper.ExecuteScalarAsync(srcDA, srcExistSql) ?? 0) > 0;
            if (!srcExists)
            {
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 源库不存在，跳过");
                return;
            }

            // 拉一次源列 + 自增列
            var srcCols = await GetColumnsHttpAsync(srcDA, viewName);
            var identityCols = await GetIdentityColumnNamesHttpAsync(srcDA, viewName);

            // 目标是否存在
            var tgtExistSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{escaped}'";
            var tgtExists = Convert.ToInt32(await ProxyHelper.ExecuteScalarAsync(tgtDA, tgtExistSql) ?? 0) > 0;

            if (!tgtExists)
            {
                var createSql = BuildCreateTableSql(viewName, srcCols, identityCols);
                var result = await ProxyHelper.ExecuteBatchAsync(tgtDA, createSql);
                if (!result.Success)
                    throw new Exception($"创建表 {viewName} 失败: {result.Message}");
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 已在目标库创建");
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = $"✓ {viewName} 表结构已创建";
                    lblProgress.ForeColor = Color.Green;
                }));
                return;
            }

            // 双方都有 -> 加缺失列
            var tgtCols = await GetColumnsHttpAsync(tgtDA, viewName);
            var missing = srcCols.Where(sc => !tgtCols.Any(tc => string.Equals(tc.Name, sc.Name, StringComparison.OrdinalIgnoreCase))).ToList();

            if (missing.Count == 0) return;

            foreach (var col in missing)
            {
                var alterSql = $"ALTER TABLE [dbo].[{viewName}] ADD [{col.Name}] {col.Type}";
                await ProxyHelper.ExecuteNonQueryAsync(tgtDA, alterSql);
                System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 已加列 {col.Name} {col.Type}");
            }
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✓ {viewName} 已补 {missing.Count} 个缺失列";
                lblProgress.ForeColor = Color.Green;
            }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[表结构] {viewName} 复制失败：{ex.Message}");
            this.Invoke(new Action(() =>
            {
                lblProgress.Text = $"✗ {viewName} 复制失败：{ex.Message}";
                lblProgress.ForeColor = Color.Red;
            }));
        }
    }

    /// <summary>
    /// Http 模式: 获取自增列名集合
    /// </summary>
    private async Task<HashSet<string>> GetIdentityColumnNamesHttpAsync(IDataAccess da, string tableName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var escaped = ProxyHelper.EscapeSql(tableName);
            var sql = $"SELECT name FROM sys.identity_columns WHERE object_id = OBJECT_ID('{escaped}')";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(da, sql);
            foreach (DataRow row in dt.Rows)
            {
                var name = row[0]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    result.Add(name);
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Http 模式: 获取表的列定义
    /// </summary>
    private async Task<List<(string Name, string Type)>> GetColumnsHttpAsync(IDataAccess da, string viewName)
    {
        var result = new List<(string, string)>();
        var escaped = ProxyHelper.EscapeSql(viewName);
        var sql = $@"SELECT COLUMN_NAME, DATA_TYPE,
                      ISNULL(CHARACTER_MAXIMUM_LENGTH, 0),
                      ISNULL(NUMERIC_PRECISION, 0),
                      ISNULL(NUMERIC_SCALE, 0),
                      IS_NULLABLE
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_NAME = '{escaped}'
                      ORDER BY ORDINAL_POSITION";
        var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(da, sql);
        foreach (DataRow row in dt.Rows)
        {
            var colName = row[0]?.ToString() ?? "";
            var dataType = (row[1]?.ToString() ?? "").ToUpper();
            var charMax = Convert.ToInt32(row[2] ?? 0);
            var numPrec = Convert.ToInt32(row[3] ?? 0);
            var numScale = Convert.ToInt32(row[4] ?? 0);
            var isNullable = (row[5]?.ToString() ?? "") == "YES";

            string typeStr;
            if (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR" || dataType == "BINARY" || dataType == "VARBINARY")
                typeStr = charMax == -1 ? $"{dataType}(MAX)" : $"{dataType}({charMax})";
            else if (dataType == "DECIMAL" || dataType == "NUMERIC")
                typeStr = $"{dataType}({numPrec},{numScale})";
            else
                typeStr = dataType;
            if (!isNullable)
                typeStr += " NOT NULL";

            result.Add((colName, typeStr));
        }
        return result;
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

    // ==================== 编码规则 & 标准查询复制（直连模式，原逻辑不变） ====================

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

    // ==================== Http 代理模式辅助方法 ====================

    private async Task<List<(string FieldName, string ProcName)>> GetStoredProcNamesHttpAsync(IDataAccess srcDA, string objectGuid)
    {
        var result = new List<(string, string)>();
        var sql = $@"SELECT AUDITINGPROCNAME, DELETEPROCNAME, UNAUDITINGPROCNAME
                     FROM dbo.S_OBJECT WHERE GUID = '{ProxyHelper.EscapeSql(objectGuid)}'";
        var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);
        if (dt.Rows.Count > 0)
        {
            foreach (DataColumn col in dt.Columns)
            {
                var val = dt.Rows[0][col]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                    result.Add((col.ColumnName, val));
            }
        }

        foreach (var (fn, pn) in result)
        {
            System.Diagnostics.Debug.WriteLine($"[GetStoredProcNamesHttp] {fn} -> {pn}");
        }

        return result;
    }

    private async Task<bool> ProcExistsInTargetHttpAsync(IDataAccess tgtDA, string procName)
    {
        var sql = $@"SELECT COUNT(*) FROM sys.objects
                     WHERE type = 'P' AND name = '{ProxyHelper.EscapeSql(procName)}' AND is_ms_shipped = 0";
        var result = await ProxyHelper.ExecuteScalarAsync(tgtDA, sql);
        return Convert.ToInt32(result ?? 0) > 0;
    }

    private async Task<string> GetProcDefinitionHttpAsync(IDataAccess srcDA, string procName)
    {
        var escaped = ProxyHelper.EscapeSql(procName);
        var sql = $@"SELECT definition FROM sys.sql_modules
                     WHERE object_id = OBJECT_ID('{escaped}', 'P')";
        var result = await ProxyHelper.ExecuteScalarAsync(srcDA, sql);
        if (result == null || result == DBNull.Value)
        {
            var sql2 = $@"SELECT OBJECT_DEFINITION(OBJECT_ID('{escaped}', 'P'))";
            result = await ProxyHelper.ExecuteScalarAsync(srcDA, sql2);
        }
        var text = result as string ?? "";
        System.Diagnostics.Debug.WriteLine($"[GetProcDefinitionHttp] procName={procName}, definition length={text.Length}");
        return text;
    }

    private async Task CreateProcInTargetHttpAsync(IDataAccess tgtDA, string procName, string definition)
    {
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTargetHttp] procName={procName}, definition length={definition?.Length ?? -1}");

        // 先删除已存在的同名存储过程
        if (await ProcExistsInTargetHttpAsync(tgtDA, procName))
        {
            var dropSql = "DROP PROCEDURE [" + procName + "]";
            await ProxyHelper.ExecuteNonQueryAsync(tgtDA, dropSql);
        }

        var createSql = NormalizeProcDefinition(definition, procName);
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTargetHttp] createSql length={createSql.Length}");
        System.Diagnostics.Debug.WriteLine($"[CreateProcInTargetHttp] createSql preview: {createSql.Substring(0, Math.Min(200, createSql.Length))}");
        // 存储过程定义可能含多语句，用 ExecuteBatchAsync
        var batchResult = await ProxyHelper.ExecuteBatchAsync(tgtDA, createSql);
        if (!batchResult.Success)
        {
            throw new Exception($"创建存储过程失败: {batchResult.Message}");
        }
    }

    private async Task CopyStoredProcsForObjectHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid, bool deleteFirst)
    {
        var procNames = await GetStoredProcNamesHttpAsync(srcDA, objectGuid);
        foreach (var (fieldName, procName) in procNames)
        {
            try
            {
                if (!await ProcExistsInTargetHttpAsync(tgtDA, procName))
                {
                    var definition = await GetProcDefinitionHttpAsync(srcDA, procName);
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
                    await CreateProcInTargetHttpAsync(tgtDA, procName, definition);
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

    private async Task CopyCodeRulesForObjectHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid)
    {
        try
        {
            var sql = $@"SELECT EXTENDS FROM dbo.S_CONTROL
                         WHERE OBJECTGUID = '{ProxyHelper.EscapeSql(objectGuid)}' AND (DATANAME = 'CODE' OR DATANAME = 'BILLNO')";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);
            var codeRuleGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                var extends = row[0]?.ToString();
                var dict = ParseExtendsField(extends);
                if (dict.TryGetValue("CodeRuleGuid", out var ruleGuid) && !string.IsNullOrWhiteSpace(ruleGuid))
                {
                    codeRuleGuids.Add(ruleGuid);
                }
            }

            foreach (var ruleGuid in codeRuleGuids)
            {
                await CopyOneCodeRuleHttpAsync(srcDA, tgtDA, ruleGuid);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[编码规则] 复制失败：" + ex.Message);
        }
    }

    private async Task CopyOneCodeRuleHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string ruleCode)
    {
        try
        {
            if (await CodeRuleExistsInTargetHttpAsync(tgtDA, ruleCode))
            {
                System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 目标库已存在，跳过");
                return;
            }

            var guid = await GetCodeRuleGuidFromSourceHttpAsync(srcDA, ruleCode);
            if (string.IsNullOrEmpty(guid))
            {
                System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 在源库中未找到");
                return;
            }

            await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_BILLCODERULE", "GUID", guid, false, "[编码规则]");
            await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_BILLCODERULEDETAIL", "BILLCODERULEGUID", guid, false, "[编码规则]");

            System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[编码规则] {ruleCode} 复制失败：" + ex.Message);
        }
    }

    private async Task<bool> CodeRuleExistsInTargetHttpAsync(IDataAccess tgtDA, string code)
    {
        var sql = $@"SELECT COUNT(*) FROM dbo.S_BILLCODERULE WHERE CODE = '{ProxyHelper.EscapeSql(code)}'";
        var result = await ProxyHelper.ExecuteScalarAsync(tgtDA, sql);
        return Convert.ToInt32(result ?? 0) > 0;
    }

    private async Task<string?> GetCodeRuleGuidFromSourceHttpAsync(IDataAccess srcDA, string code)
    {
        var sql = $@"SELECT GUID FROM dbo.S_BILLCODERULE WHERE CODE = '{ProxyHelper.EscapeSql(code)}'";
        var result = await ProxyHelper.ExecuteScalarAsync(srcDA, sql);
        return result?.ToString();
    }

    private async Task CopyStandardQueriesForObjectHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string objectGuid)
    {
        try
        {
            var sql = $@"SELECT EXTENDS FROM dbo.S_CONTROL
                         WHERE OBJECTGUID = '{ProxyHelper.EscapeSql(objectGuid)}' AND (CONTROLTYPE = 'A3Text' OR CONTROLTYPE = 'GridColumn')";
            var dt = await ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql);
            var dataSelectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                var extends = row[0]?.ToString();
                var dict = ParseExtendsField(extends);
                if (dict.TryGetValue("DataSelectCode", out var dataSelectCode) && !string.IsNullOrWhiteSpace(dataSelectCode))
                {
                    dataSelectCodes.Add(dataSelectCode);
                }
            }

            foreach (var code in dataSelectCodes)
            {
                await CopyOneStandardQueryHttpAsync(srcDA, tgtDA, code);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[标准查询] 复制失败：" + ex.Message);
        }
    }

    private async Task CopyOneStandardQueryHttpAsync(IDataAccess srcDA, IDataAccess tgtDA, string code)
    {
        try
        {
            if (await StandardQueryExistsInTargetHttpAsync(tgtDA, code))
            {
                System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 目标库已存在，跳过");
                return;
            }

            // 检查源库是否存在
            var existsSql = $@"SELECT COUNT(*) FROM dbo.S_DATASELECT WHERE CODE = '{ProxyHelper.EscapeSql(code)}'";
            var countResult = await ProxyHelper.ExecuteScalarAsync(srcDA, existsSql);
            if (Convert.ToInt32(countResult ?? 0) == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 在源库中未找到");
                return;
            }

            await ProxyHelper.CopyTableDataByParentGuidAsync(srcDA, tgtDA, "S_DATASELECT", "CODE", code, false, "[标准查询]");
            System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 复制成功");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[标准查询] {code} 复制失败：" + ex.Message);
        }
    }

    private async Task<bool> StandardQueryExistsInTargetHttpAsync(IDataAccess tgtDA, string code)
    {
        var sql = $@"SELECT COUNT(*) FROM dbo.S_DATASELECT WHERE CODE = '{ProxyHelper.EscapeSql(code)}'";
        var result = await ProxyHelper.ExecuteScalarAsync(tgtDA, sql);
        return Convert.ToInt32(result ?? 0) > 0;
    }

}
