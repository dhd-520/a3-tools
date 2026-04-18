namespace A3Tools.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // 控件字段声明
    private Panel titleBar = null!;
    private Label lblTitle = null!;
    private Label lblVersion = null!;
    private TabControl tabControl = null!;
    private TabPage tabLaunch = null!;
    private TabPage tabTools = null!;
    private Panel searchPanel = null!;
    private Label lblSearch = null!;
    private TextBox txtSearch = null!;
    private FlowLayoutPanel buttonRow = null!;
    private Button btnAdd = null!;
    private Button btnEdit = null!;
    private Button btnDelete = null!;
    private Button btnRefresh = null!;
    private Button btnLaunch = null!;
    private Button btnSettings = null!;
    private Button btnClose = null!;
    private Button btnImport = null!;
    private Button btnConnectDB = null!;
    private Button btnRemote = null!;
    private DataGridView dgvAccounts = null!;
    private Panel descPanel = null!;
    private Label lblDesc = null!;
    private Label lblPluginStatus = null!;
    private Panel scrollPanel = null!;
    private FlowLayoutPanel flpTools = null!;

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
        titleBar = new Panel();
        lblVersion = new Label();
        lblTitle = new Label();
        tabControl = new TabControl();
        tabLaunch = new TabPage();
        tabTools = new TabPage();
        titleBar.SuspendLayout();
        tabControl.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblVersion);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 0);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(1100, 55);
        titleBar.TabIndex = 1;
        // 
        // lblVersion
        // 
        lblVersion.AutoSize = true;
        lblVersion.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblVersion.ForeColor = Color.FromArgb(200, 230, 240);
        lblVersion.Location = new Point(1019, 23);
        lblVersion.Name = "lblVersion";
        lblVersion.Size = new Size(69, 28);
        lblVersion.TabIndex = 0;
        lblVersion.Text = "v1.0.0";
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("微软雅黑", 16F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(4, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(188, 50);
        lblTitle.TabIndex = 1;
        lblTitle.Text = "A3工具箱";
        // 
        // tabControl
        // 
        tabControl.Controls.Add(tabLaunch);
        tabControl.Controls.Add(tabTools);
        tabControl.Dock = DockStyle.Fill;
        tabControl.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        tabControl.ForeColor = Color.FromArgb(60, 60, 60);
        tabControl.HotTrack = true;
        tabControl.ItemSize = new Size(150, 40);
        tabControl.Location = new Point(0, 55);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(1100, 645);
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.TabIndex = 0;
        // 
        // tabLaunch
        // 
        tabLaunch.BackColor = Color.White;
        tabLaunch.Location = new Point(4, 44);
        tabLaunch.Name = "tabLaunch";
        tabLaunch.Size = new Size(1092, 597);
        tabLaunch.TabIndex = 0;
        tabLaunch.Text = "  A3程序启动  ";
        // 
        // tabTools
        // 
        tabTools.BackColor = Color.White;
        tabTools.Location = new Point(4, 44);
        tabTools.Name = "tabTools";
        tabTools.Size = new Size(1092, 597);
        tabTools.TabIndex = 1;
        tabTools.Text = "  工具箱  ";
        // 
        // MainForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(245, 245, 245);
        ClientSize = new Size(1100, 700);
        Controls.Add(tabControl);
        Controls.Add(titleBar);
        MinimumSize = new Size(900, 600);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "A3工具箱";
        titleBar.ResumeLayout(false);
        titleBar.PerformLayout();
        tabControl.ResumeLayout(false);
        ResumeLayout(false);

        // 初始化Tab页控件
        InitLaunchTabControls();
        InitToolsTabControls();
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
        buttonRow.Size = new System.Drawing.Size(1000, 60);
        buttonRow.Name = "buttonRow";

        // 按钮高度 34*1.2 = 41
        int btnHeight = 41;

        btnAdd = new Button();
        btnAdd.Text = "➕ 新增";
        btnAdd.Size = new Size(110, btnHeight);
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

        btnRefresh = new Button();
        btnRefresh.Text = "🔄 刷新";
        btnRefresh.Size = new Size(110, btnHeight);
        btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnRefresh.FlatAppearance.BorderSize = 1;
        btnRefresh.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnRefresh.BackColor = System.Drawing.Color.White;
        btnRefresh.Font = new Font("微软雅黑", 10F);
        btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Margin = new Padding(0, 0, 10, 0);

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

        btnClose = new Button();
        btnClose.Text = "❌ 关闭";
        btnClose.Size = new Size(110, btnHeight);
        btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.BackColor = System.Drawing.Color.FromArgb(220, 53, 69);
        btnClose.ForeColor = System.Drawing.Color.White;
        btnClose.Font = new Font("微软雅黑", 10F);
        btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
        btnClose.Name = "btnClose";
        btnClose.Margin = new Padding(0, 0, 0, 0);

        buttonRow.Controls.Add(btnAdd);
        buttonRow.Controls.Add(btnImport);
        buttonRow.Controls.Add(btnEdit);
        buttonRow.Controls.Add(btnDelete);
        buttonRow.Controls.Add(btnRefresh);
        buttonRow.Controls.Add(btnLaunch);
        buttonRow.Controls.Add(btnSettings);
        buttonRow.Controls.Add(btnConnectDB);
        buttonRow.Controls.Add(btnRemote);
        buttonRow.Controls.Add(btnClose);
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
    }

    private void InitToolsTabControls()
    {
        // --- 说明面板 ---
        descPanel = new Panel();
        descPanel.SuspendLayout();
        descPanel.Height = 50;
        descPanel.Dock = System.Windows.Forms.DockStyle.Top;
        descPanel.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
        descPanel.Name = "descPanel";

        lblDesc = new Label();
        lblDesc.Text = "选择工具开始操作（需先在【A3程序启动】中选择账套）";
        lblDesc.Location = new Point(20, 16);
        lblDesc.Font = new Font("微软雅黑", 10F);
        lblDesc.ForeColor = System.Drawing.Color.FromArgb(102, 109, 118);
        lblDesc.AutoSize = true;
        lblDesc.Name = "lblDesc";

        lblPluginStatus = new Label();
        lblPluginStatus.Text = "已加载 0 个工具";
        lblPluginStatus.Location = new Point(500, 16);
        lblPluginStatus.Font = new Font("微软雅黑", 9F);
        lblPluginStatus.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
        lblPluginStatus.AutoSize = true;
        lblPluginStatus.Name = "lblPluginStatus";

        descPanel.Controls.Add(lblPluginStatus);
        descPanel.Controls.Add(lblDesc);

        // --- 滚动面板 ---
        scrollPanel = new Panel();
        scrollPanel.SuspendLayout();
        scrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        scrollPanel.AutoScroll = true;
        scrollPanel.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        scrollPanel.Name = "scrollPanel";

        // FlowLayoutPanel 工具卡片（TopDown 自动排列）
        flpTools = new FlowLayoutPanel();
        flpTools.SuspendLayout();
        flpTools.Dock = System.Windows.Forms.DockStyle.Top;
        flpTools.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        flpTools.WrapContents = true;
        flpTools.AutoScroll = false;
        flpTools.Padding = new Padding(20, 15, 20, 20);
        flpTools.BackColor = System.Drawing.Color.Transparent;
        flpTools.Name = "flpTools";

        scrollPanel.Controls.Add(flpTools);
        tabTools.Controls.Add(scrollPanel);
        tabTools.Controls.Add(descPanel);

        flpTools.ResumeLayout(false);
        scrollPanel.ResumeLayout(false);
        descPanel.ResumeLayout(false);
    }
}
