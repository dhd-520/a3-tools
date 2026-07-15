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
/// 识别 5 种语法：
///   FROM table [AS] alias
///   FROM table alias(col1, col2)        -- 表值函数列别名（罕见，识别 alias 但忽略列映射）
///   FROM schema.table [AS] alias
///   FROM [schema].[table] [AS] alias
///   JOIN table [AS] alias
///   [INNER | LEFT | RIGHT | FULL | CROSS] [OUTER] JOIN ...
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
    /// 返回 dict 的 key 是不带方括号的"裸 alias"（A / order / c）。
    /// value 是 (schemaName 或 null, objectName)。
    ///
    /// 调用方先查 alias 命中 → 列；miss → 把 word 当对象名查"裸对象"。
    /// </summary>
    public static Dictionary<string, AliasedObject> Parse(string sqlText, int caretOffset)
    {
        var map = new Dictionary<string, AliasedObject>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(sqlText)) return map;

        // 只在光标之前的文本里解析（用户看的是这一段）
        // 但别忘了：别名可能出现在光标之前很远的位置，所以全文本都解析
        // 性能：即使 10K SQL 也只是几次 regex.Matches（O(n)），远低于其他 IO

        // 匹配 FROM / JOIN 后面的"对象 AS alias"或"对象 alias"或"对象 alias(...)"
        // 用 (\w+|\[[^\]]+\]) 兼容 [dbo].[Customer] 写法
        //
        // 模式 A：schema.table [AS] alias
        //   (?<obj>(?:\w+|\[[^\]]+\])\.(?:\w+|\[[^\]]+\]))  -> "dbo.Customer" / "[dbo].[Customer]"
        //   (?:\s+(?:AS\s+)?)(?<alias>\w+)
        //
        // 模式 B：table [AS] alias
        //   (?<obj>\w+|\[[^\]]+\])(?:\s+(?:AS\s+)?)(?<alias>\w+)
        //
        // 不再处理子查询 / 派生表（复杂且罕见，按需扩展）

        // 合并三段正则到一个（用 (?J) 标志内置正则不灵活，直接拆开跑）
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

        // 4. 陛下反馈：SELECT BI FROM S_SCM_SEORDER → BI 应弹 S_SCM_SEORDER 的列
        // 原因：表后没别名，旧 regex 要求 FROM 后必须带 alias 才记入 aliasMap
        // 修复：FROM/JOIN 后只一个 obj、无别名 → 以表名末段为 alias 记入
        // 边界：表名后必须是 $ ; , ( ) \r \n WHERE/GROUP/ORDER/.../JOIN/...
        var regexObjNoAlias = new Regex(
            @"(?:\b(?:FROM|JOIN)\b|,)\s+" +
            @"(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?" +
            @"(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)" +
            @"\s*(?=$|;|,|\(|\)|\r|\n|\b(?:WHERE|GROUP|ORDER|HAVING|LIMIT|UNION|INTERSECT|EXCEPT|JOIN|LEFT|RIGHT|FULL|CROSS|INNER|OUTER|APPLY)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        try
        {
            // ★ 2026-07-15 剥掉注释 / 字符串内容再用 regex 匹配
            // 原因：注释 /* JOIN T1 */ 或字符串 'FROM T1' 里的 FROM / JOIN 关键字
            //       会被现有正则误识别为 alias 定义，导致 alias map 错位（比如在 ON 子句里弹错表的列）。
            // 策略：用同长度空格替换注释 / 字符串内容（保持偏移），保留方括号内容（obj 正则要识别 [dbo].[T1]）。
            var cleanSql = StripCommentsAndStrings(sqlText);

            foreach (Match m in regexObj.Matches(cleanSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                var alias = StripBrackets(m.Groups["alias"].Value);
                map[alias] = new AliasedObject(schema, name);
            }

            // TVF 列定义形式只补充 alias，没匹配上的再补一次（避免覆盖）
            foreach (Match m in regexTvfCols.Matches(cleanSql))
            {
                var alias = StripBrackets(m.Groups["alias"].Value);
                if (!map.ContainsKey(alias))
                {
                    var (schema, name) = SplitObj(m.Groups["obj"].Value);
                    map[alias] = new AliasedObject(schema, name);
                }
            }

            // 逗号分隔的多表：FROM A a, B b  → b 关联到 B
            foreach (Match m in regexComma.Matches(cleanSql))
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
            foreach (Match m in regexObjNoAlias.Matches(cleanSql))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                // alias 用表名末段
                var alias = name;
                if (!map.ContainsKey(alias))
                {
                    map[alias] = new AliasedObject(schema, name);
                }
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
