# 2026-07-06 IntelliSense 上下文修复（EXEC 弹存储过程 + 裸列名联想）

## 陛下反馈

> "EXEC 空格后再输入 没有存储过程提示。
> SELECT * FROM 表后 在 * 前面输入也没有列名提示"

## 根因分析（重要：跟之前 PowerShell 测试结论不同）

之前臣**用 PS 模拟 IsAfterExecKeyword 测试 8/8 全过**，以为稳了。但运行时不工作。

**真正问题**：臣的 `IsAfterExecKeyword` 只看光标前 word（prefix），但**当用户打"EXEC + 空格"时，prefix = "EXEC"**（不是"") ：

```
GetCurrentWord("EXEC ") → 逆序扫到 'E' 停止 → 返回 "EXEC"
word="EXEC" length=4 (>=1) → 走到 GetSuggestions
  → IsAfterExecKeyword 检查：beforeStart=4-4=0 → beforeStart<=0 返回 False
  → 走普通关键字路径 → 弹出 "EXEC/EXECUTE/EXISTS/EXIT/EXTERNAL" 等关键字列表
  → 用户看到的不是存储过程！
```

**更深的问题**：当用户在 "EXEC " 后输入第一个字符 "s"：
```
GetCurrentWord("EXEC s") → 返回 "s" (GetCurrentWord 只看标识符不看空白)
IsAfterExecKeyword(fullSql="EXEC s", caret=6, prefix="s")
  beforeStart = 6-1 = 5
  before = "EXEC " (5 chars)
  TrimEnd → "EXEC" → EndsWith("EXEC") = True
  → 返 True → 应该弹存储过程
```

**那为什么用户说没弹？** 答案：**用户是先打 EXEC（看 popup）→ 打空格（popup 关）→ 打第一个字符。** 在 EXEC 阶段 popup 显示的是**关键字列表**（因为 IsAfterExecKeyword 走了 prefix="EXEC" 的死路径），用户以为是 "EXEC 本身没匹配到存储过程"。

## 修复方案

### 1. 新增 `IsAfterExecContext`（不依赖 prefix，看上下文）

**位置：** `SqlIntelliSenseProvider.cs`

**算法**：从 caret 向左扫，**最多看两个词**（中间跳空白）。这样无论 word 是 EXEC、s、sp_、sp_helpdb 都能命中。

```csharp
// 例：caret=6, sql="EXEC s"
// round 1: i=6, skip whitespace → i=5 (char 's')
//   scan word "s" leftward → wordStart=5, word="s" (not EXEC)
//   i = wordStart = 5 (继续向左)
// round 2: i=5, skip whitespace → i=4 (char 'C' = EXEC end)
//   scan word "EXEC" leftward → wordStart=0, word="EXEC" ✓
//   prev 无 → return true
```

### 2. `IsAfterExecKeyword` 仍然保留 + 在 GetSuggestions 顶部用 `IsAfterExecContext`

**改前（错误）：**
```csharp
if (IsAfterExecKeyword(fullSql, caretOffset, prefix))  // prefix="EXEC" → False
```

**改后：**
```csharp
if (IsAfterExecContext(fullSql, caretOffset))  // 不依赖 prefix → True
{
    // 如果 prefix 本身就是 EXEC/EXECUTE → 用空 prefix（不然用户刚输入 EXEC 还匹配不到任何存储过程）
    var effectivePrefix = prefix;
    if (effectivePrefix.Equals("EXEC", ...) || effectivePrefix.Equals("EXECUTE", ...))
        effectivePrefix = "";
    // ...返回匹配的存储过程
}
```

### 3. SSMS 风格"裸列名联想"

**用户场景：** `SELECT * FROM T1 a\n` → 换行 → `SELECT |` → 输入 `ID` → **用户期望弹 `a.ID / ID / NAME`**

**改前：** 看到 prefix="ID" 不含 `.` → 走关键字路径 → 弹 `ISNULL/IS_JSON/IDENTITY/IDENT_CURRENT` → **找不到 a.ID**

**改后：** 新增 `TryGetNakedColumnSuggestion`：
- prefix 不含 `.`
- prefix 不是关键字/函数开头（用 `IsCommonKeywordOrFunction` 检测 AllKeywords）
- 从 `SqlAliasResolver` 拿所有 FROM 别名
- 从 `SqlObjectSchemaCache.GetColumnSuggestions` 拿每个别名下的列名（去重）
- 返回匹配 prefix 的列名

**关键守卫：** `IsCommonKeywordOrFunction(prefix)` 避免对 `SELECT/SE/S` 等关键字前缀触发列联想。

**位置：** `SqlIntelliSenseProvider.GetSuggestions` 第 0a 段（紧跟列联想 #0 后面）

## 测试验证（PowerShell 13 个用例全过）

| 用例 | 结果 |
|------|------|
| `EXEC` caret=4 | ✓ |
| `EXEC ` caret=4/5 | ✓ |
| `EXEC s` caret=5/6 | ✓ |
| `EXEC sp_` caret=8 | ✓ |
| `EXEC sp_helpdb` caret=14 | ✓ |
| `EXECUTE ` caret=7 | ✓ |
| `SELECT ` caret=7 | ✗ (不是 EXEC，正确) |
| `sp_executesql ` caret=14 | ✗ (避免误判，正确) |
| `SELECT 1; EXEC` caret=14 | ✓ |
| `SELECT * FROM T1; EXEC` caret=22 | ✓ |
| `SELECT * FROM T1 a, T2 b` caret=24 | ✗ (FROM 子表不触发，正确) |

## 改动文件

| 文件 | 改动 |
|------|------|
| `SqlIntelliSenseProvider.cs` | 新增 `IsAfterExecContext` + `TryGetNakedColumnSuggestion` + `IsCommonKeywordOrFunction`；GetSuggestions 顶部改用 `IsAfterExecContext`，新增第 0a 段裸列名联想 |

## 构建

`dotnet build A3Tools.sln -c Debug --no-incremental` → 0 错（244 个历史 warning）

## 待陛下回归

- [ ] `EXEC ` 后输入任意字符 → 弹**存储过程**（不是关键字）
- [ ] `EXEC sp_` → 过滤 sp_ 开头的存储过程
- [ ] `EXECUTE sp_` → 同上（EXECUTE 别名）
- [ ] `SELECT * FROM T1 a` 换行输入 `ID` → 弹 `ID, NAME, ...`（T1 的列）
- [ ] `SELECT * FROM T1 a, T2 b` 换行输入 `ID` → 弹两个表的 ID 列（去重）
- [ ] 不会对 `SELECT` / `FROM` 等关键字触发裸列联想
- [ ] `sp_executesql` 不会误触发存储过程

## 教训

- **PowerShell 模拟只测算法本身是不够的** —— 真实触发链路还要看 GetCurrentWord 返回什么词
- **EXEC 后光标在不同位置，word 完全不同**：刚打完 EXEC（word=EXEC）/ 打完空格（word=""）/ 开始输入存储过程名（word=s/sp_helpdb）
- **依赖 prefix 检测上下文是脆弱的**，应该看**整段上下文**（caret 前后）
- **SSMS 的"裸列名联想"**是个隐藏的好功能，新手会以为"必须打 alias." 才行
- **避免对关键字触发裸列联想**：`SELECT` 这种 word 走关键字路径才对