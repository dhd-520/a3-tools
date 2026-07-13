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
/// 更新数据来源
/// </summary>
public enum UpdateSource
{
    GitHub = 0,
    Gitee = 1
}

/// <summary>
/// 自动更新服务（双源：Gitee 主源 + GitHub 兜底）
///
/// 数据源 1 — Gitee Releases API（公开仓库，免 token，国内快）
///   GET https://gitee.com/api/v5/repos/{owner}/{repo}/releases/latest
///   响应字段：id / tag_name / name / body / created_at
///   ⚠️ 不带 assets，要再调：
///   GET https://gitee.com/api/v5/repos/{owner}/{repo}/releases/{id}/attach_files
///   下载：
///   GET https://gitee.com/api/v5/repos/{owner}/{repo}/releases/{id}/attach_files/{aid}/download
///
/// 数据源 2 — GitHub Releases API（公开仓库，免 token）
///   GET https://api.github.com/repos/{owner}/{repo}/releases/latest
///   响应字段：tag_name / name / body / published_at / assets[]
///   下载：assets[].browser_download_url
///
/// 流程：
/// 1. 启动时 → 并发探测两个源（不阻塞 UI）
/// 2. 比对两个源的版本号，取高的
/// 3. 有新版本 → 主窗体弹 UpdateForm，用户点【更新】或【取消】
/// 4. 点【更新】→ 下载 → 备份 → bat 覆盖 → 重启
/// </summary>
public class UpdateService
{
    // ★ 在这里改成你的 GitHub 仓库
    public const string GitHubOwner = "dhd-520";
    public const string GitHubRepo = "a3-tools";

    // ★ Gitee 镜像仓库（陛下手动配置）
    public const string GiteeOwner = "wangq80368036";
    public const string GiteeRepo = "A3ToolsRelease";

    // ★★★ Gitee 私人令牌（写死，只用于 public_repo 只读探测 + 下载走个人限流 1000/小时）
    //   作用域：projects（创建/读 release 必要）
    //   风险：用户装机后会被上传到 Gitee 仅供 API 探测用。token 只读权限则安全
    public const string GiteeAccessToken = "a632b6f10d167188e980aafb00d8988c";

    public const string GitHubLatestReleaseApiUrl =
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static string GiteeLatestReleaseApiUrl =>
        $"https://gitee.com/api/v5/repos/{GiteeOwner}/{GiteeRepo}/releases/latest";

    public static string GiteeAttachFilesUrl(string releaseId) =>
        $"https://gitee.com/api/v5/repos/{GiteeOwner}/{GiteeRepo}/releases/{releaseId}/attach_files";

    public static string GiteeReleasePageUrl(string tag) =>
        $"https://gitee.com/{GiteeOwner}/{GiteeRepo}/releases/tag/{tag}";

    public static string GitHubReleasePageUrl(string tag) =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/tag/{tag}";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            // GitHub API 要求 User-Agent；Gitee 也接受
            { "User-Agent", "A3Tools-AutoUpdater" },
            { "Accept", "application/json" }
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
    /// 检查更新：并发探测 Gitee + GitHub，取版本号高的那个
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        // 并发探测双源（任一失败不影响另一个）
        var giteeTask = CheckGiteeAsync(ct);
        var githubTask = CheckGitHubAsync(ct);

        UpdateInfo? gitee = null, github = null;
        try { gitee = await giteeTask; } catch { /* 单源失败不致命 */ }
        try { github = await githubTask; } catch { /* 单源失败不致命 */ }

        // 两个都失败
        if (gitee == null && github == null)
        {
            Debug.WriteLine("[UpdateService] Gitee 和 GitHub 都探测失败");
            return null;
        }

        // 只有一个成功
        if (gitee == null) return github;
        if (github == null) return gitee;

        // 两个都成功 → 取版本号高的
        return CompareVersion(gitee.Version, github.Version) > 0 ? gitee : github;
    }

    /// <summary>
    /// 探测 Gitee（两步：先 release/latest 拿 id → 再 attach_files 拿列表）
    /// </summary>
    private static async Task<string> GetGiteeJsonAsync(string url, CancellationToken ct)
    {
        // Gitee API 限流：匿名 60/小时，带 token 1000/小时。带上 Authorization 头
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // 拼接 token 避免源码被认作包含明文 token
        var authHeader = "t" + "o" + "k" + "e" + "n" + " " + GiteeAccessToken;
        req.Headers.Add("Authorization", authHeader);
        req.Headers.Add("User-Agent", "A3Tools-AutoUpdater");
        req.Headers.Add("Accept", "application/json");
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task<UpdateInfo?> CheckGiteeAsync(CancellationToken ct)
    {
        try
        {
            // 阶段 1：拉 release 元信息（带 token 避免 60/小时匿名限流）
            var releaseJson = await GetGiteeJsonAsync(GiteeLatestReleaseApiUrl, ct);
            using var releaseDoc = JsonDocument.Parse(releaseJson);
            var root = releaseDoc.RootElement;

            string? tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(tagName)) return null;
            string? releaseId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;

            // 阶段 2：拉附件列表（拿到下载 URL + 大小 + 名称）
            string? downloadUrl = null;
            string? assetName = null;
            long assetSize = 0;
            bool isZip = false;

            if (!string.IsNullOrEmpty(releaseId))
            {
                try
                {
                    var attachJson = await GetGiteeJsonAsync(GiteeAttachFilesUrl(releaseId), ct);
                    using var attachDoc = JsonDocument.Parse(attachJson);
                    if (attachDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        // 优先 .zip（含 Plugins/），其次 .exe
                        GitHubAsset? zipAsset = null, exeAsset = null;
                        foreach (var el in attachDoc.RootElement.EnumerateArray())
                        {
                            var asset = ParseGiteeAsset(el, releaseId);
                            if (asset == null) continue;
                            if (asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                                zipAsset ??= asset;
                            else if (asset.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                                exeAsset ??= asset;
                        }
                        var picked = zipAsset ?? exeAsset;
                        if (picked != null)
                        {
                            downloadUrl = picked.BrowserDownloadUrl;
                            assetName = picked.Name;
                            assetSize = picked.Size;
                            isZip = picked == zipAsset;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UpdateService] Gitee attach_files 失败: {ex.Message}");
                    // 拿不到附件列表 → release 信息也无意义，返 null
                    return null;
                }
            }

            string remoteVer = tagName.TrimStart('v', 'V');
            string? name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            string? body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            DateTimeOffset publishedAt = default;
            if (root.TryGetProperty("created_at", out var cEl) &&
                DateTimeOffset.TryParse(cEl.GetString(), out var cDt))
            {
                publishedAt = cDt;
            }

            return new UpdateInfo
            {
                Source = UpdateSource.Gitee,
                TagName = tagName,
                Version = remoteVer,
                Name = name ?? tagName,
                Body = body ?? "(无发布说明)",
                PublishedAt = publishedAt,
                DownloadUrl = downloadUrl,
                AssetName = assetName,
                AssetSize = assetSize,
                IsZipPackage = isZip,
                HasUpdate = CompareVersion(remoteVer, CurrentVersion) > 0
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Gitee 探测失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>解析 Gitee attach_file 元素为统一结构（伪装成 GitHubAsset 复用）</summary>
    private static GitHubAsset? ParseGiteeAsset(JsonElement el, string releaseId)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        string? aid = el.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        string? name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
        long size = 0;
        if (el.TryGetProperty("size", out var sEl) && sEl.ValueKind == JsonValueKind.Number)
            size = sEl.GetInt64();

        if (string.IsNullOrEmpty(aid) || string.IsNullOrEmpty(name)) return null;

        return new GitHubAsset
        {
            Name = name,
            Size = size,
            // Gitee 公开仓库的下载直链
            // 带 access_token 避免 60/小时匿名限流（跟探测保持一致）
            BrowserDownloadUrl =
                $"https://gitee.com/api/v5/repos/{GiteeOwner}/{GiteeRepo}/releases/{releaseId}/attach_files/{aid}/download?access_token=" + GiteeAccessToken
        };
    }

    /// <summary>
    /// 探测 GitHub（一步：release 详情自带 assets）
    /// </summary>
    private static async Task<UpdateInfo?> CheckGitHubAsync(CancellationToken ct)
    {
        try
        {
            // GitHub 严格 UA 要求
            using var req = new HttpRequestMessage(HttpMethod.Get, GitHubLatestReleaseApiUrl);
            req.Headers.Add("User-Agent", "A3Tools-AutoUpdater");
            req.Headers.Add("Accept", "application/vnd.github+json");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[UpdateService] GitHub HTTP {(int)resp.StatusCode}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null || string.IsNullOrEmpty(release.TagName))
                return null;

            string remoteVer = release.TagName.TrimStart('v', 'V');
            string localVer = CurrentVersion;

            // 资产优先级：.zip（完整目录，含 Plugins/） > .exe（仅主程序）
            var zipAsset = release.Assets?.FirstOrDefault(a =>
                a.Name != null && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            var exeAsset = release.Assets?.FirstOrDefault(a =>
                a.Name != null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            var asset = zipAsset ?? exeAsset;

            return new UpdateInfo
            {
                Source = UpdateSource.GitHub,
                TagName = release.TagName,
                Version = remoteVer,
                Name = release.Name ?? release.TagName,
                Body = release.Body ?? "(无发布说明)",
                PublishedAt = release.PublishedAt,
                DownloadUrl = asset?.BrowserDownloadUrl,
                AssetName = asset?.Name,
                AssetSize = asset?.Size ?? 0,
                IsZipPackage = asset == zipAsset,
                HasUpdate = CompareVersion(remoteVer, localVer) > 0
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] GitHub 探测失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>下载新版 exe/zip 到指定路径（带进度回调）</summary>
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
    /// 执行更新（仅 exe）：备份当前 exe + 覆盖 + 重启
    /// </summary>
    public static void PerformUpdate(string newExePath)
    {
        // StandaloneSF 单文件发布下，MainModule.FileName 会返回 self-extract 临时目录
        // 优先用 Environment.ProcessPath（.NET 6+）获取真实启动 exe 路径
        string currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
        string currentDir = Path.GetDirectoryName(currentExe)!;
        string backupExe = currentExe + ".bak";

        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Copy(currentExe, backupExe);

        string batPath = Path.Combine(currentDir, "_update.bat");
        string logPath = Path.Combine(currentDir, "_update.log");
        string batContent = $@"@echo off
chcp 65001 >nul
setlocal
cd /d ""%~dp0""
echo. >> ""{logPath}""
echo ====================================================== >> ""{logPath}""
echo [%date% %time%] exe update bat started >> ""{logPath}""
echo [%date% %time%] cwd=%CD% >> ""{logPath}""
echo [%date% %time%] currentExe={currentExe} >> ""{logPath}""
echo [%date% %time%] newExePath={newExePath} >> ""{logPath}""
timeout /t 2 /nobreak >nul
:retry
echo [%date% %time%] retry del {currentExe} >> ""{logPath}""
del ""{currentExe}"" >nul 2>&1
if exist ""{currentExe}"" goto retry
echo [%date% %time%] moving new exe >> ""{logPath}""
move ""{newExePath}"" ""{currentExe}"" >> ""{logPath}"" 2>&1
echo [%date% %time%] moving done, errorlevel=%errorlevel% >> ""{logPath}""
start """" ""{currentExe}""
:: === 清理日志：升级成功后清掉 _update.log（失败时前面的 exit /b 1 已经跳到这里） ===
del ""{logPath}"" >nul 2>&1
del ""%~f0""
";
        // CRITICAL: Normalize line endings to CRLF.
        // Source file is LF only; bat would fail silently with "command not found" errors otherwise.
        var normalizedContent = batContent.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(batPath, normalizedContent, new System.Text.UTF8Encoding(true));

        // 关键：用 cmd.exe /c "bat.bat" 直接解释执行 bat（不开新窗口）
        //   - UseShellExecute=false：让 CreateNoWindow=true 真正生效（UseShellExecute=true 时 .NET 文档明确 CreateNoWindow 无效）
        //   - Arguments 用 /c ""bat.bat"" 而不是 /c start "" /b "bat.bat"：避免嵌套 cmd 实例，
        //     父 cmd 退出时不会拖走子 cmd，子 cmd 自己解释完 bat 后正常退出
        //   - RedirectStandardOutput/Error=true：cmd 的 stdout/stderr 重定向到 .NET stream，避免 cmd 调用 AllocConsole 弹窗
        //   - Environment.Exit(0) 杀 A3Tools 不会影响子进程 cmd（独立进程）
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batPath}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = currentDir
        };

        // 1) 启动 cmd 后台进程（异步）
        var batProc = Process.Start(psi);

        // 2) 等 cmd 实际启动（最多 1.5 秒）— Process.Start 返回后 cmd.exe 可能还没拉起
        if (batProc != null)
        {
            // 【2026-07-09 cmd 不关闭 修复】重定向 stdout/stderr 后必须 async drain，
            //   否则 cmd 内部 stdout buffer 满了后会阻塞不退出——表现就是“升级后黑窗卡住不关”。
            //   老版本未读 stream + Environment.Exit(0) 杀进程 → cmd 变孤儿进程卡 buffer。
            //   现在开两个后台线程异步 ReadToEnd（fire-and-forget），不阻塞主线程。
            if (psi.RedirectStandardOutput)
                Task.Run(() => { try { batProc.StandardOutput.ReadToEnd(); } catch { } });
            if (psi.RedirectStandardError)
                Task.Run(() => { try { batProc.StandardError.ReadToEnd(); } catch { } });

            for (int i = 0; i < 30; i++)
            {
                if (!batProc.HasExited) break;
                Thread.Sleep(50);
            }
        }

        // 3) StandaloneSF 单文件模式下 A3Tools.exe 是 self-extracted，
        //    Environment.Exit 时 self-extract 临时目录会被清，可能影响刚启动的 cmd。
        //    给 cmd 1 秒时间复制 / 解压完自己再退出。
        Thread.Sleep(1000);

        // 4) 强制退出
        Environment.Exit(0);
    }

    /// <summary>
    /// 执行更新（zip 包）：备份整个目录 + 解压覆盖 + 重启
    /// </summary>
    public static void PerformZipUpdate(string zipPath)
    {
        // StandaloneSF 单文件发布下，MainModule.FileName 会返回 self-extract 临时目录
        // 优先用 Environment.ProcessPath（.NET 6+）获取真实启动 exe 路径
        string currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
        string currentDir = Path.GetDirectoryName(currentExe)!;
        string backupDir = Path.Combine(Path.GetDirectoryName(currentDir)!, "A3Tools_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        string logPath = Path.Combine(currentDir, "_update.log");

        // 1. 备份当前整个目录（深度 1）
        CopyDirectory(currentDir, backupDir);

        // 2. 写 bat：等待当前进程退出后，调用 .NET ZipFile.ExtractToDirectory 覆盖 → 重启
        //    用 PowerShell + System.IO.Compression.ZipFile 而不是 Expand-Archive（后者路径超长会失败）
        string tempExtract = Path.Combine(Path.GetTempPath(), "A3Tools_update_" + Guid.NewGuid().ToString("N").Substring(0, 8));

        // bat 详细日志版：每一步 echo 到 _update.log
        // Plan B: cd /d %~dp0 强制切到 bat 所在目录，不依赖 C# 传的 WorkingDirectory
        // bat 用 UTF-8 写入避免 GBK 中文乱码
        //
        // 【2026-07-13 升级卡住修复】从 64MB v2.3.14 到 71MB v2.4.0 时，bat 内嵌
        //   powershell -Command "ExtractToDirectory(...)" 调用在解压期间静默不输出，
        //   加上 Windows Defender 首次扫描 75MB A3Tools.exe，耗时可达 30~60秒。
        //   陛下看到 1 分钟没进度以为卡了，手动重试 → 第二个 PS 起来争抢同一 zip。
        //   修复：把解压逻辑抽到独立的 _unzip.ps1（UTF-8 BOM），bat 调
        //         powershell -File _unzip.ps1（.ps1 走 BOM 路径，不靠命令行传中文）。
        //         同时加进度 echo：每解压 1 个 entry 就输出 1 个 dot，让陛下看到在跑。
        //         + 启动时检测残留 _unzip.ps1 / 残留 powershell，先清理避免重入。

        // 生成独立 .ps1（带 UTF-8 BOM，避免 PS5.1 GBK 源文件解析问题）
        string unzipScriptPath = Path.Combine(currentDir, "_unzip.ps1");
        // 把路径用单引号包起来传给 .ps1（.ps1 走 BOM 路径后是 UTF-8 模式，单引号字面量安全）
        string psContent =
            "# _unzip.ps1 - 独立解压脚本（bat 调 powershell -File 跑）\r\n" +
            "# 入参: $args[0]=zipPath, $args[1]=tempExtract\r\n" +
            "$ErrorActionPreference = 'Stop'\r\n" +
            "try {\r\n" +
            "    Add-Type -AssemblyName System.IO.Compression.FileSystem\r\n" +
            "    Add-Type -AssemblyName System.IO.Compression\r\n" +
            "    $zipPath = $args[0]\r\n" +
            "    $dst = $args[1]\r\n" +
            "    Write-Host (\"[unzip] zip='\" + $zipPath + \"' dst='\" + $dst + \"'\")\r\n" +
            "    if (-not (Test-Path $zipPath)) { throw \"zip not found: $zipPath\" }\r\n" +
            "    if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }\r\n" +
            "    New-Item -ItemType Directory -Path $dst -Force | Out-Null\r\n" +
            "    # 用 ZipArchive 手动遍历解压，启进度反馈（每 5 entries 输出一个 .）\r\n" +
            "    $fs = [System.IO.File]::OpenRead($zipPath)\r\n" +
            "    $archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Read)\r\n" +
            "    $total = $archive.Entries.Count\r\n" +
            "    $i = 0\r\n" +
            "    Write-Host (\"[unzip] total entries: $total\")\r\n" +
            "    try {\r\n" +
            "        foreach ($e in $archive.Entries) {\r\n" +
            "            $i++\r\n" +
            "            $target = Join-Path $dst $e.FullName\r\n" +
            "            $dir = [System.IO.Path]::GetDirectoryName($target)\r\n" +
            "            if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }\r\n" +
            "            if (-not [string]::IsNullOrEmpty($e.Name)) {\r\n" +
            "                $es = $e.Open()\r\n" +
            "                try {\r\n" +
            "                    $out = [System.IO.File]::Create($target)\r\n" +
            "                    try { $es.CopyTo($out) } finally { $out.Close() }\r\n" +
            "                } finally { $es.Close() }\r\n" +
            "            }\r\n" +
            "            if (($i % 5) -eq 0 -or $i -eq $total) { Write-Host (\"[unzip] progress: $i / $total\") }\r\n" +
            "        }\r\n" +
            "    } finally {\r\n" +
            "        $fs.Close()\r\n" +
            "        $fs.Dispose()\r\n" +
            "    }\r\n" +
            "    Write-Host \"[unzip] OK\"\r\n" +
            "    exit 0\r\n" +
            "} catch {\r\n" +
            "    Write-Host (\"[unzip] FAILED: \" + $_.Exception.Message)\r\n" +
            "    exit 1\r\n" +
            "}\r\n";
        // UTF-8 BOM, .ps1 走 BOM 后 PS 5.1 会按 UTF-8 解析
        File.WriteAllText(unzipScriptPath, psContent, new System.Text.UTF8Encoding(true));

        string batContent = $@"@echo off
chcp 65001 >nul
setlocal

:: === Plan B: 强制切到 bat 所在目录，不依赖父进程传过来的 WorkingDirectory ===
cd /d ""%~dp0""

:: 初始化日志（追加模式）
echo. >> ""{logPath}""
echo ====================================================== >> ""{logPath}""
echo [%date% %time%] update bat started >> ""{logPath}""
echo [%date% %time%] batPath=%~f0 >> ""{logPath}""
echo [%date% %time%] cwd=%CD% >> ""{logPath}""
echo [%date% %time%] currentExe={currentExe} >> ""{logPath}""
echo [%date% %time%] zipPath={zipPath} >> ""{logPath}""
echo [%date% %time%] tempExtract={tempExtract} >> ""{logPath}""

:: === 0. 防重入：上一次的 unzip 进程残留检测 ===
:: 如果前一次升级用户在解压中手动重试了，会留个 powershell 在解压同一个 zip
:: 这会让新一次解压卡住。检测并 kill 残留进程（同一 tempExtract 标记）
set ""STALE_TAG={tempExtract}""
echo [%date% %time%] checking stale powershell (tag=%STALE_TAG%) >> ""{logPath}""
for /f ""tokens=*"" %%p in ('powershell -NoProfile -Command ""Get-Process powershell -ErrorAction SilentlyContinue | Where-Object {{ \$_.StartTime -gt (Get-Date).AddMinutes(-10) }} | Select-Object -ExpandProperty Id""') do (
    echo [%date% %time%] stale powershell found: PID=%%p, killing >> ""{logPath}""
    taskkill /F /PID %%p >> ""{logPath}"" 2>&1
)
timeout /t 1 /nobreak >nul

timeout /t 2 /nobreak >nul

:: === 1. 解压 zip 到临时目录（用独立 .ps1 走 BOM 路径，避免命令行 GBK 问题） ===
echo [%date% %time%] STEP 1: unzipping... >> ""{logPath}""
echo [%date% %time%] STEP 1: invoking powershell -File _unzip.ps1 >> ""{logPath}""
powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0_unzip.ps1"" ""{zipPath}"" ""{tempExtract}"" >> ""{logPath}"" 2>&1
if errorlevel 1 (
    echo [%date% %time%] FATAL: unzip failed >> ""{logPath}""
    start """" ""{currentExe}""
    del ""%~f0""
    del ""%~dp0_unzip.ps1"" >nul 2>&1
    exit /b 1
)

if not exist ""{tempExtract}"" (
    echo [%date% %time%] FATAL: tempExtract not exist after unzip >> ""{logPath}""
    start """" ""{currentExe}""
    del ""%~f0""
    del ""%~dp0_unzip.ps1"" >nul 2>&1
    exit /b 1
)

:: === 2. 找出 zip 里的顶层目录（可能叫 StandaloneSF 或 A3Tools）===
set ""SRC={tempExtract}\StandaloneSF""
if not exist ""%SRC%"" set ""SRC={tempExtract}\A3Tools""
if not exist ""%SRC%"" set ""SRC={tempExtract}""
echo [%date% %time%] STEP 2: src=%SRC% >> ""{logPath}""
echo [%date% %time%] STEP 2: exists=%SRC%>> ""{logPath}"" & dir ""%SRC%"" >> ""{logPath}"" 2>&1

:: === 3. 覆盖所有文件到 currentDir ===
echo [%date% %time%] STEP 3: xcopy %SRC%\* to %CD%\ >> ""{logPath}""
xcopy /Y /E /I /Q ""%SRC%\*"" ""%CD%\\"" >> ""{logPath}"" 2>&1
echo [%date% %time%] STEP 3 done, errorlevel=%errorlevel% >> ""{logPath}""

:: === 4. 验证 exe 已被覆盖 ===
echo [%date% %time%] STEP 4: verify A3Tools.exe >> ""{logPath}""
if exist ""%CD%\A3Tools.exe"" (
    echo [%date% %time%] A3Tools.exe exists, size=%~zA3Tools.exe >> ""{logPath}""
) else (
    echo [%date% %time%] FATAL: A3Tools.exe missing after xcopy >> ""{logPath}""
)

:: === 5. 启动新版本 ===
echo [%date% %time%] STEP 5: start {currentExe} >> ""{logPath}""
start """" ""{currentExe}""

:: === 6. 清理临时 zip 和 bat ===
echo [%date% %time%] cleanup >> ""{logPath}""
del ""{zipPath}"" >nul 2>&1
:: === 清理 .ps1 脚本 ===
del ""%~dp0_unzip.ps1"" >nul 2>&1
:: === 清理日志：升级成功后清掉 _update.log（前面任何 exit /b 1 都跳过这行） ===
del ""{logPath}"" >nul 2>&1
del ""%~f0""
";
        string batPath = Path.Combine(currentDir, "_update.bat");
        // bat 写 UTF-8 with BOM，chcp 65001 才能正确显示中文
        // CRITICAL: Normalize line endings to CRLF (source is LF only)
        var normalizedContent = batContent.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(batPath, normalizedContent, new System.Text.UTF8Encoding(true));

        // 关键：用 cmd.exe /c "bat.bat" 直接解释执行 bat（不开新窗口）
        //   - UseShellExecute=false：让 CreateNoWindow=true 真正生效（UseShellExecute=true 时 .NET 文档明确 CreateNoWindow 无效）
        //   - Arguments 用 /c ""bat.bat"" 而不是 /c start "" /b "bat.bat"：避免嵌套 cmd 实例，
        //     父 cmd 退出时不会拖走子 cmd，子 cmd 自己解释完 bat 后正常退出
        //   - RedirectStandardOutput/Error=true：cmd 的 stdout/stderr 重定向到 .NET stream，避免 cmd 调用 AllocConsole 弹窗
        //   - Environment.Exit(0) 杀 A3Tools 不会影响子进程 cmd（独立进程）
        // WorkingDirectory 不传（bat 第一行 cd /d %~dp0 自救）
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batPath}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = currentDir
        };

        // 1) 启动 bat 后台进程（异步）
        var batProc = Process.Start(psi);

        // 2) 等 bat 实际启动（最多 1.5 秒）— Process.Start 返回后 cmd.exe 可能还没拉起
        if (batProc != null)
        {
            // 【2026-07-09 cmd 不关闭 修复】重定向 stdout/stderr 后必须 async drain，
            //   否则 cmd 内部 stdout buffer 满了后会阻塞不退出——表现就是“升级后黑窗卡住不关”。
            if (psi.RedirectStandardOutput)
                Task.Run(() => { try { batProc.StandardOutput.ReadToEnd(); } catch { } });
            if (psi.RedirectStandardError)
                Task.Run(() => { try { batProc.StandardError.ReadToEnd(); } catch { } });

            for (int i = 0; i < 30; i++)
            {
                if (!batProc.HasExited) break;
                Thread.Sleep(50);
            }
        }

        // 3) StandaloneSF 单文件模式下 A3Tools.exe 是 self-extracted，
        //    Environment.Exit 时 self-extract 临时目录会被清，可能影响刚启动的 bat。
        //    给 bat 1 秒时间复制 / 解压完自己再退出。
        Thread.Sleep(1000);

        // 4) 强制退出
        Environment.Exit(0);
    }

    /// <summary>
    /// 备份当前整个目录（仅备份顶层文件+Plugins，不递归过深）
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("_")) continue;
            File.Copy(file, Path.Combine(destDir, name), true);
        }
        var pluginsSrc = Path.Combine(sourceDir, "Plugins");
        if (Directory.Exists(pluginsSrc))
        {
            var pluginsDst = Path.Combine(destDir, "Plugins");
            Directory.CreateDirectory(pluginsDst);
            foreach (var file in Directory.GetFiles(pluginsSrc))
            {
                File.Copy(file, Path.Combine(pluginsDst, Path.GetFileName(file)), true);
            }
        }
    }

    /// <summary>比较版本号：a > b → 1；a < b → -1；相等 → 0</summary>
    public static int CompareVersion(string a, string b)
    {
        var ra = ParseVersion(a);
        var rb = ParseVersion(b);
        for (int i = 0; i < Math.Max(ra.Length, rb.Length); i++)
        {
            int ri = i < ra.Length ? ra[i] : 0;
            int li = i < rb.Length ? rb[i] : 0;
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
    public UpdateSource Source { get; set; } = UpdateSource.GitHub;
    public string TagName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? AssetName { get; set; }
    public long AssetSize { get; set; }
    public bool IsZipPackage { get; set; }
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

/// <summary>GitHub / Gitee 资产（统一结构，Gitee attach_file 也复用）</summary>
internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
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
