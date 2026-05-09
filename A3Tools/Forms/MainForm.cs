using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class MainForm : Form, IToolContext
{
    private readonly DataService _dataService;
    private readonly ToolsConfigService _toolsConfigService;
    private readonly ToolExecutorService _toolExecutorService;
    private readonly List<IPlugin> _plugins = new();
    private readonly List<int> _processIds = new();
    private readonly Dictionary<string, AccountStatus> _accountStatuses = new();
    private EdgeDockManager? _edgeDockManager;
    private HotkeyManager? _hotkeyManager;
    private bool _isInitializing = true;
    private bool _isRootMode = false;
    private int _titleClickCount = 0;
    private DateTime _lastTitleClickTime = DateTime.MinValue;
    private const string ROOT_PASSWORD = "xiaopacai"; // Root模式密码
    private bool _isHiddenToTray = false; // 是否已隐藏到托盘
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_RESTORE = 0xF120;

    public MainForm()
    {
        _dataService = new DataService();
        _toolsConfigService = new ToolsConfigService();
        _toolExecutorService = new ToolExecutorService();
        // 启动时更新所有现有账套的拼音
        _dataService.UpdateAllPinyin();
        InitializeComponent();
        WireUpEvents();
        LoadPlugins();
        LoadAccounts();
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
    }

    private void InitHotkey()
    {
        _hotkeyManager = new HotkeyManager();
        RegisterTrayHotkey();
    }

    /// <summary>
    /// 注册托盘显示快捷键
    /// </summary>
    public void RegisterTrayHotkey()
    {
        var settings = _dataService.LoadSettings();
        if (!string.IsNullOrEmpty(settings.TrayShowHotkey))
        {
            _hotkeyManager?.UnregisterCurrentHotkey();
            if (_hotkeyManager?.RegisterHotkey(settings.TrayShowHotkey) == true)
            {
                // 订阅快捷键事件
                _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            }
        }
        else
        {
            _hotkeyManager?.UnregisterCurrentHotkey();
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        // 在UI线程上执行
        this.BeginInvoke(new Action(() => ShowFromTray()));
    }

    private void MainForm_ResizeForMinimize(object? sender, EventArgs e)
    {
        // 如果窗体已隐藏到托盘，点击最小化按钮时恢复正常显示
        if (_isHiddenToTray && this.WindowState == FormWindowState.Minimized)
        {
            ShowFromTray();
        }
    }

    private void SetTrayIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "A3Tool.ico");
            if (File.Exists(iconPath))
            {
                notifyIcon.Icon = new Icon(iconPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载托盘图标失败: {ex.Message}");
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
        this.btnAdd.Click += BtnAdd_Click;
        this.btnImport.Click += BtnImport_Click;
        this.btnEdit.Click += BtnEdit_Click;
        this.btnDelete.Click += BtnDelete_Click;
        this.btnRefresh.Click += BtnRefresh_Click;
        this.btnLaunch.Click += BtnLaunch_Click;
        this.btnSettings.Click += BtnSettings_Click;
        this.btnConnectDB.Click += BtnConnectDB_Click;
        this.btnRemote.Click += BtnRemote_Click;
        this.tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        this.dgvAccounts.DoubleClick += DgvAccounts_DoubleClick;
        this.dgvAccounts.ColumnWidthChanged += DgvAccounts_ColumnWidthChanged;
        this.menuCopyAccount.Click += MenuCopyAccount_Click;
        this.menuAbout.Click += MenuAbout_Click;
        this.lblTitle.Click += LblTitle_Click;
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
    }

    private void BtnAdd_Click(object? sender, EventArgs e) => ShowAccountDialog(null);
    private void BtnImport_Click(object? sender, EventArgs e) => ImportFromXml();

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
                string server = ztaddr.Replace("http://","").Replace("https://","").Replace("/","");
                string dbAddr = sqladdr.Split(' ')[0].Trim();
                string dbUser = usercode.Split(' ')[0].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(server)) continue;
                string serverUrl = ztaddr.EndsWith("/") ? ztaddr : ztaddr + "/";
                // 保存原始XML行到备注
                string remark = block.Trim();
                accounts.Add(new Account
                {
                    Code = "", Name = name, Pinyin = PinyinHelper.GetPinyinInitial(name), Server = serverUrl, ServerPassword = ztpwd,
                    Database = dbAddr, DatabaseName = "", DbUser = dbUser, DbPassword = userpwd,
                    RemoteType = "", RemoteAddress = "", RemoteUser = "", RemotePassword = "", Remark = remark
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
    private void BtnRefresh_Click(object? sender, EventArgs e) => LoadAccounts();
    private void BtnLaunch_Click(object? sender, EventArgs e) => LaunchSelectedAccount();
    private void DgvAccounts_DoubleClick(object? sender, EventArgs e) => EditSelectedAccount();

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
            using var dialog = new LaunchOptionsDialog(settings.LaunchDesktop, settings.LaunchDevTools, settings.LaunchWeb, settings.SelectedBrowser);
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
                var p = Process.Start(new ProcessStartInfo { FileName = exe1, WorkingDirectory = appDir, UseShellExecute = true });
                if (p != null) 
                {
                    _processIds.Add(p.Id);
                    RecordProcess(account.Code, p.Id, "client");
                }
            }
        }

        if (settings.LaunchDevTools)
        {
            string exe2 = Path.Combine(appDir, "君则A3集成开发工具.exe");
            if (File.Exists(exe2))
            {
                var p = Process.Start(new ProcessStartInfo { FileName = exe2, WorkingDirectory = appDir, UseShellExecute = true });
                if (p != null) 
                {
                    _processIds.Add(p.Id);
                    RecordProcess(account.Code, p.Id, "dev");
                }
            }
        }

        if (settings.LaunchWeb && !string.IsNullOrEmpty(account.Server))
        {
            string url = account.Server.TrimEnd('/') + "/h5comerp/#/login";
            LaunchWebBrowser(url, settings.SelectedBrowser, account.Code);
        }
    }

    private void LaunchWebBrowser(string url, string browser, string accountCode)
    {
        var settings = _dataService.LoadSettings();
        bool newWindow = settings.BrowserNewWindow;
        string browserPath = GetBrowserPath(browser);

        if (!string.IsNullOrEmpty(browserPath) && File.Exists(browserPath))
        {
            // 根据浏览器类型和设置使用不同的启动参数
            string args;
            if (newWindow)
            {
                // 新窗口模式
                args = browser switch
                {
                    // Chrome 效能优化参数
                    "chrome" => $"--new-window --no-first-run --no-default-browser-check --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding \"{url}\"",
                    // Edge 使用简单参数（Edge对参数更严格）
                    "msedge" => $"--new-window \"{url}\"",
                    // Firefox 参数
                    "firefox" => $"-new-window \"{url}\"",
                    // 360浏览器
                    "360se" => $"--new-window \"{url}\"",
                    // 其他浏览器使用默认参数
                    _ => $"--new-window \"{url}\""
                };
            }
            else
            {
                // 当前Tab模式
                args = browser switch
                {
                    "chrome" => $"\"{url}\"",
                    "msedge" => $"\"{url}\"",
                    "firefox" => $"\"{url}\"",
                    "360se" => $"\"{url}\"",
                    _ => $"\"{url}\""
                };
            }

            // Edge 和 Firefox 使用 ShellExecute 更可靠
            bool useShellExecute = browser == "msedge" || browser == "firefox";

            var startInfo = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = args,
                UseShellExecute = useShellExecute,
                CreateNoWindow = !useShellExecute
            };
            try
            {
                var p = Process.Start(startInfo);
                if (p != null)
                {
                    _processIds.Add(p.Id);
                    RecordProcess(accountCode, p.Id, "web");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动{browser}失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            // 浏览器未找到，尝试从注册表查找
            string? foundPath = FindBrowserFromRegistry(browser);
            if (!string.IsNullOrEmpty(foundPath) && File.Exists(foundPath))
            {
                // 使用注册表找到的路径启动
                string args = BuildBrowserArgs(url, browser, newWindow);
                bool useShellExecute = browser == "msedge" || browser == "firefox";
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = foundPath,
                        Arguments = args,
                        UseShellExecute = useShellExecute,
                        CreateNoWindow = !useShellExecute
                    };
                    var p = Process.Start(startInfo);
                    if (p != null)
                    {
                        _processIds.Add(p.Id);
                        RecordProcess(accountCode, p.Id, "web");
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

    private string BuildBrowserArgs(string url, string browser, bool newWindow)
    {
        if (newWindow)
        {
            return browser switch
            {
                "chrome" => $"--new-window --no-first-run --no-default-browser-check --disable-extensions --disable-background-networking --disable-sync --disable-translate --disable-background-timer-throttling --disable-renderer-backgrounding \"{url}\"",
                "msedge" => $"--new-window \"{url}\"",
                "firefox" => $"-new-window \"{url}\"",
                "360se" => $"--new-window \"{url}\"",
                _ => $"--new-window \"{url}\""
            };
        }
        else
        {
            return $"\"{url}\"";
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
            "chrome" => FindFileInPaths(programFiles, @"Google\Chrome\Application\chrome.exe"),
            "msedge" => FindFileInPaths(
                programFiles + @"\Microsoft\Edge\Application\msedge.exe",
                localAppData + @"\Microsoft\Edge\Application\msedge.exe"
            ),
            "firefox" => FindFileInPaths(programFiles, @"Mozilla Firefox\firefox.exe"),
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
            // 重新注册快捷键（如果设置改变了）
            RegisterTrayHotkey();
        }
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (this.tabControl.SelectedTab == this.tabTools)
            this.scrollPanel?.BringToFront();
        else if (this.tabControl.SelectedTab == this.tabLaunch)
            this.dgvAccounts?.BringToFront();
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
        string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
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
        // 连续点击标题5次触发Root模式
        var now = DateTime.Now;
        if ((now - _lastTitleClickTime).TotalSeconds > 3)
        {
            _titleClickCount = 0;
        }
        _lastTitleClickTime = now;
        _titleClickCount++;

        if (_titleClickCount >= 5 && !_isRootMode)
        {
            ShowRootPasswordDialog();
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
            // Root模式下标题显示特殊标识
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

    private void MenuExit_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    private void MenuAbout_Click(object? sender, EventArgs e)
    {
        MessageBox.Show("A3工具箱 v1.1.0\n\n一个用于管理A3账套的桌面工具。\n\n包含账套管理、一键启动、数据库连接、远程访问等功能。", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (!p.HasExited)
                    p.Kill();
            }
            catch { }
        }
        
        status.ProcessIds.Clear();
        _accountStatuses.Remove(status.Code);
        RefreshStatusGrid();
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

        // 先设置 WindowState 再 Show，避免状态冲突
        this.WindowState = FormWindowState.Normal;
        this.ShowInTaskbar = true;
        this.Show();
        this.notifyIcon.Visible = false;
        _isHiddenToTray = false;

        // 通知 EdgeDockManager 记录显示时间，防止立即被隐藏
        _edgeDockManager?.RecordShowTime();

        // 重新注册快捷键（窗体重新显示后需要重新注册）
        RegisterTrayHotkey();

        // 更新托盘菜单文字
        this.menuHide.Text = "隐藏到托盘";

        this.Activate();
    }

    /// <summary>
    /// 拦截窗体消息，处理托盘模式下的最小化
    /// </summary>
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
