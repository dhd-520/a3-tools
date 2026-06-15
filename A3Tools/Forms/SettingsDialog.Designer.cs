namespace A3Tools.Forms;

partial class SettingsDialog
{
    private void InitializeComponent()
    {
        titleBar = new Panel();
        lblTitle = new Label();
        mainPanel = new Panel();
        lblAppDir = new Label();
        txtAppDir = new TextBox();
        btnBrowse = new Button();
        hint = new Label();
        lblSsmsPath = new Label();
        txtSsmsPath = new TextBox();
        btnSsmsBrowse = new Button();
        btnSsmsClear = new Button();
        hintSsms = new Label();
        lblLaunchTitle = new Label();
        chkShowLaunchDialog = new CheckBox();
        hintLaunch = new Label();
        lblBrowserLaunch = new Label();
        chkBrowserNewWindow = new CheckBox();
        lblWebSelectors = new Label();
        lblUsernameSel = new Label();
        txtUsernameSel = new TextBox();
        lblPasswordSel = new Label();
        txtPasswordSel = new TextBox();
        lblSubmitSel = new Label();
        txtSubmitSel = new TextBox();
        hintWebSel = new Label();
        bottom = new Panel();
        btnCancel = new Button();
        btnOK = new Button();
        titleBar.SuspendLayout();
        mainPanel.SuspendLayout();
        bottom.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 0);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(1152, 60);
        titleBar.TabIndex = 2;
        // 
        // lblTitle
        // 
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Padding = new Padding(24, 0, 0, 0);
        lblTitle.Size = new Size(1152, 60);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "⚙️ 设置";
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // mainPanel
        // 
        mainPanel.BackColor = Color.White;
        mainPanel.Controls.Add(lblAppDir);
        mainPanel.Controls.Add(txtAppDir);
        mainPanel.Controls.Add(btnBrowse);
        mainPanel.Controls.Add(hint);
        mainPanel.Controls.Add(lblSsmsPath);
        mainPanel.Controls.Add(txtSsmsPath);
        mainPanel.Controls.Add(btnSsmsBrowse);
        mainPanel.Controls.Add(btnSsmsClear);
        mainPanel.Controls.Add(hintSsms);
        mainPanel.Controls.Add(lblLaunchTitle);
        mainPanel.Controls.Add(chkShowLaunchDialog);
        mainPanel.Controls.Add(hintLaunch);
        mainPanel.Controls.Add(lblBrowserLaunch);
        mainPanel.Controls.Add(chkBrowserNewWindow);
        mainPanel.Controls.Add(lblWebSelectors);
        mainPanel.Controls.Add(lblUsernameSel);
        mainPanel.Controls.Add(txtUsernameSel);
        mainPanel.Controls.Add(lblPasswordSel);
        mainPanel.Controls.Add(txtPasswordSel);
        mainPanel.Controls.Add(lblSubmitSel);
        mainPanel.Controls.Add(txtSubmitSel);
        mainPanel.Controls.Add(hintWebSel);
        mainPanel.Dock = DockStyle.Fill;
        mainPanel.Location = new Point(0, 60);
        mainPanel.Name = "mainPanel";
        mainPanel.Size = new Size(1152, 960);
        mainPanel.TabIndex = 1;
        // 
        // lblAppDir
        // 
        lblAppDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblAppDir.Location = new Point(36, 30);
        lblAppDir.Name = "lblAppDir";
        lblAppDir.Size = new Size(220, 50);
        lblAppDir.TabIndex = 0;
        lblAppDir.Text = "应用程序目录：";
        lblAppDir.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtAppDir
        // 
        txtAppDir.BackColor = Color.FromArgb(248, 248, 248);
        txtAppDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtAppDir.Location = new Point(36, 80);
        txtAppDir.Name = "txtAppDir";
        txtAppDir.ReadOnly = true;
        txtAppDir.Size = new Size(800, 41);
        txtAppDir.TabIndex = 1;
        // 
        // btnBrowse
        // 
        btnBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnBrowse.Cursor = Cursors.Hand;
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnBrowse.FlatStyle = FlatStyle.Flat;
        btnBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnBrowse.Location = new Point(844, 80);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(150, 41);
        btnBrowse.TabIndex = 2;
        btnBrowse.Text = "浏览...";
        btnBrowse.UseVisualStyleBackColor = false;
        btnBrowse.Click += BtnBrowse_Click;
        // 
        // hint
        // 
        hint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hint.ForeColor = Color.FromArgb(150, 150, 150);
        hint.Location = new Point(36, 124);
        hint.Name = "hint";
        hint.Size = new Size(958, 30);
        hint.TabIndex = 3;
        hint.Text = "设置A3应用程序所在目录，用于启动账套时定位程序";
        // 
        // lblSsmsPath
        // 
        lblSsmsPath.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblSsmsPath.Location = new Point(36, 160);
        lblSsmsPath.Name = "lblSsmsPath";
        lblSsmsPath.Size = new Size(220, 50);
        lblSsmsPath.TabIndex = 4;
        lblSsmsPath.Text = "数据库管理器路径：";
        lblSsmsPath.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtSsmsPath
        // 
        txtSsmsPath.BackColor = Color.FromArgb(248, 248, 248);
        txtSsmsPath.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtSsmsPath.Location = new Point(36, 210);
        txtSsmsPath.Name = "txtSsmsPath";
        txtSsmsPath.ReadOnly = true;
        txtSsmsPath.Size = new Size(800, 41);
        txtSsmsPath.TabIndex = 5;
        // 
        // btnSsmsBrowse
        // 
        btnSsmsBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnSsmsBrowse.Cursor = Cursors.Hand;
        btnSsmsBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnSsmsBrowse.FlatStyle = FlatStyle.Flat;
        btnSsmsBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnSsmsBrowse.Location = new Point(844, 210);
        btnSsmsBrowse.Name = "btnSsmsBrowse";
        btnSsmsBrowse.Size = new Size(112, 41);
        btnSsmsBrowse.TabIndex = 6;
        btnSsmsBrowse.Text = "浏览...";
        btnSsmsBrowse.UseVisualStyleBackColor = false;
        btnSsmsBrowse.Click += BtnSsmsBrowse_Click;
        // 
        // btnSsmsClear
        // 
        btnSsmsClear.BackColor = Color.FromArgb(245, 245, 245);
        btnSsmsClear.Cursor = Cursors.Hand;
        btnSsmsClear.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnSsmsClear.FlatStyle = FlatStyle.Flat;
        btnSsmsClear.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnSsmsClear.Location = new Point(962, 210);
        btnSsmsClear.Name = "btnSsmsClear";
        btnSsmsClear.Size = new Size(108, 41);
        btnSsmsClear.TabIndex = 7;
        btnSsmsClear.Text = "清除";
        btnSsmsClear.UseVisualStyleBackColor = false;
        btnSsmsClear.Click += BtnSsmsClear_Click;
        // 
        // hintSsms
        // 
        hintSsms.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintSsms.ForeColor = Color.FromArgb(150, 150, 150);
        hintSsms.Location = new Point(36, 254);
        hintSsms.Name = "hintSsms";
        hintSsms.Size = new Size(958, 30);
        hintSsms.TabIndex = 8;
        hintSsms.Text = "设置SSMS可执行文件路径，为空则自动查找";
        // 
        // lblLaunchTitle
        // 
        lblLaunchTitle.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblLaunchTitle.Location = new Point(36, 290);
        lblLaunchTitle.Name = "lblLaunchTitle";
        lblLaunchTitle.Size = new Size(220, 50);
        lblLaunchTitle.TabIndex = 9;
        lblLaunchTitle.Text = "启动选项：";
        lblLaunchTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // chkShowLaunchDialog
        // 
        chkShowLaunchDialog.Checked = true;
        chkShowLaunchDialog.CheckState = CheckState.Checked;
        chkShowLaunchDialog.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkShowLaunchDialog.Location = new Point(36, 340);
        chkShowLaunchDialog.Name = "chkShowLaunchDialog";
        chkShowLaunchDialog.Size = new Size(400, 36);
        chkShowLaunchDialog.TabIndex = 10;
        chkShowLaunchDialog.Text = "启动时弹出启动选项选择窗口";
        // 
        // hintLaunch
        // 
        hintLaunch.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintLaunch.ForeColor = Color.FromArgb(150, 150, 150);
        hintLaunch.Location = new Point(36, 376);
        hintLaunch.Name = "hintLaunch";
        hintLaunch.Size = new Size(958, 30);
        hintLaunch.TabIndex = 11;
        hintLaunch.Text = "不勾选则按上次选择直接启动（首次使用会弹出选择）";
        // 
        // lblBrowserLaunch
        // 
        lblBrowserLaunch.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblBrowserLaunch.Location = new Point(36, 420);
        lblBrowserLaunch.Name = "lblBrowserLaunch";
        lblBrowserLaunch.Size = new Size(341, 50);
        lblBrowserLaunch.TabIndex = 12;
        lblBrowserLaunch.Text = "浏览器启动方式：";
        lblBrowserLaunch.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // chkBrowserNewWindow
        // 
        chkBrowserNewWindow.Checked = true;
        chkBrowserNewWindow.CheckState = CheckState.Checked;
        chkBrowserNewWindow.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkBrowserNewWindow.Location = new Point(36, 470);
        chkBrowserNewWindow.Name = "chkBrowserNewWindow";
        chkBrowserNewWindow.Size = new Size(500, 36);
        chkBrowserNewWindow.TabIndex = 13;
        chkBrowserNewWindow.Text = "启动新窗口（不勾选则在当前浏览器中打开新Tab）";
        // 
        // lblWebSelectors
        // 
        lblWebSelectors.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblWebSelectors.ForeColor = Color.FromArgb(24, 145, 176);
        lblWebSelectors.Location = new Point(36, 510);
        lblWebSelectors.Name = "lblWebSelectors";
        lblWebSelectors.Size = new Size(341, 30);
        lblWebSelectors.TabIndex = 14;
        lblWebSelectors.Text = "🌐 网页版自动登录选择器：";
        lblWebSelectors.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblUsernameSel
        // 
        lblUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblUsernameSel.Location = new Point(36, 550);
        lblUsernameSel.Name = "lblUsernameSel";
        lblUsernameSel.Size = new Size(180, 30);
        lblUsernameSel.TabIndex = 15;
        lblUsernameSel.Text = "用户名选择器：";
        lblUsernameSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtUsernameSel
        // 
        txtUsernameSel.BackColor = Color.FromArgb(248, 248, 248);
        txtUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtUsernameSel.Location = new Point(220, 548);
        txtUsernameSel.Name = "txtUsernameSel";
        txtUsernameSel.Size = new Size(500, 34);
        txtUsernameSel.TabIndex = 16;
        txtUsernameSel.PlaceholderText = "例：#username 或 input[ng-model=username]";
        // 
        // lblPasswordSel
        // 
        lblPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblPasswordSel.Location = new Point(36, 590);
        lblPasswordSel.Name = "lblPasswordSel";
        lblPasswordSel.Size = new Size(180, 30);
        lblPasswordSel.TabIndex = 17;
        lblPasswordSel.Text = "密码选择器：";
        lblPasswordSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtPasswordSel
        // 
        txtPasswordSel.BackColor = Color.FromArgb(248, 248, 248);
        txtPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtPasswordSel.Location = new Point(220, 588);
        txtPasswordSel.Name = "txtPasswordSel";
        txtPasswordSel.Size = new Size(500, 34);
        txtPasswordSel.TabIndex = 18;
        txtPasswordSel.PlaceholderText = "例：#password 或 input[type=password]";
        // 
        // lblSubmitSel
        // 
        lblSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSubmitSel.Location = new Point(36, 630);
        lblSubmitSel.Name = "lblSubmitSel";
        lblSubmitSel.Size = new Size(180, 30);
        lblSubmitSel.TabIndex = 19;
        lblSubmitSel.Text = "登录按钮选择器：";
        lblSubmitSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtSubmitSel
        // 
        txtSubmitSel.BackColor = Color.FromArgb(248, 248, 248);
        txtSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSubmitSel.Location = new Point(220, 628);
        txtSubmitSel.Name = "txtSubmitSel";
        txtSubmitSel.Size = new Size(500, 34);
        txtSubmitSel.TabIndex = 20;
        txtSubmitSel.PlaceholderText = "例：button[type=submit] 或 .login-btn";
        // 
        // hintWebSel
        // 
        hintWebSel.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintWebSel.ForeColor = Color.FromArgb(150, 150, 150);
        hintWebSel.Location = new Point(220, 665);
        hintWebSel.Name = "hintWebSel";
        hintWebSel.Size = new Size(600, 30);
        hintWebSel.TabIndex = 21;
        hintWebSel.Text = "用浏览器 F12 → Elements 面板查看登录页 input 元素的 id/class/name";
        // 
        // bottom
        // 
        bottom.BackColor = Color.FromArgb(248, 248, 248);
        bottom.Controls.Add(btnCancel);
        bottom.Controls.Add(btnOK);
        bottom.Dock = DockStyle.Bottom;
        bottom.Location = new Point(0, 942);
        bottom.Name = "bottom";
        bottom.Size = new Size(1152, 78);
        bottom.TabIndex = 0;
        bottom.Resize += Bottom_Resize;
        // 
        // btnCancel
        // 
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.Cursor = Cursors.Hand;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.Location = new Point(984, 16);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(132, 46);
        btnCancel.TabIndex = 0;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        // 
        // btnOK
        // 
        btnOK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOK.BackColor = Color.FromArgb(24, 145, 176);
        btnOK.Cursor = Cursors.Hand;
        btnOK.DialogResult = DialogResult.OK;
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.FlatStyle = FlatStyle.Flat;
        btnOK.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnOK.ForeColor = Color.White;
        btnOK.Location = new Point(842, 16);
        btnOK.Name = "btnOK";
        btnOK.Size = new Size(132, 46);
        btnOK.TabIndex = 1;
        btnOK.Text = "确定";
        btnOK.UseVisualStyleBackColor = false;
        btnOK.Click += BtnOK_Click;
        // 
        // SettingsDialog
        // 
        AcceptButton = btnOK;
        BackColor = Color.White;
        CancelButton = btnCancel;
        ClientSize = new Size(1152, 1020);
        Controls.Add(bottom);
        Controls.Add(mainPanel);
        Controls.Add(titleBar);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "设置";
        titleBar.ResumeLayout(false);
        mainPanel.ResumeLayout(false);
        mainPanel.PerformLayout();
        bottom.ResumeLayout(false);
        ResumeLayout(false);
    }

    private void Bottom_Resize(object? sender, EventArgs e)
    {
        btnCancel.Left = ((Panel)sender).Width - 36 - btnCancel.Width;
        btnOK.Left = btnCancel.Left - 12 - btnOK.Width;
    }

    private Panel mainPanel;
    private Label lblAppDir;
    private Label hint;
    private Label lblSsmsPath;
    private Label hintSsms;
    private Label lblLaunchTitle;
    private Label hintLaunch;
    private Label lblBrowserLaunch;
    private Label lblWebSelectors;
    private Label lblUsernameSel;
    private TextBox txtUsernameSel;
    private Label lblPasswordSel;
    private TextBox txtPasswordSel;
    private Label lblSubmitSel;
    private TextBox txtSubmitSel;
    private Label hintWebSel;
    private Panel bottom;
}