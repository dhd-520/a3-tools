# 2026-08-01 JointSpawn 升级序列化(等客户端升级完再启开发工具)

## 背景

陛下 09:44 反馈(在 09:36「默认升级」语义设计之上):

> 同时启动客户端 + 开发工具 → launcher 检测到更新弹框 → 选「是」升级 → **现在两个都启动升级**(因为 launcher 把 client 和 devtools 同时启了,都会走 `TryAutoConfirmUpdateDialog`)→ 这是不对的。**只需要客户端升级完成,再启动开发工具**。

陛下 09:47 补充:

> 升级完成后 A3 会弹一个**「升级完成」确认框**,点完确认按钮才会继续弹出客户端登录界面。这时候才表示升级完成了。**超时 5 分钟**。

陛下 09:48 确认设计三连 OK:
1. 模糊匹配关键字 OK
2. 方案 B(等登录页出现 + 5s 稳定)OK
3. 先写 worklist 再写代码 OK

## 触发条件(本次设计的「升级序列化」场景)

```
_userUpdateChoice == true   (launcher 要升级)
&& settings.LaunchDesktop   (勾了客户端)
&& settings.LaunchDevTools  (勾了开发工具)
```

> 只有**两个都勾 + 要升级**才需要序列化。其他情况(只勾一个 / 不升级)走现状。

## 改造方案

### 现状(有问题)
```
LaunchSelectedAccount:
  启 client (Process.Start 君则A3.exe)
    → TryAutoConfirmUpdateDialog(client.Pid)  ← 同步阻塞 12s
  启 devtools (Process.Start 君则A3集成开发工具.exe)
    → TryAutoConfirmUpdateDialog(devtools.Pid) ← 同步阻塞 12s
两个并行启动,client 升级时 devtools 也开始升级,互相干扰。
```

### 目标
```
LaunchSelectedAccount:
  if (要升级 && 两勾) {
    LaunchClientOnly()                              ← 抽出来,只启 client
    WaitForClientUpgradeComplete(timeout=5*60*1000)  ← 等 client 升级完成
    LaunchDevToolsForAccount()                      ← 再启 devtools
  } else {
    // 现状:两个一起启
    LaunchClient()
    LaunchDevToolsForAccount()
  }
```

### WaitForClientUpgradeComplete 实现

```
1. while (累计时间 < 5 分钟) {
   2. 检测「升级完成」弹窗 → TryFindAndClickUpdateDialog(true) → 按确认
      (复用现有 EnumWindows+EnumChildWindows+BM_CLICK 机制)
   3. 检测 client 主窗体 (Title 包含 "君则A3" 登录页关键字):
      - EnumWindows 找 client PID 的窗体
      - 主窗体可见 + 标题是登录页(不含"升级/更新/检测")
      - 稳定 5 秒(每 1s 轮询,5 次都符合条件)
      → return true(升级完成,可以启 devtools)
   4. Sleep 1s 继续轮询
}
5. 超时:打 warn 日志「等 client 升级完成超时 5min,降级直接启 devtools」
   return false
```

**关键:不阻塞 launcher 主线程**

`LaunchSelectedAccount` 当前是从 UI 线程同步执行(被按钮 Click 调用),5min 阻塞会让 launcher 卡死、看起来像死机。
**改成 fire-and-forget 异步**:
- 启 client → 立刻返回(走 Task.Run 起后台轮询)
- WaitForClientUpgradeComplete 在后台跑
- 完成后**用 BeginInvoke 回到 UI 线程**启 devtools

或者更简单:**保留同步阻塞**(因为 launcher 启完账套会 `HideToTray`,后台卡 5min 也无所谓,反正 launcher 隐藏了;只是 UI 线程会阻塞,陛下不会看到 launcher)。

我倾向**同步阻塞**(陛下启账套后 launcher 立刻隐藏,等就等,反正陛下看不见 launcher;async 反而引入跨线程问题复杂化)。

> 跟陛下确认?但陛下已经 OK 方案 B,这里我倾向先做同步,真有问题再改 async。

### 关键字扩充

```csharp
// A3_UPDATE_DIALOG_TITLES 新增:
"升级完成", "更新完成", "升级成功", "更新成功", "升级完成确认",

// A3_YES_BUTTON_TEXTS 新增:
"确认", "知道了", "好的", "继续", "完成", "Confirm", "OK"
```

## 改动文件

| 文件 | 改动 |
|------|------|
| `A3Tools/Forms/MainForm.cs` | 1. `A3_UPDATE_DIALOG_TITLES` 数组新增 5 个升级完成关键字(line ~432)<br>2. `A3_YES_BUTTON_TEXTS` 数组新增 7 个确认按钮关键字(line ~418)<br>3. 抽 `LaunchClientOnly(account, appDir, settings)` 复用启 client 的逻辑(含 auto-login)<br>4. 新增 `WaitForClientUpgradeComplete(int clientPid, int timeoutMs)` 方法<br>5. `LaunchSelectedAccount` 主流程加分支:`if (要升级 && 两勾)` → 序列化;else → 现状 |

## 关键代码骨架

```csharp
private void LaunchSelectedAccount()
{
    // ... 现有准备逻辑 ...
    
    if (!PrepareUpdateScenarioForLaunch(account, settings)) return;
    
    string appDir = settings.AppDirectory;
    
    // ★ 2026-08-01 升级序列化触发条件
    bool needSerialize = _userUpdateChoice == true 
                         && settings.LaunchDesktop 
                         && settings.LaunchDevTools;
    
    if (settings.LaunchDesktop)
    {
        LaunchClientOnly(account, appDir, settings);
        // TryAutoConfirmUpdateDialog 在 LaunchClientOnly 内部已调
    }
    
    if (settings.LaunchDevTools)
    {
        if (needSerialize)
        {
            // ★ 等 client 升级完成再启 devtools
            int clientPid = GetClientPid(account.Code);  // 从 _processIds / Status 找
            bool ok = WaitForClientUpgradeComplete(clientPid, 5 * 60 * 1000);
            if (!ok)
                System.Diagnostics.Debug.WriteLine(
                    "[UpgradeSerialize] 等 client 升级完成超时 5min,降级启 devtools");
        }
        LaunchDevToolsForAccount(account, appDir, devSettings);
    }
    
    // ... 现状 web 启动 + HideToTray ...
}

private int? LaunchClientOnly(Account account, string appDir, AppSettings settings)
{
    string exe1 = Path.Combine(appDir, "君则A3.exe");
    if (!File.Exists(exe1)) return null;
    
    if (TryBringAccountProcessesToFront(account.Code, "client"))
    {
        ShowToast($"账套【{account.Name}】客户端已在运行，已切到前台");
        // 找已运行的 client PID
        return GetClientPid(account.Code);
    }
    
    Process? p;
    var appSettingsClient = _dataService.LoadSettings();
    if (appSettingsClient.ClientAutoLogin
        && !string.IsNullOrEmpty(account.ServerPassword)
        && !string.IsNullOrEmpty(account.ServerUsername))
    {
        p = Win32AutoLoginHelper.LaunchAndAutoLogin(...);
    }
    else
    {
        p = Process.Start(new ProcessStartInfo { ... });
    }
    
    if (p != null)
    {
        _processIds.Add(p.Id);
        RecordProcess(account.Code, p.Id, "client");
        TryAutoConfirmUpdateDialog(p.Id);  // 第一次升级框(「升级文件检测」)
        return p.Id;
    }
    return null;
}

private bool WaitForClientUpgradeComplete(int clientPid, int timeoutMs)
{
    var startTime = DateTime.Now;
    int stableCount = 0;
    
    while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
    {
        // 1. 处理「升级完成」弹窗
        TryAutoConfirmUpdateDialog(clientPid);
        
        // 2. 检测 client 主窗体(登录页) + 5s 稳定
        if (IsClientLoginWindowVisible(clientPid))
        {
            stableCount++;
            if (stableCount >= 5)  // 5 次连续检测都稳定
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[UpgradeSerialize] client 升级完成,登录窗体稳定 5s (PID={clientPid})");
                return true;
            }
        }
        else
        {
            stableCount = 0;
        }
        
        Thread.Sleep(1000);
    }
    
    System.Diagnostics.Debug.WriteLine(
        $"[UpgradeSerialize] 等 client 升级完成超时 {timeoutMs/1000}s (PID={clientPid})");
    return false;
}

private bool IsClientLoginWindowVisible(int clientPid)
{
    // EnumWindows 找 client PID 的所有可见窗体
    // 至少 1 个窗体:标题包含 "君则A3" 且不含 "升级/更新/检测/完成/确认"
    //   → 这是登录页
}
```

## 编译

✔ **0 CS 错误 2 警告**(原历史警告,不变)
- 输出到 `D:\work\A3Tools\scratch\compile_v246\Debug\net7.0-windows\`(临时目录,因 VS PID 19624 + A3Tools PID 22128 锁了原 bin/Debug)
- **A3Tools.dll = 341,504 bytes**(v2.4.5 = 338,432 → +3,072 = +3 KB,符合新增方法预期)
- A3Tools.Plugins.Default.dll = 607,232 bytes(不变)
- A3Tools.Common.dll = 87,040 bytes(不变)

⚠️ **陛下需要**:关 VS (19624) + 关 A3Tools (22128) → 我再跑一次 `dotnet build` 写到正式 bin/Debug

## 测试步骤

1. 关掉 VS + 关掉跑的 A3Tools → 释放文件锁
2. `dotnet build D:\work\A3Tools\A3Tools.sln -c Debug` → 写到 bin/Debug
3. 起 DebugView → 滤 `*UpgradeSerialize*` 和 `*AutoConfirm*`
4. 0081 账套 → 勾上客户端 + 开发工具
5. **前置**:故意先打开一个旧 A3 客户端(模拟 launcher 检测到更新 + 外部 A3 进程在跑)→ launcher 弹 External 框
6. launcher External 框选「是」→ 期望日志:
   ```
   [UpdateScenario] 默认 _userUpdateChoice=true (scenario=External)
   [AutoConfirm] 找到更新弹窗 '升级文件检测' (...) → 按是
   [UpgradeSerialize] client 升级完成,登录窗体稳定 5s (PID=...)
   [UpgradeSerialize] 启 devtools ...
   ```
7. 验证 devtools 启动后**没有走升级流程**(因为 client 已经升级完了)

## 边界 case(陛下已 OK 模糊匹配 + 5min 超时降级)

- **升级完成弹窗标题变体**:模糊匹配覆盖(陛下说不确定,加 5 个常见字)
- **5min 内没升级完**:降级照常启 devtools,不影响 launcher 主流程
- **client 升级时 PID 变了**(重启自己):需要让 `WaitForClientUpgradeComplete` 处理 PID 漂移(轮询时找同 Code 的 client PID,不用入参的旧 PID)
- **devtools 也勾但 launcher 没要升级**(`_userUpdateChoice=false`):走现状两个一起启(陛下 09:36 语义)

## 经验沉淀

**「launcher 自动启两个相关进程」的总原则**:
- launcher 主动启动时,要考虑两个进程是否可能互相干扰(共享文件锁 / 共享更新流程 / 共享硬件)
- A3 客户端 + 开发工具 共享 A3 升级流程 → 必须**先升级完一个,再启另一个**
- 解决方案:同步阻塞等「升级完成确认」信号 → 简单可靠,避免 async 复杂度

**「等进程稳定」的判定标准**:
- 弹窗消失 ≠ 进程稳定(可能马上又弹别的框)
- 主窗体出现 + N 秒无新弹窗 = 稳定(行业通用做法)
- 5 秒是经验值,够滤掉短抖动,又不至于太长

**「超时降级 vs 阻塞」**:
- 同步阻塞 5min 在 launcher 隐藏态下 OK(陛下看不到)
- 但加超时降级 = 万一真出问题不卡 launcher

---

**未提交、未发版。** 等陛下实测后决定是否并入 v2.4.6。