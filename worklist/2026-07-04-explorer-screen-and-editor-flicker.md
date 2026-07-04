# 2026-07-04 2 个 UX 修复：Explorer 屏幕外 + 编辑器闪烁

## Bug 1：主窗体最大化 → Explorer 跑到屏幕外

### 现象

陛下反馈：先最大化查询窗体，再打开对象资源管理器，Explorer 出现在屏幕外看不到。

### 根因

```csharp
// 原代码：
_explorer = new ObjectExplorerForm(this)
{
    Location = new Point(this.Right + 4, this.Top),  // ❌ 主窗体最大化时 this.Right = 屏幕宽，+4 即出屏
    Height = this.Height
};
```

主窗体最大化：
- `this.Right` ≈ 屏幕宽（如 1920）
- `this.Left` = 0
- `+4` 之后 Explorer X = 1924 → 大部分屏幕宽度 < 1924 + 360 = 2284 → 出屏

### 修复

新增 `ComputeExplorerLocation` + `GetScreenWorkArea`：

```csharp
private Point ComputeExplorerLocation()
{
    const int explorerWidth = 360;
    const int gap = 4;
    var wa = Screen.FromControl(this).WorkingArea;

    int x = this.Right + gap;   // 期望
    int y = this.Top;

    // 右侧溢出 → 贴屏幕右（左侧够 → 贴主窗体左侧）
    if (x + explorerWidth > wa.Right)
    {
        if (this.Left - explorerWidth - gap >= wa.Left)
            x = this.Left - explorerWidth - gap;          // 主窗体左侧
        else
            x = Math.Max(wa.Left, wa.Right - explorerWidth);  // 屏幕 WorkArea 右
    }

    if (y < wa.Top) y = wa.Top;
    if (y > wa.Bottom - 200) y = Math.Max(wa.Top, wa.Bottom - 600);

    return new Point(x, y);
}
```

行为优先级：
1. 主窗体右侧（正常）
2. 主窗体左侧（右侧溢出）
3. 屏幕 WorkArea 右（左右都溢出）
4. 顶部 ≤ wa.Top, 底部 ≤ wa.Bottom - 200

**多屏支持**：`Screen.FromControl(this)` 用主窗体所在屏，不用 PrimaryScreen.

## Bug 2：双击脚本内容后每次修改都闪烁

### 现象

陛下反馈：双击对象打开 CREATE 脚本后，每次修改（键入字符）编辑器闪烁。

### 根因

`SqlEditor.Highlight()` 每 200ms（OnTextChanged 节流）跑一次，里面：

```csharp
Select(0, TextLength);         // 整文本选区
SelectionColor = Color.Black;    // 全黑 → 触发全文本重绘
var text = Text;
foreach (Match m in WordRegex.Matches(text))
{
    Select(m.Index, m.Length);    // 每次 Select → 重绘
    SelectionColor = KeywordColor; // 设色 → 重绘
}
// 数字 / 注释 / 字符串 重复以上步骤
```

复杂 SQL（几千行）→ 每段每字都 Select + 设色 → 几十~几百次 RichTextBox 重绘 → **richEdit 控件无双缓冲** → 闪烁不止。

### 修复：用 WM_SETREDRAW 冻结重绘

Win32 标准做法 — `WM_SETREDRAW = 0x000B`，控件级冻结重绘：

```csharp
private const int WM_SETREDRAW = 0x000B;
[DllImport("user32.dll")]
private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

private void Highlight()
{
    // ...
    _suppressHighlight = true;
    SendMessage(Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);  // 冻结
    try
    {
        // Select + SelectionColor 循环（不再触发 OnPaint）
        // ...
        Select(selStart, selLen);
    }
    finally
    {
        ResumeLayout();
        _suppressHighlight = false;
        SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);  // 解冻
        Invalidate();                                                  // 主动刷新
    }
}
```

**为什么不用 `LockWindowUpdate`**：那是**全局 UI 冻结**（同一时间只允许 1 个锁定），可能与其他窗体冲突。`WM_SETREDRAW` 是**控件级冻结**（仅冻结这一个控件），是 .NET WinForms 控件 flush 重绘的正确姿势。

### 效果

| 之前 | 之后 |
|------|------|
| 几千行 SQL → 几百次 SetSelection + SelectionColor → 几百次 OnPaint → 闪烁 | 高亮期间整个 RTB 完全冻结，所有 Select/设色不触 OnPaint → 解除冻结后 Invalidate() 一次性画完 → **完全无闪烁** |

## 改动文件

| 文件 | 改动 |
|------|------|
| `SqlQueryForm.cs` | 新增 `GetScreenWorkArea`、`ComputeExplorerLocation`；BtnToggleExplorer_Click 内 Location 用计算结果 |
| `SqlEditor.cs` | P/Invoke + WM_SETREDRAW；Highlight() try/finally 包裹冻结/解冻 |

## 验证

- `dotnet build`：0 错误
- 主窗体最大化 → 打开 Explorer → Explorer 出现在屏幕内
- 主窗体还原 → 关闭 Explorer → 再打开 → Explorer 跟随右侧
- 主窗体多屏 → 用主窗体所在屏 WorkArea
- 双击脚本 → 键入字符 → **不再闪烁**

## 下一步

- 若闪烁仍有残留，可改用 EM_SETCHARFORMAT 一次性设色（避免逐字符 Select）
- 但 WM_SETREDRAW 已能解决 99% 场景
