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
        lblSummary = new Label();
        progressBar = new ProgressBar();
        lblProgress = new Label();
        pnlResults = new Panel();
        dgvDifferences = new DataGridView();
        mainLayout.SuspendLayout();
        pnlButtons.SuspendLayout();
        pnlResults.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvDifferences).BeginInit();
        SuspendLayout();
        // 
        // mainLayout
        // 
        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(pnlButtons, 0, 0);
        mainLayout.Controls.Add(lblSummary, 0, 1);
        mainLayout.Controls.Add(progressBar, 0, 2);
        mainLayout.Controls.Add(lblProgress, 0, 3);
        mainLayout.Controls.Add(pnlResults, 0, 4);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.RowCount = 5;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Size = new Size(1222, 757);
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
        // lblSummary
        // 
        lblSummary.Dock = DockStyle.Fill;
        lblSummary.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
        lblSummary.ForeColor = Color.FromArgb(24, 145, 176);
        lblSummary.Location = new Point(3, 60);
        lblSummary.Name = "lblSummary";
        lblSummary.Size = new Size(1216, 30);
        lblSummary.TabIndex = 1;
        lblSummary.Text = "正在对比表结构...";
        lblSummary.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // progressBar
        // 
        progressBar.Dock = DockStyle.Fill;
        progressBar.Location = new Point(3, 93);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(1216, 19);
        progressBar.TabIndex = 2;
        // 
        // lblProgress
        // 
        lblProgress.Dock = DockStyle.Fill;
        lblProgress.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        lblProgress.ForeColor = Color.Gray;
        lblProgress.Location = new Point(3, 115);
        lblProgress.Name = "lblProgress";
        lblProgress.Size = new Size(1216, 25);
        lblProgress.TabIndex = 3;
        lblProgress.Text = "就绪";
        lblProgress.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // pnlResults
        // 
        pnlResults.BorderStyle = BorderStyle.FixedSingle;
        pnlResults.Controls.Add(dgvDifferences);
        pnlResults.Dock = DockStyle.Fill;
        pnlResults.Location = new Point(3, 143);
        pnlResults.Name = "pnlResults";
        pnlResults.Size = new Size(1216, 611);
        pnlResults.TabIndex = 4;
        // 
        // dgvDifferences
        // 
        dgvDifferences.AllowUserToAddRows = false;
        dgvDifferences.AllowUserToDeleteRows = false;
        dgvDifferences.BackgroundColor = Color.White;
        dgvDifferences.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvDifferences.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvDifferences.ScrollBars = ScrollBars.Both;
        dgvDifferences.Dock = DockStyle.Fill;
        dgvDifferences.Location = new Point(0, 0);
        dgvDifferences.Name = "dgvDifferences";
        dgvDifferences.ReadOnly = true;
        dgvDifferences.RowHeadersWidth = 60;
        dgvDifferences.RowTemplate.Height = 25;
        dgvDifferences.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvDifferences.Size = new Size(1214, 609);
        dgvDifferences.TabIndex = 0;
        // 
        // CompareTablesForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1222, 757);
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
        pnlResults.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvDifferences).EndInit();
        ResumeLayout(false);
    }
}
