using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Common.Forms;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class MainForm : Form, IToolContext
{
    private readonly DataService _dataService;
    private readonly ToolsConfigService _toolsConfigService;
    private readonly ToolExecutorService _toolExecutorService;
    private readonly List<IPlugin> _plugins = new();
    private readonly List<int> _processIds = new();
    private readonly Dictionary<int, bool> _processLaunchModes = new(); // PID -> 是否新窗口模式
    private readonly Dictionary<string, AccountStatus> _accountStatuses = new();
    private EdgeDockManager? _edgeDockManager;
    private HotkeyManager? _hotkeyManager;
    private bool _isInitializing = true;
    private Account? _toolSourceAccount = null;
    private Account? _toolTargetAccount = null;
    private bool _isRootMode = false;
    private int _titleClickCount = 0;
    private DateTime _lastTitleClickTime = DateTime.MinValue;
    private const string ROOT_PASSWORD = "xiaopacai"; // Root模式密码

    // 账套列表中需要脱敏显示的列（正常模式显示 ***，Root 模式才显示明文）
    private static readonly HashSet<string> _sensitiveColumnNames = new(StringComparer.Ordinal)
    {
        nameof(Account.Database),
        nameof(Account.DatabaseName),
        nameof(Account.DbUser),
        nameof(Account.RemoteAddress),
        nameof(Account.RemoteUser),
    };
    private const string MaskedPlaceholder = "***";
    private bool _isHiddenToTray = false; // 是否已隐藏到托盘
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_RESTORE = 0xF120;

    private void SetTrayIcon()
    {
        try
        {
            // 优先使用 AppContext.BaseDirectory（.NET 6+，单文件发布下指向 exe 真实目录）
            // 回退到 AppDomain.BaseDirectory
            string baseDir = AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(baseDir) || !File.Exists(Path.Combine(baseDir, "A3Tool.ico")))
            {
                baseDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            string iconPath = Path.Combine(baseDir, "A3Tool.ico");
            if (File.Exists(iconPath))
            {
                notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // 兜底：找不到外部 .ico 时使用系统默认图标，避免 notifyIcon.Icon 为 null
                notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载托盘图标失败: {ex.Message}");
            // 异常时也兜底一个系统图标
            try { notifyIcon.Icon = SystemIcons.Application; } catch { }
        }
    }

    #region IToolContext 实现

    /// <summary>
    /// 获取当前选中的账套（供工具箱开发者使用）
    /// </summary>
    public Account? GetSelectedAccount()
    {
        if (this.dgvAccounts.SelectedRows.Count > 0)
        {
            return this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        }
        return null;
    }

    /// <summary>
    /// 获取当前选中的账套代码
    /// </summary>
    public string? GetSelectedAccountCode()
    {
        return GetSelectedAccount()?.Code;
    }

    /// <summary>
    /// 获取所有账套
    /// </summary>
    public List<Account> GetAllAccounts()
    {
        return _dataService.LoadAccounts();
    }

    /// <summary>
    /// 获取工具箱 Tab 中预先选择的源/目标数据库账套。
    /// 工具打开时会自动带入这两项；工具内仍允许用户自行修改或重新选择。
    /// </summary>
    public ToolDatabasePreset GetToolDatabasePreset()
    {
        return new ToolDatabasePreset
        {
            SourceAccount = _toolSourceAccount,
            TargetAccount = _toolTargetAccount
        };
    }

    private void RefreshToolDbLabels()
    {
        if (lblToolsSourceDbName == null || lblToolsTargetDbName == null) return;
        lblToolsSourceDbName.Text = _toolSourceAccount == null
            ? "（未选择）"
            : $"{_toolSourceAccount.Code} - {_toolSourceAccount.Name}";
        lblToolsTargetDbName.Text = _toolTargetAccount == null
            ? "（未选择）"
            : $"{_toolTargetAccount.Code} - {_toolTargetAccount.Name}";
        UpdateToolsDescLabel();
    }

    private void BtnToolsSelectSourceDb_Click(object? sender, EventArgs e)
    {
        SelectToolAccount(isSource: true);
    }

    private void BtnToolsSelectTargetDb_Click(object? sender, EventArgs e)
    {
        SelectToolAccount(isSource: false);
    }

    private void BtnToolsClearSourceDb_Click(object? sender, EventArgs e)
    {
        _toolSourceAccount = null;
        RefreshToolDbLabels();
    }

    private void BtnToolsClearTargetDb_Click(object? sender, EventArgs e)
    {
        _toolTargetAccount = null;
        RefreshToolDbLabels();
    }

    private void SelectToolAccount(bool isSource)
    {
        var accounts = _dataService.LoadAndDecryptAccounts();
        using var dlg = new AccountSelectForm(accounts);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedAccount != null)
        {
            if (isSource) _toolSourceAccount = dlg.SelectedAccount;
            else _toolTargetAccount = dlg.SelectedAccount;
            RefreshToolDbLabels();
        }
    }

    private void UpdateToolsDescLabel()
    {
        if (lblDesc == null) return;
        var source = _toolSourceAccount;
        var target = _toolTargetAccount;
        var sourceText = source == null ? "未选择源库" : $"源库：{source.Code}-{source.Name}";
        var targetText = target == null ? "未选择目标库" : $"目标库：{target.Code}-{target.Name}";
        lblDesc.Text = $"{sourceText}    {targetText}";
    }

    private void UpdateToolsHeaderLayout()
    {
        if (descPanel == null || lblPluginStatus == null) return;

        lblPluginStatus.Left = Math.Max(500, descPanel.Width - lblPluginStatus.Width - 20);

        int available = descPanel.Width - 990;
        int extra = Math.Max(0, available / 2);
        int labelWidth = Math.Min(420, 290 + extra);

        lblToolsSourceDbName.Width = labelWidth;
        lblToolsSourceDbName.Left = btnToolsSelectSourceDb.Right + 8;
        btnToolsClearSourceDb.Left = lblToolsSourceDbName.Right + 8;
        lblToolsTargetDb.Left = btnToolsClearSourceDb.Right + 20;
        btnToolsSelectTargetDb.Left = lblToolsTargetDb.Right + 5;
        lblToolsTargetDbName.Width = labelWidth;
        lblToolsTargetDbName.Left = btnToolsSelectTargetDb.Right + 8;
        btnToolsClearTargetDb.Left = lblToolsTargetDbName.Right + 8;
    }

    /// <summary>
    /// 显示消息提示
    /// </summary>
    public void ShowMessage(string message)
    {
        MessageBox.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// 显示错误提示
    /// </summary>
    public void ShowError(string message)
    {
        MessageBox.Show(message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    #endregion

    public MainForm()
    {
        _dataService = new DataService();
        _toolsConfigService = new ToolsConfigService();
        _toolExecutorService = new ToolExecutorService();
        // 启动时更新所有现有账套的拼音
        _dataService.UpdateAllPinyin();
        InitializeComponent();
        InitLaunchTabControls();
        InitToolsTabControls();
        InitStatusTabControls();
        WireUpEvents();
        LoadPlugins();
        LoadAccounts();
        RefreshToolDbLabels();
        LoadAccountStatuses();
        this.scrollPanel?.BringToFront();
        this.Resize += MainForm_Resize;
        UpdateVersionPosition();
        _isInitializing = false;
        UpdateRootModeUI();
        _edgeDockManager = new EdgeDockManager(this);
        _edgeDockManager.OnHideToTray = HideToTray;
        _edgeDockManager.OnShowFromTray = ShowFromTray;
        _edgeDockManager.OnShowFromEdge = ShowFromTray;

        // 设置托盘图标
        SetTrayIcon();

        // 监听窗体大小变化，处理最小化
        this.Resize += MainForm_ResizeForMinimize;

        // 初始化快捷键管理器（延迟到窗体显示后注册，确保Handle已创建）
        this.Shown += (s, e) => InitHotkey();

        // 启用键盘预览，使F键能聚焦到搜索框
        this.KeyPreview = true;
    }

    private void InitHotkey()
    {
        if (_hotkeyManager != null) return;
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.EnsureReceiver();
        _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterAllHotkeys();
    }

    private void MainForm_ResizeForMinimize(object? sender, EventArgs e)
    {
        if (_isHiddenToTray && this.WindowState == FormWindowState.Minimized)
        {
            ShowFromTray();
        }
    }

    /// <summary>
    /// 注册所有全局快捷键
    /// </summary>
    public void RegisterAllHotkeys()
    {
        if (_hotkeyManager == null) return;


        var settings = _dataService.LoadSettings();
        _hotkeyManager.UnregisterAll();

        // 托盘显示
        if (!string.IsNullOrEmpty(settings.TrayShowHotkey))
            _hotkeyManager.ReregisterHotkey(1, settings.TrayShowHotkey);

        // 新增
        if (!string.IsNullOrEmpty(settings.AddHotkey))
            _hotkeyManager.ReregisterHotkey(2, settings.AddHotkey);


        // 删除
        if (!string.IsNullOrEmpty(settings.DeleteHotkey))
            _hotkeyManager.ReregisterHotkey(3, settings.DeleteHotkey);

        // 启动
        if (!string.IsNullOrEmpty(settings.LaunchHotkey))
            _hotkeyManager.ReregisterHotkey(4, settings.LaunchHotkey);


        // 设置
        if (!string.IsNullOrEmpty(settings.SettingsHotkey))
            _hotkeyManager.ReregisterHotkey(5, settings.SettingsHotkey);


        // 链接数据库
        if (!string.IsNullOrEmpty(settings.ConnectDBHotkey))
            _hotkeyManager.ReregisterHotkey(6, settings.ConnectDBHotkey);


        // 远程连接
        if (!string.IsNullOrEmpty(settings.RemoteHotkey))
            _hotkeyManager.ReregisterHotkey(7, settings.RemoteHotkey);

        // 刷新账套列表
        if (!string.IsNullOrEmpty(settings.RefreshHotkey))
            _hotkeyManager.ReregisterHotkey(8, settings.RefreshHotkey);
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        this.BeginInvoke(new Action(() =>
        {
            switch (hotkeyId)
            {
                case 1:
                    ShowFromTray();
                    break;
                case 2:
                    if (tabControl.SelectedTab == tabLaunch) ShowAccountDialog(null);
                    break;
                case 3:
                    if (tabControl.SelectedTab == tabLaunch) DeleteSelectedAccount();
                    break;
                case 4:
                    if (tabControl.SelectedTab == tabLaunch) LaunchSelectedAccount();
                    break;
                case 5:
                    BtnSettings_Click(null, EventArgs.Empty);
                    break;
                case 6:
                    if (tabControl.SelectedTab == tabLaunch) BtnConnectDB_Click(null, EventArgs.Empty);
                    break;
                case 7:
                    if (tabControl.SelectedTab == tabLaunch) BtnRemote_Click(null, EventArgs.Empty);
                    break;
                case 8:
                    if (tabControl.SelectedTab == tabLaunch) BtnRefresh_Click(null, EventArgs.Empty);
                    break;
            }
        }));
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        UpdateVersionPosition();
        RefreshDgvColumns();
    }

    private void RefreshDgvColumns()
    {
        // 强制刷新 Fill 列的宽度
        if (this.dgvAccounts == null || this.dgvAccounts.IsDisposed) return;

        foreach (DataGridViewColumn col in this.dgvAccounts.Columns)
        {
            if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
            {
                // 先设为 None 再设回 Fill，强制重新计算
                var oldWidth = col.Width;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }
    }

    private void UpdateVersionPosition()
    {
        if (lblVersion != null && lblVersion.Parent != null)
        {
            int parentWidth = lblVersion.Parent.Width;
            lblVersion.Left = parentWidth - lblVersion.Width - 15;
            lblVersion.Top = (lblVersion.Parent.Height - lblVersion.Height) / 2;
        }
    }

    private void WireUpEvents()
    {
        // 托盘事件
        this.notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        this.menuShow.Click += MenuShow_Click;
        this.menuHide.Click += MenuHide_Click;
        this.menuTrayExit.Click += MenuTrayExit_Click;
        this.menuExit.Click += MenuExit_Click;

        this.txtSearch.TextChanged += TxtSearch_TextChanged;
        this.txtSearch.KeyDown += TxtSearch_KeyDown;
        this.btnAdd.Click += BtnAdd_Click;
        this.btnImport.Click += BtnImport_Click;
        this.btnEdit.Click += BtnEdit_Click;
        this.btnDelete.Click += BtnDelete_Click;

        this.btnLaunch.Click += BtnLaunch_Click;
        this.btnSettings.Click += BtnSettings_Click;
        this.btnRefresh.Click += BtnRefresh_Click;
        this.btnConnectDB.Click += BtnConnectDB_Click;
        this.btnRemote.Click += BtnRemote_Click;
        this.btnToolsSelectSourceDb.Click += BtnToolsSelectSourceDb_Click;
        this.btnToolsSelectTargetDb.Click += BtnToolsSelectTargetDb_Click;
        this.btnToolsClearSourceDb.Click += BtnToolsClearSourceDb_Click;
        this.btnToolsClearTargetDb.Click += BtnToolsClearTargetDb_Click;
        this.tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        this.dgvAccounts.DoubleClick += DgvAccounts_DoubleClick;
        this.dgvAccounts.KeyDown += DgvAccounts_KeyDown;
        this.dgvAccounts.ColumnWidthChanged += DgvAccounts_ColumnWidthChanged;
        this.dgvAccounts.CellFormatting += DgvAccounts_CellFormatting;
        this.menuCopyAccount.Click += MenuCopyAccount_Click;
        this.menuHotkeySettings.Click += MenuHotkeySettings_Click;
        this.menuAbout.Click += MenuAbout_Click;
        this.lblTitle.Click += LblTitle_Click;

        this.KeyDown += MainForm_KeyDown;
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (this.tabControl.SelectedTab == this.tabTools && e.KeyData == (Keys.S | Keys.Control))
        {
            SelectToolAccount(isSource: true);
            e.SuppressKeyPress = true;
        }
        else if (this.tabControl.SelectedTab == this.tabTools && e.KeyData == (Keys.D | Keys.Control))
        {
            SelectToolAccount(isSource: false);
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == System.Windows.Forms.Keys.Oem3)
        {
            FocusSearchBox();
            e.SuppressKeyPress = true;
        }
        // Root模式下Ctrl+C快速复制账套信息
        else if (_isRootMode && e.KeyData == (Keys.C | Keys.Control))
        {
            CopySelectedAccountSilently();
            e.SuppressKeyPress = true;
        }
    }

    private void DgvAccounts_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
    {
        // 初始化阶段跳过，避免与 HandleCreated 时的布局冲突
        if (_isInitializing) return;

        // 用户手动调整列宽后，固定该列为手动模式
        var column = e.Column;
        if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
        {
            // 使用 Timer 延迟执行，避开布局冲突
            var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                timer.Dispose();
                try
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
                catch { }
            };
            timer.Start();
        }
    }

    private void LoadAccounts()
    {
        var accounts = _dataService.LoadAndDecryptAccounts();
        // 为没有拼音的历史数据自动计算
        foreach (var acc in accounts)
        {
            if (string.IsNullOrEmpty(acc.Pinyin) && !string.IsNullOrEmpty(acc.Name))
            {
                acc.Pinyin = PinyinHelper.GetPinyinInitial(acc.Name);
            }
        }
        // 如果搜索框有内容，按搜索条件过滤
        var keyword = txtSearch?.Text?.Trim()?.ToLower();
        if (!string.IsNullOrEmpty(keyword))
        {
            accounts = accounts.Where(a =>
                (a.Code ?? "").ToLower().Contains(keyword) ||
                (a.Name ?? "").ToLower().Contains(keyword) ||
                (a.Pinyin ?? "").ToLower().Contains(keyword) ||
                (a.Server ?? "").ToLower().Contains(keyword) ||
                (a.Database ?? "").ToLower().Contains(keyword) ||
                (a.RemoteAddress ?? "").ToLower().Contains(keyword)
            ).ToList();
        }
        this.dgvAccounts.DataSource = null;
        this.dgvAccounts.DataSource = accounts;
        SetupDataGridViewColumns();
        UpdatePasswordColumns();
        RefreshToolDbLabels();
        this.dgvAccounts.BringToFront();
        this.dgvAccounts.ClearSelection();
        this.dgvAccounts.PerformLayout();
        if (accounts.Count > 0)
            this.dgvAccounts.FirstDisplayedScrollingRowIndex = 0;
    }

    private void SetupDataGridViewColumns()
    {
        this.dgvAccounts.AutoGenerateColumns = false;
        this.dgvAccounts.Columns.Clear();

        var cols = new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "代码", DataPropertyName = "Code", Name = "ColCode", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewTextBoxColumn { HeaderText = "账套名称", DataPropertyName = "Name", Name = "ColName", FillWeight = 25, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "账套地址", DataPropertyName = "Server", Name = "ColServer", FillWeight = 30, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "数据库地址", DataPropertyName = "Database", Name = "ColDatabase", FillWeight = 20, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "数据库名称", DataPropertyName = "DatabaseName", Name = "ColDatabaseName", FillWeight = 18, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "DB用户", DataPropertyName = "DbUser", Name = "ColDbUser", FillWeight = 12, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "远程方式", DataPropertyName = "RemoteType", Name = "ColRemoteType", FillWeight = 10, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "远程地址", DataPropertyName = "RemoteAddress", Name = "ColRemoteAddress", FillWeight = 25, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewTextBoxColumn { HeaderText = "远程用户", DataPropertyName = "RemoteUser", Name = "ColRemoteUser", FillWeight = 12, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
        };
        this.dgvAccounts.Columns.AddRange(cols);
    }

    private void UpdatePasswordColumns()
    {
        foreach (DataGridViewRow row in this.dgvAccounts.Rows)
            row.Height = 32;
    }

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        SearchAccounts();
    }

    private void SearchAccounts()
    {
        var keyword = this.txtSearch.Text.Trim().ToLower();
        var accounts = _dataService.LoadAndDecryptAccounts();

        if (string.IsNullOrEmpty(keyword))
        {
            // 空关键字显示全部
        }
        else
        {
            // 搜索：代码、名称、拼音、地址等
            accounts = accounts.Where(a =>
                (a.Code ?? "").ToLower().Contains(keyword) ||
                (a.Name ?? "").ToLower().Contains(keyword) ||
                (a.Pinyin ?? "").ToLower().Contains(keyword) ||
                (a.Server ?? "").ToLower().Contains(keyword) ||
                (a.Database ?? "").ToLower().Contains(keyword) ||
                (a.RemoteAddress ?? "").ToLower().Contains(keyword)
            ).ToList();
        }

        this.dgvAccounts.DataSource = null;
        this.dgvAccounts.DataSource = accounts;
        SetupDataGridViewColumns();
        UpdatePasswordColumns();
        RefreshToolDbLabels();
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        // 弹出下拉菜单选择「手动添加」/「一键添加」
        if (sender is Control ctrl && this.addMenu != null)
        {
            this.addMenu.Show(ctrl, 0, ctrl.Height);
        }
    }

    private void MiManualAdd_Click(object? sender, EventArgs e)
    {
        ShowAccountDialog(null);
    }

    private void MiQuickAdd_Click(object? sender, EventArgs e)
    {
        using var dialog = new QuickAddAccountDialog();
        var result = dialog.ShowDialog();
        if (dialog.SwitchToManual)
        {
            // 用户在一键添加窗体里选了「切换为手动添加」 → 弹原有 AccountDialog
            ShowAccountDialog(null);
            return;
        }
        if (result == DialogResult.OK && dialog.CreatedAccount != null)
        {
            LoadAccounts();
            var added = dialog.CreatedAccount;
            ShowToast($"账套「{added.Name}」已添加（代码 {added.Code}）");
        }
    }
    private void BtnImport_Click(object? sender, EventArgs e) => ImportFromXml();
    private void BtnRefresh_Click(object? sender, EventArgs e) => RefreshAccountList();

    /// <summary>
    /// 刷新账套列表：从磁盘重新加载 + 重新应用当前搜索关键字 + 刷新状态栏。
    /// 被按钮点击、全局快捷键共用。
    /// </summary>
    private void RefreshAccountList()
    {
        try
        {
            LoadAccounts();
            LoadAccountStatuses();
            RefreshStatusGrid();
            this.txtSearch?.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportFromXml()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择XML账套文件",
            Filter = "XML文件(*.xml)|*.xml|所有文件(*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        string xmlPath = dialog.FileName;
        if (!File.Exists(xmlPath))
        {
            MessageBox.Show("文件不存在！", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            string content = File.ReadAllText(xmlPath);
            var accounts = new List<Account>();
            var rgx = new System.Text.RegularExpressions.Regex("<TbLYBillSet>(.*?)</TbLYBillSet>", System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match m in rgx.Matches(content))
            {
                string block = m.Groups[1].Value;
                string name = GetXmlField(block, "NAME").Trim();
                name = System.Text.RegularExpressions.Regex.Replace(name, @"https?://[^\s<]*", "").Trim();
                string ztaddr = GetXmlField(block, "ZTADDRESS");
                string ztpwd = GetXmlField(block, "ZTPWD");
                string sqladdr = GetXmlField(block, "SQLADDRESS");
                string usercode = GetXmlField(block, "USERCODE");
                string userpwd = GetXmlField(block, "USERPWD");
                string server = ztaddr.Replace("http://", "").Replace("https://", "").Replace("/", "");
                string dbAddr = sqladdr.Split(' ')[0].Trim();
                string dbUser = usercode.Split(' ')[0].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(server)) continue;
                string serverUrl = ztaddr.EndsWith("/") ? ztaddr : ztaddr + "/";
                // 保存原始XML行到备注
                string remark = block.Trim();
                accounts.Add(new Account
                {
                    Code = "",
                    Name = name,
                    Pinyin = PinyinHelper.GetPinyinInitial(name),
                    Server = serverUrl,
                    ServerPassword = ztpwd,
                    Database = dbAddr,
                    DatabaseName = "",
                    DbUser = dbUser,
                    DbPassword = userpwd,
                    RemoteType = "",
                    RemoteAddress = "",
                    RemoteUser = "",
                    RemotePassword = "",
                    Remark = remark
                });
            }
            if (accounts.Count == 0)
            {
                MessageBox.Show("未找到有效账套数据！", "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 自动生成CODE
            var existingAccounts = _dataService.LoadAccounts();
            int maxCode = 0;
            foreach (var acc in existingAccounts)
            {
                if (int.TryParse(acc.Code, out int code) && code > maxCode)
                    maxCode = code;
            }
            int newCode = maxCode + 1;
            foreach (var account in accounts)
            {
                account.Code = newCode.ToString("D4");
                newCode++;
                _dataService.AddAccount(account);
            }
            LoadAccounts();
            MessageBox.Show($"成功从 {Path.GetFileName(xmlPath)} 导入 {accounts.Count} 条账套！", "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GetXmlField(string block, string field)
    {
        var match = System.Text.RegularExpressions.Regex.Match(block, $"<{field}>(.*?)</{field}>", System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private void BtnEdit_Click(object? sender, EventArgs e) => EditSelectedAccount();
    private void BtnDelete_Click(object? sender, EventArgs e) => DeleteSelectedAccount();
    private void BtnLaunch_Click(object? sender, EventArgs e) => LaunchSelectedAccount();

    private void DgvAccounts_DoubleClick(object? sender, EventArgs e) => EditSelectedAccount();

    /// <summary>
    /// 账套列表单元格格式化：正常模式下对敏感字段脱敏（显示 ***），Root 模式显示明文。
    /// 触发时机：加载、滚动、Root 模式切换后 Invalidate。
    /// </summary>
    private void DgvAccounts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (this.dgvAccounts.Rows.Count <= e.RowIndex) return;
        if (this.dgvAccounts.Columns.Count <= e.ColumnIndex) return;

        var col = this.dgvAccounts.Columns[e.ColumnIndex];
        if (!_sensitiveColumnNames.Contains(col.DataPropertyName)) return;

        // Root 模式：放行，使用默认绑定值
        if (_isRootMode) return;

        var acc = this.dgvAccounts.Rows[e.RowIndex].DataBoundItem as Account;
        var prop = typeof(Account).GetProperty(col.DataPropertyName);
        var raw = prop?.GetValue(acc) as string;

        // 空值保持空白；非空统一 *** 脱敏
        e.Value = string.IsNullOrEmpty(raw) ? string.Empty : MaskedPlaceholder;
        e.FormattingApplied = true;
    }

    private void DgvAccounts_KeyDown(object? sender, KeyEventArgs e)
    {
        if (dgvAccounts.DataSource == null || dgvAccounts.Rows.Count == 0) return;

        if (e.KeyData == Keys.Up || e.KeyData == Keys.Down || e.KeyData == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            int currentIdx = dgvAccounts.CurrentRow?.Index ?? -1;

            if (e.KeyData == Keys.Up)
            {
                if (currentIdx > 0)
                {
                    dgvAccounts.CurrentCell = dgvAccounts.Rows[currentIdx - 1].Cells[0];
                    dgvAccounts.Rows[currentIdx - 1].Selected = true;
                    dgvAccounts.FirstDisplayedScrollingRowIndex = Math.Max(0, currentIdx - 2);
                }
            }
            else if (e.KeyData == Keys.Down)
            {
                if (currentIdx < 0) currentIdx = 0;
                if (currentIdx < dgvAccounts.Rows.Count - 1)
                {
                    dgvAccounts.CurrentCell = dgvAccounts.Rows[currentIdx + 1].Cells[0];
                    dgvAccounts.Rows[currentIdx + 1].Selected = true;
                    dgvAccounts.FirstDisplayedScrollingRowIndex = Math.Min(dgvAccounts.Rows.Count - 1, currentIdx);
                }
            }
            else if (e.KeyData == Keys.Enter)
            {
                if (dgvAccounts.SelectedRows.Count > 0)
                    LaunchSelectedAccount();
            }
        }
    }

    private void FocusSearchBox()
    {
        if (txtSearch != null && !txtSearch.IsDisposed)
        {
            txtSearch.Focus();
            txtSearch.Select(txtSearch.Text.Length, 0);
        }
    }

    private void TxtSearch_KeyDown(object? sender, KeyEventArgs e)
    {
        if (dgvAccounts.DataSource == null || dgvAccounts.Rows.Count == 0) return;


        if (e.KeyData == Keys.Up || e.KeyData == Keys.Down || e.KeyData == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            // 先让搜索框失去焦点并选中当前行，确保视觉一致
            int currentIdx = dgvAccounts.CurrentRow?.Index ?? -1;

            if (e.KeyData == Keys.Up)
            {
                if (currentIdx > 0)
                {
                    dgvAccounts.CurrentCell = dgvAccounts.Rows[currentIdx - 1].Cells[0];
                    dgvAccounts.Rows[currentIdx - 1].Selected = true;
                    dgvAccounts.FirstDisplayedScrollingRowIndex = Math.Max(0, currentIdx - 2);
                }
            }
            else if (e.KeyData == Keys.Down)
            {
                if (currentIdx < 0) currentIdx = 0;
                if (currentIdx < dgvAccounts.Rows.Count - 1)
                {
                    dgvAccounts.CurrentCell = dgvAccounts.Rows[currentIdx + 1].Cells[0];
                    dgvAccounts.Rows[currentIdx + 1].Selected = true;
                    dgvAccounts.FirstDisplayedScrollingRowIndex = Math.Min(dgvAccounts.Rows.Count - 1, currentIdx);
                }
            }
            else if (e.KeyData == Keys.Enter)
            {
                if (dgvAccounts.SelectedRows.Count > 0)
                    LaunchSelectedAccount();
            }
        }
    }

    private void ShowAccountDialog(Account? account)
    {
        bool isRoot = _isRootMode;
        using var dialog = new AccountDialog(account, isRoot);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var edited = dialog.GetAccount();
            // 自动计算拼音
            edited.Pinyin = PinyinHelper.GetPinyinInitial(edited.Name);
            if (account == null)
                _dataService.AddAccount(edited);
            else
                _dataService.UpdateAccount(account.Code, edited);
            LoadAccounts();
        }
    }

    private void EditSelectedAccount()
    {
        if (this.dgvAccounts.SelectedRows.Count == 0)
        {
            // 尝试选择当前行
            if (this.dgvAccounts.CurrentRow != null && this.dgvAccounts.CurrentRow.Index >= 0)
            {
                this.dgvAccounts.Rows[this.dgvAccounts.CurrentRow.Index].Selected = true;
            }
        }

        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account != null)
        {
            // 调试：显示Root模式状态
            // MessageBox.Show($"Root模式：{_isRootMode}", "调试");
            ShowAccountDialog(account);
        }
    }

    private void DeleteSelectedAccount()
    {
        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null) return;
        var result = MessageBox.Show($"确定要删除账套【{account.Name}】吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _dataService.DeleteAccount(account.Code);
            LoadAccounts();
        }
    }

    private void LaunchSelectedAccount()
    {
        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null) return;

        // 保证主窗在最前端：
        // - 托盘隐藏状态：ShowFromTray() 恢复 + 拉前台
        // - 正常但非激活状态：ForceForegroundWindow 拉前台（避免启动后主窗被压在后看不见）
        if (_isHiddenToTray)
            ShowFromTray();
        else if (Form.ActiveForm != this)
            ForceForegroundWindow(this.Handle);

        var settings = _dataService.LoadSettings();
        if (string.IsNullOrEmpty(settings.AppDirectory) || !Directory.Exists(settings.AppDirectory))
        {
            MessageBox.Show("请先在【设置】中设置A3应用程序目录！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 检查是否需要弹出启动选项对话框
        // 条件：勾选了"弹出"选项，或者从未保存过设置（首次使用）
        bool shouldShowDialog = settings.ShowLaunchOptionsDialog || !_dataService.HasSettings();

        if (shouldShowDialog)
        {
            // 弹出启动选项选择对话框
            using var dialog = new LaunchOptionsDialog(settings.LaunchDesktop, settings.LaunchDevTools, settings.LaunchWeb, settings.SelectedBrowser, account.Name, account.Code);
            if (dialog.ShowDialog() != DialogResult.OK) return;

            // 保存用户的选择
            settings.LaunchDesktop = dialog.LaunchDesktop;
            settings.LaunchDevTools = dialog.LaunchDevTools;
            settings.LaunchWeb = dialog.LaunchWeb;
            settings.SelectedBrowser = dialog.SelectedBrowser;
            // 如果是首次使用且用户选择了保存，下次不再弹出
            if (!_dataService.HasSettings())
            {
                settings.ShowLaunchOptionsDialog = false;
            }
            _dataService.SaveSettings(settings);
        }

        string appDir = settings.AppDirectory;

        string configPath = Path.Combine(appDir, "AppConfig.xml");
        if (File.Exists(configPath))
        {
            try
            {
                var doc = XDocument.Load(configPath);
                var serverItem = doc.Root?.Descendants("ServerItem").FirstOrDefault();
                if (serverItem != null && serverItem.Parent?.Name == "ServerSettings")
                {
                    serverItem.Value = account.Server;
                    doc.Save(configPath);
                }
                else
                {
                    var config = doc.Root?.Element("Configuration");
                    var serverSettings = config?.Element("ServerSettings");
                    var item = serverSettings?.Element("ServerItem");
                    if (item != null)
                    {
                        item.Value = account.Server;
                        doc.Save(configPath);
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(account.ServerPassword))
            Clipboard.SetText(account.ServerPassword);

        if (settings.LaunchDesktop)
        {
            string exe1 = Path.Combine(appDir, "君则A3.exe");
            if (File.Exists(exe1))
            {
                // 按账套判断：A3 客户端进程名都一样，全局去重会误伤其他账套
                // 改为查「这个账套 Code」是否已经启动过客户端 → 有则只切到前台
                if (TryBringAccountProcessesToFront(account.Code, "client"))
                {
                    ShowToast($"账套【{account.Name}】客户端已在运行，已切到前台");
                }
                else
                {
                    Process? p;
                    // 客户端自动登录：复用 ServerUsername + ServerPassword
                    // 受 Settings.ClientAutoLogin 开关控制
                    var appSettingsClient = _dataService.LoadSettings();
                    if (appSettingsClient.ClientAutoLogin
                        && !string.IsNullOrEmpty(account.ServerPassword)
                        && !string.IsNullOrEmpty(account.ServerUsername))
                    {
                        p = Win32AutoLoginHelper.LaunchAndAutoLogin(
                            exe1,
                            windowTitleContains: "君则A3",
                            accountName: account.Name,
                            username: account.ServerUsername,
                            password: account.ServerPassword,
                            timeoutMs: 30000);
                    }
                    else
                    {
                        p = Process.Start(new ProcessStartInfo { FileName = exe1, WorkingDirectory = appDir, UseShellExecute = true });
                    }

                    if (p != null)
                    {
                        _processIds.Add(p.Id);
                        RecordProcess(account.Code, p.Id, "client");
                    }
                }
            }
        }

        if (settings.LaunchDevTools)
        {
            string exe2 = Path.Combine(appDir, "君则A3集成开发工具.exe");
            if (File.Exists(exe2))
            {
                // 按账套判断：开发工具进程名也都一样，全局去重会误伤其他账套
                if (TryBringAccountProcessesToFront(account.Code, "dev"))
                {
                    ShowToast($"账套【{account.Name}】开发工具已在运行，已切到前台");
                }
                else
                {
                    Process? p;
                    // 开发工具自动登录：两步登录
                    // 步骤 1：复用 ServerUsername + ServerPassword（客户端登录）
                    // 步骤 2：从 Settings.DevToolsPassword 读开发工具密码（仅填密码，开发账号默认记住）
                    // 受 Settings.DevToolsAutoLogin 开关控制
                    var appSettings = _dataService.LoadSettings();
                    string devToolsPassword = appSettings.DevToolsPassword;
                    if (appSettings.DevToolsAutoLogin
                        && !string.IsNullOrEmpty(account.ServerPassword)
                        && !string.IsNullOrEmpty(account.ServerUsername)
                        && !string.IsNullOrEmpty(devToolsPassword))
                    {
                        p = Win32AutoLoginHelper.LaunchAndAutoLoginDevTools(
                            exe2,
                            windowTitleContains: "IDE授权登录",  // 兼容保留（内部不使用）
                            accountName: account.Name,
                            clientUsername: account.ServerUsername,
                            clientPassword: account.ServerPassword,
                            devPassword: devToolsPassword,
                            stepTimeoutMs: 30000,
                            transitionDelayMs: 100);
                    }
                    else
                    {
                        p = Process.Start(new ProcessStartInfo { FileName = exe2, WorkingDirectory = appDir, UseShellExecute = true });
                    }

                    if (p != null)
                    {
                        _processIds.Add(p.Id);
                        RecordProcess(account.Code, p.Id, "dev");
                    }
                }
            }
        }

        if (settings.LaunchWeb && !string.IsNullOrEmpty(account.Server))
        {
            string url = account.Server.TrimEnd('/') + "/h5comerp/#/login";
            LaunchWebBrowser(url, settings.SelectedBrowser, account.Code, account);
        }

        // 启动成功（已记录在案）后自动隐藏到托盘，释放屏幕交给 A3
        // 取消 if-else 走过的都是「什么也没启动」场景，不隐藏避免误伤
        // _processIds 是启动过的进程集合（web 启动不进这里），有内容说明至少起了一个
        if (_processIds.Count > 0 && !_isHiddenToTray)
        {
            this.BeginInvoke(new Action(() => HideToTray()));
        }
    }

    private void LaunchWebBrowser(string url, string browser, string accountCode, Account? account = null)
    {
        var settings = _dataService.LoadSettings();
        bool newWindow = settings.BrowserNewWindow;
        string browserPath = GetBrowserPath(browser);

        // === Tab 模式 + CDP：直接 ShellExecute 跳板 在用户原 Edge 开新 Tab，不自动登录 ===
        // 理由：CDP 自动登录需要独占 user-data-dir 启动独立 Edge 进程，会以新窗口出现
        //       无法做到“用原 Edge 实例 + 开新 Tab”这种 “原 Tab 模式” 体验
        // 设计选择：Tab 模式仅用 ShellExecute 跳板（explorer.exe 打开 URL）让系统默认浏览器
        //       在已开 Edge 实例里 开新 Tab，自动登录表放弃
        // 提示：用户自己手动输入用户名密码
        if (!newWindow)
        {
            try
            {
                CdpHelper.CdpLog($"Tab 模式：ShellExecute 跳板打开 {url}（不启动新进程，不自动登录）");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + url + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开浏览器失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        // === 新窗口模式：CDP 自动登录 ===
        // 独占 user-data-dir 启动独立 Edge 进程，调试端口生效后 CDP 接填表
        bool useCdp = account != null
            && account.HasWebAutoLogin
            && CdpHelper.IsCdpSupported(browser);
        int cdpPort = 0;
        string? cdpUserDataDir = null;
        string fullArgs = "";
        bool useExistingBrowser = false;
        if (useCdp)
        {
            // 新窗口模式：独占 user-data-dir
            cdpPort = CdpHelper.FindFreePort();
            cdpUserDataDir = CdpHelper.GetTempUserDataDir();
            fullArgs = BuildBrowserArgs(url, browser, newWindow, cdpPort, cdpUserDataDir);
            CdpHelper.CdpLog($"新窗口模式：准备启动浏览器：exe={browserPath}");
            CdpHelper.CdpLog($"完整参数：{fullArgs}");
            CdpHelper.CdpLog($"调试端口：{cdpPort}, user-data-dir={cdpUserDataDir}");
        }

        // 【2026-06-16 新增】复用现有浏览器：跳过 Process.Start，直接进 CDP 自动登录
        if (useExistingBrowser)
        {
            CdpHelper.CdpLog("复用现有浏览器进程，不启动新进程");
            if (useCdp)
            {
                _ = RunCdpAutoLoginAsync(cdpPort, url, account!, browser, newWindow, useExistingBrowser: true);
            }
            return;
        }

        if (!string.IsNullOrEmpty(browserPath) && File.Exists(browserPath))
        {
            CdpHelper.CdpLog($"找到浏览器：{browserPath}");
            // 如果 useCdp 已算出 fullArgs，复用它；否则现场算
            string args = !string.IsNullOrEmpty(fullArgs) ? fullArgs : BuildBrowserArgs(url, browser, newWindow, useCdp ? cdpPort : 0, useCdp ? cdpUserDataDir : null);

            // Chrome/360 直接启动，UseShellExecute=false 可获取真实 PID
            // Edge/Firefox 也去掉 ShellExecute，用 --new-window 参数保证新窗口
            bool useShellExecute = false;

            var startInfo = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = args,
                UseShellExecute = useShellExecute,
                CreateNoWindow = !useShellExecute
            };
            try
            {
                // Edge/Chrome 是多进程浏览器，Process.Start 返回的 PID 可能只是壳进程，
                // 也可能 2 秒后退出并把窗口转交给其它新进程。
                // 所以启动前先记录现有 PID，启动后按差集登记该账套新增的所有浏览器进程。
                var existingPids = GetExistingBrowserPids(browser);

                CdpHelper.CdpLog($"调用 Process.Start，useShellExecute={useShellExecute}");
                var p = Process.Start(startInfo);
                if (p != null && !p.HasExited)
                {
                    CdpHelper.CdpLog($"进程启动成功：PID={p.Id}");
                    // 等 2 秒再检查一次，避免 Edge 立即退出但 Process.Start 还没感知
                    System.Threading.Thread.Sleep(2000);
                    try
                    {
                        if (p.HasExited)
                        {
                            CdpHelper.CdpLog($"⚠️ 进程 2 秒后已退出 (ExitCode={p.ExitCode})，可能是 Edge 单实例转发给了其它进程");
                        }
                        else
                        {
                            CdpHelper.CdpLog($"✓ 进程 2 秒后仍在运行（真实存活）");
                        }
                    }
                    catch (Exception ex)
                    {
                        CdpHelper.CdpLog($"检查进程状态失败: {ex.Message}");
                    }
                }
                else
                {
                    // 进程立即退出！可能是 Edge 单实例把 URL 转发给其它进程了
                    CdpHelper.CdpLog($"⚠️ 进程立即退出 (HasExited={p?.HasExited})，可能是 Edge 单实例转发给了其它进程");
                    // 如果是 CDP 模式，等一等再尝试 CDP 连接（可能有新进程）
                    if (useCdp)
                    {
                        CdpHelper.CdpLog("CDP 模式：等待 2 秒让 Edge 启动...");
                        System.Threading.Thread.Sleep(2000);
                    }
                }

                var recordedWebPid = false;
                var newPids = GetNewBrowserPids(browser, existingPids);
                foreach (var newPid in newPids)
                {
                    _processIds.Add(newPid);
                    _processLaunchModes[newPid] = newWindow;
                    RecordProcess(accountCode, newPid, "web");
                    recordedWebPid = true;
                }

                // 兜底：如果没找到新增 PID，但 Process.Start 返回的进程仍存活，至少登记它。
                if (!recordedWebPid && p != null && !p.HasExited)
                {
                    _processIds.Add(p.Id);
                    _processLaunchModes[p.Id] = newWindow;
                    RecordProcess(accountCode, p.Id, "web");
                }

                // CDP 自动登录：浏览器启动后异步执行
                if (useCdp)
                {
                    _ = RunCdpAutoLoginAsync(cdpPort, url, account!, browser, newWindow);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动{browser}失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            CdpHelper.CdpLog($"未找到浏览器：browser={browser} path=“{browserPath}”，尝试从注册表查找");
            // 浏览器未找到，尝试从注册表查找
            string? foundPath = FindBrowserFromRegistry(browser);
            if (!string.IsNullOrEmpty(foundPath) && File.Exists(foundPath))
            {
                CdpHelper.CdpLog($"注册表找到浏览器：{foundPath}");
                // 使用注册表找到的路径启动
                string args = BuildBrowserArgs(url, browser, newWindow, useCdp ? cdpPort : 0, useCdp ? cdpUserDataDir : null);
                bool useShellExecute = false;
                try
                {
                    var existingPids = GetExistingBrowserPids(browser);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = foundPath,
                        Arguments = args,
                        UseShellExecute = useShellExecute,
                        CreateNoWindow = !useShellExecute
                    };
                    var p = Process.Start(startInfo);
                    System.Threading.Thread.Sleep(useCdp ? 2000 : 500);

                    var recordedWebPid = false;
                    foreach (var newPid in GetNewBrowserPids(browser, existingPids))
                    {
                        _processIds.Add(newPid);
                        _processLaunchModes[newPid] = newWindow;
                        RecordProcess(accountCode, newPid, "web");
                        recordedWebPid = true;
                    }

                    if (!recordedWebPid && p != null && !p.HasExited)
                    {
                        _processIds.Add(p.Id);
                        _processLaunchModes[p.Id] = newWindow;
                        RecordProcess(accountCode, p.Id, "web");
                    }

                    // CDP 自动登录
                    if (useCdp)
                    {
                        _ = RunCdpAutoLoginAsync(cdpPort, url, account!, browser, newWindow);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动{browser}失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                // 完全找不到，使用默认浏览器但仍尝试 --new-window
                try
                {
                    string defaultArgs = browser switch
                    {
                        "chrome" => $"--new-window \"{url}\"",
                        "msedge" => $"--new-window \"{url}\"",
                        "firefox" => $"-new-window \"{url}\"",
                        "360se" => $"--new-window \"{url}\"",
                        // 系统默认浏览器无法加参数
                        _ => $"\"{url}\""
                    };
                    // 直接用 URL 让系统处理
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true,
                        Arguments = defaultArgs
                    };
                    var p = Process.Start(startInfo);
                    if (p != null)
                    {
                        _processIds.Add(p.Id);
                        _processLaunchModes[p.Id] = newWindow;
                        RecordProcess(accountCode, p.Id, "web");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动浏览器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }

    /// <summary>
    /// CDP 自动登录（异步）：连接远程调试端口 → 填表 → 提交
    /// 【2026-06-16 重构】使用 browser-level session + pageSessionId 路由，
    ///                    修复 page-level WebSocket 在 page 导航后被销毁的问题
    /// </summary>
    /// <param name="port">CDP 远程调试端口</param>
    /// <param name="url">要打开的 URL</param>
    /// <param name="account">账套（含登录凭证）</param>
    /// <param name="browser">浏览器类型</param>
    /// <param name="isNewWindow">是否“启动新窗口”模式</param>
    /// <param name="useExistingBrowser">Tab 模式且现有浏览器已启用调试端口时为 true，
    ///                                  不启动新进程，直接复用现有浏览器开新 Tab</param>
    private async Task RunCdpAutoLoginAsync(int port, string url, Account account, string browser, bool isNewWindow, bool useExistingBrowser = false)
    {
        CdpSession? session = null;
        try
        {
            // 从设置中读取选择器配置
            var settings = _dataService.LoadSettings();
            string usernameSel = settings.WebUsernameSelector;
            string passwordSel = settings.WebPasswordSelector;
            string submitSel = settings.WebSubmitSelector;
            string username = account.ServerUsername;
            string password = account.ServerPassword;

            // 检查必要参数
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                CdpHelper.CdpLog("✗ 账套用户名为空或密码为空，跳过自动登录");
                return;
            }
            if (string.IsNullOrEmpty(usernameSel) || string.IsNullOrEmpty(passwordSel) || string.IsNullOrEmpty(submitSel))
            {
                CdpHelper.CdpLog("✗ 选择器未配置，请到设置中配置网页登录选择器");
                return;
            }

            string? pageSessionId = null;

            if (useExistingBrowser)
            {
                // === 【Tab 模式复用现有浏览器】在现有窗口开新 Tab ===
                CdpHelper.CdpLog($"Tab 模式：现有 {browser} 调试端口 {port}，用 Target.createTarget 在该窗口开新 Tab");
                var created = await CdpHelper.CreateNewTabAsync(port, url);
                if (created == null)
                {
                    CdpHelper.CdpLog("✗ 现有浏览器开新 Tab 失败，跳过自动登录");
                    return;
                }
                (session, pageSessionId) = created.Value;
                CdpHelper.CdpLog($"✓ 已在现有 {browser} 中开新 Tab，pageSessionId={pageSessionId}");
            }
            else
            {
                // === 【新进程】连 browser-level session，attach 到已打开 URL 的 page target ===
                // 最多等 30 次 × 200ms = 6s，等新进程起来并启动 CDP
                CdpHelper.CdpLog($"等待新进程 CDP 启动 (port={port})...");
                for (int i = 0; i < 30; i++)
                {
                    var attached = await CdpHelper.AttachToPageAsync(port, url);
                    if (attached != null)
                    {
                        (session, pageSessionId) = attached.Value;
                        break;
                    }
                    await Task.Delay(200);
                }
                if (session == null || pageSessionId == null)
                {
                    CdpHelper.CdpLog("✗ 拿不到 page session，浏览器可能启动失败（请在 VS Debug 输出中查看 CDP 日志）");
                    return;
                }
            }

            CdpHelper.CdpLog($"开始自动登录：账号={username} 密码长度={password?.Length ?? 0}");
            CdpHelper.CdpLog($"选择器：user={usernameSel} pwd={passwordSel} btn={submitSel}");

            bool ok = await CdpHelper.AutoLoginAsync(
                session,
                url,
                usernameSel,
                passwordSel,
                submitSel,
                username,
                password,
                timeoutMs: 45000,
                sessionId: pageSessionId);

            if (ok)
            {
                CdpHelper.CdpLog("✓ 自动登录成功（填表并点击登录）");
            }
            else
            {
                CdpHelper.CdpLog($"✗ 自动登录超时（45000ms 内未完成填表 + 点击 + URL 跳转）");
            }
        }
        catch (Exception ex)
        {
            CdpHelper.CdpLog($"✗ 自动登录异常: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // browser-level session 生命周期由调用方控制（不要在这里 Dispose）
            // 页面可能还在加载后续操作，后续调用还会用
            // 这里仅输出调试
        }
    }

    /// <summary>
    /// 查现有浏览器进程的 --user-data-dir 参数（Tab 模式用，共享 profile）
    /// </summary>
    private string? GetExistingBrowserUserDataDir(string browser)
    {
        string procName = browser == "msedge" ? "msedge" : (browser == "chrome" ? "chrome" : "");
        if (string.IsNullOrEmpty(procName)) return null;
        try
        {
            var procs = Process.GetProcessesByName(procName);
            foreach (var p in procs)
            {
                try
                {
                    var searcher = new System.Management.ManagementObjectSearcher(
                        $"SELECT CommandLine FROM Win32_Process WHERE ProcessId={p.Id}");
                    foreach (var obj in searcher.Get())
                    {
                        var cmd = obj["CommandLine"]?.ToString() ?? "";
                        var match = System.Text.RegularExpressions.Regex.Match(
                            cmd, @"--user-data-dir=(""?)([^""\s]+)\1");
                        if (match.Success)
                        {
                            return match.Groups[2].Value;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 获取浏览器默认 profile 路径
    /// </summary>
    private string GetDefaultUserDataDir(string browser)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return browser switch
        {
            "msedge" => Path.Combine(localAppData, @"Microsoft\Edge\User Data"),
            "chrome" => Path.Combine(localAppData, @"Google\Chrome\User Data"),
            _ => Path.Combine(localAppData, @"A3Tools_TempProfile")
        };
    }

    /// <summary>
    /// 复制现有浏览器 profile 关键文件到 temp profile，实现共享登录状态
    /// 【问题背景】Edge/Chrome 启动新进程时，如果 user-data-dir 被现有实例锁住，
    ///            会把 URL 转发给现有实例并退出。所以 Tab 模式必须用独立 profile。
    /// 【本方案】用独立 temp profile，但复制关键文件（Cookies/Login Data/Bookmarks/Local State），
    ///           让新进程能复用现有实例的登录态。
    /// </summary>
    private void CopyBrowserProfile(string srcProfile, string dstProfile, string browser)
    {
        if (string.IsNullOrEmpty(srcProfile) || !Directory.Exists(srcProfile))
        {
            CdpHelper.CdpLog($"现有 profile 不存在，跳过复制: {srcProfile}");
            return;
        }
        try
        {
            // 关键文件列表（按重要性排序）
            // 1. Local State - 全局状态、首选语言
            // 2. Default/Cookies - 登录态（最重要）
            // 3. Default/Login Data - 保存的密码
            // 4. Default/Bookmarks - 收藏
            // 5. Default/Preferences - 偏好设置
            string[] criticalFiles = new[]
            {
                "Local State",
                Path.Combine("Default", "Cookies"),
                Path.Combine("Default", "Cookies-journal"),
                Path.Combine("Default", "Login Data"),
                Path.Combine("Default", "Login Data-journal"),
                Path.Combine("Default", "Bookmarks"),
                Path.Combine("Default", "Preferences"),
                Path.Combine("Default", "Secure Preferences"),
                Path.Combine("Default", "Web Data"),
                Path.Combine("Default", "Web Data-journal"),
            };
            int copiedCount = 0;
            foreach (var relPath in criticalFiles)
            {
                string src = Path.Combine(srcProfile, relPath);
                string dst = Path.Combine(dstProfile, relPath);
                if (!File.Exists(src)) continue;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, overwrite: true);
                    copiedCount++;
                }
                catch (Exception ex)
                {
                    // 现有 Edge 可能锁住文件（如 Cookies.sqlite），跳过不复制
                    CdpHelper.CdpLog($"  跳过（可能被锁）: {relPath} - {ex.Message}");
                }
            }
            CdpHelper.CdpLog($"Tab 模式复制了 {copiedCount} 个关键文件到 temp profile");
        }
        catch (Exception ex)
        {
            CdpHelper.CdpLog($"复制 profile 失败: {ex.Message}");
        }
    }

    private string BuildBrowserArgs(string url, string browser, bool newWindow, int cdpPort = 0, string? cdpUserDataDir = null)
    {
        // CDP 远程调试参数（开启后可被 A3Tools 通过 WebSocket 注入 JS）
        // Edge/Chrome 111+ 都需要 --remote-allow-origins=* 避免 WebSocket Origin 检查报错
        string cdpPart = cdpPort > 0
            ? $" --remote-debugging-port={cdpPort} --remote-allow-origins=*"
            : "";
        // 隔离的 user-data-dir（避免与用户自己的浏览器配置冲突）
        string userDataPart = !string.IsNullOrEmpty(cdpUserDataDir)
            ? $" --user-data-dir=\"{cdpUserDataDir}\""
            : "";
        // 启动最大化：仅新窗口模式生效（Tab 模式走 explorer.exe 跳板不经过这里）
        // Edge/Chrome 都支持 --start-maximized；Firefox -start-maximized
        string maxPart = newWindow ? " --start-maximized" : "";

        if (newWindow)
        {
            return browser switch
            {
                "chrome" => $"--new-window --start-maximized --no-first-run --no-default-browser-check --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding{userDataPart}{cdpPart} \"{url}\"",
                "msedge" => $"--new-window --start-maximized --no-first-run --no-default-browser-check --disable-features=msEdgeFirstRunExperience --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding{userDataPart}{cdpPart} \"{url}\"",
                "firefox" => $"-new-window -start-maximized \"{url}\"",
                "360se" => $"--new-window --start-maximized \"{url}\"",
                _ => $"--new-window --start-maximized{userDataPart}{cdpPart} \"{url}\""
            };
        }
        else
        {
            // Tab 模式：以独占 user-data-dir 启动 + 调试端口 + URL
            // 用独占 user-data-dir 避免单实例转发隔离现有浏览器进程
            return browser switch
            {
                "chrome" => $"--no-first-run --no-default-browser-check --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding{userDataPart}{cdpPart} \"{url}\"",
                "msedge" => $"--no-first-run --no-default-browser-check --disable-features=msEdgeFirstRunExperience --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding{userDataPart}{cdpPart} \"{url}\"",
                "firefox" => $"\"{url}\"",
                "360se" => $"\"{url}\"",
                _ => $"{userDataPart}{cdpPart} \"{url}\""
            };
        }
    }

    /// <summary>
    /// 杀掉所有同类型浏览器进程（Edge/Chrome）
    /// 【保留作为应急方案】一般情况下不需要调用
    /// 原因：Edge 单实例 IPC 按 user-data-dir 隔离，独占目录启动的实例可以正常绑定调试端口
    /// 只有当用户启用了 --remote-debugging-port 的 Edge 已在运行时，新启动实例才会被单实例转发
    /// 以后如果遇到这种边界场景可以调用本方法
    /// </summary>
    private void KillAllBrowserProcesses(string browser)
    {
        try
        {
            string processName = browser switch
            {
                "msedge" => "msedge",
                "chrome" => "chrome",
                _ => ""  // 360/Firefox 不杀
            };
            if (string.IsNullOrEmpty(processName)) return;

            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0)
            {
                CdpHelper.CdpLog($"没有 {processName} 进程，跳过杀进程");
                return;
            }
            CdpHelper.CdpLog($"杀掉 {procs.Length} 个 {processName} 进程");
            foreach (var p in procs)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    CdpHelper.CdpLog($"  杀 PID {p.Id} 失败: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
            // 等进程退出
            Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            CdpHelper.CdpLog($"杀进程异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string? FindBrowserFromRegistry(string browser)
    {
        // 从注册表查找浏览器路径
        string keyPath = browser switch
        {
            "chrome" => @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            "msedge" => @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
            "firefox" => @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\firefox.exe",
            "360se" => @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\sechrome.exe",
            _ => ""
        };

        if (string.IsNullOrEmpty(keyPath)) return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key != null)
                return key.GetValue("") as string;
        }
        catch { }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            if (key != null)
                return key.GetValue("") as string;
        }
        catch { }

        return null;
    }

    private string GetBrowserPath(string browser)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return browser switch
        {
            "chrome" => FindFileInPaths(
                programFiles + @"\Google\Chrome\Application\chrome.exe",
                programFilesX86 + @"\Google\Chrome\Application\chrome.exe"
            ),
            "msedge" => FindFileInPaths(
                // Edge 可能安装在 3 个位置：
                // 1. C:\Program Files (x86)\Microsoft\Edge\... （64位系统上的默认位置）
                // 2. C:\Program Files\Microsoft\Edge\... （少量机器）
                // 3. C:\Users\xxx\AppData\Local\Microsoft\Edge\... （Dev/Canary）
                programFilesX86 + @"\Microsoft\Edge\Application\msedge.exe",
                programFiles + @"\Microsoft\Edge\Application\msedge.exe",
                localAppData + @"\Microsoft\Edge\Application\msedge.exe"
            ),
            "firefox" => FindFileInPaths(
                programFiles + @"\Mozilla Firefox\firefox.exe",
                programFilesX86 + @"\Mozilla Firefox\firefox.exe"
            ),
            "360se" => FindFileInPaths(programFilesX86, @"360safe\sechrome\sechrome.exe"),
            _ => string.Empty
        };
    }

    private string FindFileInPaths(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }
        return string.Empty;
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dialog = new SettingsDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            RegisterAllHotkeys();
        }
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (this.tabControl.SelectedTab == this.tabTools)
            this.scrollPanel?.BringToFront();
        else if (this.tabControl.SelectedTab == this.tabLaunch)
        {
            this.dgvAccounts?.BringToFront();
            FocusSearchBox();
        }
    }

    private void BtnConnectDB_Click(object? sender, EventArgs e)
    {
        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null) return;
        if (string.IsNullOrWhiteSpace(account.Database) || string.IsNullOrWhiteSpace(account.DbUser)) return;

        // 优先使用设置中的SSMS路径
        var settings = new DataService().LoadSettings();
        string ssmsPath;

        if (!string.IsNullOrWhiteSpace(settings.SsmsPath) && File.Exists(settings.SsmsPath))
        {
            ssmsPath = settings.SsmsPath;
        }
        else
        {
            string[] ssmsPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft SQL Server Management Studio 20\Common7\IDE\Ssms.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SQL Server Management Studio 20\Common7\IDE\Ssms.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe")
            };
            ssmsPath = ssmsPaths.FirstOrDefault(File.Exists) ?? "Ssms.exe";
        }

        // 复制数据库密码到剪贴板
        if (!string.IsNullOrEmpty(account.DbPassword))
            Clipboard.SetText(account.DbPassword);

        string dbName = string.IsNullOrWhiteSpace(account.DatabaseName) ? "master" : account.DatabaseName;
        string args = $"-S \"{account.Database}\" -d {dbName} -U {account.DbUser}";

        try
        {
            var p = Process.Start(new ProcessStartInfo { FileName = ssmsPath, Arguments = args, UseShellExecute = true });
            if (p != null)
            {
                _processIds.Add(p.Id);
                RecordProcess(account.Code, p.Id, "db");
            }
        }
        catch
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c start \"\" \"{ssmsPath}\" {args}", UseShellExecute = false, CreateNoWindow = true });
                if (p != null)
                {
                    _processIds.Add(p.Id);
                    RecordProcess(account.Code, p.Id, "db");
                }
            }
            catch { }
        }
    }

    private void BtnRemote_Click(object? sender, EventArgs e)
    {
        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null) return;
        if (string.IsNullOrWhiteSpace(account.RemoteAddress)) return;

        string remoteType = account.RemoteType ?? "";

        if (remoteType == "RDP")
        {
            try
            {
                string rdpContent = $"full address:s:{account.RemoteAddress}\nusername:s:{account.RemoteUser}";
                string tempRdp = Path.Combine(Path.GetTempPath(), $"remote_{DateTime.Now.Ticks}.rdp");
                File.WriteAllText(tempRdp, rdpContent);
                var p = Process.Start(new ProcessStartInfo { FileName = "mstsc.exe", Arguments = $"\"{tempRdp}\"", UseShellExecute = true });
                if (p != null)
                {
                    _processIds.Add(p.Id);
                    RecordProcess(account.Code, p.Id, "remote");
                }
                if (!string.IsNullOrEmpty(account.RemotePassword))
                    Clipboard.SetText(account.RemotePassword);
            }
            catch { }
        }
        else if (remoteType == "向日葵")
        {
            string[] sunflowerPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Oray\SunLogin\SunloginClient.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Oray\SunLogin\SunloginClient.exe")
            };
            string sunflowerPath = sunflowerPaths.FirstOrDefault(File.Exists) ?? "";
            if (!string.IsNullOrEmpty(sunflowerPath))
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo { FileName = sunflowerPath, Arguments = $"--type=remote --code={account.RemoteAddress}", UseShellExecute = true });
                    if (p != null)
                    {
                        _processIds.Add(p.Id);
                        RecordProcess(account.Code, p.Id, "remote");
                    }
                }
                catch { }
            }
        }
        else if (remoteType == "其他")
        {
            Clipboard.SetText(account.RemoteAddress);
        }
    }

    private void LoadPlugins()
    {
        _plugins.Clear();
        this.flpTools.Controls.Clear();

        // 只加载配置化工具（禁用旧版IPlugin兼容加载）
        LoadTools();
    }

    private void LoadExternalPlugins()
    {
        // 优先使用 AppContext.BaseDirectory（.NET 6+，单文件发布下指向 exe 真实目录）
        // 回退到 AppDomain.BaseDirectory
        string baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(Path.Combine(baseDir, "Plugins")))
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
        }
        string pluginDir = Path.Combine(baseDir, "Plugins");
        if (!Directory.Exists(pluginDir)) return;

        foreach (var file in Directory.GetFiles(pluginDir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(file);
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
                    {
                        var plugin = (IPlugin?)Activator.CreateInstance(type);
                        if (plugin != null)
                        {
                            _plugins.Add(plugin);
                            var btn = CreateToolCard("🔌", plugin.Name, plugin.Description);
                            btn.Click += (s, e) =>
                            {
                                var account = this.dgvAccounts.SelectedRows.Count > 0
                                    ? this.dgvAccounts.SelectedRows[0].DataBoundItem as Account : null;
                                plugin.Execute(account);
                            };
                            this.flpTools.Controls.Add(btn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载插件失败: {file} - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载配置化工具（从tools.json读取）
    /// </summary>
    private void LoadTools()
    {
        _toolExecutorService.LoadTools(_toolsConfigService, this);

        foreach (var tool in _toolExecutorService.Tools)
        {
            var btn = CreateToolCard("🔧", tool.Config.Name, tool.Config.Description);
            var loadedTool = tool;
            btn.Click += (s, e) =>
            {
                var account = GetSelectedAccount();
                _toolExecutorService.ExecuteTool(loadedTool, account, this);
            };
            this.flpTools.Controls.Add(btn);
        }

        this.lblPluginStatus.Text = $"已加载 {_toolExecutorService.Tools.Count} 个工具";
    }

    private Button CreateToolCard(string icon, string name, string description)
    {
        var btn = new Button
        {
            Text = $"{icon}  {name}\n{description}",
            Size = new System.Drawing.Size(200, 65),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 9F),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(8),
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(220, 220, 220);
        btn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(240, 248, 255);
        btn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(230, 240, 250);
        return btn;
    }

    #region Root模式

    private void LblTitle_Click(object? sender, EventArgs e)
    {
        // Root 模式下单击标题直接弹退出确认
        if (_isRootMode)
        {
            ConfirmExitRootMode();
            return;
        }

        // 非 Root 模式：连续点击标题 5 次触发 Root 模式
        var now = DateTime.Now;
        if ((now - _lastTitleClickTime).TotalSeconds > 3)
        {
            _titleClickCount = 0;
        }
        _lastTitleClickTime = now;
        _titleClickCount++;

        if (_titleClickCount >= 5)
        {
            ShowRootPasswordDialog();
        }
    }

    private void ConfirmExitRootMode()
    {
        var result = MessageBox.Show(
            "确认要退出 Root 模式吗？\n退出后将无法查看和复制明文密码。",
            "退出 Root 模式",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.OK)
        {
            _isRootMode = false;
            _titleClickCount = 0;
            UpdateRootModeUI();
        }
    }

    private void ShowRootPasswordDialog()
    {
        var dialog = new Form
        {
            Text = "Root模式验证",
            Size = new System.Drawing.Size(400, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Text = "请输入Root密码：", Location = new System.Drawing.Point(30, 25), AutoSize = true };
        var txtPassword = new TextBox { Location = new System.Drawing.Point(30, 55), Width = 320, UseSystemPasswordChar = true };
        var btnOk = new Button { Text = "确定", Location = new System.Drawing.Point(100, 110), Width = 90, Height = 35 };
        var btnCancel = new Button { Text = "取消", Location = new System.Drawing.Point(210, 110), Width = 90, Height = 35 };

        btnOk.Click += (s, e) =>
        {
            if (txtPassword.Text == ROOT_PASSWORD)
            {
                _isRootMode = true;
                _titleClickCount = 0;
                UpdateRootModeUI();
                dialog.Close();
                MessageBox.Show("Root模式已开启！\n可查看和复制密码。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("密码错误！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPassword.Clear();
            }
        };
        btnCancel.Click += (s, e) => dialog.Close();
        txtPassword.KeyDown += (s, e) => { if (e.KeyValue == 13) btnOk.PerformClick(); };

        dialog.Controls.AddRange(new Control[] { lbl, txtPassword, btnOk, btnCancel });
        dialog.ShowDialog(this);
    }

    private void UpdateRootModeUI()
    {
        if (_isRootMode)
        {
            // Root模式下标题显示特殊标识（带小锁提示用户点这里退出）
            this.lblTitle.Text = "🔓 A3工具箱 (Root)";
            this.lblTitle.ForeColor = Color.Yellow;
            this.menuCopyAccount.Visible = true;
        }
        else
        {
            this.lblTitle.Text = "A3工具箱";
            this.lblTitle.ForeColor = Color.White;
            this.menuCopyAccount.Visible = false;
        }

        // 切换 Root 模式后重绘账套列表，重新走 CellFormatting 决定是否脱敏
        if (this.dgvAccounts != null && !this.dgvAccounts.IsDisposed)
        {
            this.dgvAccounts.Invalidate();
        }
    }

    #endregion

    #region 菜单事件

    private void MenuCopyAccount_Click(object? sender, EventArgs e)
    {
        // 确保选中了一行
        if (this.dgvAccounts.SelectedRows.Count == 0)
        {
            // 尝试点击当前单元格所在行
            if (this.dgvAccounts.CurrentRow != null)
            {
                this.dgvAccounts.CurrentRow.Selected = true;
            }
        }

        if (this.dgvAccounts.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先在列表中选择要复制的账套！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null)
        {
            MessageBox.Show("无法获取选中的账套信息！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        CopyAccountInfo(account);
    }

    private void CopyAccountInfo(Account account)
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"代码：{account.Code}");
        info.AppendLine($"名称：{account.Name}");
        info.AppendLine($"账套地址：{account.Server}");
        info.AppendLine($"备用地址：{account.ServerBackup}");
        info.AppendLine($"账套用户名：{account.ServerUsername}");
        info.AppendLine($"账套密码：{account.ServerPassword}");
        info.AppendLine($"数据库地址：{account.Database}");
        info.AppendLine($"数据库名称：{account.DatabaseName}");
        info.AppendLine($"DB用户：{account.DbUser}");
        info.AppendLine($"DB密码：{account.DbPassword}");
        info.AppendLine($"远程方式：{account.RemoteType}");
        info.AppendLine($"远程地址：{account.RemoteAddress}");
        info.AppendLine($"远程用户：{account.RemoteUser}");
        info.AppendLine($"远程密码：{account.RemotePassword}");

        Clipboard.SetText(info.ToString());
        MessageBox.Show("账套信息已复制到剪贴板！" + (_isRootMode ? "（包含明文密码）" : ""), "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Root模式快捷键复制：不弹窗，用Toast提示2秒后自动消失
    /// </summary>
    private void CopySelectedAccountSilently()
    {
        // 获取当前选中行
        if (this.dgvAccounts.SelectedRows.Count == 0)
        {
            if (this.dgvAccounts.CurrentRow != null && this.dgvAccounts.CurrentRow.Index >= 0)
                this.dgvAccounts.Rows[this.dgvAccounts.CurrentRow.Index].Selected = true;
        }

        if (this.dgvAccounts.SelectedRows.Count == 0) return;


        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account == null) return;


        var info = new System.Text.StringBuilder();
        info.AppendLine($"代码：{account.Code}");
        info.AppendLine($"名称：{account.Name}");
        info.AppendLine($"账套地址：{account.Server}");
        info.AppendLine($"备用地址：{account.ServerBackup}");
        info.AppendLine($"账套用户名：{account.ServerUsername}");
        info.AppendLine($"账套密码：{account.ServerPassword}");
        info.AppendLine($"数据库地址：{account.Database}");
        info.AppendLine($"数据库名称：{account.DatabaseName}");
        info.AppendLine($"DB用户：{account.DbUser}");
        info.AppendLine($"DB密码：{account.DbPassword}");
        info.AppendLine($"远程方式：{account.RemoteType}");
        info.AppendLine($"远程地址：{account.RemoteAddress}");
        info.AppendLine($"远程用户：{account.RemoteUser}");
        info.AppendLine($"远程密码：{account.RemotePassword}");

        Clipboard.SetText(info.ToString());
        ShowToast("账套信息已复制（包含明文密码）");
    }

    /// <summary>
    /// 显示Toast提示，2秒后自动消失
    /// </summary>
    private void ShowToast(string message, int durationMs = 2000)
    {
        var toast = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(40, 40, 40),
            Size = new Size(320, 40),
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            TopMost = true
        };

        // 定位到主窗体下方居中
        var mainPos = this.Bounds;
        toast.Location = new Point(
            mainPos.Left + (mainPos.Width - toast.Width) / 2,
            mainPos.Top + mainPos.Height - toast.Height - 60
        );

        var lbl = new Label
        {
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font("微软雅黑", 10F)
        };
        toast.Controls.Add(lbl);


        toast.Show();
        var timer = new System.Windows.Forms.Timer { Interval = durationMs };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            timer.Dispose();
            toast.Close();
            toast.Dispose();
        };
        timer.Start();
    }

    private void MenuExit_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    private void MenuAbout_Click(object? sender, EventArgs e)
    {
        MessageBox.Show("A3工具箱 v1.2.0\n\n一个用于管理A3账套的桌面工具。\n\n包含账套管理、一键启动、数据库连接、远程访问等功能。", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MenuHotkeySettings_Click(object? sender, EventArgs e)
    {
        using var form = new HotkeySettingsForm();
        if (form.ShowDialog() == DialogResult.OK)
            RegisterAllHotkeys();
    }

    #endregion

    #region 账套运行状态

    private void LoadAccountStatuses()
    {
        // 初始为空，只有运行了才添加
        _accountStatuses.Clear();
        RefreshStatusGrid();
    }

    private void RefreshAccountStatuses()
    {
        // 清理已结束的进程，更新显示
        var deadCodes = new List<string>();
        foreach (var kvp in _accountStatuses)
        {
            var deadPids = new List<int>();
            foreach (var pid in kvp.Value.ProcessIds)
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    if (p.HasExited)
                        deadPids.Add(pid);
                }
                catch
                {
                    deadPids.Add(pid);
                }
            }
            foreach (var pid in deadPids)
                kvp.Value.ProcessIds.Remove(pid);

            if (kvp.Value.ProcessIds.Count == 0)
                deadCodes.Add(kvp.Key);
        }

        // 移除没有进程的账套
        foreach (var code in deadCodes)
            _accountStatuses.Remove(code);

        RefreshStatusGrid();
    }

    private void RefreshStatusGrid()
    {
        if (dgvStatus == null || dgvStatus.IsDisposed) return;

        var statusList = _accountStatuses.Values.ToList();

        // 移除旧事件
        dgvStatus.CellFormatting -= DgvStatus_CellFormatting;
        dgvStatus.CellContentClick -= DgvStatus_CellContentClick;

        dgvStatus.DataSource = null;
        dgvStatus.Columns.Clear();

        // 设置列
        var cols = new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "账套代码", DataPropertyName = "Code", Name = "ColCode", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewTextBoxColumn { HeaderText = "账套名称", DataPropertyName = "Name", Name = "ColName", FillWeight = 30, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
            new DataGridViewCheckBoxColumn { HeaderText = "Web", DataPropertyName = "IsWebRunning", Name = "ColWeb", Width = 60, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true },
            new DataGridViewCheckBoxColumn { HeaderText = "客户端", DataPropertyName = "IsClientRunning", Name = "ColClient", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true },
            new DataGridViewCheckBoxColumn { HeaderText = "开发工具", DataPropertyName = "IsDevToolsRunning", Name = "ColDev", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true },
            new DataGridViewCheckBoxColumn { HeaderText = "数据库", DataPropertyName = "IsDbConnected", Name = "ColDb", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true },
            new DataGridViewCheckBoxColumn { HeaderText = "远程连接", DataPropertyName = "IsRemoteConnected", Name = "ColRemote", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "进程数", Name = "ColPidCount", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
            new DataGridViewButtonColumn { HeaderText = "操作", Name = "ColAction", Text = "关闭", UseColumnTextForButtonValue = true, Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None }
        };

        dgvStatus.Columns.AddRange(cols);
        dgvStatus.DataSource = statusList;

        // 绑定事件
        dgvStatus.CellFormatting += DgvStatus_CellFormatting;
        dgvStatus.CellContentClick += DgvStatus_CellContentClick;
    }

    private void DgvStatus_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= dgvStatus.Rows.Count) return;
        if (dgvStatus.Columns[e.ColumnIndex].Name == "ColPidCount")
        {
            var status = dgvStatus.Rows[e.RowIndex].DataBoundItem as AccountStatus;
            if (status != null)
                e.Value = status.ProcessIds.Count.ToString();
        }
    }

    private void DgvStatus_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= dgvStatus.Rows.Count) return;
        if (dgvStatus.Columns[e.ColumnIndex].Name == "ColAction")
        {
            var status = dgvStatus.Rows[e.RowIndex].DataBoundItem as AccountStatus;
            if (status != null && status.ProcessIds.Count > 0)
            {
                CloseAccountProcesses(status);
            }
        }
    }


    /// <summary>
    /// 获取现有浏览器进程的 PID 列表
    /// </summary>
    private HashSet<int> GetExistingBrowserPids(string browser)
    {
        var pids = new HashSet<int>();
        try
        {
            var procName = browser == "msedge" ? "msedge" : (browser == "chrome" ? "chrome" : browser);
            foreach (var p in Process.GetProcessesByName(procName))
            {
                pids.Add(p.Id);
            }
        }
        catch { }
        return pids;
    }

    /// <summary>
    /// 获取新增的浏览器进程 PID（排除已存在的）
    /// </summary>
    private List<int> GetNewBrowserPids(string browser, HashSet<int> existingPids)
    {
        var newPids = new List<int>();
        try
        {
            var procName = browser == "msedge" ? "msedge" : (browser == "chrome" ? "chrome" : browser);
            foreach (var p in Process.GetProcessesByName(procName))
            {
                if (!existingPids.Contains(p.Id))
                {
                    newPids.Add(p.Id);
                }
            }
        }
        catch { }
        return newPids;
    }

    private void CloseAccountProcesses(AccountStatus status)
    {
        if (status.ProcessIds.Count == 0) return;

        var result = MessageBox.Show($"确定要关闭账套【{status.Name}】的所有进程吗？\n共 {status.ProcessIds.Count} 个进程。",
            "确认关闭", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        foreach (var pid in status.ProcessIds.ToList())
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (p != null && !p.HasExited)
                {
                    try
                    {
                        // Edge/Chrome 等浏览器是多进程结构，只杀主 PID 可能留下窗口进程。
                        // 使用进程树关闭，确保账套运行情况中的“关闭”能真正关掉新增浏览器窗口。
                        p.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        p.Kill();
                    }
                }
            }
            catch { }
            _processIds.Remove(pid);
            _processLaunchModes.Remove(pid);
        }

        status.ProcessIds.Clear();
        _accountStatuses.Remove(status.Code);
        RefreshStatusGrid();
    }

    /// <summary>
    /// 尝试优雅关闭浏览器标签页（保留窗口）
    /// </summary>
    private bool TryCloseBrowserTab(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            if (p == null || p.HasExited) return true;
            // 先尝试发送 CloseMainWindow（相当于点击浏览器窗口的关闭按钮）
            if (p.CloseMainWindow())
            {
                p.WaitForExit(500);
                return !p.HasExited;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 记录账套启动的进程
    /// </summary>
    public void RecordProcess(string code, int processId, string processType = "")
    {
        // 如果账套不在列表中，先获取账套名称
        if (!_accountStatuses.ContainsKey(code))
        {
            var accounts = _dataService.LoadAccounts();
            var acc = accounts.FirstOrDefault(a => a.Code == code);
            _accountStatuses[code] = new AccountStatus
            {
                Code = code,
                Name = acc?.Name ?? code
            };
        }

        if (!_accountStatuses[code].ProcessIds.Contains(processId))
            _accountStatuses[code].ProcessIds.Add(processId);

        // 设置运行状态
        var status = _accountStatuses[code];
        switch (processType.ToLower())
        {
            case "web":
                status.IsWebRunning = true;
                break;
            case "client":
                status.IsClientRunning = true;
                break;
            case "dev":
                status.IsDevToolsRunning = true;
                break;
            case "db":
                status.IsDbConnected = true;
                break;
            case "remote":
                status.IsRemoteConnected = true;
                break;
        }

        RefreshStatusGrid();
    }

    /// <summary>
    /// 按账套 Code + 进程类型（如 "client" / "dev"）查找仍存活的进程 ID 列表。
    /// 自动过滤已退出的进程（GetProcessById 抛异常 = 已死），并同步清理死进程 + 对应的 ProcessIds。
    /// 用于「按账套判断是否已启动」场景：A3 客户端/开发工具进程名都一样，必须按账套 Code 区分。
    /// </summary>
    /// <param name="code">账套代号</param>
    /// <param name="processType">进程类型："client" / "dev" / "web" / "db" / "remote"</param>
    /// <returns>仍存活的 PID 列表（空 = 该账套未启动此类型进程）</returns>
    private List<int> GetActiveAccountProcessIds(string code, string processType)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(code) || !_accountStatuses.ContainsKey(code)) return result;

        var status = _accountStatuses[code];
        var dead = new List<int>();

        foreach (var pid in status.ProcessIds)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (p != null && !p.HasExited)
                {
                    // 仅收集匹配类型的存活进程（用 AccountStatus 上的 bool 标记判断更稳）
                    bool typeMatch = processType switch
                    {
                        "client" => status.IsClientRunning,
                        "dev" => status.IsDevToolsRunning,
                        "web" => status.IsWebRunning,
                        "db" => status.IsDbConnected,
                        "remote" => status.IsRemoteConnected,
                        _ => true, // 未指定类型时不过滤
                    };
                    if (typeMatch) result.Add(pid);
                }
                else
                {
                    dead.Add(pid);
                }
            }
            catch
            {
                // GetProcessById 进程不存在时抛 ArgumentException → 视为已退出
                dead.Add(pid);
            }
        }

        // 顺手清掉死进程，避免列表越长越脏
        foreach (var pid in dead)
        {
            status.ProcessIds.Remove(pid);
            _processIds.Remove(pid);
            _processLaunchModes.Remove(pid);
        }

        return result;
    }

    /// <summary>
    /// 尝试把指定账套已启动的某类进程全部切到前台。
    /// 多个实例时只切第一个存活实例；都死了视为「未启动」，返回 false 让调用方走启动流程。
    /// </summary>
    private bool TryBringAccountProcessesToFront(string code, string processType)
    {
        var pids = GetActiveAccountProcessIds(code, processType);
        if (pids.Count == 0) return false;

        int pid = pids[0];
        if (Win32AutoLoginHelper.BringProcessByIdToFront(pid))
        {
            return true;
        }

        // 切前台失败（可能窗口已最小化到不显示/或窗口已销毁），把死 PID 清掉
        if (_accountStatuses.TryGetValue(code, out var status))
            status.ProcessIds.Remove(pid);
        _processIds.Remove(pid);
        _processLaunchModes.Remove(pid);
        return false;
    }

    #endregion

    #region 托盘相关

    /// <summary>
    /// 隐藏到系统托盘
    /// </summary>
    public void HideToTray()
    {
        if (_isHiddenToTray) return;

        // 使用最小化 + ShowInTaskbar=false 实现托盘隐藏
        // 这样点击任务栏图标仍然可以恢复（只是图标变灰或看不见）
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.notifyIcon.Visible = true;
        _isHiddenToTray = true;

        // 更新托盘菜单文字
        this.menuHide.Text = "显示主窗体";
    }

    /// <summary>
    /// 从托盘显示窗体
    /// </summary>
    public void ShowFromTray()
    {
        if (!_isHiddenToTray) return;

        this.WindowState = FormWindowState.Normal;
        this.ShowInTaskbar = true;
        this.Show();
        this.notifyIcon.Visible = false;
        _isHiddenToTray = false;

        _edgeDockManager?.RecordShowTime();
        RegisterAllHotkeys();
        this.menuHide.Text = "隐藏到托盘";

        // 从托盘恢复时强制拉到最前端（避免需要手动点任务栏）
        ForceForegroundWindow(this.Handle);
    }

    /// <summary>
    /// 强制将指定窗口拉到最前端（突破 Windows 不许后台进程抢焦点的限制）。
    /// 实现思路：
    ///   1. 先调用 AllowSetForegroundWindow 让「当前前台窗口的进程」允许我们抢焦点
    ///   2. 再调用 SetForegroundWindow + BringToFront + Activate
    /// Win32 限制：Vista 之后默认不允许后台进程强行 SetForegroundWindow，
    /// 必须先调 AllowSetForegroundWindow(前台进程 ID) 才能拿到焦点。
    /// </summary>
    private void ForceForegroundWindow(IntPtr hWnd)
    {
        try
        {
            // 拿到当前前台窗口的进程 ID
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                AllowSetForegroundWindow((int)fgPid);
            }
            SetForegroundWindow(hWnd);
            this.BringToFront();
            this.Activate();
        }
        catch
        {
            // 退化路径：仅走 WinForms 自带的 Activate
            this.Activate();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    /// <summary>
    /// 拦截窗体消息，处理托盘模式下的最小化
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.WindowState = FormWindowState.Minimized;
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        // 当隐藏到托盘时，拦截最小化消息，直接隐藏到托盘
        if (m.Msg == WM_SYSCOMMAND && m.WParam.ToInt32() == SC_MINIMIZE)
        {
            if (!_isHiddenToTray)
            {
                // 正常最小化
                base.WndProc(ref m);
            }
            else
            {
                // 托盘模式下最小化，什么都不做，保持托盘状态
                // 或者可以恢复显示
                ShowFromTray();
            }
            return;
        }

        // 拦截恢复消息（当任务栏图标被点击时）
        if (m.Msg == WM_SYSCOMMAND && m.WParam.ToInt32() == SC_RESTORE)
        {
            if (_isHiddenToTray)
            {
                ShowFromTray();
                return;
            }
        }

        base.WndProc(ref m);
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void MenuShow_Click(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void MenuHide_Click(object? sender, EventArgs e)
    {
        if (_isHiddenToTray)
            ShowFromTray();
        else
            HideToTray();
    }

    private void MenuTrayExit_Click(object? sender, EventArgs e)
    {
        // 退出程序
        _edgeDockManager?.Dispose();
        notifyIcon.Visible = false;
        Application.Exit();
    }

    #endregion
}
