using System.Collections.Generic;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL IntelliSense 数据源：关键字 + 系统函数（MVP 静态表）。
/// 后续可扩展：异步加载表/视图/列。
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
        "DUMP", "ELSE", "END", "ERRLVL", "ESCAPE", "EXCEPT", "EXEC", "EXECUTE", "EXISTS", "EXIT",
        "EXTERNAL", "FETCH", "FILE", "FILLFACTOR", "FOR", "FOREIGN", "FREETEXT", "FROM", "FULL", "FUNCTION",
        "GOTO", "GRANT", "GROUP", "HAVING", "HOLDLOCK", "IDENTITY", "IDENTITY_INSERT", "IF", "IN", "INDEX",
        "INNER", "INSERT", "INTERSECT", "INTO", "IS", "JOIN", "KEY", "KILL", "LEFT", "LIKE",
        "LINENO", "LOAD", "MERGE", "NATIONAL", "NOCHECK", "NONCLUSTERED", "NOT", "NULL", "NULLIF", "OF",
        "OFF", "OFFSETS", "ON", "OPEN", "OPENDATASOURCE", "OPENQUERY", "OPENROWSET", "OPENXML", "OPTION", "OR",
        "ORDER", "OUTER", "OVER", "PERCENT", "PIVOT", "PLAN", "PRECISION", "PRIMARY", "PRINT", "PROC",
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

    /// <summary>大小写不敏感的前缀匹配（用于过滤）</summary>
    public static IEnumerable<string> Filter(string prefix, int maxResults = 100)
    {
        if (string.IsNullOrEmpty(prefix))
            return AllKeywords.Take(maxResults);
        return AllKeywords
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults);
    }
}