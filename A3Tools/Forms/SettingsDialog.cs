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

    public string AppDirectory { get; private set; } = string.Empty;

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
        this.ClientSize = new System.Drawing.Size(1152, 480);
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

        // 主内容
        var content = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.White, Padding = new Padding(36) };

        // 应用目录
        var lblAppDir = new Label
        {
            Text = "应用程序目录：",
            Font = new System.Drawing.Font("微软雅黑", 11F),
            Location = new System.Drawing.Point(0, 0),
            Size = new System.Drawing.Size(180, 50),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        txtAppDir = new TextBox
        {
            Location = new System.Drawing.Point(0, 55),
            Size = new System.Drawing.Size(900, 42),
            Font = new System.Drawing.Font("微软雅黑", 11F),
            ReadOnly = true,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        btnBrowse = new Button
        {
            Text = "浏览...",
            Location = new System.Drawing.Point(912, 52),
            Size = new System.Drawing.Size(144, 50),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(245, 245, 245),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand
        };
        btnBrowse.Click += BtnBrowse_Click;

        var hint = new Label
        {
            Text = "设置A3应用程序所在目录，用于启动账套时定位程序",
            Location = new System.Drawing.Point(0, 115),
            Size = new System.Drawing.Size(1080, 30),
            Font = new System.Drawing.Font("微软雅黑", 9F),
            ForeColor = System.Drawing.Color.FromArgb(150, 150, 150)
        };

        // 启动选项设置区域
        var lblLaunchTitle = new Label
        {
            Text = "启动选项：",
            Font = new System.Drawing.Font("微软雅黑", 11F),
            Location = new System.Drawing.Point(0, 155),
            Size = new System.Drawing.Size(180, 40),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        chkShowLaunchDialog = new CheckBox
        {
            Text = "启动时弹出启动选项选择窗口",
            Location = new System.Drawing.Point(0, 200),
            Size = new System.Drawing.Size(400, 36),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Checked = true
        };

        var hintLaunch = new Label
        {
            Text = "不勾选则按上次选择直接启动（首次使用会弹出选择）",
            Location = new System.Drawing.Point(0, 240),
            Size = new System.Drawing.Size(600, 30),
            Font = new System.Drawing.Font("微软雅黑", 9F),
            ForeColor = System.Drawing.Color.FromArgb(150, 150, 150)
        };

        content.Controls.Add(hint);
        content.Controls.Add(hintLaunch);
        content.Controls.Add(chkShowLaunchDialog);
        content.Controls.Add(lblLaunchTitle);
        content.Controls.Add(btnBrowse);
        content.Controls.Add(txtAppDir);
        content.Controls.Add(lblAppDir);

        // 底部按钮
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 78,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        btnCancel = new Button
        {
            Text = "取消",
            Size = new System.Drawing.Size(132, 46),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnCancel.DialogResult = DialogResult.Cancel;

        btnOK = new Button
        {
            Text = "确定",
            Size = new System.Drawing.Size(132, 46),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand,
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
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择A3应用程序目录",
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrEmpty(txtAppDir.Text) && Directory.Exists(txtAppDir.Text))
            dialog.SelectedPath = txtAppDir.Text;

        if (dialog.ShowDialog() == DialogResult.OK)
            txtAppDir.Text = dialog.SelectedPath;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        AppDirectory = txtAppDir.Text;
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        settings.AppDirectory = AppDirectory;
        settings.ShowLaunchOptionsDialog = chkShowLaunchDialog.Checked;
        dataService.SaveSettings(settings);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}