# 2026-06-18 修复 Edge 启动时每次弹设置项

## 问题
陛下反馈：A3Tools 启动 Edge 时，每次都会跳出 Edge 设置项/首次运行体验。

## 根因
A3Tools 在网页自动登录（CDP）模式下会使用独立的临时 `--user-data-dir` 启动浏览器。Chrome 分支已经带有：

- `--no-first-run`
- `--no-default-browser-check`
- 以及一组禁用后台网络/同步/扩展等参数

但 Edge 分支之前只传：

- `--new-window`
- `--start-maximized`
- `--user-data-dir=...`
- `--remote-debugging-port=...`

Edge 每次看到新的临时 profile，就会认为是首次运行，从而弹设置项。

## 修改
在 `A3Tools/Forms/MainForm.cs` 的 `BuildBrowserArgs` 中，给 `msedge` 新窗口模式和非新窗口 CDP 参数补充：

- `--no-first-run`
- `--no-default-browser-check`
- `--disable-features=msEdgeFirstRunExperience`
- `--disable-extensions`
- `--disable-background-networking`
- `--disable-sync`
- `--disable-translate`
- `--disable-background-timer-throttling`
- `--disable-renderer-backgrounding`

## 验证
- 执行 `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`
- 结果：0 错误，149 个历史 warning。

## 打包
已重新发布 StandaloneSF：

```bash
dotnet publish D:\work\A3Tools\A3Tools\A3Tools.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o D:\work\A3Tools\publish\StandaloneSF
```

输出：
- `D:\work\A3Tools\publish\StandaloneSF\A3Tools.exe`
- 文件大小：77,675,914 bytes
- 总大小：约 76.04 MB

发布后清理：
- 删除旧残留 `cdp.log` / `win32-login.log`
- 删除 Word 临时锁文件 `~$工具箱_用户手册.docx`
- 检查输出目录无 `*.log` 和 `~$*` 临时文件。
