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

        // 2026-08-04 陛下反馈修复: 取消同步等 10s 的 EnsureLoadedSync（会冻 UI）。
        // 原同步逻辑会为首次按 EXEC 的联想等最多 10s。
        // 新逻辑: GetSuggestions 只读缓存, 缓存未就绪返空; 触发端订阅 Loaded 事件后重弹。
        // 若触发端是 SqlEditor.TriggerIntelliSense, 它会在 IsLoaded=false 时 fire-and-forget
        // 调 EnsureLoadingAsync + 订阅 Loaded 重弹。

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
        // ★ 2026-09-24 陛下反馈修复列联想误触: 别名/表名验证 (方案 B)
        // 场景: `SELECT * FROM S_SCM_SEORDER |` (caret 后是空格)
        //   现状: DetectContext 扫到 `S_SCM_SEORDER` (非关键字) + caret 在 word 后 → 返 AfterColumnKeyword → 弹列。
        //         但 S_SCM_SEORDER 可能不是该 SQL 里已识别的表/别名,只是用户刚输完的前缀。
        //   修复: 在弹列前先验柢:
        //         1) word 是空 → 上一轮非关键字 word 在 caret 处 (原逻辑不动)
        //         2) word 非空 → 拿去 SqlAliasResolver.Parse 看是否在 aliasMap 中;不在 → 降级到对象联想。
        //   额外验证: 即使 aliasMap 没命中,也查 schema cache 看是否真存在该对象名。
        //             存在 → 可能是刚输完的对象 (例 `FROM SE|` 后用户输完想输表),仍按列逻辑弹
        //                    (符合 SSMS 行为:FROM 输完表名后下一上下文默认弹列)。
        //             不存在 → 完全没出现过,降级到对象联想弹表。
        if (ctx == SqlContextKind.AfterColumnKeyword)
        {
            if (!string.IsNullOrEmpty(fullSql))
            {
                // ★ 2026-09-24 v2 陛下反馈问题 3 仍未解决修复: 空白位置判定。
                // 根因: 之前 IsKnownTableOrAlias("S_SCM_SEORDER") 返 true (aliasMap 末段),
                //       所以未降级 → 仍弹列。
                // 真正的判定: caret 与 word 之间是否有空白/逗号/末尾 → 决定列上下文还是对象上下文。
                //   - 有空白: 用户输完表名,准备输列条件 → 弹列 (符合 SSMS 行为)
                //   - 无空白: 用户还在输 word (可能未输完,可能刚输完想输逗号或下一表)
                //             → 弹对象 (表/视图/表值函数),不弹列
                int caret = caretOffset;
                bool hasSeparatorBetweenWordAndCaret = false;
                if (caret > 0 && caret <= fullSql.Length)
                {
                    // 从 caret-1 向前扫,遇到 word 首字符就停;中间有空白/逗号则为 true
                    int scan = caret - 1;
                    while (scan >= 0)
                    {
                        char c = fullSql[scan];
                        if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#')
                            break;  // 到达 word 首字符
                        if (char.IsWhiteSpace(c) || c == ',' || c == '\t' || c == '\r' || c == '\n')
                        {
                            hasSeparatorBetweenWordAndCaret = true;
                            break;
                        }
                        // 其他符号 (. ( 等 → 不是 separator
                        scan--;
                    }
                }

                if (!hasSeparatorBetweenWordAndCaret)
                {
                    // ★ 关键场景: `SELECT * FROM S_SCM_SEORDER|` (caret 紧接 R 后,无空格)
                    // 用户表达:"我没有输入任何空格表明我的表已经输入完成了"
                    // → 这是对象上下文 (用户还在输 / 准备输逗号 / 准备输下一表),不弹列。
                    ctx = SqlContextKind.AfterObjectKeyword;
                }
                else if (!string.IsNullOrEmpty(prefix))
                {
                    // 有空白 → 才检查 word 是否真在 aliasMap/schema 中(避免 `FROM NonExistingTable ` 误弹列)
                    string lastWord = ExtractLastWordBeforeCaret(fullSql, caretOffset);
                    bool isKnown = IsKnownTableOrAlias(lastWord, fullSql, caretOffset, connectionString);
                    if (!isKnown)
                    {
                        ctx = SqlContextKind.AfterObjectKeyword;
                    }
                }
            }

            if (ctx == SqlContextKind.AfterColumnKeyword)
            {
                // ★ 2026-09-14: 透传 caretOffset, 让 SqlAliasResolver.Parse 缩到当前语句
                var cols = GetAllColumnsFromAliases(connectionString, fullSql, caretOffset, prefix);
                if (cols != null && cols.Count > 0) return cols;
                // 降级补底: aliasMap 查出 0 列也走对象路径
                if (cols == null || cols.Count == 0)
                {
                    ctx = SqlContextKind.AfterObjectKeyword;
                }
            }
        }

        // ===== -0. FROM/JOIN 后空白 → 弹对象 =====
        if (ctx == SqlContextKind.AfterObjectKeyword)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                // FROM/JOIN/UPDATE/TABLE 上下文只允许表/视图/表值函数
                // 排除存储过程 P、标量函数 FN、触发器 TR（陛下 2026-07-17 反馈）
                var fromKinds = new[] { SqlObjectSchemaCache.ObjectKind.Table, SqlObjectSchemaCache.ObjectKind.View, SqlObjectSchemaCache.ObjectKind.TableValuedFunction };
                var objs = SqlObjectSchemaCache.GetObjectSuggestions(connectionString, prefix ?? "", fromKinds);
                return objs.Take(maxResults).ToList();
            }
            return new List<string>();
        }

        // ===== 0. ★ 2026-07-15 精确列名联想（prefix 含 "." 的优先级最高） =====
        // 陛下反馈：JOIN 时输入 "表名." 或 "别名." 提示混乱（混入其他表的列 / 关键字）。
        // 之前：先走 AfterColumnKeyword 路径（拿所有别名列），miss 才走 TryGetColumnSuggestion；
        //        再 miss 就 fall through 到 关键字 + 对象，把 SELECT/WHERE 之类都涌进来。
        // 现在：prefix 含 "." 时强制走精确路径；解析不到就退到 schema 路径；都没有就返回空（保持弹窗干净）。
        //   - "a." / "A."           → 只返 a 别名的列
        //   - "Customer."           → 只返 Customer 表的列
        //   - "dbo.TableA." / "dbo.TableA.col" → 只返 dbo.TableA 的列（3 段全限定名）
        //   - "dbo."                → 退到 schema 路径，返 dbo 下所有对象
        //   - "xxx."                → 既不是表/别名，也不是 schema → 空（之前会涌关键字进来）
        if (!string.IsNullOrEmpty(prefix) && prefix.Contains('.'))
        {
            var colResult = TryGetColumnSuggestion(prefix, connectionString, fullSql);
            if (colResult != null && colResult.Columns.Count > 0)
            {
                foreach (var c in colResult.Columns)
                {
                    if (list.Count >= maxResults) break;
                    if (seen.Add(c)) list.Add(c);
                }
                return list;
            }
            // miss → 尝试 schema 路径（如 "dbo." → 返 dbo 下所有对象）
            if (!string.IsNullOrEmpty(connectionString))
            {
                // schema 前缀路径仍属 FROM-like 上下文，只取表/视图/表值函数
                var fromKinds = new[] { SqlObjectSchemaCache.ObjectKind.Table, SqlObjectSchemaCache.ObjectKind.View, SqlObjectSchemaCache.ObjectKind.TableValuedFunction };
                var objs = SqlObjectSchemaCache.GetObjectSuggestions(connectionString, prefix, fromKinds);
                if (objs.Count > 0) return objs.Take(maxResults).ToList();
            }
            // 既不是表/别名，也不是 schema → 返回空列表（保持弹窗干净）
            return new List<string>();
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
                // 普通文本路径同样只取表/视图/表值函数（避免存储过程混入 SELECT/WHERE 等位置）
                ? SqlObjectSchemaCache.GetObjectSuggestions(connectionString, prefix ?? "",
                    new[] { SqlObjectSchemaCache.ObjectKind.Table, SqlObjectSchemaCache.ObjectKind.View, SqlObjectSchemaCache.ObjectKind.TableValuedFunction })
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
    /// 尝试识别 prefix 为 "alias." / "table." / "schema.table." / "schema.table.col" 模式，找出对应列名。
    /// 返回 null 表示不是列联想场景，调用方继续走 schema 路径或返回空。
    /// </summary>
    /// <remarks>
    /// ★ 2026-07-15 增强：
    /// - 支持 3 段全限定名 "dbo.TableA." / "dbo.TableA.col" → 直接按 schema+obj 查列
    /// - 之前只支持 length=2，3 段会返回 null 导致 fall through 到关键字/对象（陛下反馈的"混乱"根源之一）
    /// - alias 优先：如果 leftPart 命中 alias map，则按 alias 解析（即便 prefix 有 schema 段）
    /// </remarks>
    private static ColumnResult? TryGetColumnSuggestion(string prefix, string? connectionString, string? fullSql)
    {
        if (string.IsNullOrEmpty(prefix) || !prefix.Contains('.')) return null;
        if (string.IsNullOrEmpty(connectionString)) return null;

        // 用 SqlAliasResolver 解析 alias -> 对象映射
        var aliasMap = string.IsNullOrEmpty(fullSql)
            ? new Dictionary<string, SqlAliasResolver.AliasedObject>(StringComparer.OrdinalIgnoreCase)
            : SqlAliasResolver.Parse(fullSql, 0);

        // 把 prefix 拆解为 [leftPart].[rightPart?]
        //   2 段：
        //     "A."        → leftPart="A",  rightPart=""
        //     "A.N"       → leftPart="A",  rightPart="N"
        //     "dbo.C."    → leftPart="dbo"（误判为对象名 → 上层 fall back 到 schema 路径处理）
        //   3 段：
        //     "dbo.C."    → schema="dbo", objName="C", columnPrefix=""
        //     "dbo.C.N"   → schema="dbo", objName="C", columnPrefix="N"
        //   4+ 段：返回 null
        var parts = prefix.Split('.');
        if (parts.Length < 2 || parts.Length > 3) return null;

        string leftPart;
        string rightPart;
        if (parts.Length == 2)
        {
            leftPart = parts[0];
            rightPart = parts[1];
        }
        else // 3
        {
            // schema.obj.col → leftPart 是 obj，rightPart 是 col 前缀
            leftPart = parts[1];
            rightPart = parts[2];
        }
        if (string.IsNullOrEmpty(leftPart)) return null;

        // 1) 先尝试把 leftPart 当别名（alias 优先）
        SqlAliasResolver.AliasedObject? target = null;
        if (aliasMap.TryGetValue(leftPart, out var aliased))
            target = aliased;

        // 2) miss → 把 leftPart 当裸对象名 / 全限定名
        if (target == null)
        {
            var (schema, name) = SqlAliasResolver.SplitObj(leftPart);
            if (string.IsNullOrEmpty(name)) return null;

            // 3 段 prefix：schema 已从 parts[0] 明确给出，不依赖 SplitObj 的推断
            if (parts.Length == 3 && !string.IsNullOrEmpty(parts[0]))
            {
                target = new SqlAliasResolver.AliasedObject(parts[0], name);
            }
            else
            {
                target = new SqlAliasResolver.AliasedObject(schema, name);
            }
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
    /// 从 SQL 当前语句的 alias 拉列名（去重），用于 SELECT/WHERE/ON 后空白场景。
    /// ★ 2026-09-14 增强 (陛下反馈 "LEFT JOIN 别名. 无提示"):
    ///   - 如果 prefix 命中 aliasMap 某个 key → **只拉该 alias 的列**，不混入其他表的列。
    ///     例: `SELECT * FROM T1 LEFT JOIN T2 b ON b` → prefix="b" → 命中 alias "b" → 只返 T2 的列。
    ///   - 如果 prefix 未命中 alias (用户输到一半 / 表名末段匹配不到) → 拉所有 alias 的列,
    ///     按 prefix startsWith 过滤 (兼容旧逻辑)。
    ///   - 同步修正 caretOffset 默认 0 的问题: 现在透传实际光标位置,
    ///     SqlAliasResolver.Parse 才能正确缩到当前语句 (避免前一句 SELECT 的 FROM 污染)。
    /// </summary>
    /// <param name="connectionString">当前账套连接串</param>
    /// <param name="fullSql">编辑器完整 SQL</param>
    /// <param name="caretOffset">光标位置 (透传给 SqlAliasResolver)</param>
    /// <param name="prefix">光标前的 token (可能是 alias 名 / 表名末段 / 列名前缀)</param>
    private static List<string>? GetAllColumnsFromAliases(string? connectionString, string? fullSql, int caretOffset, string? prefix)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(fullSql)) return null;
        var aliasMap = SqlAliasResolver.Parse(fullSql, caretOffset);
        if (aliasMap.Count == 0) return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ★ 关键修复: 如果 prefix 是某个 alias 名 (case-insensitive), 只拉该 alias 的列
        // 不再像旧逻辑那样把所有 alias 的列拼一起 startsWith(prefix), 那会导致:
        //   - alias "b" (T2) 和 alias "Body" (T3) 的 Body 列一起 startsWith "b" → 提示混乱
        //   - 用户想输 "b.Name" 时混入了 "Body", "Brand" 等
        if (!string.IsNullOrEmpty(prefix) && aliasMap.TryGetValue(prefix, out var aliased))
        {
            var aliasCols = SqlObjectSchemaCache.GetColumnSuggestions(
                connectionString, aliased.SchemaName, aliased.ObjectName, "");
            return aliasCols.Count == 0 ? null : aliasCols;
        }

        // prefix 是空 / 不是 alias → 拉所有 alias 的列 (兼容 "SELECT * FROM T1|" 这种还没输 alias 的场景)
        var all = new List<string>();
        var pre = prefix ?? "";
        foreach (var kv in aliasMap)
        {
            // ★ 2026-09-18 修复: 透传用户的 prefix 到内层, 让 GetColumnSuggestions 在 filter 之后再 Take(50)。
            // 之前传 "" 导致 GetColumnSuggestions 不过滤, 直接拿前 50 个 → TOPITEMTYPEGUID (column_id 60+)
            // 被砍掉, 弹窗里只剩 TOP 关键字。陛下实测 colsLen=2820/200+ 列, Take(50) 漏掉所有 TOP* 列。
            var cols = SqlObjectSchemaCache.GetColumnSuggestions(
                connectionString, kv.Value.SchemaName, kv.Value.ObjectName, pre);
            foreach (var c in cols) if (seen.Add(c)) all.Add(c);
        }
        // 内层已经按 prefix StartsWith 过滤+Take(50), 直接返回即可。
        return all.Count == 0 ? null : all;
    }

    /// <summary>
    /// ★ 2026-09-24 陛下反馈修复列联想误触 (方案 B 配套): 提取 caret 之前的最后一个标识符 word。
    /// 例: "SELECT * FROM S_SCM_SEORDER |" caret 在末尾 → 返 "S_SCM_SEORDER"
    ///     "SELECT * FROM T1 a|" caret 在 a 后 → 返 "a"
    ///     "SELECT * FROM T1 a|" caret 在空格前 → 返 "a"
    /// 不包括 schema. / alias. 点号,只返点号后的末段。
    /// </summary>
    private static string ExtractLastWordBeforeCaret(string fullSql, int caretOffset)
    {
        if (string.IsNullOrEmpty(fullSql) || caretOffset <= 0) return "";
        int end = caretOffset;
        // 先跳过尾部空白,定位到实际 word 末尾
        while (end > 0 && char.IsWhiteSpace(fullSql[end - 1])) end--;
        int start = end;
        while (start > 0)
        {
            char c = fullSql[start - 1];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#')
                start--;
            else
                break;
        }
        if (start >= end) return "";
        return fullSql.Substring(start, end - start);
    }

    /// <summary>
    /// ★ 2026-09-24 陛下反馈修复列联想误触 (方案 B): 验证 word 是否在该 SQL 已识别的表/别名/对象中。
    /// 用于 AfterColumnKeyword 分支前置过滤,避免 `SELECT * FROM S_SCM_SEORDER |` 误弹列。
    /// 验证优先级:
    ///   1. aliasMap.ContainsKey(word) - 已解析的 FROM/JOIN 别名 (例 `FROM T1 a` 输 "a")
    ///   2. aliasMap.Values 里 ObjectName 匹配 - 已解析的真实表名 (例 `FROM T1` 输 "T1")
    ///   3. SqlObjectSchemaCache 真实存在该对象 - 用户输完想输表 (例 `FROM SE|` 后输完才进列)
    ///   4. 都没有 → 降级到对象联想
    /// </summary>
    private static bool IsKnownTableOrAlias(string word, string fullSql, int caretOffset, string? connectionString)
    {
        if (string.IsNullOrEmpty(word)) return true;  // 空 word 不验证,沿用原逻辑
        var aliasMap = SqlAliasResolver.Parse(fullSql, caretOffset);
        // 1. 别名直命中
        if (aliasMap.ContainsKey(word)) return true;
        // 2. ObjectName 匹配 (从裸对象名入 aliasMap 的情况)
        foreach (var kv in aliasMap)
        {
            if (string.Equals(kv.Value.ObjectName, word, StringComparison.OrdinalIgnoreCase))
                return true;
            // key 是 schema.object 的形式也匹配末段
            if (kv.Key.Equals(word, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        // 3. schema cache 真实存在 (陛下 `FROM S_SCM_SEORDER|` 输完后想输表,但表名在该账套里)
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                var suggestions = SqlObjectSchemaCache.GetObjectSuggestions(
                    connectionString, word,
                    new[] { SqlObjectSchemaCache.ObjectKind.Table,
                            SqlObjectSchemaCache.ObjectKind.View,
                            SqlObjectSchemaCache.ObjectKind.TableValuedFunction });
                if (suggestions != null && suggestions.Count > 0)
                {
                    // 精确命中 word 或 word 是 schema.object 的末段
                    foreach (var s in suggestions)
                    {
                        var lastDot = s.LastIndexOf('.');
                        var namePart = lastDot >= 0 ? s.Substring(lastDot + 1) : s;
                        if (string.Equals(namePart, word, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { /* cache 未加载返空 → 走降级路径 */ }
        }
        return false;
    }
}
