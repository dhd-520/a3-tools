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

        // 陛下反馈：EXEC 弹不出存储过程 → 原因：缓存未就绪。
        // 修复：GetSuggestions 入口同步等缓存就绪（最多 10s）。第一次会同步等，后续直接读缓存。
        if (!string.IsNullOrEmpty(connectionString))
            SqlObjectSchemaCache.EnsureLoadedSync(connectionString);

        // ===== -2. 上下文检测：光标位置决定弹什么类型（不依赖 prefix） =====
        // 陛下反馈：“EXEC 空格后” / “SELECT * 后” 必须弹（即使 word="" 也不能关 popup）。
        var ctx = DetectContext(fullSql, caretOffset);

        // ===== -1. EXEC/EXECUTE 上下文 → 提示存储过程 =====
        if (ctx == SqlContextKind.AfterExec)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var procs = SqlObjectSchemaCache.GetObjectsByKind(connectionString,
                    new[] { SqlObjectSchemaCache.ObjectKind.StoredProcedure })
                    .Select(o => new { Schema = o.SchemaName, Name = o.Name, Full = $"{o.SchemaName}.{o.Name}" })
                    .OrderBy(p => p.Full, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var effectivePrefix = prefix ?? "";
                if (effectivePrefix.Equals("EXEC", StringComparison.OrdinalIgnoreCase) ||
                    effectivePrefix.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
                    effectivePrefix = "";

                // 陛下反馈：EXEC 弹不出 + 对象资源管理器能含 85 个 → 源数据不一致
                // 修复：完全照搬对象资源管理器的 contains 匹配逻辑（IndexOf 包含）。
                // 去除 80 条截断限制（对象资源管理器不截断），让陛下看到全部。
                // 不按 schema 点额外过滤。
                var matched = procs
                    .Where(p => string.IsNullOrEmpty(effectivePrefix)
                                || p.Name.IndexOf(effectivePrefix, StringComparison.OrdinalIgnoreCase) >= 0
                                || p.Full.IndexOf(effectivePrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(p => p.Full)
                    .ToList();
                try
                {
                    var t3 = $"  [EXEC] matched.Count={matched.Count} (first 5: {string.Join(",", matched.Take(5))})" + Environment.NewLine;
                    System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a3-intellisense.log"), t3);
                }
                catch { }
                return matched;
            }
            return new List<string>();
        }

        // ===== -0. SELECT/WHERE/ON 后空白 → 弹列 =====
        if (ctx == SqlContextKind.AfterColumnKeyword)
        {
            var cols = GetAllColumnsFromAliases(connectionString, fullSql, prefix);
            if (cols != null && cols.Count > 0) return cols;
        }

        // ===== -0. FROM/JOIN 后空白 → 弹对象 =====
        if (ctx == SqlContextKind.AfterObjectKeyword)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var objs = SqlObjectSchemaCache.GetObjectSuggestions(connectionString, prefix ?? "");
                return objs.Take(maxResults).ToList();
            }
            return new List<string>();
        }

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

    /// <summary>SQL 上下文类型——光标所在位置的语法环境，决定弹什么。</summary>
    public enum SqlContextKind
    {
        Generic,
        AfterExec,
        AfterObjectKeyword,
        AfterColumnKeyword,
    }

    /// <summary>
    /// 检测光标所在 SQL 上下文。不依赖 prefix，即使 word="" 也能检测。
    /// 从 caret 向左扫：跳过空白 → 取一个"实词区段" → 看是否是上下文关键字。
    /// 不是 → 跳过该区段 + 空白 → 看上一个区段（最多 8 轮足够）。
    /// 例：
    ///   "EXEC" caret=6             → 词=EXEC → AfterExec
    ///   "EXEC " caret=7            → 跳过空白 → 词=EXEC → AfterExec
    ///   "EXEC sp_helpdb" caret=14  → 词=sp_helpdb(非关键字) → 跳过 → 词=EXEC → AfterExec
    ///   "SELECT" caret=6            → 词=SELECT → AfterColumnKeyword
    ///   "SELECT * FROM T1" caret=18→ 词=T1(非关键字) → 跳过 → 词=FROM → AfterObjectKeyword
    ///   "SELECT * FROM T1 a" caret=19 → 词=a → 跳过 → T1 → 跳过 → FROM → AfterObjectKeyword
    /// </summary>
    public static SqlContextKind DetectContext(string? fullSql, int caretOffset)
    {
        if (string.IsNullOrEmpty(fullSql)) return SqlContextKind.Generic;
        if (caretOffset <= 0 || caretOffset > fullSql.Length) return SqlContextKind.Generic;

        int i = caretOffset;
        bool sawFromLike = false;
        bool sawSelectLike = false;
        // 陛下反馈：输完表名/别名后弹列。
        // 例：SELECT * FROM T1 |  → first non-kw word T1 紧贴 caret
        // 记录该 word，等下一轮扫到 FROM-like 时返 AfterColumnKeyword
        bool lastNonKwAtCaret = false;
        for (int round = 0; round < 8; round++)
        {
            while (i > 0)
            {
                char c = fullSql[i - 1];
                if (char.IsWhiteSpace(c) || c == '\t' || c == '\r' || c == '\n')
                    i--;
                else
                    break;
            }
            if (i <= 0) return SqlContextKind.Generic;

            int segEnd = i;
            char firstChar = fullSql[i - 1];
            // ; → 语句边界，停止
            if (firstChar == ';') return SqlContextKind.Generic;
            // , → 列表分隔符（如 FROM T1 a, T2 ），跳过看左边可能仍是 FROM/JOIN 上下文
            if (firstChar == ',') { i = segEnd - 1; continue; }
            // + - = > < ! ) → 表达式符号
            if (firstChar == '+' || firstChar == '-' || firstChar == '=' ||
                firstChar == '>' || firstChar == '<' || firstChar == '!' || firstChar == ')')
            { i = segEnd - 1; continue; }
            // * ( . → 跳过（这些不组成 word 头）
            if (firstChar == '*' || firstChar == '(' || firstChar == '.')
            { i = segEnd - 1; continue; }

            // 数字开头 → 也走字词扫（可能是 T1 这种混在表名中的数字）
            // 后续 wordStart 循环会倒推字母数字组合

            // 字母 / _ / @ / # → 扫完整词（含 . 跨 schema.name）
            int wordStart = segEnd;
            while (wordStart > 0)
            {
                char c = fullSql[wordStart - 1];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '.')
                    wordStart--;
                else
                    break;
            }
            int wordLen = segEnd - wordStart;
            if (wordLen <= 0) return SqlContextKind.Generic;
            var word = fullSql.Substring(wordStart, wordLen);

            if (word.Equals("EXEC", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
            {
                if (wordStart == 0) return SqlContextKind.AfterExec;
                char prev = fullSql[wordStart - 1];
                if (char.IsWhiteSpace(prev) || prev == '(' || prev == ';')
                    return SqlContextKind.AfterExec;
            }
            if (word.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("JOIN", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("APPLY", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("INTO", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                // 输完表名/别名后弹列：上一轮扫到的非关键字 word 是表名/别名
                if (lastNonKwAtCaret && !sawSelectLike)
                    return SqlContextKind.AfterColumnKeyword;

                // 看 caret 后面是什么：空白/末尾 → 是 FROM 上下文
                // 标识符 → FROM 后面已输表名/别名 → 跳过本关键字，继续扫
                bool caretAfterWord = (caretOffset == wordStart + wordLen);
                if (caretAfterWord && caretOffset < fullSql.Length)
                {
                    char next = fullSql[caretOffset];
                    if (char.IsLetterOrDigit(next) || next == '_' || next == '#' || next == '@')
                    {
                        // FROM 后已输表名/别名 → 跳过本关键字
                        sawFromLike = true;
                        i = wordStart;
                        continue;
                    }
                }
                if (wordStart == 0) return SqlContextKind.AfterObjectKeyword;
                char prev = fullSql[wordStart - 1];
                if (char.IsWhiteSpace(prev) || prev == '(' || prev == ';')
                    return SqlContextKind.AfterObjectKeyword;
            }
            if (word.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("HAVING", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("BY", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                if (wordStart == 0) return SqlContextKind.AfterColumnKeyword;
                char prev = fullSql[wordStart - 1];
                if (char.IsWhiteSpace(prev) || prev == '(' || prev == ';')
                    return SqlContextKind.AfterColumnKeyword;
            }

            // 补充逻辑：记录上一轮是否见过 FROM/JOIN（对象上下文）或 SELECT/WHERE（列上下文）
            // 如果本轮词不是关键字 → 是表名/别名/列名
            //   下一个词后面应该是列（"FROM T1 后输 a 别名后"）
            // 但仅限最后一次
            sawFromLike = sawFromLike || IsFromLike(word);
            sawSelectLike = sawSelectLike || IsSelectLike(word);

            // 陛下反馈：输完表名/别名后默认弹列名。
            // 例：SELECT * FROM T1 |  → caret 紧接 T1 末尾（后面是空白/文档末尾）
            //      SELECT * FROM T1 a| → caret 紧接 a 末尾
            //      SELECT * FROM T1 a, T2| → caret 紧接 T2 末尾
            // 此时该 word 是表名/别名 → 弹该 word 的列。
            if (sawFromLike && !sawSelectLike)
            {
                bool caretAtWordEnd = (caretOffset == wordStart + wordLen);
                bool caretAtDocEnd = (caretOffset == fullSql.Length);
                if (caretAtWordEnd && (caretAtDocEnd ||
                    char.IsWhiteSpace(fullSql[caretOffset]) ||
                    fullSql[caretOffset] == ','))
                {
                    return SqlContextKind.AfterColumnKeyword;
                }
            }

            // 关键补丁：如果该 word 不是关键字且 caret 紧接 word 末尾
            // （即 word 是 caret 处最近一个已输完的标识符），
            // 且 word 左侧（跳过空白后）有 FROM-like 关键字（未来 round 才会看到）
            // → 不管后面如何，都当 AfterColumnKeyword
            if (IsFromLike(word) || IsSelectLike(word))
            {
                // 是关键字，按上面分支处理
            }
            else
            {
                // 不是关键字 → 是表名/别名/列名
                // caret 紧接 word 末尾？
                bool caretAtWordEnd = (caretOffset == wordStart + wordLen);
                if (caretAtWordEnd)
                {
                    // 看 word 后面（caret 处）是什么
                    if (caretOffset == fullSql.Length ||
                        char.IsWhiteSpace(fullSql[caretOffset]) ||
                        fullSql[caretOffset] == ',')
                    {
                        // caret 在 word 后面（空白/末尾/逗号）→ word 是已输完的标识符
                        // 标记：等下一轮找到 FROM-like 后返 AfterColumnKeyword
                        lastNonKwAtCaret = true;
                    }
                }
            }

            // 不是关键字 → 跳过该词
            i = wordStart;
        }
        // 8 轮后还没有命中 → 看是否走过 FROM → 列上下文（输完表名/别名后默认弹列名）
        if (sawFromLike && !sawSelectLike) return SqlContextKind.AfterColumnKeyword;
        return SqlContextKind.Generic;
    }

    private static bool IsFromLike(string w) =>
        w.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("JOIN", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("APPLY", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("INTO", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("TABLE", StringComparison.OrdinalIgnoreCase);

    private static bool IsSelectLike(string w) =>
        w.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("HAVING", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("BY", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("OR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 从 SQL 所有 FROM 别名拉列名（去重），用于 SELECT/WHERE/ON 后空白场景。
    /// 如果 prefix 非空 → 前缀过滤。
    /// </summary>
    private static List<string>? GetAllColumnsFromAliases(string? connectionString, string? fullSql, string? prefix)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(fullSql)) return null;
        var aliasMap = SqlAliasResolver.Parse(fullSql, 0);
        if (aliasMap.Count == 0) return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<string>();
        foreach (var kv in aliasMap)
        {
            var cols = SqlObjectSchemaCache.GetColumnSuggestions(connectionString, kv.Value.SchemaName, kv.Value.ObjectName, "");
            foreach (var c in cols) if (seen.Add(c)) all.Add(c);
        }
        if (all.Count == 0) return null;
        var pre = prefix ?? "";
        var matched = string.IsNullOrEmpty(pre)
            ? all
            : all.Where(c => c.StartsWith(pre, StringComparison.OrdinalIgnoreCase)).ToList();
        return matched.Count == 0 ? null : matched;
    }
}
