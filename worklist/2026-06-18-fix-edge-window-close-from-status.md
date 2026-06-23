# 2026-06-18 修复 Edge 新增窗口无法在账套运行情况中关闭

## 问题
陛下反馈：Edge 浏览器新增窗口后，无法在【账套运行情况】中关闭。

## 根因
Edge/Chrome 是多进程浏览器：

- `Process.Start` 返回的 PID 可能只是 browser 主进程或启动壳进程；
- Edge 可能在 2 秒后将窗口转交给其它进程；
- 原逻辑只登记 `Process.Start` 返回的 PID；
- 【账套运行情况】关闭时只 `p.Kill()` 单个 PID，没有杀进程树。

因此实际承载窗口的 Edge 子进程/同 profile 新进程可能没有被登记，或者关闭时没有被杀掉。

## 修改
文件：`A3Tools/Forms/MainForm.cs`

### 1. 启动浏览器时登记新增浏览器进程
在主浏览器路径分支和注册表 fallback 分支中：

1. 启动前调用 `GetExistingBrowserPids(browser)` 记录现有 PID；
2. `Process.Start` 后等待 Edge/Chrome 稳定；
3. 调用 `GetNewBrowserPids(browser, existingPids)` 计算差集；
4. 将新增的所有浏览器 PID 都 `RecordProcess(accountCode, pid, "web")`。

如果差集为空但 `Process.Start` 返回进程仍存活，则兜底登记该 PID。

### 2. 关闭时杀进程树
`CloseAccountProcesses` 中将：

```csharp
p.Kill();
```

改为优先：

```csharp
p.Kill(entireProcessTree: true);
```

失败时再 fallback 到 `p.Kill()`。

同时关闭后从 `_processIds` 和 `_processLaunchModes` 中移除 PID。

## 验证
- 执行：`dotnet build D:\work\A3Tools\A3Tools.sln -c Release`
- 结果：0 错误，149 个历史 warning。

## 打包
已重新发布 StandaloneSF：

- 输出目录：`D:\work\A3Tools\publish\StandaloneSF`
- `A3Tools.exe`：77,675,995 bytes
- 总大小：约 76.14 MB
- 已清理旧 `cdp.log` / `win32-login.log` 和 `~$*` 临时文件。

## 测试建议
1. 设置浏览器为 Edge；
2. 勾选“启动新窗口”；
3. 启动账套 Web；
4. 打开【账套运行情况】；
5. 点击该账套行【关闭】；
6. Edge 新增窗口及其关联进程应被关闭。
