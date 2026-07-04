using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 加载数据库对象的 CREATE 脚本（存储过程/函数/视图/触发器）。
/// 从 sys.sql_modules.definition 取定义，统一把 ALTER 替换为 CREATE。
/// 后期会被「复制数据库对象」等工具双击穿透调用。
/// </summary>
public static class SqlScriptLoader
{
    /// <summary>
    /// 加载对象的 CREATE 脚本。
    /// </summary>
    /// <param name="connStr">数据库连接串（SqlQueryForm 的当前连接串）</param>
    /// <param name="objType">对象类型描述（P=存储过程 FN=标量函数 TF=表函数 IF=内联表函数 V=视图 TR=触发器 U=表）— 仅用于日志</param>
    /// <param name="objName">对象名</param>
    /// <returns>CREATE 脚本；找不到返回 null</returns>
    public static async Task<string?> LoadCreateScriptAsync(string connStr, string objType, string objName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) throw new ArgumentException("connStr 不能为空", nameof(connStr));
        if (string.IsNullOrWhiteSpace(objName)) throw new ArgumentException("objName 不能为空", nameof(objName));

        // 拆分 schema.name（如果传入带点）。SqlScriptLoader 总是按 schema, name 精确查，避免重名
        string? schemaName = null;
        string pureName = objName;
        var dotIdx = objName.LastIndexOf('.');
        if (dotIdx > 0)
        {
            schemaName = objName.Substring(0, dotIdx);
            pureName = objName.Substring(dotIdx + 1);
        }

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        const string sql = @"
SELECT m.definition, o.type, o.type_desc, SCHEMA_NAME(o.schema_id) AS [schema]
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.name = @name
  AND (@schema IS NULL OR SCHEMA_NAME(o.schema_id) = @schema)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", pureName);
        cmd.Parameters.AddWithValue("@schema", (object?)schemaName ?? DBNull.Value);

        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var definition = r.IsDBNull(0) ? null : r.GetString(0);
        var schema = r.GetString(3);
        if (string.IsNullOrWhiteSpace(definition))
            return $"-- 对象 [{schema}].[{objName}] 没有可用的脚本定义（可能是加密的）";

        // 把开头的 ALTER 替换为 CREATE
        var trimmed = definition.Trim();
        if (trimmed.StartsWith("ALTER ", StringComparison.OrdinalIgnoreCase))
            trimmed = "CREATE " + trimmed.Substring(6);

        var header = $"-- 对象类型: {objType} | 架构: [{schema}]\n-- 原始数据库: [{conn.Database}]\n\n";
        return $"{header}USE [{conn.Database}]\nGO\n\n{trimmed}\nGO";
    }

    /// <summary>
    /// 加载表结构脚本（CREATE TABLE）。
    /// 简化版：从 sys.columns + sys.types 拼出 CREATE TABLE 语句。
    /// 后续可扩展支持主键/外键/索引。
    /// </summary>
    public static async Task<string?> LoadTableScriptAsync(string connStr, string objName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) throw new ArgumentException("connStr 不能为空", nameof(connStr));
        if (string.IsNullOrWhiteSpace(objName)) throw new ArgumentException("objName 不能为空", nameof(objName));

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // 检查表是否存在
        const string checkSql = @"
SELECT SCHEMA_NAME(schema_id) FROM sys.tables WHERE name = @name";
        using (var check = new SqlCommand(checkSql, conn))
        {
            check.Parameters.AddWithValue("@name", objName);
            var schema = await check.ExecuteScalarAsync();
            if (schema == null) return null;
        }

        const string sql = @"
SELECT c.name AS ColumnName, tp.name AS DataType, c.max_length, c.is_nullable, c.is_identity
FROM sys.columns c
JOIN sys.types tp ON c.user_type_id = tp.user_type_id
WHERE c.object_id = OBJECT_ID(@name)
ORDER BY c.column_id";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", objName);

        var lines = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var col = r.GetString(0);
            var type = r.GetString(1);
            var maxLen = r.GetInt16(2);
            var isNullable = r.GetBoolean(3);
            var isIdentity = r.GetBoolean(4);

            string typeStr = type switch
            {
                "varchar" => maxLen == -1 ? "VARCHAR(MAX)" : $"VARCHAR({maxLen})",
                "nvarchar" => maxLen == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({maxLen / 2})",
                "char" => $"CHAR({maxLen})",
                "nchar" => $"NCHAR({maxLen / 2})",
                "varbinary" => maxLen == -1 ? "VARBINARY(MAX)" : $"VARBINARY({maxLen})",
                "decimal" or "numeric" => $"DECIMAL(18,4)",
                _ => type.ToUpper()
            };

            var nullable = isNullable ? "NULL" : "NOT NULL";
            var identity = isIdentity ? " IDENTITY(1,1)" : "";
            lines.Add($"    [{col}] {typeStr}{identity} {nullable}");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"USE [{conn.Database}]");
        sb.AppendLine("GO");
        sb.AppendLine();
        sb.AppendLine($"CREATE TABLE [dbo].[{objName}] (");
        sb.AppendLine(string.Join(",\n", lines));
        sb.AppendLine(")");
        sb.AppendLine("GO");
        return sb.ToString();
    }
}