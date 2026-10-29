namespace A3Tools.Forms;

partial class PromptInputForm
{
    private System.ComponentModel.IContainer components = null;

    private Label lblPrompt = null!;
    private TextBox txtInput = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblPrompt = new Label();
        txtInput = new TextBox();
        btnOk = new Button();
        btnCancel = new Button();
        SuspendLayout();
        //
        // lblPrompt
        //
        lblPrompt.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        lblPrompt.ForeColor = Color.FromArgb(80, 80, 80);
        lblPrompt.Location = new Point(20, 20);
        lblPrompt.Name = "lblPrompt";
        lblPrompt.Size = new Size(380, 40);
        lblPrompt.TabIndex = 0;
        lblPrompt.Text = "请输入：";
        //
        // txtInput
        //
        txtInput.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtInput.Location = new Point(20, 62);
        txtInput.Name = "txtInput";
        txtInput.Size = new Size(380, 30);
        txtInput.TabIndex = 1;
        //
        // btnOk
        //
        btnOk.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOk.BackColor = Color.FromArgb(24, 145, 176);
        btnOk.DialogResult = DialogResult.OK;
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.FlatStyle = FlatStyle.Flat;
        btnOk.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
        btnOk.ForeColor = Color.White;
        btnOk.Location = new Point(200, 108);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(90, 36);
        btnOk.TabIndex = 2;
        btnOk.Text = "确定";
        btnOk.UseVisualStyleBackColor = false;
        //
        // btnCancel
        //
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.BackColor = Color.White;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F);
        btnCancel.ForeColor = Color.FromArgb(80, 80, 80);
        btnCancel.Location = new Point(300, 108);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(90, 36);
        btnCancel.TabIndex = 3;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        //
        // PromptInputForm
        //
        AcceptButton = btnOk;
        // ★ 10pt 微软雅黑对应的设计尺寸是 (14F, 30F)（11pt 才是 16F,35F）
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        CancelButton = btnCancel;
        ClientSize = new Size(420, 160);
        Controls.Add(lblPrompt);
        Controls.Add(txtInput);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "PromptInputForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "输入";
        ResumeLayout(false);
    }
}
