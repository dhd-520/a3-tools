namespace A3Tools.Forms;

partial class ScanResultForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows 窗体设计器生成的代码

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.lblTitle = new System.Windows.Forms.Label();
        this.clbFiles = new System.Windows.Forms.CheckedListBox();
        this.btnSelectAll = new System.Windows.Forms.Button();
        this.btnSelectNone = new System.Windows.Forms.Button();
        this.btnOk = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.lblSummary = new System.Windows.Forms.Label();

        this.SuspendLayout();

        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.lblTitle.Location = new System.Drawing.Point(12, 12);
        this.lblTitle.Text = "📂 选择要导入的文件:";

        this.clbFiles.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.clbFiles.CheckOnClick = true;
        this.clbFiles.IntegralHeight = false;
        this.clbFiles.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
        this.clbFiles.Location = new System.Drawing.Point(12, 42);
        this.clbFiles.Size = new System.Drawing.Size(660, 400);
        this.clbFiles.ThreeDCheckBoxes = true;
        this.clbFiles.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.ClbFiles_ItemCheck);

        this.btnSelectAll.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.btnSelectAll.Location = new System.Drawing.Point(12, 458);
        this.btnSelectAll.Size = new System.Drawing.Size(90, 32);
        this.btnSelectAll.Text = "全选";
        this.btnSelectAll.UseVisualStyleBackColor = true;
        this.btnSelectAll.Click += new System.EventHandler(this.BtnSelectAll_Click);

        this.btnSelectNone.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.btnSelectNone.Location = new System.Drawing.Point(110, 458);
        this.btnSelectNone.Size = new System.Drawing.Size(90, 32);
        this.btnSelectNone.Text = "全不选";
        this.btnSelectNone.UseVisualStyleBackColor = true;
        this.btnSelectNone.Click += new System.EventHandler(this.BtnSelectNone_Click);

        this.lblSummary.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.lblSummary.AutoSize = true;
        this.lblSummary.Location = new System.Drawing.Point(210, 466);
        this.lblSummary.ForeColor = System.Drawing.Color.Gray;
        this.lblSummary.Text = "已选 0 / 0";

        this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnOk.Location = new System.Drawing.Point(490, 458);
        this.btnOk.Size = new System.Drawing.Size(90, 32);
        this.btnOk.Text = "✅ 导入选中";
        this.btnOk.UseVisualStyleBackColor = true;
        this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;

        this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnCancel.Location = new System.Drawing.Point(584, 458);
        this.btnCancel.Size = new System.Drawing.Size(90, 32);
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;

        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(684, 504);
        this.Controls.Add(this.clbFiles);
        this.Controls.Add(this.btnSelectAll);
        this.Controls.Add(this.btnSelectNone);
        this.Controls.Add(this.lblSummary);
        this.Controls.Add(this.btnOk);
        this.Controls.Add(this.btnCancel);
        this.Controls.Add(this.lblTitle);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "选择要导入的文件";
        this.Load += new System.EventHandler(this.ScanResultForm_Load);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.CheckedListBox clbFiles;
    private System.Windows.Forms.Button btnSelectAll;
    private System.Windows.Forms.Button btnSelectNone;
    private System.Windows.Forms.Button btnOk;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.Label lblSummary;
}