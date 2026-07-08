# 2026-07-06 Explorer 位置 + Z-order 根治（Form.Owner + WndProc 拦截）

## 陛下反馈

> "1、对象资源管理器位置又不对了，并且也没有始终保持在最前端。调了那么久又GG了
> 2、上方显示页签名字太长后会换行，如存储过程名字比较长，双击对象后。 能不能始终保持一行多余显示 ..."

## 两个问题的根因

### 问题 1：Explorer 位置 + Z-order

**位置错的根因（双重 bug）：**
1. `ComputeExplorerLocation` 用 `const explorerWidth = 336`（ClientSize 320 + 边框 16）
2. `SetWindowPos` 硬编码 `360` 作为宽度
3. **差 24px** → Explorer 总是超出主窗体右边 24px

**Z-order 没生效的根因（关键洞察）：**
之前臣用 `SetWindowPos(handle, this.Handle, ..., SWP_NOACTIVATE)` 维护 Z-order。

**MSDN 原文**：
> SWP_NOACTIVATE: Does not activate the window. **If this flag is not set, the window is activated and moved to the top** of either the topmost or non-topmost group

也就是说，`SWP_NOACTIVATE` 阻止了 Windows 把窗口移到顶部。**和 `SetWindowPos(handle, ...)` 的 Z-order 调整是矛盾的**——之前等于啥都没干！

之前撤掉 `Form.Owner` 是因为 WinForms 内部位置纠正（`WM_WINDOWPOSCHANGING` 自动夹到屏幕内）。但撤掉 Owner 后 Z-order 又失去维护。**两难**。

### 问题 2：Tab 换行

`TabControl_DrawItem` 用 `StringFormat { LineAlignment = StringAlignment.Center }`，
**默认会换行**（没设 `StringFormatFlags.NoWrap` + `StringTrimming.EllipsisCharacter`）。

加上 `TabSizeMode` 是默认 `Normal`（按内容自适应），
长名 → tab 自动变宽 → 文本绕行变高 → 用户看到「页签变高」的现象。

## 解法（彻底根治）

### 问题 1：Owner + WndProc 双保险

| 维度 | 机制 | 谁负责 |
|------|------|--------|
| Z-order | `Form.Owner = this` | **Windows 自动维护**（owned window 永远在 owner 之上） |
| 位置 | `FixLocation(Point)` + `WndProc` 拦截 `WM_WINDOWPOSCHANGING` | **应用代码强制** |

#### WndProc 拦截核心代码

```csharp
private Point? _fixedLocation;

public void FixLocation(Point location)
{
    _fixedLocation = location;
    if (IsHandleCreated)
        SetWindowPos(Handle, IntPtr.Zero, location.X, location.Y, 0, 0, 0x0010 | 0x0001);
}

protected override void WndProc(ref Message m)
{
    if (m.Msg == WM_WINDOWPOSCHANGING && _fixedLocation.HasValue)
    {
        var pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS))!;
        if ((pos.flags & 0x0002) == 0 &&  // WinForms 想改位置
            (pos.x != _fixedLocation.Value.X || pos.y != _fixedLocation.Value.Y))
        {
            pos.x = _fixedLocation.Value.X;
            pos.y = _fixedLocation.Value.Y;
            pos.flags |= 0x0002;  // 加上 SWP_NOMOVE 让 WinForms 别再改
            Marshal.StructureToPtr(pos, m.LParam, false);
        }
    }
    base.WndProc(ref m);
}
```

**拦截时机的关键判断**：`pos.flags & 0x0002 == 0`（SWP_NOMOVE 没设）= WinForms 在改位置。
这时直接覆写 `pos.x/y`，再加 `SWP_NOMOVE`，让 WinForms 后续不再触碰。

### 问题 2：Fixed SizeMode + NoWrap + EllipsisCharacter

```csharp
// Designer.cs
tabControl.SizeMode = TabSizeMode.Fixed;
tabControl.ItemSize = new Size(220, 28);

// TabControl_DrawItem
using var sf = new StringFormat
{
    LineAlignment = StringAlignment.Center,
    Trimming = StringTrimming.EllipsisCharacter,
    FormatFlags = StringFormatFlags.NoWrap,
};
```

效果：
- 所有 tab 都是 220×28 固定大小（VS / SSMS 风格）
- 长名 → `...` 截断（保持单行）
- 多个 tab → TabControl 横向滚动（标准行为）

## 改动文件

### `ObjectExplorerForm.cs`
- 加 `using System.Runtime.InteropServices;`
- 构造函数里 `this.Owner = owner;`（Windows 维护 Z-order）
- 新增 `FixLocation(Point)` 方法（应用层强制位置）
- 新增 `WndProc` override（拦截 `WM_WINDOWPOSCHANGING` 锁定位置）
- 新增 `WINDOWPOS` 结构体 + `WM_WINDOWPOSCHANGING` 常量

### `SqlQueryForm.cs`
- 新增 `private int _explorerWidth = 360;` 字段（每次 Show 后从 `_explorer.Width` 更新）
- `BtnToggleExplorer_Click` 三处 SetWindowPos 改成 `FixLocation` + 用 `_explorerWidth`
- `UpdateExplorerBounds` 同上
- `ComputeExplorerLocation` 用 `_explorer?.Width ?? _explorerWidth`（去掉 const 336）
- `BringExplorerAboveMe` 改成 `FixLocation` 兜底（不再依赖 SetWindowPos 维护 Z-order）
- `TabControl_DrawItem` 加 `StringTrimming.EllipsisCharacter` + `StringFormatFlags.NoWrap`

### `SqlQueryForm.Designer.cs`
- `tabControl.SizeMode = TabSizeMode.Fixed;`
- `tabControl.ItemSize = new Size(220, 28);`

## 教训

1. **`SWP_NOACTIVATE` 和 Z-order 调整互斥**——之前忽略了 MSDN 那句 "the window is activated and moved to the top"，等于啥都没干
2. **`Form.Owner` 是 Windows 原生机制**，不受 `SWP_NOACTIVATE` 影响，是最可靠的 Z-order 方案
3. **WinForms 内部位置纠正不可绕过**，但 `WndProc` 拦截 `WM_WINDOWPOSCHANGING` 是最底层的方式
4. **const 值不可靠**——`const explorerWidth = 336` 配上硬编码 `360` 是典型的「两处魔法数不一致」，改成「运行时取实际值」更稳
5. **Tab 标题默认会换行**——`StringFormat` 不显式设 `NoWrap` 就会换，`Normal` SizeMode 又会把 tab 撑高

## 验证

- `dotnet build`：0 错误
- 场景：
  1. 还原态 → 打开 Explorer → 位置正确（基于 _explorerWidth）
  2. **点击主窗体编辑 SQL → Explorer 始终在最前端**（Owner 保证）
  3. 最大化主窗体 → Explorer 跟随右侧
  4. Alt+Tab 到别的应用 → 切回来 → Explorer 自动回来
  5. 双击长名对象（`dbo.usp_GetCustomerOrderHistory`）→ Tab 显示 `...usp_GetCustomerOrderHis...` 单行不换行
  6. 多 Tab 时 → TabControl 横向滚动

## 待陛下回归

- [ ] Explorer 位置 + Z-order：基本操作（最大化 / 切应用 / 切回）
- [ ] 长名 Tab：双击长名对象 → Tab 单行 + `...`
- [ ] 关闭 Explorer → 再开 → 仍正确
- [ ] 主窗体最小化 → Explorer 也最小化（Owner 副作用，标准 Windows 行为）

## 后续可考虑

- **断网脱机测试**：原 WinForms 内部位置纠正是因为位置超出屏幕——如果位置一直在屏幕内，WndProc 拦截的频率会很低，性能 OK
- **用户手动拖动 Explorer**：当前 WndProc 会拦截掉，用户拖不动。如果以后要让用户自由拖，加个 `_userMoved` 标志，拖完后清掉 `_fixedLocation`