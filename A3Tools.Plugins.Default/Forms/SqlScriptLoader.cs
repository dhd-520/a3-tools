using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 加载数据库对象的 CREATE 脚本(存储过程/函数/视图/触发器)。
/// 从 sys.sql_modules.definition 取定义,统一把 ALTER 替换为 CREATE。
/// 后期会被「复制数据库对象」等工具双击穿透调用。
/// </summary>
public static class SqlScriptLoader
{
    /// <summary>
    /// 可选的 IDataAccess 代理(Http 模式下由 SqlQueryForm 注入)。
    /// </summary>
    private static A3Tools.Common.DataAccess.IDataAccess? _dataAccess;

    /// <summary>
    /// 注入 IDataAccess(Http 模式下由 SqlQueryForm 调用)。
    /// </summary>
    public static void SetDataAccess(A3Tools.Common.DataAccess.IDataAccess? dataAccess)
    {
        _dataAccess = dataAccess;
    }

    /// <summary>当前是否走 Http 代理</summary>
    private static bool IsHttpMode => _dataAccess != null && _dataAccess.Mode == A3Tools.Common.DataAccess.DataAccessMode.Http;

    /// <summary>
    /// 加载对象的 CREATE 脚本。
    /// </summary>
    /// <param name="connStr">数据库连接串（SqlQueryForm 的当前连接串）</param>
    /// <param name="objType">对象类型描述（P=存储过程 FN=标量函数 TF=表函数 IF=内联表函数 V=视图 TR=触发器 U=表）- 仅用于日志</param>
    /// <param name="objName">对象名</param>
    /// <returns>CREATE 脚本；找不到返回 null</returns>
    /// <summary>
    /// 加载对象脚本(默认 "ALTER 到" 模式,与 SSMS 双击行为一致)。
    ///
    /// 为什么不生成 "IF EXISTS DROP + CREATE":
    ///   - DROP 会破坏对象的权限 (GRANT EXECUTE)、依赖(引用这个 proc 的 view/func 会被标记 recompile)、
    ///     扩展属性 (MS_Description)、SEED 等
    ///   - SSMS 双击存储过程默认就是 ALTER PROCEDURE,不删重建
    ///
    /// 所以默认输出是 "ALTER PROC/FN/View/Trigger"。用户修改 SQL 后 F5 直接生效。
    /// 如果对象不存在(双击了一个不存在的对象) - ALTER 会报错,符合预期。
    /// </summary>
    public static async Task<string?> LoadCreateScriptAsync(string connStr, string objType, string objName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) throw new ArgumentException("connStr 不能为空", nameof(connStr));
        if (string.IsNullOrWhiteSpace(objName)) throw new ArgumentException("objName 不能为空", nameof(objName));

        // 拆分 schema.name(如果传入带点)。SqlScriptLoader 总是按 schema, name 精确查,避免重名
        string? schemaName = null;
        string pureName = objName;
        var dotIdx = objName.LastIndexOf('.');
        if (dotIdx > 0)
        {
            schemaName = objName.Substring(0, dotIdx);
            pureName = objName.Substring(dotIdx + 1);
        }

        // ★ 表(U) 没有 sys.sql_modules 记录,必须走专门的 CREATE TABLE 拼接路径
        //   否则会查不到 definition,直接返回 null → UI 提示"脚本为空"
        //   直连和 Http 模式都覆盖,避免再次踩同一坑
        if (string.Equals(objType, "U", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadTableScriptAsync(connStr, schemaName, pureName);
        }

        // Http 代理模式
        if (IsHttpMode)
        {
            return await LoadCreateScriptViaHttpAsync(objType, objName, schemaName, pureName);
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
            return $"-- 对象 [{schema}].[{objName}] 没有可用的脚本定义(可能是加密的)";

        // 把 CREATE/ALTER 统一改成 ALTER(SSMS "Alter To" 行为)
        // - 不删对象 → 保留权限 / 依赖 / 扩展属性 / SEED 等
        // - SQL Server 支持 ALTER PROCEDURE / ALTER FUNCTION / ALTER VIEW / ALTER TRIGGER
        var trimmed = definition.Trim();
        if (trimmed.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
            trimmed = "ALTER " + trimmed.Substring(7);
        // 如果原本来就是 ALTER,保持不变

        // 顶部 USE 切到原始库;如果用户在编辑器换库不会冲突
        var header = $"-- 对象类型: {objType} | 架构: [{schema}]\n-- 原始数据库: [{conn.Database}]\n-- 双击加载(默认 ALTER 模式):修改 SQL 后 F5 生效\n\n";
        return $"{header}USE [{conn.Database}]\nGO\n\n{trimmed}\nGO";
    }

    /// <summary>
    /// 加载表结构脚本(CREATE TABLE)。
    /// 支持直连 + Http 代理两种模式;支持 schema 拆分(避免重名)。
    /// 从 sys.columns + sys.types 拼出 CREATE TABLE 语句。
    /// 后续可扩展支持主键/外键/索引。
    /// </summary>
    public static async Task<string?> LoadTableScriptAsync(string connStr, string? schemaName, string pureName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) throw new ArgumentException("connStr 不能为空", nameof(connStr));
        if (string.IsNullOrWhiteSpace(pureName)) throw new ArgumentException("pureName 不能为空", nameof(pureName));

        if (IsHttpMode)
        {
            return await LoadTableScriptViaHttpAsync(connStr, schemaName, pureName);
        }

        // ====== 直连模式 ======
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // 1) 检查表是否存在 + 拿到真实 schema(避免 [dbo].[table] 写到错误的 schema 下)
        const string checkSql = @"
SELECT SCHEMA_NAME(schema_id) FROM sys.tables WHERE name = @name";
        string? resolvedSchema;
        using (var check = new SqlCommand(checkSql, conn))
        {
            check.Parameters.AddWithValue("@name", pureName);
            var s = await check.ExecuteScalarAsync();
            if (s == null) return null;
            resolvedSchema = s.ToString();
        }
        // 调用方如果传了 schema,做精确匹配校验;未传则用库里的 schema
        if (!string.IsNullOrEmpty(schemaName)
            && !string.Equals(schemaName, resolvedSchema, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 2) 取列定义
        const string colsSql = @"
SELECT c.name AS ColumnName, tp.name AS DataType, c.max_length, c.is_nullable, c.is_identity
FROM sys.columns c
JOIN sys.types tp ON c.user_type_id = tp.user_type_id
WHERE c.object_id = OBJECT_ID(@fullname)
ORDER BY c.column_id";
        var cols = new List<ColumnDef>();
        using (var cmd = new SqlCommand(colsSql, conn))
        {
            cmd.Parameters.AddWithValue("@fullname", $"{resolvedSchema}.{pureName}");
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                cols.Add(new ColumnDef(
                    Name: r.GetString(0),
                    DataType: r.GetString(1),
                    MaxLength: r.GetInt16(2),
                    IsNullable: r.GetBoolean(3),
                    IsIdentity: r.GetBoolean(4)
                ));
            }
        }
        if (cols.Count == 0) return null;

        return GenerateTableScript(conn.Database, resolvedSchema!, pureName, cols);
    }

    /// <summary>
    /// Http 代理模式加载表脚本:走 _dataAccess.ExecuteQueryAsync 跑同一段 SQL,
    /// 与直连版保持输出格式一致,前端不感知差异。
    /// HTTP 模式需要手写 Replace 转义(ExecuteQueryAsync 不支持参数化)。
    /// </summary>
    private static async Task<string?> LoadTableScriptViaHttpAsync(string connStr, string? schemaName, string pureName)
    {
        // 1) 拿 schema
        const string checkSql = @"
SELECT SCHEMA_NAME(schema_id) FROM sys.tables WHERE name = '{name}'";
        var safeCheck = checkSql.Replace("'{name}'", $"'{pureName.Replace("'", "''")}'");
        var result = await _dataAccess!.ExecuteQueryAsync(safeCheck);
        if (!result.Success) return null;
        if (result.Tables.Count == 0 || result.Tables[0].Rows.Count == 0) return null;

        var resolvedSchema = result.Tables[0].Rows[0][0]?.ToString();
        if (string.IsNullOrEmpty(resolvedSchema)) return null;
        if (!string.IsNullOrEmpty(schemaName)
            && !string.Equals(schemaName, resolvedSchema, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 2) 拿列定义
        const string colsSql = @"
SELECT c.name AS ColumnName, tp.name AS DataType, c.max_length, c.is_nullable, c.is_identity
FROM sys.columns c
JOIN sys.types tp ON c.user_type_id = tp.user_type_id
WHERE c.object_id = OBJECT_ID('{fullname}')
ORDER BY c.column_id";
        var safeFullName = $"{resolvedSchema}.{pureName}".Replace("'", "''");
        var safeCols = colsSql.Replace("'{fullname}'", $"'{safeFullName}'");

        var colResult = await _dataAccess!.ExecuteQueryAsync(safeCols);
        if (!colResult.Success || colResult.Tables.Count == 0) return null;

        var cols = new List<ColumnDef>();
        foreach (var row in colResult.Tables[0].Rows)
        {
            if (row == null || row.Length < 5) continue;
            cols.Add(new ColumnDef(
                Name: row[0]?.ToString() ?? "",
                DataType: row[1]?.ToString() ?? "",
                MaxLength: ToInt16Safe(row[2]),
                IsNullable: ToBoolSafe(row[3]),
                IsIdentity: ToBoolSafe(row[4])
            ));
        }
        if (cols.Count == 0) return null;

        // Http 模式拿不到 conn.Database —— 从连接串解析出 Initial Catalog
        var database = "";
        try { database = new SqlConnectionStringBuilder(connStr).InitialCatalog ?? ""; } catch { /* ignore */ }
        return GenerateTableScript(database, resolvedSchema, pureName, cols);
    }

    /// <summary>
    /// 列定义(直连 reader / Http row 转换后的统一中间类型)。
    /// </summary>
    private record ColumnDef(string Name, string DataType, short MaxLength, bool IsNullable, bool IsIdentity);

    /// <summary>
    /// 把行里 object 列值转成 short(JSON 反序列后可能是 int/short/long,统一处理)
    /// </summary>
    private static short ToInt16Safe(object? v) => v switch
    {
        null => 0,
        short s => s,
        int i => (short)i,
        long l => (short)l,
        _ => Convert.ToInt16(v)
    };

    /// <summary>object 列值转 bool(JSON 里通常是 bool,直连是 bool,统一兜底)</summary>
    private static bool ToBoolSafe(object? v) => v switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        _ => Convert.ToBoolean(v)
    };

    /// <summary>
    /// 拼装 CREATE TABLE 文本(直连 + Http 共用,保证输出格式一致)。
    /// 顶部 USE 数据库 + GO;列定义带 NOT NULL / IDENTITY(1,1)。
    /// </summary>
    private static string GenerateTableScript(string database, string schemaName, string tableName, List<ColumnDef> cols)
    {
        var lines = cols.Select(c =>
        {
            var typeStr = c.DataType switch
            {
                "varchar" => c.MaxLength == -1 ? "VARCHAR(MAX)" : $"VARCHAR({c.MaxLength})",
                "nvarchar" => c.MaxLength == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({c.MaxLength / 2})",
                "char" => $"CHAR({c.MaxLength})",
                "nchar" => $"NCHAR({c.MaxLength / 2})",
                "varbinary" => c.MaxLength == -1 ? "VARBINARY(MAX)" : $"VARBINARY({c.MaxLength})",
                "decimal" or "numeric" => "DECIMAL(18,4)",
                _ => c.DataType.ToUpper()
            };
            var nullable = c.IsNullable ? "NULL" : "NOT NULL";
            var identity = c.IsIdentity ? " IDENTITY(1,1)" : "";
            return $"    [{c.Name}] {typeStr}{identity} {nullable}";
        }).ToList();

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(database))
        {
            sb.AppendLine($"USE [{database}]");
            sb.AppendLine("GO");
            sb.AppendLine();
        }
        sb.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] (");
        sb.AppendLine(string.Join(",\n", lines));
        sb.AppendLine(")");
        sb.AppendLine("GO");
        return sb.ToString();
    }

    // ============================================
    // Http 代理模式
    // ============================================

    /// <summary>
    /// Http 代理模式下加载对象脚本
    /// </summary>
    private static async Task<string?> LoadCreateScriptViaHttpAsync(string objType, string objName, string? schemaName, string pureName)
    {
        const string sql = @"
SELECT m.definition, o.type, o.type_desc, SCHEMA_NAME(o.schema_id) AS [schema]
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.name = '{pureName}'
  AND ('{schemaName}' IS NULL OR SCHEMA_NAME(o.schema_id) = '{schemaName}')";

        // 用参数化查询避免 SQL 注入
        var safeSql = sql
            .Replace("'{pureName}'", $"'{pureName.Replace("'", "''")}'")
            .Replace("'{schemaName}'", schemaName != null ? $"'{schemaName.Replace("'", "''")}'" : "NULL");

        var result = await _dataAccess!.ExecuteQueryAsync(safeSql);
        if (!result.Success || result.Tables.Count == 0 || result.Tables[0].Rows.Count == 0) return null;

        var row = result.Tables[0].Rows[0];
        var definition = row[0]?.ToString();
        var schema = row[3]?.ToString() ?? "dbo";
        if (string.IsNullOrWhiteSpace(definition))
            return $"-- 对象 [{schema}].[{objName}] 没有可用的脚本定义(可能是加密的)";

        var trimmed = definition.Trim();
        if (trimmed.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
            trimmed = "ALTER " + trimmed.Substring(7);

        var header = $"-- 对象类型: {objType} | 架构: [{schema}]\n-- 双击加载(默认 ALTER 模式):修改 SQL 后 F5 生效\n\n";
        return $"{header}{trimmed}\nGO";
    }
}