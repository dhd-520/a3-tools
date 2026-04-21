namespace A3Tools.Plugins.Default.Forms;

partial class CrossDbCopyTableForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TableLayoutPanel mainLayout;
    private System.Windows.Forms.Panel sourcePanel;
    private System.Windows.Forms.Panel targetPanel;
    private System.Windows.Forms.Panel tablePanel;
    private System.Windows.Forms.Panel progressPanel;
    private System.Windows.Forms.TableLayoutPanel sourceLayout;
    private System.Windows.Forms.TableLayoutPanel targetLayout;
    private System.Windows.Forms.TableLayoutPanel tableLayout;
    private System.Windows.Forms.TableLayoutPanel buttonLayout;

    private System.Windows.Forms.Label lblSourceTitle;
    private System.Windows.Forms.Button btnSelectSource;
    private System.Windows.Forms.Label lblSourceServer;
    private System.Windows.Forms.TextBox txtSourceServer;
    private System.Windows.Forms.Label lblSourceUser;
    private System.Windows.Forms.TextBox txtSourceUser;
    private System.Windows.Forms.Label lblSourcePwd;
    private System.Windows.Forms.TextBox txtSourcePassword;

    private System.Windows.Forms.Label lblTargetTitle;
    private System.Windows.Forms.Button btnSelectTarget;
    private System.Windows.Forms.Label lblTargetServer;
    private System.Windows.Forms.TextBox txtTargetServer;
    private System.Windows.Forms.Label lblTargetUser;
    private System.Windows.Forms.TextBox txtTargetUser;
    private System.Windows.Forms.Label lblTargetPwd;
    private System.Windows.Forms.TextBox txtTargetPassword;

    private System.Windows.Forms.Label lblTableTitle;
    private System.Windows.Forms.Label lblTableTip;
    private System.Windows.Forms.TextBox txtTables;

    private System.Windows.Forms.Label lblProgress;
    private System.Windows.Forms.ProgressBar progressBar;

    private System.Windows.Forms.Button btnConfirm;
    private System.Windows.Forms.Button btnCancel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        // 主布局
        this.mainLayout = new System.Windows.Forms.TableLayoutPanel();
        this.mainLayout.Name = "mainLayout";
        this.mainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.mainLayout.Padding = new System.Windows.Forms.Padding(10);
        this.mainLayout.RowCount = 5;
        this.mainLayout.ColumnCount = 1;
        this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 156));
        this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 156));
        this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
        this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50));
        this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50));

        // 源数据库面板
        this.sourcePanel = new System.Windows.Forms.Panel();
        this.sourcePanel.Name = "sourcePanel";
        this.sourcePanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.sourcePanel.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);
        this.sourcePanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);

        this.sourceLayout = new System.Windows.Forms.TableLayoutPanel();
        this.sourceLayout.Name = "sourceLayout";
        this.sourceLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.sourceLayout.RowCount = 3;
        this.sourceLayout.ColumnCount = 4;
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40));
        this.sourceLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100));
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50));
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100));
        this.sourceLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50));

        this.lblSourceTitle = new System.Windows.Forms.Label();
        this.lblSourceTitle.Name = "lblSourceTitle";
        this.lblSourceTitle.Text = "源数据库";
        this.lblSourceTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
        this.lblSourceTitle.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);

        this.btnSelectSource = new System.Windows.Forms.Button();
        this.btnSelectSource.Name = "btnSelectSource";
        this.btnSelectSource.Text = "选择账套";
        this.btnSelectSource.Dock = System.Windows.Forms.DockStyle.Fill;
        this.btnSelectSource.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSelectSource.BackColor = System.Drawing.Color.White;
        this.btnSelectSource.ForeColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnSelectSource.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnSelectSource.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnSelectSource.FlatAppearance.BorderSize = 1;
        this.btnSelectSource.Click += new System.EventHandler(this.BtnSelectSource_Click);

        this.lblSourceServer = new System.Windows.Forms.Label();
        this.lblSourceServer.Name = "lblSourceServer";
        this.lblSourceServer.Text = "数据库地址：";
        this.lblSourceServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceServer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtSourceServer = new System.Windows.Forms.TextBox();
        this.txtSourceServer.Name = "txtSourceServer";
        this.txtSourceServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourceServer.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtSourceServer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblSourceUser = new System.Windows.Forms.Label();
        this.lblSourceUser.Name = "lblSourceUser";
        this.lblSourceUser.Text = "用户名：";
        this.lblSourceUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourceUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtSourceUser = new System.Windows.Forms.TextBox();
        this.txtSourceUser.Name = "txtSourceUser";
        this.txtSourceUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourceUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblSourcePwd = new System.Windows.Forms.Label();
        this.lblSourcePwd.Name = "lblSourcePwd";
        this.lblSourcePwd.Text = "密码：";
        this.lblSourcePwd.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblSourcePwd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtSourcePassword = new System.Windows.Forms.TextBox();
        this.txtSourcePassword.Name = "txtSourcePassword";
        this.txtSourcePassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtSourcePassword.PasswordChar = '*';
        this.txtSourcePassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        // 目标数据库面板
        this.targetPanel = new System.Windows.Forms.Panel();
        this.targetPanel.Name = "targetPanel";
        this.targetPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.targetPanel.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);
        this.targetPanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);

        this.targetLayout = new System.Windows.Forms.TableLayoutPanel();
        this.targetLayout.Name = "targetLayout";
        this.targetLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.targetLayout.RowCount = 3;
        this.targetLayout.ColumnCount = 4;
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40));
        this.targetLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100));
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50));
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100));
        this.targetLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50));

        this.lblTargetTitle = new System.Windows.Forms.Label();
        this.lblTargetTitle.Name = "lblTargetTitle";
        this.lblTargetTitle.Text = "目标数据库";
        this.lblTargetTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
        this.lblTargetTitle.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);

        this.btnSelectTarget = new System.Windows.Forms.Button();
        this.btnSelectTarget.Name = "btnSelectTarget";
        this.btnSelectTarget.Text = "选择账套";
        this.btnSelectTarget.Dock = System.Windows.Forms.DockStyle.Fill;
        this.btnSelectTarget.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSelectTarget.BackColor = System.Drawing.Color.White;
        this.btnSelectTarget.ForeColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnSelectTarget.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnSelectTarget.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnSelectTarget.FlatAppearance.BorderSize = 1;
        this.btnSelectTarget.Click += new System.EventHandler(this.BtnSelectTarget_Click);

        this.lblTargetServer = new System.Windows.Forms.Label();
        this.lblTargetServer.Name = "lblTargetServer";
        this.lblTargetServer.Text = "数据库地址：";
        this.lblTargetServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetServer.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtTargetServer = new System.Windows.Forms.TextBox();
        this.txtTargetServer.Name = "txtTargetServer";
        this.txtTargetServer.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetServer.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTargetServer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblTargetUser = new System.Windows.Forms.Label();
        this.lblTargetUser.Name = "lblTargetUser";
        this.lblTargetUser.Text = "用户名：";
        this.lblTargetUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtTargetUser = new System.Windows.Forms.TextBox();
        this.txtTargetUser.Name = "txtTargetUser";
        this.txtTargetUser.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetUser.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblTargetPwd = new System.Windows.Forms.Label();
        this.lblTargetPwd.Name = "lblTargetPwd";
        this.lblTargetPwd.Text = "密码：";
        this.lblTargetPwd.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTargetPwd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this.txtTargetPassword = new System.Windows.Forms.TextBox();
        this.txtTargetPassword.Name = "txtTargetPassword";
        this.txtTargetPassword.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTargetPassword.PasswordChar = '*';
        this.txtTargetPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        // 表名称面板
        this.tablePanel = new System.Windows.Forms.Panel();
        this.tablePanel.Name = "tablePanel";
        this.tablePanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tablePanel.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);
        this.tablePanel.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);

        this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
        this.tableLayout.Name = "tableLayout";
        this.tableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tableLayout.RowCount = 2;
        this.tableLayout.ColumnCount = 2;
        this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34));
        this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
        this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120));
        this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));

        this.lblTableTitle = new System.Windows.Forms.Label();
        this.lblTableTitle.Name = "lblTableTitle";
        this.lblTableTitle.Text = "表名称";
        this.lblTableTitle.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTableTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
        this.lblTableTitle.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);

        this.lblTableTip = new System.Windows.Forms.Label();
        this.lblTableTip.Name = "lblTableTip";
        this.lblTableTip.Text = "多张表用英文分号隔开，例如：Table1;Table2;Table3";
        this.lblTableTip.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblTableTip.Font = new System.Drawing.Font("微软雅黑", 9F);
        this.lblTableTip.ForeColor = System.Drawing.Color.Gray;

        this.txtTables = new System.Windows.Forms.TextBox();
        this.txtTables.Name = "txtTables";
        this.txtTables.Dock = System.Windows.Forms.DockStyle.Fill;
        this.txtTables.Multiline = true;
        this.txtTables.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.txtTables.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.txtTables.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;

        // 进度面板
        this.progressPanel = new System.Windows.Forms.Panel();
        this.progressPanel.Name = "progressPanel";
        this.progressPanel.Dock = System.Windows.Forms.DockStyle.Fill;

        this.lblProgress = new System.Windows.Forms.Label();
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.Text = "";
        this.lblProgress.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblProgress.Height = 22;
        this.lblProgress.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.lblProgress.ForeColor = System.Drawing.Color.FromArgb(24, 145, 176);

        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.progressBar.Name = "progressBar";
        this.progressBar.Dock = System.Windows.Forms.DockStyle.Top;
        this.progressBar.Height = 20;
        this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        this.progressBar.Maximum = 100;

        // 按钮面板
        this.buttonLayout = new System.Windows.Forms.TableLayoutPanel();
        this.buttonLayout.Name = "buttonLayout";
        this.buttonLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this.buttonLayout.ColumnCount = 3;
        this.buttonLayout.RowCount = 1;
        this.buttonLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
        this.buttonLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110));
        this.buttonLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110));

        this.btnConfirm = new System.Windows.Forms.Button();
        this.btnConfirm.Name = "btnConfirm";
        this.btnConfirm.Text = "确认";
        this.btnConfirm.Size = new System.Drawing.Size(100, 36);
        this.btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnConfirm.BackColor = System.Drawing.Color.FromArgb(24, 145, 176);
        this.btnConfirm.ForeColor = System.Drawing.Color.White;
        this.btnConfirm.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnConfirm.FlatAppearance.BorderSize = 0;
        this.btnConfirm.Click += new System.EventHandler(this.BtnConfirm_Click);

        this.btnCancel = new System.Windows.Forms.Button();
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Text = "取消";
        this.btnCancel.Size = new System.Drawing.Size(100, 36);
        this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnCancel.BackColor = System.Drawing.Color.White;
        this.btnCancel.ForeColor = System.Drawing.Color.FromArgb(102, 102, 102);
        this.btnCancel.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(180, 180, 180);
        this.btnCancel.FlatAppearance.BorderSize = 1;
        this.btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);

        System.Windows.Forms.Label buttonSpacer = new System.Windows.Forms.Label();
        buttonSpacer.Name = "buttonSpacer";
        buttonSpacer.Dock = System.Windows.Forms.DockStyle.Fill;

        // 布局子控件
        this.sourceLayout.Controls.Add(this.lblSourceTitle, 0, 0);
        this.sourceLayout.SetColumnSpan(this.lblSourceTitle, 3);
        this.sourceLayout.Controls.Add(this.btnSelectSource, 3, 0);
        this.sourceLayout.Controls.Add(this.lblSourceServer, 0, 1);
        this.sourceLayout.Controls.Add(this.txtSourceServer, 1, 1);
        this.sourceLayout.SetColumnSpan(this.txtSourceServer, 3);
        this.sourceLayout.Controls.Add(this.lblSourceUser, 0, 2);
        this.sourceLayout.Controls.Add(this.txtSourceUser, 1, 2);
        this.sourceLayout.Controls.Add(this.lblSourcePwd, 2, 2);
        this.sourceLayout.Controls.Add(this.txtSourcePassword, 3, 2);
        this.sourcePanel.Controls.Add(this.sourceLayout);

        this.targetLayout.Controls.Add(this.lblTargetTitle, 0, 0);
        this.targetLayout.SetColumnSpan(this.lblTargetTitle, 3);
        this.targetLayout.Controls.Add(this.btnSelectTarget, 3, 0);
        this.targetLayout.Controls.Add(this.lblTargetServer, 0, 1);
        this.targetLayout.Controls.Add(this.txtTargetServer, 1, 1);
        this.targetLayout.SetColumnSpan(this.txtTargetServer, 3);
        this.targetLayout.Controls.Add(this.lblTargetUser, 0, 2);
        this.targetLayout.Controls.Add(this.txtTargetUser, 1, 2);
        this.targetLayout.Controls.Add(this.lblTargetPwd, 2, 2);
        this.targetLayout.Controls.Add(this.txtTargetPassword, 3, 2);
        this.targetPanel.Controls.Add(this.targetLayout);

        this.tableLayout.Controls.Add(this.lblTableTitle, 0, 0);
        this.tableLayout.Controls.Add(this.lblTableTip, 1, 0);
        this.tableLayout.Controls.Add(this.txtTables, 0, 1);
        this.tableLayout.SetColumnSpan(this.txtTables, 2);
        this.tablePanel.Controls.Add(this.tableLayout);

        this.progressPanel.Controls.Add(this.progressBar);
        this.progressPanel.Controls.Add(this.lblProgress);

        this.buttonLayout.Controls.Add(buttonSpacer, 0, 0);
        this.buttonLayout.Controls.Add(this.btnConfirm, 1, 0);
        this.buttonLayout.Controls.Add(this.btnCancel, 2, 0);

        // 主布局添加子面板
        this.mainLayout.Controls.Add(this.sourcePanel, 0, 0);
        this.mainLayout.Controls.Add(this.targetPanel, 0, 1);
        this.mainLayout.Controls.Add(this.tablePanel, 0, 2);
        this.mainLayout.Controls.Add(this.progressPanel, 0, 3);
        this.mainLayout.Controls.Add(this.buttonLayout, 0, 4);

        //
        // CrossDbCopyTableForm
        //
        this.Text = "跨库复制表结构";
        this.ClientSize = new System.Drawing.Size(1080, 624);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = System.Drawing.Color.White;
        this.Font = new System.Drawing.Font("微软雅黑", 10F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.Padding = new System.Windows.Forms.Padding(10);
        this.Controls.Add(this.mainLayout);
    }
}