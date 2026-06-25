namespace A3Tools.Forms;

partial class QuickAddAccountDialog
{
    private System.ComponentModel.IContainer components = null;

    // ===== 窗体分区 =====
    private Panel titleBar = null!;
    private Label lblTitle = null!;
    private Panel contentPanel = null!;
    private Panel footerPanel = null!;

    // ===== 内容 =====
    private Label lblHint = null!;
    private PastePreservingTextBox txtPaste = null!;
    private Label lblStatus = null!;

    // ===== 按钮 =====
    private Button btnSwitchManual = null!;
    private Button btnConfirm = null!;
    private Button btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        titleBar = new Panel();
        lblTitle = new Label();
        contentPanel = new Panel();
        lblHint = new Label();
        txtPaste = new PastePreservingTextBox();
        lblStatus = new Label();
        footerPanel = new Panel();
        btnSwitchManual = new Button();
        btnCancel = new Button();
        btnConfirm = new Button();
        titleBar.SuspendLayout();
        contentPanel.SuspendLayout();
        footerPanel.SuspendLayout();
        SuspendLayout();
        // 
        // titleBar
        // 
        titleBar.BackColor = Color.FromArgb(24, 145, 176);
        titleBar.Controls.Add(lblTitle);
        titleBar.Dock = DockStyle.Top;
        titleBar.Location = new Point(0, 0);
        titleBar.Name = "titleBar";
        titleBar.Size = new Size(776, 50);
        titleBar.TabIndex = 0;
        // 
        // lblTitle
        // 
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(776, 50);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "🔖 一键添加账套";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // contentPanel
        // 
        contentPanel.BackColor = Color.White;
        contentPanel.Controls.Add(lblHint);
        contentPanel.Controls.Add(txtPaste);
        contentPanel.Controls.Add(lblStatus);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 50);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(20, 15, 20, 15);
        contentPanel.Size = new Size(776, 391);
        contentPanel.TabIndex = 1;
        // 
        // lblHint
        // 
        lblHint.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblHint.ForeColor = Color.FromArgb(120, 120, 120);
        lblHint.Location = new Point(20, 15);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(736, 73);
        lblHint.TabIndex = 0;
        lblHint.Text = "请粘贴账套信息文本（在 Root 模式下复制账套信息后粘贴可识别全部字段，含密码）。\r\n「代码」自动分配，其他字段按「字段名：值」自动匹配。";
        // 
        // txtPaste
        // 
        txtPaste.AcceptsReturn = true;
        txtPaste.AcceptsTab = true;
        txtPaste.BorderStyle = BorderStyle.FixedSingle;
        txtPaste.Font = new Font("Consolas", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        txtPaste.Location = new Point(20, 91);
        txtPaste.Multiline = true;
        txtPaste.Name = "txtPaste";
        txtPaste.ScrollBars = ScrollBars.Both;
        txtPaste.Size = new Size(736, 267);
        txtPaste.TabIndex = 1;
        txtPaste.WordWrap = false;
        // 
        // lblStatus
        // 
        lblStatus.Font = new Font("微软雅黑", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        lblStatus.ForeColor = Color.FromArgb(120, 120, 120);
        lblStatus.Location = new Point(20, 318);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(660, 20);
        lblStatus.TabIndex = 2;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // footerPanel
        // 
        footerPanel.BackColor = Color.FromArgb(245, 245, 245);
        footerPanel.Controls.Add(btnSwitchManual);
        footerPanel.Controls.Add(btnCancel);
        footerPanel.Controls.Add(btnConfirm);
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Location = new Point(0, 441);
        footerPanel.Name = "footerPanel";
        footerPanel.Size = new Size(776, 70);
        footerPanel.TabIndex = 2;
        // 
        // btnSwitchManual
        // 
        btnSwitchManual.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSwitchManual.BackColor = Color.White;
        btnSwitchManual.Cursor = Cursors.Hand;
        btnSwitchManual.FlatAppearance.BorderSize = 0;
        btnSwitchManual.FlatStyle = FlatStyle.Flat;
        btnSwitchManual.Font = new Font("微软雅黑", 11F, FontStyle.Underline, GraphicsUnit.Point);
        btnSwitchManual.ForeColor = Color.FromArgb(24, 145, 176);
        btnSwitchManual.Location = new Point(96, 18);
        btnSwitchManual.Name = "btnSwitchManual";
        btnSwitchManual.Size = new Size(160, 40);
        btnSwitchManual.TabIndex = 0;
        btnSwitchManual.Text = "切换为手动添加";
        btnSwitchManual.UseVisualStyleBackColor = false;
        btnSwitchManual.Click += BtnSwitchManual_Click;
        // 
        // btnCancel
        // 
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.Cursor = Cursors.Hand;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.ForeColor = Color.FromArgb(80, 80, 80);
        btnCancel.Location = new Point(606, 17);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(150, 41);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "✖ 取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // btnConfirm
        // 
        btnConfirm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnConfirm.BackColor = Color.FromArgb(24, 145, 176);
        btnConfirm.Cursor = Cursors.Hand;
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.FlatStyle = FlatStyle.Flat;
        btnConfirm.Font = new Font("微软雅黑", 11F, FontStyle.Bold, GraphicsUnit.Point);
        btnConfirm.ForeColor = Color.White;
        btnConfirm.Location = new Point(420, 18);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(171, 40);
        btnConfirm.TabIndex = 2;
        btnConfirm.Text = "💾 确定添加";
        btnConfirm.UseVisualStyleBackColor = false;
        btnConfirm.Click += BtnConfirm_Click;
        // 
        // QuickAddAccountDialog
        // 
        AutoScaleDimensions = new SizeF(16F, 35F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(776, 511);
        Controls.Add(contentPanel);
        Controls.Add(footerPanel);
        Controls.Add(titleBar);
        Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "QuickAddAccountDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "一键添加账套";
        KeyDown += QuickAddAccountDialog_KeyDown;
        titleBar.ResumeLayout(false);
        contentPanel.ResumeLayout(false);
        contentPanel.PerformLayout();
        footerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}