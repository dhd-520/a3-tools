# 2026-06-22 启动去重：检测已运行则切前台不重复启动

## 需求

启动账套时，先判断 A3 客户端 / 集成开发工具是否已在运行：
- **已在运行** → 直接把那个实例切到前台，**不启动新进程**
- **未在运行** → 正常启动

避免「最小化到托盘 → 不确定有没有启动 → 误点多次 → 起多个 A3 实例」的问题。

## 实现

### 1. `A3Tools/Services/Win32AutoLoginHelper.cs` 新增两个公开静态方法

**`IsProcessRunning(string processName) → bool`**
- 用 `Process.GetProcessesByName(processName)` 判断（不带 .exe 后缀）

**`BringProcessToFront(string processName, out int processId) → bool`**
- 取首个运行实例的 `Process` 对象
- 优先 `MainWindowHandle`；拿不到时用 `EnumWindows` + `GetWindowThreadProcessId` 兜底
- `IsIconic` 判断是否最小化 → `ShowWindow(SW_RESTORE)` 恢复
- `GetForegroundWindow` + `GetWindowThreadProcessId` + `AllowSetForegroundWindow` 突破 Vista+ 抢焦点限制
- `SetForegroundWindow` 拉到前台
- 整个过程 `try/catch` 包裹（防止多线程/句柄失效把启动流程炸掉）

**新增 Win32 imports：**
```csharp
[DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
[DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int dwProcessId);
[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
```
（`SW_RESTORE` 已存在；只补了 `SW_SHOW` 常量）

### 2. `A3Tools/Forms/MainForm.cs LaunchSelectedAccount` 接入去重

启动 A3 客户端 / 集成开发工具之前，先 `IsProcessRunning` 探测：

```csharp
if (Win32AutoLoginHelper.IsProcessRunning("君则A3"))
{
    if (Win32AutoLoginHelper.BringProcessToFront("君则A3", out int existingPid))
    {
        ShowToast("A3 客户端已在运行，已切到前台");
        if (!_processIds.Contains(existingPid))
            _processIds.Add(existingPid);
        RecordProcess(account.Code, existingPid, "client");
    }
}
else
{
    // 原来的 LaunchAndAutoLogin / Process.Start 流程
    ...
}
```

集成开发工具同理，进程名 `"君则A3集成开发工具"`。

### 3. 重要细节

- **`_processIds` 仍记录已有进程 ID**：从「账套运行情况」关闭时能找到这个进程并 Kill
- **`RecordProcess` 也调**：账套名→进程 ID 的映射同步更新，状态栏能正确显示「是谁启动的」
- **`ShowToast` 提示**：「A3 客户端已在运行，已切到前台」/「集成开发工具已在运行，已切到前台」2 秒自动消失
- **不阻止启动参数变化**：已运行的实例不会被新账套的「账套名/密码」重登录（避免登录冲突），交给用户手动切换

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，18 警告全是历史 nullable
- 重新打包 `StandaloneSF`：
  - `A3Tools.exe` 77,677,886 bytes（74.07 MB，比上次 +524 字节）
  - 总大小约 76.05 MB
  - Plugins / 3 个 pdb / 无残留

## 待测试（陛下测试验收）

1. **场景 A**：A3 客户端已开 → 工具箱选账套点启动 → 客户端自动切到前台 + 底部 Toast「已在运行」
2. **场景 B**：A3 客户端已最小化（任务栏） → 启动账套 → 客户端从最小化恢复并切到前台
3. **场景 C**：A3 集成开发工具已开 → 勾选「启动开发工具」启动 → 切到开发工具前台
4. **场景 D**：A3 客户端已开 + 工具箱主窗在托盘 → 启动账套 → 工具箱主窗也恢复前台 + 客户端也切到前台
5. **场景 E**：A3 客户端未开 → 启动账套 → 正常启动 + 自动登录流程跑通
6. **边界**：已运行的客户端和开发工具在「账套运行情况」列表里仍能点 ✕ 关闭（PID 跟踪有效）
