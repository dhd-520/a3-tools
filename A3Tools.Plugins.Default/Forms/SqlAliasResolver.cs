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
            foreach (Match m in regexObj.Matches(sqlText))
            {
                var (schema, name) = SplitObj(m.Groups["obj"].Value);
                var alias = StripBrackets(m.Groups["alias"].Value);
                map[alias] = new AliasedObject(schema, name);
            }

            // TVF 列定义形式只补充 alias，没匹配上的再补一次（避免覆盖）
            foreach (Match m in regexTvfCols.Matches(sqlText))
            {
                var alias = StripBrackets(m.Groups["alias"].Value);
                if (!map.ContainsKey(alias))
                {
                    var (schema, name) = SplitObj(m.Groups["obj"].Value);
                    map[alias] = new AliasedObject(schema, name);
                }
            }

            // 逗号分隔的多表：FROM A a, B b  → b 关联到 B
            foreach (Match m in regexComma.Matches(sqlText))
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
            foreach (Match m in regexObjNoAlias.Matches(sqlText))
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
