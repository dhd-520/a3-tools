namespace A3Tools.Forms;

partial class AccountDialog
{
    private System.ComponentModel.IContainer components = null;

    // 控件字段
    private System.Windows.Forms.Panel titleBar = null!;
    private System.Windows.Forms.Label lblTitle = null!;
    private System.Windows.Forms.Panel contentPanel = null!;
    private System.Windows.Forms.Panel footerPanel = null!;
    private System.Windows.Forms.Panel sep1 = null!;
    private System.Windows.Forms.Panel sep2 = null!;

    private System.Windows.Forms.Label lblCode = null!;
    private System.Windows.Forms.TextBox txtCode = null!;
    private System.Windows.Forms.Label lblName = null!;
    private System.Windows.Forms.TextBox txtName = null!;
    private System.Windows.Forms.Label lblServer = null!;
    private System.Windows.Forms.TextBox txtServer = null!;
    private System.Windows.Forms.Label lblServerPassword = null!;
    private System.Windows.Forms.TextBox txtServerPassword = null!;
    private System.Windows.Forms.Label lblDatabase = null!;
    private System.Windows.Forms.TextBox txtDatabase = null!;
    private System.Windows.Forms.Label lblDbUser = null!;
    private System.Windows.Forms.TextBox txtDbUser = null!;
    private System.Windows.Forms.Label lblDbPassword = null!;
    private System.Windows.Forms.TextBox txtDbPassword = null!;
    private System.Windows.Forms.Label lblRemoteType = null!;
    private System.Windows.Forms.ComboBox cboRemoteType = null!;
    private System.Windows.Forms.Label lblRemoteAddress = null!;
    private System.Windows.Forms.TextBox txtRemoteAddress = null!;
    private System.Windows.Forms.Label lblRemoteUser = null!;
    private System.Windows.Forms.TextBox txtRemoteUser = null!;
    private System.Windows.Forms.Label lblRemotePassword = null!;
    private System.Windows.Forms.TextBox txtRemark = null!;
    private System.Windows.Forms.TextBox txtDatabaseName = null!;
    private System.Windows.Forms.TextBox txtRemotePassword = null!;

    private System.Windows.Forms.Button btnSave = null!;
    private System.Windows.Forms.Button btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Text = "账套编辑";
        this.Size = new System.Drawing.Size(1200, 1160);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = System.Drawing.Color.White;

        // 标题栏
        titleBar = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Top,
            Height = 68,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176)
        };

        lblTitle = new System.Windows.Forms.Label
        {
            Text = "➕ 新增账套",
            Font = new System.Drawing.Font("微软雅黑", 18F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.White,
            Location = new System.Drawing.Point(25, 15),
            AutoSize = true
        };
        titleBar.Controls.Add(lblTitle);

        // 内容面板
        contentPanel = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoScroll = true,
            Padding = new System.Windows.Forms.Padding(30, 15, 30, 15)
        };

        // 分隔线
        sep1 = new System.Windows.Forms.Panel { BackColor = System.Drawing.Color.FromArgb(220, 220, 220), Size = new System.Drawing.Size(1110, 1) };
        sep2 = new System.Windows.Forms.Panel { BackColor = System.Drawing.Color.FromArgb(220, 220, 220), Size = new System.Drawing.Size(1110, 1) };

        // 底部面板
        footerPanel = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Bottom,
            Height = 90,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        // 初始化内容控件
        InitContentControls();

        // 初始化按钮
        InitFooterButtons();

        // 加载控件
        contentPanel.Controls.Add(sep1);
        contentPanel.Controls.Add(sep2);
        contentPanel.Controls.Add(footerPanel);

        this.Controls.Add(contentPanel);
        this.Controls.Add(titleBar);

        this.ResumeLayout(false);
    }

    private void InitContentControls()
    {
        int lx = 30, ix = 250, lw = 210, iw = 900, rh = 57, y = 20;

        // 基本信息区
        AddRow(lx, y, lw, "代码：", ix, out txtCode); y += rh;
        AddRow(lx, y, lw, "账套名称：", ix, out txtName); y += rh;
        AddRow(lx, y, lw, "账套地址：", ix, out txtServer); y += rh;
        AddRow(lx, y, lw, "账套密码：", ix, out txtServerPassword); txtServerPassword.UseSystemPasswordChar = true; y += rh;
        AddRow(lx, y, lw, "数据库地址：", ix, out txtDatabase); y += rh;
        AddRow(lx, y, lw, "数据库名称：", ix, out txtDatabaseName); y += rh;
        AddRow(lx, y, lw, "DB用户名：", ix, out txtDbUser); y += rh;
        AddRow(lx, y, lw, "DB密码：", ix, out txtDbPassword); txtDbPassword.UseSystemPasswordChar = true; y += rh + 10;

        sep1.Location = new System.Drawing.Point(lx, y); y += 30;

        // 远程信息区
        AddLabel(lx, y, lw, "远程方式：", out lblRemoteType);
        AddCombo(ix, y, iw, out cboRemoteType); cboRemoteType.Items.AddRange(new object[] { "RDP", "向日葵", "其他" }); y += rh;

        AddRow(lx, y, lw, "远程地址：", ix, out txtRemoteAddress); y += rh;
        AddRow(lx, y, lw, "远程用户名：", ix, out txtRemoteUser); y += rh;
        AddRow(lx, y, lw, "远程密码：", ix, out txtRemotePassword); txtRemotePassword.UseSystemPasswordChar = true; y += rh + 10;

        // 备注区
        sep2.Location = new System.Drawing.Point(lx, y); y += 20;
        AddMultiRow(lx, y, lw, "备注：", ix, 900, 80, out txtRemark); y += rh + 30;

        sep2.Location = new System.Drawing.Point(lx, y); y += 15;
        footerPanel.Location = new System.Drawing.Point(0, y);
    }

    private void InitFooterButtons()
    {
        btnSave = new System.Windows.Forms.Button
        {
            Text = "💾 保存",
            Size = new System.Drawing.Size(150, 53),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 13F),
            Cursor = System.Windows.Forms.Cursors.Hand,
            Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        btnCancel = new System.Windows.Forms.Button
        {
            Text = "✖ 取消",
            Size = new System.Drawing.Size(150, 53),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(245, 245, 245),
            ForeColor = System.Drawing.Color.FromArgb(80, 80, 80),
            Font = new System.Drawing.Font("微软雅黑", 13F),
            Cursor = System.Windows.Forms.Cursors.Hand,
            Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnCancel.Click += BtnCancel_Click;

        footerPanel.Controls.Add(btnSave);
        footerPanel.Controls.Add(btnCancel);

        footerPanel.Resize += (s, e) =>
        {
            btnCancel.Left = footerPanel.Width - 30 - btnCancel.Width;
            btnSave.Left = btnCancel.Left - 15 - btnSave.Width;
        };
        btnCancel.Left = footerPanel.Width - 30 - btnCancel.Width;
        btnSave.Left = btnCancel.Left - 15 - btnSave.Width;
    }

    private void AddRow(int lx, int ly, int lw, string ltxt, int ix, out System.Windows.Forms.TextBox txt)
    {
        AddLabel(lx, ly, lw, ltxt, out var lbl);
        txt = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(ix, ly),
            Size = new System.Drawing.Size(900, 38),
            Font = new System.Drawing.Font("微软雅黑", 13F),
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        };
        contentPanel.Controls.Add(lbl);
        contentPanel.Controls.Add(txt);
    }

    private void AddLabel(int lx, int ly, int lw, string ltxt, out System.Windows.Forms.Label lbl)
    {
        lbl = new System.Windows.Forms.Label
        {
            Text = ltxt,
            Location = new System.Drawing.Point(lx, ly + 5),
            Size = new System.Drawing.Size(lw, 33),
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font = new System.Drawing.Font("微软雅黑", 13F),
            ForeColor = System.Drawing.Color.FromArgb(80, 80, 80)
        };
        contentPanel.Controls.Add(lbl);
    }

    private void AddCombo(int ix, int iy, int iw, out System.Windows.Forms.ComboBox cbo)
    {
        cbo = new System.Windows.Forms.ComboBox
        {
            Location = new System.Drawing.Point(ix, iy),
            Size = new System.Drawing.Size(iw, 38),
            Font = new System.Drawing.Font("微软雅黑", 13F),
            DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        };
        contentPanel.Controls.Add(cbo);
    }

    private void AddMultiRow(int lx, int ly, int lw, string ltxt, int ix, int iw, int ih, out System.Windows.Forms.TextBox txt)
    {
        AddLabel(lx, ly, lw, ltxt, out var lbl);
        txt = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(ix, ly),
            Size = new System.Drawing.Size(iw, ih),
            Font = new System.Drawing.Font("微软雅黑", 13F),
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            Multiline = true,
            ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        };
        contentPanel.Controls.Add(lbl);
        contentPanel.Controls.Add(txt);
    }
}
