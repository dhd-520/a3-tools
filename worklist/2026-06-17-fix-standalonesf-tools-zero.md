# 2026-06-17 修复 StandaloneSF 工具箱 0 个问题

## 问题
打包后工具箱显示 0 个工具。

## 根因
1. StandaloneSF 发布目录 Plugins 下缺少 A3Tools.Common.dll。A3Tools.Plugins.Default.dll 引用 A3Tools.Common，缺失时 Assembly.LoadFrom 失败，工具加载为 0。
2. A3Tools.csproj 之前把 A3Tools\Plugins\A3Tools.Plugins.Default.dll 作为内容文件复制到发布目录，容易覆盖真正构建出来的新插件 DLL，导致新增工具类不存在。
3. ToolsConfigService 使用 AppDomain.CurrentDomain.BaseDirectory，单文件发布下可能指向解压临时目录；应与 ToolExecutorService 一样使用 AppContext.BaseDirectory。
4. ToolsConfigService 文件读写未显式指定 UTF-8，存在中文 Windows 环境编码隐患。

## 修改
- A3Tools.csproj：
  - 移除 Plugins\A3Tools.Plugins.Default.dll 的内容复制项。
  - Build 后复制 A3Tools.Plugins.Default.dll 和 A3Tools.Common.dll 到输出目录 Plugins。
  - Publish 后从真实输出目录复制 A3Tools.Plugins.Default.dll 和 A3Tools.Common.dll 到发布目录 Plugins，避免旧 DLL 覆盖。
- ToolsConfigService.cs：
  - 使用 AppContext.BaseDirectory 优先定位 exe 真实目录。
  - File.ReadAllText / File.WriteAllText 显式使用 Encoding.UTF8。

## 验证
- 已执行 dotnet publish StandaloneSF，退出码 0。
- 发布目录 D:\work\A3Tools\publish\StandaloneSF\Plugins 现包含：
  - A3Tools.Common.dll
  - A3Tools.Plugins.Default.dll
  - tools.json
- 临时 .NET 程序验证 tools.json 10 个 className 全部能从插件程序集 GetType 成功：OK=10, FAIL=0。
