# 2026-07-08 修复 SQL 编辑器 Enter 键不换行

## 问题
A3Tools SQL 查询工具的编辑器（SqlEditor）里直接按 Enter 键没有任何反应 — 不换行、不缩进，光标原地不动。

## 根因
`SqlEditor.HandleEnterWithIndent` 中这段代码（2026-07-07 写的"干净实现"，实际是 bug）：

```csharp
// 2. 默认行为（处理换行）
base.OnKeyDown(e);
e.SuppressKeyPress = true;   // ← 元凶

// 3. 插入缩进
SelectedText = indent;
```

**机制：** WinForms RichTextBox 的 Enter 换行不是 `base.OnKeyDown(e)` 干的，也不是 `base.OnKeyDown(e)` 内部的回调干的 — 而是 base 返回之后 WinForms 走 **`WM_CHAR 注入 '\r' → richedit 处理 → 插入段落符`** 这条隐式链路实现的。

`e.SuppressKeyPress = true` 在 `base.OnKeyDown(e)` 之后才设置，等于 **在隐式 WM_CHAR 链路已经完成之后** 才喊"别让 WM_CHAR 处理"，结果就是隐式链路被 SuppressKeyPress 整个吃掉，richedit 永远看不到 `'\r'` → 不换行。

原注释观察到"base.OnKeyDown 后再调 e.SuppressKeyPress 不起作用"，方向完全对，但重写时换汤没换药，base.OnKeyDown + SuppressKeyPress 这两个错误搭档依然原封不动留在代码里，导致 Enter 直接死。

## 修复
绕开 base + 隐式 WM_CHAR 链路的不可靠行为，显式用 `SelectedText` 插入换行 + 缩进，然后 SuppressKeyPress=true 阻断后续 base / WM_CHAR 二次处理。

```csharp
private void HandleEnterWithIndent(KeyEventArgs e)
{
    // 1. 计算上一行缩进（保持原有 BEGIN/IF/WHILE/CASE/( 多一级缩进的逻辑）
    int lineIdxBefore = GetLineFromCharIndex(SelectionStart);
    string indent = "";
    bool needExtraIndent = false;
    if (lineIdxBefore > 0)
    {
        // ... 保持原状 ...
        if (needExtraIndent) indent += "    ";
    }

    // 2. 显式插入 换行 + 缩进（不再依赖 base.OnKeyDown 的隐式段落插入）
    if (IsDisposed || !IsHandleCreated)
    {
        e.SuppressKeyPress = true;
        return;
    }
    _suppressHighlight = true;
    _suppressIntelliSense = true;
    try
    {
        SelectedText = Environment.NewLine + indent;   // "\r\n" → richedit 标准段落符
    }
    catch { /* 控件可能正在销毁 */ }
    finally
    {
        _suppressHighlight = false;
        _suppressIntelliSense = false;
    }

    // 3. 阻断 base.OnKeyDown 与后续 WM_CHAR 二次插入
    e.SuppressKeyPress = true;
}
```

辅助清理：移除未使用的 `int caretBefore = SelectionStart;`（lineIdxBefore 直接走 GetLineFromCharIndex(SelectionStart) 即可）。

## 修改文件
- D:\work\A3Tools\A3Tools.Plugins.Default\Forms\SqlEditor.cs

## 验证
- `dotnet build A3Tools.Plugins.Default/A3Tools.Plugins.Default.csproj -c Debug`：**0 错误**，240 个历史 warning（与本次无关）
- 行为预期：编辑器里按 Enter → 换行 + 自动对齐上一行缩进；BEGIN/IF/WHILE/CASE/( 后多一级缩进

## 教训
1. **WinForms RichTextBox 处理 Enter 的最佳实践**就是"显式 SelectedText，不要玩 suppress 跟 base 之间的暧昧关系"。需要插入文本 + 阻止默认行为时，clear & clean 自己干，不要依赖 OnKeyDown 链路的隐式 WM_CHAR。
2. **修复时把根因写进注释**：上次注释描述了症状，没记根本原因，过两天再回来就只能"按注释继续错"。
