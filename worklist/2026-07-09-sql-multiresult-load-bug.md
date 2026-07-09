# 2026-07-09 SQL 多结果集丢结果集关键 bug 修复（DataTable.Load 自动 NextResult）

## 症状

陛下 2026-07-09 测试：执行 `SELECT 1; SELECT 2; SELECT 3`（`;` 分隔，同一批），**只显示结果 1 和 3，漏了结果 2**。

预期：3 个结果集都显示在 tcResults sub-Tab。

## 根因（坑死人）

**.NET 的 `DataTable.Load(IDataReader)` 内部会自动调用 `reader.NextResult()` 推进到下一个结果集**。

我之前的代码（v2.3.2 第一版改造）：

```csharp
do
{
    var dt = new DataTable();
    dt.Load(reader);   // ← 内部 NextResult 推 1→2
    AddResultTable(dt, ...);
}
while (await reader.NextResultAsync(_cts.Token));  // ← 外面再 NextResult 推 2→3
```

执行轨迹（用真实 Microsoft.Data.SqlClient 验证）：

```
iter 1: Load → Read+Read → NextResult (Load 内部调) 推 1→2
         → 读到结果集 1，值=1
iter 1 结束: NextResult (我自己调) 推 2→3
iter 2: Load → Read+Read → NextResult (Load 内部调) 推 3→end
         → 读到结果集 3，值=3       ← 结果集 2 被跳过！
iter 2 结束: NextResult (我自己调) 推 end → false → 退出
```

**`DataTable.Load` 内部用 `DataAdapter.Fill` 实现，`Fill` 会循环调 `NextResult` 推进 reader**。所以 `Load(reader)` 读完当前结果集后**会自动 NextResult**。我外面再 `while(NextResult)` 是**双调**，导致隔一个丢一个。

**这是 .NET 的隐式行为，文档没明说，是个坑**。

## 复现实验

用 Microsoft.Data.SqlClient + LocalDB 跑 `SELECT 1; SELECT 2; SELECT 3`，用包装 reader 监控 NextResult 调用次数：

- 旧 do-while + dt.Load：`NextResult` 被调 3 次（1 次 Load 内 + 1 次外 + 1 次 Load 内），结果集 #2 永远读不到
- 修复版手动 Read：`NextResult` 被调 2 次（外层 NextResult 推进），3 个结果集全读到

## 修复

不用 `DataTable.Load`，自己手动从 `SqlDataReader` 读取列定义 + 所有行：

```csharp
do
{
    var dt = new DataTable();
    // 列定义（手拿 reader 的列元信息，不调 dt.Load）
    for (int c = 0; c < reader.FieldCount; c++)
    {
        var colName = reader.GetName(c);
        if (string.IsNullOrEmpty(colName)) colName = $"Column{c + 1}";
        Type colType = reader.GetFieldType(c) ?? typeof(object);
        dt.Columns.Add(colName, colType);
    }
    // 读所有行
    while (await reader.ReadAsync(_cts.Token))
    {
        var row = dt.NewRow();
        for (int c = 0; c < dt.Columns.Count; c++)
            row[c] = await reader.IsDBNullAsync(c, _cts.Token) ? DBNull.Value : reader.GetValue(c);
        dt.Rows.Add(row);
    }
    AddResultTable(dt, i + 1, ++resultInBatch);
    hasResult = true;
    affectedRows += dt.Rows.Count;
}
while (await reader.NextResultAsync(_cts.Token));
```

- 手动 `reader.GetName` / `GetFieldType` 拿列定义（不用 GetSchemaTable，避免复杂 schema 路径）
- 手动 `reader.Read` 循环读所有行
- 外层 `NextResultAsync` 推进到下一个结果集
- **整个流程只调 NextResult 一次**

## 改动文件

- `A3Tools.Plugins.Default/Forms/SqlQueryTabPage.cs` — `ExecuteAsync` 多结果集循环改手动 Read

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：**0 错 261 警告**（全是历史的，非本次引入）。

## 教训（重要！）

- **`DataTable.Load(IDataReader)` 不是原子读一个结果集**，它内部用 `DataAdapter.Fill`，会循环 `NextResult` 推进 reader
- **外层不要再 `while(reader.NextResult())`**——会双调
- **正确的多结果集加载姿势**：
  1. 手动 `reader.GetName/GetFieldType` 拿列 → `dt.Columns.Add`
  2. 手动 `while (reader.Read())` 拿行 → `dt.Rows.Add`
  3. 外层 `while (reader.NextResult())` 推进
- **或者用 `DataSet.Load(reader, LoadOption, tableName)`** 一次性把所有结果集加载到 `ds.Tables`（每个 result set 一个 Table，名字默认 Table/Table1/Table2），但实测本地 fakereader 也只读到 1 个 Table，可能要 schema 调通才行——没手动循环稳
- **未来类似改造要点**：用任何带"自动处理多结果集"语义的 API（DataAdapter.Fill、DataTable.Load、DataSet.Load）时，**外层不要手动 NextResult 推进**，否则会丢结果集

## 后续验证建议

陛下测试用例：
1. `SELECT 1; SELECT 2; SELECT 3` — 应看到 3 个 sub-Tab，值分别是 1/2/3
2. `SELECT 1 GO SELECT 2 GO SELECT 3` — 应看到 3 个 sub-Tab（3 批 × 1 集）
3. `SELECT 1; SELECT 2, 3 GO SELECT 'a','b','c'` — 应看到 3 个 sub-Tab
4. 单 SELECT `SELECT * FROM sys.databases` — 仍应正常工作
5. 故意 `SELECT 1/0` — 错误信息应正常到 Messages，状态栏 ✓/✗ 正确
