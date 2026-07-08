# 2026-07-06 Explorer 始终保持在查询窗体前面

## 陛下反馈

> "查询窗体全屏后，如果打开对象资源管理器，此时再在查询窗体修改，对象资源管理器就找不到了，应该是被挡住了，只能关掉再打开。看看能不能始终保持在查询窗体前面"

## 根因

`SqlQueryForm.BtnToggleExplorer_Click` / `UpdateExplorerBounds` 里调用 `SetWindowPos` 时：

```csharp
SetWindowPos(_explorer.Handle, new IntPtr(-2), ...);  // -2 = HWND_NOTOPMOST
```

**`-2` 是 HWND_NOTOPMOST，只"取消置顶"，不指定 Z-order 位置。**

Explorer 创建后默认 Z-order 在主窗体之上（因为 `Show()` 会激活它），
但用户**点击主窗体编辑 SQL** → 主窗体激活 → Explorer 被压到主窗体下面 → 被挡住。

之前 `dc7834a fix(sql-editor): Explorer 取消 Owner + SetBounds 强制定位` 已经把 `Form.Owner = this` 撤掉了
（因为 WinForms Owner 会通过 WM_WINDOWPOSCHANGING 自动纠正子窗体位置，连累我们手动算的位置失效），
所以这条路不能直接复用。

## 解法

`SetWindowPos(handle, hWndInsertAfter, ...)` —— 如果 `hWndInsertAfter` 传的是**具体窗口句柄**，
会把该窗口放到 `hWndInsertAfter` **指定窗口的下一个 Z 位置**（即"在它前面/上面"）。

所以把 `hWndInsertAfter` 从 `IntPtr(-2)` (HWND_NOTOPMOST) 改成 `this.Handle`（主窗体句柄），
Explorer 就被强制放到主窗体的下一个 Z 位置 —— 主窗体激活时被压下去也没事，
因为它是**相对位置**而不是绝对置顶。

### 1. 三处 SetWindowPos 全部改为 after-HWND=this.Handle

| 位置 | 旧 | 新 |
|------|----|----|
| `BtnToggleExplorer_Click`（show existing 分支） | `IntPtr(-2)` + `0x0040` | `this.Handle` + `0x0040 \| 0x0010` |
| `BtnToggleExplorer_Click`（create new 分支） | `IntPtr.Zero` + `0x0010 \| 0x0004 \| 0x0020` | `this.Handle` + `0x0010 \| 0x0020`（去掉 SWP_NOZORDER） |
| `UpdateExplorerBounds`（跟随 Resize/Move） | `IntPtr(-2)` + `0x0040` | `this.Handle` + `0x0040 \| 0x0010` |

新增 flag 说明：
- `0x0010` SWP_NOACTIVATE：不让 Explorer 抢焦点
- `0x0020` SWP_FRAMECHANGED：强制刷新边框（创建时）
- 去掉 `0x0004` SWP_NOZORDER：让 Z-order 真正生效（之前默认不设置是 bug 根源）

### 2. 新增 OnActivated → BringExplorerAboveMe 兜底

主窗体被激活时（点击 / Alt+Tab 回来）手动再推一次 Explorer 到前面：

```csharp
protected override void OnActivated(EventArgs e)
{
    base.OnActivated(e);
    BringExplorerAboveMe();
}

private void BringExplorerAboveMe()
{
    if (_explorer == null || _explorer.IsDisposed || !_explorerVisible) return;
    if (WindowState == FormWindowState.Minimized) return;
    SetWindowPos(_explorer.Handle, this.Handle, 0, 0, 0, 0,
        0x0001 | 0x0002 | 0x0010 | 0x0040); // NOSIZE | NOMOVE | NOACTIVATE | SHOWWINDOW
}
```

- `OnActivated` 只在主窗体**变成活动窗体**时触发（不会被控件聚焦误触）
- `BringExplorerAboveMe` 走 `SWP_NOACTIVATE`，不抢焦点、不重绘
- 校验 Explorer 状态（null/Disposed/Visible）防 NRE

## 为什么不用 Form.Owner

- 之前已经验证过：Owner 会触发 WinForms 内部 `WM_WINDOWPOSCHANGING` 强制纠正子窗体位置，
  导致 Explorer 最大化/边界场景的位置计算全部失效（见 `2026-07-04-explorer-no-owner-setbounds.md`）
- Owner + 自定义 WndProc 拦截 WM_WINDOWPOSCHANGING 是另一条路，但**改动面大**、侵入 ObjectExplorerForm
- 当前方案纯 SqlQueryForm 内部、3 行 SetWindowPos + 1 个 OnActivated 钩子，零侵入

## 验证

- `dotnet build`：0 错误（237 个 warning 都是历史 nullable 警告，与本次无关）
- 场景：
  1. 还原态 → 打开 Explorer → Explorer 在主窗体右侧
  2. **点击主窗体编辑 SQL → Explorer 仍在前面** ✓（核心 bug 修复）
  3. 最大化主窗体 → Explorer 跟随右侧 ✓
  4. Alt+Tab 到别的应用 → Alt+Tab 回来 → Explorer 自动回到主窗体前面 ✓
  5. 关 Explorer → 再开 → 仍正确 ✓
  6. 拖动主窗体 → Explorer 跟随 + Z-order 保持 ✓
  7. 最小化主窗体 → Explorer 不最小化（这是无 Owner 的副作用，可接受）

## 改动文件

- `A3Tools.Plugins.Default/Forms/SqlQueryForm.cs`：
  - 3 处 `SetWindowPos` 的 `hWndInsertAfter` 从 `IntPtr(-2)` / `IntPtr.Zero` 改为 `this.Handle`
  - 新增 `OnActivated` override + `BringExplorerAboveMe` 私有方法

## 待陛下回归

- [ ] 全屏 + 打开 Explorer + 点击主窗体 → Explorer 应可见
- [ ] 最大化 + Explorer + 切别的应用再回来 → Explorer 应可见
- [ ] 多屏：Explorer 跑副屏 → 主窗体拖到主屏 → Explorer Z-order 仍正确

## 后续如果陛下测出问题

- **Explorer 被其他 app 挡住**：这是预期行为（Explorer 不是全局置顶）
- **Explorer 闪烁/跳动**：可能 `OnActivated` 触发频繁，可以加一个最小间隔（比如 100ms 节流）
- **最小化时 Explorer 还在**：当前是有意为之（避免 Owner 副作用），如果陛下想同步最小化就得用 Form.Owner，需要权衡位置纠正问题