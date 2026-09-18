# 修复 IntelliSense 列名提示丢失 — Take(50) 把 TOP* 列砍掉

**日期**: 2026-09-18
**现象**: `select * from S_SCM_ITEM WHERE TOP` 弹窗里只显示 `TOP` 关键字,看不到 `TOPITEMTYPEGUID` 等 TOP* 列名(200+ 列的大表)
**真实根因**: `GetAllColumnsFromAliases` 把用户 prefix 丢了,内层 `GetColumnSuggestions` 不过滤直接拿前 50 个 → 排在 column_id 60+ 的 `TOPITEMTYPEGUID` 被砍

---

## 现象回顾(陛下原话)

> "表 `S_SCM_ITEM` 有一列 `TOPITEMTYPEGUID` 无论怎么输入都无法提示出来,是不是列名包含关键词 TOP 原因"

试过:
- `WHERE TOP` 末尾 → 弹窗只有 `TOP`
- `SELECT TOPITEMTYPEGUID FROM ...` 中间 → 也不提示
- `WHERE TOP` 前面 `FROM` 后面 → 也不提示

---

## 排查过程(3 步定位)

### Step 1: 第一个假设 — STRING_AGG 截断 ❌

观察:`SqlObjectSchemaCache.LoadFromDbAsync` 用 `STRING_AGG(c.name, ',')` 拼接列名。
默认返回类型是 `NVARCHAR(4000)`,超过会被**静默截断** → 后加的自定义字段排在末尾被砍掉。

**改了一版**:`CAST(c.name AS NVARCHAR(MAX))` 让返回类型升级到 NVARCHAR(MAX)。
二进制验证改动进了 DLL(`NVARCHAR(MAX)` count=1,旧 `STRING_AGG(c.name` count=0)。

但陛下手测**仍然不工作**。

### Step 2: 加诊断日志定位 ❌

加 2 处 temp log(`%TEMP%\a3-intellisense.log`):

| 日志标记 | 位置 | 关键字段 |
|---|---|---|
| `[DIAG-CACHE]` | `LoadFromDbAsync` 行级 | `colsLen` `hasTopItemTypeGuid` `last100` |
| `[DIAG-GETCOL]` | `GetColumnSuggestions` 调用时 | `cacheTotal` `cacheHas` `matched` `first5` |

陛下实测后日志:

```
[DIAG-CACHE] obj=S_SCM_ITEM schema=dbo type=U colsLen=2820 hasTopItemTypeGuid=True last100=...ITEMTAGFONTCOLORS
[DIAG-GETCOL] obj=S_SCM_ITEM schema= prefix= cacheTotal=2820 cacheHas=True matched=50 first5=GUID,PARENTITEMTYPEGUID,ITEMTYPEGUID,PARENTSITEMTYPEGUID,ITEMTYPE
```

| 字段值 | 揭示真相 |
|---|---|
| `colsLen=2820` | **远小于 4000,根本不会被截断!** STRING_AGG 假设是错的 |
| `hasTopItemTypeGuid=True` | cache 里**确实有** `TOPITEMTYPEGUID` |
| `matched=50` | **Take(50) 上限触发了!** |
| `first5=GUID,PARENT...` | 按 `column_id` 排序前 50 个里**没有 TOPITEMTYPEGUID** |

### Step 3: 找到真凶 ✅ — prefix 被丢 + Take(50) 截断

链路:

```
User 输入 "TOP"
   ↓
GetSuggestions(prefix="TOP")              ← ✓ 用户的 prefix 对
   ↓ (AfterColumnKeyword 分支)
GetAllColumnsFromAliases(prefix="TOP")
   ↓ aliasMap.TryGetValue("TOP") = false  (alias 是 S_SCM_ITEM)
   ↓ 走 loop 拉所有 alias 的列
GetColumnSuggestions(..., prefix="")       ← ★  这里 prefix 被丢了!
   ↓
.Where(c => StartsWith(""))               ← 等于没过滤
   ↓
.Take(50)                                  ← ★ 取前 50 个就截断!
```

S_SCM_ITEM 大约 **200+ 列**(colsLen=2820 / ~12字节/列)。`TOPITEMTYPEGUID` 是后加字段,`column_id` 大,排在第 60-70 位。
`Take(50)` 直接砍掉 → `all.Where(StartsWith("TOP"))` 在 50 列范围里找不到任何 TOP* → 返空 → 退到关键字弹窗 → 只剩 `TOP` 关键字。

---

## 修复

### 主修复:`GetAllColumnsFromAliases` 透传 prefix

文件: `A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs`

**改之前**:
```csharp
var all = new List<string>();
foreach (var kv in aliasMap)
{
    var cols = SqlObjectSchemaCache.GetColumnSuggestions(
        connectionString, kv.Value.SchemaName, kv.Value.ObjectName, "");  // ★ 空 prefix
    foreach (var c in cols) if (seen.Add(c)) all.Add(c);
}
if (all.Count == 0) return null;

var pre = prefix ?? "";
var matched = string.IsNullOrEmpty(pre)
    ? all
    : all.Where(c => c.StartsWith(pre, StringComparison.OrdinalIgnoreCase)).ToList();  // 外层补救过滤
return matched.Count == 0 ? null : matched;
```

**改之后**:
```csharp
var all = new List<string>();
var pre = prefix ?? "";
foreach (var kv in aliasMap)
{
    // ★ 2026-09-18 修复: 透传用户的 prefix 到内层, 让 GetColumnSuggestions 在 filter 之后再 Take(50)。
    // 之前传 "" 导致 GetColumnSuggestions 不过滤, 直接拿前 50 个 → TOPITEMTYPEGUID (column_id 60+)
    // 被砍掉, 弹窗里只剩 TOP 关键字。陛下实测 colsLen=2820/200+ 列, Take(50) 漏掉所有 TOP* 列。
    var cols = SqlObjectSchemaCache.GetColumnSuggestions(
        connectionString, kv.Value.SchemaName, kv.Value.ObjectName, pre);  // ★ 传 pre
    foreach (var c in cols) if (seen.Add(c)) all.Add(c);
}
// 内层已经按 prefix StartsWith 过滤+Take(50), 直接返回即可。
return all.Count == 0 ? null : all;
```

**为什么这样改**:`GetColumnSuggestions` 内部已实现 `Where(StartsWith(prefix)).Take(50)`,把 prefix 透传过去,过滤在 `Take(50)` **之前**完成 → TOP* 列先被过滤出来(数量少,远不到 50),然后 Take(50) 完全没影响。

**为什么 `aliasMap.TryGetValue(prefix, out aliased)` 分支仍传 `""`**:当 prefix 是 alias 名(如 `A.`)时,`A.` 后需要返回 A 的**所有**列,不能按 prefix 过滤 — 这是 `TryGetColumnSuggestion` 的语义,保留。

### 副修复(顺手做,虽然不是必要):`STRING_AGG` CAST NVARCHAR(MAX)

文件: `A3Tools.Plugins.Default/Forms/SqlObjectSchemaCache.cs`(line 375 + 432)

两处 `LoadFromDbAsync` / `LoadFromDbViaHttpAsync`:

```sql
SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY c.column_id)
```

改为:

```sql
SELECT STRING_AGG(CAST(c.name AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY c.column_id)
```

**为什么保留这个改动**:虽然本次 S_SCM_ITEM colsLen=2820 没截断,但如果将来某个表 > 4000 字节(列数 ~300+)就会被**静默**砍尾部列。CAST 成 NVARCHAR(MAX) 后输出就是 NVARCHAR(MAX),**以后任何宽表都不会再被截断**。防患于未然,不改白不改。

---

## 验证

| 阶段 | 结果 |
|---|---|
| 改之前 | `WHERE TOP` → 弹窗只有 `TOP` 关键字 |
| 改之后(陛下手测) | `WHERE TOP` → 弹窗**看到** `TOPITEMTYPEGUID`(以及其他 TOP* 列) |

---

## 改动清单

| 文件 | 行 | 性质 |
|---|---|---|
| `A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs` | 567-578 | 主修复(prefix 透传) |
| `A3Tools.Plugins.Default/Forms/SqlObjectSchemaCache.cs` | 375 | 副修复(STRING_AGG NVARCHAR(MAX)) |
| `A3Tools.Plugins.Default/Forms/SqlObjectSchemaCache.cs` | 432 | 副修复(STRING_AGG NVARCHAR(MAX)) |

**改动行数**:3 行核心改动 + 注释 6 行,共 9 行净增

---

## 学到的经验

1. **`Take(N)` 是精度陷阱**:任何限制数量的 `Take` 都要确保在过滤**之后**,否则会把"应该匹配的项"砍掉。本次就是 `Where(prefix).Take(50)` 的语义被外层破坏(传空 prefix),导致 `Where` 失效,`Take(50)` 砍掉本应命中的列。
2. **诊断日志比理论分析更快定位**:日志一行 `[DIAG-GETCOL] matched=50 first5=GUID,...` 直接锁定了 `Take(50)` 砍列表的问题,不用再纠结关键字 / alias / cache。
3. **`STRING_AGG` 默认 4000 截断是 SQL Server 经典坑**:即使本次不是根因,所有 `STRING_AGG` 都建议 CAST 成 NVARCHAR(MAX)。

---

## Commit

待补:本文件配合 commit `feat(intellisense): 修复 Take(50) 把 TOP* 列砍掉导致 IntelliSense 不提示`