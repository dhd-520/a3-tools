namespace A3Tools.Plugins.Default.Forms;

partial class A3DesDecryptForm
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
        this.grpInput = new System.Windows.Forms.GroupBox();
        this.lblCipher = new System.Windows.Forms.Label();
        this.txtCipher = new System.Windows.Forms.TextBox();
        this.btnDecrypt = new System.Windows.Forms.Button();
        this.btnFromFile = new System.Windows.Forms.Button();
        this.btnClear = new System.Windows.Forms.Button();
        this.btnCopy = new System.Windows.Forms.Button();
        this.lblResult = new System.Windows.Forms.Label();
        this.txtOutput = new System.Windows.Forms.TextBox();
        this.lblStatus = new System.Windows.Forms.Label();
        this.grpInput.SuspendLayout();
        this.SuspendLayout();
        //
        // grpInput
        //
        this.grpInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.grpInput.Controls.Add(this.txtCipher);
        this.grpInput.Controls.Add(this.lblCipher);
        this.grpInput.Location = new System.Drawing.Point(12, 12);
        this.grpInput.Name = "grpInput";
        this.grpInput.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
        this.grpInput.Size = new System.Drawing.Size(752, 156);
        this.grpInput.TabIndex = 0;
        this.grpInput.TabStop = false;
        this.grpInput.Text = "密文输入";
        //
        // lblCipher
        //
        this.lblCipher.AutoSize = true;
        this.lblCipher.Location = new System.Drawing.Point(9, 22);
        this.lblCipher.Name = "lblCipher";
        this.lblCipher.Size = new System.Drawing.Size(299, 17);
        this.lblCipher.TabIndex = 0;
        this.lblCipher.Text = "粘贴 A3 密文，每行一条（支持多行批量解密）";
        //
        // txtCipher
        //
        this.txtCipher.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom)));
        this.txtCipher.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtCipher.Location = new System.Drawing.Point(9, 45);
        this.txtCipher.Multiline = true;
        this.txtCipher.Name = "txtCipher";
        this.txtCipher.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtCipher.Size = new System.Drawing.Size(734, 91);
        this.txtCipher.TabIndex = 1;
        //
        // btnDecrypt
        //
        this.btnDecrypt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
        this.btnDecrypt.Location = new System.Drawing.Point(12, 178);
        this.btnDecrypt.Name = "btnDecrypt";
        this.btnDecrypt.Size = new System.Drawing.Size(96, 28);
        this.btnDecrypt.TabIndex = 1;
        this.btnDecrypt.Text = "解密(&D)";
        this.btnDecrypt.UseVisualStyleBackColor = true;
        this.btnDecrypt.Click += new System.EventHandler(this.BtnDecrypt_Click);
        //
        // btnFromFile
        //
        this.btnFromFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
        this.btnFromFile.Location = new System.Drawing.Point(114, 178);
        this.btnFromFile.Name = "btnFromFile";
        this.btnFromFile.Size = new System.Drawing.Size(150, 28);
        this.btnFromFile.TabIndex = 2;
        this.btnFromFile.Text = "读取 Account.info(&F)";
        this.btnFromFile.UseVisualStyleBackColor = true;
        this.btnFromFile.Click += new System.EventHandler(this.BtnFromFile_Click);
        //
        // btnClear
        //
        this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
        this.btnClear.Location = new System.Drawing.Point(270, 178);
        this.btnClear.Name = "btnClear";
        this.btnClear.Size = new System.Drawing.Size(96, 28);
        this.btnClear.TabIndex = 3;
        this.btnClear.Text = "清空(&C)";
        this.btnClear.UseVisualStyleBackColor = true;
        this.btnClear.Click += new System.EventHandler(this.BtnClear_Click);
        //
        // btnCopy
        //
        this.btnCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnCopy.Location = new System.Drawing.Point(668, 178);
        this.btnCopy.Name = "btnCopy";
        this.btnCopy.Size = new System.Drawing.Size(96, 28);
        this.btnCopy.TabIndex = 4;
        this.btnCopy.Text = "复制结果(&P)";
        this.btnCopy.UseVisualStyleBackColor = true;
        this.btnCopy.Click += new System.EventHandler(this.BtnCopy_Click);
        //
        // lblResult
        //
        this.lblResult.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
        this.lblResult.AutoSize = true;
        this.lblResult.Location = new System.Drawing.Point(12, 216);
        this.lblResult.Name = "lblResult";
        this.lblResult.Size = new System.Drawing.Size(68, 17);
        this.lblResult.TabIndex = 5;
        this.lblResult.Text = "解密结果：";
        //
        // txtOutput
        //
        this.txtOutput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom)));
        this.txtOutput.BackColor = System.Drawing.SystemColors.Window;
        this.txtOutput.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtOutput.Location = new System.Drawing.Point(12, 240);
        this.txtOutput.Multiline = true;
        this.txtOutput.Name = "txtOutput";
        this.txtOutput.ReadOnly = true;
        this.txtOutput.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        this.txtOutput.Size = new System.Drawing.Size(752, 236);
        this.txtOutput.TabIndex = 6;
        this.txtOutput.WordWrap = false;
        //
        // lblStatus
        //
        this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new System.Drawing.Point(12, 484);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(431, 17);
        this.lblStatus.TabIndex = 7;
        this.lblStatus.Text = "算法：DES / CBC / PKCS7（密钥取自 A3.Common.Util 的 A3DESEncrypt）";
        //
        // A3DesDecryptForm
        //
        this.AcceptButton = this.btnDecrypt;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(776, 508);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.txtOutput);
        this.Controls.Add(this.lblResult);
        this.Controls.Add(this.btnCopy);
        this.Controls.Add(this.btnClear);
        this.Controls.Add(this.btnFromFile);
        this.Controls.Add(this.btnDecrypt);
        this.Controls.Add(this.grpInput);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "A3DesDecryptForm";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "A3 密文解密";
        this.grpInput.ResumeLayout(false);
        this.grpInput.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.GroupBox grpInput;
    private System.Windows.Forms.Label lblCipher;
    private System.Windows.Forms.TextBox txtCipher;
    private System.Windows.Forms.Button btnDecrypt;
    private System.Windows.Forms.Button btnFromFile;
    private System.Windows.Forms.Button btnClear;
    private System.Windows.Forms.Button btnCopy;
    private System.Windows.Forms.Label lblResult;
    private System.Windows.Forms.TextBox txtOutput;
    private System.Windows.Forms.Label lblStatus;
}