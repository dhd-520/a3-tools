# 2026-07-13 对象资源管理器双击表加载 CREATE TABLE 脚本失败

## 现象

A3Tools SQL 查询工具内置对象资源管理器，筛选表双击后提示：

> -- 加载 U.dbo.S_SCM_OLACTIVITY 失败：脚本为空。

直连和 HTTP 代理模式都报。

## 根因

调用链：

```
ObjectExplorerForm.Tree_NodeMouseDoubleClick (L359)
  └─→ _owner.OpenScript(database: "", objType: "U", objName: "dbo.S_SCM_OLACTIVITY")
        └─→ SqlQueryForm.LoadAndFillScriptAsync (L787)
              └─→ SqlScriptLoader.LoadCreateScriptAsync(connStr, "U", "dbo.S_SCM_OLACTIVITY")
```

`LoadCreateScriptAsync` 一律走 `sys.sql_modules.definition`：

```sql
SELECT m.definition, ...
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.name = @name AND (@schema IS NULL OR SCHEMA_NAME(o.schema_id) = @schema)
```

**问题：** 表（`sys.objects.type = 'U'`）在 `sys.sql_modules` 里**根本没有记录**——这个视图只存储有可执行体的对象（存储过程 / 标量函数 / 表值函数 / 视图 / 触发器 / DEFAULT 约束 / 规则等）。表走这条路 `r.ReadAsync()` 直接返回 false，方法 return null → UI 提示"脚本为空"。

直连和 HTTP 都吃了同一发子弹，症状一致。

`SqlScriptLoader.cs` 里其实早就写了 `LoadTableScriptAsync`（直连版，从 `sys.columns + sys.types` 拼 `CREATE TABLE`），但**没有任何调用点接入**——属于死代码。

## 修复

文件：`A3Tools.Plugins.Default/Forms/SqlScriptLoader.cs`

### 1. `LoadCreateScriptAsync` 加 objType=="U" 早期 return

`objType` 拆 `schema.name` 之后第一件事就判：

```csharp
if (string.Equals(objType, "U", StringComparison.OrdinalIgnoreCase))
{
    return await LoadTableScriptAsync(connStr, schemaName, pureName);
}
```

这条直达表脚本路径，避免被 `sys.sql_modules` 误判。

### 2. 重写 `LoadTableScriptAsync` 支持双模式

- **直连版**：保留 `SqlConnection`，参数化查 `sys.tables`（拿真实 schema）+ `sys.columns/sys.types`（拿列定义）
- **HTTP 版**：走 `_dataAccess.ExecuteQueryAsync`，跑同一段 `sys.columns/sys.types` SQL，依赖手工 `Replace("'", "''")` 转义（与已有 `LoadCreateScriptViaHttpAsync` 风格一致）
- **统一中间类型** `ColumnDef`（record 类型），避免直连 reader / HTTP row 转换代码在主流程里到处散落
- **统一格式生成** `GenerateTableScript`，直连和 HTTP 都进同一函数，输出格式 100% 一致

### 3. 边界处理

- 表不存在 → return null（维持原契约，调用方显示"脚本为空"是兜底）
- 调用方传入的 schema 与库里实际 schema 不一致 → return null（防御性，避免错表）
- HTTP 模式拿不到 `conn.Database` → 从 `SqlConnectionStringBuilder.InitialCatalog` 反解
- `OBJECT_ID(@fullname)` 在直连用参数化安全；HTTP 模式手工转义单引号注入

### 4. `LoadTableScriptAsync` 签名变化

- 旧：`(string connStr, string objName)`
- 新：`(string connStr, string? schemaName, string pureName)`

外部调用点为 0（`grep` 全仓只有定义本身），改签名安全。

## 验证

- `dotnet build A3Tools.Plugins.Default\A3Tools.Plugins.Default.csproj -c Debug`：✅ 0 错
- 315 warning 全是改动前就有的 nullable/async warning，与本次修复无关

## 没改的部分（保持稳定）

- `SqlQueryForm.cs` 入口 `LoadAndFillScriptAsync` 未动——它继续走 `LoadCreateScriptAsync`，新逻辑自动接管
- 存储过程 / 函数 / 视图 / 触发器加载路径未动
- `SqlObjectSchemaCache` 未动
- `IDataAccess` / `HttpDataAccess` / `DirectDataAccess` 未动

## 后续可扩展（不影响本次修复）

- [ ] 主键 / 外键 / 索引拼到 CREATE TABLE（仅列定义 → 完整 DDL）
- [ ] 默认值 / CHECK 约束
- [ ] 表注释（`MS_Description` 扩展属性）

## 测试场景（待陛下验证）

1. 打开 SQL 工具 → 切库 U → 对象资源管理器 → 表 Tab → 双击 `dbo.S_SCM_OLACTIVITY`
2. 直连模式 → 应看到完整 CREATE TABLE 脚本（USE + GO + 列定义 + GO）
3. 切到 HTTP 代理模式 → 同样双击 → 输出格式与直连保持一致
4. 试一张不存在的表 → 仍走 `LoadCreateScriptAsync` 失败兜底（"脚本为空"），不崩
