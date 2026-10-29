namespace A3Tools.Forms;

partial class AccountFieldSettingsForm
{
    private System.ComponentModel.IContainer components = null;

    // ===== 标题区 =====
    private Panel headerPanel = null!;
    private Label lblTitle = null!;
    private Label lblHint = null!;

    // ===== 表格 =====
    private DataGridView dgvFields = null!;

    // ===== 底部按钮区 =====
    private Panel footerPanel = null!;
    private Label lblStatus = null!;
    private Button btnReset = null!;
    private Button btnCancel = null!;
    private Button btnSave = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        headerPanel = new Panel();
        lblTitle = new Label();
        lblHint = new Label();
        dgvFields = new DataGridView();
        footerPanel = new Panel();
        lblStatus = new Label();
        btnReset = new Button();
        btnCancel = new Button();
        btnSave = new Button();
        ((System.ComponentModel.ISupportInitialize)dgvFields).BeginInit();
        headerPanel.SuspendLayout();
        footerPanel.SuspendLayout();
        SuspendLayout();
        //
        // headerPanel
        //
        headerPanel.BackColor = Color.FromArgb(24, 145, 176);
        headerPanel.Controls.Add(lblTitle);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Name = "headerPanel";
        headerPanel.Size = new Size(860, 46);
        headerPanel.TabIndex = 0;
        //
        // lblTitle
        //
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("微软雅黑", 12F, FontStyle.Bold, GraphicsUnit.Point);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(0, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(860, 46);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "账套字段设置";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        //
        // lblHint
        //
        lblHint.Dock = DockStyle.Top;
        lblHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblHint.ForeColor = Color.FromArgb(80, 80, 80);
        lblHint.Location = new Point(0, 46);
        lblHint.Name = "lblHint";
        lblHint.Padding = new Padding(12, 8, 12, 8);
        lblHint.Size = new Size(860, 58);
        lblHint.TabIndex = 1;
        lblHint.Text = "逐个账套字段设置它是否显示在列表、列表宽度、列表排序，以及是否显示在录入界面、录入界面排序。\r\n" +
            "排序号 = 位置（填 3 就是排第 3 位，其余自动顺延）；列表宽度填 0 表示自动（随窗口按权重铺满），填具体像素表示固定宽度。\r\n" +
            "密码类字段不能显示在列表（避免明文泄露）；代码、账套名称为必填，不能从录入界面隐藏。\r\n" +
            "标了「（整组一起移动）」的字段在分组面板内部（如「网页版自动登录」），改它的排序号 = 整个分组在录入界面里上下移动。";
        //
        // dgvFields
        //
        dgvFields.AllowUserToAddRows = false;
        dgvFields.AllowUserToDeleteRows = false;
        dgvFields.AllowUserToResizeRows = false;
        dgvFields.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        dgvFields.AutoGenerateColumns = false;
        dgvFields.BackgroundColor = Color.White;
        dgvFields.BorderStyle = BorderStyle.None;
        dgvFields.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvFields.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
        dgvFields.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
        dgvFields.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 80, 80);
        dgvFields.ColumnHeadersHeight = 34;
        dgvFields.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvFields.Dock = DockStyle.Fill;
        dgvFields.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        dgvFields.EnableHeadersVisualStyles = false;
        dgvFields.Font = new Font("微软雅黑", 9F);
        dgvFields.Location = new Point(0, 104);
        dgvFields.MultiSelect = false;
        dgvFields.Name = "dgvFields";
        dgvFields.RowHeadersVisible = false;
        dgvFields.RowTemplate.Height = 30;
        dgvFields.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvFields.Size = new Size(860, 452);
        dgvFields.TabIndex = 2;
        //
        // 列定义（★ 2026-10-28 陛下要的账套字段设置）
        //
        dgvFields.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColField",
            HeaderText = "账套字段",
            ReadOnly = true,
            Width = 230,
            MinimumWidth = 140,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        dgvFields.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "ColShowList",
            HeaderText = "显示列表",
            Width = 90,
            MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        dgvFields.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColListWidth",
            HeaderText = "列表宽度(px)",
            ValueType = typeof(int),
            Width = 110,
            MinimumWidth = 90,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        dgvFields.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColListOrder",
            HeaderText = "列表排序",
            ValueType = typeof(int),
            Width = 90,
            MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        dgvFields.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "ColShowEditor",
            HeaderText = "显示录入界面",
            Width = 110,
            MinimumWidth = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        });
        dgvFields.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColEditorOrder",
            HeaderText = "录入界面排序",
            ValueType = typeof(int),
            Width = 120,
            MinimumWidth = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        //
        // footerPanel
        //
        footerPanel.BackColor = Color.FromArgb(245, 245, 245);
        footerPanel.Controls.Add(lblStatus);
        footerPanel.Controls.Add(btnReset);
        footerPanel.Controls.Add(btnCancel);
        footerPanel.Controls.Add(btnSave);
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Location = new Point(0, 556);
        footerPanel.Name = "footerPanel";
        footerPanel.Size = new Size(860, 64);
        footerPanel.TabIndex = 3;
        //
        // lblStatus
        //
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = Color.FromArgb(120, 120, 120);
        lblStatus.Location = new Point(12, 22);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(53, 15);
        lblStatus.TabIndex = 0;
        lblStatus.Text = "就绪";
        //
        // btnReset
        //
        btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnReset.BackColor = Color.White;
        btnReset.Cursor = Cursors.Hand;
        btnReset.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnReset.FlatStyle = FlatStyle.Flat;
        btnReset.Font = new Font("微软雅黑", 10F);
        btnReset.ForeColor = Color.FromArgb(80, 80, 80);
        btnReset.Location = new Point(404, 12);
        btnReset.Name = "btnReset";
        btnReset.Size = new Size(120, 40);
        btnReset.TabIndex = 1;
        btnReset.Text = "↺ 恢复默认";
        btnReset.UseVisualStyleBackColor = false;
        //
        // btnCancel
        //
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.Cursor = Cursors.Hand;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F);
        btnCancel.ForeColor = Color.FromArgb(80, 80, 80);
        btnCancel.Location = new Point(534, 12);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(110, 40);
        btnCancel.TabIndex = 2;
        btnCancel.Text = "✖ 取消";
        btnCancel.UseVisualStyleBackColor = false;
        //
        // btnSave
        //
        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSave.BackColor = Color.FromArgb(24, 145, 176);
        btnSave.Cursor = Cursors.Hand;
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
        btnSave.ForeColor = Color.White;
        btnSave.Location = new Point(654, 12);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(110, 40);
        btnSave.TabIndex = 3;
        btnSave.Text = "💾 保存";
        btnSave.UseVisualStyleBackColor = false;
        //
        // AccountFieldSettingsForm
        //
        AutoScaleDimensions = new SizeF(16F, 35F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(860, 620);
        Controls.Add(dgvFields);
        Controls.Add(lblHint);
        Controls.Add(headerPanel);
        Controls.Add(footerPanel);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        MinimumSize = new Size(760, 520);
        Name = "AccountFieldSettingsForm";
        Size = new Size(860, 620);
        StartPosition = FormStartPosition.CenterParent;
        Text = "账套字段设置";
        ((System.ComponentModel.ISupportInitialize)dgvFields).EndInit();
        headerPanel.ResumeLayout(false);
        footerPanel.ResumeLayout(false);
        footerPanel.PerformLayout();
        ResumeLayout(false);
    }
}
