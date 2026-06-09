namespace A3Tools.Plugins.Default.Forms;

partial class CrossDbCopyAppChartForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    private System.Windows.Forms.TableLayoutPanel sourceLayout;
    private System.Windows.Forms.Label lblSourceTitle;
    private System.Windows.Forms.Label lblSourceServer;
    private System.Windows.Forms.TextBox txtSourceServer;
    private System.Windows.Forms.Label lblSourceDbName;
    private System.Windows.Forms.TextBox txtSourceDbName;
    private System.Windows.Forms.Label lblSourceUser;
    private System.Windows.Forms.TextBox txtSourceUser;
    private System.Windows.Forms.Label lblSourcePassword;
    private System.Windows.Forms.TextBox txtSourcePassword;
    private System.Windows.Forms.Button btnSelectSource;
    private System.Windows.Forms.TableLayoutPanel targetLayout;
    private System.Windows.Forms.Label lblTargetTitle;
    private System.Windows.Forms.Label lblTargetServer;
    private System.Windows.Forms.TextBox txtTargetServer;
    private System.Windows.Forms.Label lblTargetDbName;
    private System.Windows.Forms.TextBox txtTargetDbName;
    private System.Windows.Forms.Label lblTargetUser;
    private System.Windows.Forms.TextBox txtTargetUser;
    private System.Windows.Forms.Label lblTargetPassword;
    private System.Windows.Forms.TextBox txtTargetPassword;
    private System.Windows.Forms.Button btnSelectTarget;
    private System.Windows.Forms.Label lblCode;
    private System.Windows.Forms.TextBox txtCode;
    private System.Windows.Forms.Label lblCodeHint;
    private System.Windows.Forms.CheckBox chkDeleteFirst;
    private System.Windows.Forms.Button btnConfirm;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblProgress;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        this.sourceLayout = new System.Windows.Forms.TableLayoutPanel();
        this.lblSourceTitle = new System.Windows.Forms.Label();
        this.lblSourceServer = new System.Windows.Forms.Label();
        this.txtSourceServer = new System.Windows.Forms.TextBox();
        this.lblSourceDbName = new System.Windows.Forms.Label();
        this.txtSourceDbName = new System.Windows.Forms.TextBox();
        this.lblSourceUser = new System.Windows.Forms.Label();
        this.txtSourceUser = new System.Windows.Forms.TextBox();
        this.lblSourcePassword = new System.Windows.Forms.Label();
        this.txtSourcePassword = new System.Windows.Forms.TextBox();
        this.btnSelectSource = new System.Windows.Forms.Button();
        this.targetLayout = new System.Windows.Forms.TableLayoutPanel();
        this.lblTargetTitle = new System.Windows.Forms.Label();
        this.lblTargetServer = new System.Windows.Forms.Label();
        this.txtTargetServer = new System.Windows.Forms.TextBox();
        this.lblTargetDbName = new System.Windows.Forms.Label();
        this.txtTargetDbName = new System.Windows.Forms.TextBox();
        this.lblTargetUser = new System.Windows.Forms.Label();
        this.txtTargetUser = new System.Windows.Forms.TextBox();
        this.lblTargetPassword = new System.Windows.Forms.Label();
        this.txtTargetPassword = new System.Windows.Forms.TextBox();
        this.btnSelectTarget = new System.Windows.Forms.Button();
        this.lblCode = new System.Windows.Forms.Label();
        this.txtCode = new System.Windows.Forms.TextBox();
        this.lblCodeHint = new System.Windows.Forms.Label();
        this.chkDeleteFirst = new System.Windows.Forms.CheckBox();
        this.btnConfirm = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.lblProgress = new System.Windows.Forms.Label();
        this.tableLayoutPanel1.SuspendLayout();
        this.sourceLayout.SuspendLayout();
        this.targetLayout.SuspendLayout();
        this.SuspendLayout();
        // 
        // tableLayoutPanel1
        // 
        this.tableLayoutPanel1.ColumnCount = 2;
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.tableLayoutPanel1.Controls.Add(this.sourceLayout, 0, 0);
        this.tableLayoutPanel1.Controls.Add(this.targetLayout, 1, 0);
        this.tableLayoutPanel1.Controls.Add(this.lblCode, 0, 1);
        this.tableLayoutPanel1.Controls.Add(this.txtCode, 0, 2);
        this.tableLayoutPanel1.Controls.Add(this.lblCodeHint, 0, 3);
        this.tableLayoutPanel1.Controls.Add(this.chkDeleteFirst, 1, 3);
        this.tableLayoutPanel1.Controls.Add(this.btnConfirm, 0, 4);
        this.tableLayoutPanel1.Controls.Add(this.btnCancel, 1, 4);
        this.tableLayoutPanel1.Controls.Add(this.progressBar, 0, 5);
        this.tableLayoutPanel1.Controls.Add(this.lblProgress, 1, 5);
        this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tableLayoutPanel1.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
        this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 10, 3, 3);
        this.tableLayoutPanel1.Name = "tableLayoutPanel1";
        this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(10);
        this.tableLayoutPanel1.RowCount = 6;
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tableLayoutPanel1.Size = new System.Drawing.Size(873, 480);
        this.tableLayoutPanel1.TabIndex = 0;
        // 
        // sourceLayout
        // 
        this.sourceLayout.BackColor = System.Drawing.Color.FromArgb(245, 248, 250);
        this.sourceLayout.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
        this.sourceLayout.ColumnCount = 2;
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
        this.sourceLayout.Controls.Add(this.lblSourceTitle, 0, 0);
        this.sourceLayout.Controls.Add(this.lblSourceServer, 0, 1);
        this.sourceLayout.Controls.Add(this.txtSourceServer, 1, 1);
        this.sourceLayout.Controls.Add(this.lblSourceDbName, 0, 2);
        this.sourceLayout.Controls.Add(this.txtSourceDbName, 1, 2);
        this.sourceLayout.Controls.Add(this.lblSourceUser, 0, 3);
        this.sourceLayout.Controls.Add(this.txtSourceUser, 1, 3);
        this.sourceLayout.Controls.Add(this.lblSourcePassword, 0, 4);
        this.sourceLayout.Controls.Add(this.txtSourcePassword, 1, 4);
        this.sourceLayout.Controls.Add(this.btnSelectSource, 1, 5);
        this.sourceLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.sourceLayout.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.sourceLayout.Location = new System.Drawing.Point(13, 13);
        this.sourceLayout.Margin = new System.Windows.Forms.Padding(3, 3, 10, 10);
        this.sourceLayout.Name = "sourceLayout";
        this.sourceLayout.RowCount = 6;
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.sourceLayout.Size = new System.Drawing.Size(413, 300);
        this.sourceLayout.TabIndex = 0;
        // 
        // lblSourceTitle
        // 
        this.lblSourceTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
        this.lblSourceTitle.ForeColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.lblSourceTitle.Location = new System.Drawing.Point(4, 4);
        this.lblSourceTitle.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
        this.lblSourceTitle.Name = "lblSourceTitle";
        this.lblSourceTitle.Size = new System.Drawing.Size(117, 30);
        this.lblSourceTitle.TabIndex = 0;
        this.lblSourceTitle.Text = "源数据库";
        this.lblSourceTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // lblSourceServer
        // 
        this.lblSourceServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceServer.Location = new System.Drawing.Point(4, 189);
        this.lblSourceServer.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblSourceServer.Name = "lblSourceServer";
        this.lblSourceServer.Size = new System.Drawing.Size(117, 28);
        this.lblSourceServer.TabIndex = 1;
        this.lblSourceServer.Text = "服务器地址：";
        this.lblSourceServer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtSourceServer
        // 
        this.txtSourceServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourceServer.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtSourceServer.Location = new System.Drawing.Point(128, 189);
        this.txtSourceServer.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtSourceServer.Name = "txtSourceServer";
        this.txtSourceServer.Size = new System.Drawing.Size(281, 28);
        this.txtSourceServer.TabIndex = 2;
        // 
        // lblSourceDbName
        // 
        this.lblSourceDbName.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceDbName.Location = new System.Drawing.Point(4, 239);
        this.lblSourceDbName.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblSourceDbName.Name = "lblSourceDbName";
        this.lblSourceDbName.Size = new System.Drawing.Size(117, 28);
        this.lblSourceDbName.TabIndex = 20;
        this.lblSourceDbName.Text = "数据库名称：";
        this.lblSourceDbName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtSourceDbName
        // 
        this.txtSourceDbName.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourceDbName.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtSourceDbName.Location = new System.Drawing.Point(128, 239);
        this.txtSourceDbName.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtSourceDbName.Name = "txtSourceDbName";
        this.txtSourceDbName.Size = new System.Drawing.Size(281, 28);
        this.txtSourceDbName.TabIndex = 21;
        // 
        // lblSourceUser
        // 
        this.lblSourceUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceUser.Location = new System.Drawing.Point(4, 289);
        this.lblSourceUser.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblSourceUser.Name = "lblSourceUser";
        this.lblSourceUser.Size = new System.Drawing.Size(117, 28);
        this.lblSourceUser.TabIndex = 3;
        this.lblSourceUser.Text = "用户名：";
        this.lblSourceUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtSourceUser
        // 
        this.txtSourceUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourceUser.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtSourceUser.Location = new System.Drawing.Point(128, 289);
        this.txtSourceUser.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtSourceUser.Name = "txtSourceUser";
        this.txtSourceUser.Size = new System.Drawing.Size(281, 28);
        this.txtSourceUser.TabIndex = 4;
        // 
        // lblSourcePassword
        // 
        this.lblSourcePassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourcePassword.Location = new System.Drawing.Point(4, 339);
        this.lblSourcePassword.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblSourcePassword.Name = "lblSourcePassword";
        this.lblSourcePassword.Size = new System.Drawing.Size(117, 28);
        this.lblSourcePassword.TabIndex = 5;
        this.lblSourcePassword.Text = "密码：";
        this.lblSourcePassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtSourcePassword
        // 
        this.txtSourcePassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourcePassword.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtSourcePassword.Location = new System.Drawing.Point(128, 339);
        this.txtSourcePassword.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtSourcePassword.Name = "txtSourcePassword";
        this.txtSourcePassword.Size = new System.Drawing.Size(281, 28);
        this.txtSourcePassword.TabIndex = 6;
        this.txtSourcePassword.UseSystemPasswordChar = true;
        // 
        // btnSelectSource
        // 
        this.btnSelectSource.BackColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnSelectSource.FlatAppearance.BorderSize = 0;
        this.btnSelectSource.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSelectSource.Font = new System.Drawing.Font("微软雅黑", 9F);
        this.btnSelectSource.ForeColor = System.Drawing.Color.White;
        this.btnSelectSource.Location = new System.Drawing.Point(128, 389);
        this.btnSelectSource.Name = "btnSelectSource";
        this.btnSelectSource.Size = new System.Drawing.Size(126, 32);
        this.btnSelectSource.TabIndex = 7;
        this.btnSelectSource.Text = "选择账套";
        this.btnSelectSource.UseVisualStyleBackColor = false;
        this.btnSelectSource.Click += new System.EventHandler(this.BtnSelectSource_Click);
        // 
        // targetLayout
        // 
        this.targetLayout.BackColor = System.Drawing.Color.FromArgb(250, 245, 245);
        this.targetLayout.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
        this.targetLayout.ColumnCount = 2;
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
        this.targetLayout.Controls.Add(this.lblTargetTitle, 0, 0);
        this.targetLayout.Controls.Add(this.lblTargetServer, 0, 1);
        this.targetLayout.Controls.Add(this.txtTargetServer, 1, 1);
        this.targetLayout.Controls.Add(this.lblTargetDbName, 0, 2);
        this.targetLayout.Controls.Add(this.txtTargetDbName, 1, 2);
        this.targetLayout.Controls.Add(this.lblTargetUser, 0, 3);
        this.targetLayout.Controls.Add(this.txtTargetUser, 1, 3);
        this.targetLayout.Controls.Add(this.lblTargetPassword, 0, 4);
        this.targetLayout.Controls.Add(this.txtTargetPassword, 1, 4);
        this.targetLayout.Controls.Add(this.btnSelectTarget, 1, 5);
        this.targetLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.targetLayout.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.targetLayout.Location = new System.Drawing.Point(439, 13);
        this.targetLayout.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
        this.targetLayout.Name = "targetLayout";
        this.targetLayout.RowCount = 6;
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.targetLayout.Size = new System.Drawing.Size(421, 300);
        this.targetLayout.TabIndex = 1;
        // 
        // lblTargetTitle
        // 
        this.lblTargetTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
        this.lblTargetTitle.ForeColor = System.Drawing.Color.FromArgb(200, 80, 80);
        this.lblTargetTitle.Location = new System.Drawing.Point(4, 4);
        this.lblTargetTitle.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
        this.lblTargetTitle.Name = "lblTargetTitle";
        this.lblTargetTitle.Size = new System.Drawing.Size(119, 30);
        this.lblTargetTitle.TabIndex = 0;
        this.lblTargetTitle.Text = "目标数据库";
        this.lblTargetTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // lblTargetServer
        // 
        this.lblTargetServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetServer.Location = new System.Drawing.Point(4, 189);
        this.lblTargetServer.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblTargetServer.Name = "lblTargetServer";
        this.lblTargetServer.Size = new System.Drawing.Size(119, 28);
        this.lblTargetServer.TabIndex = 1;
        this.lblTargetServer.Text = "服务器地址：";
        this.lblTargetServer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtTargetServer
        // 
        this.txtTargetServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetServer.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTargetServer.Location = new System.Drawing.Point(130, 189);
        this.txtTargetServer.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtTargetServer.Name = "txtTargetServer";
        this.txtTargetServer.Size = new System.Drawing.Size(287, 28);
        this.txtTargetServer.TabIndex = 8;
        // 
        // lblTargetDbName
        // 
        this.lblTargetDbName.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetDbName.Location = new System.Drawing.Point(4, 239);
        this.lblTargetDbName.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblTargetDbName.Name = "lblTargetDbName";
        this.lblTargetDbName.Size = new System.Drawing.Size(119, 28);
        this.lblTargetDbName.TabIndex = 22;
        this.lblTargetDbName.Text = "数据库名称：";
        this.lblTargetDbName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtTargetDbName
        // 
        this.txtTargetDbName.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetDbName.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTargetDbName.Location = new System.Drawing.Point(130, 239);
        this.txtTargetDbName.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtTargetDbName.Name = "txtTargetDbName";
        this.txtTargetDbName.Size = new System.Drawing.Size(287, 28);
        this.txtTargetDbName.TabIndex = 23;
        // 
        // lblTargetUser
        // 
        this.lblTargetUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetUser.Location = new System.Drawing.Point(4, 289);
        this.lblTargetUser.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblTargetUser.Name = "lblTargetUser";
        this.lblTargetUser.Size = new System.Drawing.Size(119, 28);
        this.lblTargetUser.TabIndex = 9;
        this.lblTargetUser.Text = "用户名：";
        this.lblTargetUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtTargetUser
        // 
        this.txtTargetUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetUser.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTargetUser.Location = new System.Drawing.Point(130, 289);
        this.txtTargetUser.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtTargetUser.Name = "txtTargetUser";
        this.txtTargetUser.Size = new System.Drawing.Size(287, 28);
        this.txtTargetUser.TabIndex = 10;
        // 
        // lblTargetPassword
        // 
        this.lblTargetPassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetPassword.Location = new System.Drawing.Point(4, 339);
        this.lblTargetPassword.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.lblTargetPassword.Name = "lblTargetPassword";
        this.lblTargetPassword.Size = new System.Drawing.Size(119, 28);
        this.lblTargetPassword.TabIndex = 11;
        this.lblTargetPassword.Text = "密码：";
        this.lblTargetPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // txtTargetPassword
        // 
        this.txtTargetPassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetPassword.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTargetPassword.Location = new System.Drawing.Point(130, 339);
        this.txtTargetPassword.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this.txtTargetPassword.Name = "txtTargetPassword";
        this.txtTargetPassword.Size = new System.Drawing.Size(287, 28);
        this.txtTargetPassword.TabIndex = 12;
        this.txtTargetPassword.UseSystemPasswordChar = true;
        // 
        // btnSelectTarget
        // 
        this.btnSelectTarget.BackColor = System.Drawing.Color.FromArgb(200, 80, 80);
        this.btnSelectTarget.FlatAppearance.BorderSize = 0;
        this.btnSelectTarget.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSelectTarget.Font = new System.Drawing.Font("微软雅黑", 9F);
        this.btnSelectTarget.ForeColor = System.Drawing.Color.White;
        this.btnSelectTarget.Location = new System.Drawing.Point(130, 389);
        this.btnSelectTarget.Name = "btnSelectTarget";
        this.btnSelectTarget.Size = new System.Drawing.Size(125, 32);
        this.btnSelectTarget.TabIndex = 13;
        this.btnSelectTarget.Text = "选择账套";
        this.btnSelectTarget.UseVisualStyleBackColor = false;
        this.btnSelectTarget.Click += new System.EventHandler(this.BtnSelectTarget_Click);
        // 
        // lblCode
        // 
        this.lblCode.AutoSize = true;
        this.lblCode.Font = new System.Drawing.Font("微软雅黑", 11F);
        this.lblCode.Location = new System.Drawing.Point(13, 458);
        this.lblCode.Margin = new System.Windows.Forms.Padding(3, 10, 3, 3);
        this.lblCode.Name = "lblCode";
        this.lblCode.Size = new System.Drawing.Size(308, 22);
        this.lblCode.TabIndex = 2;
        this.lblCode.Text = "看板CODE（多个用分号隔开）：";
        this.lblCode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // txtCode
        // 
        this.tableLayoutPanel1.SetColumnSpan(this.txtCode, 2);
        this.txtCode.Multiline = true;
        this.txtCode.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtCode.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtCode.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtCode.Location = new System.Drawing.Point(13, 493);
        this.txtCode.Name = "txtCode";
        this.txtCode.Size = new System.Drawing.Size(847, 80);
        this.txtCode.TabIndex = 14;
        // 
        // lblCodeHint
        // 
        this.tableLayoutPanel1.SetColumnSpan(this.lblCodeHint, 2);
        this.lblCodeHint.AutoSize = true;
        this.lblCodeHint.Font = new System.Drawing.Font("微软雅黑", 9F);
        this.lblCodeHint.ForeColor = System.Drawing.Color.Gray;
        this.lblCodeHint.Location = new System.Drawing.Point(13, 580);
        this.lblCodeHint.Margin = new System.Windows.Forms.Padding(3);
        this.lblCodeHint.Name = "lblCodeHint";
        this.lblCodeHint.Size = new System.Drawing.Size(336, 18);
        this.lblCodeHint.TabIndex = 3;
        this.lblCodeHint.Text = "提示：输入S_APP_CHART表的CODE，多个用英文分号;隔开";
        // 
        // chkDeleteFirst
        // 
        this.chkDeleteFirst.AutoSize = true;
        this.chkDeleteFirst.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.chkDeleteFirst.Location = new System.Drawing.Point(439, 605);
        this.chkDeleteFirst.Name = "chkDeleteFirst";
        this.chkDeleteFirst.Size = new System.Drawing.Size(150, 24);
        this.chkDeleteFirst.TabIndex = 15;
        this.chkDeleteFirst.Text = "先删除目标数据";
        this.chkDeleteFirst.UseVisualStyleBackColor = true;
        // 
        // btnConfirm
        // 
        this.btnConfirm.BackColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnConfirm.FlatAppearance.BorderSize = 0;
        this.btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnConfirm.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnConfirm.ForeColor = System.Drawing.Color.White;
        this.btnConfirm.Location = new System.Drawing.Point(13, 640);
        this.btnConfirm.Name = "btnConfirm";
        this.btnConfirm.Size = new System.Drawing.Size(180, 40);
        this.btnConfirm.TabIndex = 16;
        this.btnConfirm.Text = "确认复制";
        this.btnConfirm.UseVisualStyleBackColor = false;
        this.btnConfirm.Click += new System.EventHandler(this.BtnConfirm_Click);
        // 
        // btnCancel
        // 
        this.btnCancel.BackColor = System.Drawing.Color.White;
        this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
        this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnCancel.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnCancel.ForeColor = System.Drawing.Color.Gray;
        this.btnCancel.Location = new System.Drawing.Point(439, 640);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(180, 40);
        this.btnCancel.TabIndex = 17;
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = false;
        this.btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
        // 
        // progressBar
        // 
        this.progressBar.Location = new System.Drawing.Point(13, 690);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(400, 25);
        this.progressBar.TabIndex = 18;
        // 
        // lblProgress
        // 
        this.lblProgress.AutoSize = true;
        this.lblProgress.Font = new System.Drawing.Font("微软雅黑", 9F);
        this.lblProgress.ForeColor = System.Drawing.Color.Gray;
        this.lblProgress.Location = new System.Drawing.Point(439, 690);
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.Size = new System.Drawing.Size(50, 18);
        this.lblProgress.TabIndex = 19;
        this.lblProgress.Text = "就绪";
        // 
        // CrossDbCopyAppChartForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 30F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(873, 730);
        this.Controls.Add(this.tableLayoutPanel1);
        this.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "CrossDbCopyAppChartForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "复制移动看板";
        this.tableLayoutPanel1.ResumeLayout(false);
        this.tableLayoutPanel1.PerformLayout();
        this.sourceLayout.ResumeLayout(false);
        this.sourceLayout.PerformLayout();
        this.targetLayout.ResumeLayout(false);
        this.targetLayout.PerformLayout();
        this.ResumeLayout(false);
    }
}