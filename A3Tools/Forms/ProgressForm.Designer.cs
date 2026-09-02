namespace A3Tools.Forms;

partial class ProgressForm
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
        this.lblTitle = new System.Windows.Forms.Label();
        this.lblCurrent = new System.Windows.Forms.Label();
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.lblStatus = new System.Windows.Forms.Label();
        this.btnCancel = new System.Windows.Forms.Button();

        this.SuspendLayout();

        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold);
        this.lblTitle.Location = new System.Drawing.Point(12, 12);
        this.lblTitle.Text = "处理中...";

        this.lblCurrent.AutoSize = true;
        this.lblCurrent.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.lblCurrent.Location = new System.Drawing.Point(12, 48);
        this.lblCurrent.Text = "准备开始...";
        this.lblCurrent.ForeColor = System.Drawing.Color.FromArgb(60, 60, 60);

        this.progressBar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.progressBar.Location = new System.Drawing.Point(12, 76);
        this.progressBar.Size = new System.Drawing.Size(460, 24);

        this.lblStatus.AutoSize = true;
        this.lblStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.lblStatus.Location = new System.Drawing.Point(12, 108);
        this.lblStatus.Text = "0%";
        this.lblStatus.ForeColor = System.Drawing.Color.Gray;

        this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnCancel.Location = new System.Drawing.Point(392, 142);
        this.btnCancel.Size = new System.Drawing.Size(80, 32);
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);

        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(484, 188);
        this.Controls.Add(this.btnCancel);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.progressBar);
        this.Controls.Add(this.lblCurrent);
        this.Controls.Add(this.lblTitle);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ProgressForm";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "进度";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ProgressForm_FormClosing);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblCurrent;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Button btnCancel;
}