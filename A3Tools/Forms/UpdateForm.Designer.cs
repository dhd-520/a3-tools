namespace A3Tools.Forms;

partial class UpdateForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTitle = new System.Windows.Forms.Label();
        this.lblVersion = new System.Windows.Forms.Label();
        this.lblDate = new System.Windows.Forms.Label();
        this.lblCurrent = new System.Windows.Forms.Label();
        this.lblSize = new System.Windows.Forms.Label();
        this.txtBody = new System.Windows.Forms.TextBox();
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.lblProgress = new System.Windows.Forms.Label();
        this.btnUpdate = new System.Windows.Forms.Button();
        this.btnLater = new System.Windows.Forms.Button();
        this.btnViewRelease = new System.Windows.Forms.Button();
        this.SuspendLayout();
        // 
        // lblTitle
        // 
        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        this.lblTitle.Location = new System.Drawing.Point(20, 18);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new System.Drawing.Size(180, 27);
        this.lblTitle.Text = "发现新版本";
        // 
        // lblVersion
        // 
        this.lblVersion.AutoSize = true;
        this.lblVersion.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Bold);
        this.lblVersion.ForeColor = System.Drawing.Color.FromArgb(24, 144, 255);
        this.lblVersion.Location = new System.Drawing.Point(20, 50);
        this.lblVersion.Name = "lblVersion";
        this.lblVersion.Size = new System.Drawing.Size(80, 26);
        this.lblVersion.Text = "v2.2.0";
        // 
        // lblDate
        // 
        this.lblDate.AutoSize = true;
        this.lblDate.Location = new System.Drawing.Point(20, 82);
        this.lblDate.Name = "lblDate";
        this.lblDate.Size = new System.Drawing.Size(220, 17);
        this.lblDate.Text = "发布时间：2026-07-07 16:00";
        // 
        // lblCurrent
        // 
        this.lblCurrent.AutoSize = true;
        this.lblCurrent.Location = new System.Drawing.Point(20, 102);
        this.lblCurrent.Name = "lblCurrent";
        this.lblCurrent.Size = new System.Drawing.Size(160, 17);
        this.lblCurrent.Text = "当前版本：2.1.0";
        // 
        // lblSize
        // 
        this.lblSize.AutoSize = true;
        this.lblSize.Location = new System.Drawing.Point(20, 122);
        this.lblSize.Name = "lblSize";
        this.lblSize.Size = new System.Drawing.Size(180, 17);
        this.lblSize.Text = "更新包大小：75 MB";
        // 
        // txtBody
        // 
        this.txtBody.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.txtBody.BackColor = System.Drawing.Color.White;
        this.txtBody.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.txtBody.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.txtBody.Location = new System.Drawing.Point(20, 150);
        this.txtBody.Multiline = true;
        this.txtBody.Name = "txtBody";
        this.txtBody.ReadOnly = true;
        this.txtBody.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtBody.Size = new System.Drawing.Size(540, 280);
        this.txtBody.TabIndex = 0;
        // 
        // progressBar
        // 
        this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.progressBar.Location = new System.Drawing.Point(20, 445);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(540, 22);
        this.progressBar.TabIndex = 1;
        this.progressBar.Visible = false;
        // 
        // lblProgress
        // 
        this.lblProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.lblProgress.AutoSize = true;
        this.lblProgress.Location = new System.Drawing.Point(20, 472);
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.Size = new System.Drawing.Size(0, 17);
        this.lblProgress.Text = "";
        this.lblProgress.Visible = false;
        // 
        // btnUpdate
        // 
        this.btnUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.btnUpdate.BackColor = System.Drawing.Color.FromArgb(24, 144, 255);
        this.btnUpdate.FlatAppearance.BorderSize = 0;
        this.btnUpdate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnUpdate.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.btnUpdate.ForeColor = System.Drawing.Color.White;
        this.btnUpdate.Location = new System.Drawing.Point(304, 495);
        this.btnUpdate.Name = "btnUpdate";
        this.btnUpdate.Size = new System.Drawing.Size(120, 38);
        this.btnUpdate.TabIndex = 2;
        this.btnUpdate.Text = "立即更新";
        this.btnUpdate.UseVisualStyleBackColor = false;
        this.btnUpdate.Click += new System.EventHandler(this.BtnUpdate_Click);
        // 
        // btnLater
        // 
        this.btnLater.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.btnLater.Location = new System.Drawing.Point(430, 495);
        this.btnLater.Name = "btnLater";
        this.btnLater.Size = new System.Drawing.Size(80, 38);
        this.btnLater.TabIndex = 3;
        this.btnLater.Text = "稍后";
        this.btnLater.UseVisualStyleBackColor = true;
        this.btnLater.Click += new System.EventHandler(this.BtnLater_Click);
        // 
        // btnViewRelease
        // 
        this.btnViewRelease.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
        this.btnViewRelease.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnViewRelease.Location = new System.Drawing.Point(20, 495);
        this.btnViewRelease.Name = "btnViewRelease";
        this.btnViewRelease.Size = new System.Drawing.Size(120, 38);
        this.btnViewRelease.TabIndex = 4;
        this.btnViewRelease.Text = "查看完整说明";
        this.btnViewRelease.UseVisualStyleBackColor = true;
        this.btnViewRelease.Click += new System.EventHandler(this.BtnViewRelease_Click);
        // 
        // UpdateForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(580, 550);
        this.Controls.Add(this.btnViewRelease);
        this.Controls.Add(this.btnLater);
        this.Controls.Add(this.btnUpdate);
        this.Controls.Add(this.lblProgress);
        this.Controls.Add(this.progressBar);
        this.Controls.Add(this.txtBody);
        this.Controls.Add(this.lblSize);
        this.Controls.Add(this.lblCurrent);
        this.Controls.Add(this.lblDate);
        this.Controls.Add(this.lblVersion);
        this.Controls.Add(this.lblTitle);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "UpdateForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "软件更新";
        this.Load += new System.EventHandler(this.UpdateForm_Load);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblVersion;
    private System.Windows.Forms.Label lblDate;
    private System.Windows.Forms.Label lblCurrent;
    private System.Windows.Forms.Label lblSize;
    private System.Windows.Forms.TextBox txtBody;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblProgress;
    private System.Windows.Forms.Button btnUpdate;
    private System.Windows.Forms.Button btnLater;
    private System.Windows.Forms.Button btnViewRelease;
}
