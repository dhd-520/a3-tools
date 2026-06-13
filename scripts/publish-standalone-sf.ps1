# A3Tools StandaloneSF 打包脚本（2026-06-13）
# 
# 单文件发布模式的特殊问题：
# - dotnet publish 流程下，Plugins 项目的 build 输出会被重定向到 obj/ 目录，
#   bin/.../Plugins/A3Tools.Plugins.Default.dll 不存在
# - CopyPluginsToFolder Target 的 SourceFiles 找不到源
# - A3Tools.Common.dll 在单文件模式被吞进 exe，插件加载时找不到依赖
#
# 解决方案：publish 完成后用 PowerShell 手动把 Plugins.dll + Common.dll 复制到输出目录
# 调用方在项目根目录运行：.\scripts\publish-standalone-sf.ps1

$ErrorActionPreference = "Stop"

# 路径
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\StandaloneSF"

Write-Host "=== A3Tools StandaloneSF 打包脚本 ===" -ForegroundColor Cyan
Write-Host "项目根: $root"
Write-Host "输出目录: $publishDir"
Write-Host ""

# Step 1: 清理旧产物
Write-Host "[1/4] 清理旧 publish 目录..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# Step 2: 单独编译 Plugins 项目（让 A3Tools\bin\.../Plugins\ 有最新的 dll）
Write-Host "[2/4] 编译 Plugins 项目（生成最新的 Plugins.dll + Common.dll 到 bin 目录）..." -ForegroundColor Yellow
Set-Location $root
& dotnet build A3Tools.Plugins.Default\A3Tools.Plugins.Default.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Plugins 项目编译失败" }

# Step 3: 编译 Common 项目（让 A3Tools.Common\bin\... 有最新的 dll）
Write-Host "[3/4] 编译 Common 项目（生成最新的 A3Tools.Common.dll）..." -ForegroundColor Yellow
& dotnet build A3Tools.Common\A3Tools.Common.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Common 项目编译失败" }

# Step 4: 发布单文件版
Write-Host "[4/4] 发布单文件版..." -ForegroundColor Yellow
& dotnet publish A3Tools\A3Tools.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }

# Step 5: 手动复制最新的 Plugins.dll 和 Common.dll 到 StandaloneSF/Plugins/
# （publish 流程下 Target 复制用旧 dll，手动覆盖保证最新）
Write-Host ""
Write-Host "[补充] 复制最新的 Plugins.dll + Common.dll 到 StandaloneSF/Plugins/..." -ForegroundColor Yellow
$binPluginsDir = Join-Path $root "A3Tools\bin\Release\net7.0-windows\Plugins"
$binCommonDir = Join-Path $root "A3Tools.Common\bin\Release\net7.0-windows"
$dstPluginsDir = Join-Path $publishDir "Plugins"

if (-not (Test-Path $dstPluginsDir)) {
    New-Item -ItemType Directory -Path $dstPluginsDir -Force | Out-Null
}

$pluginsSrc = Join-Path $binPluginsDir "A3Tools.Plugins.Default.dll"
$commonSrc = Join-Path $binCommonDir "A3Tools.Common.dll"

if (Test-Path $pluginsSrc) {
    Copy-Item $pluginsSrc -Destination $dstPluginsDir -Force
    Write-Host "  ✅ 复制 Plugins.dll" -ForegroundColor Green
} else {
    throw "找不到 Plugins.dll: $pluginsSrc"
}

if (Test-Path $commonSrc) {
    Copy-Item $commonSrc -Destination $dstPluginsDir -Force
    Write-Host "  ✅ 复制 A3Tools.Common.dll（解决插件依赖丢失）" -ForegroundColor Green
} else {
    throw "找不到 A3Tools.Common.dll: $commonSrc"
}

# Step 6: 统计
Write-Host ""
Write-Host "=== 打包完成 ===" -ForegroundColor Cyan
Get-ChildItem $publishDir -Recurse -File | Select-Object Name, Length | Format-Table -AutoSize
$total = (Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum
Write-Host ("总大小: {0:N2} MB" -f ($total / 1MB)) -ForegroundColor Green
