# A3 程序更新序列化（场景3 完整闭环 v3）

**日期**：2026-08-14 08:41 ~ 09:28
**状态**：✅ 编译通过（0 错 343 警告），待陛下重测
**主要提交者**：哈士奇（陛下反馈，臣实现）

## 零、第4轮修复（09:25）

### 陛下反馈
> "升级完成确定又没有处理，我手动点后也没有自动启动"

### 问题诊断
**阶段1 只找「升级文件检测」系列标题**，不包含「升级完成」/「系统提示」等标题。
**时序问题**：
1. launcher 入口启阶段1（监听升级文件检测框）
2. 启 client → client 升级中 → launcher 在 WaitForClientUpgradeComplete 循环 DoEvents
3. **client 升级完成 → A3 弹"升级完成!"框** → **阶段1不抓这个标题** → 没人处理！
4. launcher 末尾 `StopA3ProgramUpdateWatcher()` → 阶段2 接管
5. 阶段2 从阶段1退出瞬间开始等"升级完成!"框 → 但升级完成框在步骤3 已经弹出过 + 可能在阶段1退出前就消失了

**结果**：升级完成框在阶段1/阶段2交接间隙无人处理。

### 修复
**阶段1 同时检测升级完成框**：
- `FindUpdateDialog`（升级文件检测）保持
- 新增 `FindUpgradeCompleteDialog`（升级完成）+ `ClickUpgradeCompleteButton`
- 阶段1 循环里：先查升级文件检测 → 再查升级完成框 → 都在阶段1 处理

## 一、各轮修复汇总

### 第1轮（09:04）陛下明确需求
- 同时启 client+devtools 默认升级+序列化
- 杀 launcher 自己启的 devtools（释放文件锁）
- 等 client升级完成 → 启 devtools

### 第2轮（09:11）错误修复尝试
- 改 needSerializeUpgrade 触发条件（两勾就触发）
- 改 HandleA3ProgramUpdateFound 场景2 自动点"是"
- 阶段1 改持续监听（CancellationTokenSource）

### 第3轮（09:12）bug 修复尝试
- `Thread.Sleep` 阻塞 main thread 消息泵 → Invoke 永远 marshal 失败
- 改成 `Task.Delay` async 异步 sleep（但用 GetAwaiter().GetResult 在 UI 线程同步等仍有死锁问题）

### 第4轮（09:16）bug 修复尝试
- 用 `ManualResetEvent.WaitOne()` 等后台完成 → 但 Invoke 的 PostMessage 不能 set 这个事件，main thread 卡死
- 改成 `Application.DoEvents()` + `WaitOne(0)` 自旋循环 → 让 main thread 主动跑消息泵 → Invoke 能 marshal ✅

### 第5轮（09:25）升级完成框处理
- 阶段1 不抓"升级完成!"框标题 → 升级完成框在阶段1/阶段2交接间隙无人处理
- 阶段1 同时检测升级完成框（`FindUpgradeCompleteDialog` + `ClickUpgradeCompleteButton`）

## 二、文件改动清单

### 修改文件
- `A3Tools/Forms/MainForm.cs`
  - 新增 `_a3WatcherCts` 字段（CancellationTokenSource）
  - 新增 `_clientUpgradeCompleteEvent` 字段（ManualResetEvent）
  - 改 `needSerializeUpgrade` 触发条件（两勾就触发）
  - 加"杀 launcher 自己启的 devtools"逻辑
  - 改 `StartA3ProgramUpdateWatcher` 持续监听模式
  - 加 `StopA3ProgramUpdateWatcher` 方法
  - 改 `HandleA3ProgramUpdateFound` 场景2 自动点"是"
  - 简化 `PrepareUpdateScenarioForLaunch` JointSpawn 分支
  - `WaitForClientUpgradeComplete` → `WaitForClientUpgradeCompleteAsync`（async + Task.Delay）
  - 改为 `Application.DoEvents()` + `WaitOne(0)` 自旋循环跑消息泵
  - 阶段1 同时检测升级完成框
  - `LaunchSelectedAccount` 末尾调 `StopA3ProgramUpdateWatcher()`

- `A3Tools/Services/A3ProgramUpdateChecker.cs`
  - 新增 `FindUpdateDialog(out IntPtr hwnd)` 公开方法
  - 新增 `FindUpgradeCompleteDialog(out IntPtr hwnd)` 公开方法
  - 新增 `ClickUpgradeCompleteButton(IntPtr hwnd)` 公开方法

## 三、Build 结果

```
0 个错误
343 个警告（原项目既有的，6 个）
```

## 四、关键经验教训

1. **Thread.Sleep 在 UI 线程会卡消息泵** → 后台线程 Invoke 永远进不去 → 死锁
2. **ManualResetEvent.WaitOne 在 UI 线程** → 等 OS 事件，PostMessage 不能 set 这个事件 → main thread 仍然收不到 Invoke
3. **Application.DoEvents() + WaitOne(0) 自旋** → 主动跑消息泵 + 检查事件状态 → 既能等后台完成，又能处理 Invoke
4. **.GetAwaiter().GetResult() 在 UI 线程** → async-over-sync 死锁（即使 Task.Delay 不占线程）
5. **阶段分工要明确** → 阶段1/阶段2 间隙不要漏场景（升级完成框在交接时弹出）

## 一、陛下反馈时间线

| 时间 | 反馈 |
|------|------|
| 08:41 | "二哈起床了" |
| 08:45 | "A3Tools 判断是否存在已运行的还是有问题" |
| 08:46 | "我是说 A3 客户端有更新时的提示，不是启动时的提示" |
| 08:50 | "启动 A3 客户端 + 已有 client 在跑 → 提示 OK；但有 devtools 在跑没 client → 不提示" |
| 08:57 | 进一步明确"异类场景没提示"（client+devtools / devtools+client）|
| 08:58 | 后续测试发现异类场景**可以了**（实际是正常的） |
| 09:04 | **场景3（同时启 client+devtools 默认升级+序列化）** — 本次重点修复 |
| 09:08 | 催促加快 |

## 二、09:04 陛下明确场景3需求

> "同时启动A3客户端和开发工具，并且当前没有任何其他A3客户端和开发工具正在运行。
> 默认情况还是更新，但此时不能起启动开发工具，需要先启动客户端，等更新完成，再启动开发工具。
> 因为会出现文件占用情况。
> 如果两个已经都启动，需要先杀掉开发工具启动进程，等升级完再重新启动"

**翻译成技术规则**：
1. 两勾（client + devtools）→ **必须升级**（不管 launcher 自己有没有新版）
2. **不能同时启** → 先启 client，等 client 升级完成 → 再启 devtools
3. **如果 launcher 自己之前启的 devtools 还在跑** → 先杀掉（释放文件锁）

## 三、问题诊断

旧版 `needSerializeUpgrade` 触发条件被 11:07 修复坏：
```csharp
// 旧（11:07 修复后永远 false）
bool needSerializeUpgrade = _userUpdateChoice == true
                            && settings.LaunchDesktop
                            && settings.LaunchDevTools;
```

`_userUpdateChoice` 在 `PrepareUpdateScenarioForLaunch` 默认被设为 `null`（避免 Solo 场景卡死），导致 `needSerializeUpgrade` 永远是 false → 序列化逻辑**永远不执行**。

另外：
- `HandleA3ProgramUpdateFound` 场景2 分支 `return true` 不点"是" → A3 升级框卡桌面
- `StartA3ProgramUpdateWatcher` 阶段1 是单次 30s 超时 → devtools 弹升级框（30s 之后）没人监听

## 四、修复

### 1. 改 `needSerializeUpgrade` 触发条件（MainForm.cs 行 1784）

```csharp
// 旧
bool needSerializeUpgrade = _userUpdateChoice == true
                            && settings.LaunchDesktop
                            && settings.LaunchDevTools;

// 新（两勾就触发，不管 _userUpdateChoice）
bool needSerializeUpgrade = settings.LaunchDesktop && settings.LaunchDevTools;
```

### 2. 启 client 之前杀 launcher 自己启的 devtools（MainForm.cs）

```csharp
if (needSerializeUpgrade)
{
    var trackedDevList = GetOurTrackedDevtools();
    if (trackedDevList.Count > 0)
    {
        // 杀 launcher 自己启的 devtools（释放文件锁）
        foreach (var (p, code, _) in trackedDevList)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            finally { try { p.Dispose(); } catch { } }
            if (_accountStatuses.TryGetValue(code, out var st))
            {
                st.ProcessIds.Remove(p.Id);
                st.DevToolsProcessIds.Remove(p.Id);
            }
            _processIds.Remove(p.Id);
            _processLaunchModes.Remove(p.Id);
        }
        Thread.Sleep(1200); // 等被杀进程释放文件锁
    }
}
```

### 3. 改 `HandleA3ProgramUpdateFound` 场景2 也自动点是

```csharp
if (wantClient && wantDevtools)
{
    // 旧版只 return true（不点）→ A3 升级框卡桌面
    // 新版自动点是 → client 升级开始 → WaitForClientUpgradeComplete 等升级完成 → 启 devtools
    A3ProgramUpdateChecker.ClickYesButton(timeoutMs: 2000);
    return true;
}
```

### 4. `StartA3ProgramUpdateWatcher` 改持续监听模式

**旧问题**：阶段1 是单次 30s 超时 → devtools 在 client 升级完后才弹升级框 → **没人点**

**新设计**：阶段1 改成持续监听（直到 `StopA3ProgramUpdateWatcher` 取消）
- 使用 `CancellationTokenSource _a3WatcherCts` 控制
- launcher 启动流程结束时调 `StopA3ProgramUpdateWatcher()` → 阶段1 退出 → 阶段2 接管

```csharp
// 阶段1：持续监听升级文件检测框
while (!cts.IsCancellationRequested)
{
    if (A3ProgramUpdateChecker.FindUpdateDialog(out var hwnd))
    {
        // 触发场景1/2/3 处理
        bool continueLoop = (bool)this.Invoke(...);
        if (!continueLoop) return; // 场景3 选否 → 终止
        Thread.Sleep(500);
        continue;
    }
    Thread.Sleep(500);
}
// 阶段2：等升级完成
A3ProgramUpdateChecker.WaitAndConfirmUpgradeComplete(timeoutMs: 5 * 60 * 1000);
```

### 5. `PrepareUpdateScenarioForLaunch` JointSpawn 分支简化

去掉重复杀 launcher devtools 的逻辑（避免双重 sleep 1.2s），改为只做场景识别 + 返回。

### 6. 新增 `A3ProgramUpdateChecker.FindUpdateDialog`

```csharp
public static bool FindUpdateDialog(out IntPtr hwnd)
{
    return FindDialogByTitle(UPDATE_DIALOG_TITLES, out hwnd);
}
```

## 五、完整流程（两勾 + 无外部 A3 + 无 launcher 启的 devtools）

1. `LaunchSelectedAccount` 入口
2. `PrepareUpdateScenarioForLaunch` Solo 分支 → return true（不杀 launcher 启的 devtools，因为没有）
3. `StartA3ProgramUpdateWatcher()`（后台线程持续监听升级文件检测框）
4. `needSerializeUpgrade = true`，但 `GetOurTrackedDevtools()` 返回空 → 不杀
5. `clientPid = LaunchClientOnly(...)` → A3 客户端启动 + 检测升级
6. A3 客户端弹"升级文件检测"框
7. **阶段1 监听到** → Invoke 回主线程 → `HandleA3ProgramUpdateFound`
8. `GetExternalProcessDisplayNames()` → 返回空（无外部 A3）
9. 场景2（两勾 + 无外部）→ `ClickYesButton` 自动点是 → A3 开始升级 → return true
10. 主线程在 `WaitForClientUpgradeComplete(clientPid, 5min)` 同步等待
11. A3 客户端升级完成 → 重启 → 登录窗体稳定 5s → return true
12. `LaunchDevToolsForAccount(...)` → A3 开发工具启动 + 检测升级
13. A3 开发工具弹"升级文件检测"框
14. **阶段1 仍监听到**（持续模式） → Invoke → `HandleA3ProgramUpdateFound`
15. 场景2 → `ClickYesButton` 自动点是 → A3 devtools 升级
16. `WaitForClientUpgradeComplete` 循环里也 `TryAutoConfirmUpdateDialog` 抓 devtools 升级完成框
17. launcher 启动流程结束 → `StopA3ProgramUpdateWatcher()` → 阶段1 退出
18. 阶段2 接管 → `WaitAndConfirmUpgradeComplete` 5min 等"升级完成!"框
19. 自动点确定 → 完成

## 六、文件改动清单

### 修改文件
- `A3Tools/Forms/MainForm.cs`
  - 新增 `_a3WatcherCts` 字段（CancellationTokenSource）
  - 改 `needSerializeUpgrade` 触发条件
  - 加"杀 launcher 自己启的 devtools"逻辑
  - 改 `StartA3ProgramUpdateWatcher` 持续监听模式
  - 加 `StopA3ProgramUpdateWatcher` 方法
  - 改 `HandleA3ProgramUpdateFound` 场景2 自动点是
  - 简化 `PrepareUpdateScenarioForLaunch` JointSpawn 分支
  - `LaunchSelectedAccount` 末尾调 `StopA3ProgramUpdateWatcher()`

- `A3Tools/Services/A3ProgramUpdateChecker.cs`
  - 新增 `FindUpdateDialog(out IntPtr hwnd)` 公开方法

## 七、Build 结果

```
0 个错误
25 个警告（原项目既有的，6 个）
```

## 八、待陛下重测场景

| 场景 | 期望行为 | 状态 |
|------|---------|------|
| 同时启 client+devtools，无外部 A3 | 杀 launcher devtools（如有）→ client 先升级完成 → devtools 再启 | ⏳ 待重测 |
| 同时启 client+devtools + 外部 A3 | 弹场景3 选是 → 关外部 → 走序列化 | ⏳ 待重测 |
| 同时启 client+devtools + launcher 启的 devtools 在跑 | 先杀 launcher devtools → 启 client → 序列化 | ⏳ 待重测 |
| 单启 client/devtools | 不触发序列化 | ⏳ 待重测 |

## 九、注意事项

1. **`_userUpdateChoice` 字段保留默认 null**（11:07 修复不动）—— 只在 launcher 自己升级（`UpdateService`）路径生效
2. **`_a3ProgramUpdateChoice` 字段**仍是 A3 程序更新专用（09:43 区分）
3. **新 `_a3WatcherCts` 字段**生命周期：每次 `StartA3ProgramUpdateWatcher` 会 cancel 上一个并创建新的，避免多个 watcher 冲突
4. **`WaitForClientUpgradeComplete` 仍是同步等待 5min**——陛下明确两勾场景默认升级，等是合理的