using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

partial class SqlQueryForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel pnlTop;
    private Label lblAccount;
    private Label lblServer;
    private Label lblDatabase;
    private ComboBox cmbDatabase;
    private Button btnRefreshDb;
    private Button btnDisconnect;

    private Panel pnlToolBar;
    private Button btnNewTab;
    private Button btnCloseCurrent;
    private Button btnCloseOthers;
    private Button btnToggleExplorer;

    private TabControl tabControl;
    private ContextMenuStrip ctxTab;

    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblConnInfo;
    private ToolStripStatusLabel lblStatus;
    private ToolStripStatusLabel lblElapsed;
    private ToolStripStatusLabel lblRows;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        ctxTab = new ContextMenuStrip(components);
        miClose = new ToolStripMenuItem();
        miCloseOthers = new ToolStripMenuItem();
        miRename = new ToolStripMenuItem();
        pnlTop = new Panel();
        lblAccount = new Label();
        lblServer = new Label();
        lblDatabase = new Label();
        cmbDatabase = new ComboBox();
        btnRefreshDb = new Button();
        btnDisconnect = new Button();
        pnlToolBar = new Panel();
        btnNewTab = new Button();
        btnCloseCurrent = new Button();
        btnCloseOthers = new Button();
        btnToggleExplorer = new Button();
        tabControl = new TabControl();
        statusStrip = new StatusStrip();
        lblConnInfo = new ToolStripStatusLabel();
        lblStatus = new ToolStripStatusLabel();
        lblElapsed = new ToolStripStatusLabel();
        lblRows = new ToolStripStatusLabel();
        ctxTab.SuspendLayout();
        pnlTop.SuspendLayout();
        pnlToolBar.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // ctxTab
        // 
        ctxTab.ImageScalingSize = new Size(28, 28);
        ctxTab.Items.AddRange(new ToolStripItem[] { miClose, miCloseOthers, miRename });
        ctxTab.Name = "ctxTab";
        ctxTab.Size = new Size(169, 106);
        // 
        // miClose
        // 
        miClose.Name = "miClose";
        miClose.Size = new Size(168, 34);
        miClose.Text = "关闭当前";
        miClose.Click += MiClose_Click;
        // 
        // miCloseOthers
        // 
        miCloseOthers.Name = "miCloseOthers";
        miCloseOthers.Size = new Size(168, 34);
        miCloseOthers.Text = "关闭其他";
        miCloseOthers.Click += BtnCloseOthers_Click;
        // 
        // miRename
        // 
        miRename.Name = "miRename";
        miRename.Size = new Size(168, 34);
        miRename.Text = "重命名";
        miRename.Click += MiRename_Click;
        // 
        // pnlTop
        // 
        pnlTop.BackColor = Color.FromArgb(245, 247, 250);
        pnlTop.Controls.Add(lblAccount);
        pnlTop.Controls.Add(lblServer);
        pnlTop.Controls.Add(lblDatabase);
        pnlTop.Controls.Add(cmbDatabase);
        pnlTop.Controls.Add(btnRefreshDb);
        pnlTop.Controls.Add(btnDisconnect);
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Location = new Point(0, 0);
        pnlTop.Name = "pnlTop";
        pnlTop.Padding = new Padding(8);
        pnlTop.Size = new Size(1280, 40);
        pnlTop.TabIndex = 3;
        // 
        // lblAccount
        // 
        lblAccount.AutoSize = true;
        lblAccount.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        lblAccount.Location = new Point(8, 12);
        lblAccount.Name = "lblAccount";
        lblAccount.Size = new Size(75, 28);
        lblAccount.TabIndex = 0;
        lblAccount.Text = "账套: -";
        // 
        // lblServer
        // 
        lblServer.AutoSize = true;
        lblServer.ForeColor = Color.FromArgb(96, 96, 96);
        lblServer.Location = new Point(200, 12);
        lblServer.Name = "lblServer";
        lblServer.Size = new Size(95, 28);
        lblServer.TabIndex = 1;
        lblServer.Text = "服务器: -";
        // 
        // lblDatabase
        // 
        lblDatabase.AutoSize = true;
        lblDatabase.ForeColor = Color.FromArgb(96, 96, 96);
        lblDatabase.Location = new Point(420, 12);
        lblDatabase.Name = "lblDatabase";
        lblDatabase.Size = new Size(80, 28);
        lblDatabase.TabIndex = 2;
        lblDatabase.Text = "当前库:";
        // 
        // cmbDatabase
        // 
        cmbDatabase.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDatabase.Location = new Point(506, 9);
        cmbDatabase.Name = "cmbDatabase";
        cmbDatabase.Size = new Size(372, 36);
        cmbDatabase.TabIndex = 3;
        cmbDatabase.SelectedIndexChanged += CmbDatabase_SelectedIndexChanged;
        // 
        // btnRefreshDb
        // 
        btnRefreshDb.Location = new Point(884, 8);
        btnRefreshDb.Name = "btnRefreshDb";
        btnRefreshDb.Size = new Size(103, 32);
        btnRefreshDb.TabIndex = 4;
        btnRefreshDb.Text = "刷新";
        btnRefreshDb.Click += BtnRefreshDb_Click;
        // 
        // btnDisconnect
        // 
        btnDisconnect.Location = new Point(1003, 9);
        btnDisconnect.Name = "btnDisconnect";
        btnDisconnect.Size = new Size(119, 32);
        btnDisconnect.TabIndex = 5;
        btnDisconnect.Text = "断开";
        btnDisconnect.Click += BtnDisconnect_Click;
        // 
        // pnlToolBar
        // 
        pnlToolBar.BackColor = Color.FromArgb(250, 250, 250);
        pnlToolBar.Controls.Add(btnNewTab);
        pnlToolBar.Controls.Add(btnCloseCurrent);
        pnlToolBar.Controls.Add(btnCloseOthers);
        pnlToolBar.Controls.Add(btnToggleExplorer);
        pnlToolBar.Dock = DockStyle.Top;
        pnlToolBar.Location = new Point(0, 40);
        pnlToolBar.Name = "pnlToolBar";
        pnlToolBar.Padding = new Padding(8, 5, 8, 5);
        pnlToolBar.Size = new Size(1280, 36);
        pnlToolBar.TabIndex = 2;
        // 
        // btnNewTab
        // 
        btnNewTab.Location = new Point(8, 5);
        btnNewTab.Name = "btnNewTab";
        btnNewTab.Size = new Size(92, 31);
        btnNewTab.TabIndex = 0;
        btnNewTab.Text = "+ 新建查询";
        btnNewTab.Click += BtnNewTab_Click;
        // 
        // btnCloseCurrent
        // 
        btnCloseCurrent.Location = new Point(106, 5);
        btnCloseCurrent.Name = "btnCloseCurrent";
        btnCloseCurrent.Size = new Size(137, 31);
        btnCloseCurrent.TabIndex = 1;
        btnCloseCurrent.Text = "× 关闭当前";
        btnCloseCurrent.Click += BtnCloseCurrent_Click;
        // 
        // btnCloseOthers
        //
        btnCloseOthers.Location = new Point(249, 4);
        btnCloseOthers.Name = "btnCloseOthers";
        btnCloseOthers.Size = new Size(174, 32);
        btnCloseOthers.TabIndex = 2;
        btnCloseOthers.Text = "× 关闭其他";
        btnCloseOthers.Click += BtnCloseOthers_Click;

        //
        // btnToggleExplorer
        //
        btnToggleExplorer.Location = new Point(450, 4);
        btnToggleExplorer.Name = "btnToggleExplorer";
        btnToggleExplorer.Size = new Size(180, 32);
        btnToggleExplorer.TabIndex = 3;
        btnToggleExplorer.Text = "📂 对象资源管理器";
        btnToggleExplorer.Click += BtnToggleExplorer_Click;
        // 
        // tabControl
        // 
        tabControl.ContextMenuStrip = ctxTab;
        tabControl.Dock = DockStyle.Fill;
        tabControl.Location = new Point(0, 76);
        tabControl.Name = "tabControl";
        tabControl.Padding = new Point(12, 6);
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(1280, 603);
        tabControl.TabIndex = 1;
        // 
        // statusStrip
        // 
        statusStrip.ImageScalingSize = new Size(28, 28);
        statusStrip.Items.AddRange(new ToolStripItem[] { lblConnInfo, lblStatus, lblElapsed, lblRows });
        statusStrip.Location = new Point(0, 679);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1280, 41);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 4;
        // 
        // lblConnInfo
        // 
        lblConnInfo.Name = "lblConnInfo";
        lblConnInfo.Size = new Size(1051, 32);
        lblConnInfo.Spring = true;
        lblConnInfo.Text = "连接: -";
        lblConnInfo.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblStatus
        // 
        lblStatus.BorderSides = ToolStripStatusLabelBorderSides.Left;
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(58, 32);
        lblStatus.Text = "就绪";
        // 
        // lblElapsed
        // 
        lblElapsed.BorderSides = ToolStripStatusLabelBorderSides.Left;
        lblElapsed.Name = "lblElapsed";
        lblElapsed.Size = new Size(78, 32);
        lblElapsed.Text = "耗时: -";
        // 
        // lblRows
        // 
        lblRows.BorderSides = ToolStripStatusLabelBorderSides.Left;
        lblRows.Name = "lblRows";
        lblRows.Size = new Size(78, 32);
        lblRows.Text = "影响: -";
        // 
        // SqlQueryForm
        // 
        ClientSize = new Size(1280, 720);
        Controls.Add(tabControl);
        Controls.Add(pnlToolBar);
        Controls.Add(pnlTop);
        Controls.Add(statusStrip);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        MinimumSize = new Size(900, 500);
        Name = "SqlQueryForm";
        StartPosition = FormStartPosition.CenterScreen;
        ctxTab.ResumeLayout(false);
        pnlTop.ResumeLayout(false);
        pnlTop.PerformLayout();
        pnlToolBar.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private ToolStripMenuItem miClose;
    private ToolStripMenuItem miCloseOthers;
    private ToolStripMenuItem miRename;
}