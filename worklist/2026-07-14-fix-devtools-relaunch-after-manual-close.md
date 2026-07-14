# 2026-07-14 修复手动关闭开发工具后无法重新启动

## 问题
陛下反馈：
> 客户端和开发工具一起启动，然后手动关掉了开发工具，而不是用工具一键关闭，
> 这时候就无法启动开发工具，会提示已启动并把客户端前台显示，但是我要启动的是开发工具

## 根因
`AccountStatus.ProcessIds` 是**混合 list** — 客户端 / 开发工具 / web 浏览器的 PID 全混在一起，只靠 bool 标记（`IsClientRunning` / `IsDevToolsRunning` / ...）区分类型。

死进程清理（`RefreshAccountStatuses` + `GetActiveAccountProcessIds` 内部清理）**只清 PID 不同步 bool 标记**。

### 重现路径
1. 启动账套 A → 客户端 PID=100，开发工具 PID=200
2. `_accountStatuses["A"].ProcessIds = [100, 200]`，`IsClientRunning=true`，`IsDevToolsRunning=true`
3. 手动关掉开发工具 → 200 死了，但 `_accountStatuses` 完全没变
4. 再次点启动账套 → 试图启动开发工具
5. `TryBringAccountProcessesToFront(code, "dev")` 调用
6. 旧 `GetActiveAccountProcessIds` 遍历 `[100, 200]`：
   - PID 100 存活 + `IsDevToolsRunning=true` → 加进 dev 结果（**但 100 是客户端 PID！**）
   - PID 200 已死 → 加进 dead 列表
7. 返回 `[100]`（客户端 PID）
8. `TryBringAccountProcessesToFront` 把客户端窗口切到前台
9. toast 显示「开发工具已在运行，已切到前台」
10. **开发工具永远不会启动**，用户以为启动了但实际没有

`IsDevToolsRunning` 永远不会被重置（除非用一键关闭，那会清空整个账套状态），所以这个 bug 会一直存在。

## 修复
按进程类型分别存储 PID，清理死进程时同步重算 bool 标记。

### 文件改动

#### 1. `A3Tools.Common/Models/AccountStatus.cs`
增加 5 个类型化 PID list（保留 `ProcessIds` 作为合并显示用）：

```csharp
public List<int> ClientProcessIds { get; set; } = new();
public List<int> DevToolsProcessIds { get; set; } = new();
public List<int> WebProcessIds { get; set; } = new();
public List<int> DbProcessIds { get; set; } = new();
public List<int> RemoteProcessIds { get; set; } = new();
```

#### 2. `A3Tools/Forms/MainForm.cs` - `RecordProcess`
PID 加到对应类型 list（保留合并 ProcessIds）：

```csharp
case "client":
    if (!status.ClientProcessIds.Contains(processId))
        status.ClientProcessIds.Add(processId);
    status.IsClientRunning = true;
    break;
// ... dev / web / db / remote 同理
```

#### 3. `A3Tools/Forms/MainForm.cs` - `GetActiveAccountProcessIds`
按类型取对应 PID list（不再靠 bool 标记判断）：

```csharp
List<int> typedList = processType.ToLower() switch
{
    "client" => status.ClientProcessIds,
    "dev" => status.DevToolsProcessIds,
    "web" => status.WebProcessIds,
    "db" => status.DbProcessIds,
    "remote" => status.RemoteProcessIds,
    _ => status.ProcessIds,
};
```

清理死 PID 后**同步 bool 标记**：
```csharp
if (status.ClientProcessIds.Count == 0) status.IsClientRunning = false;
if (status.DevToolsProcessIds.Count == 0) status.IsDevToolsRunning = false;
// ... web / db / remote 同理
```

#### 4. `A3Tools/Forms/MainForm.cs` - `RefreshAccountStatuses`
重构：按类型化 list 分别清理死 PID（新增 `CleanupDeadPids` 私有方法），同步 bool 标记，清理合并 ProcessIds 里已死的 PID。

## Build
A3Tools.Common + A3Tools.Plugins.Default + A3Tools：**0 错**（仅有 2 个原有 NU1701 警告）

## 验证场景
- [x] 启动账套（客户端 + 开发工具）
- [x] 手动关掉开发工具
- [x] 再次点启动账套 → 应该正常启动新开发工具（不再误切客户端前台）
- [x] DataGridView 显示「开发工具」勾选框应该正确反映实际状态

## 影响面
只动了 `AccountStatus.cs` + `MainForm.cs` 3 个方法（RecordProcess / GetActiveAccountProcessIds / RefreshAccountStatuses）+ 1 个新私有方法 CleanupDeadPids。其他位置没动。
