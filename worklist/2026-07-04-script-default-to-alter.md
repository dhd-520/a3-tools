# 2026-07-04 脚本加载改为 SSMS 默认 "Alter To" 模式

## 陛下反馈

> "改成 ALTER 也报错，提示列名不存在。就是不能像 SSMS 直接修改存储过程"
> "除了表结构是 CREATE，其他对象都是 ALTER"

## 设计目标

**双击 = 修改对象**（SSMS "右键 → 修改" 行为），不是"首次建"。

| 对象类型 | 加载关键字 | F5 行为 |
|---------|-----------|--------|
| 表 (U) | `CREATE TABLE` | 重新建表结构（特殊，独立路径 `LoadTableScriptAsync`）|
| 存储过程 (P) | `ALTER PROCEDURE` | 修改过程（保留 GRANT/依赖/扩展属性）|
| 函数 (FN/TF/IF) | `ALTER FUNCTION` | 修改函数 |
| 视图 (V) | `ALTER VIEW` | 修改视图 |
| 触发器 (TR) | `ALTER TRIGGER` | 修改触发器 |

## 实现

`SqlScriptLoader.LoadCreateScriptAsync`：

```csharp
// 强制把 CREATE 替换为 ALTER（除表外）
var trimmed = definition.Trim();
if (trimmed.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
    trimmed = "ALTER " + trimmed.Substring(7);
// 如果原本来就是 ALTER，保持不变

var header = $"-- 对象类型: {objType} | 架构: [{schema}]\n-- 原始数据库: [{conn.Database}]\n-- 双击加载（默认 ALTER 模式）：修改 SQL 后 F5 生效\n\n";
return $"{header}USE [{conn.Database}]\nGO\n\n{trimmed}\nGO";
```

## 为什么不生成 "IF EXISTS DROP + CREATE"

之前 9c95b76 用了 `BuildDropStatement` 生成 IF EXISTS DROP + CREATE。这错：
- DROP 会破坏对象的 GRANT EXECUTE / 依赖（sp_depends 失联）/ MS_Description
- SSMS 默认是 ALTER，不是 DROP+CREATE
- 陛下改 CREATE → ALTER 还报错"列名不存在"，正是因为 DROP/CREATE 之间事务边界不清晰

## 不改写的原则

- 不动 sys.sql_modules.definition 的 body（procedure body 内的 SQL、CREATE TABLE 内容等）
- 只改第一行的关键字 CREATE → ALTER
- **表**走独立路径（LoadTableScriptAsync）保持 CREATE

## 改动文件

- `SqlScriptLoader.cs`：删除 BuildDropStatement；CREATE → ALTER 替换

## 验证

- `dotnet build`：0 错误
- 双击存储过程 → 加载 `ALTER PROCEDURE [dbo].[sp_xxx]` → F5 成功
- 双击函数 / 视图 / 触发器 → 加载 `ALTER ...` → F5 成功
- 双击表 → 加载 `CREATE TABLE ...`（走 LoadTableScriptAsync）

## 教训

- 强制改写 sys.sql_modules.definition 是危险操作
- SSMS 默认行为是 ALTER（修改已存在对象），不是 DROP+CREATE
- 表是唯一例外：表是结构定义，CREATE 是合理默认
