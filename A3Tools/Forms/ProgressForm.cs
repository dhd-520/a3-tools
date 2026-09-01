using System;
using System.Threading;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// 进度窗 - 显示后台任务进度,支持取消
/// ★ 2026-09-01 用于知识库 AI 提取等耗时操作
/// </summary>
public partial class ProgressForm : Form
{
    private readonly CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;
    private bool _cancelling = false;

    public ProgressForm(string title = "处理中...")
    {
        InitializeComponent();
        lblTitle.Text = title;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
    }

    /// <summary>更新进度(线程安全)</summary>
    public void SetProgress(int index, int total, string fileName, string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetProgress(index, total, fileName, status)));
            return;
        }
        if (total > 0)
        {
            progressBar.Maximum = total;
            progressBar.Value = Math.Min(index, total);
            lblStatus.Text = $"{index} / {total}  ({index * 100 / total}%)";
        }
        lblCurrent.Text = $"[{status}] {fileName}";
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (_cancelling) return;
        _cancelling = true;
        _cts.Cancel();
        btnCancel.Enabled = false;
        btnCancel.Text = "取消中...";
        lblCurrent.Text = "正在取消,请稍候...";
    }

    private void ProgressForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // 强制取消时 X 也要取消 token
        if (e.CloseReason == CloseReason.UserClosing && !_cancelling)
        {
            _cts.Cancel();
        }
    }
}