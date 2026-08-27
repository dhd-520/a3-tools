using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// AI Action 二次确认弹窗
/// <para>★ 2026-08-26 R2-C：所有非只读 Action 都要走这个弹窗</para>
/// <para>支持两种模式：</para>
/// <list type="bullet">
///   <item>普通确认：只显示 [允许] / [拒绝]</item>
///   <item>高危确认：要求陛下输入指定文本（防误操作）</item>
/// </list>
/// </summary>
public class ConfirmActionForm : Form
{
    /// <summary>用户是否同意执行</summary>
    public bool Confirmed { get; private set; } = false;

    private readonly string _actionName;
    private readonly string _impact;
    private readonly string? _confirmText;
    private readonly string _userInput;

    private Label lblHeader = null!;
    private Label lblAction = null!;
    private Label lblImpact = null!;
    private Label lblArgs = null!;
    private Label lblConfirmPrompt = null!;
    private TextBox txtConfirm = null!;
    private Button btnConfirm = null!;
    private Button btnCancel = null!;

    /// <summary>
    /// 显示确认弹窗（modal，阻塞调用方直到用户点按钮）
    /// </summary>
    /// <param name="actionName">AI 想做的操作名（中文）</param>
    /// <param name="impact">影响描述（中文）</param>
    /// <param name="argumentsJson">AI 传来的参数（JSON 字符串，展示给陛下看）</param>
    /// <param name="confirmText">高危：要求陛下输入这个文本才放行；null = 普通确认</param>
    public ConfirmActionForm(string actionName, string impact, string argumentsJson, string? confirmText)
    {
        _actionName = actionName;
        _impact = impact;
        _confirmText = confirmText;
        _userInput = confirmText ?? string.Empty;

        InitializeComponent();
        lblAction.Text = $"操作：{actionName}";
        lblImpact.Text = impact;
        lblArgs.Text = $"参数：{(string.IsNullOrWhiteSpace(argumentsJson) ? "（无）" : argumentsJson)}";

        if (_confirmText != null)
        {
            lblConfirmPrompt.Visible = true;
            txtConfirm.Visible = true;
            lblConfirmPrompt.Text = $"请输入 [ {_confirmText} ] 以确认：";
            btnConfirm.Text = "确认执行";
        }

        // 默认焦点：取消按钮（防误操作）
        AcceptButton = btnCancel;
        btnCancel.Focus();
    }

    private void InitializeComponent()
    {
        Text = _confirmText == null ? "AI 操作确认" : "高危操作确认";
        Size = new Size(540, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9.5F);

        // 警告色顶部条
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = _confirmText == null ? Color.FromArgb(255, 192, 0) : Color.FromArgb(251, 67, 42)
        };
        lblHeader = new Label
        {
            Text = _confirmText == null ? "⚠ 需要陛下确认" : "� 高危操作",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 10)
        };
        pnlHeader.Controls.Add(lblHeader);

        // 内容区
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 12)
        };

        lblAction = new Label
        {
            Location = new Point(16, 12),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };

        lblImpact = new Label
        {
            Location = new Point(16, 42),
            Size = new Size(490, 60),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        lblArgs = new Label
        {
            Location = new Point(16, 108),
            Size = new Size(490, 60),
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Consolas", 8.5F)
        };

        lblConfirmPrompt = new Label
        {
            Location = new Point(16, 175),
            AutoSize = true,
            Visible = false,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(251, 67, 42)
        };

        txtConfirm = new TextBox
        {
            Location = new Point(16, 198),
            Size = new Size(490, 28),
            Visible = false,
            Font = new Font("Consolas", 11F, FontStyle.Bold)
        };
        txtConfirm.TextChanged += (_, _) =>
        {
            btnConfirm.Enabled = txtConfirm.Text.Trim() == _userInput;
            if (btnConfirm.Enabled)
                btnConfirm.BackColor = Color.FromArgb(251, 67, 42);
            else
                btnConfirm.BackColor = Color.FromArgb(200, 200, 200);
        };

        pnlBody.Controls.AddRange(new Control[]
        {
            lblAction, lblImpact, lblArgs, lblConfirmPrompt, txtConfirm
        });

        // 按钮
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(16, 8, 16, 8)
        };

        btnConfirm = new Button
        {
            Text = "允许执行",
            Size = new Size(110, 36),
            Location = new Point(290, 10),
            BackColor = Color.FromArgb(57, 181, 74),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Enabled = _confirmText == null // 高危时要等输入正确才启用
        };
        btnConfirm.FlatAppearance.BorderSize = 0;
        btnConfirm.Click += (_, _) =>
        {
            if (_confirmText != null && txtConfirm.Text.Trim() != _confirmText)
            {
                MessageBox.Show($"输入不正确，请输入 [ {_confirmText} ]", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        btnCancel = new Button
        {
            Text = "拒绝",
            Size = new Size(110, 36),
            Location = new Point(410, 10),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(80, 80, 80),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
        btnCancel.Click += (_, _) =>
        {
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        pnlBottom.Controls.AddRange(new Control[] { btnConfirm, btnCancel });

        Controls.Add(pnlBody);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { Confirmed = false; DialogResult = DialogResult.Cancel; Close(); }
        };
    }
}
