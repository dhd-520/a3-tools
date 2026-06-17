namespace A3Tools.Forms;

partial class AccountDialog
{
    private System.ComponentModel.IContainer components = null;

    // ===== 窗体分区 =====
    private Panel titleBar = null!;
    private Label lblTitle = null!;
    private Panel contentPanel = null!;
    private Panel footerPanel = null!;

    // ===== 基本信息 =====
    private Label lblCode = null!;
    private TextBox txtCode = null!;
    private Label lblName = null!;
    private TextBox txtName = null!;
    private Label lblServer = null!;
    private TextBox txtServer = null!;
    private Label lblServerPassword = null!;
    private TextBox txtServerPassword = null!;
    private Label lblDatabase = null!;
    private TextBox txtDatabase = null!;
    private Label lblDatabaseName = null!;
    private TextBox txtDatabaseName = null!;
    private Label lblDbUser = null!;
    private TextBox txtDbUser = null!;
    private Label lblDbPassword = null!;
    private TextBox txtDbPassword = null!;

    // ===== 远程信息 =====
    private Label lblRemoteType = null!;
    private ComboBox cboRemoteType = null!;
    private Label lblRemoteAddress = null!;
    private TextBox txtRemoteAddress = null!;
    private Label lblRemoteUser = null!;
    private TextBox txtRemoteUser = null!;
    private Label lblRemotePassword = null!;
    private TextBox txtRemotePassword = null!;

    // ===== 备注 =====
    private Label lblRemark = null!;
    private TextBox txtRemark = null!;

    // ===== 网页版自动登录 =====
    private Panel pnlWebGroup = null!;
    private Label lblWebHint = null!;
    private Label lblServerUsername = null!;
    private TextBox txtServerUsername = null!;

    // ===== 按钮 =====
    private Button btnSave = null!;
    private Button btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        titleBar = new Panel();
        lblTitle = new Label();
        contentPanel = new Panel();
        lblCode = new Label();
        txtCode = new TextBox();
        lblName = new Label();
        txtName = new TextBox();
        lblServer = new Label();
        txtServer = new TextBox();
        lblServerPassword = new Label();
        txtServerPassword = new TextBox();
        lblDatabase = new Label();
        txtDatabase = new TextBox();
        lblDatabaseName = new Label();
        txtDatabaseName = new TextBox();
        lblDbUser = new Label();
        txtDbUser = new TextBox();
        lblDbPassword = new Label();
        txtDbPassword = new TextBox();
        lblRemoteType = new Label();
        cboRemoteType = new ComboBox();
        lblRemoteAddress = new Label();
        txtRemoteAddress = new TextBox();
        lblRemoteUser = new Label();
        txtRemoteUser = new TextBox();
        lblRemotePassword = new Label();
        txtRemotePassword = new TextBox();
        lblRemark = new Label();
        txtRemark = new TextBox();
        pnlWebGroup = new Panel();
        lblWebHint = new Label();
        lblServerUsername = new Label();
        txtServerUsername = new TextBox();
        footerPanel = new Panel();
        btnCancel = new Button();
        btnSave = new Button();
        titleBar.SuspendLayout();
        contentPanel.SuspendLayout();
        pnlWebGroup.SuspendLayout();
        footerPanel.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 0);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(942, 50);
        titleBar.TabIndex = 0;
        // 
        // lblTitle
        // 
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(942, 50);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "账套编辑";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // contentPanel
        // 
        contentPanel.AutoScroll = true;
        contentPanel.BackColor = Color.White;
        contentPanel.Controls.Add(lblCode);
        contentPanel.Controls.Add(txtCode);
        contentPanel.Controls.Add(lblName);
        contentPanel.Controls.Add(txtName);
        contentPanel.Controls.Add(lblServer);
        contentPanel.Controls.Add(txtServer);
        contentPanel.Controls.Add(lblServerPassword);
        contentPanel.Controls.Add(txtServerPassword);
        contentPanel.Controls.Add(lblDatabase);
        contentPanel.Controls.Add(txtDatabase);
        contentPanel.Controls.Add(lblDatabaseName);
        contentPanel.Controls.Add(txtDatabaseName);
        contentPanel.Controls.Add(lblDbUser);
        contentPanel.Controls.Add(txtDbUser);
        contentPanel.Controls.Add(lblDbPassword);
        contentPanel.Controls.Add(txtDbPassword);
        contentPanel.Controls.Add(lblRemoteType);
        contentPanel.Controls.Add(cboRemoteType);
        contentPanel.Controls.Add(lblRemoteAddress);
        contentPanel.Controls.Add(txtRemoteAddress);
        contentPanel.Controls.Add(lblRemoteUser);
        contentPanel.Controls.Add(txtRemoteUser);
        contentPanel.Controls.Add(lblRemotePassword);
        contentPanel.Controls.Add(txtRemotePassword);
        contentPanel.Controls.Add(lblRemark);
        contentPanel.Controls.Add(txtRemark);
        contentPanel.Controls.Add(pnlWebGroup);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 50);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(30, 20, 30, 20);
        contentPanel.Size = new Size(942, 794);
        contentPanel.TabIndex = 1;
        // 
        // lblCode
        // 
        lblCode.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblCode.ForeColor = Color.FromArgb(80, 80, 80);
        lblCode.Location = new Point(30, 20);
        lblCode.Name = "lblCode";
        lblCode.Size = new Size(210, 35);
        lblCode.TabIndex = 0;
        lblCode.Text = "代码：";
        lblCode.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtCode
        // 
        txtCode.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtCode.Location = new Point(250, 20);
        txtCode.Name = "txtCode";
        txtCode.Size = new Size(590, 41);
        txtCode.TabIndex = 1;
        // 
        // lblName
        // 
        lblName.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblName.ForeColor = Color.FromArgb(80, 80, 80);
        lblName.Location = new Point(30, 65);
        lblName.Name = "lblName";
        lblName.Size = new Size(210, 35);
        lblName.TabIndex = 2;
        lblName.Text = "账套名称：";
        lblName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtName
        // 
        txtName.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtName.Location = new Point(250, 65);
        txtName.Name = "txtName";
        txtName.Size = new Size(590, 41);
        txtName.TabIndex = 3;
        // 
        // lblServer
        // 
        lblServer.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblServer.ForeColor = Color.FromArgb(80, 80, 80);
        lblServer.Location = new Point(30, 110);
        lblServer.Name = "lblServer";
        lblServer.Size = new Size(210, 35);
        lblServer.TabIndex = 4;
        lblServer.Text = "账套地址：";
        lblServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtServer
        // 
        txtServer.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtServer.Location = new Point(250, 110);
        txtServer.Name = "txtServer";
        txtServer.Size = new Size(590, 41);
        txtServer.TabIndex = 5;
        // 
        // lblServerPassword
        // 
        lblServerPassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblServerPassword.ForeColor = Color.FromArgb(80, 80, 80);
        lblServerPassword.Location = new Point(30, 155);
        lblServerPassword.Name = "lblServerPassword";
        lblServerPassword.Size = new Size(210, 35);
        lblServerPassword.TabIndex = 6;
        lblServerPassword.Text = "账套密码：";
        lblServerPassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtServerPassword
        // 
        txtServerPassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtServerPassword.Location = new Point(250, 155);
        txtServerPassword.Name = "txtServerPassword";
        txtServerPassword.Size = new Size(590, 41);
        txtServerPassword.TabIndex = 7;
        txtServerPassword.UseSystemPasswordChar = true;
        // 
        // lblDatabase
        // 
        lblDatabase.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblDatabase.ForeColor = Color.FromArgb(80, 80, 80);
        lblDatabase.Location = new Point(30, 200);
        lblDatabase.Name = "lblDatabase";
        lblDatabase.Size = new Size(210, 35);
        lblDatabase.TabIndex = 8;
        lblDatabase.Text = "数据库地址：";
        lblDatabase.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtDatabase
        // 
        txtDatabase.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtDatabase.Location = new Point(250, 200);
        txtDatabase.Name = "txtDatabase";
        txtDatabase.Size = new Size(590, 41);
        txtDatabase.TabIndex = 9;
        // 
        // lblDatabaseName
        // 
        lblDatabaseName.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblDatabaseName.ForeColor = Color.FromArgb(80, 80, 80);
        lblDatabaseName.Location = new Point(30, 245);
        lblDatabaseName.Name = "lblDatabaseName";
        lblDatabaseName.Size = new Size(210, 35);
        lblDatabaseName.TabIndex = 10;
        lblDatabaseName.Text = "数据库名称：";
        lblDatabaseName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtDatabaseName
        // 
        txtDatabaseName.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtDatabaseName.Location = new Point(250, 245);
        txtDatabaseName.Name = "txtDatabaseName";
        txtDatabaseName.Size = new Size(590, 41);
        txtDatabaseName.TabIndex = 11;
        // 
        // lblDbUser
        // 
        lblDbUser.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblDbUser.ForeColor = Color.FromArgb(80, 80, 80);
        lblDbUser.Location = new Point(30, 290);
        lblDbUser.Name = "lblDbUser";
        lblDbUser.Size = new Size(210, 35);
        lblDbUser.TabIndex = 12;
        lblDbUser.Text = "DB用户名：";
        lblDbUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtDbUser
        // 
        txtDbUser.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtDbUser.Location = new Point(250, 290);
        txtDbUser.Name = "txtDbUser";
        txtDbUser.Size = new Size(590, 41);
        txtDbUser.TabIndex = 13;
        // 
        // lblDbPassword
        // 
        lblDbPassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblDbPassword.ForeColor = Color.FromArgb(80, 80, 80);
        lblDbPassword.Location = new Point(30, 335);
        lblDbPassword.Name = "lblDbPassword";
        lblDbPassword.Size = new Size(210, 35);
        lblDbPassword.TabIndex = 14;
        lblDbPassword.Text = "DB密码：";
        lblDbPassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtDbPassword
        // 
        txtDbPassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtDbPassword.Location = new Point(250, 335);
        txtDbPassword.Name = "txtDbPassword";
        txtDbPassword.Size = new Size(590, 41);
        txtDbPassword.TabIndex = 15;
        txtDbPassword.UseSystemPasswordChar = true;
        // 
        // lblRemoteType
        // 
        lblRemoteType.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemoteType.ForeColor = Color.FromArgb(80, 80, 80);
        lblRemoteType.Location = new Point(30, 390);
        lblRemoteType.Name = "lblRemoteType";
        lblRemoteType.Size = new Size(210, 35);
        lblRemoteType.TabIndex = 16;
        lblRemoteType.Text = "远程方式：";
        lblRemoteType.TextAlign = ContentAlignment.MiddleRight;
        // 
        // cboRemoteType
        // 
        cboRemoteType.DropDownStyle = ComboBoxStyle.DropDownList;
        cboRemoteType.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        cboRemoteType.Items.AddRange(new object[] { "RDP", "向日葵", "其他" });
        cboRemoteType.Location = new Point(250, 390);
        cboRemoteType.Name = "cboRemoteType";
        cboRemoteType.Size = new Size(590, 43);
        cboRemoteType.TabIndex = 17;
        // 
        // lblRemoteAddress
        // 
        lblRemoteAddress.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemoteAddress.ForeColor = Color.FromArgb(80, 80, 80);
        lblRemoteAddress.Location = new Point(30, 435);
        lblRemoteAddress.Name = "lblRemoteAddress";
        lblRemoteAddress.Size = new Size(210, 35);
        lblRemoteAddress.TabIndex = 18;
        lblRemoteAddress.Text = "远程地址：";
        lblRemoteAddress.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtRemoteAddress
        // 
        txtRemoteAddress.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtRemoteAddress.Location = new Point(250, 435);
        txtRemoteAddress.Name = "txtRemoteAddress";
        txtRemoteAddress.Size = new Size(590, 41);
        txtRemoteAddress.TabIndex = 19;
        // 
        // lblRemoteUser
        // 
        lblRemoteUser.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemoteUser.ForeColor = Color.FromArgb(80, 80, 80);
        lblRemoteUser.Location = new Point(30, 480);
        lblRemoteUser.Name = "lblRemoteUser";
        lblRemoteUser.Size = new Size(210, 35);
        lblRemoteUser.TabIndex = 20;
        lblRemoteUser.Text = "远程用户名：";
        lblRemoteUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtRemoteUser
        // 
        txtRemoteUser.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtRemoteUser.Location = new Point(250, 480);
        txtRemoteUser.Name = "txtRemoteUser";
        txtRemoteUser.Size = new Size(590, 41);
        txtRemoteUser.TabIndex = 21;
        // 
        // lblRemotePassword
        // 
        lblRemotePassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemotePassword.ForeColor = Color.FromArgb(80, 80, 80);
        lblRemotePassword.Location = new Point(30, 525);
        lblRemotePassword.Name = "lblRemotePassword";
        lblRemotePassword.Size = new Size(210, 35);
        lblRemotePassword.TabIndex = 22;
        lblRemotePassword.Text = "远程密码：";
        lblRemotePassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtRemotePassword
        // 
        txtRemotePassword.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtRemotePassword.Location = new Point(250, 525);
        txtRemotePassword.Name = "txtRemotePassword";
        txtRemotePassword.Size = new Size(590, 41);
        txtRemotePassword.TabIndex = 23;
        txtRemotePassword.UseSystemPasswordChar = true;
        // 
        // lblRemark
        // 
        lblRemark.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblRemark.ForeColor = Color.FromArgb(80, 80, 80);
        lblRemark.Location = new Point(30, 570);
        lblRemark.Name = "lblRemark";
        lblRemark.Size = new Size(210, 35);
        lblRemark.TabIndex = 24;
        lblRemark.Text = "备注：";
        lblRemark.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtRemark
        // 
        txtRemark.BorderStyle = BorderStyle.FixedSingle;
        txtRemark.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtRemark.Location = new Point(250, 570);
        txtRemark.Multiline = true;
        txtRemark.Name = "txtRemark";
        txtRemark.ScrollBars = ScrollBars.Vertical;
        txtRemark.Size = new Size(590, 60);
        txtRemark.TabIndex = 25;
        // 
        // pnlWebGroup
        // 
        pnlWebGroup.BackColor = Color.FromArgb(245, 248, 250);
        pnlWebGroup.BorderStyle = BorderStyle.FixedSingle;
        pnlWebGroup.Controls.Add(lblWebHint);
        pnlWebGroup.Controls.Add(lblServerUsername);
        pnlWebGroup.Controls.Add(txtServerUsername);
        pnlWebGroup.Location = new Point(30, 650);
        pnlWebGroup.Name = "pnlWebGroup";
        pnlWebGroup.Size = new Size(840, 110);
        pnlWebGroup.TabIndex = 26;
        // 
        // lblWebHint
        // 
        lblWebHint.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblWebHint.ForeColor = Color.FromArgb(24, 145, 176);
        lblWebHint.Location = new Point(15, 10);
        lblWebHint.Name = "lblWebHint";
        lblWebHint.Size = new Size(810, 30);
        lblWebHint.TabIndex = 0;
        lblWebHint.Text = "🌐 自动登录配置（账套用户名用于客户端/开发工具/网页三处共用；客户端密码=账套密码；开发工具密码=单独加密存储）";
        // 
        // lblServerUsername
        // 
        lblServerUsername.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblServerUsername.ForeColor = Color.FromArgb(80, 80, 80);
        lblServerUsername.Location = new Point(15, 55);
        lblServerUsername.Name = "lblServerUsername";
        lblServerUsername.Size = new Size(210, 35);
        lblServerUsername.TabIndex = 1;
        lblServerUsername.Text = "账套用户名：";
        lblServerUsername.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtServerUsername
        // 
        txtServerUsername.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtServerUsername.Location = new Point(235, 55);
        txtServerUsername.Name = "txtServerUsername";
        txtServerUsername.Size = new Size(590, 41);
        txtServerUsername.TabIndex = 2;
        // 
        // footerPanel
        // 
        footerPanel.BackColor = Color.FromArgb(245, 245, 245);
        footerPanel.Controls.Add(btnCancel);
        footerPanel.Controls.Add(btnSave);
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Location = new Point(0, 844);
        footerPanel.Name = "footerPanel";
        footerPanel.Size = new Size(942, 80);
        footerPanel.TabIndex = 2;
        // 
        // btnCancel
        // 
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.Cursor = Cursors.Hand;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.ForeColor = Color.FromArgb(80, 80, 80);
        btnCancel.Location = new Point(772, 14);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(150, 53);
        btnCancel.TabIndex = 0;
        btnCancel.Text = "✖ 取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // btnSave
        // 
        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSave.BackColor = Color.FromArgb(24, 145, 176);
        btnSave.Cursor = Cursors.Hand;
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.Font = new Font("微软雅黑", 12F, FontStyle.Bold, GraphicsUnit.Point);
        btnSave.ForeColor = Color.White;
        btnSave.Location = new Point(607, 14);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(150, 53);
        btnSave.TabIndex = 1;
        btnSave.Text = "💾 保存";
        btnSave.UseVisualStyleBackColor = false;
        btnSave.Click += BtnSave_Click;
        // 
        // AccountDialog
        // 
        AutoScaleDimensions = new SizeF(16F, 35F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(942, 924);
        Controls.Add(contentPanel);
        Controls.Add(footerPanel);
        Controls.Add(titleBar);
        Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "AccountDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "账套编辑";
        titleBar.ResumeLayout(false);
        contentPanel.ResumeLayout(false);
        contentPanel.PerformLayout();
        pnlWebGroup.ResumeLayout(false);
        pnlWebGroup.PerformLayout();
        footerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
