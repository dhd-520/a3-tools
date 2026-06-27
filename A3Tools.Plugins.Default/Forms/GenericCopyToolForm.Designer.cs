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

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
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
        btnAddSelected = new Button();
        btnClearSelected = new Button();
        lblSearchProgress = new Label();
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

        // mainLayout
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
        mainLayout.Font = new Font("微软雅黑", 10F);
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

        // pnlDatabases
        pnlDatabases.Controls.Add(sourceLayout);
        pnlDatabases.Controls.Add(targetLayout);
        pnlDatabases.Dock = DockStyle.Fill;
        pnlDatabases.Name = "pnlDatabases";
        pnlDatabases.Size = new Size(1250, 294);

        // sourceLayout
        sourceLayout.Anchor = AnchorStyles.None;
        sourceLayout.BackColor = Color.FromArgb(245, 248, 250);
        sourceLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
        sourceLayout.ColumnCount = 2;
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        sourceLayout.Controls.Add(lblSourceTitle, 0, 0);
        sourceLayout.SetColumnSpan(lblSourceTitle, 2);
        sourceLayout.Controls.Add(lblSourceServer, 0, 1);
        sourceLayout.Controls.Add(txtSourceServer, 1, 1);
        sourceLayout.Controls.Add(lblSourceDbName, 0, 2);
        sourceLayout.Controls.Add(txtSourceDbName, 1, 2);
        sourceLayout.Controls.Add(lblSourceUser, 0, 3);
        sourceLayout.Controls.Add(txtSourceUser, 1, 3);
        sourceLayout.Controls.Add(lblSourcePassword, 0, 4);
        sourceLayout.Controls.Add(txtSourcePassword, 1, 4);
        sourceLayout.Controls.Add(btnSelectSource, 1, 5);
        sourceLayout.Font = new Font("微软雅黑", 10F);
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

        // lblSourceTitle
        lblSourceTitle.Dock = DockStyle.Fill;
        lblSourceTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold);
        lblSourceTitle.ForeColor = Color.FromArgb(24, 145, 176);
        lblSourceTitle.Margin = new Padding(3, 3, 3, 10);
        lblSourceTitle.Name = "lblSourceTitle";
        lblSourceTitle.Text = "源数据库";
        lblSourceTitle.TextAlign = ContentAlignment.MiddleCenter;

        // lblSourceServer
        lblSourceServer.Dock = DockStyle.Fill;
        lblSourceServer.Font = new Font("微软雅黑", 10F);
        lblSourceServer.Margin = new Padding(3, 3, 3, 8);
        lblSourceServer.Name = "lblSourceServer";
        lblSourceServer.Text = "服务器地址：";
        lblSourceServer.TextAlign = ContentAlignment.MiddleRight;

        // txtSourceServer
        txtSourceServer.Dock = DockStyle.Fill;
        txtSourceServer.Font = new Font("微软雅黑", 10F);
        txtSourceServer.Margin = new Padding(3, 3, 3, 8);
        txtSourceServer.Name = "txtSourceServer";

        // lblSourceDbName
        lblSourceDbName.Dock = DockStyle.Fill;
        lblSourceDbName.Font = new Font("微软雅黑", 10F);
        lblSourceDbName.Margin = new Padding(3, 3, 3, 8);
        lblSourceDbName.Name = "lblSourceDbName";
        lblSourceDbName.Text = "数据库名称：";
        lblSourceDbName.TextAlign = ContentAlignment.MiddleRight;

        // txtSourceDbName
        txtSourceDbName.Dock = DockStyle.Fill;
        txtSourceDbName.Font = new Font("微软雅黑", 10F);
        txtSourceDbName.Margin = new Padding(3, 3, 3, 8);
        txtSourceDbName.Name = "txtSourceDbName";

        // lblSourceUser
        lblSourceUser.Dock = DockStyle.Fill;
        lblSourceUser.Font = new Font("微软雅黑", 10F);
        lblSourceUser.Margin = new Padding(3, 3, 3, 8);
        lblSourceUser.Name = "lblSourceUser";
        lblSourceUser.Text = "用户名：";
        lblSourceUser.TextAlign = ContentAlignment.MiddleRight;

        // txtSourceUser
        txtSourceUser.Dock = DockStyle.Fill;
        txtSourceUser.Font = new Font("微软雅黑", 10F);
        txtSourceUser.Margin = new Padding(3, 3, 3, 8);
        txtSourceUser.Name = "txtSourceUser";

        // lblSourcePassword
        lblSourcePassword.Dock = DockStyle.Fill;
        lblSourcePassword.Font = new Font("微软雅黑", 10F);
        lblSourcePassword.Margin = new Padding(3, 3, 3, 8);
        lblSourcePassword.Name = "lblSourcePassword";
        lblSourcePassword.Text = "密码：";
        lblSourcePassword.TextAlign = ContentAlignment.MiddleRight;

        // txtSourcePassword
        txtSourcePassword.Dock = DockStyle.Fill;
        txtSourcePassword.Font = new Font("微软雅黑", 10F);
        txtSourcePassword.Margin = new Padding(3, 3, 3, 8);
        txtSourcePassword.Name = "txtSourcePassword";
        txtSourcePassword.UseSystemPasswordChar = true;

        // btnSelectSource
        btnSelectSource.BackColor = Color.FromArgb(24, 145, 176);
        btnSelectSource.FlatAppearance.BorderSize = 0;
        btnSelectSource.FlatStyle = FlatStyle.Flat;
        btnSelectSource.Font = new Font("微软雅黑", 9F);
        btnSelectSource.ForeColor = Color.White;
        btnSelectSource.Margin = new Padding(3, 0, 3, 3);
        btnSelectSource.Name = "btnSelectSource";
        btnSelectSource.Size = new Size(134, 32);
        btnSelectSource.Text = "选择账套";
        btnSelectSource.UseVisualStyleBackColor = false;
        btnSelectSource.Click += BtnSelectSource_Click;

        // targetLayout
        targetLayout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        targetLayout.BackColor = Color.FromArgb(250, 245, 245);
        targetLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
        targetLayout.ColumnCount = 2;
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        targetLayout.Controls.Add(lblTargetTitle, 0, 0);
        targetLayout.SetColumnSpan(lblTargetTitle, 2);
        targetLayout.Controls.Add(lblTargetServer, 0, 1);
        targetLayout.Controls.Add(txtTargetServer, 1, 1);
        targetLayout.Controls.Add(lblTargetDbName, 0, 2);
        targetLayout.Controls.Add(txtTargetDbName, 1, 2);
        targetLayout.Controls.Add(lblTargetUser, 0, 3);
        targetLayout.Controls.Add(txtTargetUser, 1, 3);
        targetLayout.Controls.Add(lblTargetPassword, 0, 4);
        targetLayout.Controls.Add(txtTargetPassword, 1, 4);
        targetLayout.Controls.Add(btnSelectTarget, 1, 5);
        targetLayout.Font = new Font("微软雅黑", 10F);
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

        // lblTargetTitle
        lblTargetTitle.Dock = DockStyle.Fill;
        lblTargetTitle.Font = new Font("微软雅黑", 11F, FontStyle.Bold);
        lblTargetTitle.ForeColor = Color.FromArgb(200, 80, 80);
        lblTargetTitle.Margin = new Padding(3, 3, 3, 10);
        lblTargetTitle.Name = "lblTargetTitle";
        lblTargetTitle.Text = "目标数据库";
        lblTargetTitle.TextAlign = ContentAlignment.MiddleCenter;

        // lblTargetServer
        lblTargetServer.Dock = DockStyle.Fill;
        lblTargetServer.Font = new Font("微软雅黑", 10F);
        lblTargetServer.Margin = new Padding(3, 3, 3, 8);
        lblTargetServer.Name = "lblTargetServer";
        lblTargetServer.Text = "服务器地址：";
        lblTargetServer.TextAlign = ContentAlignment.MiddleRight;

        // txtTargetServer
        txtTargetServer.Dock = DockStyle.Fill;
        txtTargetServer.Font = new Font("微软雅黑", 10F);
        txtTargetServer.Margin = new Padding(3, 3, 3, 8);
        txtTargetServer.Name = "txtTargetServer";

        // lblTargetDbName
        lblTargetDbName.Dock = DockStyle.Fill;
        lblTargetDbName.Font = new Font("微软雅黑", 10F);
        lblTargetDbName.Margin = new Padding(3, 3, 3, 8);
        lblTargetDbName.Name = "lblTargetDbName";
        lblTargetDbName.Text = "数据库名称：";
        lblTargetDbName.TextAlign = ContentAlignment.MiddleRight;

        // txtTargetDbName
        txtTargetDbName.Dock = DockStyle.Fill;
        txtTargetDbName.Font = new Font("微软雅黑", 10F);
        txtTargetDbName.Margin = new Padding(3, 3, 3, 8);
        txtTargetDbName.Name = "txtTargetDbName";

        // lblTargetUser
        lblTargetUser.Dock = DockStyle.Fill;
        lblTargetUser.Font = new Font("微软雅黑", 10F);
        lblTargetUser.Margin = new Padding(3, 3, 3, 8);
        lblTargetUser.Name = "lblTargetUser";
        lblTargetUser.Text = "用户名：";
        lblTargetUser.TextAlign = ContentAlignment.MiddleRight;

        // txtTargetUser
        txtTargetUser.Dock = DockStyle.Fill;
        txtTargetUser.Font = new Font("微软雅黑", 10F);
        txtTargetUser.Margin = new Padding(3, 3, 3, 8);
        txtTargetUser.Name = "txtTargetUser";

        // lblTargetPassword
        lblTargetPassword.Dock = DockStyle.Fill;
        lblTargetPassword.Font = new Font("微软雅黑", 10F);
        lblTargetPassword.Margin = new Padding(3, 3, 3, 8);
        lblTargetPassword.Name = "lblTargetPassword";
        lblTargetPassword.Text = "密码：";
        lblTargetPassword.TextAlign = ContentAlignment.MiddleRight;

        // txtTargetPassword
        txtTargetPassword.Dock = DockStyle.Fill;
        txtTargetPassword.Font = new Font("微软雅黑", 10F);
        txtTargetPassword.Margin = new Padding(3, 3, 3, 8);
        txtTargetPassword.Name = "txtTargetPassword";
        txtTargetPassword.UseSystemPasswordChar = true;

        // btnSelectTarget
        btnSelectTarget.BackColor = Color.FromArgb(200, 80, 80);
        btnSelectTarget.FlatAppearance.BorderSize = 0;
        btnSelectTarget.FlatStyle = FlatStyle.Flat;
        btnSelectTarget.Font = new Font("微软雅黑", 9F);
        btnSelectTarget.ForeColor = Color.White;
        btnSelectTarget.Margin = new Padding(3, 0, 3, 3);
        btnSelectTarget.Name = "btnSelectTarget";
        btnSelectTarget.Size = new Size(132, 32);
        btnSelectTarget.Text = "选择账套";
        btnSelectTarget.UseVisualStyleBackColor = false;
        btnSelectTarget.Click += BtnSelectTarget_Click;

        // lblConfigInfo
        lblConfigInfo.Dock = DockStyle.Fill;
        lblConfigInfo.Font = new Font("微软雅黑", 9F);
        lblConfigInfo.ForeColor = Color.Gray;
        lblConfigInfo.Name = "lblConfigInfo";
        lblConfigInfo.Text = "主表：…  复制关键字：…  关联表：…  关联字段：…";

        // lblTitleHint
        lblTitleHint.Dock = DockStyle.Fill;
        lblTitleHint.Font = new Font("微软雅黑", 10F);
        lblTitleHint.Name = "lblTitleHint";
        lblTitleHint.Text = "复制关键字：";

        // txtKeyValues
        txtKeyValues.Dock = DockStyle.Fill;
        txtKeyValues.Font = new Font("微软雅黑", 10F);
        txtKeyValues.Multiline = true;
        txtKeyValues.Name = "txtKeyValues";
        txtKeyValues.Size = new Size(1250, 94);

        // rowHintAndCheckbox
        rowHintAndCheckbox.ColumnCount = 2;
        rowHintAndCheckbox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowHintAndCheckbox.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        rowHintAndCheckbox.Controls.Add(lblSearchHint, 0, 0);
        rowHintAndCheckbox.Controls.Add(chkDeleteFirst, 1, 0);
        rowHintAndCheckbox.Dock = DockStyle.Fill;
        rowHintAndCheckbox.Name = "rowHintAndCheckbox";
        rowHintAndCheckbox.RowCount = 1;
        rowHintAndCheckbox.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowHintAndCheckbox.Size = new Size(1250, 38);

        // lblSearchHint
        lblSearchHint.Dock = DockStyle.Fill;
        lblSearchHint.Font = new Font("微软雅黑", 9F);
        lblSearchHint.ForeColor = Color.Gray;
        lblSearchHint.Name = "lblSearchHint";
        lblSearchHint.Text = "提示：可通过下方搜索添加";

        // chkDeleteFirst
        chkDeleteFirst.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        chkDeleteFirst.AutoSize = true;
        chkDeleteFirst.Checked = true;
        chkDeleteFirst.Font = new Font("微软雅黑", 10F);
        chkDeleteFirst.Name = "chkDeleteFirst";
        chkDeleteFirst.Text = "先删除目标数据";

        // pnlButtons
        pnlButtons.Controls.Add(btnConfirm);
        pnlButtons.Controls.Add(btnCancel);
        pnlButtons.Dock = DockStyle.Fill;
        pnlButtons.Name = "pnlButtons";
        pnlButtons.Size = new Size(1250, 40);

        // btnConfirm
        btnConfirm.BackColor = Color.FromArgb(24, 145, 176);
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.FlatStyle = FlatStyle.Flat;
        btnConfirm.Font = new Font("微软雅黑", 10F);
        btnConfirm.ForeColor = Color.White;
        btnConfirm.Location = new Point(452, 0);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(120, 40);
        btnConfirm.Text = "确认复制";
        btnConfirm.UseVisualStyleBackColor = false;
        btnConfirm.Click += BtnConfirm_Click;

        // btnCancel
        btnCancel.BackColor = Color.White;
        btnCancel.FlatAppearance.BorderColor = Color.Gray;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F);
        btnCancel.ForeColor = Color.Gray;
        btnCancel.Location = new Point(649, 0);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(120, 39);
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;

        // progressBar
        progressBar.Dock = DockStyle.Fill;
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(1250, 19);

        // lblProgress
        lblProgress.Dock = DockStyle.Fill;
        lblProgress.Font = new Font("微软雅黑", 9F);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Name = "lblProgress";
        lblProgress.Text = "就绪";

        // pnlSearch
        pnlSearch.Controls.Add(lblSearchKeyword);
        pnlSearch.Controls.Add(txtSearchKeyword);
        pnlSearch.Controls.Add(btnSearch);
        pnlSearch.Controls.Add(btnAddSelected);
        pnlSearch.Controls.Add(btnClearSelected);
        pnlSearch.Controls.Add(lblSearchProgress);
        pnlSearch.Controls.Add(dgvSearchResults);
        pnlSearch.Dock = DockStyle.Fill;
        pnlSearch.Name = "pnlSearch";
        pnlSearch.Size = new Size(1250, 375);

        // lblSearchKeyword
        lblSearchKeyword.AutoSize = true;
        lblSearchKeyword.Font = new Font("微软雅黑", 10F);
        lblSearchKeyword.Location = new Point(10, 10);
        lblSearchKeyword.Name = "lblSearchKeyword";
        lblSearchKeyword.Text = "搜索关键字：";

        // txtSearchKeyword
        txtSearchKeyword.Font = new Font("微软雅黑", 10F);
        txtSearchKeyword.Location = new Point(168, 7);
        txtSearchKeyword.Name = "txtSearchKeyword";
        txtSearchKeyword.PlaceholderText = "按主键或名称搜索...";
        txtSearchKeyword.Size = new Size(383, 38);

        // btnSearch
        btnSearch.BackColor = Color.FromArgb(24, 145, 176);
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.FlatStyle = FlatStyle.Flat;
        btnSearch.Font = new Font("微软雅黑", 10F);
        btnSearch.ForeColor = Color.White;
        btnSearch.Location = new Point(570, 5);
        btnSearch.Name = "btnSearch";
        btnSearch.Size = new Size(88, 41);
        btnSearch.Text = "查询";
        btnSearch.UseVisualStyleBackColor = false;
        btnSearch.Click += BtnSearch_Click;

        // btnAddSelected
        btnAddSelected.BackColor = Color.FromArgb(57, 181, 74);
        btnAddSelected.FlatAppearance.BorderSize = 0;
        btnAddSelected.FlatStyle = FlatStyle.Flat;
        btnAddSelected.Font = new Font("微软雅黑", 10F);
        btnAddSelected.ForeColor = Color.White;
        btnAddSelected.Location = new Point(679, 4);
        btnAddSelected.Name = "btnAddSelected";
        btnAddSelected.Size = new Size(144, 41);
        btnAddSelected.Text = "添加选中";
        btnAddSelected.UseVisualStyleBackColor = false;
        btnAddSelected.Click += BtnAddSelected_Click;

        // btnClearSelected
        btnClearSelected.BackColor = Color.FromArgb(200, 80, 80);
        btnClearSelected.FlatAppearance.BorderSize = 0;
        btnClearSelected.FlatStyle = FlatStyle.Flat;
        btnClearSelected.Font = new Font("微软雅黑", 10F);
        btnClearSelected.ForeColor = Color.White;
        btnClearSelected.Location = new Point(843, 4);
        btnClearSelected.Name = "btnClearSelected";
        btnClearSelected.Size = new Size(141, 41);
        btnClearSelected.Text = "清空选项";
        btnClearSelected.UseVisualStyleBackColor = false;
        btnClearSelected.Click += BtnClearSelected_Click;

        // lblSearchProgress
        lblSearchProgress.AutoSize = true;
        lblSearchProgress.Font = new Font("微软雅黑", 9F);
        lblSearchProgress.ForeColor = Color.Gray;
        lblSearchProgress.Location = new Point(605, 10);
        lblSearchProgress.Name = "lblSearchProgress";

        // dgvSearchResults
        dgvSearchResults.AllowUserToAddRows = false;
        dgvSearchResults.AllowUserToDeleteRows = false;
        dgvSearchResults.BackgroundColor = Color.White;
        dgvSearchResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvSearchResults.Location = new Point(10, 45);
        dgvSearchResults.Name = "dgvSearchResults";
        dgvSearchResults.ReadOnly = true;
        dgvSearchResults.RowHeadersWidth = 72;
        dgvSearchResults.RowTemplate.Height = 25;
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.Size = new Size(1225, 297);

        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1256, 951);
        Controls.Add(mainLayout);
        Font = new Font("微软雅黑", 10F);
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