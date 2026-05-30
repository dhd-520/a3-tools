using System;
using System.IO;
using System.Windows.Forms;
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

    public string AppDirectory { get; private set; } = string.Empty;
    public string TrayShowHotkey { get; private set; } = "Ctrl+Shift+Z";

    public SettingsDialog()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Text = "设置";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ClientSize = new System.Drawing.Size(1152, 780);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = System.Drawing.Color.White;

        // 标题栏
        titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176)
        };

        lblTitle = new Label
        {
            Text = "⚙️ 设置",
            Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.White,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(24, 0, 0, 0)
        };
        titleBar.Controls.Add(lblTitle);

        // 主内容 - 使用 TableLayoutPanel 实现垂直排列 + 列布局
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.White,
            Padding = new Padding(36, 20, 36, 20),
            RowCount = 0,
            ColumnCount = 2,
            ColumnStyles = {
                new ColumnStyle(SizeType.Absolute, 220),
                new ColumnStyle(SizeType.Percent, 100)
            }
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        // === 应用程序目录 ===
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var lblAppDir = new Label { Text = "应用程序目录：", Font = new System.Drawing.Font("微软雅黑", 11F), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        content.Controls.Add(lblAppDir, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        txtAppDir = new TextBox { Font = new System.Drawing.Font("微软雅黑", 11F), ReadOnly = true, BackColor = System.Drawing.Color.FromArgb(248, 248, 248), Dock = DockStyle.Fill };
        content.Controls.Add(txtAppDir, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        btnBrowse = new Button { Text = "浏览...", FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.FromArgb(245, 245, 245), Font = new System.Drawing.Font("微软雅黑", 10F), Cursor = Cursors.Hand, Dock = DockStyle.Left };
        btnBrowse.Click += BtnBrowse_Click;
        content.Controls.Add(btnBrowse, 0, content.RowCount - 1);

        var hint = new Label { Text = "设置A3应用程序所在目录，用于启动账套时定位程序", Font = new System.Drawing.Font("微软雅黑", 9F), ForeColor = System.Drawing.Color.FromArgb(150, 150, 150), Dock = DockStyle.Fill };
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.Controls.Add(hint, 0, content.RowCount - 1);
        content.SetColumnSpan(hint, 2);

        // === SSMS路径 ===
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var lblSsmsPath = new Label { Text = "数据库管理器路径：", Font = new System.Drawing.Font("微软雅黑", 11F), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        content.Controls.Add(lblSsmsPath, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        txtSsmsPath = new TextBox { Font = new System.Drawing.Font("微软雅黑", 11F), ReadOnly = true, BackColor = System.Drawing.Color.FromArgb(248, 248, 248), Dock = DockStyle.Fill };
        content.Controls.Add(txtSsmsPath, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var btnSsmsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        btnSsmsBrowse = new Button { Text = "浏览...", FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.FromArgb(245, 245, 245), Font = new System.Drawing.Font("微软雅黑", 10F), Cursor = Cursors.Hand };
        btnSsmsBrowse.Click += BtnSsmsBrowse_Click;
        btnSsmsClear = new Button { Text = "清除", FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.FromArgb(245, 245, 245), Font = new System.Drawing.Font("微软雅黑", 10F), Cursor = Cursors.Hand };
        btnSsmsClear.Click += (s, e) => txtSsmsPath.Text = "";
        btnSsmsRow.Controls.Add(btnSsmsBrowse);
        btnSsmsRow.Controls.Add(btnSsmsClear);
        content.Controls.Add(btnSsmsRow, 0, content.RowCount - 1);

        var hintSsms = new Label { Text = "设置SSMS可执行文件路径，为空则自动查找", Font = new System.Drawing.Font("微软雅黑", 9F), ForeColor = System.Drawing.Color.FromArgb(150, 150, 150), Dock = DockStyle.Fill };
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.Controls.Add(hintSsms, 0, content.RowCount - 1);
        content.SetColumnSpan(hintSsms, 2);

        // === 启动选项 ===
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var lblLaunchTitle = new Label { Text = "启动选项：", Font = new System.Drawing.Font("微软雅黑", 11F), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        content.Controls.Add(lblLaunchTitle, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        chkShowLaunchDialog = new CheckBox { Text = "启动时弹出启动选项选择窗口", Font = new System.Drawing.Font("微软雅黑", 10F), Checked = true, Dock = DockStyle.Fill };
        content.Controls.Add(chkShowLaunchDialog, 0, content.RowCount - 1);

        var hintLaunch = new Label { Text = "不勾选则按上次选择直接启动（首次使用会弹出选择）", Font = new System.Drawing.Font("微软雅黑", 9F), ForeColor = System.Drawing.Color.FromArgb(150, 150, 150), Dock = DockStyle.Fill };
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.Controls.Add(hintLaunch, 0, content.RowCount - 1);
        content.SetColumnSpan(hintLaunch, 2);

        // === 浏览器启动方式 ===
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var lblBrowserLaunch = new Label { Text = "浏览器启动方式：", Font = new System.Drawing.Font("微软雅黑", 11F), TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        content.Controls.Add(lblBrowserLaunch, 0, content.RowCount - 1);

        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        chkBrowserNewWindow = new CheckBox { Text = "启动新窗口（不勾选则在当前浏览器中打开新Tab）", Font = new System.Drawing.Font("微软雅黑", 10F), Checked = true, Dock = DockStyle.Fill };
        content.Controls.Add(chkBrowserNewWindow, 0, content.RowCount - 1);

        // 底部按钮
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 78, BackColor = System.Drawing.Color.FromArgb(248, 248, 248) };

        btnCancel = new Button
        {
            Text = "取消", Size = new System.Drawing.Size(132, 46), FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White, Font = new System.Drawing.Font("微软雅黑", 10F), Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnCancel.DialogResult = DialogResult.Cancel;

        btnOK = new Button
        {
            Text = "确定", Size = new System.Drawing.Size(132, 46), FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176), ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F), Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.DialogResult = DialogResult.OK;
        btnOK.Click += BtnOK_Click;

        bottom.Controls.Add(btnOK);
        bottom.Controls.Add(btnCancel);
        bottom.Resize += (s, e) =>
        {
            btnCancel.Left = bottom.Width - 36 - btnCancel.Width;
            btnOK.Left = btnCancel.Left - 12 - btnOK.Width;
        };
        btnCancel.Left = bottom.Width - 36 - btnCancel.Width;
        btnOK.Left = btnCancel.Left - 12 - btnOK.Width;

        this.Controls.Add(bottom);
        this.Controls.Add(content);
        this.Controls.Add(titleBar);
        this.AcceptButton = btnOK;
        this.CancelButton = btnCancel;
        this.ResumeLayout(false);
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

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        AppDirectory = txtAppDir.Text;
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        settings.AppDirectory = AppDirectory;
        settings.ShowLaunchOptionsDialog = chkShowLaunchDialog.Checked;
        settings.SsmsPath = txtSsmsPath.Text;
        settings.BrowserNewWindow = chkBrowserNewWindow.Checked;
        dataService.SaveSettings(settings);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}