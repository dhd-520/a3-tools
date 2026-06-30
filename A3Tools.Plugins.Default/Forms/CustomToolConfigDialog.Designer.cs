namespace A3Tools.Plugins.Default.Forms;

partial class CustomToolConfigDialog
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel mainLayout;
    private Label lblName;
    private TextBox txtName;
    private Label lblDescription;
    private TextBox txtDescription;
    private Label lblMainTable;
    private TextBox txtMainTable;
    private Label lblPrimaryKey;
    private TextBox txtPrimaryKey;
    private Label lblRelatedTables;
    private TextBox txtRelatedTables;
    private Label lblForeignKey;
    private TextBox txtForeignKey;
    private Label lblSearchColumns;
    private TextBox txtSearchColumns;
    private Label lblColumnDisplayNames;
    private TextBox txtColumnDisplayNames;
    private Label lblHiddenColumns;
    private TextBox txtHiddenColumns;
    private Label lblHint;
    private Label lblSearchHint2;
    private Panel pnlButtons;
    private Button btnSave;
    private Button btnCancel;

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
        mainLayout = new TableLayoutPanel();
        lblName = new Label();
        txtName = new TextBox();
        lblDescription = new Label();
        txtDescription = new TextBox();
        lblMainTable = new Label();
        txtMainTable = new TextBox();
        lblPrimaryKey = new Label();
        txtPrimaryKey = new TextBox();
        lblRelatedTables = new Label();
        txtRelatedTables = new TextBox();
        lblForeignKey = new Label();
        txtForeignKey = new TextBox();
        lblSearchColumns = new Label();
        txtSearchColumns = new TextBox();
        lblColumnDisplayNames = new Label();
        txtColumnDisplayNames = new TextBox();
        lblHiddenColumns = new Label();
        txtHiddenColumns = new TextBox();
        lblHint = new Label();
        lblSearchHint2 = new Label();
        pnlButtons = new Panel();
        btnCancel = new Button();
        btnSave = new Button();
        mainLayout.SuspendLayout();
        pnlButtons.SuspendLayout();
        SuspendLayout();
        //
        // mainLayout
        //
        mainLayout.ColumnCount = 2;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 167F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(lblName, 0, 0);
        mainLayout.Controls.Add(txtName, 1, 0);
        mainLayout.Controls.Add(lblDescription, 0, 1);
        mainLayout.Controls.Add(txtDescription, 1, 1);
        mainLayout.Controls.Add(lblMainTable, 0, 2);
        mainLayout.Controls.Add(txtMainTable, 1, 2);
        mainLayout.Controls.Add(lblPrimaryKey, 0, 3);
        mainLayout.Controls.Add(txtPrimaryKey, 1, 3);
        mainLayout.Controls.Add(lblRelatedTables, 0, 4);
        mainLayout.Controls.Add(txtRelatedTables, 1, 4);
        mainLayout.Controls.Add(lblForeignKey, 0, 5);
        mainLayout.Controls.Add(txtForeignKey, 1, 5);
        mainLayout.Controls.Add(lblSearchColumns, 0, 6);
        mainLayout.Controls.Add(txtSearchColumns, 1, 6);
        mainLayout.Controls.Add(lblColumnDisplayNames, 0, 7);
        mainLayout.Controls.Add(txtColumnDisplayNames, 1, 7);
        mainLayout.Controls.Add(lblHiddenColumns, 0, 8);
        mainLayout.Controls.Add(txtHiddenColumns, 1, 8);
        mainLayout.Controls.Add(lblHint, 1, 9);
        mainLayout.Controls.Add(lblSearchHint2, 1, 10);
        mainLayout.Controls.Add(pnlButtons, 0, 11);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Margin = new Padding(6, 5, 6, 5);
        mainLayout.Name = "mainLayout";
        mainLayout.Padding = new Padding(30, 26, 30, 13);
        mainLayout.RowCount = 12;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 99F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 79F));
        mainLayout.Size = new Size(1040, 875);
        mainLayout.TabIndex = 0;
        //
        // lblName
        //
        lblName.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblName.Location = new Point(36, 36);
        lblName.Margin = new Padding(6, 0, 6, 0);
        lblName.Name = "lblName";
        lblName.Size = new Size(155, 38);
        lblName.TabIndex = 0;
        lblName.Text = "工具名称:";
        lblName.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtName
        //
        txtName.Dock = DockStyle.Fill;
        txtName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtName.Location = new Point(208, 36);
        txtName.Margin = new Padding(11, 10, 0, 10);
        txtName.Name = "txtName";
        txtName.PlaceholderText = "如:复制报表";
        txtName.Size = new Size(802, 38);
        txtName.TabIndex = 1;
        //
        // lblDescription
        //
        lblDescription.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblDescription.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblDescription.Location = new Point(36, 115);
        lblDescription.Margin = new Padding(6, 0, 6, 0);
        lblDescription.Name = "lblDescription";
        lblDescription.Size = new Size(155, 38);
        lblDescription.TabIndex = 2;
        lblDescription.Text = "描  述:";
        lblDescription.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtDescription
        //
        txtDescription.Dock = DockStyle.Fill;
        txtDescription.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtDescription.Location = new Point(208, 95);
        txtDescription.Margin = new Padding(11, 10, 0, 10);
        txtDescription.Name = "txtDescription";
        txtDescription.PlaceholderText = "可选,按钮副标题";
        txtDescription.Size = new Size(802, 38);
        txtDescription.TabIndex = 3;
        //
        // lblMainTable
        //
        lblMainTable.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblMainTable.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblMainTable.Location = new Point(36, 194);
        lblMainTable.Margin = new Padding(6, 0, 6, 0);
        lblMainTable.Name = "lblMainTable";
        lblMainTable.Size = new Size(155, 38);
        lblMainTable.TabIndex = 4;
        lblMainTable.Text = "主  表:";
        lblMainTable.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtMainTable
        //
        txtMainTable.Dock = DockStyle.Fill;
        txtMainTable.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtMainTable.Location = new Point(208, 194);
        txtMainTable.Margin = new Padding(11, 10, 0, 10);
        txtMainTable.Name = "txtMainTable";
        txtMainTable.PlaceholderText = "如:S_REPORT";
        txtMainTable.Size = new Size(802, 38);
        txtMainTable.TabIndex = 5;
        //
        // lblPrimaryKey
        //
        lblPrimaryKey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblPrimaryKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblPrimaryKey.Location = new Point(36, 253);
        lblPrimaryKey.Margin = new Padding(6, 0, 6, 0);
        lblPrimaryKey.Name = "lblPrimaryKey";
        lblPrimaryKey.Size = new Size(155, 38);
        lblPrimaryKey.TabIndex = 6;
        lblPrimaryKey.Text = "复制关键字:";
        lblPrimaryKey.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtPrimaryKey
        //
        txtPrimaryKey.Dock = DockStyle.Fill;
        txtPrimaryKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtPrimaryKey.Location = new Point(208, 253);
        txtPrimaryKey.Margin = new Padding(11, 10, 0, 10);
        txtPrimaryKey.Name = "txtPrimaryKey";
        txtPrimaryKey.PlaceholderText = "主键字段名,如:CODE";
        txtPrimaryKey.Size = new Size(802, 38);
        txtPrimaryKey.TabIndex = 7;
        //
        // lblRelatedTables
        //
        lblRelatedTables.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblRelatedTables.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblRelatedTables.Location = new Point(36, 361);
        lblRelatedTables.Margin = new Padding(6, 0, 6, 0);
        lblRelatedTables.Name = "lblRelatedTables";
        lblRelatedTables.Size = new Size(155, 38);
        lblRelatedTables.TabIndex = 8;
        lblRelatedTables.Text = "关 联 表:";
        lblRelatedTables.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtRelatedTables
        //
        txtRelatedTables.Dock = DockStyle.Fill;
        txtRelatedTables.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtRelatedTables.Location = new Point(208, 312);
        txtRelatedTables.Margin = new Padding(11, 10, 0, 10);
        txtRelatedTables.Multiline = true;
        txtRelatedTables.Name = "txtRelatedTables";
        txtRelatedTables.PlaceholderText = "多个用英文分号分隔,如:S_REPORTLINKDEFINE;S_REPORTDOUBLECLICK;S_REPORTFINANCEOFFSET";
        txtRelatedTables.ScrollBars = ScrollBars.Vertical;
        txtRelatedTables.Size = new Size(802, 136);
        txtRelatedTables.TabIndex = 9;
        //
        // lblForeignKey
        //
        lblForeignKey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblForeignKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblForeignKey.Location = new Point(36, 468);
        lblForeignKey.Margin = new Padding(6, 0, 6, 0);
        lblForeignKey.Name = "lblForeignKey";
        lblForeignKey.Size = new Size(155, 38);
        lblForeignKey.TabIndex = 10;
        lblForeignKey.Text = "关联字段:";
        lblForeignKey.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtForeignKey
        //
        txtForeignKey.Dock = DockStyle.Fill;
        txtForeignKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtForeignKey.Location = new Point(208, 468);
        txtForeignKey.Margin = new Padding(11, 10, 0, 10);
        txtForeignKey.Name = "txtForeignKey";
        txtForeignKey.PlaceholderText = "所有关联表共用的外键字段,如:REPORTGUID";
        txtForeignKey.Size = new Size(802, 38);
        txtForeignKey.TabIndex = 11;
        //
        // lblSearchColumns
        //
        lblSearchColumns.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblSearchColumns.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchColumns.Location = new Point(36, 527);
        lblSearchColumns.Margin = new Padding(6, 0, 6, 0);
        lblSearchColumns.Name = "lblSearchColumns";
        lblSearchColumns.Size = new Size(155, 38);
        lblSearchColumns.TabIndex = 12;
        lblSearchColumns.Text = "搜索列名:";
        lblSearchColumns.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtSearchColumns
        //
        txtSearchColumns.Dock = DockStyle.Fill;
        txtSearchColumns.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSearchColumns.Location = new Point(208, 527);
        txtSearchColumns.Margin = new Padding(11, 10, 0, 10);
        txtSearchColumns.Multiline = true;
        txtSearchColumns.Name = "txtSearchColumns";
        txtSearchColumns.PlaceholderText = "多个用英文分号分隔,留空表示用旧行为(主键+NAME),如:GUID;CODE;SUBSYSTEMGUID;NAME;NOTES";
        txtSearchColumns.ScrollBars = ScrollBars.Vertical;
        txtSearchColumns.Size = new Size(802, 79);
        txtSearchColumns.TabIndex = 13;
        //
        // lblColumnDisplayNames
        //
        lblColumnDisplayNames.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblColumnDisplayNames.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblColumnDisplayNames.Location = new Point(36, 622);
        lblColumnDisplayNames.Margin = new Padding(6, 0, 6, 0);
        lblColumnDisplayNames.Name = "lblColumnDisplayNames";
        lblColumnDisplayNames.Size = new Size(155, 38);
        lblColumnDisplayNames.TabIndex = 14;
        lblColumnDisplayNames.Text = "列显示名称:";
        lblColumnDisplayNames.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtColumnDisplayNames
        //
        txtColumnDisplayNames.Dock = DockStyle.Fill;
        txtColumnDisplayNames.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtColumnDisplayNames.Location = new Point(208, 622);
        txtColumnDisplayNames.Margin = new Padding(11, 10, 0, 10);
        txtColumnDisplayNames.Multiline = true;
        txtColumnDisplayNames.Name = "txtColumnDisplayNames";
        txtColumnDisplayNames.PlaceholderText = "数量必须与搜索列名一致;缺失则保留数据库原列名,如:GUID;代码;分类;名称;备注";
        txtColumnDisplayNames.ScrollBars = ScrollBars.Vertical;
        txtColumnDisplayNames.Size = new Size(802, 79);
        txtColumnDisplayNames.TabIndex = 15;
        //
        // lblHiddenColumns
        //
        lblHiddenColumns.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblHiddenColumns.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblHiddenColumns.Location = new Point(36, 717);
        lblHiddenColumns.Margin = new Padding(6, 0, 6, 0);
        lblHiddenColumns.Name = "lblHiddenColumns";
        lblHiddenColumns.Size = new Size(155, 38);
        lblHiddenColumns.TabIndex = 16;
        lblHiddenColumns.Text = "隐 藏 列:";
        lblHiddenColumns.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtHiddenColumns
        //
        txtHiddenColumns.Dock = DockStyle.Fill;
        txtHiddenColumns.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtHiddenColumns.Location = new Point(208, 717);
        txtHiddenColumns.Margin = new Padding(11, 10, 0, 10);
        txtHiddenColumns.Multiline = true;
        txtHiddenColumns.Name = "txtHiddenColumns";
        txtHiddenColumns.PlaceholderText = "多个用英文分号分隔,搜索结果中强制隐藏;PrimaryKey 始终显示,如:GUID;SUBSYSTEMGUID";
        txtHiddenColumns.ScrollBars = ScrollBars.Vertical;
        txtHiddenColumns.Size = new Size(802, 54);
        txtHiddenColumns.TabIndex = 17;
        //
        // lblHint
        //
        lblHint.AutoSize = true;
        lblHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblHint.ForeColor = Color.Gray;
        lblHint.Location = new Point(203, 787);
        lblHint.Margin = new Padding(6, 0, 6, 0);
        lblHint.Name = "lblHint";
        lblHint.Padding = new Padding(11, 7, 0, 0);
        lblHint.Size = new Size(683, 26);
        lblHint.TabIndex = 18;
        lblHint.Text = "提示：关联表/关联字段留空表示只复制主表（按主键全量或选中行）。";
        //
        // lblSearchHint2
        //
        lblSearchHint2.AutoSize = true;
        lblSearchHint2.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblSearchHint2.ForeColor = Color.Gray;
        lblSearchHint2.Location = new Point(203, 819);
        lblSearchHint2.Margin = new Padding(6, 0, 6, 0);
        lblSearchHint2.Name = "lblSearchHint2";
        lblSearchHint2.Padding = new Padding(11, 5, 0, 0);
        lblSearchHint2.Size = new Size(683, 26);
        lblSearchHint2.TabIndex = 19;
        lblSearchHint2.Text = "提示：搜索列名/列显示名称留空 = 保留旧行为（显示主表所有列）；搜索结果中 PrimaryKey 始终显示。";
        //
        // pnlButtons
        //
        mainLayout.SetColumnSpan(pnlButtons, 2);
        pnlButtons.Controls.Add(btnCancel);
        pnlButtons.Controls.Add(btnSave);
        pnlButtons.Dock = DockStyle.Fill;
        pnlButtons.Location = new Point(36, 851);
        pnlButtons.Margin = new Padding(6, 5, 6, 5);
        pnlButtons.Name = "pnlButtons";
        pnlButtons.Size = new Size(968, 69);
        pnlButtons.TabIndex = 20;
        //
        // btnCancel
        //
        btnCancel.Anchor = AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.ForeColor = Color.Gray;
        btnCancel.Location = new Point(680, 8);
        btnCancel.Margin = new Padding(6, 5, 6, 5);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(130, 48);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        //
        // btnSave
        //
        btnSave.Anchor = AnchorStyles.Right;
        btnSave.BackColor = Color.FromArgb(24, 145, 176);
        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnSave.ForeColor = Color.White;
        btnSave.Location = new Point(830, 8);
        btnSave.Margin = new Padding(6, 5, 6, 5);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(130, 48);
        btnSave.TabIndex = 2;
        btnSave.Text = "保存";
        btnSave.UseVisualStyleBackColor = false;
        btnSave.Click += BtnSave_Click;
        //
        // CustomToolConfigDialog
        //
        AcceptButton = btnSave;
        AutoScaleDimensions = new SizeF(13F, 28F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(1040, 880);
        Controls.Add(mainLayout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(6, 5, 6, 5);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CustomToolConfigDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "新建自定义工具";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        pnlButtons.ResumeLayout(false);
        ResumeLayout(false);
    }
}