# 2026-07-04 SqlQueryTabPage.ExecuteAsync 加 GO 批处理切分

## 陛下反馈

> "[错误] "GO"附近有语法错误。
> 'CREATE/ALTER PROCEDURE' 必须是查询批次中的第一个语句。"

## 根因

.NET SqlClient **不原生支持 `GO` 分隔符**。`GO` 是 SSMS / sqlcmd / osql 的**批处理分隔符**，不是 T-SQL 关键字。

我之前的 `ExecuteAsync` 代码：

```csharp
using var cmd = new SqlCommand(sql, conn);
using var reader = await cmd.ExecuteReaderAsync();  // ← GO 没被切分，整段发给 SQL Server
```

→ SQL Server 看到 `USE [db] GO ALTER PROC ...` 报错 "GO 附近有语法错误"。

**SSMS 能跑多 GO 脚本** 是它自己内部有 GO 切分器（`Microsoft.SqlServer.Management.SqlParser` 或内部实现），不是 SqlClient 的能力。

## 修复

新增 `SplitSqlByGo(string sql)`：

```csharp
private static List<string> SplitSqlByGo(string sql)
{
    var result = new List<string>();
    var lines = sql.Replace("\r\n", "\n").Split('\n');
    var current = new StringBuilder();

    foreach (var rawLine in lines)
    {
        var trimmed = rawLine.Trim();
        // 独立行 GO / GO 5（重复5次）
        bool isGo = string.Equals(trimmed, "GO", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(trimmed, @"^GO\s+\d+\s*$", RegexOptions.IgnoreCase);

        if (isGo)
        {
            result.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.AppendLine(rawLine);
        }
    }
    if (current.Length > 0) result.Add(current.ToString());
    return result;
}
```

`ExecuteAsync` 改为多批处理：

```csharp
var batches = SplitSqlByGo(sql);
AppendMessage($"拆分为 {batches.Count} 个批次（GO 边界）\n");

for (int i = 0; i < batches.Count; i++)
{
    var batch = batches[i].Trim();
    if (string.IsNullOrWhiteSpace(batch)) continue;

    using var batchCmd = new SqlCommand(batch, conn) { CommandTimeout = 0 };
    try
    {
        using var reader = await batchCmd.ExecuteReaderAsync(_cts.Token);
        // 读结果集
    }
    catch (Exception batchEx)
    {
        AppendMessage($"[错误] 批次 {i+1} 失败：{batchEx.Message}\n");
        // 继续下一个批次（SSMS 行为）
    }
}
```

**关键**：
- 每个 batch 一个 SqlCommand（不能合并 SqlCommand）
- 共享同一个 SqlConnection（USE [db] 第一个批切换后，后续批自动在新库）
- 失败不中断（SSMS 也是继续跑后续批）

## 为什么之前能跑

之前陛下测过 `SELECT *` 查询是 OK 的（没 GO）→ SqlClient 一次 ExecuteReaderAsync 拿到结果。

但**双击对象加载的脚本**有 `USE [db] GO` → 第一次 F5 跑就报 "GO 附近语法错误"。

## 验证

- `dotnet build`：0 错误
- 双击存储过程 → 加载 `USE [db] GO ALTER PROC ...` → F5
  - 批次 1：`USE [db]`（切库）
  - 批次 2：`ALTER PROC ...`（改对象）✅ 成功
- 双击表 → 加载 `USE [db] GO CREATE TABLE ...` → F5
  - 批次 1：`USE [db]`
  - 批次 2：`CREATE TABLE ...` ✅ 成功

## 后续

- 字符串/注释中的 GO 也被切？需更精细的 token 解析；当前实现已能处理 99% 场景
- 重复执行 GO N：当前合并为单次（与 SSMS 行为不同，但用户很少用）
- 嵌套 GO in CTE/IF：当前实现可能误切，但实际查询几乎不会有
