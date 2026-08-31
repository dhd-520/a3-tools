namespace A3Tools.Forms;

partial class AiChatForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        pnlHeader = new Panel();
        btnNewSession = new Button();
        btnSettings = new Button();
        cmbProvider = new ComboBox();
        lblTitle = new Label();
        pnlSidebar = new Panel();
        btnClearAll = new Button();
        btnDeleteSession = new Button();
        lstSessions = new ListBox();
        lblSidebarTitle = new Label();
        pnlChatArea = new Panel();
        splitChat = new SplitContainer();
        pnlMessagesScroll = new Panel();
        pnlInput = new Panel();
        lblStatus = new Label();
        btnCancel = new Button();
        btnSend = new Button();
        txtInput = new TextBox();
        pnlTopBar = new Panel();
        lblCurrentProvider = new Label();
        pnlHeader.SuspendLayout();
        pnlSidebar.SuspendLayout();
        pnlChatArea.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitChat).BeginInit();
        splitChat.Panel1.SuspendLayout();
        splitChat.Panel2.SuspendLayout();
        splitChat.SuspendLayout();
        pnlMessagesScroll.SuspendLayout();
        pnlInput.SuspendLayout();
        pnlTopBar.SuspendLayout();
        SuspendLayout();
        // 
        // pnlHeader
        // 
        pnlHeader.BackColor = Color.FromArgb(24, 144, 255);
        pnlHeader.Controls.Add(btnNewSession);
        pnlHeader.Controls.Add(btnSettings);
        pnlHeader.Controls.Add(cmbProvider);
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Location = new Point(0, 0);
        pnlHeader.Margin = new Padding(6);
        pnlHeader.Name = "pnlHeader";
        pnlHeader.Size = new Size(2006, 97);
        pnlHeader.TabIndex = 0;
        // 
        // btnNewSession
        // 
        btnNewSession.BackColor = Color.FromArgb(57, 181, 74);
        btnNewSession.FlatAppearance.BorderSize = 0;
        btnNewSession.FlatStyle = FlatStyle.Flat;
        btnNewSession.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnNewSession.ForeColor = Color.White;
        btnNewSession.Location = new Point(1709, 22);
        btnNewSession.Margin = new Padding(6);
        btnNewSession.Name = "btnNewSession";
        btnNewSession.Size = new Size(167, 52);
        btnNewSession.TabIndex = 3;
        btnNewSession.Text = "新会话";
        btnNewSession.UseVisualStyleBackColor = false;
        // 
        // btnSettings
        // 
        btnSettings.BackColor = Color.White;
        btnSettings.FlatAppearance.BorderColor = Color.White;
        btnSettings.FlatStyle = FlatStyle.Flat;
        btnSettings.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnSettings.ForeColor = Color.FromArgb(24, 144, 255);
        btnSettings.Location = new Point(1541, 22);
        btnSettings.Margin = new Padding(6);
        btnSettings.Name = "btnSettings";
        btnSettings.Size = new Size(149, 52);
        btnSettings.TabIndex = 2;
        btnSettings.Text = "设置";
        btnSettings.UseVisualStyleBackColor = false;
        // 
        // cmbProvider
        // 
        cmbProvider.BackColor = Color.White;
        cmbProvider.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProvider.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        cmbProvider.Location = new Point(1003, 22);
        cmbProvider.Margin = new Padding(6);
        cmbProvider.Name = "cmbProvider";
        cmbProvider.Size = new Size(517, 38);
        cmbProvider.TabIndex = 1;
        // 
        // lblTitle
        // 
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(30, 26);
        lblTitle.Margin = new Padding(6, 0, 6, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(121, 40);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "AI 助理";
        // 
        // pnlSidebar
        // 
        pnlSidebar.BackColor = Color.FromArgb(250, 250, 252);
        pnlSidebar.Controls.Add(lblStatus);
        pnlSidebar.Controls.Add(btnClearAll);
        pnlSidebar.Controls.Add(btnDeleteSession);
        pnlSidebar.Controls.Add(lstSessions);
        pnlSidebar.Controls.Add(lblSidebarTitle);
        pnlSidebar.Dock = DockStyle.Left;
        pnlSidebar.Location = new Point(0, 97);
        pnlSidebar.Margin = new Padding(6);
        pnlSidebar.Name = "pnlSidebar";
        pnlSidebar.Padding = new Padding(15);
        pnlSidebar.Size = new Size(409, 1172);
        pnlSidebar.TabIndex = 1;
        // 
        // btnClearAll
        // 
        btnClearAll.FlatStyle = FlatStyle.Flat;
        btnClearAll.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearAll.Location = new Point(212, 993);
        btnClearAll.Margin = new Padding(6);
        btnClearAll.Name = "btnClearAll";
        btnClearAll.Size = new Size(182, 52);
        btnClearAll.TabIndex = 3;
        btnClearAll.Text = "清空全部";
        btnClearAll.UseVisualStyleBackColor = true;
        // 
        // btnDeleteSession
        // 
        btnDeleteSession.BackColor = Color.FromArgb(251, 67, 42);
        btnDeleteSession.FlatAppearance.BorderSize = 0;
        btnDeleteSession.FlatStyle = FlatStyle.Flat;
        btnDeleteSession.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnDeleteSession.ForeColor = Color.White;
        btnDeleteSession.Location = new Point(15, 993);
        btnDeleteSession.Margin = new Padding(6);
        btnDeleteSession.Name = "btnDeleteSession";
        btnDeleteSession.Size = new Size(182, 52);
        btnDeleteSession.TabIndex = 2;
        btnDeleteSession.Text = "删除选中";
        btnDeleteSession.UseVisualStyleBackColor = false;
        // 
        // lstSessions
        // 
        lstSessions.BackColor = Color.FromArgb(250, 250, 252);
        lstSessions.BorderStyle = BorderStyle.None;
        lstSessions.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        lstSessions.IntegralHeight = false;
        lstSessions.ItemHeight = 30;
        lstSessions.Location = new Point(15, 82);
        lstSessions.Margin = new Padding(6);
        lstSessions.Name = "lstSessions";
        lstSessions.Size = new Size(379, 896);
        lstSessions.TabIndex = 1;
        // 
        // lblSidebarTitle
        // 
        lblSidebarTitle.AutoSize = true;
        lblSidebarTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblSidebarTitle.ForeColor = Color.FromArgb(60, 60, 60);
        lblSidebarTitle.Location = new Point(22, 22);
        lblSidebarTitle.Margin = new Padding(6, 0, 6, 0);
        lblSidebarTitle.Name = "lblSidebarTitle";
        lblSidebarTitle.Size = new Size(110, 31);
        lblSidebarTitle.TabIndex = 0;
        lblSidebarTitle.Text = "会话列表";
        // 
        // pnlChatArea
        // 
        pnlChatArea.Controls.Add(splitChat);
        pnlChatArea.Controls.Add(pnlTopBar);
        pnlChatArea.Dock = DockStyle.Fill;
        pnlChatArea.Location = new Point(409, 97);
        pnlChatArea.Margin = new Padding(6);
        pnlChatArea.Name = "pnlChatArea";
        pnlChatArea.Size = new Size(1597, 1172);
        pnlChatArea.TabIndex = 2;
        // 
        // splitChat
        // 
        splitChat.Dock = DockStyle.Fill;
        splitChat.FixedPanel = FixedPanel.Panel2;
        splitChat.Location = new Point(0, 60);
        splitChat.Margin = new Padding(0);
        splitChat.Name = "splitChat";
        splitChat.Orientation = Orientation.Horizontal;
        // 
        // splitChat.Panel1
        // 
        // ★ 2026-08-29 修复：去掉 AutoScroll，滚动由 pnlMessagesScroll 统一负责
        //   之前双层 AutoScroll（Panel1 + pnlMessagesScroll）互相干扰，导致气泡高度不撑开
        splitChat.Panel1.AutoScroll = false;
        splitChat.Panel1.BackColor = Color.White;
        splitChat.Panel1.Padding = new Padding(0);
        splitChat.Panel1.Controls.Add(pnlMessagesScroll);
        splitChat.Panel1MinSize = 0;
        // 
        // splitChat.Panel2
        // 
        splitChat.Panel2.Controls.Add(pnlInput);
        splitChat.Panel2MinSize = 0;
        splitChat.Size = new Size(1597, 1112);
        splitChat.SplitterDistance = 955;
        splitChat.SplitterWidth = 6;
        splitChat.TabIndex = 0;
        // 
        // pnlMessagesScroll
        // 
        pnlMessagesScroll.AutoScroll = true;
        pnlMessagesScroll.BackColor = Color.White;
        pnlMessagesScroll.Dock = DockStyle.Fill;
        pnlMessagesScroll.Location = new Point(0, 0);
        pnlMessagesScroll.Margin = new Padding(0);
        pnlMessagesScroll.Name = "pnlMessagesScroll";
        // ★ 2026-08-29 修复滚动条遮挡气泡：右侧加 17px 预留滚动条位置
        pnlMessagesScroll.Padding = new Padding(0, 0, 17, 0);
        pnlMessagesScroll.Size = new Size(1597, 955);
        pnlMessagesScroll.TabIndex = 0;
        // 
        // pnlMessages
        // 
        // ★ 2026-08-29 修复气泡高度不撑开（终极方案）：
        //   问题根因：FlowLayoutPanel AutoSize + Dock=Top 在子控件动态改 Height 时不可靠
        //   解决方案：不用 FlowLayoutPanel，直接用 Panel + 手动垂直排列 row
        //   - pnlMessagesScroll.AutoScroll=true 负责滚动
        //   - row 位置手动算（y += height + gap）
        //   - row 高度变化时调用 RelayoutMessages() 重算所有 row 位置
        //   - pnlMessagesScroll.AutoScrollMinSize 手动设置总高度
        // 
        flpMessages = new FlowLayoutPanel();
        flpMessages.AutoScroll = false;
        flpMessages.AutoSize = false;  // ★ 不再用 AutoSize，手动控制高度
        flpMessages.BackColor = Color.White;
        // ★ 关键修复：Dock=Top 会被 WinForms 布局引擎覆盖手动设的 Height！
        //   改为 Dock=None，完全手动管理 Location + Size
        flpMessages.Dock = DockStyle.None;
        flpMessages.FlowDirection = FlowDirection.TopDown;
        flpMessages.Location = new Point(0, 0);
        flpMessages.Margin = new Padding(0);
        flpMessages.Name = "flpMessages";
        flpMessages.Padding = new Padding(22, 14, 22 + 17, 4);
        flpMessages.Size = new Size(1597, 60);
        flpMessages.TabIndex = 0;
        flpMessages.WrapContents = false;
        pnlMessagesScroll.Controls.Add(flpMessages); 
        // pnlInput
        // 
        pnlInput.BackColor = Color.FromArgb(245, 245, 245);
        pnlInput.Controls.Add(btnCancel);
        pnlInput.Controls.Add(btnSend);
        pnlInput.Controls.Add(txtInput);
        pnlInput.Dock = DockStyle.Fill;
        pnlInput.Location = new Point(0, 0);
        pnlInput.Margin = new Padding(0);
        pnlInput.Name = "pnlInput";
        pnlInput.Padding = new Padding(22, 12, 22, 12);
        pnlInput.Size = new Size(1597, 151);
        pnlInput.TabIndex = 2;
        // 
        // lblStatus
        // 
        lblStatus.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        lblStatus.ForeColor = Color.Gray;
        lblStatus.Location = new Point(30, 1093);
        lblStatus.Margin = new Padding(6, 0, 6, 0);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(334, 41);
        lblStatus.TabIndex = 3;
        lblStatus.Text = "就绪";
        // 
        // btnCancel
        // 
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Location = new Point(1653, 15);
        btnCancel.Margin = new Padding(6);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(149, 52);
        btnCancel.TabIndex = 2;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Visible = false;
        // 
        // btnSend
        // 
        btnSend.BackColor = Color.FromArgb(24, 144, 255);
        btnSend.FlatAppearance.BorderSize = 0;
        btnSend.FlatStyle = FlatStyle.Flat;
        btnSend.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        btnSend.ForeColor = Color.White;
        btnSend.Location = new Point(1406, 13);
        btnSend.Margin = new Padding(6);
        btnSend.Name = "btnSend";
        btnSend.Size = new Size(178, 112);
        btnSend.TabIndex = 1;
        btnSend.Text = "发送";
        btnSend.UseVisualStyleBackColor = false;
        // 
        // txtInput
        // 
        txtInput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtInput.BorderStyle = BorderStyle.FixedSingle;
        txtInput.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtInput.Location = new Point(22, 15);
        txtInput.Margin = new Padding(0);
        txtInput.Multiline = true;
        txtInput.Name = "txtInput";
        txtInput.PlaceholderText = "输入消息，Enter 发送，Shift+Enter 换行...";
        txtInput.ScrollBars = ScrollBars.Vertical;
        txtInput.Size = new Size(1372, 118);
        txtInput.TabIndex = 0;
        // 
        // pnlTopBar
        // 
        pnlTopBar.BackColor = Color.FromArgb(245, 245, 245);
        pnlTopBar.Controls.Add(lblCurrentProvider);
        pnlTopBar.Dock = DockStyle.Top;
        pnlTopBar.Location = new Point(0, 0);
        pnlTopBar.Margin = new Padding(6);
        pnlTopBar.Name = "pnlTopBar";
        pnlTopBar.Size = new Size(1597, 60);
        pnlTopBar.TabIndex = 1;
        // 
        // lblCurrentProvider
        // 
        lblCurrentProvider.AutoSize = true;
        lblCurrentProvider.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblCurrentProvider.ForeColor = Color.FromArgb(100, 100, 100);
        lblCurrentProvider.Location = new Point(22, 15);
        lblCurrentProvider.Margin = new Padding(6, 0, 6, 0);
        lblCurrentProvider.Name = "lblCurrentProvider";
        lblCurrentProvider.Size = new Size(180, 28);
        lblCurrentProvider.TabIndex = 0;
        lblCurrentProvider.Text = "当前厂商：未配置";
        // 
        // AiChatForm
        // 
        AutoScaleDimensions = new SizeF(13F, 28F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(245, 247, 250);
        ClientSize = new Size(2006, 1269);
        Controls.Add(pnlChatArea);
        Controls.Add(pnlSidebar);
        Controls.Add(pnlHeader);
        Margin = new Padding(6);
        MinimumSize = new Size(1502, 953);
        Name = "AiChatForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AI 助理 - A3Tools 智能助手";
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        pnlSidebar.ResumeLayout(false);
        pnlSidebar.PerformLayout();
        pnlChatArea.ResumeLayout(false);
        splitChat.Panel1.ResumeLayout(false);
        splitChat.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitChat).EndInit();
        splitChat.ResumeLayout(false);
        pnlMessagesScroll.ResumeLayout(false);
        pnlMessagesScroll.PerformLayout();
        pnlInput.ResumeLayout(false);
        pnlInput.PerformLayout();
        pnlTopBar.ResumeLayout(false);
        pnlTopBar.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private Panel pnlHeader;
    private Label lblTitle;
    private ComboBox cmbProvider;
    private Button btnSettings;
    private Button btnNewSession;
    private Panel pnlSidebar;
    private Label lblSidebarTitle;
    private ListBox lstSessions;
    private Button btnDeleteSession;
    private Button btnClearAll;
    private Panel pnlChatArea;
    private SplitContainer splitChat;
    private Panel pnlTopBar;
    private Label lblCurrentProvider;
    private Panel pnlMessagesScroll;
    private FlowLayoutPanel flpMessages;
    private Panel pnlInput;
    private TextBox txtInput;
    private Button btnSend;
    private Button btnCancel;
    private Label lblStatus;
}
