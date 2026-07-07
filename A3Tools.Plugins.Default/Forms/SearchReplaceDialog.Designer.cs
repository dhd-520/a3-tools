namespace A3Tools.Plugins.Default.Forms;

partial class SearchReplaceDialog
{
    private System.ComponentModel.IContainer components = null;

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
        this.lblFind = new System.Windows.Forms.Label();
        this.txtFind = new System.Windows.Forms.TextBox();
        this.lblReplace = new System.Windows.Forms.Label();
        this.txtReplace = new System.Windows.Forms.TextBox();
        this.chkCase = new System.Windows.Forms.CheckBox();
        this.btnFindNext = new System.Windows.Forms.Button();
        this.btnFindPrevious = new System.Windows.Forms.Button();
        this.btnReplace = new System.Windows.Forms.Button();
        this.btnReplaceAll = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.SuspendLayout();
        // 
        // lblFind
        // 
        this.lblFind.AutoSize = true;
        this.lblFind.Location = new System.Drawing.Point(12, 15);
        this.lblFind.Name = "lblFind";
        this.lblFind.Size = new System.Drawing.Size(44, 17);
        this.lblFind.TabIndex = 0;
        this.lblFind.Text = "查找(&F):";
        // 
        // txtFind
        // 
        this.txtFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.txtFind.Location = new System.Drawing.Point(70, 12);
        this.txtFind.Name = "txtFind";
        this.txtFind.Size = new System.Drawing.Size(280, 23);
        this.txtFind.TabIndex = 1;
        this.txtFind.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtFind_KeyDown);
        // 
        // lblReplace
        // 
        this.lblReplace.AutoSize = true;
        this.lblReplace.Location = new System.Drawing.Point(12, 45);
        this.lblReplace.Name = "lblReplace";
        this.lblReplace.Size = new System.Drawing.Size(44, 17);
        this.lblReplace.TabIndex = 2;
        this.lblReplace.Text = "替换(&R):";
        // 
        // txtReplace
        // 
        this.txtReplace.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.txtReplace.Location = new System.Drawing.Point(70, 42);
        this.txtReplace.Name = "txtReplace";
        this.txtReplace.Size = new System.Drawing.Size(280, 23);
        this.txtReplace.TabIndex = 3;
        // 
        // chkCase
        // 
        this.chkCase.AutoSize = true;
        this.chkCase.Location = new System.Drawing.Point(70, 72);
        this.chkCase.Name = "chkCase";
        this.chkCase.Size = new System.Drawing.Size(84, 21);
        this.chkCase.TabIndex = 4;
        this.chkCase.Text = "区分大小写(&C)";
        this.chkCase.UseVisualStyleBackColor = true;
        // 
        // btnFindNext
        // 
        this.btnFindNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnFindNext.Location = new System.Drawing.Point(366, 10);
        this.btnFindNext.Name = "btnFindNext";
        this.btnFindNext.Size = new System.Drawing.Size(88, 27);
        this.btnFindNext.TabIndex = 5;
        this.btnFindNext.Text = "查找下一个(&N)";
        this.btnFindNext.UseVisualStyleBackColor = true;
        this.btnFindNext.Click += new System.EventHandler(this.BtnFindNext_Click);
        // 
        // btnFindPrevious
        // 
        this.btnFindPrevious.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnFindPrevious.Location = new System.Drawing.Point(366, 43);
        this.btnFindPrevious.Name = "btnFindPrevious";
        this.btnFindPrevious.Size = new System.Drawing.Size(88, 27);
        this.btnFindPrevious.TabIndex = 6;
        this.btnFindPrevious.Text = "查找上一个(&P)";
        this.btnFindPrevious.UseVisualStyleBackColor = true;
        this.btnFindPrevious.Click += new System.EventHandler(this.BtnFindPrevious_Click);
        // 
        // btnReplace
        // 
        this.btnReplace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnReplace.Location = new System.Drawing.Point(366, 76);
        this.btnReplace.Name = "btnReplace";
        this.btnReplace.Size = new System.Drawing.Size(88, 27);
        this.btnReplace.TabIndex = 7;
        this.btnReplace.Text = "替换(&E)";
        this.btnReplace.UseVisualStyleBackColor = true;
        this.btnReplace.Click += new System.EventHandler(this.BtnReplace_Click);
        // 
        // btnReplaceAll
        // 
        this.btnReplaceAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnReplaceAll.Location = new System.Drawing.Point(366, 109);
        this.btnReplaceAll.Name = "btnReplaceAll";
        this.btnReplaceAll.Size = new System.Drawing.Size(88, 27);
        this.btnReplaceAll.TabIndex = 8;
        this.btnReplaceAll.Text = "全部替换(&A)";
        this.btnReplaceAll.UseVisualStyleBackColor = true;
        this.btnReplaceAll.Click += new System.EventHandler(this.BtnReplaceAll_Click);
        // 
        // btnCancel
        // 
        this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.btnCancel.Location = new System.Drawing.Point(366, 142);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(88, 27);
        this.btnCancel.TabIndex = 9;
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
        // 
        // SearchReplaceDialog
        // 
        this.AcceptButton = this.btnFindNext;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.CancelButton = this.btnCancel;
        this.ClientSize = new System.Drawing.Size(466, 181);
        this.Controls.Add(this.btnCancel);
        this.Controls.Add(this.btnReplaceAll);
        this.Controls.Add(this.btnReplace);
        this.Controls.Add(this.btnFindPrevious);
        this.Controls.Add(this.btnFindNext);
        this.Controls.Add(this.chkCase);
        this.Controls.Add(this.txtReplace);
        this.Controls.Add(this.lblReplace);
        this.Controls.Add(this.txtFind);
        this.Controls.Add(this.lblFind);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "SearchReplaceDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "查找";
        this.Load += new System.EventHandler(this.SearchReplaceDialog_Load);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Label lblFind;
    private System.Windows.Forms.TextBox txtFind;
    private System.Windows.Forms.Label lblReplace;
    private System.Windows.Forms.TextBox txtReplace;
    private System.Windows.Forms.CheckBox chkCase;
    private System.Windows.Forms.Button btnFindNext;
    private System.Windows.Forms.Button btnFindPrevious;
    private System.Windows.Forms.Button btnReplace;
    private System.Windows.Forms.Button btnReplaceAll;
    private System.Windows.Forms.Button btnCancel;
}
