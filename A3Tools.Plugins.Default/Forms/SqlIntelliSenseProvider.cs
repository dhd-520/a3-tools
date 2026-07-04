using System.Collections.Generic;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL IntelliSense 数据源：
/// - 静态关键字 + 系统函数（保留，不再变）
/// - 当前库对象（表 / 视图 / 表值函数 / 标量函数）—— 由 SqlObjectSchemaCache 提供
///
/// 调用方（SqlEditor.TriggerIntelliSense）：
///   1. 提取 word = "Sel" / "dbo.Sel" / "Sales." / "Sel.Cust" 等
///   2. 调 GetSuggestions(word, currentConnStr)
///   3. UI 直接拿来弹 popup
/// </summary>
public static class SqlIntelliSenseProvider
{
    /// <summary>SQL Server 关键字 + 常用函数（MVP 静态表，按字母排序）</summary>
    public static readonly string[] AllKeywords = new[]
    {
        // SQL 关键字
        "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AUTHORIZATION", "BACKUP", "BEGIN",
        "BETWEEN", "BREAK", "BROWSE", "BULK", "BY", "CASCADE", "CASE", "CHECK", "CHECKPOINT", "CLOSE",
        "CLUSTERED", "COALESCE", "COLLATE", "COLUMN", "COMMIT", "COMPUTE", "CONSTRAINT", "CONTAINS", "CONTINUE", "CONVERT",
        "CREATE", "CROSS", "CURRENT", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "DATABASE", "DBCC",
        "DEALLOCATE", "DECLARE", "DEFAULT", "DELETE", "DENY", "DESC", "DISTINCT", "DISTRIBUTED", "DOUBLE", "DROP",
        "ELSE", "END", "ERRLVL", "ESCAPE", "EXCEPT", "EXEC", "EXECUTE", "EXISTS", "EXIT",
        "EXTERNAL", "FETCH", "FILE", "FILLFACTOR", "FOR", "FOREIGN", "FREETEXT", "FROM", "FULL", "FUNCTION",
        "GRANT", "GROUP", "HAVING", "HOLDLOCK", "IDENTITY", "IDENTITY_INSERT", "IF", "IN", "INDEX",
        "INNER", "INSERT", "INTERSECT", "INTO", "IS", "JOIN", "KEY", "KILL", "LEFT", "LIKE",
        "LOAD", "MERGE", "NATIONAL", "NOCHECK", "NONCLUSTERED", "NOT", "NULL", "NULLIF", "OF",
        "OFF", "OFFSETS", "ON", "OPEN", "OPENDATASOURCE", "OPENQUERY", "OPENROWSET", "OPENXML", "OPTION", "OR",
        "ORDER", "OUTER", "OVER", "PERCENT", "PIVOT", "PRECISION", "PRIMARY", "PRINT", "PROC",
        "PROCEDURE", "PUBLIC", "RAISERROR", "READ", "READTEXT", "RECONFIGURE", "REFERENCES", "REPLICATION", "RESTORE", "RESTRICT",
        "RETURN", "REVERT", "REVOKE", "RIGHT", "ROLLBACK", "ROWCOUNT", "ROWGUIDCOL", "RULE", "SAVE", "SCHEMA",
        "SECURITYAUDIT", "SELECT", "SEMANTICKEYPHRASETABLE", "SEMANTICSIMILARITYDETAILSTABLE", "SEMANTICSIMILARITYTABLE", "SESSION_USER", "SET", "SETUSER", "SHUTDOWN", "SOME",
        "STATISTICS", "SYSTEM_USER", "TABLE", "TABLESAMPLE", "TEXTSIZE", "THEN", "TO", "TOP", "TRAN", "TRANSACTION",
        "TRIGGER", "TRUNCATE", "TRY", "CATCH", "TSEQUAL", "UNION", "UNIQUE", "UNPIVOT", "UPDATE", "UPDATETEXT",
        "USE", "USER", "VALUES", "VARYING", "VIEW", "WAITFOR", "WHEN", "WHERE", "WHILE", "WITH",
        "WRITETEXT",
        // 常用数据类型
        "BIGINT", "BINARY", "BIT", "CHAR", "DATE", "DATETIME", "DATETIME2", "DATETIMEOFFSET", "DECIMAL", "FLOAT",
        "GEOGRAPHY", "GEOMETRY", "HIERARCHYID", "IMAGE", "INT", "MONEY", "NCHAR", "NTEXT", "NUMERIC", "NVARCHAR",
        "REAL", "SMALLDATETIME", "SMALLINT", "SMALLMONEY", "SQL_VARIANT", "TEXT", "TIME", "TINYINT", "UNIQUEIDENTIFIER", "VARBINARY",
        "VARCHAR", "XML",
        // 常用函数
        "AVG", "CHECKSUM_AGG", "COUNT", "COUNT_BIG", "GROUPING", "GROUPING_ID", "MAX", "MIN", "STDEV", "STDEVP",
        "SUM", "VAR", "VARP",
        "ABS", "ACOS", "ASIN", "ATAN", "ATN2", "CEILING", "COS", "COT", "DEGREES", "EXP",
        "FLOOR", "LOG", "LOG10", "PI", "POWER", "RADIANS", "RAND", "ROUND", "SIGN", "SIN",
        "SQRT", "SQUARE", "TAN",
        "ASCII", "CHAR", "CHARINDEX", "CONCAT", "DATALENGTH", "DIFFERENCE", "FORMAT", "LEFT", "LEN", "LOWER",
        "LTRIM", "NCHAR", "PATINDEX", "QUOTENAME", "REPLACE", "REPLICATE", "REVERSE", "RIGHT", "RTRIM", "SOUNDEX",
        "SPACE", "STR", "STRING_AGG", "STRING_ESCAPE", "STRING_SPLIT", "STUFF", "SUBSTRING", "TRANSLATE", "TRIM", "UNICODE",
        "UPPER",
        "CAST", "CONVERT", "TRY_CAST", "TRY_CONVERT", "TRY_PARSE", "PARSE",
        "DATEADD", "DATEDIFF", "DATEFROMPARTS", "DATENAME", "DATEPART", "DATETIME2FROMPARTS", "DATETIMEFROMPARTS", "DATETIMEOFFSETFROMPARTS", "DAY", "EOMONTH", "GETDATE",
        "GETUTCDATE", "ISDATE", "MONTH", "SMALLDATETIMEFROMPARTS", "SWITCHOFFSET", "SYSDATETIME", "SYSUTCDATETIME", "TIMEFROMPARTS", "TODATETIMEOFFSET", "YEAR",
        "ISNULL", "ISNUMERIC", "NULLIF", "SESSIONPROPERTY", "CONTEXT_INFO",
        "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE", "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE", "PERCENT_RANK", "CUME_DIST",
        "IIF", "CHOOSE", "GREATEST", "LEAST",
        "NEWID", "NEWSEQUENTIALID", "SCOPE_IDENTITY", "IDENT_CURRENT", "IDENTITY", "@@IDENTITY",
        "IS_JSON", "JSON_VALUE", "JSON_QUERY", "JSON_MODIFY", "OPENJSON", "FOR JSON",
        "XACT_ABORT", "XACT_STATE", "@@TRANCOUNT", "@@SPID", "@@ERROR", "@@FETCH_STATUS", "@@ROWCOUNT", "@@VERSION", "@@SERVERNAME", "@@SERVICENAME",
        "DB_NAME", "DB_ID", "SCHEMA_NAME", "SCHEMA_ID", "OBJECT_NAME", "OBJECT_ID", "SUSER_NAME", "SUSER_ID", "USER_NAME", "HOST_NAME"
    };

    /// <summary>
    /// 综合候选（关键字 + 当前库对象 + 列名/别名）。
    /// 行为：
    /// - prefix = "Sel" → ["SELECT", "SESSION_USER", ...关键字] + [...当前库的 dbo.SaleOrder, Sales.Customer ...]
    /// - prefix = "dbo.Sel" → 仅返回 dbo.Sel*
    /// - prefix = "Sales." → 仅返 Sales schema 下所有对象（名字前缀空）
    /// - prefix = "Sales.X" → 仅返 Sales.X*
    /// - prefix = "A.N" 且 A 是别名 → A.N* 列名（仅返列，去掉 "A." 前缀）
    /// - prefix = "Customer." → 该表/视图/函数的列名
    /// - prefix = "S_SCM_SEORDER.N" 走 table 列名（解析全名 schema.table.col）
    /// </summary>
    /// <param name="prefix">光标前的"单词"（含 schema. / alias. 限定）</param>
    /// <param name="connectionString">当前账套连接串；为空则只返回关键字</param>
    /// <param name="fullSql">编辑器完整 SQL（用来解析别名映射，仅在 prefix 含 . 时用到）</param>
    /// <param name="caretOffset">光标在 fullSql 中的位置（暂未特殊用，传 0 也行）</param>
    /// <param name="maxResults">最大条数</param>
    public static IEnumerable<string> GetSuggestions(
        string prefix,
        string? connectionString,
        string? fullSql = null,
        int caretOffset = 0,
        int maxResults = 80)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(maxResults);

        // ===== 0. 列名联想（仅当 prefix 含 "." 且能解析成 别名/表名 时） =====
        // 行为：
        //   "A."          → 列（且 A 在 alias map 里）
        //   "A.N"         → 列（且 A 在 alias map 里）
        //   "Customer."   → 列（Customer 是表/视图/函数名）
        //   "Customer.N"  → 列
        //   "dbo.Customer." / "dbo.Customer.N" → 列（优先按 全限定名 查对象）
        //   "Sales.X."    → 不常见，但若 Sales 是 schema → 退回到"Sales. 补全"由 section 2 处理
        ColumnResult? colResult = TryGetColumnSuggestion(prefix, connectionString, fullSql);
        if (colResult != null && colResult.Columns.Count > 0)
        {
            foreach (var c in colResult.Columns)
            {
                if (list.Count >= maxResults) break;
                if (seen.Add(c)) list.Add(c);
            }
            if (list.Count >= maxResults) return list;
            // 列联想命中 → 不再混关键字 / 对象（用户期望就是列）
            return list;
        }

        // ===== 1. 关键字 =====
        if (string.IsNullOrEmpty(prefix))
        {
            foreach (var kw in AllKeywords)
            {
                if (list.Count >= maxResults) break;
                if (seen.Add(kw)) list.Add(kw);
            }
        }
        else
        {
            foreach (var kw in AllKeywords)
            {
                if (list.Count >= maxResults) break;
                if (kw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && seen.Add(kw))
                    list.Add(kw);
            }
        }

        // ===== 2. 当前库对象（按 schema 限定） =====
        if (!string.IsNullOrEmpty(connectionString))
        {
            // 如果 word 含多个 .（如 "a.b.c"）→ 不查对象（避免脏数据）
            var schemaQualified = !string.IsNullOrEmpty(prefix) && prefix.Contains('.')
                ? prefix.Split('.').Length == 2 && !prefix.EndsWith(".")
                : true;

            var objs = schemaQualified
                ? SqlObjectSchemaCache.GetObjectSuggestions(connectionString, prefix ?? "")
                : new();

            foreach (var o in objs)
            {
                if (list.Count >= maxResults) break;
                if (seen.Add(o)) list.Add(o);
            }
        }

        return list;
    }

    private class ColumnResult
    {
        public List<string> Columns = new();
    }

    /// <summary>
    /// 尝试识别 prefix 为 "alias." / "table." / "schema.table." 模式，找出对应列名。
    /// 返回 null 表示不是列联想场景，调用方继续走"对象 / 关键字"路径。
    /// </summary>
    private static ColumnResult? TryGetColumnSuggestion(string prefix, string? connectionString, string? fullSql)
    {
        if (string.IsNullOrEmpty(prefix) || !prefix.Contains('.')) return null;
        if (string.IsNullOrEmpty(connectionString)) return null;

        // 用 SqlAliasResolver 解析 alias -> 对象映射
        var aliasMap = string.IsNullOrEmpty(fullSql)
            ? new Dictionary<string, SqlAliasResolver.AliasedObject>(StringComparer.OrdinalIgnoreCase)
            : SqlAliasResolver.Parse(fullSql, 0);

        // 把 word 拆解为 [leftPart].[rightPart?]
        // "A."        -> left="A", right=null
        // "A.N"       -> left="A", right="N"
        // "dbo.Customer." -> left="dbo.Customer", right=null
        // "dbo.Customer.N"-> left="dbo.Customer", right="N"
        // "dbo.Customer.N." -> 太深（>2 段），不是列联想
        // ".N"        -> left=null，不查列
        var parts = prefix.Split('.');
        if (parts.Length != 2) return null;
        var leftPart = parts[0];
        var rightPart = parts[1];
        if (string.IsNullOrEmpty(leftPart)) return null;

        // 1) 先尝试把 leftPart 当别名
        SqlAliasResolver.AliasedObject? target = null;
        if (aliasMap.TryGetValue(leftPart, out var aliased))
            target = aliased;

        // 2) miss → 把 leftPart 当裸对象名 / 全限定名
        if (target == null)
        {
            // 拆分 [schema, name]（支持 [dbo].[Customer] 已经过 GetCurrentWord 处理）
            var (schema, name) = SqlAliasResolver.SplitObj(leftPart);
            if (!string.IsNullOrEmpty(name))
                target = new SqlAliasResolver.AliasedObject(string.IsNullOrEmpty(schema) ? null : schema, name);
        }

        if (target == null) return null;

        var cols = SqlObjectSchemaCache.GetColumnSuggestions(
            connectionString,
            target.SchemaName,
            target.ObjectName,
            rightPart ?? "");

        if (cols.Count == 0) return null;
        return new ColumnResult { Columns = cols };
    }

    /// <summary>兼容老接口（保留测试/旧调用）</summary>
    public static IEnumerable<string> Filter(string prefix, int maxResults = 100)
        => GetSuggestions(prefix, null, null, 0, maxResults);
}
