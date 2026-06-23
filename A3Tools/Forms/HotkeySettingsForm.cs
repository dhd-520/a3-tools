using System;
using System.Windows.Forms;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class HotkeySettingsForm : Form
{
    // 瀛楁澹版槑
    private Label lblTitle = null!;
    private Label lblHint = null!;
    private Panel titleBar = null!;
    private Panel bottomBar = null!;
    private TableLayoutPanel gridMain = null!;
    private Label lblColFunc = null!;
    private Label lblColHotkey = null!;
    private Label lblTray = null!;
    private Label lblAdd = null!;
    private Label lblDelete = null!;
    private Label lblLaunch = null!;
    private Label lblSettings = null!;
    private Label lblConnectDB = null!;
    private Label lblRemote = null!;
    private TextBox txtTrayHotkey = null!;
    private TextBox txtAddHotkey = null!;
    private TextBox txtDeleteHotkey = null!;
    private TextBox txtLaunchHotkey = null!;
    private TextBox txtSettingsHotkey = null!;
    private TextBox txtConnectDBHotkey = null!;
    private TextBox txtRemoteHotkey = null!;
    private Button btnClearTray = null!;
    private Button btnClearAdd = null!;
    private Button btnClearDelete = null!;
    private Button btnClearLaunch = null!;
    private Button btnClearSettings = null!;
    private Button btnClearConnectDB = null!;
    private Button btnClearRemote = null!;
    private Label lblRefresh = null!;
    private TextBox txtRefreshHotkey = null!;
    private Button btnClearRefresh = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;

    public string TrayShowHotkey = string.Empty;
    public string AddHotkey = string.Empty;
    public string DeleteHotkey = string.Empty;
    public string LaunchHotkey = string.Empty;
    public string SettingsHotkey = string.Empty;
    public string ConnectDBHotkey = string.Empty;
    public string RemoteHotkey = string.Empty;
    public string RefreshHotkey = string.Empty;

    public HotkeySettingsForm()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        txtTrayHotkey.Text = settings.TrayShowHotkey;
        txtAddHotkey.Text = settings.AddHotkey;
        txtDeleteHotkey.Text = settings.DeleteHotkey;
        txtLaunchHotkey.Text = settings.LaunchHotkey;
        txtSettingsHotkey.Text = settings.SettingsHotkey;
        txtConnectDBHotkey.Text = settings.ConnectDBHotkey;
        txtRemoteHotkey.Text = settings.RemoteHotkey;
        txtRefreshHotkey.Text = settings.RefreshHotkey;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        TrayShowHotkey = txtTrayHotkey.Text;
        AddHotkey = txtAddHotkey.Text;
        DeleteHotkey = txtDeleteHotkey.Text;
        LaunchHotkey = txtLaunchHotkey.Text;
        SettingsHotkey = txtSettingsHotkey.Text;
        ConnectDBHotkey = txtConnectDBHotkey.Text;
        RemoteHotkey = txtRemoteHotkey.Text;
        RefreshHotkey = txtRefreshHotkey.Text;

        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        settings.TrayShowHotkey = TrayShowHotkey;
        settings.AddHotkey = AddHotkey;
        settings.DeleteHotkey = DeleteHotkey;
        settings.LaunchHotkey = LaunchHotkey;
        settings.SettingsHotkey = SettingsHotkey;
        settings.ConnectDBHotkey = ConnectDBHotkey;
        settings.RemoteHotkey = RemoteHotkey;
        settings.RefreshHotkey = RefreshHotkey;
        dataService.SaveSettings(settings);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void TxtBox_KeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        var keys = new System.Collections.Generic.List<string>();
        if (e.Modifiers.HasFlag(Keys.Control)) keys.Add("Ctrl");
        if (e.Modifiers.HasFlag(Keys.Alt)) keys.Add("Alt");
        if (e.Modifiers.HasFlag(Keys.Shift)) keys.Add("Shift");
        if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.ShiftKey)
            keys.Add(e.KeyCode.ToString());
        ((TextBox)sender).Text = keys.Count > 0 ? string.Join("+", keys) : "";
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        if (sender == btnClearTray) { txtTrayHotkey.Text = ""; return; }
        if (sender == btnClearAdd) { txtAddHotkey.Text = ""; return; }
        if (sender == btnClearDelete) { txtDeleteHotkey.Text = ""; return; }
        if (sender == btnClearLaunch) { txtLaunchHotkey.Text = ""; return; }
        if (sender == btnClearSettings) { txtSettingsHotkey.Text = ""; return; }
        if (sender == btnClearConnectDB) { txtConnectDBHotkey.Text = ""; return; }
        if (sender == btnClearRemote) { txtRemoteHotkey.Text = ""; return; }
        if (sender == btnClearRefresh) { txtRefreshHotkey.Text = ""; return; }
    }

    private void BottomBar_Resize(object? sender, EventArgs e)
    {
        btnCancel.Left = bottomBar.Width - 24 - btnCancel.Width;
        btnOK.Left = btnCancel.Left - 10 - btnOK.Width;
    }
}


