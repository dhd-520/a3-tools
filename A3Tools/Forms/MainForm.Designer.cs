namespace A3Tools.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // 控件字段声明
    private NotifyIcon notifyIcon = null!;
    private ContextMenuStrip trayContextMenu = null!;
    private ToolStripMenuItem menuShow = null!;
    private ToolStripMenuItem menuHide = null!;
    private ToolStripMenuItem menuTrayExit = null!;
    private Panel titleBar = null!;
    private Label lblTitle = null!;
    private Label lblVersion = null!;
    private MenuStrip menuStrip = null!;
    private ToolStripMenuItem menuFile = null!;
    private ToolStripMenuItem menuHelp = null!;
    private ToolStripMenuItem menuHotkeySettings = null!;
    private ToolStripMenuItem menuExit = null!;
    private ToolStripMenuItem menuAbout = null!;
    private ToolStripMenuItem menuCopyAccount = null!;
    private ContextMenuStrip addMenu = null!;
    private ToolStripMenuItem miManualAdd = null!;
    private ToolStripMenuItem miQuickAdd = null!;
    private TabControl tabControl = null!;
    private TabPage tabLaunch = null!;
    private TabPage tabTools = null!;
    private TabPage tabStatus = null!;
    private Panel searchPanel = null!;
    private Label lblSearch = null!;
    private TextBox txtSearch = null!;
    private FlowLayoutPanel buttonRow = null!;
    private Button btnAdd = null!;
    private Button btnEdit = null!;
    private Button btnDelete = null!;
    private Button btnLaunch = null!;
    private Button btnSettings = null!;
    private Button btnImport = null!;
    private Button btnConnectDB = null!;
    private Button btnRemote = null!;
    private Button btnRefresh = null!;
    private DataGridView dgvAccounts = null!;
    private Panel descPanel = null!;
    private Label lblDesc = null!;
    private Label lblPluginStatus = null!;
    private Label lblToolsSourceDb = null!;
    private Button btnToolsSelectSourceDb = null!;
    private Label lblToolsSourceDbName = null!;
    private Button btnToolsClearSourceDb = null!;
    private Label lblToolsTargetDb = null!;
    private Button btnToolsSelectTargetDb = null!;
    private Label lblToolsTargetDbName = null!;
    private Button btnToolsClearTargetDb = null!;
    private Panel scrollPanel = null!;
    private FlowLayoutPanel flpTools = null!;
    private DataGridView dgvStatus = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _edgeDockManager?.Dispose();
            _hotkeyManager?.Dispose();
            notifyIcon?.Dispose();
            trayContextMenu?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        titleBar = new Panel();
        lblVersion = new Label();
        lblTitle = new Label();
        menuStrip = new MenuStrip();
        menuFile = new ToolStripMenuItem();
        menuCopyAccount = new ToolStripMenuItem();
        menuHotkeySettings = new ToolStripMenuItem();
        menuExit = new ToolStripMenuItem();
        menuHelp = new ToolStripMenuItem();
        menuAbout = new ToolStripMenuItem();
        tabControl = new TabControl();
        tabLaunch = new TabPage();
        tabTools = new TabPage();
        tabStatus = new TabPage();
        trayContextMenu = new ContextMenuStrip(components);
        menuHide = new ToolStripMenuItem();
        menuTrayExit = new ToolStripMenuItem();
        menuShow = new ToolStripMenuItem();
        notifyIcon = new NotifyIcon(components);
        addMenu = new ContextMenuStrip(components);
        miManualAdd = new ToolStripMenuItem();
        miQuickAdd = new ToolStripMenuItem();
        titleBar.SuspendLayout();
        menuStrip.SuspendLayout();
        tabControl.SuspendLayout();
        trayContextMenu.SuspendLayout();
        addMenu.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblVersion);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 36);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(1100, 55);
        titleBar.TabIndex = 1;
        // 
        // lblVersion
        // 
        lblVersion.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblVersion.AutoSize = true;
        lblVersion.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblVersion.ForeColor = Color.FromArgb(200, 230, 240);
        lblVersion.Location = new Point(1019, 24);
        lblVersion.Name = "lblVersion";
        lblVersion.Size = new Size(69, 28);
        lblVersion.TabIndex = 0;
        lblVersion.Text = "v2.0.4";
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Cursor = Cursors.Hand;
        lblTitle.Font = new Font("微软雅黑", 16F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(15, 10);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(188, 50);
        lblTitle.TabIndex = 1;
        lblTitle.Text = "A3工具箱";
        // 
        // menuStrip
        // 
        menuStrip.ImageScalingSize = new Size(28, 28);
        menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuHelp });
        menuStrip.Location = new Point(0, 0);
        menuStrip.Name = "menuStrip";
        menuStrip.Size = new Size(1100, 36);
        menuStrip.TabIndex = 2;
        menuStrip.Text = "menuStrip1";
        // 
        // menuFile
        // 
        menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuCopyAccount, menuHotkeySettings, menuExit });
        menuFile.Name = "menuFile";
        menuFile.Size = new Size(106, 32);
        menuFile.Text = "文件(_F)";
        // 
        // menuCopyAccount
        // 
        menuCopyAccount.Name = "menuCopyAccount";
        menuCopyAccount.Size = new Size(270, 40);
        menuCopyAccount.Text = "复制账套信息";
        // 
        // menuHotkeySettings
        // 
        menuHotkeySettings.Name = "menuHotkeySettings";
        menuHotkeySettings.Size = new Size(270, 40);
        menuHotkeySettings.Text = "快捷键设置(_K)";
        // 
        // menuExit
        // 
        menuExit.Name = "menuExit";
        menuExit.Size = new Size(270, 40);
        menuExit.Text = "退出(_X)";
        // 
        // menuHelp
        // 
        menuHelp.DropDownItems.AddRange(new ToolStripItem[] { menuAbout });
        menuHelp.Name = "menuHelp";
        menuHelp.Size = new Size(111, 32);
        menuHelp.Text = "帮助(_H)";
        // 
        // menuAbout
        // 
        menuAbout.Name = "menuAbout";
        menuAbout.Size = new Size(171, 40);
        menuAbout.Text = "关于";
        // 
        // tabControl
        // 
        tabControl.Controls.Add(tabLaunch);
        tabControl.Controls.Add(tabTools);
        tabControl.Controls.Add(tabStatus);
        tabControl.Dock = DockStyle.Fill;
        tabControl.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        tabControl.ForeColor = Color.FromArgb(60, 60, 60);
        tabControl.HotTrack = true;
        tabControl.ItemSize = new Size(150, 40);
        tabControl.Location = new Point(0, 91);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(1100, 609);
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.TabIndex = 0;
        // 
        // tabLaunch
        // 
        tabLaunch.BackColor = Color.White;
        tabLaunch.Location = new Point(4, 44);
        tabLaunch.Name = "tabLaunch";
        tabLaunch.Size = new Size(1092, 561);
        tabLaunch.TabIndex = 0;
        tabLaunch.Text = "  A3程序启动  ";
        // 
        // tabTools
        // 
        tabTools.BackColor = Color.White;
        tabTools.Location = new Point(4, 44);
        tabTools.Name = "tabTools";
        tabTools.Size = new Size(1092, 561);
        tabTools.TabIndex = 1;
        tabTools.Text = "  工具箱  ";
        // 
        // tabStatus
        // 
        tabStatus.BackColor = Color.White;
        tabStatus.Location = new Point(4, 44);
        tabStatus.Name = "tabStatus";
        tabStatus.Size = new Size(1092, 561);
        tabStatus.TabIndex = 2;
        tabStatus.Text = "  账套运行情况  ";
        // 
        // trayContextMenu
        // 
        trayContextMenu.ImageScalingSize = new Size(28, 28);
        trayContextMenu.Items.AddRange(new ToolStripItem[] { menuHide, menuTrayExit });
        trayContextMenu.Name = "trayContextMenu";
        trayContextMenu.Size = new Size(190, 72);
        // 
        // menuHide
        // 
        menuHide.Name = "menuHide";
        menuHide.Size = new Size(189, 34);
        menuHide.Text = "隐藏到托盘";
        // 
        // menuTrayExit
        // 
        menuTrayExit.Name = "menuTrayExit";
        menuTrayExit.Size = new Size(189, 34);
        menuTrayExit.Text = "退出";
        // 
        // menuShow
        // 
        menuShow.Name = "menuShow";
        menuShow.Size = new Size(189, 34);
        menuShow.Text = "显示主窗体";
        // 
        // notifyIcon
        // 
        notifyIcon.ContextMenuStrip = trayContextMenu;
        notifyIcon.Text = "A3工具箱";
        // 
        // addMenu
        // 
        addMenu.ImageScalingSize = new Size(28, 28);
        addMenu.Items.AddRange(new ToolStripItem[] { miManualAdd, miQuickAdd });
        addMenu.Name = "addMenu";
        addMenu.Size = new Size(169, 72);
        // 
        // miManualAdd
        // 
        miManualAdd.Name = "miManualAdd";
        miManualAdd.Size = new Size(168, 34);
        miManualAdd.Text = "手动添加";
        miManualAdd.Click += MiManualAdd_Click;
        // 
        // miQuickAdd
        // 
        miQuickAdd.Name = "miQuickAdd";
        miQuickAdd.Size = new Size(168, 34);
        miQuickAdd.Text = "一键添加";
        miQuickAdd.Click += MiQuickAdd_Click;
        // 
        // MainForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(245, 245, 245);
        ClientSize = new Size(1100, 700);
        Controls.Add(tabControl);
        Controls.Add(titleBar);
        Controls.Add(menuStrip);
        KeyPreview = true;
        MainMenuStrip = menuStrip;
        MinimumSize = new Size(900, 600);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "A3工具箱";
        titleBar.ResumeLayout(false);
        titleBar.PerformLayout();
        menuStrip.ResumeLayout(false);
        menuStrip.PerformLayout();
        tabControl.ResumeLayout(false);
        trayContextMenu.ResumeLayout(false);
        addMenu.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private void InitLaunchTabControls()
    {
        // --- 搜索栏（TableLayoutPanel 水平布局）---
        searchPanel = new Panel();
        searchPanel.SuspendLayout();
        searchPanel.Height = 55;
        searchPanel.Dock = System.Windows.Forms.DockStyle.Top;
        searchPanel.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
        searchPanel.Name = "searchPanel";

        var searchLayout = new TableLayoutPanel();
        searchLayout.SuspendLayout();
        searchLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        searchLayout.ColumnCount = 2;
        searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        searchLayout.Padding = new Padding(15, 10, 15, 10);

        lblSearch = new Label();
        lblSearch.Text = "搜索：";
        lblSearch.Font = new Font("微软雅黑", 10F);
        lblSearch.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        lblSearch.Dock = System.Windows.Forms.DockStyle.Fill;
        lblSearch.Name = "lblSearch";

        txtSearch = new TextBox();
        txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
        txtSearch.Font = new Font("微软雅黑", 10F);
        txtSearch.PlaceholderText = "输入代码、名称、地址等进行搜索...";
        txtSearch.Name = "txtSearch";

        searchLayout.Controls.Add(lblSearch, 0, 0);
        searchLayout.Controls.Add(txtSearch, 1, 0);
        searchPanel.Controls.Add(searchLayout);
        searchLayout.ResumeLayout(false);
        tabLaunch.Controls.Add(searchPanel);
        searchPanel.ResumeLayout(false);

        // --- 按钮行 ---
        buttonRow = new FlowLayoutPanel();
        buttonRow.SuspendLayout();
        buttonRow.Dock = System.Windows.Forms.DockStyle.Top;
        buttonRow.BackColor = System.Drawing.Color.White;
        buttonRow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        buttonRow.WrapContents = false;
        buttonRow.Padding = new Padding(15, 10, 15, 0);
        buttonRow.AutoSize = false;
        buttonRow.Size = new Size(1000, 60);
        buttonRow.Name = "buttonRow";

        // 按钮高度 34*1.2 = 41
        int btnHeight = 41;

        btnAdd = new Button();
        btnAdd.Text = "➕ 新增 ▾";
        btnAdd.Size = new Size(110, btnHeight);
        btnAdd.ContextMenuStrip = addMenu;
        btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnAdd.FlatAppearance.BorderSize = 1;
        btnAdd.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnAdd.BackColor = System.Drawing.Color.White;
        btnAdd.Font = new Font("微软雅黑", 10F);
        btnAdd.Cursor = System.Windows.Forms.Cursors.Hand;
        btnAdd.Name = "btnAdd";
        btnAdd.Margin = new Padding(0, 0, 10, 0);

        btnImport = new Button();
        btnImport.Text = "📥 导入";
        btnImport.Size = new Size(110, btnHeight);
        btnImport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnImport.FlatAppearance.BorderSize = 1;
        btnImport.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnImport.BackColor = System.Drawing.Color.White;
        btnImport.Font = new Font("微软雅黑", 10F);
        btnImport.Cursor = System.Windows.Forms.Cursors.Hand;
        btnImport.Name = "btnImport";
        btnImport.Margin = new Padding(0, 0, 10, 0);

        btnEdit = new Button();
        btnEdit.Text = "✏️ 编辑";
        btnEdit.Size = new Size(110, btnHeight);
        btnEdit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnEdit.FlatAppearance.BorderSize = 1;
        btnEdit.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnEdit.BackColor = System.Drawing.Color.White;
        btnEdit.Font = new Font("微软雅黑", 10F);
        btnEdit.Cursor = System.Windows.Forms.Cursors.Hand;
        btnEdit.Name = "btnEdit";
        btnEdit.Margin = new Padding(0, 0, 10, 0);

        btnDelete = new Button();
        btnDelete.Text = "🗑️ 删除";
        btnDelete.Size = new Size(110, btnHeight);
        btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnDelete.FlatAppearance.BorderSize = 1;
        btnDelete.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnDelete.BackColor = System.Drawing.Color.White;
        btnDelete.Font = new Font("微软雅黑", 10F);
        btnDelete.Cursor = System.Windows.Forms.Cursors.Hand;
        btnDelete.Name = "btnDelete";
        btnDelete.Margin = new Padding(0, 0, 10, 0);

        btnLaunch = new Button();
        btnLaunch.Text = "🚀 启动";
        btnLaunch.Size = new Size(110, btnHeight);
        btnLaunch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnLaunch.FlatAppearance.BorderSize = 0;
        btnLaunch.BackColor = System.Drawing.Color.FromArgb(24, 145, 176);
        btnLaunch.ForeColor = System.Drawing.Color.White;
        btnLaunch.Font = new Font("微软雅黑", 10F);
        btnLaunch.Cursor = System.Windows.Forms.Cursors.Hand;
        btnLaunch.Name = "btnLaunch";
        btnLaunch.Margin = new Padding(0, 0, 10, 0);

        btnSettings = new Button();
        btnSettings.Text = "⚙️ 设置";
        btnSettings.Size = new Size(110, btnHeight);
        btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnSettings.FlatAppearance.BorderSize = 1;
        btnSettings.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnSettings.BackColor = System.Drawing.Color.White;
        btnSettings.Font = new Font("微软雅黑", 10F);
        btnSettings.Cursor = System.Windows.Forms.Cursors.Hand;
        btnSettings.Name = "btnSettings";
        btnSettings.Margin = new Padding(0, 0, 10, 0);

        btnConnectDB = new Button();
        btnConnectDB.Text = "🔗 链接数据库";
        btnConnectDB.Size = new Size(110, btnHeight);
        btnConnectDB.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnConnectDB.FlatAppearance.BorderSize = 1;
        btnConnectDB.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnConnectDB.BackColor = System.Drawing.Color.White;
        btnConnectDB.Font = new Font("微软雅黑", 10F);
        btnConnectDB.Cursor = System.Windows.Forms.Cursors.Hand;
        btnConnectDB.Name = "btnConnectDB";
        btnConnectDB.Margin = new Padding(0, 0, 10, 0);

        btnRemote = new Button();
        btnRemote.Text = "🖥️ 远程";
        btnRemote.Size = new Size(110, btnHeight);
        btnRemote.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnRemote.FlatAppearance.BorderSize = 1;
        btnRemote.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnRemote.BackColor = System.Drawing.Color.White;
        btnRemote.Font = new Font("微软雅黑", 10F);
        btnRemote.Cursor = System.Windows.Forms.Cursors.Hand;
        btnRemote.Name = "btnRemote";
        btnRemote.Margin = new Padding(0, 0, 10, 0);

        btnRefresh = new Button();
        btnRefresh.Text = "🔄 刷新";
        btnRefresh.Size = new Size(100, btnHeight);
        btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnRefresh.FlatAppearance.BorderSize = 1;
        btnRefresh.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnRefresh.BackColor = System.Drawing.Color.White;
        btnRefresh.Font = new Font("微软雅黑", 10F);
        btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Margin = new Padding(0, 0, 0, 0);

        buttonRow.Controls.Add(btnAdd);
        buttonRow.Controls.Add(btnImport);
        buttonRow.Controls.Add(btnEdit);
        buttonRow.Controls.Add(btnDelete);
        buttonRow.Controls.Add(btnLaunch);
        buttonRow.Controls.Add(btnSettings);
        buttonRow.Controls.Add(btnConnectDB);
        buttonRow.Controls.Add(btnRemote);
        buttonRow.Controls.Add(btnRefresh);
        tabLaunch.Controls.Add(buttonRow);
        buttonRow.ResumeLayout(false);

        // --- DataGridView（Dock.Fill 填满剩余空间）---
        dgvAccounts = new DataGridView();
        ((System.ComponentModel.ISupportInitialize)dgvAccounts).BeginInit();
        dgvAccounts.SuspendLayout();
        dgvAccounts.Dock = System.Windows.Forms.DockStyle.Fill;
        dgvAccounts.BackgroundColor = System.Drawing.Color.White;
        dgvAccounts.BorderStyle = System.Windows.Forms.BorderStyle.None;
        dgvAccounts.RowHeadersVisible = false;
        dgvAccounts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        dgvAccounts.MultiSelect = false;
        dgvAccounts.ReadOnly = true;
        dgvAccounts.AllowUserToAddRows = false;
        dgvAccounts.AllowUserToDeleteRows = false;
        dgvAccounts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvAccounts.ColumnHeadersHeight = 36;
        dgvAccounts.RowTemplate.Height = 32;
        dgvAccounts.Name = "dgvAccounts";
        dgvAccounts.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
        dgvAccounts.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);
        dgvAccounts.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        dgvAccounts.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
        dgvAccounts.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
        dgvAccounts.EnableHeadersVisualStyles = false;
        dgvAccounts.Font = new Font("微软雅黑", 9F);

        tabLaunch.Controls.Add(dgvAccounts);
        dgvAccounts.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvAccounts).EndInit();

        this.ResumeLayout(false);
    }

    private void InitToolsTabControls()
    {
        // --- 说明面板 ---
        descPanel = new Panel();
        descPanel.SuspendLayout();
        descPanel.Height = 96;
        descPanel.Dock = System.Windows.Forms.DockStyle.Top;
        descPanel.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
        descPanel.Name = "descPanel";

        lblDesc = new Label();
        lblDesc.Text = "选择工具开始操作";
        lblDesc.Location = new Point(20, 12);
        lblDesc.Font = new Font("微软雅黑", 10F);
        lblDesc.ForeColor = System.Drawing.Color.FromArgb(102, 109, 118);
        lblDesc.AutoSize = true;
        lblDesc.Name = "lblDesc";

        lblPluginStatus = new Label();
        lblPluginStatus.Text = "已加载 0 个工具";
        lblPluginStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblPluginStatus.Location = new Point(940, 12);
        lblPluginStatus.Font = new Font("微软雅黑", 9F);
        lblPluginStatus.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
        lblPluginStatus.AutoSize = true;
        lblPluginStatus.Name = "lblPluginStatus";

        lblToolsSourceDb = new Label();
        lblToolsSourceDb.Text = "源数据库：";
        lblToolsSourceDb.Location = new Point(20, 52);
        lblToolsSourceDb.Size = new Size(85, 30);
        lblToolsSourceDb.TextAlign = ContentAlignment.MiddleRight;
        lblToolsSourceDb.Font = new Font("微软雅黑", 9F);
        lblToolsSourceDb.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        lblToolsSourceDb.Name = "lblToolsSourceDb";

        btnToolsSelectSourceDb = new Button();
        btnToolsSelectSourceDb.Text = "选择源账套…";
        btnToolsSelectSourceDb.Location = new Point(110, 51);
        btnToolsSelectSourceDb.Size = new Size(110, 32);
        btnToolsSelectSourceDb.FlatStyle = FlatStyle.Flat;
        btnToolsSelectSourceDb.BackColor = Color.FromArgb(24, 145, 176);
        btnToolsSelectSourceDb.ForeColor = Color.White;
        btnToolsSelectSourceDb.FlatAppearance.BorderSize = 0;
        btnToolsSelectSourceDb.Font = new Font("微软雅黑", 9F);
        btnToolsSelectSourceDb.Cursor = Cursors.Hand;
        btnToolsSelectSourceDb.Name = "btnToolsSelectSourceDb";

        lblToolsSourceDbName = new Label();
        lblToolsSourceDbName.Text = "（未选择）";
        lblToolsSourceDbName.Location = new Point(228, 52);
        lblToolsSourceDbName.Size = new Size(200, 30);
        lblToolsSourceDbName.TextAlign = ContentAlignment.MiddleLeft;
        lblToolsSourceDbName.Font = new Font("微软雅黑", 9F);
        lblToolsSourceDbName.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);
        lblToolsSourceDbName.Name = "lblToolsSourceDbName";

        btnToolsClearSourceDb = new Button();
        btnToolsClearSourceDb.Text = "清空";
        btnToolsClearSourceDb.Location = new Point(434, 51);
        btnToolsClearSourceDb.Size = new Size(68, 32);
        btnToolsClearSourceDb.FlatStyle = FlatStyle.Flat;
        btnToolsClearSourceDb.BackColor = Color.White;
        btnToolsClearSourceDb.Font = new Font("微软雅黑", 8F);
        btnToolsClearSourceDb.Cursor = Cursors.Hand;
        btnToolsClearSourceDb.Name = "btnToolsClearSourceDb";

        lblToolsTargetDb = new Label();
        lblToolsTargetDb.Text = "目标数据库：";
        lblToolsTargetDb.Location = new Point(516, 52);
        lblToolsTargetDb.Size = new Size(95, 30);
        lblToolsTargetDb.TextAlign = ContentAlignment.MiddleRight;
        lblToolsTargetDb.Font = new Font("微软雅黑", 9F);
        lblToolsTargetDb.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        lblToolsTargetDb.Name = "lblToolsTargetDb";

        btnToolsSelectTargetDb = new Button();
        btnToolsSelectTargetDb.Text = "选择目标账套…";
        btnToolsSelectTargetDb.Location = new Point(616, 51);
        btnToolsSelectTargetDb.Size = new Size(120, 32);
        btnToolsSelectTargetDb.FlatStyle = FlatStyle.Flat;
        btnToolsSelectTargetDb.BackColor = Color.FromArgb(228, 94, 29);
        btnToolsSelectTargetDb.ForeColor = Color.White;
        btnToolsSelectTargetDb.FlatAppearance.BorderSize = 0;
        btnToolsSelectTargetDb.Font = new Font("微软雅黑", 9F);
        btnToolsSelectTargetDb.Cursor = Cursors.Hand;
        btnToolsSelectTargetDb.Name = "btnToolsSelectTargetDb";

        lblToolsTargetDbName = new Label();
        lblToolsTargetDbName.Text = "（未选择）";
        lblToolsTargetDbName.Location = new Point(744, 52);
        lblToolsTargetDbName.Size = new Size(200, 30);
        lblToolsTargetDbName.TextAlign = ContentAlignment.MiddleLeft;
        lblToolsTargetDbName.Font = new Font("微软雅黑", 9F);
        lblToolsTargetDbName.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);
        lblToolsTargetDbName.Name = "lblToolsTargetDbName";

        btnToolsClearTargetDb = new Button();
        btnToolsClearTargetDb.Text = "清空";
        btnToolsClearTargetDb.Location = new Point(950, 51);
        btnToolsClearTargetDb.Size = new Size(68, 32);
        btnToolsClearTargetDb.FlatStyle = FlatStyle.Flat;
        btnToolsClearTargetDb.BackColor = Color.White;
        btnToolsClearTargetDb.Font = new Font("微软雅黑", 8F);
        btnToolsClearTargetDb.Cursor = Cursors.Hand;
        btnToolsClearTargetDb.Name = "btnToolsClearTargetDb";

        descPanel.Controls.Add(lblDesc);
        descPanel.Controls.Add(lblPluginStatus);
        descPanel.Controls.Add(lblToolsSourceDb);
        descPanel.Controls.Add(btnToolsSelectSourceDb);
        descPanel.Controls.Add(lblToolsSourceDbName);
        descPanel.Controls.Add(btnToolsClearSourceDb);
        descPanel.Controls.Add(lblToolsTargetDb);
        descPanel.Controls.Add(btnToolsSelectTargetDb);
        descPanel.Controls.Add(lblToolsTargetDbName);
        descPanel.Controls.Add(btnToolsClearTargetDb);

        // --- 滚动容器（只竖向滚动）---
        var toolsScrollPanel = new Panel();
        toolsScrollPanel.SuspendLayout();
        toolsScrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        toolsScrollPanel.AutoScroll = true;
        toolsScrollPanel.HorizontalScroll.Enabled = false;
        toolsScrollPanel.HorizontalScroll.Visible = false;
        toolsScrollPanel.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        toolsScrollPanel.Name = "toolsScrollPanel";

        // FlowLayoutPanel 工具卡片（左到右自动排列，换行）
        flpTools = new FlowLayoutPanel();
        flpTools.SuspendLayout();
        flpTools.Location = new Point(0, 0);
        flpTools.Size = new Size(tabTools.Width - 20, 500);
        flpTools.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        flpTools.WrapContents = true;
        flpTools.AutoScroll = false;
        flpTools.Padding = new Padding(20, 60, 20, 20);
        flpTools.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        flpTools.Name = "flpTools";

        // Tab页大小变化时调整FlowLayoutPanel宽度
        tabTools.SizeChanged += (s, e) =>
        {
            flpTools.Width = tabTools.Width - 20;
            UpdateToolsHeaderLayout();
        };

        toolsScrollPanel.Controls.Add(flpTools);
        tabTools.Controls.Add(toolsScrollPanel);
        tabTools.Controls.Add(descPanel);

        flpTools.ResumeLayout(false);
        toolsScrollPanel.ResumeLayout(false);
        descPanel.ResumeLayout(false);
    }

    private void InitStatusTabControls()
    {
        // --- 状态Tab页 --- 
        var headerPanel = new Panel();
        headerPanel.SuspendLayout();
        headerPanel.Height = 55;
        headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
        headerPanel.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);

        var lblStatusTitle = new Label();
        lblStatusTitle.Text = "实时监控所有账套的运行状态";
        lblStatusTitle.Font = new Font("微软雅黑", 10F);
        lblStatusTitle.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        lblStatusTitle.Location = new Point(20, 18);
        lblStatusTitle.AutoSize = true;

        headerPanel.Controls.Add(lblStatusTitle);

        // --- DataGridView ---
        dgvStatus = new DataGridView();
        dgvStatus.SuspendLayout();
        dgvStatus.Dock = System.Windows.Forms.DockStyle.Fill;
        dgvStatus.Name = "dgvStatus";
        dgvStatus.BackgroundColor = System.Drawing.Color.White;
        dgvStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
        dgvStatus.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvStatus.ColumnHeadersHeight = 40;
        dgvStatus.RowHeadersVisible = false;
        dgvStatus.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        dgvStatus.MultiSelect = false;
        dgvStatus.AllowUserToAddRows = false;
        dgvStatus.AllowUserToDeleteRows = false;
        dgvStatus.ReadOnly = true;
        dgvStatus.AutoGenerateColumns = false;
        dgvStatus.Font = new Font("微软雅黑", 10F);
        dgvStatus.RowTemplate.Height = 36;
        dgvStatus.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
        dgvStatus.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        dgvStatus.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);
        dgvStatus.EnableHeadersVisualStyles = false;

        tabStatus.Controls.Add(dgvStatus);
        tabStatus.Controls.Add(headerPanel);
        headerPanel.ResumeLayout(false);
        dgvStatus.ResumeLayout(false);
    }
}
