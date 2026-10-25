# v2.6.6 一键发布包装脚本
# 用途: 把 release notes **文件路径** 传给 release.ps1
#
# ★ 2026-10-25 修正: v2.6.5 版这里传的是「文件内容(多行字符串)」,
#   导致 release.ps1 里的 `Test-Path $ReleaseNotes` 拿带换行的正文当路径 ->
#   抛「路径中具有非法字符」-> ErrorActionPreference=Stop -> 脚本第 4 步整个退出。
#   现在直接传路径: release.ps1 内部会自己用 [IO.File]::ReadAllText(path, UTF8) 读,
#   既避开 GBK 污染, 又避开 Test-Path 踩雷。
$ErrorActionPreference = "Stop"
$gitRoot = "D:\Work\A3Tools"
Set-Location $gitRoot

$notesPath = Join-Path $gitRoot "publish\RELEASE_NOTES_v2.6.6.md"
if (-not (Test-Path $notesPath)) { Write-Error "notes not found: $notesPath"; exit 1 }

# 自检: 确认能按 UTF-8 读出中文(防 GBK 污染回归)
$probe = [System.IO.File]::ReadAllText($notesPath, [System.Text.Encoding]::UTF8)
if ($probe -notmatch "[\u4e00-\u9fff]") { Write-Error "notes 缺中文, 疑似编码问题"; exit 1 }
Write-Host ("[wrapper] notes OK: " + $notesPath + " (" + $probe.Length + " chars, 中文校验通过)") -ForegroundColor Cyan

& (Join-Path $gitRoot "scripts\release.ps1") -Version "2.6.6" -ReleaseNotes $notesPath
