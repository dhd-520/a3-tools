using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace A3Tools.Services;

/// <summary>
/// 自动更新服务
///
/// 数据源：GitHub Releases API（无需 Token，公开仓库）
///   GET https://api.github.com/repos/{owner}/{repo}/releases/latest
///   响应字段：tag_name（如 "v2.2.0"）、name、body（发布说明）、
///             assets[]（下载附件，含 browser_download_url + size + name）
///
/// 流程：
/// 1. 启动时 → 异步 CheckForUpdateAsync()，不阻塞 UI
/// 2. 比对本地 AssemblyVersion 与远端 tag_name
/// 3. 有新版本 → 主窗体弹 UpdateForm，让用户点【更新】或【取消】
/// 4. 点【更新】→ 后台下载 zip → 备份当前 exe → 解压覆盖 → 启动新版
/// </summary>
public class UpdateService
{
    // ★ 在这里改成你的 GitHub 仓库
    public const string GitHubOwner = "dhd-520";
    public const string GitHubRepo = "a3-tools";

    public const string LatestReleaseApiUrl =
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            // GitHub API 要求 User-Agent
            { "User-Agent", "A3Tools-AutoUpdater" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    /// <summary>当前应用版本（从 AssemblyVersion 取）</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// 检查更新：异步拉 GitHub Releases/latest，比对版本号
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync(LatestReleaseApiUrl, ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null || string.IsNullOrEmpty(release.TagName))
                return null;

            // tag_name 是 "v2.2.0" 格式，去掉 v 前缀
            string remoteVer = release.TagName.TrimStart('v', 'V');
            string localVer = CurrentVersion;

            // 找第一个 .exe 资产（StandaloneSF 打包出来的 A3Tools.exe）
            var asset = release.Assets?.FirstOrDefault(a =>
                a.Name != null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            return new UpdateInfo
            {
                TagName = release.TagName,
                Version = remoteVer,
                Name = release.Name ?? release.TagName,
                Body = release.Body ?? "(无发布说明)",
                PublishedAt = release.PublishedAt,
                DownloadUrl = asset?.BrowserDownloadUrl,
                AssetName = asset?.Name,
                AssetSize = asset?.Size ?? 0,
                HasUpdate = CompareVersion(remoteVer, localVer) > 0
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] 检查更新失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>下载新版 exe 到指定路径（带进度回调）</summary>
    public static async Task DownloadUpdateAsync(
        string url,
        string savePath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(savePath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        var sw = Stopwatch.StartNew();
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (progress != null && totalBytes > 0)
            {
                double pct = (double)downloaded / totalBytes;
                double speed = sw.Elapsed.TotalSeconds > 0 ? downloaded / sw.Elapsed.TotalSeconds : 0;
                progress.Report(new DownloadProgress
                {
                    BytesReceived = downloaded,
                    TotalBytes = totalBytes,
                    Percent = pct,
                    SpeedBytesPerSec = speed
                });
            }
        }
    }

    /// <summary>
    /// 执行更新：备份当前 exe + 覆盖 + 启动新版 + 关闭旧版
    /// </summary>
    public static void PerformUpdate(string newExePath)
    {
        // 当前 exe 路径
        string currentExe = Process.GetCurrentProcess().MainModule!.FileName!;
        string currentDir = Path.GetDirectoryName(currentExe)!;
        string backupExe = currentExe + ".bak";
        string newExeName = Path.GetFileName(currentExe);

        // 1. 备份当前 exe
        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Copy(currentExe, backupExe);

        // 2. 写一个 bat 脚本来完成覆盖+重启（旧进程不退出，文件被占用）
        //    等待 2 秒让当前进程退出后开始替换
        string batPath = Path.Combine(currentDir, "_update.bat");
        string batContent = $@"@echo off
chcp 65001 >nul
timeout /t 2 /nobreak >nul
:retry
del ""{currentExe}"" >nul 2>&1
if exist ""{currentExe}"" goto retry
move ""{newExePath}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""
";
        File.WriteAllText(batPath, batContent, System.Text.Encoding.Default);

        // 3. 启动 bat
        var psi = new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = currentDir
        };
        Process.Start(psi);

        // 4. 关闭当前进程
        Environment.Exit(0);
    }

    /// <summary>比较版本号：remote > local → 1；remote < local → -1；相等 → 0</summary>
    public static int CompareVersion(string remote, string local)
    {
        var r = ParseVersion(remote);
        var l = ParseVersion(local);
        for (int i = 0; i < Math.Max(r.Length, l.Length); i++)
        {
            int ri = i < r.Length ? r[i] : 0;
            int li = i < l.Length ? l[i] : 0;
            if (ri > li) return 1;
            if (ri < li) return -1;
        }
        return 0;
    }

    private static int[] ParseVersion(string v)
    {
        if (string.IsNullOrEmpty(v)) return new[] { 0 };
        return v.Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .ToArray();
    }
}

/// <summary>更新信息（给 UI 层用）</summary>
public class UpdateInfo
{
    public string TagName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? AssetName { get; set; }
    public long AssetSize { get; set; }
    public bool HasUpdate { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DownloadProgress
{
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }
    public double Percent { get; set; }
    public double SpeedBytesPerSec { get; set; }
}

/// <summary>GitHub Release 响应模型（精简版）</summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}

// LINQ 引用在 using System.Linq; 中
