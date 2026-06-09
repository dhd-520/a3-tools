namespace A3Tools.Plugins.Default.Forms;

partial class CrossDbCopyAppFormForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel tableLayoutPanel1;
    private Panel pnlSearch;
    private Label lblSearchKeyword;
    private TextBox txtSearchKeyword;
    private Button btnSearch;
    private Button btnAddSelected;
    private Button btnClearSelected;
    private DataGridView dgvSearchResults;
    private Label lblSearchProgress;
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
    private Label lblObjectGuids;
    private TextBox txtObjectGuids;
    private Label lblObjectGuidsHint;
    private CheckBox chkDeleteFirst;
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
        lblObjectGuids = new Label();
        txtObjectGuids = new TextBox();
        lblObjectGuidsHint = new Label();
        chkDeleteFirst = new CheckBox();
        btnConfirm = new Button();
        btnCancel = new Button();
        progressBar = new ProgressBar();
        lblProgress = new Label();
        pnlSearch = new Panel();
        lblSearchKeyword = new Label();
        txtSearchKeyword = new TextBox();
        btnSearch = new Button();
        btnAddSelected = new Button();
        btnClearSelected = new Button();
        dgvSearchResults = new DataGridView();
        lblSearchProgress = new Label();
        tableLayoutPanel1.SuspendLayout();
        pnlSearch.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvSearchResults).BeginInit();
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
        tableLayoutPanel1.Controls.Add(lblObjectGuids, 0, 1);
        tableLayoutPanel1.Controls.Add(txtObjectGuids, 0, 2);
        tableLayoutPanel1.Controls.Add(lblObjectGuidsHint, 0, 3);
        tableLayoutPanel1.Controls.Add(chkDeleteFirst, 1, 3);
        tableLayoutPanel1.Controls.Add(pnlSearch, 0, 4);
        tableLayoutPanel1.SetColumnSpan(pnlSearch, 2);
        tableLayoutPanel1.Controls.Add(btnConfirm, 0, 5);
        tableLayoutPanel1.Controls.Add(btnCancel, 1, 5);
        tableLayoutPanel1.Controls.Add(progressBar, 0, 6);
        tableLayoutPanel1.Controls.Add(lblProgress, 1, 6);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        tableLayoutPanel1.Location = new Point(0, 0);
        tableLayoutPanel1.Margin = new Padding(3, 10, 3, 3);
        tableLayoutPanel1.Name = "tableLayoutPanel1";
        tableLayoutPanel1.Padding = new Padding(10);
        tableLayoutPanel1.RowCount = 7;
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.RowStyles.Add(new RowStyle());
        tableLayoutPanel1.Size = new Size(873, 690);
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
        sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
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
        lblSourceServer.Location = new Point(4, 189);
        lblSourceServer.Margin = new Padding(3, 3, 3, 8);
        lblSourceServer.Name = "lblSourceServer";
        lblSourceServer.Size = new Size(117, 28);
        lblSourceServer.TabIndex = 1;
        lblSourceServer.Text = "服务器地址：";
        lblSourceServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceServer
        // 
        txtSourceServer.Dock = DockStyle.Fill;
        txtSourceServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceServer.Location = new Point(128, 189);
        txtSourceServer.Margin = new Padding(3, 3, 3, 8);
        txtSourceServer.Name = "txtSourceServer";
        txtSourceServer.Size = new Size(281, 28);
        txtSourceServer.TabIndex = 2;
        // 
        // lblSourceDbName
        // 
        lblSourceDbName.Dock = DockStyle.Fill;
        lblSourceDbName.Location = new Point(4, 239);
        lblSourceDbName.Margin = new Padding(3, 3, 3, 8);
        lblSourceDbName.Name = "lblSourceDbName";
        lblSourceDbName.Size = new Size(117, 28);
        lblSourceDbName.TabIndex = 20;
        lblSourceDbName.Text = "数据库名称：";
        lblSourceDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceDbName
        // 
        txtSourceDbName.Dock = DockStyle.Fill;
        txtSourceDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceDbName.Location = new Point(128, 239);
        txtSourceDbName.Margin = new Padding(3, 3, 3, 8);
        txtSourceDbName.Name = "txtSourceDbName";
        txtSourceDbName.Size = new Size(281, 28);
        txtSourceDbName.TabIndex = 21;
        // 
        // lblSourceUser
        // 
        lblSourceUser.Dock = DockStyle.Fill;
        lblSourceUser.Location = new Point(4, 289);
        lblSourceUser.Margin = new Padding(3, 3, 3, 8);
        lblSourceUser.Name = "lblSourceUser";
        lblSourceUser.Size = new Size(117, 28);
        lblSourceUser.TabIndex = 3;
        lblSourceUser.Text = "用户名：";
        lblSourceUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourceUser
        // 
        txtSourceUser.Dock = DockStyle.Fill;
        txtSourceUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourceUser.Location = new Point(128, 289);
        txtSourceUser.Margin = new Padding(3, 3, 3, 8);
        txtSourceUser.Name = "txtSourceUser";
        txtSourceUser.Size = new Size(281, 28);
        txtSourceUser.TabIndex = 4;
        // 
        // lblSourcePassword
        // 
        lblSourcePassword.Dock = DockStyle.Fill;
        lblSourcePassword.Location = new Point(4, 339);
        lblSourcePassword.Margin = new Padding(3, 3, 3, 8);
        lblSourcePassword.Name = "lblSourcePassword";
        lblSourcePassword.Size = new Size(117, 28);
        lblSourcePassword.TabIndex = 5;
        lblSourcePassword.Text = "密码：";
        lblSourcePassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtSourcePassword
        // 
        txtSourcePassword.Dock = DockStyle.Fill;
        txtSourcePassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSourcePassword.Location = new Point(128, 339);
        txtSourcePassword.Margin = new Padding(3, 3, 3, 8);
        txtSourcePassword.Name = "txtSourcePassword";
        txtSourcePassword.Size = new Size(281, 28);
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
        btnSelectSource.Location = new Point(128, 389);
        btnSelectSource.Name = "btnSelectSource";
        btnSelectSource.Size = new Size(126, 32);
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
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
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
        lblTargetServer.Location = new Point(4, 189);
        lblTargetServer.Margin = new Padding(3, 3, 3, 8);
        lblTargetServer.Name = "lblTargetServer";
        lblTargetServer.Size = new Size(119, 28);
        lblTargetServer.TabIndex = 1;
        lblTargetServer.Text = "服务器地址：";
        lblTargetServer.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetServer
        // 
        txtTargetServer.Dock = DockStyle.Fill;
        txtTargetServer.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetServer.Location = new Point(130, 189);
        txtTargetServer.Margin = new Padding(3, 3, 3, 8);
        txtTargetServer.Name = "txtTargetServer";
        txtTargetServer.Size = new Size(287, 28);
        txtTargetServer.TabIndex = 8;
        // 
        // lblTargetDbName
        // 
        lblTargetDbName.Dock = DockStyle.Fill;
        lblTargetDbName.Location = new Point(4, 239);
        lblTargetDbName.Margin = new Padding(3, 3, 3, 8);
        lblTargetDbName.Name = "lblTargetDbName";
        lblTargetDbName.Size = new Size(119, 28);
        lblTargetDbName.TabIndex = 22;
        lblTargetDbName.Text = "数据库名称：";
        lblTargetDbName.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetDbName
        // 
        txtTargetDbName.Dock = DockStyle.Fill;
        txtTargetDbName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetDbName.Location = new Point(130, 239);
        txtTargetDbName.Margin = new Padding(3, 3, 3, 8);
        txtTargetDbName.Name = "txtTargetDbName";
        txtTargetDbName.Size = new Size(287, 28);
        txtTargetDbName.TabIndex = 23;
        // 
        // lblTargetUser
        // 
        lblTargetUser.Dock = DockStyle.Fill;
        lblTargetUser.Location = new Point(4, 289);
        lblTargetUser.Margin = new Padding(3, 3, 3, 8);
        lblTargetUser.Name = "lblTargetUser";
        lblTargetUser.Size = new Size(119, 28);
        lblTargetUser.TabIndex = 9;
        lblTargetUser.Text = "用户名：";
        lblTargetUser.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetUser
        // 
        txtTargetUser.Dock = DockStyle.Fill;
        txtTargetUser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetUser.Location = new Point(130, 289);
        txtTargetUser.Margin = new Padding(3, 3, 3, 8);
        txtTargetUser.Name = "txtTargetUser";
        txtTargetUser.Size = new Size(287, 28);
        txtTargetUser.TabIndex = 10;
        // 
        // lblTargetPassword
        // 
        lblTargetPassword.Dock = DockStyle.Fill;
        lblTargetPassword.Location = new Point(4, 339);
        lblTargetPassword.Margin = new Padding(3, 3, 3, 8);
        lblTargetPassword.Name = "lblTargetPassword";
        lblTargetPassword.Size = new Size(119, 28);
        lblTargetPassword.TabIndex = 11;
        lblTargetPassword.Text = "密码：";
        lblTargetPassword.TextAlign = ContentAlignment.MiddleRight;
        // 
        // txtTargetPassword
        // 
        txtTargetPassword.Dock = DockStyle.Fill;
        txtTargetPassword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtTargetPassword.Location = new Point(130, 339);
        txtTargetPassword.Margin = new Padding(3, 3, 3, 8);
        txtTargetPassword.Name = "txtTargetPassword";
        txtTargetPassword.Size = new Size(287, 28);
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
        btnSelectTarget.Location = new Point(130, 389);
        btnSelectTarget.Name = "btnSelectTarget";
        btnSelectTarget.Size = new Size(125, 32);
        btnSelectTarget.TabIndex = 13;
        btnSelectTarget.Text = "选择账套";
        btnSelectTarget.UseVisualStyleBackColor = false;
        btnSelectTarget.Click += BtnSelectTarget_Click;
        // 
        // lblObjectGuids
        // 
        lblObjectGuids.AutoSize = true;
        lblObjectGuids.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        lblObjectGuids.Location = new Point(13, 458);
        lblObjectGuids.Margin = new Padding(3, 10, 3, 3);
        lblObjectGuids.Name = "lblObjectGuids";
        lblObjectGuids.Size = new Size(302, 35);
        lblObjectGuids.TabIndex = 2;
        lblObjectGuids.Text = "APP表单OBJECTGUID：";
        lblObjectGuids.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtObjectGuids
        // 
        tableLayoutPanel1.SetColumnSpan(txtObjectGuids, 2);
        txtObjectGuids.Dock = DockStyle.Fill;
        txtObjectGuids.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtObjectGuids.Location = new Point(13, 499);
        txtObjectGuids.Multiline = true;
        txtObjectGuids.Name = "txtObjectGuids";
        txtObjectGuids.Size = new Size(847, 120);
        txtObjectGuids.TabIndex = 14;
        // 
        // lblObjectGuidsHint
        // 
        lblObjectGuidsHint.AutoSize = true;
        lblObjectGuidsHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblObjectGuidsHint.ForeColor = Color.Gray;
        lblObjectGuidsHint.Location = new Point(13, 565);
        lblObjectGuidsHint.Margin = new Padding(3);
        lblObjectGuidsHint.Name = "lblObjectGuidsHint";
        lblObjectGuidsHint.Size = new Size(371, 28);
        lblObjectGuidsHint.TabIndex = 3;
        lblObjectGuidsHint.Text = "提示：多个OBJECTGUID用分号(;)分隔";
        // 
        // chkDeleteFirst
        // 
        chkDeleteFirst.AutoSize = true;
        chkDeleteFirst.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkDeleteFirst.Location = new Point(439, 565);
        chkDeleteFirst.Name = "chkDeleteFirst";
        chkDeleteFirst.Size = new Size(208, 35);
        chkDeleteFirst.TabIndex = 15;
        chkDeleteFirst.Text = "先删除目标数据";
        chkDeleteFirst.UseVisualStyleBackColor = true;
        // 
        // btnConfirm
        // 
        btnConfirm.BackColor = Color.FromArgb(24, 145, 176);
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.FlatStyle = FlatStyle.Flat;
        btnConfirm.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnConfirm.ForeColor = Color.White;
        btnConfirm.Location = new Point(13, 606);
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
        btnCancel.Location = new Point(439, 606);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(180, 40);
        btnCancel.TabIndex = 17;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // progressBar
        // 
        progressBar.Location = new Point(13, 652);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(400, 25);
        progressBar.TabIndex = 18;
        // 
        // lblProgress
        // 
        lblProgress.AutoSize = true;
        lblProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Location = new Point(439, 649);
        lblProgress.Name = "lblProgress";
        lblProgress.Size = new Size(54, 28);
        lblProgress.TabIndex = 19;
        lblProgress.Text = "就绪";
        // 
        // pnlSearch
        // 
        pnlSearch.Controls.Add(lblSearchKeyword);
        pnlSearch.Controls.Add(txtSearchKeyword);
        pnlSearch.Controls.Add(btnSearch);
        pnlSearch.Controls.Add(btnAddSelected);
        pnlSearch.Controls.Add(btnClearSelected);
        pnlSearch.Controls.Add(lblSearchProgress);
        pnlSearch.Controls.Add(dgvSearchResults);
        pnlSearch.Dock = DockStyle.Fill;
        pnlSearch.Location = new Point(3, 623);
        pnlSearch.Name = "pnlSearch";
        pnlSearch.Size = new Size(847, 200);
        pnlSearch.TabIndex = 25;
        // 
        // lblSearchKeyword
        // 
        lblSearchKeyword.AutoSize = true;
        lblSearchKeyword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchKeyword.Location = new Point(10, 10);
        lblSearchKeyword.Name = "lblSearchKeyword";
        lblSearchKeyword.Size = new Size(158, 31);
        lblSearchKeyword.TabIndex = 26;
        lblSearchKeyword.Text = "搜索关键字：";
        // 
        // txtSearchKeyword
        // 
        txtSearchKeyword.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSearchKeyword.Location = new Point(158, 7);
        txtSearchKeyword.Name = "txtSearchKeyword";
        txtSearchKeyword.PlaceholderText = "输入APP表单名称...";
        txtSearchKeyword.Size = new Size(377, 38);
        txtSearchKeyword.TabIndex = 27;
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
        btnSearch.TabIndex = 28;
        btnSearch.Text = "查询";
        btnSearch.UseVisualStyleBackColor = false;
        btnSearch.Click += BtnSearch_Click;
        // 
        // btnAddSelected
        // 
        btnAddSelected.BackColor = Color.FromArgb(57, 181, 74);
        btnAddSelected.FlatAppearance.BorderSize = 0;
        btnAddSelected.FlatStyle = FlatStyle.Flat;
        btnAddSelected.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnAddSelected.ForeColor = Color.White;
        btnAddSelected.Location = new Point(679, 4);
        btnAddSelected.Name = "btnAddSelected";
        btnAddSelected.Size = new Size(144, 41);
        btnAddSelected.TabIndex = 29;
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
        btnClearSelected.Location = new Point(843, 4);
        btnClearSelected.Name = "btnClearSelected";
        btnClearSelected.Size = new Size(141, 41);
        btnClearSelected.TabIndex = 32;
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
        lblSearchProgress.TabIndex = 30;
        // 
        // dgvSearchResults
        // 
        dgvSearchResults.AllowUserToAddRows = false;
        dgvSearchResults.AllowUserToDeleteRows = false;
        dgvSearchResults.BackgroundColor = Color.White;
        dgvSearchResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvSearchResults.Location = new Point(10, 50);
        dgvSearchResults.Name = "dgvSearchResults";
        dgvSearchResults.ReadOnly = true;
        dgvSearchResults.RowHeadersWidth = 72;
        dgvSearchResults.RowTemplate.Height = 25;
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.Size = new Size(825, 135);
        dgvSearchResults.TabIndex = 31;
        // 
        // CrossDbCopyAppFormForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(873, 690);
        Controls.Add(tableLayoutPanel1);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CrossDbCopyAppFormForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "复制APP表单";
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel1.PerformLayout();
        sourceLayout.ResumeLayout(false);
        sourceLayout.PerformLayout();
        targetLayout.ResumeLayout(false);
        targetLayout.PerformLayout();
        pnlSearch.ResumeLayout(false);
        pnlSearch.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)dgvSearchResults).EndInit();
        ResumeLayout(false);
    }
}
