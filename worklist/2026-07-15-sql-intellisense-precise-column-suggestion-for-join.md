# 2026-07-15 SQL IntelliSense：JOIN 场景精确列名提示

## 问题
陛下反馈：复杂 SQL（特别是 JOIN 场景）下输入 `表名.` 或 `别名.`，IntelliSense 提示混乱，混入其他表的列 / 关键字。

## 根因
`SqlIntelliSenseProvider.GetSuggestions` 在 prefix 含 `.` 时有 3 个问题：

1. **3 段全限定名（`dbo.TableA.col`）不支持**：`TryGetColumnSuggestion` 限制 `parts.Length == 2`，3 段直接 return null → fall through 到 关键字 + 对象，把 SELECT / WHERE 之类涌进来。
2. **fall through 太激进**：alias 解析失败（复杂查询、CTE、子查询）时，prefix 含 `.` 也照样走 关键字 + 对象 兜底，结果混进一坨。
3. **注释/字符串里的 `FROM` / `JOIN` 被误识别**：`/* SELECT * FROM T1 */` 里的 FROM 被 `regexObj` 误加进 alias map，导致 `T1.` 弹出错误表的列。

## 修改

### SqlIntelliSenseProvider.cs
1. **`GetSuggestions` 新增 prefix 含 `.` 的"精确路径"**（`// ===== 0.` 段重写）：
   - 调 `TryGetColumnSuggestion` → 命中 → 只返该表/别名列（不再混）
   - miss → 调 `SqlObjectSchemaCache.GetObjectSuggestions` 试 schema 路径（`dbo.` → 返 dbo 下对象）
   - 都没有 → 返回空（保持弹窗干净，不再 fall through 到关键字）

2. **`TryGetColumnSuggestion` 升级支持 3 段**：
   - 2 段：`A.` / `A.N` / `Customer.` / `Customer.N`（原有行为）
   - 3 段：`dbo.TableA.` / `dbo.TableA.col`（新增）
   - 4+ 段：返回 null
   - alias 优先：leftPart 命中 alias map 时按 alias 解析（即便 prefix 有 schema 段也不影响）

3. **删掉旧的位置（在 AfterColumnKeyword / AfterObjectKeyword 之后）**：旧的 `TryGetColumnSuggestion` 调用现在 unreachable，移除避免代码混淆。

### SqlAliasResolver.cs
1. **新增 `StripCommentsAndStrings` 方法**：用同长度空格替换 SQL 中的块注释 / 行注释 / 字符串内容（含 `''` 转义），保留方括号（供 obj 正则识别 `[dbo].[T1]`）。保持偏移 → regex 匹配位置不变。
2. **4 个正则匹配改用 `cleanSql`**：`regexObj` / `regexTvfCols` / `regexComma` / `regexObjNoAlias` 全部走剥离后的 SQL，避免注释里的 FROM/JOIN 误识别。

## 行为对照

| 输入 | 之前 | 现在 |
|------|------|------|
| `a.` 在 JOIN ON | alias 的列 | alias 的列（不变） |
| `T1.` 在 WHERE | T1 的列 | T1 的列（不变） |
| `dbo.TableA.col` | 涌关键字 | dbo.TableA 的 col* 列 |
| `dbo.` | dbo 下表 | dbo 下表（不变） |
| `xxx.`（未识别）| 涌关键字 + 对象 | 空（干净） |
| `/* JOIN */ FROM T1` | JOIN 加进 alias map | JOIN 被注释剥离，不影响 |

## 验证
- `dotnet build A3Tools.Plugins.Default` → 0 错 312 警告（警告全是既有）
- `dotnet run --project TestContext` → **48 pass / 0 fail**（DetectContext / 既有 alias 解析 / EXEC 匹配等测试全过）

---

## 2026-07-15 同一会话补：补全时保留 "别名." / "表名." 不被吃

### 问题
陛下反馈：在 JOIN ON 子句里输 `T1.Na` 弹候选列，回车选 `Name` → 文本变成 `Name`，`T1.` 被吃了。

### 根因
`SqlEditor.GetCurrentWordStart()` 把 `.` 当 word 的一部分（这是故意的——让 `T1.Na` 整体送给 `GetSuggestions` 解析表名）。但 `ReplaceCurrentWord` 拿到这个起点后**从最早起点全替换**，于是 `T1.Na` 整个被替换为 `Name`。

### 修改
**`SqlEditor.cs / ReplaceCurrentWord`**：
- 在拿到 `start = GetCurrentWordStart()` 后，从 caret 向前找**最后一个 `.`**
- 把 `start` 推到 `.+1`（含 `.` 之前的所有内容保留）
- 无 `.` 时 start 不变（走原逻辑）

行为对照：

| 输入 → 回车选 | 之前 | 现在 |
|------|------|------|
| `T1.Na` → Name | `Name` ❌ | `T1.Name` ✓ |
| `T1.` → Name | `Name` ❌ | `T1.Name` ✓ |
| `dbo.T1.Na` → Name | `Name` ❌ | `dbo.T1.Name` ✓ |
| `T1.[col].Na` → Name | `Name` ❌ | `T1.[col].Name` ✓ |
| `Na` → Name | `Name` ✓ | `Name` ✓（无 dot 走原逻辑） |

### 验证
- `dotnet build A3Tools.Plugins.Default` → 0 错 312 警告（警告全是既有）