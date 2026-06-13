# A3Tools Framework2 打包脚本（2026-06-13）
#
# Framework2 是多文件模式（PublishSingleFile=false）：
# - Plugins.dll 会被正常输出到 OutputPath（不被吞进 exe）
# - CopyPluginsToFolder Target 正常工作，StandaloneFramework/Plugins/ 会有最新 Plugins.dll
# - A3Tools.Common.dll 在 Framework2 模式下也作为独立文件保留
#
# 本脚本只做：clean → build → publish → ILRepack 合并 → 瘦身 → 复制 Plugins 依赖
# 调用方在项目根目录运行：.\scripts\publish-framework2.ps1

$ErrorActionPreference = "Stop"

# 路径
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\Framework2"
$repack = "D:\work\A3Tools\tools\ILRepack\ilrepack\tools\ILRepack.exe"

Write-Host "=== A3Tools Framework2 打包脚本 ===" -ForegroundColor Cyan
Write-Host "项目根: $root"
Write-Host "输出目录: $publishDir"
Write-Host ""

# Step 1: 清理旧产物
Write-Host "[1/6] 清理旧 publish 目录..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# Step 2: 完整 build（让所有项目的 bin 目录有最新 dll）
Write-Host "[2/6] 完整 build 所有项目..." -ForegroundColor Yellow
Set-Location $root
& dotnet build A3Tools.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "build 失败" }

# Step 3: publish 多文件模式
Write-Host "[3/6] publish 多文件模式..." -ForegroundColor Yellow
& dotnet publish A3Tools\A3Tools.csproj -c Release --no-self-contained -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }

# Step 4: ILRepack 合并托管 DLL 到 A3Tools.dll
Write-Host "[4/6] ILRepack 合并 46 个托管 DLL..." -ForegroundColor Yellow
Set-Location $publishDir
$repackArgs = @(
    "A3Tools.dll",
    "/out:A3Tools.dll",
    "/lib:.",
    "Azure.Core.dll","Azure.Identity.dll","Microsoft.Bcl.AsyncInterfaces.dll","Microsoft.Data.Tools.Sql.BatchParser.dll",
    "Microsoft.Identity.Client.dll","Microsoft.Identity.Client.Extensions.Msal.dll","Microsoft.IdentityModel.Abstractions.dll",
    "Microsoft.IdentityModel.JsonWebTokens.dll","Microsoft.IdentityModel.Logging.dll","Microsoft.IdentityModel.Protocols.dll",
    "Microsoft.IdentityModel.Protocols.OpenIdConnect.dll","Microsoft.IdentityModel.Tokens.dll",
    "Microsoft.SqlServer.Assessment.dll","Microsoft.SqlServer.Assessment.Types.dll","Microsoft.SqlServer.ConnectionInfo.dll",
    "Microsoft.SqlServer.Dmf.Common.dll","Microsoft.SqlServer.Dmf.dll","Microsoft.SqlServer.Management.Assessment.dll",
    "Microsoft.SqlServer.Management.Collector.dll","Microsoft.SqlServer.Management.CollectorEnum.dll",
    "Microsoft.SqlServer.Management.HadrData.dll","Microsoft.SqlServer.Management.HadrModel.dll",
    "Microsoft.SqlServer.Management.RegisteredServers.dll","Microsoft.SqlServer.Management.Sdk.Sfc.dll",
    "Microsoft.SqlServer.Management.SqlScriptPublish.dll","Microsoft.SqlServer.Management.XEvent.dll",
    "Microsoft.SqlServer.Management.XEventDbScoped.dll","Microsoft.SqlServer.Management.XEventDbScopedEnum.dll",
    "Microsoft.SqlServer.Management.XEventEnum.dll","Microsoft.SqlServer.PolicyEnum.dll","Microsoft.SqlServer.Server.dll",
    "Microsoft.SqlServer.ServiceBrokerEnum.dll","Microsoft.SqlServer.Smo.dll","Microsoft.SqlServer.Smo.Notebook.dll",
    "Microsoft.SqlServer.SmoExtended.dll","Microsoft.SqlServer.SqlEnum.dll","Microsoft.SqlServer.SqlWmiManagement.dll",
    "Microsoft.SqlServer.WmiEnum.dll","Newtonsoft.Json.dll","NPinyin.dll","System.ClientModel.dll",
    "System.Data.OleDb.dll","System.IdentityModel.Tokens.Jwt.dll","System.Management.dll","System.Memory.Data.dll",
    "System.Runtime.Caching.dll"
)
& $repack $repackArgs 2>&1 | Select-Object -Last 5

# Step 5: 清理 - 删除已合并 dll + 多语言包 + 非 win-x64 runtimes + pdb
Write-Host ""
Write-Host "[5/6] 清理 + 瘦身..." -ForegroundColor Yellow
$merged = @("Azure.Core.dll","Azure.Identity.dll","Microsoft.Bcl.AsyncInterfaces.dll","Microsoft.Data.Tools.Sql.BatchParser.dll",
"Microsoft.Identity.Client.dll","Microsoft.Identity.Client.Extensions.Msal.dll","Microsoft.IdentityModel.Abstractions.dll",
"Microsoft.IdentityModel.JsonWebTokens.dll","Microsoft.IdentityModel.Logging.dll","Microsoft.IdentityModel.Protocols.dll",
"Microsoft.IdentityModel.Protocols.OpenIdConnect.dll","Microsoft.IdentityModel.Tokens.dll",
"Microsoft.SqlServer.Assessment.dll","Microsoft.SqlServer.Assessment.Types.dll","Microsoft.SqlServer.ConnectionInfo.dll",
"Microsoft.SqlServer.Dmf.Common.dll","Microsoft.SqlServer.Dmf.dll","Microsoft.SqlServer.Management.Assessment.dll",
"Microsoft.SqlServer.Management.Collector.dll","Microsoft.SqlServer.Management.CollectorEnum.dll",
"Microsoft.SqlServer.Management.HadrData.dll","Microsoft.SqlServer.Management.HadrModel.dll",
"Microsoft.SqlServer.Management.RegisteredServers.dll","Microsoft.SqlServer.Management.Sdk.Sfc.dll",
"Microsoft.SqlServer.Management.SqlScriptPublish.dll","Microsoft.SqlServer.Management.XEvent.dll",
"Microsoft.SqlServer.Management.XEventDbScoped.dll","Microsoft.SqlServer.Management.XEventDbScopedEnum.dll",
"Microsoft.SqlServer.Management.XEventEnum.dll","Microsoft.SqlServer.PolicyEnum.dll","Microsoft.SqlServer.Server.dll",
"Microsoft.SqlServer.ServiceBrokerEnum.dll","Microsoft.SqlServer.Smo.dll","Microsoft.SqlServer.Smo.Notebook.dll",
"Microsoft.SqlServer.SmoExtended.dll","Microsoft.SqlServer.SqlEnum.dll","Microsoft.SqlServer.SqlWmiManagement.dll",
"Microsoft.SqlServer.WmiEnum.dll","Newtonsoft.Json.dll","NPinyin.dll","System.ClientModel.dll",
"System.Data.OleDb.dll","System.IdentityModel.Tokens.Jwt.dll","System.Management.dll","System.Memory.Data.dll",
"System.Runtime.Caching.dll")
$merged | ForEach-Object { Remove-Item $_ -ErrorAction SilentlyContinue }
# 删除非中文多语言包
@("de","es","fr","it","ja","ko","pt-BR","ru") | ForEach-Object { Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue }
# 删除 pdb（陛下 2026-06-13 决定保留 pdb，注：Framework2 下的 pdb 是必要的吗？保持现状不动）
# 删除非 win-x64 runtimes
Remove-Item "runtimes\unix","runtimes\win","runtimes\win-arm","runtimes\win-arm64","runtimes\win-x86","runtimes\win-x64\lib" -Recurse -Force -ErrorAction SilentlyContinue

# Step 6: 复制 Plugins.dll + Common.dll + SqlClient 依赖到 Plugins/ 目录
# 原因：Plugins.dll 运行时引用 A3Tools.Common.dll 和 Microsoft.Data.SqlClient.dll，Plugins/ 同级目录必须有这些依赖
Write-Host "[6/6] 复制 Plugins 依赖到 Plugins/ 目录..." -ForegroundColor Yellow
$pluginsDest = Join-Path $publishDir "Plugins"
if (-not (Test-Path $pluginsDest)) {
    New-Item -ItemType Directory -Path $pluginsDest -Force | Out-Null
}
$binDir = Join-Path $root "A3Tools\bin\Release\net7.0-windows"
$commonBin = Join-Path $root "A3Tools.Common\bin\Release\net7.0-windows"
Copy-Item (Join-Path $binDir "Plugins\A3Tools.Plugins.Default.dll") (Join-Path $pluginsDest "A3Tools.Plugins.Default.dll") -Force
Copy-Item (Join-Path $commonBin "A3Tools.Common.dll") (Join-Path $pluginsDest "A3Tools.Common.dll") -Force
Copy-Item (Join-Path $binDir "Microsoft.Data.SqlClient.dll") (Join-Path $pluginsDest "Microsoft.Data.SqlClient.dll") -Force
Copy-Item (Join-Path $binDir "runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll") (Join-Path $pluginsDest "Microsoft.Data.SqlClient.SNI.dll") -Force
Copy-Item (Join-Path $publishDir "tools.json") (Join-Path $pluginsDest "tools.json") -ErrorAction SilentlyContinue
Write-Host "  - A3Tools.Plugins.Default.dll + A3Tools.Common.dll + Microsoft.Data.SqlClient.dll + .SNI.dll → Plugins/"

# 统计
Write-Host ""
Write-Host "=== Framework2 打包完成 ===" -ForegroundColor Cyan
Get-ChildItem $publishDir -Recurse -File | Select-Object Name, Length | Format-Table -AutoSize
$total = (Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum
Write-Host ("总大小: {0:N2} MB" -f ($total / 1MB)) -ForegroundColor Green
