# 2026-07-04 Explorer 位置：Show 之前重算 + 主窗体 Resize/Move 跟随

## 陛下反馈

> "一样没有变化，最大化后，再打开，还是在主窗体最右侧但只有一半显示在屏幕内"

## 根因

**FormClosed 之后 _explorer 没被置 null**：

```csharp
_explorer.FormClosed += (_, args) =>
{
    _explorerVisible = false;
    // ❌ 漏：_explorer = null
};
```

`Close()` ≠ `Dispose()`。Close 后 _explorer 仍然存在（IsDisposed 仍为 false）。下次点按钮 → 走"已存在 Show" 分支 → 调 `_explorer.Show()` → **Explorer 回到创建时的位置**（创建时主窗体是还原态，位置对；现在主窗体已最大化，位置就错了）。

## 三处修复

### 1. Show 已存在 Explorer 之前重算位置

```csharp
else
{
    // Show 之前重新算位置 → 解决"创建时位置对、最大化后又错"
    _explorer.Location = ComputeExplorerLocation();
    _explorer.Height = ...;
    _explorer.Show();
    ...
}
```

### 2. FormClosed 事件里 _explorer = null

```csharp
_explorer.FormClosed += (_, args) =>
{
    _explorerVisible = false;
    ...
    _explorer = null;   // ★ 下次点按钮会走"创建新"分支重新计算 Location
};
```

### 3. 主窗体 Resize/Move → Explorer 跟随

```csharp
protected override void OnResize(EventArgs e)
{
    base.OnResize(e);
    UpdateExplorerBounds();
}

protected override void OnMove(EventArgs e)
{
    base.OnMove(e);
    UpdateExplorerBounds();
}

private void UpdateExplorerBounds()
{
    if (_explorer == null || _explorer.IsDisposed || !_explorerVisible) return;
    if (WindowState == FormWindowState.Minimized) return;
    _explorer.Location = ComputeExplorerLocation();
    _explorer.Height = ...;
}
```

**为什么需要 Resize/Move 跟随**：
- 用户拖动主窗体改变位置时，Explorer 应该跟着走
- 用户最大化主窗体时，Explorer 立即贴 WorkArea 右内侧
- 否则**用户必须关掉 Explorer 再开一次**才会重算位置

## 改动文件

- `SqlQueryForm.cs`：
  - BtnToggleExplorer_Click：else 分支加 `Location = ComputeExplorerLocation()`
  - FormClosed 事件加 `_explorer = null`
  - 新增 `OnResize` / `OnMove` / `UpdateExplorerBounds`

## 验证

- `dotnet build`：0 错误
- 真全屏 → 打开 Explorer → 贴 WorkArea 右内侧 ✓
- **真全屏 → 关闭 Explorer → 再打开** → 贴 WorkArea 右内侧 ✓（之前是回老位置）
- **真全屏 → 打开 Explorer → 拖动主窗体到屏幕左** → Explorer 跟随主窗体 ✓
- **真全屏 → 打开 Explorer → 还原主窗体** → Explorer 跟随到主窗体右侧 ✓

## 教训

- **Close ≠ Dispose**：Close 后 _explorer 仍存活，下次 Show 不会触发构造
- **Form 关闭后必须把 _form = null**，否则逻辑分支会跑错
- **Owned Form 应跟随主窗体 Resize/Move**，否则用户改主窗体位置/尺寸后 Explorer 卡在老位置
- **每次 Show 之前重算位置**，因为用户的屏幕状态可能已经改变
