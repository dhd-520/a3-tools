using System.Data;
using A3Tools.Models;
using A3Tools.Plugins.Default.Excel;
using A3Tools.Services;
using A3Tools.Common.Services;
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
    private ObjectExplorerForm? _explorer;   // 单例：与主窗体绑定：打开一次 → 关闭窗口 → 隐藏
    private bool _explorerUserClosed = false; // 用户点击 × 后不再默认打开，需点工具栏重开
    private bool _explorerVisible = false;    // 工具栏 toggle 状态
    private int _explorerWidth = 360;         // Explorer 实际宽度（Show 后从 _explorer.Width 更新）

    // ★ 2026-10-15 Direct 模式会话连接 (长连接): #temp 临时表必须挂在该连接上才会话级可见
    //   所有 Tab 通过 GetSessionConnAsync() 拿这个连接 (而不是 new SqlConnection),
    //   这样 #Stock / #Price 等多 Tab 共享同一会话, JOIN / 聚合 / 子查询天然支持
    //   关闭 Form 时 Dispose → #temp 自动随连接销毁
    private SqlConnection? _sessionConn;
    private readonly object _sessionConnLock = new();
    private readonly HashSet<string> _tempTables = new(StringComparer.OrdinalIgnoreCase);

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

        // 陛下反馈：默认最大化打开；后续用户手动调过 → 按用户上次状态。
        // 优先级：SQL 启动都最大化（默认） → 读 settings 有上次状态 → 用上次的。
        // - settings.SqlQueryFormMaximized=true → 最大化（默认）
        // - false 且 width/height > 0 → 用上次大小（手动还原过的）
        // - false 且 width=0 → 未手动调过（第一次走默认 Maximized）
        if (SettingsStore.ReadBool("SqlQueryFormMaximized", true))
        {
            WindowState = FormWindowState.Maximized;
            // 最大化也需记住在哪个屏的 WorkArea 上大（2026-07-07）
            int screenHashMaximized = SettingsStore.ReadInt("SqlQueryFormScreenHash", 0);
            if (screenHashMaximized != 0)
            {
                var targetScreen = FindScreenByHash(screenHashMaximized);
                if (targetScreen != null)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(targetScreen.WorkingArea.X, targetScreen.WorkingArea.Y);
                }
            }
        }
        else
        {
            int w = SettingsStore.ReadInt("SqlQueryFormWidth", 0);
            int h = SettingsStore.ReadInt("SqlQueryFormHeight", 0);
            int left = SettingsStore.ReadInt("SqlQueryFormLeft", -1);
            int top = SettingsStore.ReadInt("SqlQueryFormTop", -1);
            int screenHash = SettingsStore.ReadInt("SqlQueryFormScreenHash", 0);
            if (w > 0 && h > 0)
            {
                ClientSize = new Size(w, h);
                if (left >= 0 && top >= 0)
                {
                    // 陛下需求：上次在副屏最大化 → 下次还到副屏最大化。
                    // 先按 screenHash 找上次那块屏幕，验证 [left, top, w, h] 是否在那块屏的 WorkArea 里。
                    // 【边界情况】如果上次的屏拔走了（FindScreenByHash 返回 null），
                    // 就算 saved bounds 刚好落在主屏区域也不能用 ——那是错位重事后的点，不是用户主动选的。
                    var targetScreen = screenHash != 0 ? FindScreenByHash(screenHash) : null;
                    if (targetScreen != null && targetScreen.WorkingArea.Contains(new Rectangle(left, top, w, h)))
                    {
                        StartPosition = FormStartPosition.Manual;
                        Location = new Point(left, top);
                    }
                    else
                    {
                        // 上次屏拔走 或 saved bounds 已不在任何屏 → 退回到主屏中心
                        StartPosition = FormStartPosition.CenterScreen;
                    }
                }
                else
                {
                    StartPosition = FormStartPosition.CenterScreen;
                }
                WindowState = FormWindowState.Normal;
            }
            else
            {
                // 第一次启动且未指定 Maximized → 保持默认（最大化为宜）
                WindowState = FormWindowState.Maximized;
            }
        }

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

        // Http 模式：注入 IDataAccess 给 SchemaCache，让对象资源管理器/IntelliSense 走代理
        SqlObjectSchemaCache.SetDataAccess(CurrentDataAccess);
        SqlScriptLoader.SetDataAccess(CurrentDataAccess);

        // 后台预热当前库的 IntelliSense 缓存（不阻塞 UI；切库时也会再拉）
        // GetSuggestions 入口会同步 EnsureLoadedSync 保证后续读到了可用
        _ = SqlObjectSchemaCache.WarmupAsync(_currentConnStr);

        // 顶栏快捷键：Ctrl+N 新建查询，Ctrl+W 关闭当前，F5 / Ctrl+F5 智能执行（SSMS 风格：有选中执行选中，否则执行全部）
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { NewTab(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.W && tabControl.TabPages.Count > 0) { CloseActiveTab(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Tab) { SwitchToNextTab(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F5 && !e.Shift) { GetActiveTab()?.PerformExecuteSmart(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F5) { GetActiveTab()?.PerformExecuteSmart(); e.SuppressKeyPress = true; }
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

    /// <summary>
    /// 2026-07-09 混合连接：按账套 ConnectionMode 返回 Direct 或 Http 实现。
    /// 直连账号仍用 _currentConnStr；Http 账号会把连接串加密后送到 A3ToolsHub。
    /// </summary>
    public A3Tools.Common.DataAccess.IDataAccess CurrentDataAccess
    {
        get
        {
            if (_currentDataAccess != null) return _currentDataAccess;

            // 从 SqlConnectionStringBuilder 拼原始 connStr（HttpDataAccess 需要）
            // Http 模式下这个 connStr 是账套数据库的原始连接串（不在 A3ToolsHub 所在服务器上的）
            var rawConnStr = BuildHttpConnStr();

            if (_account.ConnectionMode == A3Tools.Common.DataAccess.DataAccessMode.Http)
            {
                _currentDataAccess = new A3Tools.Common.DataAccess.HttpDataAccess(
                    endpoint: _account.HttpEndpoint,
                    connStr: rawConnStr,
                    secretKey: _account.HttpSecretKey,
                    serverPublicKey: _account.HttpServerPublicKey,
                    displayName: _account.Name);
            }
            else
            {
                _currentDataAccess = new A3Tools.Common.DataAccess.DirectDataAccess(
                    connStr: rawConnStr,
                    displayName: _account.Name);
            }
            return _currentDataAccess;
        }
    }
    private A3Tools.Common.DataAccess.IDataAccess? _currentDataAccess = null;

    /// <summary>
    /// 从 _currentConnStr 重建原始连接串（HttpDataAccess 需要原始 connStr，不是 SqlConnectionStringBuilder 转译过的）
    /// </summary>
    private string BuildHttpConnStr()
    {
        // 优先用账套 ConnectionString 属性（拼接好的），否则退回到 _currentConnStr
        var raw = _account.ConnectionString;
        if (!string.IsNullOrEmpty(raw)) return raw;
        return _currentConnStr;
    }

    /// <summary>
    /// 把当前账套的 ConnectionString 同步给所有 Tab 编辑器 → IntelliSense 自动切到当前库。
    /// NewTab / LoadDatabasesAsync / CmbDatabase_SelectedIndexChanged 都要调。
    /// </summary>
    private void SyncEditorConnectionStrings()
    {
        foreach (TabPage page in tabControl.TabPages)
            if (page.Tag is SqlQueryTabPage tab)
                tab.Editor.ConnectionString = _currentConnStr;
    }

    private async Task LoadDatabasesAsync()
    {
        _loadingDbs = true;
        try
        {
            var dbs = new List<string>();
            // 2026-07-10 Http 模式下走代理查 sys.databases
            if (_account.ConnectionMode == A3Tools.Common.DataAccess.DataAccessMode.Http)
            {
                var dbResult = await CurrentDataAccess.ExecuteQueryAsync("SELECT name FROM sys.databases WHERE state = 0 ORDER BY name");
                if (dbResult.Success && dbResult.Tables.Count > 0)
                {
                    foreach (var row in dbResult.Tables[0].Rows)
                    {
                        var dbName = row[0]?.ToString();
                        if (!string.IsNullOrEmpty(dbName))
                            dbs.Add(dbName);
                    }
                }

                string? prevSelected2 = cmbDatabase.SelectedItem?.ToString();
                cmbDatabase.Items.Clear();
                cmbDatabase.Items.AddRange(dbs.ToArray());

                string target2 = !string.IsNullOrEmpty(prevSelected2) && cmbDatabase.Items.Contains(prevSelected2)
                    ? prevSelected2
                    : _account.DatabaseName;
                var idx2 = cmbDatabase.Items.IndexOf(target2);
                if (idx2 >= 0) cmbDatabase.SelectedIndex = idx2;
                else if (cmbDatabase.Items.Count > 0) cmbDatabase.SelectedIndex = 0;
                return;
            }
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
            // 数据库列表刚加载完，可能已 auto 选定了具体库；
            // 此时把 ConnectionString 同步到所有编辑器 + 启动一次 IntelliSense 缓存预热
            SyncEditorConnectionStrings();
            _ = SqlObjectSchemaCache.WarmupAsync(_currentConnStr);

            // Explorer 已打开 → 自动刷新
            if (_explorer != null && !_explorer.IsDisposed && _explorer.Visible)
                _ = _explorer.RefreshAsync();
        }
    }

    // ============================================
    // 对象资源管理器
    // ============================================

    /// <summary>
    /// 切换 Explorer 显示状态（带 CheckOnClick=true 的按钮）。
    /// 首次打开：从 Owner 右侧弹出；后续 Show/Hide 切换；用户关掉 × 后需按钮重启。
    /// </summary>
    private void BtnToggleExplorer_Click(object? sender, EventArgs e)
    {
        if (_explorer != null && !_explorer.IsDisposed)
        {
            if (_explorerVisible)
            {
                _explorer.Hide();
                _explorerVisible = false;
                if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 对象资源管理器";
            }
            else
            {
                // ★ 关键：Show 之前重新算位置 —— 解决"创建时位置对、最大化后又错"
                var newLoc = ComputeExplorerLocation();
                var newH = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8);
                _explorer.Show();
                _explorerWidth = _explorer.Width;  // 用实际宽度（含边框）
                _explorer.Size = new Size(_explorerWidth, newH);
                // ★ FixLocation 锁定位置（绕过 WinForms Owner 内部纠正），Z-order 由 Owner 关系自动保证
                _explorer.FixLocation(new Point(this.Right - _explorerWidth, newLoc.Y));
                _ = _explorer.RefreshAsync();
                _explorerVisible = true;
                if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 关闭资源管理器";
            }
            return;
        }

        // 创建新的 Explorer
        var newLoc2 = ComputeExplorerLocation();
        var newH2 = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8);
        _explorer = new ObjectExplorerForm(this)
        {
            StartPosition = FormStartPosition.Manual,
            // Owner 已在 ObjectExplorerForm 构造里设了 = this（保证 Z-order 在主窗体之前）
        };
        _explorer.FormClosed += (_, args) =>
        {
            _explorerVisible = false;
            if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 对象资源管理器";
            if (args.CloseReason == CloseReason.UserClosing)
                _explorerUserClosed = true;
            else
                _explorerUserClosed = false;
            _explorer = null;
        };
        _explorer.Show();
        // ★ 先 Show 拿到实际 Width（含边框）
        _explorerWidth = _explorer.Width;     // 实际含边框的宽
        _explorer.Size = new Size(_explorerWidth, newH2);
        // ★ 再根据实际宽重新计算位置 + FixLocation 锁定
        newLoc2 = new Point(this.Right - _explorerWidth, newLoc2.Y);
        _explorer.FixLocation(newLoc2);
        _ = _explorer.RefreshAsync();
        _explorerVisible = true;
        if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 关闭资源管理器";
        _explorerUserClosed = false;
    }

    // Win32 P/Invoke
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>主窗体尺寸/位置变化 → Explorer 跟随调整位置（防"位置错"）</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateExplorerBounds();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        UpdateExplorerBounds();
    }

    /// <summary>
    /// 主窗体激活（点击主窗体编辑 / Alt+Tab 回来 / 切回应用）时强制把 Explorer 放到主窗体之前。
    /// Form.Owner 关系已经保证 Z-order 在前（Windows 自动维护），这里只是 FixLocation 兜底位置。
    /// </summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        BringExplorerAboveMe();
    }

    private void BringExplorerAboveMe()
    {
        if (_explorer == null || _explorer.IsDisposed || !_explorerVisible) return;
        if (WindowState == FormWindowState.Minimized) return;
        // Z-order 由 Form.Owner 关系保证；这里只 FixLocation 兜底位置（万一 WinForms 把 Explorer 动走了）
        _explorer.FixLocation(ComputeExplorerLocation());
    }

    private void UpdateExplorerBounds()
    {
        if (_explorer == null || _explorer.IsDisposed || !_explorerVisible) return;
        if (WindowState == FormWindowState.Minimized) return;
        var loc = ComputeExplorerLocation();
        var h = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8);
        _explorerWidth = _explorer.Width;
        _explorer.Size = new Size(_explorerWidth, h);
        _explorer.FixLocation(new Point(this.Right - _explorerWidth, loc.Y));
    }

    /// <summary>窗体关闭中：记住当前大小/位置/最大化状态，下次启动按这个开（2026-07-06）
    /// 陛下反馈：主副屏位置也要记住！需要存 Screen.DeviceName（设备名）</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            // WindowState=Minimized → RestoreBounds 才是正常大小；否则 Bounds 才是。
            // 但用户主动最大化 → 存 Maximized=true，下次启动还是最大化。
            // 用户主动还原成普通窗体 → 存 Normal + 当前 Bounds。
            bool isMaximized = WindowState == FormWindowState.Maximized;
            // 当前所在屏幕的设备名 hash code（多屏环境下区分主副屏；SettingsStore 只有 int/bool 用 int 存 hash）
            string screenDevice = Screen.FromControl(this).DeviceName ?? "";
            int screenHash = string.IsNullOrEmpty(screenDevice) ? 0 : screenDevice.GetHashCode();
            SettingsStore.Update(d =>
            {
                d["SqlQueryFormMaximized"] = isMaximized;
                // RestoreBounds 在 Normal 下等于 Bounds；在 Maximized 下等于还原后的大小
                var bounds = isMaximized ? RestoreBounds : Bounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    d["SqlQueryFormWidth"] = bounds.Width;
                    d["SqlQueryFormHeight"] = bounds.Height;
                    d["SqlQueryFormLeft"] = bounds.Left;
                    d["SqlQueryFormTop"] = bounds.Top;
                }
                d["SqlQueryFormScreenHash"] = screenHash;
            });
        }
        catch { /* 保存失败不影响关闭 */ }
        base.OnFormClosing(e);
    }

    /// <summary>父窗体关闭 → Explorer 一起关（避免孤儿窗口）</summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_explorer != null && !_explorer.IsDisposed)
        {
            try { _explorer.Dispose();_explorer.Close();  } catch { /* may already closed */ }
            _explorer = null;
        }

        // ★ 2026-10-15 v2: 关掉 #temp 会话连接 → #Stock / #Price 等临时表随连接销毁
        if (_sessionConn != null)
        {
            try
            {
                if (_sessionConn.State == ConnectionState.Open)
                    _sessionConn.Close();
                _sessionConn.Dispose();
            }
            catch { /* may already disposed */ }
            _sessionConn = null;
        }
        _tempTables.Clear();

        base.OnFormClosed(e);
    }
    /// <summary>获取主窗体所在的屏幕 WorkArea（多屏支持）</summary>
    private Rectangle GetScreenWorkArea()
        => Screen.FromControl(this).WorkingArea;

    /// <summary>按 DeviceName 的 hash code 查找屏（存为 int）；找不到返回 null（2026-07-07）</summary>
    private static Screen? FindScreenByHash(int hash)
    {
        if (hash == 0) return null;
        foreach (var s in Screen.AllScreens)
        {
            var name = s.DeviceName ?? "";
            if (!string.IsNullOrEmpty(name) && name.GetHashCode() == hash)
                return s;
        }
        return null;
    }

    /// <summary>
    /// Explorer 位置 — 与主窗体右边对齐（【陛下决定】）。
    ///
    /// 公式：x = this.Right - 336（含边框的窗体实际宽）
    ///      y = this.Top（贴顶）夹到 wa 内
    /// </summary>
    private Point ComputeExplorerLocation()
    {
        var wa = GetScreenWorkArea();

        // ★ 用 Explorer 实际宽度（不是 const 336）—— 以防用户调整过 Explorer 大小
        int explorerWidth = _explorer?.Width ?? _explorerWidth;

        int x = this.Right - explorerWidth;
        if (x + explorerWidth > wa.Right)
            x = wa.Right - explorerWidth;
        if (x < wa.Left)
            x = wa.Left;

        int y = this.Top;
        if (y < wa.Top) y = wa.Top;
        if (y + 200 > wa.Bottom) y = Math.Max(wa.Top, wa.Bottom - 600);

        return new Point(x, y);
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

        // 切库后立刻把所有 Tab 编辑器的 ConnectionString 同步过去 → IntelliSense 自动切到新库
        SyncEditorConnectionStrings();

        // 后台预热新库的缓存（同 (server, db) 第二次只等结果；切了再切回来也只拉一次）
        _ = SqlObjectSchemaCache.WarmupAsync(_currentConnStr);

        // Explorer 已打开 → 刷新
        if (_explorer != null && !_explorer.IsDisposed && _explorer.Visible)
            _ = _explorer.RefreshAsync();
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
            Dock = DockStyle.Fill,
            Page = page   // ★ 重要：让 tab.Page 指向 TabPage，Supply 脚本加载完成后能更新 Tab.Text
        };
        tab.Editor.ConnectionString = _currentConnStr;  // 让 IntelliSense 知道当前库
        tab.SetStatusReporter(UpdateStatus);
        page.Controls.Add(tab);
        page.Tag = tab;
        tabControl.TabPages.Add(page);
        tabControl.SelectedTab = page;
        // 订阅字号改变 → 状态栏刷新（2026-07-07）
        tab.FontSizeChanged += (s, e) => UpdateFontSizeLabel(tab);
        // 新 Tab 也要立刻同步当前字号
        UpdateFontSizeLabel(tab);
        if (!string.IsNullOrEmpty(content)) tab.SetEditorText(content);
        tab.Editor.Focus();
        return tab;
    }

    /// <summary>显示当前 Tab 编辑器的字号到状态栏（2026-07-07）</summary>
    private void UpdateFontSizeLabel(SqlQueryTabPage tab)
    {
        var editor = tab?.Editor;
        if (editor == null || IsDisposed) return;
        var size = editor.CurrentFontSize;
        if (lblFontSize != null) lblFontSize.Text = $"字号: {size:0.#}pt";
    }

    private void BtnNewTab_Click(object? sender, EventArgs e) => NewTab();

    /// <summary>
    /// ★ 2026-10-15 v2: 导入 Excel 到 #temp 真实临时表 (Direct 模式走长连接 + CREATE TABLE + SqlBulkCopy)
    /// JOIN / 聚合 / 子查询 天然由 SQL Server 处理
    /// Http 模式: CREATE TABLE + INSERT 拼到每次 Execute 的 batch 前缀
    /// </summary>
    private async void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        // 1. 选文件
        string? filePath = null;
        using (var ofd = new OpenFileDialog
        {
            Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls|所有文件 (*.*)|*.*",
            Title = "选择要导入的 Excel"
        })
        {
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            filePath = ofd.FileName;
        }

        // 2. 弹出导入 UI
        TempTableImportRequest? request;
        try
        {
            using var form = new ExcelImportForm(filePath);
            if (form.ShowDialog(this) != DialogResult.OK) return;
            request = form.Result;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打开 Excel 失败:\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (request is null) return;

        // 3. 确认: 已存在 #temp 提示覆盖
        if (_tempTables.Contains(request.Alias))
        {
            var confirm = MessageBox.Show(this,
                $"别名 #{request.Alias} 已存在。\n\n点 [是] 覆盖现有临时表, [否] 取消导入。",
                "别名已存在", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
        }

        // ★ 2026-10-15 v2.2 陛下反馈修复: 导入无 loading + 慢
        //   1. UseWaitCursor + 状态栏"正在导入..." 即时反馈
        //   2. 解析 RawRows 丢后台线程 (不卡 UI)
        //   3. SqlBulkCopy 批量拷贝 (TDS Bulk Load 协议, 万行 <1s, 比 INSERT 快一个数量级)
        UseWaitCursor = true;
        btnImportExcel.Enabled = false;
        lblStatus.Text = $"⏳ 正在导入 #{request.Alias}: 解析数据...";
        lblStatus.ForeColor = Color.DarkBlue;
        lblStatus.BackColor = Color.LightYellow;
        statusStrip.Refresh();
        try
        {
        // 4. 解析 RawRows → 实际类型 (后台线程, 不卡 UI)
        var parseResult = await Task.Run(() =>
        {
            var rows = new List<object?[]>(request.RawRows.Count);
            int nulls = 0;
            foreach (var rawRow in request.RawRows)
            {
                var arr = new object?[request.Columns.Count];
                for (int i = 0; i < request.Columns.Count; i++)
                {
                    var v = rawRow[i] as string;
                    var parsed = TempTableValueParser.Parse(v, request.Columns[i].TypeName);
                    if (parsed is null) nulls++;
                    arr[i] = parsed ?? DBNull.Value;
                }
                rows.Add(arr);
            }
            return (rows, nulls);
        });
        var parsedRows = parseResult.rows;
        int nullCount = parseResult.nulls;

        // 5. 拼接 DDL + INSERT
        var alias = request.Alias;
        var safeName = "#" + alias;
        var columnDefs = new List<string>(request.Columns.Count);
        foreach (var c in request.Columns)
        {
            columnDefs.Add($"[{c.TargetName}] {MapToSqlType(c.TypeName)} NULL");
        }
        var ddl = $"CREATE TABLE {safeName} (\n  {string.Join(",\n  ", columnDefs)}\n)";

        // 6. Direct 模式: 走长连接
        if (CurrentDataAccess.Mode == A3Tools.Common.DataAccess.DataAccessMode.Direct)
        {
            try
            {
                lblStatus.Text = $"⏳ 正在导入 #{request.Alias}: 写入数据库...";
                statusStrip.Refresh();

                var conn = await GetSessionConnAsync();
                using var tx = conn.BeginTransaction();
                using (var ddlCmd = new SqlCommand($"IF OBJECT_ID('tempdb..{safeName}') IS NOT NULL DROP TABLE {safeName}; {ddl}", conn, tx))
                {
                    await ddlCmd.ExecuteNonQueryAsync();
                }

                // ★ 2026-10-15 v2.3 陛下建议改造: SqlBulkCopy 批量拷贝 (TDS Bulk Load 协议)
                //   10 万行 <1s, 比批量 INSERT 快一个数量级
                //   与 CREATE TABLE 同一事务, 失败一起回滚
                int colCount = request.Columns.Count;

                // 1. 构建 DataTable (列类型 = CLR 类型, SqlBulkCopy 靠它生成 TDS 元数据)
                var dt = new DataTable();
                foreach (var c in request.Columns)
                {
                    dt.Columns.Add(c.TargetName, MapToClrType(c.TypeName));
                }
                foreach (var row in parsedRows)
                {
                    var dr = dt.NewRow();
                    for (int i = 0; i < colCount; i++)
                    {
                        dr[i] = row[i] ?? DBNull.Value;
                    }
                    dt.Rows.Add(dr);
                }

                // 2. SqlBulkCopy 写入 #temp (同连接同事务)
                using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx))
                {
                    bulk.DestinationTableName = safeName;
                    for (int i = 0; i < colCount; i++)
                    {
                        bulk.ColumnMappings.Add(request.Columns[i].TargetName, request.Columns[i].TargetName);
                    }
                    bulk.BulkCopyTimeout = 300;
                    bulk.NotifyAfter = Math.Max(1, parsedRows.Count / 20);
                    bulk.SqlRowsCopied += (_, e2) =>
                    {
                        // SqlRowsCopied 在后台线程触发 → BeginInvoke 回 UI 线程
                        // ★ Invoke 防御: IsHandleCreated && !IsDisposed (全局规则)
                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke(new Action(() =>
                                lblStatus.Text = $"⏳ 正在写入 #{alias}: {e2.RowsCopied}/{parsedRows.Count} 行..."));
                        }
                    };
                    await bulk.WriteToServerAsync(dt);
                }
                tx.Commit();
                _tempTables.Add(alias);

                // ★ 2026-10-15 v2.1 成功提示改造 (陛下反馈: 导入后没提示 + 不知道怎么用)
                //   1. 状态栏绿底大字持久显示
                //   2. 自动给当前 Tab 编辑器插入 SELECT 模板 (按 F5 直接跑)
                //   3. 弹教学 MessageBox 说明 #别名 怎么用 (含 JOIN / 聚合示例)
                var nullHint = nullCount > 0 ? $"（{nullCount} 个 NULL）" : "";

                // 1. 状态栏绿底大字
                lblStatus.Text = $"✓ 已导入 #{alias}: {parsedRows.Count} 行 × {request.Columns.Count} 列 {nullHint}  |  来源: {Path.GetFileName(request.SourceFile)} / {request.SheetName}";
                lblStatus.ForeColor = Color.White;
                lblStatus.BackColor = Color.SeaGreen;
                var statusResetTimer = new System.Windows.Forms.Timer { Interval = 15000 };
                statusResetTimer.Tick += (_, _) =>
                {
                    statusResetTimer.Stop();
                    statusResetTimer.Dispose();
                    lblStatus.ForeColor = SystemColors.ControlText;
                    lblStatus.BackColor = SystemColors.Control;
                };
                statusResetTimer.Start();

                // 2. 自动插 SELECT 模板到当前 Tab 编辑器 (空编辑器替换, 有内容追加)
                var template =
                    $"SELECT TOP 100 *\nFROM #{alias};  -- 导入成功, 按 F5 执行\n" +
                    $"-- 示例 JOIN: SELECT a.*, b.* FROM #{alias} a JOIN dbo.S_SCM_ITEM b ON a.列 = b.列;";
                GetActiveTab()?.InsertSqlTemplate(template);

                // 3. 教学 MessageBox
                MessageBox.Show(this,
                    $"✓ 已导入临时表 #{alias}\n\n" +
                    $"数据: {parsedRows.Count} 行 × {request.Columns.Count} 列{nullHint}\n" +
                    $"来源: {Path.GetFileName(request.SourceFile)} / {request.SheetName}\n\n" +
                    $"━━━━━━━━━━━━ 使用方法 ━━━━━━━━━━━━\n\n" +
                    $"#别名 就像一张普通表, SQL 里直接用:\n\n" +
                    $"  SELECT * FROM #{alias};\n\n" +
                    $"  SELECT a.*, b.*\n" +
                    $"  FROM #{alias} a\n" +
                    $"  JOIN dbo.真实表 b ON a.列 = b.列;\n\n" +
                    $"  SELECT 分类列, COUNT(*), SUM(数值列)\n" +
                    $"  FROM #{alias} GROUP BY 分类列;\n\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    $"已在编辑器插入查询模板, 按 F5 执行!\n" +
                    $"关闭本窗体时 #{alias} 自动清理。",
                    "导入成功 + 使用说明",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"建 #temp 表失败:\n{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            // Http 模式: 拼接 prefix, 后续 Execute 前置
            // ★ v2 简化: Http 模式临时表暂未完整支持, 提示用户切直连或后续支持
            MessageBox.Show(this,
                "当前账套是 Http 代理模式, #temp 临时表暂未完整支持。\n\n" +
                "建议: 切到直连账套 或 后续版本补 Http 模式 session 持久化。\n\n" +
                "本导入的数据已解析, 但未建到任何 SQL Server 表, 关闭窗口即丢。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        }
        finally
        {
            // ★ v2.2 loading 收尾: 无论成败都恢复光标 + 按钮
            UseWaitCursor = false;
            btnImportExcel.Enabled = true;
        }
    }

    /// <summary>
    /// ★ 2026-10-15 v2.3: SQL 类型名 → CLR Type (SqlBulkCopy 的 DataTable 列类型)
    /// 与 TempTableValueParser.Parse 返回的类型一致, 否则 DataRow 赋值会炸
    /// </summary>
    private static Type MapToClrType(string excelTypeName)
    {
        var t = (excelTypeName ?? "NVARCHAR").Trim().ToUpperInvariant();
        return t switch
        {
            "INT" or "INTEGER" or "BIGINT" or "SMALLINT" or "TINYINT" => typeof(long),
            "DECIMAL" or "NUMERIC" or "MONEY" or "SMALLMONEY" or "FLOAT" or "REAL" => typeof(decimal),
            "DATETIME" or "DATETIME2" or "SMALLDATETIME" or "TIMESTAMP" or "DATE" => typeof(DateTime),
            "TIME" => typeof(TimeSpan),
            "BIT" or "BOOLEAN" => typeof(bool),
            "UNIQUEIDENTIFIER" => typeof(Guid),
            "BINARY" or "VARBINARY" or "IMAGE" => typeof(byte[]),
            _ => typeof(string)
        };
    }

    /// <summary>
    /// ★ 2026-10-15 SQL Server 短类型名 (Excel 推断) → DDL 类型名
    /// </summary>
    private static string MapToSqlType(string excelTypeName)
    {
        var t = (excelTypeName ?? "NVARCHAR").Trim().ToUpperInvariant();
        return t switch
        {
            "INT" or "INTEGER" or "BIGINT" or "SMALLINT" or "TINYINT" => "BIGINT",
            "DECIMAL" or "NUMERIC" or "MONEY" or "SMALLMONEY" => "DECIMAL(28,8)",
            "FLOAT" or "REAL" => "FLOAT",
            "DATETIME" or "DATETIME2" or "TIMESTAMP" or "SMALLDATETIME" => "DATETIME",
            "DATE" => "DATE",
            "TIME" => "TIME",
            "BIT" or "BOOLEAN" => "BIT",
            "UNIQUEIDENTIFIER" => "UNIQUEIDENTIFIER",
            _ => "NVARCHAR(MAX)" // 默认 NVARCHAR(MAX) 兼容所有字符串场景
        };
    }

    /// <summary>
    /// ★ 2026-10-15 v2: 拿会话级 SqlConnection (Direct 模式)
    /// 所有 Tab 共享这一根连接 → #temp 跨 Tab 可见 → JOIN / 聚合天然支持
    /// </summary>
    public async Task<SqlConnection> GetSessionConnAsync()
    {
        lock (_sessionConnLock)
        {
            if (_sessionConn != null && _sessionConn.State == ConnectionState.Open)
                return _sessionConn;
        }
        var newConn = new SqlConnection(CurrentConnectionString);
        await newConn.OpenAsync();
        lock (_sessionConnLock)
        {
            if (_sessionConn != null && _sessionConn.State == ConnectionState.Open)
            {
                // 另一个线程刚开了一个, 关掉我的
                newConn.Dispose();
                return _sessionConn;
            }
            _sessionConn = newConn;
            return _sessionConn;
        }
    }

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

        // 标题：单行 + 超出显示 "..."（避免长名换行使页签变高）
        var titleRect = new Rectangle(bounds.Left + 6, bounds.Top + 4, bounds.Width - 24, bounds.Height - 8);
        using var sf = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var titleBrush = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(80, 80, 80));
        e.Graphics.DrawString(tab.Text, Font, titleBrush, titleRect, sf);

        // × 按钮：水平+垂直居中（在 closeRect 内）
        var closeRect = new Rectangle(bounds.Right - 18, bounds.Top, 14, bounds.Height);
        using var cf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        using var closeBrush = new SolidBrush(Color.FromArgb(160, 160, 160));
        e.Graphics.DrawString("×", new Font(Font.FontFamily, 10F, FontStyle.Bold), closeBrush, closeRect, cf);
    }

    private void TabControl_MouseDown(object? sender, MouseEventArgs e)
    {
        // 点 × 关 Tab
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var r = tabControl.GetTabRect(i);
            var closeRect = new Rectangle(r.Right - 18, r.Top, 14, r.Height);
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
    /// 2. 立即同步建占位 Tab + 切到 SelectedTab + Focus Editor + Activate 主窗体
    /// 3. 异步加载 CREATE/ALTER 脚本，加载完成后填充 Tab
    /// 这样做让 ObjectExplorer 双击后用户**立即看到反馈**（Tab 切换+占位文本），而不是像陛下面临的“看起来卡”其实是 await 期间看不到任何变化。
    /// </summary>
    public void OpenScript(string database, string objType, string objName)
    {
        // 1. 切换数据库（如果需要）
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

        // 2. 立即同步建占位 Tab（不依赖 SqlConnection IO）。这是修复“看起来卡”的关键。
        var loadingTitle = $"{objType}.{objName} (加载中…)";
        var loadingContent = $"-- 加载 {objType}.{objName} 中…\r\n-- （SQL Server 连接 + sys.sql_modules 查询可能在 1-3 秒）";
        var tab = NewTab(loadingTitle, loadingContent);

        // 3. 主动拉主窗体到 Z-order 顶 + Focus 到编辑器（防止 Explorer 抢焦）
        Activate();
        tab.Editor.Focus();

        // 4. 异步加载脚本，填充 Tab
        _ = LoadAndFillScriptAsync(tab, objType, objName);
    }

    /// <summary>
    /// F12 转到定义：取光标处的词，从缓存中匹配数据库对象，调 OpenScript 打开。
    /// 等同于 Explorer 双击。
    /// </summary>
    public void GoToDefinition()
    {
        var tab = GetActiveTab();
        if (tab == null) return;

        string word = tab.Editor.GetWordAtCursor();
        if (string.IsNullOrEmpty(word)) return;

        // 去掉方括号包裹：[dbo].[S_SCM_SEORDER] -> dbo.S_SCM_SEORDER
        string clean = word.Replace("[", "").Replace("]", "").Trim();
        if (string.IsNullOrEmpty(clean)) return;

        string? schema = null;
        string name;
        if (clean.Contains('.'))
        {
            var parts = clean.Split('.');
            schema = parts[0];
            name = parts.Length > 1 ? parts[1] : "";
        }
        else
        {
            name = clean;
        }
        if (string.IsNullOrEmpty(name)) return;

        if (string.IsNullOrEmpty(_currentConnStr)) return;

        var objs = SqlObjectSchemaCache.GetObjectsByKind(_currentConnStr,
            new[] {
                SqlObjectSchemaCache.ObjectKind.Table,
                SqlObjectSchemaCache.ObjectKind.View,
                SqlObjectSchemaCache.ObjectKind.TableValuedFunction,
                SqlObjectSchemaCache.ObjectKind.ScalarFunction,
                SqlObjectSchemaCache.ObjectKind.StoredProcedure,
                SqlObjectSchemaCache.ObjectKind.Trigger,
            });

        var match = objs.FirstOrDefault(o =>
            o.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            (schema == null || o.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)));

        if (match == null)
        {
            lblStatus.Text = $"未找到对象：{clean}";
            return;
        }

        var typeChar = SqlObjectSchemaCache.KindToTypeChar(match.Kind).Split(',')[0].Trim();
        var fullName = $"{match.SchemaName}.{match.Name}";
        OpenScript(database: "", objType: typeChar, objName: fullName);
    }

    /// <summary>
    /// 异步加载脚本，加载完成后填充到已创建的占位 Tab。
    /// - 不再 OpenScript 里起 NewTab（避免“切 Tab 看起来卡”）
    /// - 在这里仅做填充（Tab 文本 / Editor 内容 / 光标位置 / Status）
    /// </summary>
    private async Task LoadAndFillScriptAsync(SqlQueryTabPage tab, string objType, string objName)
    {
        try
        {
            lblStatus.Text = $"加载 {objType}.{objName} ...";
            var script = await SqlScriptLoader.LoadCreateScriptAsync(_currentConnStr, objType, objName);

            // 在 UI 线程上动 Tab（使用 IsDisposed 判断而不是 tab.Page.IsDisposed，避免 Page 在 close 路径下被非法访问）
            BeginInvoke(() =>
            {
                if (IsDisposed || tab.IsDisposed) return;        // tab 本身是最可靠的检查点
                var page = tab.Page;                            // 此处 Page 必定 != null（NewTab 已设）
                if (page == null || page.IsDisposed) return;    // 防 TabPage 被外部关窗后丢引用
                // 改 Tab 文本（去掉 "(加载中…)"）
                page.Text = $"{objType}.{objName}";
                // 填充实际脚本
                tab.SetEditorText(script ?? $"-- 加载 {objType}.{objName} 失败：脚本为空");
                // 光标到末尾、Focus Editor
                tab.Editor.Select(script?.Length ?? 0, 0);
                tab.Editor.Focus();
                lblStatus.Text = $"已加载 {objType}.{objName}";
            });
        }
        catch (Exception ex)
        {
            BeginInvoke(() =>
            {
                if (IsDisposed || tab.IsDisposed) return;
                var page = tab.Page;
                if (page != null && !page.IsDisposed) page.Text = $"{objType}.{objName} (失败)";
                GetActiveTab()?.AppendMessage($"[错误] 加载脚本失败: {ex.Message}\n");
                lblStatus.Text = $"加载 {objType}.{objName} 失败";
            });
        }
    }

    // ============================================
    // 状态回调（由 SqlQueryTabPage 调用）
    // ============================================

    public void UpdateStatus(string status, long elapsedMs, int affectedRows, ExecStatus kind)
    {
        lblStatus.Text = status;
        lblElapsed.Text = $"耗时: {elapsedMs} ms";
        lblRows.Text = $"影响: {affectedRows} 行";
        // 状态栏文字颜色按执行结果区分（绿/红/灰/蓝）—— 让用户一眼看出有没有成功
        lblStatus.ForeColor = kind switch
        {
            ExecStatus.Success   => Color.FromArgb(57, 181, 74),   // #39b54a green
            ExecStatus.Failure   => Color.FromArgb(251, 67, 42),   // #fb432a red
            ExecStatus.Cancelled => Color.FromArgb(138, 143, 152), // #8a8f98 gray
            ExecStatus.Running   => Color.FromArgb(27, 161, 226),  // #1ba1e2 blue
            _                    => SystemColors.ControlText,        // 默认黑
        };
    }
}
