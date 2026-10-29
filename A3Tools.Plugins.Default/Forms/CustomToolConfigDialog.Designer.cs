namespace A3Tools.Plugins.Default.Forms;

partial class CustomToolConfigDialog
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel mainLayout;
    private Panel scrollPanel;
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
    private Label lblSecondaryTables;
    private TextBox txtSecondaryTables;
    private Label lblSecondaryForeignKey;
    private TextBox txtSecondaryForeignKey;
    private Label lblSecondaryParentKey;
    private TextBox txtSecondaryParentKey;
    private Label lblSearchColumns;
    private TextBox txtSearchColumns;
    private Label lblColumnDisplayNames;
    private TextBox txtColumnDisplayNames;
    private Label lblHiddenColumns;
    private TextBox txtHiddenColumns;
    private Label lblHint;
    private Label lblSecondaryHint;
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
        scrollPanel = new Panel();
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
        lblSecondaryTables = new Label();
        txtSecondaryTables = new TextBox();
        lblSecondaryForeignKey = new Label();
        txtSecondaryForeignKey = new TextBox();
        lblSecondaryParentKey = new Label();
        txtSecondaryParentKey = new TextBox();
        lblSecondaryHint = new Label();
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
        scrollPanel.SuspendLayout();
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
        mainLayout.Controls.Add(lblSecondaryTables, 0, 6);
        mainLayout.Controls.Add(txtSecondaryTables, 1, 6);
        mainLayout.Controls.Add(lblSecondaryForeignKey, 0, 7);
        mainLayout.Controls.Add(txtSecondaryForeignKey, 1, 7);
        mainLayout.Controls.Add(lblSecondaryParentKey, 0, 8);
        mainLayout.Controls.Add(txtSecondaryParentKey, 1, 8);
        mainLayout.Controls.Add(lblSearchColumns, 0, 9);
        mainLayout.Controls.Add(txtSearchColumns, 1, 9);
        mainLayout.Controls.Add(lblColumnDisplayNames, 0, 10);
        mainLayout.Controls.Add(txtColumnDisplayNames, 1, 10);
        mainLayout.Controls.Add(lblHiddenColumns, 0, 11);
        mainLayout.Controls.Add(txtHiddenColumns, 1, 11);
        mainLayout.Controls.Add(lblHint, 1, 12);
        mainLayout.Controls.Add(lblSecondaryHint, 1, 13);
        mainLayout.Controls.Add(lblSearchHint2, 1, 14);
        mainLayout.AutoSize = true;
        mainLayout.Dock = DockStyle.Top;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Margin = new Padding(6, 5, 6, 5);
        mainLayout.Name = "mainLayout";
        mainLayout.Padding = new Padding(30, 26, 30, 13);
        mainLayout.RowCount = 15;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        mainLayout.Size = new Size(1040, 995);
        mainLayout.TabIndex = 0;
        // ★ 内容超高时靠窗体滚动条承载，按钮固定在底部（不跟着滚）
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
        txtForeignKey.PlaceholderText = "只填 1 个则所有关联表共用,如:REPORTGUID;填多个则与关联表按顺序一一对应,如:REPORTGUID;DOUBLECLICKGUID";
        txtForeignKey.Size = new Size(802, 38);
        txtForeignKey.TabIndex = 11;
        //
        // lblSecondaryTables
        //
        lblSecondaryTables.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblSecondaryTables.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSecondaryTables.Location = new Point(36, 527);
        lblSecondaryTables.Margin = new Padding(6, 0, 6, 0);
        lblSecondaryTables.Name = "lblSecondaryTables";
        lblSecondaryTables.Size = new Size(155, 38);
        lblSecondaryTables.TabIndex = 12;
        lblSecondaryTables.Text = "二级关联表:";
        lblSecondaryTables.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtSecondaryTables
        //
        txtSecondaryTables.Dock = DockStyle.Fill;
        txtSecondaryTables.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSecondaryTables.Location = new Point(208, 527);
        txtSecondaryTables.Margin = new Padding(11, 10, 0, 10);
        txtSecondaryTables.Multiline = true;
        txtSecondaryTables.Name = "txtSecondaryTables";
        txtSecondaryTables.PlaceholderText = "分号=组(第N组对应第N个关联表),逗号=组内多个,如:S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3";
        txtSecondaryTables.ScrollBars = ScrollBars.Vertical;
        txtSecondaryTables.Size = new Size(802, 79);
        txtSecondaryTables.TabIndex = 13;
        //
        // lblSecondaryForeignKey
        //
        lblSecondaryForeignKey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblSecondaryForeignKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSecondaryForeignKey.Location = new Point(36, 622);
        lblSecondaryForeignKey.Margin = new Padding(6, 0, 6, 0);
        lblSecondaryForeignKey.Name = "lblSecondaryForeignKey";
        lblSecondaryForeignKey.Size = new Size(155, 38);
        lblSecondaryForeignKey.TabIndex = 14;
        lblSecondaryForeignKey.Text = "二级关联字段:";
        lblSecondaryForeignKey.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtSecondaryForeignKey
        //
        txtSecondaryForeignKey.Dock = DockStyle.Fill;
        txtSecondaryForeignKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSecondaryForeignKey.Location = new Point(208, 622);
        txtSecondaryForeignKey.Margin = new Padding(11, 10, 0, 10);
        txtSecondaryForeignKey.Multiline = true;
        txtSecondaryForeignKey.Name = "txtSecondaryForeignKey";
        txtSecondaryForeignKey.PlaceholderText = "分组同上,值为二级表里指向一级表主键的外键列,如:GUID,GUID2;GUID3;GUID5";
        txtSecondaryForeignKey.ScrollBars = ScrollBars.Vertical;
        txtSecondaryForeignKey.Size = new Size(802, 79);
        txtSecondaryForeignKey.TabIndex = 15;
        //
        // lblSecondaryParentKey
        //
        lblSecondaryParentKey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblSecondaryParentKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblSecondaryParentKey.Location = new Point(36, 717);
        lblSecondaryParentKey.Margin = new Padding(6, 0, 6, 0);
        lblSecondaryParentKey.Name = "lblSecondaryParentKey";
        lblSecondaryParentKey.Size = new Size(155, 38);
        lblSecondaryParentKey.TabIndex = 16;
        lblSecondaryParentKey.Text = "二级关联父键:";
        lblSecondaryParentKey.TextAlign = ContentAlignment.MiddleRight;
        //
        // txtSecondaryParentKey
        //
        txtSecondaryParentKey.Dock = DockStyle.Fill;
        txtSecondaryParentKey.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSecondaryParentKey.Location = new Point(208, 717);
        txtSecondaryParentKey.Margin = new Padding(11, 10, 0, 10);
        txtSecondaryParentKey.Name = "txtSecondaryParentKey";
        txtSecondaryParentKey.PlaceholderText = "可选:一级表主键列名,分号分隔与关联表对应;留空=自动探测(数据库主键→GUID→CODE)";
        txtSecondaryParentKey.Size = new Size(802, 38);
        txtSecondaryParentKey.TabIndex = 17;
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
        lblHint.Text = "提示：关联表/关联字段留空表示只复制主表（按主键全量或选中行）；关联字段只填 1 个则所有关联表共用，填多个则与关联表按顺序一一对应。";
        //
        // lblSecondaryHint
        //
        lblSecondaryHint.AutoSize = true;
        lblSecondaryHint.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblSecondaryHint.ForeColor = Color.Gray;
        lblSecondaryHint.Location = new Point(203, 819);
        lblSecondaryHint.Margin = new Padding(6, 0, 6, 0);
        lblSecondaryHint.Name = "lblSecondaryHint";
        lblSecondaryHint.Padding = new Padding(11, 5, 0, 0);
        lblSecondaryHint.Size = new Size(800, 26);
        lblSecondaryHint.TabIndex = 20;
        lblSecondaryHint.Text = "提示：二级关联表留空 = 不复制二级表（推荐）。填写时「分号」分组（组数必须与关联表数量一致，按顺序对应），「逗号」分组内多个表；组内表数必须与二级关联字段个数一致。";
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
        // scrollPanel
        //
        // ★ 内容超高时靠这个面板的滚动条承载；
        //   按钮区在窗体上单独 Dock=Bottom → 按钮永远钉在可视区底部，不会被滚走。
        scrollPanel.AutoScroll = true;
        scrollPanel.Controls.Add(mainLayout);
        scrollPanel.Dock = DockStyle.Fill;
        scrollPanel.Location = new Point(0, 0);
        scrollPanel.Margin = new Padding(6, 5, 6, 5);
        scrollPanel.Name = "scrollPanel";
        scrollPanel.Size = new Size(1040, 770);
        scrollPanel.TabIndex = 0;
        //
        // pnlButtons
        //
        // ★ 按钮固定在窗体底部（不随内容滚动）
        pnlButtons.Controls.Add(btnCancel);
        pnlButtons.Controls.Add(btnSave);
        pnlButtons.Dock = DockStyle.Bottom;
        pnlButtons.Location = new Point(0, 770);
        pnlButtons.Margin = new Padding(6, 5, 6, 5);
        pnlButtons.Name = "pnlButtons";
        pnlButtons.Size = new Size(1040, 60);
        pnlButtons.TabIndex = 1;
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
        // ★ 微软雅黑 10pt 的正确缩放基准是 (14F, 30F)。
        //   原来写 (13F, 28F) 但窗体没设字体（继承 Microsoft YaHei UI 9pt）→ 窗体被额外放大 ~8%。
        //   现在窗体显式设 微软雅黑 10pt，与各控件一致，缩放系数 = 1。
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(1040, 830);
        // ★ 先加 Fill 区再加 Bottom 区 → 底部按钮区先占位，内容区自动让开
        Controls.Add(scrollPanel);
        Controls.Add(pnlButtons);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(6, 5, 6, 5);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CustomToolConfigDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "新建自定义工具";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        scrollPanel.ResumeLayout(false);
        scrollPanel.PerformLayout();
        pnlButtons.ResumeLayout(false);
        ResumeLayout(false);
    }
}