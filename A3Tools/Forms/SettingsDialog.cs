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
    private CheckBox chkDesktop = null!;
    private CheckBox chkDevTools = null!;
    private CheckBox chkWeb = null!;

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
        this.ClientSize = new System.Drawing.Size(960, 400);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = System.Drawing.Color.White;

        // 标题栏
        titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176)
        };

        lblTitle = new Label
        {
            Text = "⚙️ 设置",
            Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.White,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0)
        };
        titleBar.Controls.Add(lblTitle);

        // 主内容
        var content = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.White, Padding = new Padding(30) };

        // 应用目录
        var lblAppDir = new Label
        {
            Text = "应用程序目录：",
            Font = new System.Drawing.Font("微软雅黑", 11F),
            Location = new System.Drawing.Point(0, 0),
            Size = new System.Drawing.Size(150, 35),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        txtAppDir = new TextBox
        {
            Location = new System.Drawing.Point(0, 40),
            Size = new System.Drawing.Size(750, 30),
            Font = new System.Drawing.Font("微软雅黑", 11F),
            ReadOnly = true,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        btnBrowse = new Button
        {
            Text = "浏览...",
            Location = new System.Drawing.Point(760, 38),
            Size = new System.Drawing.Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(245, 245, 245),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand
        };
        btnBrowse.Click += BtnBrowse_Click;

        var hint = new Label
        {
            Text = "设置A3应用程序所在目录，用于启动账套时定位程序",
            Location = new System.Drawing.Point(0, 85),
            Size = new System.Drawing.Size(900, 25),
            Font = new System.Drawing.Font("微软雅黑", 9F),
            ForeColor = System.Drawing.Color.FromArgb(150, 150, 150)
        };

        // 分隔线
        var sep = new Panel
        {
            Location = new System.Drawing.Point(0, 120),
            Size = new System.Drawing.Size(900, 1),
            BackColor = System.Drawing.Color.FromArgb(220, 220, 220)
        };

        // 启动选项
        var lblLaunch = new Label
        {
            Text = "启动选项：",
            Font = new System.Drawing.Font("微软雅黑", 11F),
            Location = new System.Drawing.Point(0, 135),
            Size = new System.Drawing.Size(150, 35),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        chkDesktop = new CheckBox
        {
            Text = "启动电脑端（君则A3.exe）",
            Location = new System.Drawing.Point(0, 175),
            Size = new System.Drawing.Size(900, 30),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Checked = true
        };

        chkDevTools = new CheckBox
        {
            Text = "启动开发工具（君则A3集成开发工具.exe）",
            Location = new System.Drawing.Point(0, 210),
            Size = new System.Drawing.Size(900, 30),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Checked = true
        };

        chkWeb = new CheckBox
        {
            Text = "启动网页版（Google浏览器访问）",
            Location = new System.Drawing.Point(0, 245),
            Size = new System.Drawing.Size(900, 30),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Checked = false
        };

        content.Controls.Add(chkWeb);
        content.Controls.Add(chkDevTools);
        content.Controls.Add(chkDesktop);
        content.Controls.Add(sep);
        content.Controls.Add(hint);
        content.Controls.Add(btnBrowse);
        content.Controls.Add(txtAppDir);
        content.Controls.Add(lblAppDir);
        content.Controls.Add(lblLaunch);

        // 底部按钮
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        btnCancel = new Button
        {
            Text = "取消",
            Size = new System.Drawing.Size(110, 38),
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
            Size = new System.Drawing.Size(110, 38),
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
            btnCancel.Left = bottom.Width - 30 - btnCancel.Width;
            btnOK.Left = btnCancel.Left - 10 - btnOK.Width;
        };
        btnCancel.Left = bottom.Width - 30 - btnCancel.Width;
        btnOK.Left = btnCancel.Left - 10 - btnOK.Width;

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
        chkDesktop.Checked = settings.LaunchDesktop;
        chkDevTools.Checked = settings.LaunchDevTools;
        chkWeb.Checked = settings.LaunchWeb;
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
        settings.LaunchDesktop = chkDesktop.Checked;
        settings.LaunchDevTools = chkDevTools.Checked;
        settings.LaunchWeb = chkWeb.Checked;
        dataService.SaveSettings(settings);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
