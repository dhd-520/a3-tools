# 2026-07-17 Http 模式 SELECT BILLNO,* 报 DuplicateNameException

## 陛下反馈

> "[14:33:33] Http 代理错误：A column named 'BILLNO' already belongs to this DataTable.
> 我是说现在报错，是要更新服务器代理程序么"

## 根因

**问题在客户端，不在服务器** —— 服务端 `A3ToolsHub/Sql/SqlExecutor.cs` 用的是 `List<ColumnInfo>` 装结果（List 允许重复名），所以服务端 SqlDataReader 拿到 2 个同名 `BILLNO` 列时**不报错**，会原样序列化发回客户端。

客户端 `SqlQueryTabPage.ExecuteViaDataAccessAsync`（Http 模式专用路径）反序列化后直接 `dt.Columns.Add(col.Name, ...)`，**没有去重兜底**，第二次撞名 → 抛 `DuplicateNameException`。

## 三条路径的不一致状态

| 路径 | 入口 | 去重逻辑 | 状态 |
|------|------|---------|------|
| 1. 直连 SqlDataReader | `SqlQueryTabPage.cs:385-394` | `try { dt.Columns.Add(...) } catch (DuplicateNameException) { 加 _2/_3 后缀 }` | ✓ |
| 2. IDataAccess → DataTable | `ProxyHelper.ExecuteQueryToDataTableAsync` (73-86 行) | HashSet 主动去重 + _2/_3 后缀 | ✓ |
| 3. **Http 模式 ExecuteBatchAsync → DataTable** | `SqlQueryTabPage.ExecuteViaDataAccessAsync` (537-538 行) | **❌ 无去重** | ✗ |

陛下踩的就是路径 3。

## 为什么之前没踩到

- 路径 3 是 2026-07-09 加的（Http 模式混合连接），当时只测了正常 `SELECT * FROM T`，没测 `SELECT col, *` 这种"故意重复列名"的边界场景。
- 客户端其他用 IDataAccess 的工具（跨库复制、对比表结构等）都走 `ProxyHelper.ExecuteQueryToDataTableAsync`，自然有 HashSet 去重。

## 修复

### `SqlQueryTabPage.ExecuteViaDataAccessAsync` 第 537 行加去重

```csharp
var dt = new DataTable();
// ★ 重复列名处理（与直连模式 + ProxyHelper 对齐）：
//   SSMS 允许 `SELECT BILLNO, *` 展开后多列同名（HTTP 服务端用 List<ColumnInfo>
//   装结果不报错，原样把两个 "BILLNO" 序列化回客户端），但 DataTable.Columns.Add
//   不允许重名——会抛 "A column named 'BILLNO' already belongs to this DataTable"
//   陛下 2026-07-17 Http 模式反馈。对齐 SSMS/直连 ProxyHelper 行为：重复名加 _2/_3 后缀。
var usedColNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var col in table.Columns)
{
    var name = col.Name;
    if (!usedColNames.Add(name))
    {
        int suffix = 2;
        string unique;
        do { unique = $"{name}_{suffix++}"; } while (!usedColNames.Add(unique));
        name = unique;
    }
    dt.Columns.Add(name, Type.GetType(col.TypeName) ?? typeof(object));
}
```

> 算法照抄 `ProxyHelper.ExecuteQueryToDataTableAsync` 的 HashSet + _2/_3 后缀模式，三条路径行为完全一致。

## 改动文件

| 文件 | 改动 |
|------|------|
| `A3Tools.Plugins.Default/Forms/SqlQueryTabPage.cs` | `ExecuteViaDataAccessAsync` 构造 dt 时加 HashSet 去重（11 行新增） |

**服务端 A3ToolsHub 不需要改** —— 服务端用 `List<ColumnInfo>`，重复列名天然不报错，原样发回客户端由客户端去重（更合理：服务端不该擅改用户 SQL 的列名）。

## 编译

`dotnet build A3Tools.sln -c Debug` → **0 错 333 warning**（全是历史 warning，无新增）

## 待陛下回归

- [ ] Http 模式执行 `SELECT BILLNO, * FROM S_SCM_SEORDER` → 列变成 `BILLNO, BILLNO_2, ...`（不再抛 DuplicateNameException）
- [ ] 直连模式同样的 SQL → 行为不变（之前就有 try/catch 去重）
- [ ] Http 模式执行 `SELECT * FROM T`（无重复列名） → 列名原样，无 _2/_3 后缀
- [ ] 多结果集批处理 `SELECT 1 AS A; SELECT 1 AS A` → 两个结果集各自去重，互不影响

## 顺手发现但**未改**

`A3Tools.Common/DataAccess/DirectDataAccess.cs:297-298` 的 `BulkCopyAsync` 也是裸 `dt.Columns.Add(col.Name, ...)` 无去重。但 BulkCopy 场景下源 ResultTable 通常来自 `SELECT {columns} FROM {table}`（列天然不重），陛下没踩到就先不动，避免扩大改动面。如果要修，加 try/catch DuplicateNameException 即可。

## 经验

- **加新路径时记得对齐已有兜底**：2026-07-09 加 ExecuteViaDataAccessAsync 时复制了 dt 构造逻辑，但漏复制去重。教训：dt 构造这种"已知易踩坑"操作应该封装成工具方法（比如 `BuildDataTableFromResultTable(table, dedup: true)`），多路径共用，避免遗漏。
- **重复列名是 SSMS 允许、DataTable 不允许的边界**：任何把 `IEnumerable<string>` 灌进 `dt.Columns` 的地方都得主动去重，不能依赖 try/catch（Http 模式 List 不可控，服务端 List 不会替你报错）。