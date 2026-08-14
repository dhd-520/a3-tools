namespace A3Tools.Forms
{
    /// <summary>
    /// ★ 2026-08-14 10:28 陛下反馈:VS 设计模式打不开 LaunchOptionsDialog。
    ///   原因:控件初始化全部在 LaunchOptionsDialog.cs 一个文件里手写,
    ///     VS 设计器要求 partial class,需要配套的 .Designer.cs 才能识别。
    ///   修复:拆成 partial class。本文件负责所有控件字段声明 + InitializeComponent,
    ///     LaunchOptionsDialog.cs 留业务代码(属性/事件/方法/BrowserItem)。
    ///   重构后:VS 设计器可以正常打开本窗体,所见即所得。
    /// </summary>
    partial class LaunchOptionsDialog
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

        #region Windows 窗体设计器生成的代码

        // 控件字段(全部挪到 Designer,跟 .NET WinForms 标准一致)
        private Panel titleBar;
        private Label lblTitle;
        private Panel accountBanner;
        private Label lblAccountInfo;
        private Panel content;
        private Label lblHint;
        private CheckBox chkDesktop;
        private CheckBox chkDevTools;
        private CheckBox chkErp;
        private CheckBox chkWechatWork;
        private Label lblBrowser;
        private ComboBox cboBrowser;
        private Panel bottom;
        private Button btnOK;
        private Button btnCancel;

        private void InitializeComponent()
        {
            titleBar = new Panel();
            lblTitle = new Label();
            accountBanner = new Panel();
            lblAccountInfo = new Label();
            content = new Panel();
            lblHint = new Label();
            chkDesktop = new CheckBox();
            chkDevTools = new CheckBox();
            chkErp = new CheckBox();
            chkWechatWork = new CheckBox();
            lblBrowser = new Label();
            cboBrowser = new ComboBox();
            bottom = new Panel();
            btnOK = new Button();
            btnCancel = new Button();
            titleBar.SuspendLayout();
            accountBanner.SuspendLayout();
            content.SuspendLayout();
            bottom.SuspendLayout();
            SuspendLayout();
            // 
            // titleBar
            // 
            titleBar.BackColor = Color.FromArgb(24, 145, 176);
            titleBar.Controls.Add(lblTitle);
            titleBar.Dock = DockStyle.Top;
            titleBar.Location = new Point(0, 0);
            titleBar.Margin = new Padding(4, 5, 4, 5);
            titleBar.Name = "titleBar";
            titleBar.Size = new Size(780, 93);
            titleBar.TabIndex = 3;
            // 
            // lblTitle
            // 
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new Font("微软雅黑", 14F, FontStyle.Bold, GraphicsUnit.Point);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(0, 0);
            lblTitle.Margin = new Padding(4, 0, 4, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Padding = new Padding(35, 0, 0, 0);
            lblTitle.Size = new Size(780, 93);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "🚀 选择启动选项";
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // accountBanner
            // 
            accountBanner.BackColor = Color.FromArgb(232, 244, 248);
            accountBanner.Controls.Add(lblAccountInfo);
            accountBanner.Dock = DockStyle.Top;
            accountBanner.Location = new Point(0, 93);
            accountBanner.Margin = new Padding(4, 5, 4, 5);
            accountBanner.Name = "accountBanner";
            accountBanner.Size = new Size(780, 68);
            accountBanner.TabIndex = 2;
            // 
            // lblAccountInfo
            // 
            lblAccountInfo.Dock = DockStyle.Fill;
            lblAccountInfo.Font = new Font("微软雅黑", 10F, FontStyle.Bold, GraphicsUnit.Point);
            lblAccountInfo.ForeColor = Color.FromArgb(24, 145, 176);
            lblAccountInfo.Location = new Point(0, 0);
            lblAccountInfo.Margin = new Padding(4, 0, 4, 0);
            lblAccountInfo.Name = "lblAccountInfo";
            lblAccountInfo.Padding = new Padding(52, 0, 52, 0);
            lblAccountInfo.Size = new Size(780, 68);
            lblAccountInfo.TabIndex = 0;
            lblAccountInfo.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // content
            // 
            content.Controls.Add(lblHint);
            content.Controls.Add(chkDesktop);
            content.Controls.Add(chkDevTools);
            content.Controls.Add(chkErp);
            content.Controls.Add(chkWechatWork);
            content.Controls.Add(lblBrowser);
            content.Controls.Add(cboBrowser);
            content.Dock = DockStyle.Fill;
            content.Location = new Point(0, 161);
            content.Margin = new Padding(4, 5, 4, 5);
            content.Name = "content";
            content.Padding = new Padding(52, 37, 52, 19);
            content.Size = new Size(780, 648);
            content.TabIndex = 1;
            // 
            // lblHint
            // 
            lblHint.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblHint.Location = new Point(0, 0);
            lblHint.Margin = new Padding(4, 0, 4, 0);
            lblHint.Name = "lblHint";
            lblHint.Size = new Size(664, 47);
            lblHint.TabIndex = 0;
            lblHint.Text = "选择要启动的程序（已记住上次选择）：";
            // 
            // chkDesktop
            // 
            chkDesktop.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            chkDesktop.Location = new Point(26, 62);
            chkDesktop.Margin = new Padding(4, 5, 4, 5);
            chkDesktop.Name = "chkDesktop";
            chkDesktop.Size = new Size(638, 56);
            chkDesktop.TabIndex = 1;
            chkDesktop.Text = "启动电脑端（君则A3.exe）";
            // 
            // chkDevTools
            // 
            chkDevTools.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            chkDevTools.Location = new Point(26, 137);
            chkDevTools.Margin = new Padding(4, 5, 4, 5);
            chkDevTools.Name = "chkDevTools";
            chkDevTools.Size = new Size(638, 56);
            chkDevTools.TabIndex = 2;
            chkDevTools.Text = "启动开发工具（君则A3集成开发工具.exe）";
            // 
            // chkErp
            //
            chkErp.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            chkErp.Location = new Point(26, 280);
            chkErp.Margin = new Padding(4, 5, 4, 5);
            chkErp.Name = "chkErp";
            chkErp.Size = new Size(638, 56);
            chkErp.TabIndex = 6;
            chkErp.Text = "启动ERP网页版（h5comerp）";

            //
            // chkWechatWork
            //
            chkWechatWork.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            chkWechatWork.Location = new Point(26, 345);
            chkWechatWork.Margin = new Padding(4, 5, 4, 5);
            chkWechatWork.Name = "chkWechatWork";
            chkWechatWork.Size = new Size(638, 56);
            chkWechatWork.TabIndex = 7;
            chkWechatWork.Text = "启动企业微信网页版（h5apperp）";
            // 
            // lblBrowser
            // 
            lblBrowser.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lblBrowser.Location = new Point(43, 212);
            lblBrowser.Margin = new Padding(4, 0, 4, 0);
            lblBrowser.Name = "lblBrowser";
            lblBrowser.Size = new Size(173, 56);
            lblBrowser.TabIndex = 4;
            lblBrowser.Text = "选择浏览器：";
            lblBrowser.TextAlign = ContentAlignment.MiddleLeft;
            //
            // cboBrowser
            //
            cboBrowser.DropDownStyle = ComboBoxStyle.DropDownList;
            cboBrowser.FlatStyle = FlatStyle.Flat;
            cboBrowser.Font = new Font("微软雅黑", 11F, FontStyle.Regular, GraphicsUnit.Point);
            cboBrowser.Location = new Point(217, 215);
            cboBrowser.Margin = new Padding(4, 5, 4, 5);
            cboBrowser.Name = "cboBrowser";
            cboBrowser.Size = new Size(403, 43);
            cboBrowser.TabIndex = 5;
            // 
            // bottom
            // 
            bottom.BackColor = Color.FromArgb(248, 248, 248);
            bottom.Controls.Add(btnOK);
            bottom.Controls.Add(btnCancel);
            bottom.Dock = DockStyle.Bottom;
            bottom.Location = new Point(0, 697);
            bottom.Margin = new Padding(4, 5, 4, 5);
            bottom.Name = "bottom";
            bottom.Size = new Size(780, 112);
            bottom.TabIndex = 0;
            bottom.Resize += Bottom_Resize;
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOK.BackColor = Color.FromArgb(24, 145, 176);
            btnOK.Cursor = Cursors.Hand;
            btnOK.DialogResult = DialogResult.OK;
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.FlatStyle = FlatStyle.Flat;
            btnOK.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            btnOK.ForeColor = Color.White;
            btnOK.Location = new Point(491, 0);
            btnOK.Margin = new Padding(4, 5, 4, 5);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(173, 65);
            btnOK.TabIndex = 0;
            btnOK.Text = "启动";
            btnOK.UseVisualStyleBackColor = false;
            btnOK.Click += BtnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.BackColor = Color.White;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
            btnCancel.Location = new Point(491, 0);
            btnCancel.Margin = new Padding(4, 5, 4, 5);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(173, 65);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "取消";
            btnCancel.UseVisualStyleBackColor = false;
            // 
            // LaunchOptionsDialog
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(13F, 28F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            CancelButton = btnCancel;
            ClientSize = new Size(780, 809);
            Controls.Add(bottom);
            Controls.Add(content);
            Controls.Add(accountBanner);
            Controls.Add(titleBar);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyPreview = true;
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "LaunchOptionsDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "选择启动选项";
            KeyDown += LaunchOptionsDialog_KeyDown;
            titleBar.ResumeLayout(false);
            accountBanner.ResumeLayout(false);
            content.ResumeLayout(false);
            bottom.ResumeLayout(false);
            ResumeLayout(false);
        }

        private void Bottom_Resize(object? sender, EventArgs e)
        {
            btnCancel.Left = bottom.Width - 36 - btnCancel.Width;
            btnOK.Left = btnCancel.Left - 12 - btnOK.Width;
        }

        #endregion
    }
}