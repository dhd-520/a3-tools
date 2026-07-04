# 2026-07-04 Explorer 位置严格限制 WorkArea 内（修复真全屏仍出屏）

## 陛下反馈

> "对象资源管理器还是不对，全屏后再打开，只有一半在屏幕内，另一半超出右侧屏幕"

## 根因

之前我加的"最大化叠加内部右侧"逻辑：

```csharp
if (this.WindowState == FormWindowState.Maximized)
{
    x = this.Right - explorerWidth;  // ❌ 错
    y = this.Top;
}
```

**两个问题**：
1. **检测错全屏**：陛下用的"全屏"是 `FormBorderStyle = None + Bounds = Screen.Bounds`（真全屏），不是 WinForms `WindowState.Maximized`
2. **叠加在父窗体内**：Owned Form 在父窗体外**能画**（WinForms 允许），但视觉上 Explorer 跨在主窗体右边界上 → 看起来一半在屏内、一半在屏外

## 修复

**移除 WindowState 判断**，统一用 WorkArea 边界检测：

```csharp
private Point ComputeExplorerLocation()
{
    const int explorerWidth = 360;
    const int gap = 4;
    var wa = GetScreenWorkArea();

    int x = this.Right + gap;     // 默认：主窗体右侧 4 px
    int y = this.Top;

    // 1. 右侧溢出
    if (x + explorerWidth > wa.Right)
    {
        if (this.Left - explorerWidth - gap >= wa.Left)
            x = this.Left - explorerWidth - gap;     // 贴主窗体左侧
        else
            x = Math.Max(wa.Left, wa.Right - explorerWidth);   // 贴 WorkArea 右
    }

    // 2. 左侧溢出（如多屏负坐标）
    if (x < wa.Left) x = wa.Left;

    // 3. 再次右侧溢出（WorkArea 极窄）
    if (x + explorerWidth > wa.Right)
        x = Math.Max(wa.Left, wa.Right - explorerWidth);

    // y 轴：贴顶
    if (y < wa.Top) y = wa.Top;
    if (y + 200 > wa.Bottom) y = Math.Max(wa.Top, wa.Bottom - 600);

    return new Point(x, y);
}
```

**关键**：
- **不依赖 WindowState** —— 真全屏、最大化、还原都正确
- **三步边界检查**：右侧 → 左侧 → 再次右侧，**保证 Explorer 完全在 `[wa.Left, wa.Right]` 内**
- **多屏支持**：`Screen.FromControl(this)` 用主窗体所在屏

## 三种状态下的位置

| 主窗体状态 | this.Right | Explorer X | 视觉 |
|----------|-----------|-----------|------|
| 还原（屏幕中）| 1100 | 1104 | 主窗体右侧 4 px |
| 最大化 | wa.Right | wa.Right - 360 | 贴 WorkArea 右内侧 |
| 真全屏 | wa.Right | wa.Right - 360 | 贴 WorkArea 右内侧 |
| 多屏左屏 | wa.Left + 1920 | wa.Right - 360 | 贴 WorkArea 右内侧 |

## 改动文件

- `SqlQueryForm.cs`：`ComputeExplorerLocation` 重写为纯 WorkArea 边界检测，删除 WindowState 分支

## 验证

- `dotnet build`：0 错误
- 真全屏 → 打开 Explorer → Explorer 紧贴 WorkArea 右内侧（完全可见）
- 还原 → 关闭 Explorer → 再开 → Explorer 出现在主窗体右侧
- 最大化 → 打开 Explorer → Explorer 贴 WorkArea 右内侧
- 拖动主窗体到屏幕左 → Explorer 紧跟主窗体右侧（不超出）
- 多屏：Explorer 跟随主窗体所在屏 WorkArea

## 教训

- **不要用 `WindowState` 判定全屏**——真全屏是 `FormBorderStyle.None` + `Bounds = Screen.Bounds`
- **位置计算用 WorkArea 边界检测**，不要耦合 WindowState
- **三步边界检查**确保多屏/真全屏/WorkArea 极窄各种情况都正确
