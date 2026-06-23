# 「A3工具箱 已在运行」提示从 MessageBox 改为 Toast 自动消失

**日期：** 2026-06-23  
**类型：** UI 优化

## 改前

```csharp
MessageBox.Show(
    "A3工具箱 已在运行，已切到前台。\n\n不需要重复启动。",
    "提示",
    MessageBoxButtons.OK,
    MessageBoxIcon.Information);
```

缺点：
- **强制抢焦点**：用户正在干别的活，弹窗抢过去还得手动关闭
- **必须点确定**：哪怕只是瞥一眼也得手动点
- **模态阻塞**：点完才能继续

## 改后

3 秒后自动消失的右下角 Toast 窗体：

- 不抢主窗焦点（WS_EX_NOACTIVATE）
- 不在任务栏/Alt+Tab 出现（WS_EX_TOOLWINDOW）
- 暗色背景 #2D2D2D + 白字 + 8px 圆角
- 右下角定位（距离任务栏 20px）
- 3 秒自动关闭（鼠标移入可暂停，移出再给 1.5 秒）
- 关闭前 200ms 淡出动画（13 帧 15ms 渐变）
- 进程退出时自动随主线程一起死

## 改动详情

### Program.cs

1. **删除 `MessageBox` 调用**，改为 `ShowAlreadyRunningHint()` 内部用 `Application.Run(toast)`

2. **新增 `AlreadyRunningToastForm` 内部类**（同文件）：
   - 关键样式：`CreateParams.ExStyle |= Program.WS_EX_NOACTIVATE | Program.WS_EX_TOOLWINDOW`
   - `protected override bool ShowWithoutActivation => true` —— 备用保险
   - `OnPaint` 画 1px 浅色边框（暗色背景在白桌面上的边界感）
   - `Load` 里设置 `Region` 实现 8px 圆角 + 右下角定位 + 启动关闭 Timer
   - `CloseTimer_Tick` 里启动 200ms 淡出动画再 Close

3. **常量改动**：
   - `private const int WS_EX_NOACTIVATE` → `internal const int WS_EX_NOACTIVATE`
   - `private const int WS_EX_TOOLWINDOW` → `internal const int WS_EX_TOOLWINDOW`
   - Form 类引用时必须带 `Program.` 前缀（常量字段编译器不自动 import）

### 关键代码片段

```csharp
// Program.cs - ShowAlreadyRunningHint
private static void ShowAlreadyRunningHint()
{
    try
    {
        var toast = new AlreadyRunningToastForm
        {
            DurationMs = AlreadyRunningToastDurationMs,  // 3000
            Message = "A3工具箱 已在运行，已切到前台",
        };

        // Application.Run(form) 启动以 toast 为唯一窗体的消息循环
        // toast.Close() 会自动退出该消息循环，进程随之结束
        Application.Run(toast);
        toast.Dispose();
    }
    catch { /* Toast 失败不重要 */ }
}
```

```csharp
// Program.cs - AlreadyRunningToastForm.CreateParams
protected override CreateParams CreateParams
{
    get
    {
        var cp = base.CreateParams;
        cp.ExStyle |= Program.WS_EX_NOACTIVATE | Program.WS_EX_TOOLWINDOW;
        return cp;
    }
}
```

## 测试场景

| 场景 | 预期 |
|---|---|
| 主窗在最前面，用户操作别的 → 双击 A3Tools.exe | 主窗抢到前台 + 右下角 toast 3 秒消失，**不抢主窗焦点** |
| 主窗最小化到任务栏 → 双击 A3Tools.exe | 主窗恢复 + toast 出现 |
| 主窗隐藏到托盘 → 双击 A3Tools.exe | 主窗从托盘恢复 + toast 出现 |
| 鼠标移入 toast | 计时器暂停（用户想看清） |
| 鼠标移出 toast | 再给 1.5 秒 |

## 编译结果

- 0 错误
- 2 警告（NPinyin 兼容性，已有）
- A3Tools.exe: 77,680,833 bytes（74.07 MB）
- 总大小 76.05 MB
- 无 .log / ~$ 残留
- Plugins 含 A3Tools.Plugins.Default.dll + A3Tools.Common.dll + tools.json
- 3 个 pdb 全保留

## 验证要点

- 重点验证 toast 不会抢主窗焦点
- 重点验证鼠标移入/移出的计时器交互
- 重点验证主窗+toast 同时存在时 z-order（toast 在主窗之上但不抢焦点）
