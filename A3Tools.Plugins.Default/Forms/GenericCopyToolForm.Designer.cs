using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

partial class GenericCopyToolForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel mainLayout;
    private Panel pnlDatabases;
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
    private Label lblConfigInfo;
    private Label lblTitleHint;
    private TextBox txtKeyValues;
    private TableLayoutPanel rowHintAndCheckbox;
    private Label lblSearchHint;
    private CheckBox chkDeleteFirst;
    private Panel pnlButtons;
    private Button btnConfirm;
    private Button btnCancel;
    private ProgressBar progressBar;
    private Label lblProgress;
    private Panel pnlSearch;
    private Label lblSearchKeyword;
    private TextBox txtSearchKeyword;
    private Button btnSearch;
    private Button btnAddSelected;
    private Button btnClearSelected;
    private Label lblSearchProgress;
    private DataGridView dgvSearchResults;
    private Button btnMissingData;
    private Label lblQuickFilter;
    private TextBox txtQuickFilter;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        mainLayout = new TableLayoutPanel();
        pnlDatabases = new Panel();
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
        lblConfigInfo = new Label();
        lblTitleHint = new Label();
        txtKeyValues = new TextBox();
        rowHintAndCheckbox = new TableLayoutPanel();
        lblSearchHint = new Label();
        chkDeleteFirst = new CheckBox();
        pnlButtons = new Panel();
        btnConfirm = new Button();
        btnCancel = new Button();
        progressBar = new ProgressBar();
        lblProgress = new Label();
        pnlSearch = new Panel();
        lblSearchKeyword = new Label();
        txtSearchKeyword = new TextBox();
        btnSearch = new Button();
        btnMissingData = new Button();
        btnAddSelected = new Button();
        btnClearSelected = new Button();
        lblSearchProgress = new Label();
        lblQuickFilter = new Label();
        txtQuickFilter = new TextBox();
        dgvSearchResults = new DataGridView();
        mainLayout.SuspendLayout();
        pnlDatabases.SuspendLayout();
        sourceLayout.SuspendLayout();
        targetLayout.SuspendLayout();
        rowHintAndCheckbox.SuspendLayout();
        pnlButtons.SuspendLayout();
        pnlSearch.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvSearchResults).BeginInit();
        SuspendLayout();
        // 
        // mainLayout
        // 
        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(pnlDatabases, 0, 0);
        mainLayout.Controls.Add(lblConfigInfo, 0, 1);
        mainLayout.Controls.Add(lblTitleHint, 0, 2);
        mainLayout.Controls.Add(txtKeyValues, 0, 3);
        mainLayout.Controls.Add(rowHintAndCheckbox, 0, 4);
        mainLayout.Controls.Add(pnlButtons, 0, 5);
        mainLayout.Controls.Add(progressBar, 0, 6);
        mainLayout.Controls.Add(lblProgress, 0, 7);
        mainLayout.Controls.Add(pnlSearch, 0, 8);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.RowCount = 9;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Size = new Size(1256, 951);
        mainLayout.TabIndex = 0;
        // 
        // pnlDatabases
        // 
        pnlDatabases.Controls.Add(sourceLayout);
        pnlDatabases.Controls.Add(targetLayout);
        pnlDatabases.Dock = DockStyle.Fill;
        pnlDatabases.Location = new Point(3, 3);
        pnlDatabases.Name = "pnlDatabases";
        pnlDatabases.Size = new Size(1250, 294);
        pnlDatabases.TabIndex = 0;
        // 
        // sourceLayout
        // 
        sourceLayout.Anchor = AnchorStyles.None;
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
        sourceLayout.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        sourceLayout.Location = new Point(29, 0);
        sourceLayout.Name = "sourceLayout";
        sourceLayout.RowCount = 6;
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        sourceLayout.Size = new Size(590, 291);
        sourceLayout.TabIndex = 0;
        // 
        // lblSourceTitle
        // 
        sourceLayout.SetColumnSpan(lblSourceTitle, 2);
        lblSourceTitle.Dock = DockStyle.Fill;
        lblSourceTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblSourceTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblSourceTitle.Location = new Point(4, 4);
        lblSourceTitle.Margin = new Padding(3, 3, 3, 10);
        lblSourceTitle.Name = "lblSourceTitle";
        lblSourceTitle.Size = new Size(582, 37);
        lblSourceTitle.TabIndex = 0;
        lblSourceTitle.Text = "源数据库";
        lblSourceTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblSourceServer
        // 
        lblSourceServer.Dock = DockStyle.Fill;
        lblSourceServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSourceServer.Location = new Point(4, 55);
        lblSourceServer.Margin = new Padding(3, 3, 3, 8);
        lblSourceServer.Name = "lblSourceServer";
        lblSourceServer.Size = new Size(170, 38);
        lblSourceServer.TabIndex = 1;
        lblSourceServer.Text = "服务器地址：";
        lblSourceServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceServer
        // 
        txtSourceServer.Dock = DockStyle.Fill;
        txtSourceServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceServer.Location = new Point(181, 55);
        txtSourceServer.Margin = new Padding(3, 3, 3, 8);
        txtSourceServer.Name = "txtSourceServer";
        txtSourceServer.Size = new Size(405, 38);
        txtSourceServer.TabIndex = 2;
        // 
        // lblSourceDbName
        // 
        lblSourceDbName.Dock = DockStyle.Fill;
        lblSourceDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSourceDbName.Location = new Point(4, 105);
        lblSourceDbName.Margin = new Padding(3, 3, 3, 8);
        lblSourceDbName.Name = "lblSourceDbName";
        lblSourceDbName.Size = new Size(170, 38);
        lblSourceDbName.TabIndex = 3;
        lblSourceDbName.Text = "数据库名称：";
        lblSourceDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceDbName
        // 
        txtSourceDbName.Dock = DockStyle.Fill;
        txtSourceDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceDbName.Location = new Point(181, 105);
        txtSourceDbName.Margin = new Padding(3, 3, 3, 8);
        txtSourceDbName.Name = "txtSourceDbName";
        txtSourceDbName.Size = new Size(405, 38);
        txtSourceDbName.TabIndex = 4;
        // 
        // lblSourceUser
        // 
        lblSourceUser.Dock = DockStyle.Fill;
        lblSourceUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSourceUser.Location = new Point(4, 155);
        lblSourceUser.Margin = new Padding(3, 3, 3, 8);
        lblSourceUser.Name = "lblSourceUser";
        lblSourceUser.Size = new Size(170, 38);
        lblSourceUser.TabIndex = 5;
        lblSourceUser.Text = "用户名：";
        lblSourceUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceUser
        // 
        txtSourceUser.Dock = DockStyle.Fill;
        txtSourceUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceUser.Location = new Point(181, 155);
        txtSourceUser.Margin = new Padding(3, 3, 3, 8);
        txtSourceUser.Name = "txtSourceUser";
        txtSourceUser.Size = new Size(405, 38);
        txtSourceUser.TabIndex = 6;
        // 
        // lblSourcePassword
        // 
        lblSourcePassword.Dock = DockStyle.Fill;
        lblSourcePassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSourcePassword.Location = new Point(4, 205);
        lblSourcePassword.Margin = new Padding(3, 3, 3, 8);
        lblSourcePassword.Name = "lblSourcePassword";
        lblSourcePassword.Size = new Size(170, 38);
        lblSourcePassword.TabIndex = 7;
        lblSourcePassword.Text = "密码：";
        lblSourcePassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourcePassword
        // 
        txtSourcePassword.Dock = DockStyle.Fill;
        txtSourcePassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourcePassword.Location = new Point(181, 205);
        txtSourcePassword.Margin = new Padding(3, 3, 3, 8);
        txtSourcePassword.Name = "txtSourcePassword";
        txtSourcePassword.Size = new Size(405, 38);
        txtSourcePassword.TabIndex = 8;
        txtSourcePassword.UseSystemPasswordChar = true;
        // 
        // btnSelectSource
        // 
        btnSelectSource.BackColor = Color.FromArgb(24, 145, 176);
        btnSelectSource.FlatAppearance.BorderSize = 0;
        btnSelectSource.FlatStyle = FlatStyle.Flat;
        btnSelectSource.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnSelectSource.ForeColor = Color.White;
        btnSelectSource.Location = new Point(181, 252);
        btnSelectSource.Margin = new Padding(3, 0, 3, 3);
        btnSelectSource.Name = "btnSelectSource";
        btnSelectSource.Size = new Size(134, 32);
        btnSelectSource.TabIndex = 9;
        btnSelectSource.Text = "选择账套";
        btnSelectSource.UseVisualStyleBackColor = false;
        btnSelectSource.Click += BtnSelectSource_Click;
        // 
        // targetLayout
        // 
        targetLayout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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
        targetLayout.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        targetLayout.Location = new Point(649, 0);
        targetLayout.Name = "targetLayout";
        targetLayout.RowCount = 6;
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 16F));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        targetLayout.Size = new Size(590, 291);
        targetLayout.TabIndex = 1;
        // 
        // lblTargetTitle
        // 
        targetLayout.SetColumnSpan(lblTargetTitle, 2);
        lblTargetTitle.Dock = DockStyle.Fill;
        lblTargetTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        lblTargetTitle.ForeColor = Color.FromArgb(200, 80, 80);
        lblTargetTitle.Location = new Point(4, 4);
        lblTargetTitle.Margin = new Padding(3, 3, 3, 10);
        lblTargetTitle.Name = "lblTargetTitle";
        lblTargetTitle.Size = new Size(582, 37);
        lblTargetTitle.TabIndex = 0;
        lblTargetTitle.Text = "目标数据库";
        lblTargetTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblTargetServer
        // 
        lblTargetServer.Dock = DockStyle.Fill;
        lblTargetServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblTargetServer.Location = new Point(4, 55);
        lblTargetServer.Margin = new Padding(3, 3, 3, 8);
        lblTargetServer.Name = "lblTargetServer";
        lblTargetServer.Size = new Size(170, 38);
        lblTargetServer.TabIndex = 1;
        lblTargetServer.Text = "服务器地址：";
        lblTargetServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetServer
        // 
        txtTargetServer.Dock = DockStyle.Fill;
        txtTargetServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetServer.Location = new Point(181, 55);
        txtTargetServer.Margin = new Padding(3, 3, 3, 8);
        txtTargetServer.Name = "txtTargetServer";
        txtTargetServer.Size = new Size(405, 38);
        txtTargetServer.TabIndex = 2;
        // 
        // lblTargetDbName
        // 
        lblTargetDbName.Dock = DockStyle.Fill;
        lblTargetDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblTargetDbName.Location = new Point(4, 105);
        lblTargetDbName.Margin = new Padding(3, 3, 3, 8);
        lblTargetDbName.Name = "lblTargetDbName";
        lblTargetDbName.Size = new Size(170, 38);
        lblTargetDbName.TabIndex = 3;
        lblTargetDbName.Text = "数据库名称：";
        lblTargetDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetDbName
        // 
        txtTargetDbName.Dock = DockStyle.Fill;
        txtTargetDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetDbName.Location = new Point(181, 105);
        txtTargetDbName.Margin = new Padding(3, 3, 3, 8);
        txtTargetDbName.Name = "txtTargetDbName";
        txtTargetDbName.Size = new Size(405, 38);
        txtTargetDbName.TabIndex = 4;
        // 
        // lblTargetUser
        // 
        lblTargetUser.Dock = DockStyle.Fill;
        lblTargetUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblTargetUser.Location = new Point(4, 155);
        lblTargetUser.Margin = new Padding(3, 3, 3, 8);
        lblTargetUser.Name = "lblTargetUser";
        lblTargetUser.Size = new Size(170, 38);
        lblTargetUser.TabIndex = 5;
        lblTargetUser.Text = "用户名：";
        lblTargetUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetUser
        // 
        txtTargetUser.Dock = DockStyle.Fill;
        txtTargetUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetUser.Location = new Point(181, 155);
        txtTargetUser.Margin = new Padding(3, 3, 3, 8);
        txtTargetUser.Name = "txtTargetUser";
        txtTargetUser.Size = new Size(405, 38);
        txtTargetUser.TabIndex = 6;
        // 
        // lblTargetPassword
        // 
        lblTargetPassword.Dock = DockStyle.Fill;
        lblTargetPassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblTargetPassword.Location = new Point(4, 205);
        lblTargetPassword.Margin = new Padding(3, 3, 3, 8);
        lblTargetPassword.Name = "lblTargetPassword";
        lblTargetPassword.Size = new Size(170, 38);
        lblTargetPassword.TabIndex = 7;
        lblTargetPassword.Text = "密码：";
        lblTargetPassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetPassword
        // 
        txtTargetPassword.Dock = DockStyle.Fill;
        txtTargetPassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetPassword.Location = new Point(181, 205);
        txtTargetPassword.Margin = new Padding(3, 3, 3, 8);
        txtTargetPassword.Name = "txtTargetPassword";
        txtTargetPassword.Size = new Size(405, 38);
        txtTargetPassword.TabIndex = 8;
        txtTargetPassword.UseSystemPasswordChar = true;
        // 
        // btnSelectTarget
        // 
        btnSelectTarget.BackColor = Color.FromArgb(200, 80, 80);
        btnSelectTarget.FlatAppearance.BorderSize = 0;
        btnSelectTarget.FlatStyle = FlatStyle.Flat;
        btnSelectTarget.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnSelectTarget.ForeColor = Color.White;
        btnSelectTarget.Location = new Point(181, 252);
        btnSelectTarget.Margin = new Padding(3, 0, 3, 3);
        btnSelectTarget.Name = "btnSelectTarget";
        btnSelectTarget.Size = new Size(132, 32);
        btnSelectTarget.TabIndex = 9;
        btnSelectTarget.Text = "选择账套";
        btnSelectTarget.UseVisualStyleBackColor = false;
        btnSelectTarget.Click += BtnSelectTarget_Click;
        // 
        // lblConfigInfo
        // 
        lblConfigInfo.Dock = DockStyle.Fill;
        lblConfigInfo.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblConfigInfo.ForeColor = Color.Gray;
        lblConfigInfo.Location = new Point(3, 300);
        lblConfigInfo.Name = "lblConfigInfo";
        lblConfigInfo.Size = new Size(1250, 28);
        lblConfigInfo.TabIndex = 1;
        lblConfigInfo.Text = "主表：…  复制关键字：…  关联表：…  关联字段：…";
        // 
        // lblTitleHint
        // 
        lblTitleHint.Dock = DockStyle.Fill;
        lblTitleHint.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblTitleHint.Location = new Point(3, 328);
        lblTitleHint.Name = "lblTitleHint";
        lblTitleHint.Size = new Size(1250, 30);
        lblTitleHint.TabIndex = 2;
        lblTitleHint.Text = "复制关键字：";
        // 
        // txtKeyValues
        // 
        txtKeyValues.Dock = DockStyle.Fill;
        txtKeyValues.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtKeyValues.Location = new Point(3, 361);
        txtKeyValues.Multiline = true;
        txtKeyValues.Name = "txtKeyValues";
        txtKeyValues.Size = new Size(1250, 94);
        txtKeyValues.TabIndex = 3;
        // 
        // rowHintAndCheckbox
        // 
        rowHintAndCheckbox.ColumnCount = 2;
        rowHintAndCheckbox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowHintAndCheckbox.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        rowHintAndCheckbox.Controls.Add(lblSearchHint, 0, 0);
        rowHintAndCheckbox.Controls.Add(chkDeleteFirst, 1, 0);
        rowHintAndCheckbox.Dock = DockStyle.Fill;
        rowHintAndCheckbox.Location = new Point(3, 461);
        rowHintAndCheckbox.Name = "rowHintAndCheckbox";
        rowHintAndCheckbox.RowCount = 1;
        rowHintAndCheckbox.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowHintAndCheckbox.Size = new Size(1250, 32);
        rowHintAndCheckbox.TabIndex = 4;
        // 
        // lblSearchHint
        // 
        lblSearchHint.Dock = DockStyle.Fill;
        lblSearchHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchHint.ForeColor = Color.Gray;
        lblSearchHint.Location = new Point(3, 0);
        lblSearchHint.Name = "lblSearchHint";
        lblSearchHint.Size = new Size(1044, 32);
        lblSearchHint.TabIndex = 0;
        lblSearchHint.Text = "提示：可通过下方搜索添加";
        // 
        // chkDeleteFirst
        // 
        chkDeleteFirst.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        chkDeleteFirst.AutoSize = true;
        chkDeleteFirst.Checked = true;
        chkDeleteFirst.CheckState = CheckState.Checked;
        chkDeleteFirst.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkDeleteFirst.Location = new Point(1053, 3);
        chkDeleteFirst.Name = "chkDeleteFirst";
        chkDeleteFirst.Size = new Size(194, 26);
        chkDeleteFirst.TabIndex = 1;
        chkDeleteFirst.Text = "先删除目标数据";
        // 
        // pnlButtons
        // 
        pnlButtons.Controls.Add(btnConfirm);
        pnlButtons.Controls.Add(btnCancel);
        pnlButtons.Dock = DockStyle.Fill;
        pnlButtons.Location = new Point(3, 499);
        pnlButtons.Name = "pnlButtons";
        pnlButtons.Size = new Size(1250, 40);
        pnlButtons.TabIndex = 5;
        // 
        // btnConfirm
        // 
        btnConfirm.BackColor = Color.FromArgb(24, 145, 176);
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.FlatStyle = FlatStyle.Flat;
        btnConfirm.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnConfirm.ForeColor = Color.White;
        btnConfirm.Location = new Point(452, 0);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(120, 40);
        btnConfirm.TabIndex = 0;
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
        btnCancel.Location = new Point(649, 0);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(120, 39);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // progressBar
        // 
        progressBar.Dock = DockStyle.Fill;
        progressBar.Location = new Point(3, 545);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(1250, 16);
        progressBar.TabIndex = 6;
        // 
        // lblProgress
        // 
        lblProgress.Dock = DockStyle.Fill;
        lblProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Location = new Point(3, 564);
        lblProgress.Name = "lblProgress";
        lblProgress.Size = new Size(1250, 22);
        lblProgress.TabIndex = 7;
        lblProgress.Text = "就绪";
        // 
        // pnlSearch
        // 
        pnlSearch.Controls.Add(lblSearchKeyword);
        pnlSearch.Controls.Add(txtSearchKeyword);
        pnlSearch.Controls.Add(btnSearch);
        pnlSearch.Controls.Add(btnMissingData);
        pnlSearch.Controls.Add(btnAddSelected);
        pnlSearch.Controls.Add(btnClearSelected);
        pnlSearch.Controls.Add(lblSearchProgress);
        pnlSearch.Controls.Add(lblQuickFilter);
        pnlSearch.Controls.Add(txtQuickFilter);
        pnlSearch.Controls.Add(dgvSearchResults);
        pnlSearch.Dock = DockStyle.Fill;
        pnlSearch.Location = new Point(3, 589);
        pnlSearch.Name = "pnlSearch";
        pnlSearch.Size = new Size(1250, 359);
        pnlSearch.TabIndex = 8;
        // 
        // lblSearchKeyword
        // 
        lblSearchKeyword.AutoSize = true;
        lblSearchKeyword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchKeyword.Location = new Point(10, 10);
        lblSearchKeyword.Name = "lblSearchKeyword";
        lblSearchKeyword.Size = new Size(158, 31);
        lblSearchKeyword.TabIndex = 0;
        lblSearchKeyword.Text = "搜索关键字：";
        // 
        // txtSearchKeyword
        // 
        txtSearchKeyword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSearchKeyword.Location = new Point(168, 7);
        txtSearchKeyword.Name = "txtSearchKeyword";
        txtSearchKeyword.PlaceholderText = "按主键或名称搜索...";
        txtSearchKeyword.Size = new Size(383, 38);
        txtSearchKeyword.TabIndex = 1;
        // 
        // btnSearch
        // 
        btnSearch.BackColor = Color.FromArgb(24, 145, 176);
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.FlatStyle = FlatStyle.Flat;
        btnSearch.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnSearch.ForeColor = Color.White;
        btnSearch.Location = new Point(570, 5);
        btnSearch.Name = "btnSearch";
        btnSearch.Size = new Size(88, 41);
        btnSearch.TabIndex = 2;
        btnSearch.Text = "查询";
        btnSearch.UseVisualStyleBackColor = false;
        btnSearch.Click += BtnSearch_Click;
        // 
        // btnMissingData
        // 
        btnMissingData.BackColor = Color.FromArgb(228, 88, 38);
        btnMissingData.FlatAppearance.BorderSize = 0;
        btnMissingData.FlatStyle = FlatStyle.Flat;
        btnMissingData.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        btnMissingData.ForeColor = Color.White;
        btnMissingData.Location = new Point(665, 5);
        btnMissingData.Name = "btnMissingData";
        btnMissingData.Size = new Size(133, 41);
        btnMissingData.TabIndex = 3;
        btnMissingData.Text = "缺失数据";
        btnMissingData.UseVisualStyleBackColor = false;
        btnMissingData.Click += BtnMissingData_Click;
        // 
        // btnAddSelected
        // 
        btnAddSelected.BackColor = Color.FromArgb(57, 181, 74);
        btnAddSelected.FlatAppearance.BorderSize = 0;
        btnAddSelected.FlatStyle = FlatStyle.Flat;
        btnAddSelected.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnAddSelected.ForeColor = Color.White;
        btnAddSelected.Location = new Point(804, 6);
        btnAddSelected.Name = "btnAddSelected";
        btnAddSelected.Size = new Size(144, 41);
        btnAddSelected.TabIndex = 4;
        btnAddSelected.Text = "添加选中";
        btnAddSelected.UseVisualStyleBackColor = false;
        btnAddSelected.Click += BtnAddSelected_Click;
        // 
        // btnClearSelected
        // 
        btnClearSelected.BackColor = Color.FromArgb(200, 80, 80);
        btnClearSelected.FlatAppearance.BorderSize = 0;
        btnClearSelected.FlatStyle = FlatStyle.Flat;
        btnClearSelected.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearSelected.ForeColor = Color.White;
        btnClearSelected.Location = new Point(954, 5);
        btnClearSelected.Name = "btnClearSelected";
        btnClearSelected.Size = new Size(141, 41);
        btnClearSelected.TabIndex = 5;
        btnClearSelected.Text = "清空选项";
        btnClearSelected.UseVisualStyleBackColor = false;
        btnClearSelected.Click += BtnClearSelected_Click;
        // 
        // lblSearchProgress
        // 
        lblSearchProgress.AutoSize = true;
        lblSearchProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchProgress.ForeColor = Color.Gray;
        lblSearchProgress.Location = new Point(605, 10);
        lblSearchProgress.Name = "lblSearchProgress";
        lblSearchProgress.Size = new Size(0, 28);
        lblSearchProgress.TabIndex = 6;
        // 
        // dgvSearchResults
        // 
        dgvSearchResults.AllowUserToAddRows = false;
        dgvSearchResults.AllowUserToDeleteRows = false;
        dgvSearchResults.BackgroundColor = Color.White;
        dgvSearchResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvSearchResults.Location = new Point(10, 86);
        dgvSearchResults.Name = "dgvSearchResults";
        dgvSearchResults.ReadOnly = true;
        dgvSearchResults.RowHeadersWidth = 72;
        dgvSearchResults.RowTemplate.Height = 25;
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.Size = new Size(1225, 256);
        dgvSearchResults.TabIndex = 7;
        // 
        // lblQuickFilter
        // 
        lblQuickFilter.AutoSize = true;
        lblQuickFilter.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblQuickFilter.Location = new Point(10, 52);
        lblQuickFilter.Name = "lblQuickFilter";
        lblQuickFilter.Size = new Size(134, 28);
        lblQuickFilter.TabIndex = 8;
        lblQuickFilter.Text = "快速过滤：";
        // 
        // txtQuickFilter
        // 
        txtQuickFilter.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtQuickFilter.Location = new Point(168, 49);
        txtQuickFilter.Name = "txtQuickFilter";
        txtQuickFilter.PlaceholderText = "在当前结果中再次过滤（所有列模糊匹配，实时）";
        txtQuickFilter.Size = new Size(740, 38);
        txtQuickFilter.TabIndex = 9;
        // 
        // GenericCopyToolForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1256, 951);
        Controls.Add(mainLayout);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "GenericCopyToolForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "自定义工具";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        pnlDatabases.ResumeLayout(false);
        sourceLayout.ResumeLayout(false);
        sourceLayout.PerformLayout();
        targetLayout.ResumeLayout(false);
        targetLayout.PerformLayout();
        rowHintAndCheckbox.ResumeLayout(false);
        rowHintAndCheckbox.PerformLayout();
        pnlButtons.ResumeLayout(false);
        pnlSearch.ResumeLayout(false);
        pnlSearch.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)dgvSearchResults).EndInit();
        ResumeLayout(false);
    }
}