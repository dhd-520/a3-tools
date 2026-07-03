using A3Tools.Models;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL 查询主窗体（多 Tab 容器 + 顶栏 + 状态栏）。
/// Tab 内嵌 SqlQueryTabPage 用户控件（SQL 编辑器 + 结果 + 消息）。
/// 按账套单实例：每个账套最多一个本窗体。
/// </summary>
public partial class SqlQueryForm : Form
{
    private readonly Account _account;
    private string _baseConnStr = "";      // 服务器+账号+默认库
    private string _currentConnStr = "";   // 含当前选择的 InitialCatalog

    private int _untitledIndex = 0;

    /// <summary>LoadDatabasesAsync 运行中（初始化 / 刷新期间），避免 SelectedIndexChanged 覆盖用户手动选择</summary>
    private bool _loadingDbs = false;

    /// <summary>设计器无参构造（VS 加载设计时使用）。运行时走带参构造。</summary>
    public SqlQueryForm() : this(new Account { Code = "DESIGN", Name = "设计时", Database = "", DatabaseName = "", DbUser = "" })
    {
        // 设计器模式下不跑运行时逻辑
        if (DesignMode) return;
    }

    public SqlQueryForm(Account account)
    {
        _account = account;
        InitializeComponent();

        // 设计器模式下只呈现 UI，不跑业务逻辑
        if (DesignMode) return;

        BuildConnString();

        Text = $"SQL 查询 — {_account.Code} {_account.Name}";
        lblAccount.Text = $"账套: {_account.Code} {_account.Name}";
        lblServer.Text = $"服务器: {_account.Database}";
        lblConnInfo.Text = $"{_account.Database} / {_account.DatabaseName}";

        // 自定义绘制 Tab 标题（含 × 关闭按钮）
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.DrawItem += TabControl_DrawItem;
        tabControl.MouseDown += TabControl_MouseDown;

        // 默认开一个空 Tab
        NewTab();

        // 异步加载数据库列表
        _ = LoadDatabasesAsync();

        // 顶栏快捷键：Ctrl+N 新建查询，Ctrl+W 关闭当前，F5 执行，Ctrl+F5 执行选中
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { NewTab(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.W && tabControl.TabPages.Count > 0) { CloseActiveTab(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Tab) { SwitchToNextTab(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5 && !e.Shift) { GetActiveTab()?.PerformExecuteAll(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F5) { GetActiveTab()?.PerformExecuteSelected(); e.SuppressKeyPress = true; }
        };
    }

    // ============================================
    // 连接管理
    // ============================================

    private void BuildConnString()
    {
        // 账套密码可能是密文（工具箱源账套代入）或明文（账套列表选中传入，MainForm.LoadAndDecryptAccounts 已解密）。
        // 用 TryDecrypt 自动判断：密文→解密，明文→原样使用。
        var decryptedPwd = EncryptionService.TryDecrypt(_account.DbPassword ?? "");
        var b = new SqlConnectionStringBuilder
        {
            DataSource = _account.Database,
            InitialCatalog = _account.DatabaseName,
            UserID = _account.DbUser,
            Password = decryptedPwd,
            ConnectTimeout = 10,
            ApplicationName = "A3Tools.SqlQuery",
            TrustServerCertificate = true
        };
        _baseConnStr = b.ConnectionString;
        _currentConnStr = _baseConnStr;
    }

    public string CurrentConnectionString => _currentConnStr;

    private async Task LoadDatabasesAsync()
    {
        _loadingDbs = true;
        try
        {
            var dbs = new List<string>();
            using var conn = new SqlConnection(_baseConnStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) dbs.Add(r.GetString(0));

            // 保留用户旧选择；如果刷新后旧选择仍存在则恢复；只有从未选择过时才选账套默认库
            string? prevSelected = cmbDatabase.SelectedItem?.ToString();
            cmbDatabase.Items.Clear();
            cmbDatabase.Items.AddRange(dbs.ToArray());

            string target = !string.IsNullOrEmpty(prevSelected) && cmbDatabase.Items.Contains(prevSelected)
                ? prevSelected
                : _account.DatabaseName;
            var idx = cmbDatabase.Items.IndexOf(target);
            if (idx >= 0) cmbDatabase.SelectedIndex = idx;
            else if (cmbDatabase.Items.Count > 0) cmbDatabase.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "数据库列表加载失败";
            GetActiveTab()?.AppendMessage($"[警告] 数据库列表加载失败: {ex.Message}\n");
        }
        finally
        {
            _loadingDbs = false;
        }
    }

    private void CmbDatabase_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingDbs) return;  // 初始化 / 刷新期间不覆盖用户手动选择
        var db = cmbDatabase.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(db)) return;
        var b = new SqlConnectionStringBuilder(_baseConnStr) { InitialCatalog = db };
        _currentConnStr = b.ConnectionString;
        lblStatus.Text = $"已切换到 [{db}]（下次执行生效）";
        lblConnInfo.Text = $"{_account.Database} / {db}";
    }

    private void BtnRefreshDb_Click(object? sender, EventArgs e) => _ = LoadDatabasesAsync();

    private void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        foreach (TabPage page in tabControl.TabPages)
        {
            if (page.Tag is SqlQueryTabPage tab)
            {
                tab.ClearResults();
                tab.AppendMessage("[用户操作] 已断开（清除结果集与消息）\n");
            }
        }
        lblStatus.Text = "已断开";
    }

    // ============================================
    // Tab 管理
    // ============================================

    private SqlQueryTabPage NewTab(string? title = null, string? content = null)
    {
        _untitledIndex++;
        var page = new TabPage
        {
            Text = title ?? $"查询{_untitledIndex}",
            Padding = new Padding(2)
        };
        var tab = new SqlQueryTabPage(this)
        {
            Dock = DockStyle.Fill
        };
        tab.SetStatusReporter(UpdateStatus);
        page.Controls.Add(tab);
        page.Tag = tab;
        tabControl.TabPages.Add(page);
        tabControl.SelectedTab = page;
        if (!string.IsNullOrEmpty(content)) tab.SetEditorText(content);
        tab.Editor.Focus();
        return tab;
    }

    private void BtnNewTab_Click(object? sender, EventArgs e) => NewTab();

    private void BtnCloseCurrent_Click(object? sender, EventArgs e) => CloseActiveTab();

    private void CloseActiveTab()
    {
        if (tabControl.TabPages.Count <= 1)
        {
            // 最后一个：保留但清空
            if (tabControl.SelectedTab?.Tag is SqlQueryTabPage tab)
            {
                tab.ClearAll();
                tab.Editor.Focus();
            }
            return;
        }
        var idx = tabControl.SelectedIndex;
        if (idx < 0) return;
        tabControl.TabPages.RemoveAt(idx);
        if (tabControl.TabPages.Count > 0)
            tabControl.SelectedIndex = Math.Min(idx, tabControl.TabPages.Count - 1);
    }

    private void BtnCloseOthers_Click(object? sender, EventArgs e)
    {
        var keep = tabControl.SelectedTab;
        if (keep == null) return;
        for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
            if (!ReferenceEquals(tabControl.TabPages[i], keep))
                tabControl.TabPages.RemoveAt(i);
    }

    private void SwitchToNextTab()
    {
        if (tabControl.TabPages.Count <= 1) return;
        tabControl.SelectedIndex = (tabControl.SelectedIndex + 1) % tabControl.TabPages.Count;
    }

    // Designer 中右键菜单调用的方法
    private void MiClose_Click(object? sender, EventArgs e) => CloseActiveTab();
    private void MiRename_Click(object? sender, EventArgs e) => RenameActiveTab();

    private void RenameActiveTab()
    {
        if (tabControl.SelectedTab == null) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入新名称：", "重命名 Tab", tabControl.SelectedTab.Text);
        if (!string.IsNullOrWhiteSpace(name))
            tabControl.SelectedTab.Text = name.Trim();
    }

    private SqlQueryTabPage? GetActiveTab()
        => tabControl.SelectedTab?.Tag as SqlQueryTabPage;

    // ============================================
    // 自定义 Tab 标题绘制（带 × 关闭按钮）
    // ============================================

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var tab = tabControl.TabPages[e.Index];
        var bounds = e.Bounds;
        bool isSelected = (e.Index == tabControl.SelectedIndex);

        // 背景
        using var bg = new SolidBrush(isSelected ? Color.White : Color.FromArgb(240, 242, 245));
        e.Graphics.FillRectangle(bg, bounds);

        // 标题
        var titleRect = new Rectangle(bounds.Left + 6, bounds.Top + 4, bounds.Width - 24, bounds.Height - 8);
        using var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        using var titleBrush = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(80, 80, 80));
        e.Graphics.DrawString(tab.Text, Font, titleBrush, titleRect, sf);

        // × 按钮
        var closeRect = new Rectangle(bounds.Right - 18, bounds.Top + (bounds.Height - 14) / 2, 14, 14);
        using var closeBrush = new SolidBrush(Color.FromArgb(160, 160, 160));
        e.Graphics.DrawString("×", new Font(Font.FontFamily, 10F, FontStyle.Bold), closeBrush, closeRect.Location);
    }

    private void TabControl_MouseDown(object? sender, MouseEventArgs e)
    {
        // 点 × 关 Tab
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var r = tabControl.GetTabRect(i);
            var closeRect = new Rectangle(r.Right - 18, r.Top + (r.Height - 14) / 2, 14, 14);
            if (closeRect.Contains(e.Location))
            {
                tabControl.SelectedIndex = i;
                CloseActiveTab();
                return;
            }
            // 中键关 Tab
            if (e.Button == MouseButtons.Middle && r.Contains(e.Location))
            {
                tabControl.SelectedIndex = i;
                CloseActiveTab();
                return;
            }
        }
    }

    // ============================================
    // 穿透接口（外部调用，从「复制数据库对象」等工具穿透进来）
    // ============================================

    /// <summary>
    /// 打开对象脚本（类似 SSMS 的 ALTER 脚本）。
    /// 1. 切换库（如果指定且不在当前下拉）
    /// 2. 异步加载 CREATE/ALTER 脚本
    /// 3. 新建 Tab，标题 "{objType}.{objName}"，内容 = 脚本
    /// </summary>
    public void OpenScript(string database, string objType, string objName)
    {
        // 切换数据库（如果需要）
        if (!string.IsNullOrEmpty(database))
        {
            var idx = cmbDatabase.Items.IndexOf(database);
            if (idx >= 0) cmbDatabase.SelectedIndex = idx;
            else
            {
                // 不在下拉里（可能是 tempdb / 用户没权限看到的库），临时切
                var b = new SqlConnectionStringBuilder(_baseConnStr) { InitialCatalog = database };
                _currentConnStr = b.ConnectionString;
                lblConnInfo.Text = $"{_account.Database} / {database}";
            }
        }
        _ = LoadAndOpenScriptAsync(objType, objName);
    }

    private async Task LoadAndOpenScriptAsync(string objType, string objName)
    {
        try
        {
            lblStatus.Text = $"加载 {objType}.{objName} ...";
            var script = await SqlScriptLoader.LoadCreateScriptAsync(_currentConnStr, objType, objName);
            BeginInvoke(() =>
            {
                var tab = NewTab($"{objType}.{objName}", script ?? $"-- 加载 {objType}.{objName} 失败");
                tabControl.SelectedTab = tab.Page;
                tab.Editor.Focus();
                lblStatus.Text = $"已加载 {objType}.{objName}";
            });
        }
        catch (Exception ex)
        {
            BeginInvoke(() =>
            {
                lblStatus.Text = $"加载 {objType}.{objName} 失败";
                GetActiveTab()?.AppendMessage($"[错误] 加载脚本失败: {ex.Message}\n");
            });
        }
    }

    // ============================================
    // 状态回调（由 SqlQueryTabPage 调用）
    // ============================================

    public void UpdateStatus(string status, long elapsedMs, int affectedRows)
    {
        lblStatus.Text = status;
        lblElapsed.Text = $"耗时: {elapsedMs} ms";
        lblRows.Text = $"影响: {affectedRows} 行";
    }
}