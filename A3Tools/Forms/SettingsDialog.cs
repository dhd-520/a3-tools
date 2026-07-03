using System;
using System.IO;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class SettingsDialog : Form
{
    private TextBox txtAppDir = null!;
    private Button btnBrowse = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;
    private Label lblTitle = null!;
    private Panel titleBar = null!;
    private CheckBox chkShowLaunchDialog = null!;
    private TextBox txtSsmsPath = null!;
    private Button btnSsmsBrowse = null!;
    private Button btnSsmsClear = null!;
    private CheckBox chkBrowserNewWindow = null!;
    private Panel sepAutoLogin = null!;
    private CheckBox chkClientAutoLogin = null!;
    private CheckBox chkDevToolsAutoLogin = null!;
    private Label lblDevToolsPassword = null!;
    private TextBox txtDevToolsPassword = null!;
    private Label hintDevTools = null!;

    public string AppDirectory { get; private set; } = string.Empty;
    public string TrayShowHotkey { get; private set; } = "Ctrl+Shift+Z";

    public SettingsDialog()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        txtAppDir.Text = settings.AppDirectory;
        AppDirectory = settings.AppDirectory;
        chkShowLaunchDialog.Checked = settings.ShowLaunchOptionsDialog;
        txtSsmsPath.Text = settings.SsmsPath;
        chkBrowserNewWindow.Checked = settings.BrowserNewWindow;
        txtUsernameSel.Text = settings.WebUsernameSelector;
        txtPasswordSel.Text = settings.WebPasswordSelector;
        txtSubmitSel.Text = settings.WebSubmitSelector;
        chkClientAutoLogin.Checked = settings.ClientAutoLogin;
        chkDevToolsAutoLogin.Checked = settings.DevToolsAutoLogin;
        txtDevToolsPassword.Text = settings.DevToolsPassword;  // LoadSettings 已自动解密
        if (settings.QueryToolMode == QueryToolMode.BuiltIn)
            rbQueryToolBuiltIn.Checked = true;
        else
            rbQueryToolSsms.Checked = true;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择A3应用程序目录", ShowNewFolderButton = false };
        if (!string.IsNullOrEmpty(txtAppDir.Text) && Directory.Exists(txtAppDir.Text))
            dialog.SelectedPath = txtAppDir.Text;
        if (dialog.ShowDialog() == DialogResult.OK)
            txtAppDir.Text = dialog.SelectedPath;
    }

    private void BtnSsmsBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "选择SSMS可执行文件", Filter = "可执行文件|*.exe", FileName = "Ssms.exe" };
        if (!string.IsNullOrEmpty(txtSsmsPath.Text) && File.Exists(txtSsmsPath.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(txtSsmsPath.Text);
        if (dialog.ShowDialog() == DialogResult.OK)
            txtSsmsPath.Text = dialog.FileName;
    }

    private void BtnSsmsClear_Click(object? sender, EventArgs e)
    {
        txtSsmsPath.Text = "";
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        AppDirectory = txtAppDir.Text;
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        settings.AppDirectory = AppDirectory;
        settings.ShowLaunchOptionsDialog = chkShowLaunchDialog.Checked;
        settings.SsmsPath = txtSsmsPath.Text;
        settings.BrowserNewWindow = chkBrowserNewWindow.Checked;
        settings.WebUsernameSelector = txtUsernameSel.Text.Trim();
        settings.WebPasswordSelector = txtPasswordSel.Text.Trim();
        settings.WebSubmitSelector = txtSubmitSel.Text.Trim();
        settings.ClientAutoLogin = chkClientAutoLogin.Checked;
        settings.DevToolsAutoLogin = chkDevToolsAutoLogin.Checked;
        settings.DevToolsPassword = txtDevToolsPassword.Text;  // 明文，SaveSettings 自动加密
        settings.QueryToolMode = rbQueryToolBuiltIn.Checked ? QueryToolMode.BuiltIn : QueryToolMode.Ssms;
        dataService.SaveSettings(settings);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}