using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Forms
{
    /// <summary>
    /// ★ 2026-08-13 13:44 A3 程序更新确认窗(场景 3 用)。
    /// 陛下 11:00/13:44 反馈:MessageBox.Show(this, ...) 弹出来但看不见。
    /// 原因:MessageBox 的 z-order/位置依赖 owner,MainForm 已隐藏到托盘导致弹到不可见位置。
    /// 修复:用自定义 Form 强制 TopMost + 屏幕中央,保证陛下一定能看见。
    /// </summary>
    public partial class A3UpdateConfirmForm : Form
    {
        public DialogResult Result { get; private set; } = DialogResult.None;

        public A3UpdateConfirmForm(string message, string title)
        {
            InitializeComponent();
            this.Text = title;
            this.lblMessage.Text = message;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;  // ★ 关键:永远在最前(覆盖 A3 升级框)
            this.ShowInTaskbar = true;  // ★ 关键:任务栏显示(陛下能切换)
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.btnYes.DialogResult = DialogResult.Yes;
            this.btnNo.DialogResult = DialogResult.No;
            this.AcceptButton = btnYes;
            this.CancelButton = btnNo;
            // 让 Form 主动 ShowDialog + Activate(确保在最前)
            this.Shown += (s, e) =>
            {
                this.Activate();
                this.BringToFront();
            };
        }

        private void InitializeComponent()
        {
            this.lblMessage = new System.Windows.Forms.Label();
            this.btnYes = new System.Windows.Forms.Button();
            this.btnNo = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Location = new System.Drawing.Point(20, 20);
            this.lblMessage.Size = new System.Drawing.Size(460, 120);
            this.lblMessage.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnYes
            //
            this.btnYes.Location = new System.Drawing.Point(220, 160);
            this.btnYes.Size = new System.Drawing.Size(100, 35);
            this.btnYes.Text = "是(&Y)";
            //
            // btnNo
            //
            this.btnNo.Location = new System.Drawing.Point(330, 160);
            this.btnNo.Size = new System.Drawing.Size(100, 35);
            this.btnNo.Text = "否(&N)";
            //
            // A3UpdateConfirmForm
            //
            this.ClientSize = new System.Drawing.Size(500, 210);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.btnYes);
            this.Controls.Add(this.btnNo);
            this.Name = "A3UpdateConfirmForm";
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.Button btnYes;
        private System.Windows.Forms.Button btnNo;
    }
}