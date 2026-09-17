using System.Text;
using System.Text.RegularExpressions;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL 一键格式化（简化版，2026-09-16 陛下需求）
/// <para>行为：</para>
/// <list type="bullet">
///   <item>关键字统一大写（SELECT / FROM / WHERE / JOIN / AND / OR ...）</item>
///   <item>主要子句（SELECT / FROM / WHERE / GROUP BY / ORDER BY / JOIN / UNION ...）独占一行</item>
///   <item>顶层逗号后换行 + 2 空格缩进；括号内逗号保持内联（保护 INSERT INTO t (a,b,c) 这种列名列表）</item>
///   <item>字符串字面量 / 行注释 / 块注释 / 带引号标识符 / 方括号标识符 保护起来，绝不动</item>
/// </list>
/// <para>局限：不做缩进级别跟踪、不对齐 AS 别名、不处理 ; 分隔的多语句对齐。够用为上。</para>
/// </summary>
public static class SqlFormatter
{
    // 主要子句（顺序关键：长短语在前，regex alternation 取首个匹配 → UNION ALL 不会先匹配成 UNION）
    private static readonly string[] _majorClauses = {
        // 多词
        "UNION ALL",
        "INSERT INTO", "DELETE FROM",
        "LEFT OUTER JOIN", "RIGHT OUTER JOIN", "FULL OUTER JOIN",
        "LEFT JOIN", "RIGHT JOIN", "INNER JOIN", "FULL JOIN", "CROSS JOIN",
        "GROUP BY", "ORDER BY", "PARTITION BY",
        "FETCH FIRST", "FETCH NEXT", "ROWS ONLY",
        // 单词（必须在多词之后）
        "SELECT", "FROM", "WHERE", "HAVING",
        "UNION", "EXCEPT", "INTERSECT",
        "LIMIT", "OFFSET",
        "INSERT", "UPDATE", "SET", "VALUES", "RETURNING",
        "JOIN", "WITH",
    };

    // 次要关键字（大写内联，不换行）
    private static readonly string[] _minorKeywords = {
        "AND", "OR", "ON", "AS", "IN", "IS", "NOT", "NULL", "LIKE", "BETWEEN",
        "EXISTS", "ANY", "ALL", "CASE", "WHEN", "THEN", "ELSE", "END",
        "ASC", "DESC", "DISTINCT", "TOP", "TRUE", "FALSE",
    };

    /// <summary>主入口</summary>
    public static string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return sql;

        // 1. 保护：注释 / 字符串字面量 / 带引号标识符（绝不动里面的内容）
        var stash = new List<string>();
        string StashIt(Match m)
        {
            stash.Add(m.Value);
            return $"§§SQLFMT_STASH_{stash.Count - 1}§§";
        }

        sql = Regex.Replace(sql, @"--[^\r\n]*", StashIt);                 // 行注释 -- ...
        sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", StashIt);             // 块注释 /* ... */
        sql = Regex.Replace(sql, @"N?'(?:''|[^'])*'", StashIt);           // 字符串 '...'（含 N'' 前缀）
        sql = Regex.Replace(sql, @"""(?:""""|[^""])*""", StashIt);        // 带引号标识符 "..."
        sql = Regex.Replace(sql, @"\[(?:\]|[^\]])*\]", StashIt);          // 方括号标识符 [...]

        // 2. 折叠空白（多空格/制表/换行 → 单空格）
        sql = Regex.Replace(sql, @"\s+", " ").Trim();

        // 3. 主要子句 → 大写 + 前置换行（紧跟 ( / , / . 的不算换行起点）
        var majorPattern = string.Join("|", _majorClauses.Select(Regex.Escape));
        sql = Regex.Replace(
            sql,
            $@"\s*({majorPattern})\b",
            m =>
            {
                string upper = m.Groups[1].Value.ToUpperInvariant();
                int idx = m.Index;
                // 往前跳过空白，看真正的前一个字符
                int i = idx - 1;
                while (i >= 0 && sql[i] == ' ') i--;
                if (i < 0) return upper + " ";                              // 字符串开头
                char prev = sql[i];
                if (prev == '(' || prev == ',' || prev == '.') return upper + " ";  // 函数调用 / 链式 / schema.table
                return "\n" + upper + " ";
            },
            RegexOptions.IgnoreCase);

        // 4. 次要关键字 → 大写内联
        var minorPattern = @"\b(" + string.Join("|", _minorKeywords) + @")\b";
        sql = Regex.Replace(sql, minorPattern, m => m.Value.ToUpperInvariant(), RegexOptions.IgnoreCase);

        // 5. 顶层逗号 → 换行 + 2 空格（括号内逗号保持内联，保护 (a, b, c) 这种列名 / IN (...) 这种列表）
        var sb = new StringBuilder(sql.Length + 64);
        int depth = 0;
        for (int j = 0; j < sql.Length; j++)
        {
            char c = sql[j];
            if (c == '(') { depth++; sb.Append(c); }
            else if (c == ')') { depth--; sb.Append(c); }
            else if (c == ',' && depth == 0)
            {
                sb.Append(",\n  ");
                // 吞掉逗号后已有的一个空格（前面 step 3 给所有 keyword 都加了尾随空格）
                while (j + 1 < sql.Length && sql[j + 1] == ' ') j++;
            }
            else
            {
                sb.Append(c);
            }
        }
        sql = sb.ToString();

        // 6. 还原保护的内容
        for (int k = 0; k < stash.Count; k++)
        {
            sql = sql.Replace($"§§SQLFMT_STASH_{k}§§", stash[k]);
        }

        // 7. 清理（多余空行 / 行尾空格）
        sql = Regex.Replace(sql, @"[ \t]+\n", "\n");
        sql = Regex.Replace(sql, @"\n{3,}", "\n\n");
        return sql.TrimEnd();
    }
}
