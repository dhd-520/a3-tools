# 2026-07-17 SQL IntelliSense FROM 弹存储过程 → 过滤

## 陛下反馈

> "A3Tools 内置 SQL 查询工具中，输入 FROM 后提示中又有存储过程，应该只提示表名、视图、表值函数。
> 不应该有存储过程提示，之前修改过又出来了"

## 根因

`SqlObjectSchemaCache.GetObjectSuggestions(connStr, word)` 之前没有按 `ObjectKind` 过滤，返回缓存里的 **6 类全对象**：

- U (Table) ✓
- V (View) ✓
- IF / TF (TableValuedFunction) ✓
- FN (ScalarFunction) ✗（FROM 里不能用）
- **P (StoredProcedure) ✗（陛下反馈的根本原因）**
- TR (Trigger) ✗（FROM 里不能用）

`SqlIntelliSenseProvider.GetSuggestions` 在 3 处 FROM-like 路径调用它都未做二次过滤：

| 位置 | 上下文 |
|------|--------|
| 第 154 行 | `AfterObjectKeyword`（FROM/JOIN/UPDATE/TABLE 后空白） |
| 第 187 行 | schema 前缀路径（如 `dbo.` / `xxx.` 退化场景） |
| 第 223 行 | 普通文本路径（generic 上下文 + schema 限定 prefix） |

### "之前修改过又出来了" 的来龙去脉

- 2026-07-04 commit `6c216b2`（object-explorer 重构）给 `SqlObjectSchemaCache` 加了 `P/TR` 两个 Kind + `GetObjectsByKind` 接口，**但没动 `GetObjectSuggestions` 的过滤**。
- 2026-07-04 commit `e0144d5`（IntelliSense 加数据库对象）首次实现 `GetObjectSuggestions`，那时**缓存里只有 U/V/IF/TF/FN 5 类**（没有 P），所以弹窗天然就没存储过程。
- 加完 P 之后 `GetObjectSuggestions` 没同步加 kind 过滤 → 存储过程泄到 FROM 弹窗。

> 教训：往缓存里加 Kind 时，**所有"返回对象的 API"都要同步审视是否需要 kind 过滤**。

## 修复

### `SqlObjectSchemaCache.GetObjectSuggestions` 加可选参

```csharp
public static List<string> GetObjectSuggestions(
    string connectionString, string word,
    IEnumerable<ObjectKind>? kinds = null)   // ← 新增，默认 null = 全部（向后兼容）
{
    ...
    HashSet<ObjectKind>? kindSet = null;
    if (kinds != null)
    {
        var arr = kinds as ObjectKind[] ?? kinds.ToArray();
        if (arr.Length > 0) kindSet = new HashSet<ObjectKind>(arr);
    }

    var matches = entry.Objects
        .Where(o => kindSet == null || kindSet.Contains(o.Kind))   // ← 新过滤
        .Where(o => ...)
        ...
}
```

### `SqlIntelliSenseProvider.GetSuggestions` 3 处调用全传 FROM-allowed kinds

```csharp
var fromKinds = new[] {
    SqlObjectSchemaCache.ObjectKind.Table,
    SqlObjectSchemaCache.ObjectKind.View,
    SqlObjectSchemaCache.ObjectKind.TableValuedFunction
};
var objs = SqlObjectSchemaCache.GetObjectSuggestions(connStr, prefix, fromKinds);
```

EXEC 上下文（`AfterExec`）走完全独立的分支（不调 GetObjectSuggestions），不受影响。

## 改动文件

| 文件 | 改动 |
|------|------|
| `A3Tools.Plugins.Default/Forms/SqlObjectSchemaCache.cs` | `GetObjectSuggestions` 加可选参 `IEnumerable<ObjectKind>? kinds = null` + 内置过滤逻辑 |
| `A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs` | 3 处调用都传 `{ Table, View, TableValuedFunction }` |

## 编译

`dotnet build A3Tools.sln -c Debug` → **0 错 2 warning**（NU1701 NPinyin 包警告，旧 warning）

## 待陛下回归

- [ ] `SELECT * FROM ` → 弹窗**只有**表/视图/表值函数（无存储过程/标量函数/触发器）
- [ ] `SELECT * FROM dbo.` → 同上，仅 dbo schema 下
- [ ] `SELECT * FROM S_SCM_SEORDER` 后输别名 `a` → 输入 `SELECT a.` 仍弹列（不受影响）
- [ ] `EXEC ` → 仍弹**存储过程**（独立路径，未动）
- [ ] `EXEC sp_` → 仍按 sp_ 前缀过滤存储过程