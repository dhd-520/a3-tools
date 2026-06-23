# 2026-06-22 启动时拉主窗前台 + 启动成功后自动隐藏到托盘

## 需求

1. **主窗不在托盘但被压在后面**（如非激活状态）→ 启动账套时也要把主窗拉到最前端
2. **启动成功后**（至少启动了一个程序）→ 主窗自动最小化到托盘

## 实现

### 1. `A3Tools/Forms/MainForm.cs LaunchSelectedAccount` 开头：双保险拉前台

```csharp
// 保证主窗在最前端：
// - 托盘隐藏状态：ShowFromTray() 恢复 + 拉前台
// - 正常但非激活状态：ForceForegroundWindow 拉前台
if (_isHiddenToTray)
    ShowFromTray();
else if (Form.ActiveForm != this)
    ForceForegroundWindow(this.Handle);
```

旧逻辑只处理了 `_isHiddenToTray == true` 场景，没考虑「主窗在屏幕但被压在后面」。

### 2. `A3Tools/Forms/MainForm.cs LaunchSelectedAccount` 末尾：自动隐藏

```csharp
// 启动成功（已记录在案）后自动隐藏到托盘
if (_processIds.Count > 0 && !_isHiddenToTray)
{
    this.BeginInvoke(new Action(() => HideToTray()));
}
```

**判定条件**：
- `_processIds.Count > 0` —— 至少启动了一个 A3 客户端 / 开发工具进程
- `!_isHiddenToTray` —— 当前不在托盘（已经在托盘就别再隐藏一次）
- `this.BeginInvoke` —— 延迟到 UI 线程空闲时执行，避免和 LaunchSelectedAccount 的同步逻辑冲突

## 设计要点

### Issue 1 设计
- **`Form.ActiveForm != this`**：判断主窗是否前台，简单可靠
- 复用 `ForceForegroundWindow`（已经包含 AllowSetForegroundWindow 突破 Vista+ 限制）
- 只在「确实不在前台」时调用，避免无意义操作

### Issue 2 设计
- **`_processIds.Count` 作为触发条件**：纯网页版启动不进 _processIds，不触发隐藏
  - 想全部场景都隐藏的话，把判断改成「任意一个 LaunchDesktop/LaunchDevTools/LaunchWeb 实际跑了」
  - 但用户实际场景以客户端为主，先这样最稳
- **`BeginInvoke` 延迟隐藏**：避免和 LaunchSelectedAccount 同步逻辑冲突
  - 如果直接调 HideToTray()，可能在 LaunchWebBrowser / 自动登录流程完成前就触发了奇怪时序
- **已经在托盘不重复隐藏**：`!_isHiddenToTray` 守卫，防止 BeginInvoke 重复触发
- **不挂「启动网页」时不会自动隐藏**：因为没进 _processIds
  - 陛下场景里基本都会勾上客户端，所以影响不大
  - 如果用户想纯网页也隐藏，告诉我加个 AppSettings 开关

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，149 警告全是历史 nullable
- 重新打包 `StandaloneSF`：
  - `A3Tools.exe` 77,678,756 bytes（74.07 MB，比上次 +79 字节）
  - 总大小约 76.05 MB
  - Plugins / 3 个 pdb / 无残留

## 待测试（陛下测试验收）

### Issue 1（非激活状态拉前台）
1. A3Tools 主窗在屏幕但**最小化到任务栏**（不是托盘） → 启动账套 → 主窗恢复 + 切前台
2. A3Tools 主窗在屏幕**被浏览器压在后面** → 启动账套 → 主窗自动切到前台
3. A3Tools 主窗在屏幕**正在使用**（本身已激活）→ 启动账套 → 不会闪烁，正常工作

### Issue 2（启动成功后自动隐藏）
1. 启动账套（勾客户端）→ 客户端起来后 A3Tools **2秒内自动隐藏到托盘**
2. 启动账套（只勾开发工具）→ 开发工具起来后 A3Tools **自动隐藏**
3. 启动账套（只勾网页版）→ A3Tools **不隐藏**（_processIds 为空）
4. 启动账套（**所有勾都不勾**）→ A3Tools **不隐藏**
5. 主窗已经在托盘 → 启动账套 → **不重复隐藏**（虽然也没必要）
