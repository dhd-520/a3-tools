# 2026-07-04 Explorer 位置：取消 Owner 关系 + SetBounds 强制定位

## 陛下反馈

> "还是一样没有任何变化，最大化后，再打开，还是在主窗体最右侧但只有一半显示在屏幕内"

## 根因

**WinForms Owner Form 行为**：当 Form 有 `Owner` 时，WinForms 内部会通过 `SetBoundsCore` + `WM_WINDOWPOSCHANGING` **自动纠正子窗体位置**——如果子窗体的 Right 超出屏幕，会被自动夹到屏幕内。

即使我们的代码算出 `x = wa.Right - 360`（屏幕右内侧），WinForms 内部**还是可能**做出奇怪的"纠正"——特别是当父窗体（Owner）最大化时。

之前所有 `ComputeExplorerLocation` 的修复都**没生效**，因为 **WinForms 内部逻辑在搞事**。

## 修复

### 1. 取消 Owner 关系

```csharp
// 之前：
_explorer = new ObjectExplorerForm(this) { Owner = this, Location = ... };

// 现在：
_explorer = new ObjectExplorerForm(this) {
    // Owner = this,  // 故意不设
    StartPosition = FormStartPosition.Manual,
};
_explorer.SetBounds(loc.X, loc.Y, 360, h, BoundsSpecified.All);
```

**取消 Owner 后**：
- WinForms 不再纠正 Explorer 位置
- 我们完全控制 Location
- Explorer 不再自动跟随主窗体关闭 —— 自己处理（在 `OnFormClosed` 中关闭）

**主窗体关闭时 Explorer 跟随**：

```csharp
protected override void OnFormClosed(FormClosedEventArgs e)
{
    if (_explorer != null && !_explorer.IsDisposed)
    {
        try { _explorer.Close(); } catch { }
        _explorer.Dispose();
        _explorer = null;
    }
    base.OnFormClosed(e);
}
```

### 2. 用 SetBounds 强制定位

```csharp
_explorer.SetBounds(loc.X, loc.Y, 360, h, BoundsSpecified.All);
```

`SetBounds` + `BoundsSpecified.All` 一次性设置 X/Y/W/H，**比 `Location =` 强**——会调 SetBoundsCore 而不是 Location setter，更底层。

### 3. StartPosition = Manual

```csharp
StartPosition = FormStartPosition.Manual,
```

避免 WinForms 在 Show 时根据 `Owner.Location` 自动重定位。

## 改动文件

- `SqlQueryForm.cs`：
  - 取消 `_explorer.Owner = this`
  - 加 `StartPosition = FormStartPosition.Manual`
  - 创建后用 `SetBounds` 强制定位
  - 调试输出已删

## 验证

- `dotnet build`：0 错误
- 还原态 → 打开 Explorer → Explorer 在主窗体**右侧外**
- 最大化 → Explorer 在 **WorkArea 右内侧**（**不被 WinForms 纠正**）
- 关掉 → 再开 → Explorer 仍在 WorkArea 右内侧
- 拖动主窗体到屏幕左 → Explorer 跟随（OnMove 触发）
- 主窗体关闭 → Explorer 跟着关（OnFormClosed 触发）

## 教训

- **WinForms Owner Form 会自动纠正子窗体位置**——这是隐藏行为，没文档说明
- **不要给需要自定义位置的子窗体设 Owner**——取消 Owner 自己控制
- **`SetBounds` 比 `Location =` 更底层**——绕过一些内部逻辑
- **陛下说"怎么改都没用"不是空话**——是 WinForms 在搞事
