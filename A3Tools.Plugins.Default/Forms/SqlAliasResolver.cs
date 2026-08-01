using System.Text.RegularExpressions;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL 别名 / 表名 解析器（轻量级，仅 MVP 用 IntelliSense，不做完整 SQL 解析）。
///
/// 输入：编辑器完整文本 + 光标位置
/// 输出：(aliasName) -> (schemaName?, objectName) 映射
///
/// 用法：
///   "SELECT * FROM S_SCM_SEORDER A WHERE A." (光标在 A. 后)
///   -> {"A" : (null, "S_SCM_SEORDER")}
///   IntelliSense 拿到 map 后再列 A.* 列名
///
/// 识别 5 类语法：
///   FROM / JOIN table [AS] alias              (SELECT / DELETE FROM)
///   UPDATE / INSERT INTO / MERGE INTO / TRUNCATE TABLE 顶层表
///   FROM table alias(col1, col2)              (TVF 列别名)
///   FROM schema.table / [schema].[table]
///   [INNER | LEFT | RIGHT | FULL | CROSS] [OUTER] JOIN ...
///   逗号多表：FROM A a, B b, C c
///
/// 简化：
/// - 不解析子查询（subquery AS alias）的内部列；alias 自己存进 map 但 Columns 为空
/// - 同名 alias 后出现的覆盖前面的（罕见，IT 习惯就好）
/// </summary>
public static class SqlAliasResolver
{
    public record AliasedObject(string? SchemaName, string ObjectName);

    /// <summary>
    /// 解析 SQL 文本，返回 alias -> 对象映射（同时还包含"无别名"的对象，key 是对象本身的小写名）。
    ///
    /// 返回 dict 的 key 是不带方括号的"裸 alias"（A / order / c / T1）。
    /// value 是 (schemaName 或 null, objectName)。
    ///
    /// 调用方先查 alias 命中 → 列；miss → 把 word 当对象名查"裸对象"。
    ///
    /// ★ 2026-07-28 多语句隔离（陛下反馈：多行 SQL 提示列时仍用最上面那条 SQL 的表）：
    ///   - 缩到 caretOffset 所在的那条语句（按 ; 划分）跑 alias 正则。
    ///   - StripCommentsAndStrings 先把字符串/注释里的 ; 替换为同长度空格，避免误判。
    ///   - 没有 ; 的多语句块（如仅换行分隔）退化为整段——按需扩展，不是主流场景。
    ///
    /// ★ 2026-07-28 DML/DDL 顶层表识别（陛下反馈：UPDATE 表名 SET 列名 列名不全）：
    ///   - 旧 regex 只识别 FROM/JOIN，没处理 UPDATE/DELETE/INSERT/MERGE/TRUNCATE 顶层表 → aliasMap 是空。
    ///   - 新增 5 条 DML/DDL 正则以表名末段作为 alias key，与现有 FROM 无别名场景一致。
    /// </summary>
    public static Dictionary<string, AliasedObject> Parse(string sqlText, int caretOffset)
    {
        var map = new Dictionary<string, AliasedObject>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(sqlText)) return map;

        // 1. (FROM|JOIN) 后面 [schema.]obj [AS] alias
        var regexObj = new Regex(
            @"\b(?:FROM|JOIN)\b\s+" +
            @"(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s+(?:AS\s+)?" +
            @"(?<alias>\w+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 2. (FROM|JOIN) 后面 alias([col1], [col2])  -- TVF 列映射（只记 alias，不记列）
        var regexTvfCols = new Regex(
            @"\b(?:FROM|JOIN)\b\s+" +
            @"(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s+(?<alias>\w+)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 3. 逗号分隔的多表：FROM A a, B b, C c  —— aliasObj 只记第一个，逗号后面需要单独提
        var regexComma = new Regex(
            @",\s*(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s+(?:AS\s+)?" +
            @"(?<alias>\w+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 4. FROM/JOIN 后只一个 obj、无别名 → 以表名末段为 alias 记入
        // 边界：表名后必须是 $ ; , ( ) \r \n WHERE/GROUP/ORDER/.../JOIN/.../INNER/OUTER/APPLY
        var regexObjNoAlias = new Regex(
            @"(?:\b(?:FROM|JOIN)\b|,)\s+" +
            @"(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|,|\(|\)|\r|\n|\b(?:WHERE|GROUP|ORDER|HAVING|LIMIT|UNION|INTERSECT|EXCEPT|JOIN|LEFT|RIGHT|FULL|CROSS|INNER|OUTER|APPLY)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 5. ★ 2026-07-28 UPDATE [schema.]table（SQL Server 顶层 UPDATE 表不支持 alias）
        //    UPDATE T SET x=1
        //    UPDATE TOP (10) T SET x=1
        //    UPDATE dbo.T SET x=1
        var regexUpdate = new Regex(
            @"\bUPDATE\s+(?:TOP\s*\(\s*\d+\s+\)\s+)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|\)|\r|\n|\bSET\b|\bWITH\b|\bOUTPUT\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 6. DELETE FROM [schema.]table
        var regexDeleteFrom = new Regex(
            @"\bDELETE\s+FROM\s+(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|\)|\r|\n|\bWHERE\b|\bOUTPUT\b|\bSET\b|\bWITH\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 7. INSERT INTO [schema.]table
        var regexInsertInto = new Regex(
            @"\bINSERT\s+INTO\s+(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|\(|\)|\r|\n|\bDEFAULT\b|\bVALUES\b|\bSELECT\b|\bEXEC\b|\bWITH\b|\bOUTPUT\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 8. MERGE INTO [schema.]table [AS alias]
        var regexMergeInto = new Regex(
            @"\bMERGE\s+INTO\s+(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"(?:\s+(?:AS\s+)?(?<alias>\w+))?" +
            @"\s*(?=$|;|\r|\n|\bUSING\b|\bWITH\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 9. TRUNCATE TABLE [schema.]table
        var regexTruncate = new Regex(
            @"\bTRUNCATE\s+TABLE\s+(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|\)|\r|\n)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        try
        {
            // ★ 2026-07-15 剥掉注释 / 字符串内容再用 regex 匹配
            // 原因：注释 /* JOIN T1 */ 或字符串 'FROM T1' 里的 FROM / JOIN 关键字
            //       会被现有正则误识别为 alias 定义，导致 alias map 错位（比如在 ON 子句里弹错表的列）。
            // 策略：用同长度空格替换注释 / 字符串内容（保持偏移），保留方括号内容（obj 正则要识别 [dbo].[T1]）。
            var cleanSql = StripCommentsAndStrings(sqlText);

            // ★ 2026-07-28 缩到 caret 所在那条语句（按 ; 划分；注释/字符串里的 ; 已被空格掩盖）
            // 思路上：找 caret 前后最近的 ; 界定当前语句；无 ; 时退化为整段。
            int caret = Math.Max(0, Math.Min(caretOffset, cleanSql.Length));
            int stmtStart = 0;
            for (int i = caret - 1; i >= 0; i--)
            {
                if (cleanSql[i] == ';') { stmtStart = i + 1; break; }
            }
            int stmtEnd = cleanSql.Length;
            for (int i = caret; i < cleanSql.Length; i++)
            {
                if (cleanSql[i] == ';') { stmtEnd = i; break; }
            }
            while (stmtStart < stmtEnd && char.IsWhiteSpace(cleanSql[stmtStart])) stmtStart++;
            while (stmtEnd > stmtStart && char.IsWhiteSpace(cleanSql[stmtEnd - 1])) stmtEnd--;
            if (stmtEnd <= stmtStart) return map;
            // ★ 只对当前语句跑 alias 正则——避免前/后语句的表名污染
            var stmtSql = cleanSql.Substring(stmtStart, stmtEnd - stmtStart);

            // ===== 现有 4 个 FROM/JOIN 系正则：仅在 stmtSql 内匹配 =====
            foreach (Match m in regexObj.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                var alias = StripBrackets(m.Groups["alias"].Value);
                map[alias] = new AliasedObject(schema, name);
            }

            // TVF 列定义形式只补充 alias，没匹配上的再补一次（避免覆盖）
            foreach (Match m in regexTvfCols.Matches(stmtSql))
            {
                var alias = StripBrackets(m.Groups["alias"].Value);
                if (!map.ContainsKey(alias))
                {
                    var (schema, name) = SplitObj(m.Groups["obj"].Value);
                    map[alias] = new AliasedObject(schema, name);
                }
            }

            // 逗号分隔的多表：FROM A a, B b  → b 关联到 B
            foreach (Match m in regexComma.Matches(stmtSql))
            {
                var alias = StripBrackets(m.Groups["alias"].Value);
                if (!map.ContainsKey(alias))
                {
                    var (schema, name) = SplitObj(m.Groups["obj"].Value);
                    map[alias] = new AliasedObject(schema, name);
                }
            }

            // 4. FROM/JOIN 后无别名：以表名末段为 alias 记入
            // 例：SELECT * FROM S_SCM_SEORDER  → map["S_SCM_SEORDER"] = (null, S_SCM_SEORDER)
            foreach (Match m in regexObjNoAlias.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                // alias 用表名末段
                var alias = name;
                if (!map.ContainsKey(alias))
                {
                    map[alias] = new AliasedObject(schema, name);
                }
            }

            // ===== ★ 2026-07-28 新增 5 个 DML/DDL 顶层表正则（同 stmtSql 内匹配） =====
            // 顶层 DML/DDL 表没有 alias 段（除 MERGE 可选 AS alias），用表名末段作为 alias key——
            // 这样 SELECT a, b SET a=| 时，prefix="a" 走前缀匹配路径没事；
            // 无 prefix 时 GetAllColumnsFromAliases 用 "T" 作为 alias 拉出 T 的全部列。

            // 5. UPDATE
            foreach (Match m in regexUpdate.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                if (!map.ContainsKey(name)) map[name] = new AliasedObject(schema, name);
            }
            // 6. DELETE FROM
            foreach (Match m in regexDeleteFrom.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                if (!map.ContainsKey(name)) map[name] = new AliasedObject(schema, name);
            }
            // 7. INSERT INTO
            foreach (Match m in regexInsertInto.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                if (!map.ContainsKey(name)) map[name] = new AliasedObject(schema, name);
            }
            // 8. MERGE INTO [AS alias]
            foreach (Match m in regexMergeInto.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                string key;
                var aliasGrp = m.Groups["alias"];
                if (aliasGrp.Success && !string.IsNullOrEmpty(aliasGrp.Value))
                    key = StripBrackets(aliasGrp.Value);
                else
                    key = name;
                if (!map.ContainsKey(key)) map[key] = new AliasedObject(schema, name);
            }
            // 9. TRUNCATE TABLE
            foreach (Match m in regexTruncate.Matches(stmtSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                if (!map.ContainsKey(name)) map[name] = new AliasedObject(schema, name);
            }
        }
        catch
        {
            // 解析失败（极端长文本或正则失败）→ 返回空 map，让 UI 退化为对象名匹配
        }

        return map;
    }

    /// <summary>
    /// ★ 2026-07-15 同长度空格替换 SQL 中的注释 / 字符串内容（保留方括号 [xxx]，供 obj 正则继续识别）。
    /// - 块注释 /* ... */ → 替换为同长度空格
    /// - 行注释 -- ... \n → 替换为同长度空格
    /// - 字符串 '...' → 替换为同长度空格（含 SQL Server 的 '' 转义）
    /// 保持偏移 → regex 匹配位置不变。
    /// </summary>
    private static string StripCommentsAndStrings(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;
        var sb = new System.Text.StringBuilder(sql.Length);
        int i = 0;
        while (i < sql.Length)
        {
            // 块注释 /* ... */
            if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
            {
                sb.Append("  ");
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    sb.Append(sql[i] == '\t' ? '\t' : ' ');
                    i++;
                }
                if (i + 1 < sql.Length)
                {
                    sb.Append("  ");
                    i += 2;
                }
                else
                {
                    // 未闭合的块注释：把剩余也替换掉
                    while (i < sql.Length) { sb.Append(' '); i++; }
                }
                continue;
            }
            // 行注释 -- ... \n
            if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    sb.Append(' ');
                    i++;
                }
                continue;
            }
            // 字符串 '...'（含 '' 转义 → 双写跳过）
            if (sql[i] == '\'')
            {
                sb.Append(' ');
                i++;
                while (i < sql.Length && sql[i] != '\'')
                {
                    if (sql[i] == '\\' && i + 1 < sql.Length)
                    {
                        sb.Append("  ");
                        i += 2;
                    }
                    else if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        // SQL Server '' 转义
                        sb.Append("  ");
                        i += 2;
                    }
                    else
                    {
                        sb.Append(' ');
                        i++;
                    }
                }
                if (i < sql.Length)
                {
                    sb.Append(' ');
                    i++;
                }
                continue;
            }
            // 其他字符（含方括号）原样保留
            sb.Append(sql[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>把 "dbo.Customer" / "[dbo].[Customer]" 拆成 (schema 或 null, name)</summary>
    public static (string? schema, string name) SplitObj(string obj)
    {
        var clean = StripBrackets(obj);
        var parts = clean.Split('.');
        if (parts.Length == 1) return (null, parts[0]);
        if (parts.Length == 2) return (parts[0], parts[1]);
        return (parts[0], parts[^1]);  // 多于 2 段一般不会出现
    }

    /// <summary>去掉所有方括号包裹（[dbo] -> dbo）</summary>
    public static string StripBrackets(string s)
    {
        s = s.Trim();
        if (s.StartsWith("[") && s.EndsWith("]"))
            s = s.Substring(1, s.Length - 2);
        return s;
    }
}
