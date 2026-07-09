# 2026-07-09 修复自动更新两个小 bug

## 症状

陛下 2026-07-09 反馈 v2.3.1 双源自动更新有两个小 bug：

1. **升级成功后 cmd 窗体没有自动关闭** —— 升级流程跑完后，能看到一个黑色的 cmd 窗口一直挂着不消失
2. **升级成功后 `update.log` 文件没清理** —— 每次升级都会在 A3Tools 目录下留下 `_update.log`，多次升级后累积

## 根因

### Bug 1：cmd 窗体不关

`UpdateService.cs` 里 `PerformUpdate` 和 `PerformZipUpdate` 启动 bat 的代码：

```csharp
var psi = new ProcessStartInfo
{
    FileName = "cmd.exe",
    Arguments = $"/c start \"\" /b \"{batPath}\"",
    UseShellExecute = true,        // ← 关键问题
    CreateNoWindow = true,         // ← 被忽略
    WorkingDirectory = currentDir
};
```

**.NET 官方文档明确写明**：

> When `UseShellExecute` is `true`, the `CreateNoWindow` property has no effect.

所以 `CreateNoWindow = true` 实际上没生效，cmd.exe 启动时按默认行为弹了控制台窗口。

加上 `start /b "bat.bat"` 嵌套启动更糟糕：
- 父 cmd 看到 `start` 命令完成立即退出（`/c` 模式）
- 父 cmd 退出时 **它的 console handle 被销毁**
- 子 cmd 启动时没有 console handle 继承
- 子 cmd 调用 `AllocConsole` **创建一个新的 console 窗口**
- 这就是陛下看到一直挂着的 cmd 窗口

### Bug 2：日志不清理

两个 bat 脚本最后只删了自己：

```bat
start "" ""{currentExe}"
del ""%~f0""
```

没有清理 `_update.log` 的逻辑，所以每次升级都留下一份日志。

## 修复

### Bug 1：cmd 窗体

```csharp
var psi = new ProcessStartInfo
{
    FileName = "cmd.exe",
    Arguments = $"/c \"\"{batPath}\"\"",   // 直接执行 bat，不要嵌套 start /b
    UseShellExecute = false,                // 让 CreateNoWindow 生效
    CreateNoWindow = true,
    RedirectStandardOutput = true,          // 阻止 cmd 调用 AllocConsole 弹窗
    RedirectStandardError = true,
    WorkingDirectory = currentDir
};
```

要点：
1. **`UseShellExecute: true → false`**：让 `CreateNoWindow = true` 真正生效
2. **`Arguments: /c start "" /b "bat.bat" → /c ""bat.bat""`**：避免嵌套 cmd，cmd 直接在自己进程内解释 bat，bat 跑完 cmd 立即退
3. **`RedirectStandardOutput/Error = true`**：cmd 的 stdout/stderr 重定向到 .NET stream，cmd 不会因为需要输出而调用 `AllocConsole`
4. **`Environment.Exit(0)` 杀 A3Tools 不影响 cmd 子进程**：独立进程，A3Tools 退出后 cmd 继续执行直到 bat 跑完

### Bug 2：日志清理

在两个 bat 脚本的 `del ""%~f0""` 之前加一行：

```bat
:: === 清理日志：升级成功后清掉 _update.log（前面任何 exit /b 1 都跳过这行） ===
del ""{logPath}"" >nul 2>&1
del ""%~f0""
```

设计意图：
- **升级成功**（所有步骤正常执行）：走到最后 `del {logPath}` 清日志
- **升级失败**（任何 `exit /b 1` 提前返回）：跳过 `del {logPath}`，**保留日志供排查**

## 改动文件

- `A3Tools/Services/UpdateService.cs`
  - `PerformUpdate` 方法：改 ProcessStartInfo 配置 + bat 内容末尾加 `del {logPath}`
  - `PerformZipUpdate` 方法：同上
  - 更新两处注释，说明 UseShellExecute / RedirectStandardOutput 的设计意图

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：**0 错** 2 警告（历史的，不是本次引入）。

## 后续验证建议

陛下升级 v2.3.2 后实际跑一次升级流程：
1. 观察整个升级过程是否还有 cmd 窗口闪过
2. 升级完成后检查 `A3Tools\_update.log` 是否自动消失
3. 如果**故意制造升级失败**（如目标 exe 被占用），确认 `_update.log` 仍保留供排查

## 教训

- **`.NET` 的 `UseShellExecute=true` 模式下 `CreateNoWindow` 静默失效**是个老坑，文档里就一句话很容易踩。建议团队约定：**所有 `ProcessStartInfo` 配置都用 `UseShellExecute = false`**，避免这个陷阱
- **bat 脚本中的 `del` 顺序很重要**：先清日志再删自己（`del %~f0`），这样即使 `del` 自己失败也不会留下半完成状态
- **日志保留 vs 清理的平衡**：升级成功不留垃圾，升级失败保留证据。靠 `exit /b 1` 提前 return + bat 末尾清理实现这个语义