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
        chkEnableAi = new CheckBox();
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
        // ★ 2026-08-14 11:34 企业微信选择器
        lblWechatWorkSelectorTitle = new Label();
        lblWechatWorkUsernameSel = new Label();
        txtWechatWorkUsernameSel = new TextBox();
        lblWechatWorkPasswordSel = new Label();
        txtWechatWorkPasswordSel = new TextBox();
        lblWechatWorkSubmitSel = new Label();
        txtWechatWorkSubmitSel = new TextBox();
        sepAutoLogin = new Panel();
        chkClientAutoLogin = new CheckBox();
        chkDevToolsAutoLogin = new CheckBox();
        lblDevToolsPassword = new Label();
        txtDevToolsPassword = new TextBox();
        hintDevTools = new Label();
        sepHubConfig = new Panel();
        lblHubConfigTitle = new Label();
        hintHubConfig = new Label();
        lblHubConfigDir = new Label();
        txtHubConfigDir = new TextBox();
        btnHubConfigBrowse = new Button();
        sepFont = new Panel();
        lblFontTitle = new Label();
        lblFontFamily = new Label();
        cmbFontFamily = new ComboBox();
        lblFontSize = new Label();
        cmbFontSize = new ComboBox();
        lblFontFile = new Label();
        txtFontFile = new TextBox();
        btnFontFileBrowse = new Button();
        btnFontFileClear = new Button();
        hintFont = new Label();
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
        mainPanel.AutoScroll = true;
        mainPanel.BackColor = Color.White;
        mainPanel.Controls.Add(sepFont);
        mainPanel.Controls.Add(lblFontTitle);
        mainPanel.Controls.Add(lblFontFamily);
        mainPanel.Controls.Add(cmbFontFamily);
        mainPanel.Controls.Add(lblFontSize);
        mainPanel.Controls.Add(cmbFontSize);
        mainPanel.Controls.Add(lblFontFile);
        mainPanel.Controls.Add(txtFontFile);
        mainPanel.Controls.Add(btnFontFileBrowse);
        mainPanel.Controls.Add(btnFontFileClear);
        mainPanel.Controls.Add(hintFont);
        mainPanel.Controls.Add(lblAppDir);
        mainPanel.Controls.Add(txtAppDir);
        mainPanel.Controls.Add(btnBrowse);
        mainPanel.Controls.Add(hint);
        mainPanel.Controls.Add(lblSsmsPath);
        mainPanel.Controls.Add(txtSsmsPath);
        mainPanel.Controls.Add(btnSsmsBrowse);
        mainPanel.Controls.Add(btnSsmsClear);
        mainPanel.Controls.Add(hintSsms);
        mainPanel.Controls.Add(chkEnableAi);
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
        // ★ 2026-08-14 11:34 企业微信选择器控件
        mainPanel.Controls.Add(lblWechatWorkSelectorTitle);
        mainPanel.Controls.Add(lblWechatWorkUsernameSel);
        mainPanel.Controls.Add(txtWechatWorkUsernameSel);
        mainPanel.Controls.Add(lblWechatWorkPasswordSel);
        mainPanel.Controls.Add(txtWechatWorkPasswordSel);
        mainPanel.Controls.Add(lblWechatWorkSubmitSel);
        mainPanel.Controls.Add(txtWechatWorkSubmitSel);
        mainPanel.Controls.Add(sepAutoLogin);
        mainPanel.Controls.Add(chkClientAutoLogin);
        mainPanel.Controls.Add(chkDevToolsAutoLogin);
        mainPanel.Controls.Add(lblDevToolsPassword);
        mainPanel.Controls.Add(txtDevToolsPassword);
        mainPanel.Controls.Add(hintDevTools);
        mainPanel.Controls.Add(sepHubConfig);
        mainPanel.Controls.Add(lblHubConfigTitle);
        mainPanel.Controls.Add(hintHubConfig);
        mainPanel.Controls.Add(lblHubConfigDir);
        mainPanel.Controls.Add(txtHubConfigDir);
        mainPanel.Controls.Add(btnHubConfigBrowse);
        mainPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        mainPanel.Location = new Point(0, 60);
        mainPanel.Name = "mainPanel";
        mainPanel.AutoScrollMinSize = new Size(0, 1700);
        mainPanel.Padding = new Padding(0, 0, 0, 12);
        mainPanel.Size = new Size(1152, 582);
        mainPanel.TabIndex = 1;
        // 
        // lblAppDir
        // 
        lblAppDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblAppDir.Location = new Point(36, 260);
        lblAppDir.Name = "lblAppDir";
        lblAppDir.Size = new Size(220, 50);
        lblAppDir.TabIndex = 11;
        lblAppDir.Text = "应用程序目录:";
        lblAppDir.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtAppDir
        // 
        txtAppDir.BackColor = Color.FromArgb(248, 248, 248);
        txtAppDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtAppDir.Location = new Point(36, 310);
        txtAppDir.Name = "txtAppDir";
        txtAppDir.ReadOnly = true;
        txtAppDir.Size = new Size(800, 41);
        txtAppDir.TabIndex = 12;
        // 
        // btnBrowse
        // 
        btnBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnBrowse.Cursor = Cursors.Hand;
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnBrowse.FlatStyle = FlatStyle.Flat;
        btnBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnBrowse.Location = new Point(844, 310);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(150, 41);
        btnBrowse.TabIndex = 13;
        btnBrowse.Text = "浏览...";
        btnBrowse.UseVisualStyleBackColor = false;
        btnBrowse.Click += BtnBrowse_Click;
        // 
        // hint
        // 
        hint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hint.ForeColor = Color.FromArgb(150, 150, 150);
        hint.Location = new Point(36, 354);
        hint.Name = "hint";
        hint.Size = new Size(958, 30);
        hint.TabIndex = 14;
        hint.Text = "设置A3应用程序所在目录,用于启动账套时定位程序";
        // 
        // lblSsmsPath
        // 
        lblSsmsPath.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblSsmsPath.Location = new Point(36, 390);
        lblSsmsPath.Name = "lblSsmsPath";
        lblSsmsPath.Size = new Size(220, 50);
        lblSsmsPath.TabIndex = 15;
        lblSsmsPath.Text = "数据库管理器路径:";
        lblSsmsPath.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtSsmsPath
        // 
        txtSsmsPath.BackColor = Color.FromArgb(248, 248, 248);
        txtSsmsPath.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtSsmsPath.Location = new Point(36, 440);
        txtSsmsPath.Name = "txtSsmsPath";
        txtSsmsPath.ReadOnly = true;
        txtSsmsPath.Size = new Size(800, 41);
        txtSsmsPath.TabIndex = 16;
        // 
        // btnSsmsBrowse
        // 
        btnSsmsBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnSsmsBrowse.Cursor = Cursors.Hand;
        btnSsmsBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnSsmsBrowse.FlatStyle = FlatStyle.Flat;
        btnSsmsBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnSsmsBrowse.Location = new Point(844, 440);
        btnSsmsBrowse.Name = "btnSsmsBrowse";
        btnSsmsBrowse.Size = new Size(112, 41);
        btnSsmsBrowse.TabIndex = 17;
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
        btnSsmsClear.Location = new Point(962, 440);
        btnSsmsClear.Name = "btnSsmsClear";
        btnSsmsClear.Size = new Size(108, 41);
        btnSsmsClear.TabIndex = 18;
        btnSsmsClear.Text = "清除";
        btnSsmsClear.UseVisualStyleBackColor = false;
        btnSsmsClear.Click += BtnSsmsClear_Click;
        // 
        // hintSsms
        // 
        hintSsms.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintSsms.ForeColor = Color.FromArgb(150, 150, 150);
        hintSsms.Location = new Point(36, 484);
        hintSsms.Name = "hintSsms";
        hintSsms.Size = new Size(958, 30);
        hintSsms.TabIndex = 19;
        hintSsms.Text = "设置SSMS可执行文件路径,为空则自动查找";
        //
        // chkEnableAi
        //
        chkEnableAi.AutoSize = true;
        chkEnableAi.Checked = false;
        chkEnableAi.CheckState = CheckState.Unchecked;
        chkEnableAi.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        chkEnableAi.Location = new Point(36, 530);
        chkEnableAi.Name = "chkEnableAi";
        chkEnableAi.Size = new Size(958, 50);
        chkEnableAi.TabIndex = 20;
        chkEnableAi.Text = "启用 AI 功能（启用后在「帮助」菜单下显示「AI 助理」「AI 助理设置」「知识库管理」）";
        chkEnableAi.UseVisualStyleBackColor = true;
        //
        // lblLaunchTitle
        // 
        lblLaunchTitle.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblLaunchTitle.Location = new Point(36, 640);
        lblLaunchTitle.Name = "lblLaunchTitle";
        lblLaunchTitle.Size = new Size(220, 50);
        lblLaunchTitle.TabIndex = 20;
        lblLaunchTitle.Text = "启动选项:";
        lblLaunchTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // chkShowLaunchDialog
        // 
        chkShowLaunchDialog.Checked = true;
        chkShowLaunchDialog.CheckState = CheckState.Checked;
        chkShowLaunchDialog.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkShowLaunchDialog.Location = new Point(36, 690);
        chkShowLaunchDialog.Name = "chkShowLaunchDialog";
        chkShowLaunchDialog.Size = new Size(400, 36);
        chkShowLaunchDialog.TabIndex = 21;
        chkShowLaunchDialog.Text = "启动时弹出启动选项选择窗口";
        // 
        // hintLaunch
        // 
        hintLaunch.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintLaunch.ForeColor = Color.FromArgb(150, 150, 150);
        hintLaunch.Location = new Point(36, 726);
        hintLaunch.Name = "hintLaunch";
        hintLaunch.Size = new Size(958, 30);
        hintLaunch.TabIndex = 22;
        hintLaunch.Text = "不勾选则按上次选择直接启动(首次使用会弹出选择)";
        // 
        // lblBrowserLaunch
        // 
        lblBrowserLaunch.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblBrowserLaunch.Location = new Point(36, 770);
        lblBrowserLaunch.Name = "lblBrowserLaunch";
        lblBrowserLaunch.Size = new Size(341, 50);
        lblBrowserLaunch.TabIndex = 23;
        lblBrowserLaunch.Text = "浏览器启动方式:";
        lblBrowserLaunch.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // chkBrowserNewWindow
        // 
        chkBrowserNewWindow.Checked = true;
        chkBrowserNewWindow.CheckState = CheckState.Checked;
        chkBrowserNewWindow.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkBrowserNewWindow.Location = new Point(36, 820);
        chkBrowserNewWindow.Name = "chkBrowserNewWindow";
        chkBrowserNewWindow.Size = new Size(500, 36);
        chkBrowserNewWindow.TabIndex = 24;
        chkBrowserNewWindow.Text = "启动新窗口(不勾选则在当前浏览器中打开新Tab)";
        // 
        // lblWebSelectors
        // 
        lblWebSelectors.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblWebSelectors.ForeColor = Color.FromArgb(24, 145, 176);
        lblWebSelectors.Location = new Point(36, 860);
        lblWebSelectors.Name = "lblWebSelectors";
        lblWebSelectors.Size = new Size(341, 30);
        lblWebSelectors.TabIndex = 25;
        lblWebSelectors.Text = "🌐 网页版自动登录选择器:";
        lblWebSelectors.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblUsernameSel
        // 
        lblUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblUsernameSel.Location = new Point(36, 900);
        lblUsernameSel.Name = "lblUsernameSel";
        lblUsernameSel.Size = new Size(180, 30);
        lblUsernameSel.TabIndex = 26;
        lblUsernameSel.Text = "用户名选择器:";
        lblUsernameSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtUsernameSel
        // 
        txtUsernameSel.BackColor = Color.FromArgb(248, 248, 248);
        txtUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtUsernameSel.Location = new Point(220, 898);
        txtUsernameSel.Name = "txtUsernameSel";
        txtUsernameSel.PlaceholderText = "例:#username 或 input[ng-model=username]";
        txtUsernameSel.Size = new Size(500, 38);
        txtUsernameSel.TabIndex = 27;
        // 
        // lblPasswordSel
        // 
        lblPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblPasswordSel.Location = new Point(36, 940);
        lblPasswordSel.Name = "lblPasswordSel";
        lblPasswordSel.Size = new Size(180, 30);
        lblPasswordSel.TabIndex = 28;
        lblPasswordSel.Text = "密码选择器:";
        lblPasswordSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtPasswordSel
        // 
        txtPasswordSel.BackColor = Color.FromArgb(248, 248, 248);
        txtPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtPasswordSel.Location = new Point(220, 938);
        txtPasswordSel.Name = "txtPasswordSel";
        txtPasswordSel.PlaceholderText = "例:#password 或 input[type=password]";
        txtPasswordSel.Size = new Size(500, 38);
        txtPasswordSel.TabIndex = 29;
        // 
        // lblSubmitSel
        // 
        lblSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSubmitSel.Location = new Point(36, 980);
        lblSubmitSel.Name = "lblSubmitSel";
        lblSubmitSel.Size = new Size(180, 30);
        lblSubmitSel.TabIndex = 30;
        lblSubmitSel.Text = "登录按钮选择器:";
        lblSubmitSel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtSubmitSel
        // 
        txtSubmitSel.BackColor = Color.FromArgb(248, 248, 248);
        txtSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSubmitSel.Location = new Point(220, 978);
        txtSubmitSel.Name = "txtSubmitSel";
        txtSubmitSel.PlaceholderText = "例:button[type=submit] 或 .login-btn";
        txtSubmitSel.Size = new Size(500, 38);
        txtSubmitSel.TabIndex = 31;
        // 
        // hintWebSel
        // 
        hintWebSel.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintWebSel.ForeColor = Color.FromArgb(150, 150, 150);
        hintWebSel.Location = new Point(220, 1025);
        hintWebSel.Name = "hintWebSel";
        hintWebSel.Size = new Size(600, 30);
        hintWebSel.TabIndex = 32;
        hintWebSel.Text = "用浏览器 F12 → Elements 面板查看登录页 input 元素的 id/class/name";

        //
        // ★ 2026-08-14 11:34 企业微信选择器 (h5apperp)
        //
        lblWechatWorkSelectorTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblWechatWorkSelectorTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblWechatWorkSelectorTitle.Location = new Point(40, 1070);
        lblWechatWorkSelectorTitle.Name = "lblWechatWorkSelectorTitle";
        lblWechatWorkSelectorTitle.Size = new Size(600, 32);
        lblWechatWorkSelectorTitle.TabIndex = 33;
        lblWechatWorkSelectorTitle.Text = "企业微信网页版登录选择器 (/h5apperp/#/login)";

        lblWechatWorkUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblWechatWorkUsernameSel.Location = new Point(40, 1110);
        lblWechatWorkUsernameSel.Name = "lblWechatWorkUsernameSel";
        lblWechatWorkUsernameSel.Size = new Size(170, 32);
        lblWechatWorkUsernameSel.TabIndex = 34;
        lblWechatWorkUsernameSel.Text = "用户名选择器：";

        txtWechatWorkUsernameSel.BackColor = Color.FromArgb(248, 248, 248);
        txtWechatWorkUsernameSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtWechatWorkUsernameSel.Location = new Point(220, 1105);
        txtWechatWorkUsernameSel.Name = "txtWechatWorkUsernameSel";
        txtWechatWorkUsernameSel.PlaceholderText = "例:input[placeholder=\"请输入登录用户账号\"]";
        txtWechatWorkUsernameSel.Size = new Size(500, 38);
        txtWechatWorkUsernameSel.TabIndex = 35;

        lblWechatWorkPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblWechatWorkPasswordSel.Location = new Point(40, 1150);
        lblWechatWorkPasswordSel.Name = "lblWechatWorkPasswordSel";
        lblWechatWorkPasswordSel.Size = new Size(170, 32);
        lblWechatWorkPasswordSel.TabIndex = 36;
        lblWechatWorkPasswordSel.Text = "密码选择器：";

        txtWechatWorkPasswordSel.BackColor = Color.FromArgb(248, 248, 248);
        txtWechatWorkPasswordSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtWechatWorkPasswordSel.Location = new Point(220, 1145);
        txtWechatWorkPasswordSel.Name = "txtWechatWorkPasswordSel";
        txtWechatWorkPasswordSel.PlaceholderText = "例:input[type=\"password\"]";
        txtWechatWorkPasswordSel.Size = new Size(500, 38);
        txtWechatWorkPasswordSel.TabIndex = 37;

        lblWechatWorkSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblWechatWorkSubmitSel.Location = new Point(40, 1190);
        lblWechatWorkSubmitSel.Name = "lblWechatWorkSubmitSel";
        lblWechatWorkSubmitSel.Size = new Size(170, 32);
        lblWechatWorkSubmitSel.TabIndex = 38;
        lblWechatWorkSubmitSel.Text = "登录按钮选择器：";

        txtWechatWorkSubmitSel.BackColor = Color.FromArgb(248, 248, 248);
        txtWechatWorkSubmitSel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtWechatWorkSubmitSel.Location = new Point(220, 1185);
        txtWechatWorkSubmitSel.Name = "txtWechatWorkSubmitSel";
        txtWechatWorkSubmitSel.PlaceholderText = "例:.btn-login-1";
        txtWechatWorkSubmitSel.Size = new Size(500, 38);
        txtWechatWorkSubmitSel.TabIndex = 39;
        // 
        // sepAutoLogin
        // 
        sepAutoLogin.BackColor = Color.FromArgb(220, 220, 220);
        sepAutoLogin.Location = new Point(36, 1250);
        sepAutoLogin.Name = "sepAutoLogin";
        sepAutoLogin.Size = new Size(1080, 1);
        sepAutoLogin.TabIndex = 36;
        // 
        // chkClientAutoLogin
        // 
        chkClientAutoLogin.Checked = true;
        chkClientAutoLogin.CheckState = CheckState.Checked;
        chkClientAutoLogin.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        chkClientAutoLogin.ForeColor = Color.FromArgb(24, 145, 176);
        chkClientAutoLogin.Location = new Point(36, 1265);
        chkClientAutoLogin.Name = "chkClientAutoLogin";
        chkClientAutoLogin.Size = new Size(500, 36);
        chkClientAutoLogin.TabIndex = 37;
        chkClientAutoLogin.Text = "🚀 启动 A3 客户端时自动登录";
        chkClientAutoLogin.UseVisualStyleBackColor = true;
        // 
        // chkDevToolsAutoLogin
        // 
        chkDevToolsAutoLogin.Checked = true;
        chkDevToolsAutoLogin.CheckState = CheckState.Checked;
        chkDevToolsAutoLogin.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        chkDevToolsAutoLogin.ForeColor = Color.FromArgb(24, 145, 176);
        chkDevToolsAutoLogin.Location = new Point(36, 1305);
        chkDevToolsAutoLogin.Name = "chkDevToolsAutoLogin";
        chkDevToolsAutoLogin.Size = new Size(500, 36);
        chkDevToolsAutoLogin.TabIndex = 38;
        chkDevToolsAutoLogin.Text = "🛠️ 启动集成开发工具时自动登录(两步)";
        chkDevToolsAutoLogin.UseVisualStyleBackColor = true;
        // 
        // lblDevToolsPassword
        // 
        lblDevToolsPassword.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblDevToolsPassword.ForeColor = Color.FromArgb(24, 145, 176);
        lblDevToolsPassword.Location = new Point(36, 1360);
        lblDevToolsPassword.Name = "lblDevToolsPassword";
        lblDevToolsPassword.Size = new Size(341, 30);
        lblDevToolsPassword.TabIndex = 33;
        lblDevToolsPassword.Text = "🛠️ 集成开发工具自动登录:";
        lblDevToolsPassword.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtDevToolsPassword
        // 
        txtDevToolsPassword.BackColor = Color.FromArgb(248, 248, 248);
        txtDevToolsPassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtDevToolsPassword.Location = new Point(36, 1400);
        txtDevToolsPassword.Name = "txtDevToolsPassword";
        txtDevToolsPassword.PlaceholderText = "集成开发工具密码(AES 加密存储,所有账套共用)";
        txtDevToolsPassword.Size = new Size(500, 38);
        txtDevToolsPassword.TabIndex = 34;
        txtDevToolsPassword.UseSystemPasswordChar = true;
        // 
        // hintDevTools
        // 
        hintDevTools.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintDevTools.ForeColor = Color.FromArgb(150, 150, 150);
        hintDevTools.Location = new Point(36, 1440);
        hintDevTools.Name = "hintDevTools";
        hintDevTools.Size = new Size(900, 60);
        hintDevTools.TabIndex = 35;
        hintDevTools.Text = "启动集成开发工具时，会先填客户端账号密码（用账套 ServerUsername + ServerPassword），\n通过后再填这个开发工具密码（开发账号默认记住，不需要输入）";
        // 
        // sepHubConfig (Root 专属分隔线)
        // 
        sepHubConfig.BackColor = Color.FromArgb(220, 220, 220);
        sepHubConfig.Location = new Point(36, 1510);
        sepHubConfig.Name = "sepHubConfig";
        sepHubConfig.Size = new Size(1080, 1);
        sepHubConfig.TabIndex = 39;
        // 
        // lblHubConfigTitle
        // 
        lblHubConfigTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblHubConfigTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblHubConfigTitle.Location = new Point(36, 1515);
        lblHubConfigTitle.Name = "lblHubConfigTitle";
        lblHubConfigTitle.Size = new Size(400, 30);
        lblHubConfigTitle.TabIndex = 40;
        lblHubConfigTitle.Text = "🔒 A3ToolsHub 服务端配置（Root 模式）";
        // 
        // hintHubConfig
        // 
        hintHubConfig.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintHubConfig.ForeColor = Color.FromArgb(150, 150, 150);
        hintHubConfig.Location = new Point(36, 1545);
        hintHubConfig.Name = "hintHubConfig";
        hintHubConfig.Size = new Size(900, 30);
        hintHubConfig.TabIndex = 41;
        hintHubConfig.Text = "设置「一键生成配置」按钮将配置文件生成到哪个目录（{账套代码+账套名称}/ 文件夹）";
        // 
        // lblHubConfigDir
        // 
        lblHubConfigDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblHubConfigDir.Location = new Point(36, 1575);
        lblHubConfigDir.Name = "lblHubConfigDir";
        lblHubConfigDir.Size = new Size(220, 50);
        lblHubConfigDir.TabIndex = 42;
        lblHubConfigDir.Text = "配置文件目录：";
        lblHubConfigDir.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtHubConfigDir
        // 
        txtHubConfigDir.BackColor = Color.FromArgb(248, 248, 248);
        txtHubConfigDir.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtHubConfigDir.Location = new Point(36, 1620);
        txtHubConfigDir.Name = "txtHubConfigDir";
        txtHubConfigDir.ReadOnly = true;
        txtHubConfigDir.Size = new Size(800, 41);
        txtHubConfigDir.TabIndex = 43;
        // 
        // btnHubConfigBrowse
        // 
        btnHubConfigBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnHubConfigBrowse.Cursor = Cursors.Hand;
        btnHubConfigBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnHubConfigBrowse.FlatStyle = FlatStyle.Flat;
        btnHubConfigBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnHubConfigBrowse.Location = new Point(844, 1620);
        btnHubConfigBrowse.Name = "btnHubConfigBrowse";
        btnHubConfigBrowse.Size = new Size(150, 41);
        btnHubConfigBrowse.TabIndex = 44;
        btnHubConfigBrowse.Text = "浏览...";
        btnHubConfigBrowse.UseVisualStyleBackColor = false;
        btnHubConfigBrowse.Click += BtnHubConfigBrowse_Click;
        // 
        // sepFont
        // 
        sepFont.BackColor = Color.FromArgb(220, 220, 220);
        sepFont.Location = new Point(36, 30);
        sepFont.Name = "sepFont";
        sepFont.Size = new Size(1080, 1);
        sepFont.TabIndex = 0;
        // 
        // lblFontTitle
        // 
        lblFontTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblFontTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblFontTitle.Location = new Point(36, 48);
        lblFontTitle.Name = "lblFontTitle";
        lblFontTitle.Size = new Size(341, 30);
        lblFontTitle.TabIndex = 1;
        lblFontTitle.Text = "界面字体";
        // 
        // lblFontFamily
        // 
        lblFontFamily.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblFontFamily.Location = new Point(36, 85);
        lblFontFamily.Name = "lblFontFamily";
        lblFontFamily.Size = new Size(90, 40);
        lblFontFamily.TabIndex = 2;
        lblFontFamily.Text = "字体：";
        lblFontFamily.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cmbFontFamily
        // 
        cmbFontFamily.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFontFamily.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        cmbFontFamily.Location = new Point(130, 88);
        cmbFontFamily.Name = "cmbFontFamily";
        cmbFontFamily.Size = new Size(380, 30);
        cmbFontFamily.TabIndex = 3;
        // 
        // lblFontSize
        // 
        lblFontSize.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblFontSize.Location = new Point(530, 85);
        lblFontSize.Name = "lblFontSize";
        lblFontSize.Size = new Size(70, 40);
        lblFontSize.TabIndex = 4;
        lblFontSize.Text = "字号：";
        lblFontSize.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cmbFontSize
        // 
        cmbFontSize.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFontSize.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        cmbFontSize.Items.AddRange(new object[] { "紧凑 (90%)", "标准 (100%)", "宽松 (115%)", "大号 (130%)" });
        cmbFontSize.Location = new Point(600, 88);
        cmbFontSize.Name = "cmbFontSize";
        cmbFontSize.Size = new Size(150, 30);
        cmbFontSize.TabIndex = 5;
        // 
        // lblFontFile
        // 
        lblFontFile.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblFontFile.Location = new Point(36, 140);
        lblFontFile.Name = "lblFontFile";
        lblFontFile.Size = new Size(90, 40);
        lblFontFile.TabIndex = 6;
        lblFontFile.Text = "字体文件：";
        lblFontFile.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtFontFile
        // 
        txtFontFile.BackColor = Color.FromArgb(248, 248, 248);
        txtFontFile.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtFontFile.Location = new Point(130, 143);
        txtFontFile.Name = "txtFontFile";
        txtFontFile.ReadOnly = true;
        txtFontFile.Size = new Size(620, 41);
        txtFontFile.TabIndex = 7;
        // 
        // btnFontFileBrowse
        // 
        btnFontFileBrowse.BackColor = Color.FromArgb(245, 245, 245);
        btnFontFileBrowse.Cursor = Cursors.Hand;
        btnFontFileBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnFontFileBrowse.FlatStyle = FlatStyle.Flat;
        btnFontFileBrowse.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnFontFileBrowse.Location = new Point(760, 143);
        btnFontFileBrowse.Name = "btnFontFileBrowse";
        btnFontFileBrowse.Size = new Size(150, 41);
        btnFontFileBrowse.TabIndex = 8;
        btnFontFileBrowse.Text = "浏览...";
        btnFontFileBrowse.UseVisualStyleBackColor = false;
        btnFontFileBrowse.Click += BtnFontFileBrowse_Click;
        // 
        // btnFontFileClear
        // 
        btnFontFileClear.BackColor = Color.FromArgb(245, 245, 245);
        btnFontFileClear.Cursor = Cursors.Hand;
        btnFontFileClear.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnFontFileClear.FlatStyle = FlatStyle.Flat;
        btnFontFileClear.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnFontFileClear.Location = new Point(920, 143);
        btnFontFileClear.Name = "btnFontFileClear";
        btnFontFileClear.Size = new Size(120, 41);
        btnFontFileClear.TabIndex = 9;
        btnFontFileClear.Text = "清除";
        btnFontFileClear.UseVisualStyleBackColor = false;
        btnFontFileClear.Click += BtnFontFileClear_Click;
        // 
        // hintFont
        // 
        hintFont.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        hintFont.ForeColor = Color.FromArgb(150, 150, 150);
        hintFont.Location = new Point(36, 200);
        hintFont.Name = "hintFont";
        hintFont.Size = new Size(1000, 50);
        hintFont.TabIndex = 10;
        hintFont.Text = "字体留空 = 保持各界面原有字体。字号按比例缩放（保留标题与正文的层级差）。\r\n字体文件可选择未安装的 TTF / OTF / TTC，选用后仅本程序使用，不安装到系统。";
        // 
        // bottom
        // 
        bottom.BackColor = Color.FromArgb(248, 248, 248);
        bottom.Controls.Add(btnCancel);
        bottom.Controls.Add(btnOK);
        bottom.Dock = DockStyle.Bottom;
        bottom.Location = new Point(0, 1540);
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
        ClientSize = new Size(1152, 720);
        Controls.Add(bottom);
        Controls.Add(mainPanel);
        Controls.Add(titleBar);
        MinimumSize = new Size(820, 600);
        Name = "SettingsDialog";
        SizeGripStyle = SizeGripStyle.Show;
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
    private CheckBox chkEnableAi;
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
    

    // Root 专属：A3ToolsHub 配置目录
    private Panel sepHubConfig = null!;
    private Label lblHubConfigTitle = null!;
    private Label hintHubConfig = null!;
    private Label lblHubConfigDir = null!;
    private TextBox txtHubConfigDir = null!;
    private Button btnHubConfigBrowse = null!;

    // ★ 2026-11-12 全局界面字体
    private Panel sepFont = null!;
    private Label lblFontTitle = null!;
    private Label lblFontFamily = null!;
    private ComboBox cmbFontFamily = null!;
    private Label lblFontSize = null!;
    private ComboBox cmbFontSize = null!;
    private Label lblFontFile = null!;
    private TextBox txtFontFile = null!;
    private Button btnFontFileBrowse = null!;
    private Button btnFontFileClear = null!;
    private Label hintFont = null!;
}