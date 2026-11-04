# v2.6.17 一键发布包装脚本
# 用途: 把 release notes **文件路径** 传给 release.ps1
#
# ★ 沿用 v2.6.6 的修正: 传「文件路径」而不是「文件内容(多行字符串)」,
#   否则 release.ps1 里的 `Test-Path $ReleaseNotes` 拿带换行的正文当路径 -> 抛错。
#   release.ps1 内部会自己用 [IO.File]::ReadAllText(path, UTF8) 读, 避开 GBK 污染。
#
# ★ v2.6.17: notes 放在仓库根目录 release-notes-v2.6.17.md（随源码一起提交），
#   release.ps1 会再写一份到 publish\RELEASE_NOTES_v2.6.17.md 并嵌进 zip。
$ErrorActionPreference = "Stop"
$gitRoot = "D:\Work\A3Tools"
Set-Location $gitRoot

$notesPath = Join-Path $gitRoot "release-notes-v2.6.17.md"
if (-not (Test-Path $notesPath)) { Write-Error "notes not found: $notesPath"; exit 1 }

# 自检: 确认能按 UTF-8 读出中文(防 GBK 污染回归)
$probe = [System.IO.File]::ReadAllText($notesPath, [System.Text.Encoding]::UTF8)
if ($probe -notmatch "[一-龥]") { Write-Error "notes 缺中文, 疑似编码问题"; exit 1 }
Write-Host ("[wrapper] notes OK: " + $notesPath + " (" + $probe.Length + " chars, 中文校验通过)") -ForegroundColor Cyan

& (Join-Path $gitRoot "scripts\release.ps1") -Version "2.6.17" -ReleaseNotes $notesPath
