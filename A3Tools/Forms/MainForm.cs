using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class MainForm : Form
{
    private readonly DataService _dataService;
    private readonly List<IPlugin> _plugins = new();
    private readonly List<int> _processIds = new();

    public MainForm()
    {
        _dataService = new DataService();
        InitializeComponent();
        WireUpEvents();
        LoadPlugins();
        LoadAccounts();
        this.scrollPanel?.BringToFront();
    }

    private void WireUpEvents()
    {
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
        this.btnClose.Click += BtnClose_Click;
        this.tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        this.dgvAccounts.DoubleClick += DgvAccounts_DoubleClick;
    }

    private void LoadAccounts()
    {
        var accounts = _dataService.LoadAndDecryptAccounts();
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
        this.dgvAccounts.Columns.Clear();
        var cols = new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { HeaderText = "代码", DataPropertyName = "Code", Width = 80, Name = "ColCode" },
            new DataGridViewTextBoxColumn { HeaderText = "账套名称", DataPropertyName = "Name", Width = 150, Name = "ColName" },
            new DataGridViewTextBoxColumn { HeaderText = "账套地址", DataPropertyName = "Server", Width = 200, Name = "ColServer" },
            new DataGridViewTextBoxColumn { HeaderText = "数据库地址", DataPropertyName = "Database", Width = 150, Name = "ColDatabase" },
            new DataGridViewTextBoxColumn { HeaderText = "数据库名称", DataPropertyName = "DatabaseName", Width = 120, Name = "ColDatabaseName" },
            new DataGridViewTextBoxColumn { HeaderText = "DB用户", DataPropertyName = "DbUser", Width = 100, Name = "ColDbUser" },
            new DataGridViewTextBoxColumn { HeaderText = "远程方式", DataPropertyName = "RemoteType", Width = 80, Name = "ColRemoteType" },
            new DataGridViewTextBoxColumn { HeaderText = "远程地址", DataPropertyName = "RemoteAddress", Width = 180, Name = "ColRemoteAddress" },
            new DataGridViewTextBoxColumn { HeaderText = "远程用户", DataPropertyName = "RemoteUser", Width = 100, Name = "ColRemoteUser" },
            // 隐藏密码列
            new DataGridViewTextBoxColumn { HeaderText = "DB密码", DataPropertyName = "DbPassword", Width = 0, Visible = false, Name = "ColDbPassword" },
            new DataGridViewTextBoxColumn { HeaderText = "账套密码", DataPropertyName = "ServerPassword", Width = 0, Visible = false, Name = "ColServerPassword" },
            new DataGridViewTextBoxColumn { HeaderText = "远程密码", DataPropertyName = "RemotePassword", Width = 0, Visible = false, Name = "ColRemotePassword" },
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
        var keyword = this.txtSearch.Text.Trim();
        var accounts = string.IsNullOrEmpty(keyword)
            ? _dataService.LoadAndDecryptAccounts()
            : _dataService.SearchAccounts(keyword);
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
                    Code = "", Name = name, Server = serverUrl, ServerPassword = ztpwd,
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
        using var dialog = new AccountDialog(account);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var edited = dialog.GetAccount();
            if (account == null)
                _dataService.AddAccount(edited);
            else
                _dataService.UpdateAccount(account.Code, edited);
            LoadAccounts();
        }
    }

    private void EditSelectedAccount()
    {
        if (this.dgvAccounts.SelectedRows.Count == 0) return;
        var account = this.dgvAccounts.SelectedRows[0].DataBoundItem as Account;
        if (account != null) ShowAccountDialog(account);
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
        if (string.IsNullOrEmpty(settings.AppDirectory) || !Directory.Exists(settings.AppDirectory)) return;

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
                if (p != null) _processIds.Add(p.Id);
            }
        }

        if (settings.LaunchDevTools)
        {
            string exe2 = Path.Combine(appDir, "君则A3集成开发工具.exe");
            if (File.Exists(exe2))
            {
                var p = Process.Start(new ProcessStartInfo { FileName = exe2, WorkingDirectory = appDir, UseShellExecute = true });
                if (p != null) _processIds.Add(p.Id);
            }
        }

        if (settings.LaunchWeb && !string.IsNullOrEmpty(account.Server))
        {
            string url = account.Server.TrimEnd('/') + "/h5comerp/#/login";
            var p = Process.Start(new ProcessStartInfo { FileName = "chrome.exe", Arguments = url, UseShellExecute = true });
            if (p != null) _processIds.Add(p.Id);
        }
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dialog = new SettingsDialog();
        dialog.ShowDialog();
    }

    private void BtnClose_Click(object? sender, EventArgs e)
    {
        foreach (var pid in _processIds.ToList())
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (!p.HasExited)
                    p.Kill();
            }
            catch { }
        }
        _processIds.Clear();
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

        string[] ssmsPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SQL Server Management Studio 19\Common7\IDE\Ssms.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe")
        };

        // 复制数据库密码到剪贴板
        if (!string.IsNullOrEmpty(account.DbPassword))
            Clipboard.SetText(account.DbPassword);

        string ssmsPath = ssmsPaths.FirstOrDefault(File.Exists) ?? "Ssms.exe";
        string dbName = string.IsNullOrWhiteSpace(account.DatabaseName) ? "master" : account.DatabaseName;
        string args = $"-S \"{account.Database}\" -d {dbName} -U {account.DbUser}";

        try
        {
            var p = Process.Start(new ProcessStartInfo { FileName = ssmsPath, Arguments = args, UseShellExecute = true });
            if (p != null) _processIds.Add(p.Id);
        }
        catch
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c start \"\" \"{ssmsPath}\" {args}", UseShellExecute = false, CreateNoWindow = true });
                if (p != null) _processIds.Add(p.Id);
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
                if (p != null) _processIds.Add(p.Id);
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
                    if (p != null) _processIds.Add(p.Id);
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

        var builtInTools = new[]
        {
            ("跨库复制表结构", "📋", "复制数据库表结构到目标库"),
            ("跨库复制Win表单", "🪟", "复制WinForms表单到目标库"),
            ("跨库复制APP表单", "📱", "复制APP表单到目标库"),
            ("跨库复制自定义数据源", "🗄️", "复制自定义数据源配置"),
        };

        foreach (var (name, icon, desc) in builtInTools)
        {
            var btn = CreateToolCard(icon, name, desc);
            var toolName = name;
            btn.Click += (s, e) =>
            {
                var account = this.dgvAccounts.SelectedRows.Count > 0
                    ? this.dgvAccounts.SelectedRows[0].DataBoundItem as Account : null;
                if (account == null)
                {
                    MessageBox.Show("请先在【A3程序启动】中选择一个账套！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                MessageBox.Show($"【{toolName}】功能开发中...\n\n目标账套：{account.Name}", toolName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            this.flpTools.Controls.Add(btn);
        }

        LoadExternalPlugins();
        this.lblPluginStatus.Text = $"已加载 {_plugins.Count} 个工具";
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

    private Button CreateToolCard(string icon, string name, string description)
    {
        var btn = new Button
        {
            Text = $"{icon}  {name}\n{description}",
            Size = new System.Drawing.Size(280, 70),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(5),
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(220, 220, 220);
        btn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(240, 248, 255);
        btn.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(230, 240, 250);
        return btn;
    }
}
