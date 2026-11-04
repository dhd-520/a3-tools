# v2.6.17 one-click release wrapper
# Purpose: pass the release notes FILE PATH (not the multi-line content) to release.ps1
#
# NOTE: this script is intentionally ASCII-only. PS 5.1 reads a .ps1 without BOM
# using the system ANSI codepage (GBK on this machine), so literal CJK chars
# inside the script file would turn into mojibake. All CJK here is built from
# code points at runtime.
$ErrorActionPreference = "Stop"
$gitRoot = "D:\Work\A3Tools"
Set-Location $gitRoot

$notesPath = Join-Path $gitRoot "release-notes-v2.6.17.md"
if (-not (Test-Path $notesPath)) { Write-Error "notes not found: $notesPath"; exit 1 }

# Self-check: read the notes as UTF-8 and make sure real CJK survived.
$cjkRange = "[" + [char]0x4e00 + "-" + [char]0x9fff + "]"
$probe = [System.IO.File]::ReadAllText($notesPath, [System.Text.Encoding]::UTF8)
if ($probe -notmatch $cjkRange) { Write-Error "notes lost CJK, suspect encoding"; exit 1 }
Write-Host ("[wrapper] notes OK: " + $notesPath + " (" + $probe.Length + " chars, CJK check passed)") -ForegroundColor Cyan

& (Join-Path $gitRoot "scripts\release.ps1") -Version "2.6.17" -ReleaseNotes $notesPath
