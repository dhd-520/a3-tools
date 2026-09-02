using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// 简单文本输入对话框
/// ★ 2026-09-01 用于知识库管理(新建名称/重命名等场景)
/// </summary>
public static class Prompt
{
    public static string ShowDialog(string text, string caption, IWin32Window? owner = null, string defaultValue = "")
    {
        using var form = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(420, 130),
        };
        var lbl = new Label
        {
            Text = text,
            Location = new Point(12, 12),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F),
        };
        var txt = new TextBox
        {
            Location = new Point(12, 48),
            Width = 396,
            Font = new Font("Microsoft YaHei UI", 10F),
            Text = defaultValue,
        };
        var btnOk = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Location = new Point(232, 88),
            Size = new Size(80, 32),
        };
        var btnCancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(320, 88),
            Size = new Size(80, 32),
        };
        form.Controls.Add(lbl);
        form.Controls.Add(txt);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text : "";
    }
}