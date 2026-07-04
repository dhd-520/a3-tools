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
    private ObjectExplorerForm? _explorer;   // 单例：与主窗体绑定：打开一次 → 关闭窗口 → 隐藏
    private bool _explorerUserClosed = false; // 用户点击 × 后不再默认打开，需点工具栏重开
    private bool _explorerVisible = false;    // 工具栏 toggle 状态

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

        // 后台预热当前库的 IntelliSense 缓存（不阻塞 UI；切库时也会再拉）
        _ = SqlObjectSchemaCache.WarmupAsync(_currentConnStr);

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
                _explorer.Location = ComputeExplorerLocation();
                _explorer.Height = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8);
                _explorer.Show();
                _ = _explorer.RefreshAsync();
                _explorerVisible = true;
                if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 关闭资源管理器";
            }
            return;
        }

        // 创建新的 Explorer
        _explorer = new ObjectExplorerForm(this)
        {
            Owner = this,
            Location = ComputeExplorerLocation(),
            Height = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8)
        };
        _explorer.FormClosed += (_, args) =>
        {
            _explorerVisible = false;
            if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 对象资源管理器";
            if (args.CloseReason == CloseReason.UserClosing)
                _explorerUserClosed = true;
            else
                _explorerUserClosed = false;
            // ★ 关键：Close 之后置 null，下次点按钮会走"创建新"分支重新计算 Location。
            // 否则下次 Show 会重用创建时的位置（可能在屏幕外）。
            _explorer = null;
        };
        _explorer.Show();
        _ = _explorer.RefreshAsync();
        _explorerVisible = true;
        if (btnToggleExplorer != null) btnToggleExplorer.Text = "📂 关闭资源管理器";
        _explorerUserClosed = false;
    }

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

    private void UpdateExplorerBounds()
    {
        if (_explorer == null || _explorer.IsDisposed || !_explorerVisible) return;
        if (WindowState == FormWindowState.Minimized) return;
        _explorer.Location = ComputeExplorerLocation();
        _explorer.Height = Math.Min(this.Height, GetScreenWorkArea().Bottom - Math.Max(this.Top, GetScreenWorkArea().Top) - 8);
    }

    /// <summary>父窗体关闭 → Explorer 一起关（避免孤儿窗口）</summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_explorer != null && !_explorer.IsDisposed)
        {
            try { _explorer.Close(); } catch { /* may already closed */ }
            _explorer.Dispose();
            _explorer = null;
        }
        base.OnFormClosed(e);
    }

    /// <summary>获取主窗体所在的屏幕 WorkArea（多屏支持）</summary>
    private Rectangle GetScreenWorkArea()
        => Screen.FromControl(this).WorkingArea;

    /// <summary>
    /// Explorer 位置计算 — 严格限制在屏幕 WorkArea 内，永不出屏。
    ///
    /// 为什么不用 "WindowState == Maximized" 判定全屏：
    ///   - 陛下会启用 "真正全屏"（无边框全屏 Bounds）— 这时 WindowState = Normal
    ///   - 也有可能主窗体位置/尺寸占满 WorkArea 但不叫 Maximized
    ///   — 所以一律按 "this.Right 贴 WorkArea 右" 判定，按 WorkArea 边界限制 Explorer
    /// </summary>
    private Point ComputeExplorerLocation()
    {
        const int explorerWidth = 360;
        const int gap = 4;

        var wa = GetScreenWorkArea();

        // 默认位置：主窗体右侧 4 px
        int x = this.Right + gap;
        int y = this.Top;

        // 严格边界检查：x 和 x + explorerWidth 必须在 [wa.Left, wa.Right] 内
        // 1. 右侧溢出
        if (x + explorerWidth > wa.Right)
        {
            // 优先贴主窗体左侧
            if (this.Left - explorerWidth - gap >= wa.Left)
                x = this.Left - explorerWidth - gap;
            else
                x = Math.Max(wa.Left, wa.Right - explorerWidth);    // 贴 WorkArea 右
        }

        // 2. 左侧溢出（主窗体在屏幕左外或主窗体已贴 WorkArea 左）
        if (x < wa.Left)
        {
            x = wa.Left;
        }

        // 3. 右侧再次溢出（如 workArea 宽度 < explorerWidth）
        if (x + explorerWidth > wa.Right)
        {
            x = Math.Max(wa.Left, wa.Right - explorerWidth);
        }

        // y 轴：贴顶
        if (y < wa.Top) y = wa.Top;
        // 底部溢出 → 上移到贴底但保留 200px
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

    public void UpdateStatus(string status, long elapsedMs, int affectedRows)
    {
        lblStatus.Text = status;
        lblElapsed.Text = $"耗时: {elapsedMs} ms";
        lblRows.Text = $"影响: {affectedRows} 行";
    }
}