# 2026-06-22 A3Tools 自身单实例：避免重复启动

## 需求

A3Tools 主程序自身也加单实例检测 —— 如果已经有一个 A3Tools 在跑，再次启动时：
- 不再创建新主窗
- 把已运行的那个实例的窗口切到前台
- 新进程直接退出（并提示一下）

避免不小心起了两个工具箱。

## 实现

### `A3Tools/Program.cs` 加单实例守卫

**核心思路：**
1. 进程启动时尝试创建命名 `Mutex`（`A3Tools_SingleInstance_Mutex_v1`）
2. `createdNew == false` → 已有实例在跑
3. 用 `FindWindow`（按窗口标题 `"A3工具箱"`）找已有主窗，找不到时改用 `EnumWindows` + 进程名匹配
4. 突破 Vista+ 抢焦点限制（`AllowSetForegroundWindow` + `SetForegroundWindow`）
5. 最小化则 `ShowWindow(SW_RESTORE)` 恢复
6. `MessageBox.Show` 提示「已在运行，已切到前台」
7. 退出新进程

**关键代码：**
```csharp
_singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out bool createdNew);

if (!createdNew)
{
    BringExistingInstanceToFront();
    ShowAlreadyRunningHint();
    _singleInstanceMutex.Dispose();
    return;
}

try
{
    ApplicationConfiguration.Initialize();
    Application.Run(new MainForm());
}
finally
{
    try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
    _singleInstanceMutex?.Dispose();
}
```

**窗口查找兜底（应对托盘隐藏场景）：**
```csharp
private static IntPtr FindByProcessName(string processName)
{
    IntPtr result = IntPtr.Zero;
    EnumWindows((hWnd, _) =>
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        try
        {
            var proc = Process.GetProcessById((int)pid);
            if (proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                if (IsWindowVisible(hWnd) || result == IntPtr.Zero)
                    result = hWnd;
            }
        }
        catch { /* 进程可能已退出 */ }
        return true;
    }, IntPtr.Zero);
    return result;
}
```

## 设计要点

- **Mutex 名字带版本后缀 `_v1`**：避免以后重装/升级时遗留的 abandoned mutex 误判
- **`initiallyOwned: true`** + 检查 `createdNew`：标准模式，几乎所有 .NET 单实例教程都用这套
- **不用 Named Pipe 转发参数**：A3Tools 不接收命令行参数，不需要跨进程通信
- **FindWindow 优先（按窗口标题 `"A3工具箱"`），EnumWindows 兜底（按进程名 `"A3Tools"`）**：
  - 正常情况：主窗可见时按标题找最快
  - 边界情况：主窗隐藏到托盘时标题可能不唯一（有的 app 会改），按进程名兜底
  - 注意区分：用户启动的是 `A3Tools.exe` 进程，进程名是 `A3Tools`（不带 .exe）
- **BringExistingInstanceToFront 全 try/catch 包裹**：单实例守卫绝对不能崩，影响退出码
- **正常退出释放 Mutex，异常退出靠 GC**：`Application.Run` 抛异常时 finally 里 `try ReleaseMutex catch { }`，GC 最终也会回收 mutex 句柄
- **不在新进程弹主窗**：只弹 `MessageBox` 一行，简洁清晰

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，149 警告全是历史 nullable
- 重新打包 `StandaloneSF`：
  - `A3Tools.exe` 77,678,677 bytes（74.07 MB，比上次 +791 字节）
  - 总大小约 76.05 MB
  - Plugins / 3 个 pdb / 无残留

## 待测试（陛下测试验收）

1. **正常启动**：双击 A3Tools.exe → 工具箱打开（第一个实例）
2. **重复启动**：再双击一次 → 弹「A3工具箱 已在运行，已切到前台」+ 已有窗口切到前台 + 新进程退出
3. **从托盘隐藏时启动**：主窗隐藏到托盘 → 双击 exe → 托盘窗口恢复 + 切前台
4. **命令行启动**：cmd 多次 `start A3Tools.exe` → 只有第一个开主窗，其他都弹提示退出
5. **任务管理器看到只有一个进程**：多次启动后 `tasklist | findstr A3Tools` 应该只有一行
6. **正常退出后能再次启动**：第一个实例关掉后，再双击 exe 应该能正常开起来（mutex 释放成功）
