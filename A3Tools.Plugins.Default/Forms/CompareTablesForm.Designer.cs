namespace A3Tools.Plugins.Default.Forms;

partial class CompareTablesForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel mainLayout;
    private Panel pnlButtons;
    private Button btnExecute;
    private Button btnCopyScript;
    private Button btnRefresh;
    private Button btnClose;
    private Panel pnlFilterRow;
    private Label lblDiffType;
    private CheckBox chkMissingTable;
    private CheckBox chkMissingCol;
    private CheckBox chkTypeDiff;
    private Label lblColumnName;
    private TextBox txtColumnFilter;
    private Button btnClearFilter;
    private Label lblSummary;
    private ProgressBar progressBar;
    private Label lblProgress;
    private Panel pnlResults;
    private DataGridView dgvDifferences;

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
        pnlButtons = new Panel();
        btnExecute = new Button();
        btnCopyScript = new Button();
        btnRefresh = new Button();
        btnClose = new Button();
        pnlFilterRow = new Panel();
        lblDiffType = new Label();
        chkMissingTable = new CheckBox();
        chkMissingCol = new CheckBox();
        chkTypeDiff = new CheckBox();
        lblColumnName = new Label();
        txtColumnFilter = new TextBox();
        btnClearFilter = new Button();
        lblSummary = new Label();
        progressBar = new ProgressBar();
        lblProgress = new Label();
        pnlResults = new Panel();
        dgvDifferences = new DataGridView();
        mainLayout.SuspendLayout();
        pnlButtons.SuspendLayout();
        pnlFilterRow.SuspendLayout();
        pnlResults.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvDifferences).BeginInit();
        SuspendLayout();
        // 
        // mainLayout
        // 
        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(pnlButtons, 0, 0);
        mainLayout.Controls.Add(pnlFilterRow, 0, 1);
        mainLayout.Controls.Add(lblSummary, 0, 2);
        mainLayout.Controls.Add(progressBar, 0, 3);
        mainLayout.Controls.Add(lblProgress, 0, 4);
        mainLayout.Controls.Add(pnlResults, 0, 5);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.RowCount = 6;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 63F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Size = new Size(1222, 780);
        mainLayout.TabIndex = 0;
        // 
        // pnlButtons
        // 
        pnlButtons.Controls.Add(btnExecute);
        pnlButtons.Controls.Add(btnCopyScript);
        pnlButtons.Controls.Add(btnRefresh);
        pnlButtons.Controls.Add(btnClose);
        pnlButtons.Dock = DockStyle.Fill;
        pnlButtons.Location = new Point(3, 3);
        pnlButtons.Name = "pnlButtons";
        pnlButtons.Size = new Size(1216, 54);
        pnlButtons.TabIndex = 0;
        // 
        // btnExecute
        // 
        btnExecute.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnExecute.BackColor = Color.FromArgb(200, 80, 80);
        btnExecute.FlatAppearance.BorderSize = 0;
        btnExecute.FlatStyle = FlatStyle.Flat;
        btnExecute.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        btnExecute.ForeColor = Color.White;
        btnExecute.Location = new Point(706, 10);
        btnExecute.Name = "btnExecute";
        btnExecute.Size = new Size(120, 38);
        btnExecute.TabIndex = 0;
        btnExecute.Text = "执行脚本";
        btnExecute.UseVisualStyleBackColor = false;
        btnExecute.Click += BtnExecute_Click;
        // 
        // btnCopyScript
        // 
        btnCopyScript.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCopyScript.BackColor = Color.FromArgb(24, 145, 176);
        btnCopyScript.FlatAppearance.BorderSize = 0;
        btnCopyScript.FlatStyle = FlatStyle.Flat;
        btnCopyScript.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        btnCopyScript.ForeColor = Color.White;
        btnCopyScript.Location = new Point(832, 10);
        btnCopyScript.Name = "btnCopyScript";
        btnCopyScript.Size = new Size(120, 38);
        btnCopyScript.TabIndex = 1;
        btnCopyScript.Text = "复制脚本";
        btnCopyScript.UseVisualStyleBackColor = false;
        btnCopyScript.Click += BtnCopyScript_Click;
        // 
        // btnRefresh
        // 
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.BackColor = Color.White;
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(24, 145, 176);
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnRefresh.ForeColor = Color.FromArgb(24, 145, 176);
        btnRefresh.Location = new Point(958, 10);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(134, 38);
        btnRefresh.TabIndex = 2;
        btnRefresh.Text = "重新对比";
        btnRefresh.UseVisualStyleBackColor = false;
        btnRefresh.Click += BtnRefresh_Click;
        // 
        // btnClose
        // 
        btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.BackColor = Color.FromArgb(120, 120, 120);
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatStyle = FlatStyle.Flat;
        btnClose.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnClose.ForeColor = Color.White;
        btnClose.Location = new Point(1102, 10);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(90, 38);
        btnClose.TabIndex = 3;
        btnClose.Text = "关闭";
        btnClose.UseVisualStyleBackColor = false;
        btnClose.Click += BtnClose_Click;
        // 
        // pnlFilterRow
        // 
        pnlFilterRow.BackColor = Color.FromArgb(245, 248, 250);
        pnlFilterRow.BorderStyle = BorderStyle.FixedSingle;
        pnlFilterRow.Controls.Add(lblDiffType);
        pnlFilterRow.Controls.Add(chkMissingTable);
        pnlFilterRow.Controls.Add(chkMissingCol);
        pnlFilterRow.Controls.Add(chkTypeDiff);
        pnlFilterRow.Controls.Add(lblColumnName);
        pnlFilterRow.Controls.Add(txtColumnFilter);
        pnlFilterRow.Controls.Add(btnClearFilter);
        pnlFilterRow.Dock = DockStyle.Fill;
        pnlFilterRow.Location = new Point(3, 63);
        pnlFilterRow.Name = "pnlFilterRow";
        pnlFilterRow.Size = new Size(1216, 57);
        pnlFilterRow.TabIndex = 1;
        // 
        // lblDiffType
        // 
        lblDiffType.AutoSize = true;
        lblDiffType.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblDiffType.Location = new Point(12, 19);
        lblDiffType.Name = "lblDiffType";
        lblDiffType.Size = new Size(134, 31);
        lblDiffType.TabIndex = 0;
        lblDiffType.Text = "差异类型：";
        lblDiffType.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // chkMissingTable
        // 
        chkMissingTable.AutoSize = true;
        chkMissingTable.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkMissingTable.Location = new Point(173, 19);
        chkMissingTable.Name = "chkMissingTable";
        chkMissingTable.Size = new Size(88, 35);
        chkMissingTable.TabIndex = 1;
        chkMissingTable.Text = "缺表";
        chkMissingTable.UseVisualStyleBackColor = true;
        // 
        // chkMissingCol
        // 
        chkMissingCol.AutoSize = true;
        chkMissingCol.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkMissingCol.Location = new Point(267, 19);
        chkMissingCol.Name = "chkMissingCol";
        chkMissingCol.Size = new Size(112, 35);
        chkMissingCol.TabIndex = 2;
        chkMissingCol.Text = "缺字段";
        chkMissingCol.UseVisualStyleBackColor = true;
        // 
        // chkTypeDiff
        // 
        chkTypeDiff.AutoSize = true;
        chkTypeDiff.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        chkTypeDiff.Location = new Point(385, 18);
        chkTypeDiff.Name = "chkTypeDiff";
        chkTypeDiff.Size = new Size(136, 35);
        chkTypeDiff.TabIndex = 3;
        chkTypeDiff.Text = "类型差异";
        chkTypeDiff.UseVisualStyleBackColor = true;
        // 
        // lblColumnName
        // 
        lblColumnName.AutoSize = true;
        lblColumnName.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblColumnName.Location = new Point(527, 19);
        lblColumnName.Name = "lblColumnName";
        lblColumnName.Size = new Size(110, 31);
        lblColumnName.TabIndex = 4;
        lblColumnName.Text = "字段名：";
        lblColumnName.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtColumnFilter
        // 
        txtColumnFilter.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtColumnFilter.Location = new Point(643, 14);
        txtColumnFilter.Name = "txtColumnFilter";
        txtColumnFilter.PlaceholderText = "模糊匹配字段名（不含表）";
        txtColumnFilter.Size = new Size(400, 38);
        txtColumnFilter.TabIndex = 5;
        // 
        // btnClearFilter
        // 
        btnClearFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClearFilter.BackColor = Color.White;
        btnClearFilter.FlatAppearance.BorderColor = Color.Gray;
        btnClearFilter.FlatStyle = FlatStyle.Flat;
        btnClearFilter.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnClearFilter.ForeColor = Color.Gray;
        btnClearFilter.Location = new Point(1082, 15);
        btnClearFilter.Name = "btnClearFilter";
        btnClearFilter.Size = new Size(120, 35);
        btnClearFilter.TabIndex = 6;
        btnClearFilter.Text = "清空筛选";
        btnClearFilter.UseVisualStyleBackColor = false;
        // 
        // lblSummary
        // 
        lblSummary.Dock = DockStyle.Fill;
        lblSummary.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblSummary.ForeColor = Color.FromArgb(24, 145, 176);
        lblSummary.Location = new Point(3, 123);
        lblSummary.Name = "lblSummary";
        lblSummary.Size = new Size(1216, 30);
        lblSummary.TabIndex = 2;
        lblSummary.Text = "正在对比表结构...";
        lblSummary.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // progressBar
        // 
        progressBar.Dock = DockStyle.Fill;
        progressBar.Location = new Point(3, 156);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(1216, 19);
        progressBar.TabIndex = 3;
        // 
        // lblProgress
        // 
        lblProgress.Dock = DockStyle.Fill;
        lblProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Location = new Point(3, 178);
        lblProgress.Name = "lblProgress";
        lblProgress.Size = new Size(1216, 25);
        lblProgress.TabIndex = 4;
        lblProgress.Text = "就绪";
        lblProgress.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // pnlResults
        // 
        pnlResults.BorderStyle = BorderStyle.FixedSingle;
        pnlResults.Controls.Add(dgvDifferences);
        pnlResults.Dock = DockStyle.Fill;
        pnlResults.Location = new Point(3, 206);
        pnlResults.Name = "pnlResults";
        pnlResults.Size = new Size(1216, 571);
        pnlResults.TabIndex = 5;
        // 
        // dgvDifferences
        // 
        dgvDifferences.AllowUserToAddRows = false;
        dgvDifferences.AllowUserToDeleteRows = false;
        dgvDifferences.BackgroundColor = Color.White;
        dgvDifferences.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvDifferences.Dock = DockStyle.Fill;
        dgvDifferences.Location = new Point(0, 0);
        dgvDifferences.Name = "dgvDifferences";
        dgvDifferences.ReadOnly = true;
        dgvDifferences.RowHeadersWidth = 60;
        dgvDifferences.RowTemplate.Height = 25;
        dgvDifferences.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvDifferences.Size = new Size(1214, 569);
        dgvDifferences.TabIndex = 0;
        // 
        // CompareTablesForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1222, 780);
        Controls.Add(mainLayout);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CompareTablesForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "表结构对比";
        mainLayout.ResumeLayout(false);
        pnlButtons.ResumeLayout(false);
        pnlFilterRow.ResumeLayout(false);
        pnlFilterRow.PerformLayout();
        pnlResults.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvDifferences).EndInit();
        ResumeLayout(false);
    }
}
