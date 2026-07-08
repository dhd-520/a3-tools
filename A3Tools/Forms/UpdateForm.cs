using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using A3Tools.Services;

namespace A3Tools.Forms;

/// <summary>
/// 更新提示窗（仿 VS Code / Discord 风格）
/// 标题栏显示"有新版本可用"
/// 内容：版本号 + 发布时间 + 发布说明（可滚动）
/// 按钮：[立即更新] [稍后] [查看完整说明]
/// 下载阶段：进度条 + 速度 + 百分比
/// </summary>
public partial class UpdateForm : Form
{
    private readonly UpdateInfo _update;
    private CancellationTokenSource? _cts;

    public UpdateForm(UpdateInfo update)
    {
        _update = update;
        InitializeComponent();
    }

    private void UpdateForm_Load(object? sender, EventArgs e)
    {
        string sourceText = _update.Source == UpdateSource.Gitee ? "Gitee" : "GitHub";
        lblTitle.Text = $"发现新版本：{_update.Name}";
        lblVersion.Text = $"v{_update.Version}";
        lblDate.Text = $"发布时间：{_update.PublishedAt.LocalDateTime:yyyy-MM-dd HH:mm}  ·  来源：{sourceText}";
        lblCurrent.Text = $"当前版本：{UpdateService.CurrentVersion}";

        // 把 markdown 风格的 body 简单转一下（去掉 # ## 等）
        var body = _update.Body ?? "";
        body = body.Replace("\r\n", "\n")
                   .Replace("**", "")
                   .Replace("`", "");
        txtBody.Text = body;

        // 大小
        if (_update.AssetSize > 0)
        {
            lblSize.Text = $"更新包大小：{FormatSize(_update.AssetSize)}";
        }
        else
        {
            lblSize.Text = "";
        }
    }

    private string FormatSize(long bytes)
    {
        if (bytes > 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.0} MB";
        if (bytes > 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_update.DownloadUrl))
        {
            MessageBox.Show("下载链接无效，请到 GitHub 手动下载。", "更新失败",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 切换到下载 UI
        btnUpdate.Enabled = false;
        btnLater.Enabled = false;
        btnViewRelease.Enabled = false;
        progressBar.Visible = true;
        lblProgress.Visible = true;
        progressBar.Value = 0;
        lblProgress.Text = _update.Source == UpdateSource.Gitee
            ? "从 Gitee 下载中..."
            : "从 GitHub 下载中...";

        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule!.FileName!;
            string currentDir = Path.GetDirectoryName(currentExe)!;
            // 按资产类型决定后缀：zip 必须 .zip，exe 才 .exe
            string ext = _update.IsZipPackage ? ".zip" : ".exe";
            string tempExe = Path.Combine(currentDir, $"A3Tools_{_update.Version}_new{ext}");

            // 【重试优化】检查本地是否已下载过同版本安装包
            //   - 如果文件存在且 size > 0 → 跳过下载，直接进入「确认替换」阶段
            //   - 如果 size 跟 _update.AssetSize 不一致（之前下载中断） → 删除重下
            bool alreadyDownloaded = false;
            if (File.Exists(tempExe))
            {
                long localSize = new FileInfo(tempExe).Length;
                if (_update.AssetSize > 0 && localSize == _update.AssetSize)
                {
                    alreadyDownloaded = true;
                }
                else if (_update.AssetSize <= 0 || localSize < _update.AssetSize)
                {
                    // 不完整 / 未知 size → 删了重下
                    try { File.Delete(tempExe); } catch { /* ignore */ }
                }
            }
            if (alreadyDownloaded)
            {
                progressBar.Value = 100;
                lblProgress.Text = $"本地已存在 v{_update.Version} 安装包，跳过下载。";
                Application.DoEvents();
                await Task.Delay(600);
            }

            _cts = new CancellationTokenSource();
            var progress = new Progress<DownloadProgress>(p =>
            {
                progressBar.Value = (int)Math.Min(100, p.Percent * 100);
                string speed = p.SpeedBytesPerSec > 1024 * 1024
                    ? $"{p.SpeedBytesPerSec / 1024 / 1024:0.0} MB/s"
                    : $"{p.SpeedBytesPerSec / 1024:0.0} KB/s";
                lblProgress.Text = $"下载中... {p.Percent * 100:0.0}%  ({FormatSize(p.BytesReceived)}/{FormatSize(p.TotalBytes)})  {speed}";
            });

            if (!alreadyDownloaded)
            {
                await UpdateService.DownloadUpdateAsync(_update.DownloadUrl, tempExe, progress, _cts.Token);
            }

            lblProgress.Text = "下载完成，准备替换...";
            Application.DoEvents();
            await Task.Delay(500);

            // 弹个确认
            var dr = MessageBox.Show(
                $"新版本 v{_update.Version} 已下载完成。\n点击【确定】将关闭当前程序并启动新版本。\n\n更新类型：{(_update.IsZipPackage ? "完整包（含 Plugins/）" : "仅主程序")}",
                "更新就绪",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (dr != DialogResult.OK) return;

            // 关闭 + 启动更新器
            if (_update.IsZipPackage)
                UpdateService.PerformZipUpdate(tempExe);
            else
                UpdateService.PerformUpdate(tempExe);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"更新失败：{ex.Message}", "更新失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnUpdate.Enabled = true;
            btnLater.Enabled = true;
            btnViewRelease.Enabled = true;
            progressBar.Visible = false;
            lblProgress.Visible = false;
        }
    }

    private void BtnLater_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void BtnViewRelease_Click(object? sender, EventArgs e)
    {
        // 按实际来源打开对应仓库的 Release 页面
        var url = _update.Source == UpdateSource.Gitee
            ? UpdateService.GiteeReleasePageUrl(_update.TagName)
            : UpdateService.GitHubReleasePageUrl(_update.TagName);
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
