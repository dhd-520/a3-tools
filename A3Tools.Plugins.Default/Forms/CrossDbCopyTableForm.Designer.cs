﻿﻿﻿namespace A3Tools.Plugins.Default.Forms;

partial class CrossDbCopyTableForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel tableLayoutPanel1;
    private TableLayoutPanel sourceLayout;
    private Label lblSourceTitle;
    private Label lblSourceServer;
    private TextBox txtSourceServer;
    private Label lblSourceDbName;
    private TextBox txtSourceDbName;
    private Label lblSourceUser;
    private TextBox txtSourceUser;
    private Label lblSourcePassword;
    private TextBox txtSourcePassword;
    private Button btnSelectSource;
    private TableLayoutPanel targetLayout;
    private Label lblTargetTitle;
    private Label lblTargetServer;
    private TextBox txtTargetServer;
    private Label lblTargetDbName;
    private TextBox txtTargetDbName;
    private Label lblTargetUser;
    private TextBox txtTargetUser;
    private Label lblTargetPassword;
    private TextBox txtTargetPassword;
    private Button btnSelectTarget;
    private Label lblObjectType;
    private ComboBox cboObjectType;
    private Label lblObjects;
    private TextBox txtObjects;
    private Label lblObjectsHint;
    private CheckBox chkDeleteIfExists;
    private Panel spacerPanel;
    private Button btnConfirm;
    private Button btnCancel;
    private ProgressBar progressBar;
    private Label lblProgress;

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
        tableLayoutPanel1 = new TableLayoutPanel();
        sourceLayout = new TableLayoutPanel();
        lblSourceTitle = new Label();
        lblSourceServer = new Label();
        txtSourceServer = new TextBox();
        lblSourceDbName = new Label();
        txtSourceDbName = new TextBox();
        lblSourceUser = new Label();
        txtSourceUser = new TextBox();
        lblSourcePassword = new Label();
        txtSourcePassword = new TextBox();
        btnSelectSource = new Button();
        targetLayout = new TableLayoutPanel();
        lblTargetTitle = new Label();
        lblTargetServer = new Label();
        txtTargetServer = new TextBox();
        lblTargetDbName = new Label();
        txtTargetDbName = new TextBox();
        lblTargetUser = new Label();
        txtTargetUser = new TextBox();
        lblTargetPassword = new Label();
        txtTargetPassword = new TextBox();
        btnSelectTarget = new Button();
        lblObjectType = new Label();
        cboObjectType = new ComboBox();
        lblObjects = new Label();
        txtObjects = new TextBox();
        lblObjectsHint = new Label();
        chkDeleteIfExists = new CheckBox();
        btnConfirm = new Button();
        btnCancel = new Button();
        lblProgress = new Label();
        progressBar = new ProgressBar();
        spacerPanel = new Panel();
        tableLayoutPanel1.SuspendLayout();
        sourceLayout.SuspendLayout();
        targetLayout.SuspendLayout();
        SuspendLayout();
        // 
        // tableLayoutPanel1
        // 
        tableLayoutPanel1.ColumnCount = 2;
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.Controls.Add(sourceLayout, 0, 0);
        tableLayoutPanel1.Controls.Add(targetLayout, 1, 0);
        tableLayoutPanel1.Controls.Add(lblObjectType, 0, 1);
        tableLayoutPanel1.Controls.Add(cboObjectType, 1, 1);
        tableLayoutPanel1.Controls.Add(lblObjects, 0, 2);
        tableLayoutPanel1.SetColumnSpan(lblObjects, 2);
        tableLayoutPanel1.Controls.Add(txtObjects, 0, 3);
        tableLayoutPanel1.SetColumnSpan(txtObjects, 2);
        tableLayoutPanel1.Controls.Add(lblObjectsHint, 0, 4);
        tableLayoutPanel1.SetColumnSpan(lblObjectsHint, 2);
        tableLayoutPanel1.Controls.Add(chkDeleteIfExists, 0, 5);
        tableLayoutPanel1.SetColumnSpan(chkDeleteIfExists, 2);
        tableLayoutPanel1.Controls.Add(btnConfirm, 0, 6);
        tableLayoutPanel1.Controls.Add(btnCancel, 1, 6);
        tableLayoutPanel1.Controls.Add(lblProgress, 1, 7);
        tableLayoutPanel1.Controls.Add(progressBar, 0, 7);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        tableLayoutPanel1.Location = new Point(0, 0);
        tableLayoutPanel1.Margin = new Padding(3, 10, 3, 3);
        tableLayoutPanel1.Name = "tableLayoutPanel1";
        tableLayoutPanel1.Padding = new Padding(10);
        tableLayoutPanel1.RowCount = 8;
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.Size = new Size(873, 750);
        tableLayoutPanel1.TabIndex = 0;
        // 
        // sourceLayout
        // 
        sourceLayout.BackColor = Color.FromArgb(245, 248, 250);
        sourceLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
        sourceLayout.ColumnCount = 2;
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        sourceLayout.Controls.Add(lblSourceTitle, 0, 0);
        sourceLayout.Controls.Add(lblSourceServer, 0, 1);
        sourceLayout.Controls.Add(txtSourceServer, 1, 1);
        sourceLayout.Controls.Add(lblSourceDbName, 0, 2);
        sourceLayout.Controls.Add(txtSourceDbName, 1, 2);
        sourceLayout.Controls.Add(lblSourceUser, 0, 3);
        sourceLayout.Controls.Add(txtSourceUser, 1, 3);
        sourceLayout.Controls.Add(lblSourcePassword, 0, 4);
        sourceLayout.Controls.Add(txtSourcePassword, 1, 4);
        sourceLayout.Controls.Add(btnSelectSource, 1, 5);
        sourceLayout.Dock = DockStyle.Fill;
        sourceLayout.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        sourceLayout.Location = new Point(13, 13);
        sourceLayout.Margin = new Padding(3, 3, 10, 10);
        sourceLayout.Name = "sourceLayout";
        sourceLayout.RowCount = 6;
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.Size = new Size(413, 300);
        sourceLayout.TabIndex = 0;
        // 
        // lblSourceTitle
        // 
        lblSourceTitle.Dock = DockStyle.Fill;
        lblSourceTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblSourceTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblSourceTitle.Location = new Point(4, 4);
        lblSourceTitle.Margin = new Padding(3, 3, 3, 10);
        lblSourceTitle.Name = "lblSourceTitle";
        lblSourceTitle.Size = new Size(117, 30);
        lblSourceTitle.TabIndex = 0;
        lblSourceTitle.Text = "源数据库";
        lblSourceTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblSourceServer
        // 
        lblSourceServer.Dock = DockStyle.Fill;
        lblSourceServer.Location = new Point(4, 48);
        lblSourceServer.Margin = new Padding(3, 3, 3, 8);
        lblSourceServer.Name = "lblSourceServer";
        lblSourceServer.Size = new Size(117, 38);
        lblSourceServer.TabIndex = 1;
        lblSourceServer.Text = "服务器地址：";
        lblSourceServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceServer
        // 
        txtSourceServer.Dock = DockStyle.Fill;
        txtSourceServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceServer.Location = new Point(128, 48);
        txtSourceServer.Margin = new Padding(3, 3, 3, 8);
        txtSourceServer.Name = "txtSourceServer";
        txtSourceServer.Size = new Size(281, 38);
        txtSourceServer.TabIndex = 2;
        // 
        // lblSourceDbName
        // 
        lblSourceDbName.Dock = DockStyle.Fill;
        lblSourceDbName.Location = new Point(4, 98);
        lblSourceDbName.Margin = new Padding(3, 3, 3, 8);
        lblSourceDbName.Name = "lblSourceDbName";
        lblSourceDbName.Size = new Size(117, 38);
        lblSourceDbName.TabIndex = 20;
        lblSourceDbName.Text = "数据库名称：";
        lblSourceDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceDbName
        // 
        txtSourceDbName.Dock = DockStyle.Fill;
        txtSourceDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceDbName.Location = new Point(128, 98);
        txtSourceDbName.Margin = new Padding(3, 3, 3, 8);
        txtSourceDbName.Name = "txtSourceDbName";
        txtSourceDbName.Size = new Size(281, 38);
        txtSourceDbName.TabIndex = 21;
        // 
        // lblSourceUser
        // 
        lblSourceUser.Dock = DockStyle.Fill;
        lblSourceUser.Location = new Point(4, 148);
        lblSourceUser.Margin = new Padding(3, 3, 3, 8);
        lblSourceUser.Name = "lblSourceUser";
        lblSourceUser.Size = new Size(117, 38);
        lblSourceUser.TabIndex = 3;
        lblSourceUser.Text = "用户名：";
        lblSourceUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceUser
        // 
        txtSourceUser.Dock = DockStyle.Fill;
        txtSourceUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceUser.Location = new Point(128, 148);
        txtSourceUser.Margin = new Padding(3, 3, 3, 8);
        txtSourceUser.Name = "txtSourceUser";
        txtSourceUser.Size = new Size(281, 38);
        txtSourceUser.TabIndex = 4;
        // 
        // lblSourcePassword
        // 
        lblSourcePassword.Dock = DockStyle.Fill;
        lblSourcePassword.Location = new Point(4, 198);
        lblSourcePassword.Margin = new Padding(3, 3, 3, 8);
        lblSourcePassword.Name = "lblSourcePassword";
        lblSourcePassword.Size = new Size(117, 38);
        lblSourcePassword.TabIndex = 5;
        lblSourcePassword.Text = "密码：";
        lblSourcePassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourcePassword
        // 
        txtSourcePassword.Dock = DockStyle.Fill;
        txtSourcePassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourcePassword.Location = new Point(128, 198);
        txtSourcePassword.Margin = new Padding(3, 3, 3, 8);
        txtSourcePassword.Name = "txtSourcePassword";
        txtSourcePassword.Size = new Size(281, 38);
        txtSourcePassword.TabIndex = 6;
        txtSourcePassword.UseSystemPasswordChar = true;
        // 
        // btnSelectSource
        // 
        btnSelectSource.BackColor = Color.FromArgb(24, 145, 176);
        btnSelectSource.FlatAppearance.BorderSize = 0;
        btnSelectSource.FlatStyle = FlatStyle.Flat;
        btnSelectSource.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnSelectSource.ForeColor = Color.White;
        btnSelectSource.Location = new Point(128, 248);
        btnSelectSource.Name = "btnSelectSource";
        btnSelectSource.Size = new Size(148, 32);
        btnSelectSource.TabIndex = 7;
        btnSelectSource.Text = "选择账套";
        btnSelectSource.UseVisualStyleBackColor = false;
        btnSelectSource.Click += BtnSelectSource_Click;
        // 
        // targetLayout
        // 
        targetLayout.BackColor = Color.FromArgb(250, 245, 245);
        targetLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
        targetLayout.ColumnCount = 2;
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        targetLayout.Controls.Add(lblTargetTitle, 0, 0);
        targetLayout.Controls.Add(lblTargetServer, 0, 1);
        targetLayout.Controls.Add(txtTargetServer, 1, 1);
        targetLayout.Controls.Add(lblTargetDbName, 0, 2);
        targetLayout.Controls.Add(txtTargetDbName, 1, 2);
        targetLayout.Controls.Add(lblTargetUser, 0, 3);
        targetLayout.Controls.Add(txtTargetUser, 1, 3);
        targetLayout.Controls.Add(lblTargetPassword, 0, 4);
        targetLayout.Controls.Add(txtTargetPassword, 1, 4);
        targetLayout.Controls.Add(btnSelectTarget, 1, 5);
        targetLayout.Dock = DockStyle.Fill;
        targetLayout.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        targetLayout.Location = new Point(439, 13);
        targetLayout.Margin = new Padding(3, 3, 3, 10);
        targetLayout.Name = "targetLayout";
        targetLayout.RowCount = 6;
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.RowStyles.Add(new RowStyle());
        targetLayout.Size = new Size(421, 300);
        targetLayout.TabIndex = 1;
        // 
        // lblTargetTitle
        // 
        lblTargetTitle.Dock = DockStyle.Fill;
        lblTargetTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblTargetTitle.ForeColor = Color.FromArgb(200, 80, 80);
        lblTargetTitle.Location = new Point(4, 4);
        lblTargetTitle.Margin = new Padding(3, 3, 3, 10);
        lblTargetTitle.Name = "lblTargetTitle";
        lblTargetTitle.Size = new Size(119, 30);
        lblTargetTitle.TabIndex = 0;
        lblTargetTitle.Text = "目标数据库";
        lblTargetTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblTargetServer
        // 
        lblTargetServer.Dock = DockStyle.Fill;
        lblTargetServer.Location = new Point(4, 48);
        lblTargetServer.Margin = new Padding(3, 3, 3, 8);
        lblTargetServer.Name = "lblTargetServer";
        lblTargetServer.Size = new Size(119, 38);
        lblTargetServer.TabIndex = 1;
        lblTargetServer.Text = "服务器地址：";
        lblTargetServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetServer
        // 
        txtTargetServer.Dock = DockStyle.Fill;
        txtTargetServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetServer.Location = new Point(130, 48);
        txtTargetServer.Margin = new Padding(3, 3, 3, 8);
        txtTargetServer.Name = "txtTargetServer";
        txtTargetServer.Size = new Size(287, 38);
        txtTargetServer.TabIndex = 8;
        // 
        // lblTargetDbName
        // 
        lblTargetDbName.Dock = DockStyle.Fill;
        lblTargetDbName.Location = new Point(4, 98);
        lblTargetDbName.Margin = new Padding(3, 3, 3, 8);
        lblTargetDbName.Name = "lblTargetDbName";
        lblTargetDbName.Size = new Size(119, 38);
        lblTargetDbName.TabIndex = 22;
        lblTargetDbName.Text = "数据库名称：";
        lblTargetDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetDbName
        // 
        txtTargetDbName.Dock = DockStyle.Fill;
        txtTargetDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetDbName.Location = new Point(130, 98);
        txtTargetDbName.Margin = new Padding(3, 3, 3, 8);
        txtTargetDbName.Name = "txtTargetDbName";
        txtTargetDbName.Size = new Size(287, 38);
        txtTargetDbName.TabIndex = 23;
        // 
        // lblTargetUser
        // 
        lblTargetUser.Dock = DockStyle.Fill;
        lblTargetUser.Location = new Point(4, 148);
        lblTargetUser.Margin = new Padding(3, 3, 3, 8);
        lblTargetUser.Name = "lblTargetUser";
        lblTargetUser.Size = new Size(119, 38);
        lblTargetUser.TabIndex = 9;
        lblTargetUser.Text = "用户名：";
        lblTargetUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetUser
        // 
        txtTargetUser.Dock = DockStyle.Fill;
        txtTargetUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetUser.Location = new Point(130, 148);
        txtTargetUser.Margin = new Padding(3, 3, 3, 8);
        txtTargetUser.Name = "txtTargetUser";
        txtTargetUser.Size = new Size(287, 38);
        txtTargetUser.TabIndex = 10;
        // 
        // lblTargetPassword
        // 
        lblTargetPassword.Dock = DockStyle.Fill;
        lblTargetPassword.Location = new Point(4, 198);
        lblTargetPassword.Margin = new Padding(3, 3, 3, 8);
        lblTargetPassword.Name = "lblTargetPassword";
        lblTargetPassword.Size = new Size(119, 38);
        lblTargetPassword.TabIndex = 11;
        lblTargetPassword.Text = "密码：";
        lblTargetPassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetPassword
        // 
        txtTargetPassword.Dock = DockStyle.Fill;
        txtTargetPassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetPassword.Location = new Point(130, 198);
        txtTargetPassword.Margin = new Padding(3, 3, 3, 8);
        txtTargetPassword.Name = "txtTargetPassword";
        txtTargetPassword.Size = new Size(287, 38);
        txtTargetPassword.TabIndex = 12;
        txtTargetPassword.UseSystemPasswordChar = true;
        // 
        // btnSelectTarget
        // 
        btnSelectTarget.BackColor = Color.FromArgb(200, 80, 80);
        btnSelectTarget.FlatAppearance.BorderSize = 0;
        btnSelectTarget.FlatStyle = FlatStyle.Flat;
        btnSelectTarget.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnSelectTarget.ForeColor = Color.White;
        btnSelectTarget.Location = new Point(130, 248);
        btnSelectTarget.Name = "btnSelectTarget";
        btnSelectTarget.Size = new Size(135, 32);
        btnSelectTarget.TabIndex = 13;
        btnSelectTarget.Text = "选择账套";
        btnSelectTarget.UseVisualStyleBackColor = false;
        btnSelectTarget.Click += BtnSelectTarget_Click;
        // 
        // lblObjectType
        // 
        lblObjectType.Dock = DockStyle.Fill;
        lblObjectType.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblObjectType.Location = new Point(13, 323);
        lblObjectType.Margin = new Padding(3, 10, 3, 3);
        lblObjectType.Name = "lblObjectType";
        lblObjectType.Size = new Size(120, 35);
        lblObjectType.TabIndex = 28;
        lblObjectType.Text = "对象类型：";
        lblObjectType.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cboObjectType
        // 
        cboObjectType.Dock = DockStyle.Fill;
        cboObjectType.DropDownStyle = ComboBoxStyle.DropDownList;
        cboObjectType.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        cboObjectType.Location = new Point(139, 320);
        cboObjectType.Margin = new Padding(3, 10, 3, 3);
        cboObjectType.Name = "cboObjectType";
        cboObjectType.Size = new Size(721, 38);
        cboObjectType.TabIndex = 29;
        // 
        // lblObjects
        // 
        lblObjects.AutoSize = true;
        lblObjects.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblObjects.Location = new Point(13, 368);
        lblObjects.Margin = new Padding(3, 3, 3, 3);
        lblObjects.Name = "lblObjects";
        lblObjects.Size = new Size(119, 35);
        lblObjects.TabIndex = 2;
        lblObjects.Text = "对象名称：";
        lblObjects.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtObjects
        // 
        txtObjects.Dock = DockStyle.Fill;
        txtObjects.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtObjects.Location = new Point(13, 409);
        txtObjects.Multiline = true;
        txtObjects.Name = "txtObjects";
        txtObjects.Size = new Size(847, 150);
        txtObjects.TabIndex = 14;
        // 
        // lblObjectsHint
        // 
        lblObjectsHint.AutoSize = true;
        lblObjectsHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblObjectsHint.ForeColor = Color.Gray;
        lblObjectsHint.Location = new Point(13, 565);
        lblObjectsHint.Margin = new Padding(3);
        lblObjectsHint.Name = "lblObjectsHint";
        lblObjectsHint.Size = new Size(557, 28);
        lblObjectsHint.TabIndex = 3;
        lblObjectsHint.Text = "提示：多个对象用分号(;)分隔，例如：Table1;Table2;Table3";
        // 
        // chkDeleteIfExists
        // 
        chkDeleteIfExists.AutoSize = true;
        chkDeleteIfExists.Dock = DockStyle.Left;
        chkDeleteIfExists.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkDeleteIfExists.Location = new Point(13, 598);
        chkDeleteIfExists.Name = "chkDeleteIfExists";
        chkDeleteIfExists.Size = new Size(300, 35);
        chkDeleteIfExists.TabIndex = 30;
        chkDeleteIfExists.Text = "已存在对象先删除再创建";
        chkDeleteIfExists.UseVisualStyleBackColor = true;
        // 
        // btnConfirm
        // 
        btnConfirm.BackColor = Color.FromArgb(24, 145, 176);
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.FlatStyle = FlatStyle.Flat;
        btnConfirm.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnConfirm.ForeColor = Color.White;
        btnConfirm.Location = new Point(13, 638);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(180, 40);
        btnConfirm.TabIndex = 16;
        btnConfirm.Text = "确认复制";
        btnConfirm.UseVisualStyleBackColor = false;
        btnConfirm.Click += BtnConfirm_Click;
        // 
        // btnCancel
        // 
        btnCancel.BackColor = Color.White;
        btnCancel.FlatAppearance.BorderColor = Color.Gray;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.ForeColor = Color.Gray;
        btnCancel.Location = new Point(439, 638);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(180, 40);
        btnCancel.TabIndex = 17;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // lblProgress
        // 
        lblProgress.AutoSize = true;
        lblProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Location = new Point(439, 685);
        lblProgress.Name = "lblProgress";
        lblProgress.Size = new Size(54, 28);
        lblProgress.TabIndex = 19;
        lblProgress.Text = "就绪";
        // 
        // progressBar
        // 
        progressBar.Location = new Point(13, 688);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(400, 25);
        progressBar.TabIndex = 18;
        // 
        // spacerPanel
        // 
        spacerPanel.Location = new Point(0, 0);
        spacerPanel.Name = "spacerPanel";
        spacerPanel.Size = new Size(200, 30);
        spacerPanel.TabIndex = 0;
        // 
        // CrossDbCopyTableForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(873, 750);
        Controls.Add(tableLayoutPanel1);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CrossDbCopyTableForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "跨库复制对象";
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel1.PerformLayout();
        sourceLayout.ResumeLayout(false);
        sourceLayout.PerformLayout();
        targetLayout.ResumeLayout(false);
        targetLayout.PerformLayout();
        ResumeLayout(false);
    }
}
