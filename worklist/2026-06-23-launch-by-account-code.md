# 启动账套按账套 Code 判断（不再用进程名全局判断）

**日期：** 2026-06-23  
**类型：** Bug 修复 + 重构

## 问题

之前客户端/开发工具的"防重复启动"用的是进程名全局判断：

```csharp
if (Win32AutoLoginHelper.IsProcessRunning("君则A3")) { 切前台; return; }
```

导致：

- 账套 A 启动了 A3 客户端
- 启动账套 B → 看到「君则A3」已在跑 → 直接切到 A 的客户端前台，**B 永远启不起来**

实际场景里多个账套同时挂着的需求是合理的（开发/测试要切来切去）。

## 解决方案

判断依据从「进程名」改为「(账套 Code, 进程类型)」组合。

A3 客户端和开发工具进程名都一样（`君则A3` / `君则A3集成开发工具`），**光靠进程名根本分不出账套**。所以方案是：

1. **用账套→进程的映射**（`_accountStatuses[code].ProcessIds`）作为权威记录
2. 启动前查：「这个账套 Code 是否已经启动过该类型进程？」
3. 用 PID 切前台（不是进程名）

## 改动

### Win32AutoLoginHelper.cs（按 PID 切前台）

新增 3 个方法：

```csharp
// 按 PID 找主窗口（MainWindowHandle → EnumWindows 兜底）
public static bool TryGetProcessWindow(int processId, out IntPtr hWnd)

// 按 PID 把窗口拉到前台（用于「按账套切前台」场景）
public static bool BringProcessByIdToFront(int processId)

// 把前台切换逻辑抽出来共用（私有）
private static bool BringWindowToFront(IntPtr hWnd)
```

旧的 `BringProcessToFront(processName, ...)` 保留，内部改成调 `BringWindowToFront` —— 不影响其他调用方。

### MainForm.cs

新增 2 个辅助方法（RecordProcess 后面）：

```csharp
// 按账套 Code + 进程类型查找仍存活的 PID 列表
// 自动过滤已退出进程 + 清理死 PID
private List<int> GetActiveAccountProcessIds(string code, string processType)

// 把指定账套已启动的进程切到前台；都死了返回 false
private bool TryBringAccountProcessesToFront(string code, string processType)
```

启动块改成：

```csharp
// 客户端
if (TryBringAccountProcessesToFront(account.Code, "client"))
{
    ShowToast($"账套【{account.Name}】客户端已在运行，已切到前台");
}
else { /* 启动新进程 */ }

// 开发工具
if (TryBringAccountProcessesToFront(account.Code, "dev"))
{
    ShowToast($"账套【{account.Name}】开发工具已在运行，已切到前台");
}
else { /* 启动新进程 */ }
```

## 关键细节

1. **类型过滤用 AccountStatus 的 bool 标记**（`IsClientRunning` / `IsDevToolsRunning`）而不是字符串前缀匹配，更可靠
2. **死进程清理**：每次查询时顺手把死 PID 从 `_accountStatuses[code].ProcessIds` / `_processIds` / `_processLaunchModes` 三个列表都清掉，避免越积越多
3. **切前台失败也清 PID**：找不到主窗口句柄说明进程已经"半死不活"，清掉让下次能正常重启
4. **Toast 文案带账套名**：之前"A3 客户端已在运行"看不出是哪个账套，现在显示「账套【标准8088】客户端已在运行」

## 不影响的部分

- `LaunchWebBrowser`（web 已经在 RecordProcess 时按账套记 PID 了）
- `LaunchDbConnect` / `LaunchRemoteConnect`（DB 远程连接是连接对象，不是进程，不存在"重复启动"问题）
- 老的 `BringProcessToFront(processName, ...)` 和 `IsProcessRunning(processName)` 仍然保留兼容

## 测试要点

| 场景 | 预期 |
|---|---|
| 启动账套 A → 客户端启动成功 | ✓ 正常启动 |
| 账套 A 客户端跑着，再启动账套 B → 客户端启动 | ✓ B 应能正常启动自己的客户端 |
| 账套 A 客户端跑着，再启动账套 A 客户端 | ✓ 切到 A 客户端前台，不重启 |
| 关闭账套 A 客户端（手动 kill），再启动账套 A | ✓ 死 PID 被清理，正常启动 |
| 启动账套 A 开发工具 + 启动账套 B 客户端 | ✓ 两个独立进程互不影响 |

## 编译结果

- 0 错误
- 2 警告（NPinyin 兼容性，已有）