# 2026-09-14 SQL 编辑器修复（陛下 09:23 反馈）

## 陛下反馈（重复请求，09-11 和 09-14 都说了一样的两件事）

1. **选中不好用**：经常选多或选少，不听使唤
2. **LEFT JOIN 别名后无提示**：`LEFT JOIN 表 别名` 后输入 `别名.` 没提示

## 根因分析

### Bug 1: 选中跳选 — `SqlEditor.Highlight` 打断拖选

**位置**：`A3Tools.Plugins.Default/Forms/SqlEditor.cs` `Highlight()`

**机制**：
- `OnTextChanged` 节流（200ms）→ `_highlightTimer` 触发 → `Highlight()`
- `Highlight()` 内部强制 `Select(0, TextLength)` → 全文字色 → 恢复 `(selStart, selLen)`
- **如果用户在 200ms 节流窗口内开始鼠标拖选**：
  - 200ms 前 timer 启动，记录旧 `selStart`/`selLen`
  - 用户从旧位置开始拖到新位置 → `SelectionStart` 改变
  - 200ms 后 timer 触发 `Highlight` → `Select(0, TextLength)` 把选区覆盖为全文 → 再 `Select(selStart, selLen)` 强制拉回 timer 启动时的位置
  - **结果**：用户拖的选区被吞掉，"选区跳回" / "选少了"
  - 反过来：如果 timer 启动时选区很小，highlight 后用户正在大范围拖选 → 强制拉回小选区 = "选多了"

**历史背景**：
- 2026-07-04 `2026-07-04-explorer-screen-and-editor-flicker.md`：`WM_SETREDRAW` 冻结重绘 + `SuspendLayout()` 防闪烁 → 没考虑拖选场景
- 2026-07-07 `2026-07-07-fix-selection-add-search-replace.md`：加了搜索替换，没动选中
- 2026-07-15 `2026-07-15-sql-query-single-execute-button-and-selection-fix.md`：把 `AutoWordSelection = false` → **解决了"双击选词吸附"**，但**没解决**"高亮打断拖选"

**修复**：`Highlight()` 入口检查 `MouseButtons != MouseButtons.None` → 用户在拖选时直接 return。
- 用户拖选时高亮延迟到松手后再执行（下次 `OnTextChanged` 或 timer 重启触发）
- 这是无侵入修复，不改任何已有行为

### Bug 2: LEFT JOIN 别名无提示 — `GetAllColumnsFromAliases` 逻辑漏洞

**位置**：`A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs`

**机制**：
- 场景 A：`SELECT * FROM T1 LEFT JOIN T2 b ON b`（光标在 `b` 后，无 `.`）
  - `GetCurrentWord()` → `"b"`
  - `DetectContext()` → `AfterColumnKeyword`（正确）
  - 走到 `-0. SELECT/WHERE/ON 后空白 → 弹列` 分支：
    ```csharp
    var cols = GetAllColumnsFromAliases(connectionString, fullSql, prefix);
    ```
  - `GetAllColumnsFromAliases` 内部：
    ```csharp
    var matched = string.IsNullOrEmpty(pre)
        ? all
        : all.Where(c => c.StartsWith(pre, StringComparison.OrdinalIgnoreCase)).ToList();
    ```
  - `all` = 所有 alias 的所有列名（纯名 `Name`/`GUID`/`Status` 等）
  - `pre = "b"` → `matched` = 所有 startsWith "b" 的列（`Body`/`Brand`...）
  - **问题**：`b` 是别名 `T2 b`，期望返 T2 的列；实际返所有表的列名 startsWith "b"
  - 退一步：即使 `matched` 非空，返的是混合的列名（来自 T1、T2 和其他），不是别名 `b` 对应的 T2 的列 → **"提示混乱"**
- 场景 B：`SELECT * FROM T1 LEFT JOIN T2 b ON b.`（光标在 `.` 后，有 `.`）
  - 走 `prefix 含 '.'` 分支 → `TryGetColumnSuggestion("b.", ...)` → 命中 alias `b` → 返 T2 的列 ✅（这条 OK）
- 场景 C：`SELECT * FROM T1 LEFT JOIN T2 b ON b.G`（光标在 `G` 后）
  - 走 `prefix 含 '.'` 分支 → `TryGetColumnSuggestion("b.G", ...)` → 命中 alias `b`，rightPart=`G` → 返 T2 startsWith "G" 的列 ✅（这条也 OK）

**所以唯一漏的场景就是 "prefix 不含 . 且 prefix 是 alias 名"**，原逻辑没走 alias 路径。

**修复**：
- 把 `GetAllColumnsFromAliases` 改名 `GetColumnsForAliasPrefix`，增加 `caretOffset` 参数（之前传 0 没用上）
- 核心逻辑：
  - 如果 `prefix` 命中 `aliasMap` 某个 key → **只拉该 alias 的列**（陛下要的）
  - 如果 `prefix` 没命中 alias → 拉所有 alias 的列（兼容 "输了一半匹配不到" 的场景），按 `prefix` startsWith 过滤

**完整流程**（修复后）：
- `b`（无 .）→ 命中 alias `b` → 只返 T2 的列 ✅
- `T1`（无 .）→ aliasMap 无 `T1`（除非 FROM T1 T1 这种） → 拉所有 alias 列 startsWith "T1" → 没匹配返 null → fall through 到关键字
- `B`（无 .）→ 命中 alias `b`（大小写不敏感）→ 只返 T2 startsWith "B" 的列 ✅（不返别表的）
- `b.`（有 .）→ 走 `TryGetColumnSuggestion` 路径不变 ✅
- `b.G`（有 .）→ 走 `TryGetColumnSuggestion` 路径不变 ✅

## 修复方案

### 改动文件
| 文件 | 改动 |
|------|------|
| `A3Tools.Plugins.Default/Forms/SqlEditor.cs` | `Highlight()` 入口加 `MouseButtons` 检查 |
| `A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs` | `GetAllColumnsFromAliases` 改名 + 优化为 alias-aware + 加 `caretOffset` 参数 |

### Build 验证
- `dotnet build D:\work\A3Tools\A3Tools.Plugins.Default\A3Tools.Plugins.Default.csproj -c Debug`
- 期望：0 错 0 警（之前已有警不增加）

### 测试用例
1. 鼠标从 `SELECT` 中拖选 5 个字符 → 慢慢拖（>200ms）→ 选区不被吞 ✓
2. `SELECT * FROM T1 LEFT JOIN T2 b ON b` 光标在 `b` 后 → 弹 T2 的列（不是混合的）✓
3. `SELECT * FROM T1 LEFT JOIN T2 b ON b.` 光标在 `.` 后 → 弹 T2 的列（之前就 OK，不退化）✓
4. `SELECT * FROM T1 LEFT JOIN T2 b ON b.G` 光标在 `G` 后 → 弹 T2 startsWith "G" 的列 ✓
5. 多 alias 场景：`SELECT * FROM T1 a, T2 b ON a.` 光标在 `.` 后 → 弹 T1 的列 ✓