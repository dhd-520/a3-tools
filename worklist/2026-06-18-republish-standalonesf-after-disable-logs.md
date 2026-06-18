# 2026-06-18 重新打包 StandaloneSF（禁用日志文件后）

## 背景
修复 `CdpHelper.CdpLog` / `Win32AutoLoginHelper.WinLog` 后，陛下要求重新打一个包用于测试，重点验证运行后不再生成：

- `cdp.log`
- `win32-login.log`

## 操作

### 1. Release 编译
```bash
dotnet build D:\work\A3Tools\A3Tools.sln -c Release
```
结果：0 错误，2 个 NPinyin 兼容性历史 warning。

### 2. StandaloneSF 发布
```bash
dotnet publish D:\work\A3Tools\A3Tools\A3Tools.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o D:\work\A3Tools\publish\StandaloneSF
```
结果：退出码 0。

### 3. 清理旧日志残留
`dotnet publish -o` 不会自动清空输出目录，因此发布后发现目录内还残留旧文件：

- `D:\work\A3Tools\publish\StandaloneSF\cdp.log`
- `D:\work\A3Tools\publish\StandaloneSF\win32-login.log`

已手动删除。删除后递归检查 `*.log`，无输出。

## 输出目录
`D:\work\A3Tools\publish\StandaloneSF`

主要文件：
- `A3Tools.exe`：77,675,833 bytes
- `A3Tool.ico`
- `A3Tools.pdb`
- `A3Tools.Common.pdb`
- `A3Tools.Plugins.Default.pdb`
- `A3工具箱_用户手册.docx`
- `DATA\`
- `Plugins\`

插件目录：
- `Plugins\A3Tools.Plugins.Default.dll`：284,160 bytes
- `Plugins\A3Tools.Common.dll`：15,872 bytes
- `Plugins\tools.json`：3,365 bytes

总大小：约 76.14 MB。

## 验证
- 发布成功，退出码 0。
- `Plugins` 目录包含 `A3Tools.Common.dll`，避免工具箱 0 个问题复发。
- 发布目录递归检查无 `*.log` 文件。
