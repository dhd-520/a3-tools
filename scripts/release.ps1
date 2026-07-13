<#
.SYNOPSIS
  A3Tools 一键发布脚本

.DESCRIPTION
  用法：
    .\release.ps1 -Version "2.3.2"

  前置：
    1. PowerShell 5.1+（Win10/11 自带）
    2. 配置环境变量（任选其一方式）：
       - $env:GITEE_TOKEN = "xxx"   Gitee 私人令牌：https://gitee.com/profile/personal_access_tokens
       - $env:GITHUB_TOKEN = "xxx"  GitHub PAT（勾选 repo 权限；或装 gh CLI）
       - 或在项目根目录创建 secrets.local.env（脚本自动加载，**已 .gitignore**）
         格式：GITEE_TOKEN=xxx
    3. 在 D:\work\A3Tools 根目录运行
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [string]$ReleaseNotes,

    [string]$PublishDir = "publish",

    # 默认$true：2026-07-08 陛下决定先只用 Gitee（GitHub 国内连接不稳 + gh CLI 未 auth）
    # 设 -IncludeGitHub 可手动启用 GitHub 发布
    [bool]$SkipGitHub = $true
)

$ErrorActionPreference = "Stop"

function Info($msg) { Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg) -ForegroundColor Cyan }
function Ok($msg)   { Write-Host ("[{0}] ✓ {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg) -ForegroundColor Green }
function Warn($msg) { Write-Host ("[{0}] ! {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg) -ForegroundColor Yellow }
function Err($msg)  { Write-Host ("[{0}] ✗ {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg) -ForegroundColor Red }

# 1) 参数校验
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Err ("Version format wrong: " + $Version + " (expect x.y.z)")
    exit 1
}

$repoRoot = (Get-Location).Path
if (-not (Test-Path (Join-Path $repoRoot "A3Tools.sln"))) {
    Err "Run this from D:\work\A3Tools root (A3Tools.sln not found)"
    exit 1
}

# 2) git 状态检查
$gitRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0) { Err "Not in a git repo"; exit 1 }
Set-Location $gitRoot
Info ("Working dir: " + $gitRoot)

$gitStatus = (& git status --porcelain)
if ($gitStatus) {
    Warn "git working dir has uncommitted changes:"
    $gitStatus | ForEach-Object { Write-Host ("    " + $_) -ForegroundColor Yellow }
    # 非交互模式(Agent / CI): 默认继续;交互模式: 询问
    if ([Environment]::UserInteractive) {
        $ans = Read-Host "Continue? [y/N]"
        if ($ans -ne 'y' -and $ans -ne 'Y') { exit 1 }
    } else {
        Warn "non-interactive mode -> auto continue"
    }
}

# 3) Token 校验（优先读 env，其次读项目根 ./secrets.local.env）
#    持久化位点：D:\work\A3Tools\secrets.local.env（.gitignore，已限 ACL 仅当前用户可读）
function Load-SecretsFromFile {
    param([string]$FilePath)
    if (-not (Test-Path $FilePath)) { return }
    Write-Host ("[secret] loading " + $FilePath)
    Get-Content $FilePath | ForEach-Object {
        $line = $_.Trim()
        if (-not $line -or $line.StartsWith("#")) { return }
        $eq = $line.IndexOf("=")
        if ($eq -lt 1) { return }
        $key = $line.Substring(0, $eq).Trim()
        $val = $line.Substring($eq + 1).Trim()
        # 现有 env 优先（例手动 override），未设才从文件加载
        $existing = [Environment]::GetEnvironmentVariable($key)
        if ([string]::IsNullOrEmpty($existing)) {
            [Environment]::SetEnvironmentVariable($key, $val)
            # Set-Item env: 在进程内立即可见，setx / [Environment]::SetEnvironmentVariable 只在 Process 作用域才可见
            Set-Item -Path "env:$key" -Value $val
        }
    }
}
Load-SecretsFromFile (Join-Path $gitRoot "secrets.local.env")

$giteeToken = $env:GITEE_TOKEN
$githubToken = $env:GITHUB_TOKEN
$ghCli = $null
try { $null = (& gh --version); if ($LASTEXITCODE -eq 0) { $ghCli = "gh" } } catch {}

if (-not $giteeToken) { Warn "GITEE_TOKEN not set, will skip Gitee" }
if (-not $githubToken -and -not $ghCli) {
    Warn "GITHUB_TOKEN not set and gh CLI not found, will skip GitHub"
}

# 4) 默认发布说明（单引号 heredoc，不解析变量）
if (-not $ReleaseNotes) {
    $ReleaseNotes = @'
## A3Tools vNEW_VERSION

更新内容：见 commit 列表

### 升级提示
- 启动 A3Tools → 帮助 → 检查更新
- 工具会自动从 Gitee（国内快）/ GitHub 兜底拉取最新版
'@
    $ReleaseNotes = $ReleaseNotes -replace 'NEW_VERSION', $Version
}

# 5) 更新 csproj 版本号
Info ("Bumping csproj version -> " + $Version)
$csprojs = @(
    "A3Tools\A3Tools.csproj",
    "A3Tools.Plugins.Default\A3Tools.Plugins.Default.csproj",
    "A3Tools.Common\A3Tools.Common.csproj"
)
foreach ($csproj in $csprojs) {
    $path = Join-Path $gitRoot $csproj
    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $updated = [regex]::Replace($content, '<Version>\d+\.\d+\.\d+</Version>', ('<Version>' + $Version + '</Version>'))
    if ($content -ne $updated) {
        [System.IO.File]::WriteAllText($path, $updated, (New-Object System.Text.UTF8Encoding $false))
        Ok ("  " + $csproj)
    } else {
        Info ("  - " + $csproj + " (already " + $Version + ")")
    }
}

# 6) dotnet publish StandaloneSF
$standaloneDir = Join-Path $gitRoot (Join-Path $PublishDir "StandaloneSF")
if (Test-Path $standaloneDir) { Remove-Item $standaloneDir -Recurse -Force }

Info "dotnet publish StandaloneSF..."
& dotnet publish "A3Tools\A3Tools.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained `
    -o $standaloneDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --nologo
if ($LASTEXITCODE -ne 0) { Err "dotnet publish failed"; exit 1 }
Ok "StandaloneSF done"

# 7) 打 zip
$zipPath = Join-Path $gitRoot (Join-Path $PublishDir ("A3Tools_v" + $Version + ".zip"))
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Info ("Zipping: " + $zipPath)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($standaloneDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Ok (("zip done (" + $zipSize + " MB)"))

# 8) 推送 tag
$tag = "v" + $Version
Info ("Pushing tag " + $tag + " to remotes")
try {
    & git tag -d $tag 2>$null | Out-Null
    & git tag $tag
    # 2026-07-08：只推 Gitee remote（GitHub 国内连接不稳）
    $remotes = (& git remote)
    foreach ($remote in $remotes) {
        $url = (& git remote get-url $remote)
        if ($url -notmatch 'gitee\.com') {
            Info ("  skip " + $remote + " (not gitee)")
            continue
        }
        try {
            Info ("  pushing tag to " + $remote + "...")
            & git push $remote $tag --force
        } catch {
            Warn ("  push to " + $remote + " failed: " + $_.Exception.Message)
        }
    }
    Ok "tag pushed to Gitee remotes"
} catch {
    Warn ("tag push failed (may not affect API publish): " + $_.Exception.Message)
}

# 9) Gitee 发布
$giteeReleaseUrl = $null
if ($giteeToken) {
    Info "Publishing to Gitee..."
    $giteeOwner = $env:GITEE_OWNER
    $giteeRepo  = $env:GITEE_REPO
    if (-not $giteeOwner) { $giteeOwner = "wangq80368036" }
    if (-not $giteeRepo)  { $giteeRepo  = "a3-tools" }

    # 创建 release
    $createBody = @{
        access_token     = $giteeToken
        tag_name         = $tag
        name             = ("A3Tools v" + $Version)
        body             = $ReleaseNotes
        target_commitish = "master"
        prerelease       = "false"
    } | ConvertTo-Json -Depth 5

    $giteeReleaseId = $null
    try {
        # 【2026-07-09 中文乱码修复】Content-Type 必须带 charset=utf-8 + Body 显式 UTF-8 字节。
        #   PS 5.1 不带 charset 时会按 Default encoding（系统区域，GBK）发，Gitee 收 GBK 中文 → mojibake
        $jsonText = $createBody
        $release = Invoke-RestMethod `
            -Uri ("https://gitee.com/api/v5/repos/" + $giteeOwner + "/" + $giteeRepo + "/releases") `
            -Method Post `
            -ContentType "application/json; charset=utf-8" `
            -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonText))
        $giteeReleaseId = $release.id
        Ok (("Gitee release created (id=" + $giteeReleaseId + ")"))
    } catch {
        Err ("Gitee release create failed: " + $_.Exception.Message)
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                Warn ("  Response: " + $reader.ReadToEnd())
            } catch {}
        }
    }

    # 上传 zip（PS 5.1 不支持 -Form，手动构造 multipart/form-data）
    if ($giteeReleaseId) {
        Info "Uploading zip to Gitee..."
        try {
            $boundary = [System.Guid]::NewGuid().ToString()
            $fileBytes = [System.IO.File]::ReadAllBytes($zipPath)
            $fileName  = Split-Path $zipPath -Leaf
            $enc = [System.Text.Encoding]::GetEncoding("utf-8")

            # multipart 体的各部分
            $bodyParts = @()
            # access_token 字段
            $bodyParts += "--" + $boundary
            $bodyParts += "Content-Disposition: form-data; name=""access_token"""
            $bodyParts += ""
            $bodyParts += $giteeToken
            # file 字段（头部 + 二进制 + 结尾）
            $bodyParts += "--" + $boundary
            $bodyParts += ("Content-Disposition: form-data; name=""file""; filename=""" + $fileName + """")
            $bodyParts += "Content-Type: application/zip"
            $bodyParts += ""
            # 这里需要拼接二制内容，所以用 .NET HttpClient 走

            # 使用 .NET HttpClient 处理（拼接字符串头部 + 二进制文件体 + 结尾边界）
            $crlf = "`r`n"
            $preamble = (($bodyParts -join $crlf) + $crlf + $crlf).ToCharArray() | ForEach-Object { [byte]$_ } | ForEach-Object { [byte]$_ }
            # 上面拼接是 UTF-8 字符串，但 PS char 转换不安全；换用 [System.Text.EncodingBuilder]
            $sb = New-Object System.Text.StringBuilder
            $null = $sb.AppendLine("--" + $boundary)
            $null = $sb.AppendLine("Content-Disposition: form-data; name=""access_token""")
            $null = $sb.AppendLine("")
            $null = $sb.AppendLine($giteeToken)
            $null = $sb.AppendLine("--" + $boundary)
            $null = $sb.AppendLine(("Content-Disposition: form-data; name=""file""; filename=""" + $fileName + """"))
            $null = $sb.AppendLine("Content-Type: application/zip")
            $null = $sb.AppendLine("")
            $preambleBytes = $enc.GetBytes($sb.ToString())
            $closingBytes = $enc.GetBytes(($crlf + "--" + $boundary + "--" + $crlf))

            $ms = New-Object System.IO.MemoryStream
            $ms.Write($preambleBytes, 0, $preambleBytes.Length)
            $ms.Write($fileBytes, 0, $fileBytes.Length)
            $ms.Write($closingBytes, 0, $closingBytes.Length)
            $body = $ms.ToArray()

            $resp = Invoke-RestMethod `
                -Uri ("https://gitee.com/api/v5/repos/" + $giteeOwner + "/" + $giteeRepo + "/releases/" + $giteeReleaseId + "/attach_files") `
                -Method Post `
                -ContentType ("multipart/form-data; boundary=" + $boundary) `
                -Body $body
            Ok "Gitee zip uploaded (browser_download_url: " + $resp.browser_download_url + ")"
            $giteeReleaseUrl = ("https://gitee.com/" + $giteeOwner + "/" + $giteeRepo + "/releases/tag/" + $tag)
        } catch {
            Err ("Gitee zip upload failed: " + $_.Exception.Message)
        }
    }
} else {
    Info "Skipping Gitee (no token)"
}

# 10) GitHub 发布（默认跳过）
$githubReleaseUrl = $null
if ($SkipGitHub) {
    Info "Skipping GitHub (SkipGitHub=$SkipGitHub, set -IncludeGitHub to enable)"
} elseif ($githubToken -or $ghCli) {
    Info "Publishing to GitHub..."
    $ghOwner = $env:GITHUB_OWNER
    $ghRepo  = $env:GITHUB_REPO
    if (-not $ghOwner) { $ghOwner = "dhd-520" }
    if (-not $ghRepo)  { $ghRepo  = "a3-tools" }

    if ($ghCli) {
        try {
            & gh release create $tag $zipPath `
                --repo ("$ghOwner/$ghRepo") `
                --title ("A3Tools v" + $Version) `
                --notes $ReleaseNotes `
                --latest
            Ok "GitHub release created (gh CLI)"
            $githubReleaseUrl = ("https://github.com/" + $ghOwner + "/" + $ghRepo + "/releases/tag/" + $tag)
        } catch {
            Err ("gh release create failed: " + $_.Exception.Message)
        }
    } else {
        try {
            $headers = @{
                Authorization = ("token " + $githubToken)
                Accept        = "application/vnd.github+json"
                "User-Agent"  = "A3Tools-release-script"
            }
            $createBody = @{
                tag_name         = $tag
                target_commitish = "master"
                name             = ("A3Tools v" + $Version)
                body             = $ReleaseNotes
                draft            = $false
                prerelease       = $false
            } | ConvertTo-Json -Depth 5

            $release = Invoke-RestMethod `
                -Uri ("https://api.github.com/repos/" + $ghOwner + "/" + $ghRepo + "/releases") `
                -Method Post `
                -Headers $headers `
                -ContentType "application/json" `
                -Body $createBody
            $uploadUrl = $release.upload_url -replace '\{.*\}', ''
            Ok (("GitHub release created (id=" + $release.id + ")"))

            $assetName = ("A3Tools_v" + $Version + ".zip")
            $bytes = [System.IO.File]::ReadAllBytes($zipPath)
            $uploadHeaders = $headers + @{ "Content-Type" = "application/zip" }
            $null = Invoke-RestMethod `
                -Uri ($uploadUrl + "?name=" + $assetName) `
                -Method Post `
                -Headers $uploadHeaders `
                -Body $bytes
            Ok "GitHub zip uploaded"
            $githubReleaseUrl = $release.html_url
        } catch {
            Err ("GitHub publish failed: " + $_.Exception.Message)
        }
    }
} else {
    Info "Skipping GitHub (no token / gh CLI)"
}

# 11) 收尾
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host ("  Release v" + $Version + " done") -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
if ($giteeReleaseUrl)  { Write-Host ("  Gitee:  " + $giteeReleaseUrl)  -ForegroundColor Cyan }
if ($githubReleaseUrl) { Write-Host ("  GitHub: " + $githubReleaseUrl) -ForegroundColor Cyan }
Write-Host ""
Info ("Users will see v" + $Version + " on next launch via Help -> Check Update")
