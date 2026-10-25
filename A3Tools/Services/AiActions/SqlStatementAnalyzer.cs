using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace A3Tools.Services.AiActions;

/// <summary>
/// ★ 2026-10-25 陛下要求：execute_sql 开放 INSERT 权限
///   SQL 语句类型（由 SqlStatementAnalyzer 判定）
/// </summary>
public enum SqlStatementKind
{
    /// <summary>只读查询（SELECT / SHOW / DESCRIBE / EXPLAIN）</summary>
    Select,
    /// <summary>INSERT 新增（开放执行）</summary>
    Insert,
    /// <summary>UPDATE 更新（不执行，只回传语句给陛下手动跑）</summary>
    Update,
    /// <summary>DELETE 删除（不执行，只回传语句给陛下手动跑）</summary>
    Delete,
    /// <summary>其他（DDL / EXEC / MERGE / GRANT ...）</summary>
    Other
}

/// <summary>
/// SQL 语句分析结果
/// </summary>
public sealed class SqlAnalysisResult
{
    /// <summary>语句类型</summary>
    public SqlStatementKind Kind { get; init; }

    /// <summary>是否允许继续（false = 被安全检查拦下）</summary>
    public bool Allowed { get; init; }

    /// <summary>拦截原因</summary>
    public string Reason { get; init; } = "";

    /// <summary>主动词（SELECT / INSERT / UPDATE ...）</summary>
    public string MainVerb { get; init; } = "";

    /// <summary>原始语句（UPDATE/DELETE 时原样回传给陛下手动执行）</summary>
    public string Statement { get; init; } = "";

    /// <summary>INSERT 的 VALUES 元组行数（0 = 无法估算，如 INSERT ... SELECT）</summary>
    public int InsertRowEstimate { get; init; }
}

/// <summary>
/// ★ 2026-10-25 execute_sql 安全检查重写
///
/// 【为什么要重写而不是打补丁】
///   旧实现是「整条 SQL 扫黑名单 + 必须以 SELECT 开头」，开放 INSERT 会踩 3 个坑：
///     1) 旧黑名单含 INTO → INSERT INTO 会被自己误杀
///     2) 旧实现扫整条 SQL → SELECT 'delete' 这种字符串字面量会被误判成写操作
///     3) 旧多语句检查扫 ';' → INSERT INTO t VALUES('a;b') 会被误判成多语句
///
/// 【新实现】
///   1) 先做「词法遮罩」：去注释、字符串字面量内容清空、方括号标识符内容清空
///      → 字面量/标识符里的关键字不再干扰判定
///   2) 解析「主语句动词」：只认语句的第一个词（WITH CTE 会正确穿透到 CTE 之后的动词）
///   3) 按动词分类决定放行策略
///   4) 只在「括号深度 0」层面判断多语句，天然忽略子查询/字面量里的分号
/// </summary>
public static class SqlStatementAnalyzer
{
    /// <summary>INSERT 单次最大写入行数（VALUES 元组数）</summary>
    public const int MaxInsertRows = 5000;

    /// <summary>可作为「主语句动词」的词</summary>
    private static readonly HashSet<string> StatementVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP",
        "TRUNCATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE",
        "SHUTDOWN", "KILL", "OPENROWSET", "OPENDATASOURCE", "OPENQUERY", "BULK",
        "USE", "SET", "DECLARE", "PRINT", "WAITFOR", "BEGIN", "COMMIT", "ROLLBACK",
        // ★ 只读动词也要能当主动词（2026-08-26 旧实现就允许 SHOW/DESCRIBE/EXPLAIN，不能回归）
        //   注意：只认「语句第一个词」，所以 ORDER BY x DESC 的 DESC 不会被误当成主动词
        "SHOW", "DESCRIBE", "DESC", "EXPLAIN"
    };

    /// <summary>只读动词</summary>
    private static readonly HashSet<string> SelectVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "SHOW", "DESCRIBE", "DESC", "EXPLAIN"
    };

    /// <summary>任何类型都禁止的写操作关键字（词边界匹配，不误伤 UPDATED_AT / create_date）</summary>
    private static readonly string[] ForbiddenWords =
    {
        "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "MERGE", "GRANT", "REVOKE", "DENY",
        "BACKUP", "RESTORE", "SHUTDOWN", "KILL",
        "OPENROWSET", "OPENDATASOURCE", "OPENQUERY"
    };

    /// <summary>
    /// 分析一条 SQL
    /// </summary>
    /// <param name="sql">原始 SQL</param>
    /// <param name="limit">行数上限（查询返回行数 / INSERT 写入行数）</param>
    public static SqlAnalysisResult Analyze(string sql, int limit = 1000)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new SqlAnalysisResult { Kind = SqlStatementKind.Other, Allowed = false, Reason = "SQL 为空" };

        string original = sql.Trim();
        string trimmed = original.TrimEnd(';').Trim();

        // ── ① 词法遮罩 ───────────────────────────────────────────
        // comments 去掉；字符串字面量内容清空（保留引号）；方括号标识符内容清空
        string noComment = StripComments(trimmed);
        string text = MaskStringLiterals(noComment);   // 保留方括号 → 供 sp_ 检测用
        string scan = MaskBracketIdentifiers(text);      // 方括号内容也清空 → 供关键字扫描用

        // ── ② 多语句检查（在遮罩后的文本上做，字面量/子查询里的 ';' 天然不触发）──
        int semi = scan.IndexOf(';');
        if (semi >= 0)
        {
            string after = scan.Substring(semi + 1).Trim();
            if (after.Length > 0)
                return new SqlAnalysisResult
                {
                    Kind = SqlStatementKind.Other,
                    Allowed = false,
                    Reason = "SQL 不允许多语句（分号后还有内容）"
                };
        }

        // ── ③ 解析主语句动词 ─────────────────────────────────────
        if (!TryGetMainVerb(scan, out string verb, out int verbStart))
        {
            return new SqlAnalysisResult
            {
                Kind = SqlStatementKind.Other,
                Allowed = false,
                Reason = "无法识别 SQL 语句类型（只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 查询，或 INSERT 新增）"
            };
        }

        // 主动词之后的部分（用于关键字扫描；主动词本身排除）
        string body = scan.Substring(verbStart + verb.Length);

        // ── ④ 按类型处理 ─────────────────────────────────────────
        if (SelectVerbs.Contains(verb))
        {
            var bad = FindForbidden(body, includeInsert: true);
            if (bad != null)
                return Reject(verb, $"包含禁止的关键字 [{bad}]，只读查询只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN");

            // SELECT ... INTO newTbl = 建表（写操作）
            if (CountWord(body, "INTO") > 0)
                return Reject(verb, "SELECT ... INTO 会创建新表（写操作），请改用 execute_ddl");

            // FROM sp_xxx / xp_xxx = 在执行存储过程
            if (Regex.IsMatch(text, @"\bFROM\s+(?:\[?[\w\-#]+\]?\s*\.\s*)?\[?(?:SP|XP)_[\w\-#]*\]?", RegexOptions.IgnoreCase))
                return Reject(verb, "不允许通过 SELECT ... FROM sp_xxx 调用存储过程");

            return new SqlAnalysisResult { Kind = SqlStatementKind.Select, Allowed = true, MainVerb = verb };
        }

        if (verb.Equals("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            var bad = FindForbidden(body, includeInsert: false);
            if (bad != null)
                return Reject(verb, $"INSERT 语句里不允许 [{bad}]，只允许纯 INSERT INTO 表(列) VALUES(...)");

            // INTO 只允许「紧跟 INSERT」的那一个（INSERT INTO / INSERT t 均可）
            int intoCount = CountWord(body, "INTO");
            if (Regex.IsMatch(body, @"^\s*INTO\b", RegexOptions.IgnoreCase)) intoCount--;
            if (intoCount > 0)
                return Reject(verb, "INSERT 语句里的 INTO 只能是 INSERT INTO 的目标表（SELECT ... INTO 建表已禁止）");

            if (Regex.IsMatch(text, @"\bFROM\s+(?:\[?[\w\-#]+\]?\s*\.\s*)?\[?(?:SP|XP)_[\w\-#]*\]?", RegexOptions.IgnoreCase))
                return Reject(verb, "不允许通过 INSERT ... FROM sp_xxx 调用存储过程");

            int tuples = CountInsertTuples(scan);
            if (tuples > MaxInsertRows)
                return Reject(verb, $"单次 INSERT 最多 {MaxInsertRows} 行（当前 {tuples} 行），请分批插入");

            return new SqlAnalysisResult
            {
                Kind = SqlStatementKind.Insert,
                Allowed = true,
                MainVerb = verb,
                InsertRowEstimate = tuples
            };
        }

        if (verb.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            verb.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            // ★ 陛下要求：更新/删除不自动执行，但要把语句原样交出来
            return new SqlAnalysisResult
            {
                Kind = verb.Equals("UPDATE", StringComparison.OrdinalIgnoreCase)
                    ? SqlStatementKind.Update
                    : SqlStatementKind.Delete,
                Allowed = true,
                MainVerb = verb,
                Statement = original
            };
        }

        // DDL / EXEC / MERGE / GRANT ... → 全部拒绝
        return Reject(verb, $"不允许执行 {verb} 语句。execute_sql 只开放只读查询与 INSERT 新增；DDL 请用 execute_ddl");
    }

    private static SqlAnalysisResult Reject(string verb, string reason) => new()
    {
        Kind = SqlStatementKind.Other,
        Allowed = false,
        Reason = reason,
        MainVerb = verb
    };

    /// <summary>
    /// 在文本中找第一个禁止关键字（词边界，避免 UPDATED_AT / create_date 误伤）
    /// </summary>
    private static string? FindForbidden(string body, bool includeInsert)
    {
        foreach (var w in ForbiddenWords)
        {
            if (Regex.IsMatch(body, $@"\b{w}\b", RegexOptions.IgnoreCase))
                return w;
        }
        if (includeInsert && Regex.IsMatch(body, @"\bINSERT\b", RegexOptions.IgnoreCase))
            return "INSERT";
        return null;
    }

    /// <summary>统计独立词出现次数</summary>
    private static int CountWord(string text, string word)
    {
        return Regex.Matches(text, $@"\b{word}\b", RegexOptions.IgnoreCase).Count;
    }

    /// <summary>
    /// 解析主语句动词
    ///   - 普通语句：第一个词就是动词
    ///   - WITH cte AS (...) [, cte2 AS (...)] 后面才是动词
    /// </summary>
    private static bool TryGetMainVerb(string scan, out string verb, out int verbStart)
    {
        verb = "";
        verbStart = -1;

        int i = SkipWhitespace(scan, 0);
        if (i >= scan.Length) return false;

        string first = ReadWord(scan, i, out int firstEnd);
        if (!first.Equals("WITH", StringComparison.OrdinalIgnoreCase))
        {
            if (StatementVerbs.Contains(first)) { verb = first; verbStart = i; return true; }
            return false;
        }

        // WITH <name> AS ( ... ) [, <name> AS ( ... )] <verb>
        int p = firstEnd;
        while (true)
        {
            p = SkipWhitespace(scan, p);
            if (p >= scan.Length) return false;

            ReadWord(scan, p, out p);              // CTE 名
            p = SkipWhitespace(scan, p);
            if (!StartsWithWord(scan, p, "AS")) return false;
            p += 2;
            p = SkipWhitespace(scan, p);
            if (p >= scan.Length || scan[p] != '(') return false;
            p = SkipBalanced(scan, p);             // 跳过 ( ... )

            p = SkipWhitespace(scan, p);
            if (p < scan.Length && scan[p] == ',') { p++; continue; }
            break;
        }

        if (p >= scan.Length) return false;
        string v = ReadWord(scan, p, out int vend);
        if (!StatementVerbs.Contains(v)) return false;
        verb = v; verbStart = p;
        return true;
    }

    /// <summary>统计 INSERT 的 VALUES 元组行数（INSERT ... SELECT 无法估算，返回 0）</summary>
    private static int CountInsertTuples(string scan)
    {
        var m = Regex.Match(scan, @"\bVALUES\b", RegexOptions.IgnoreCase);
        if (!m.Success) return 0;

        int depth = 0, tuples = 0;
        for (int i = m.Index + m.Length; i < scan.Length; i++)
        {
            char c = scan[i];
            if (c == '(') { if (depth == 0) tuples++; depth++; }
            else if (c == ')') { if (depth > 0) depth--; }
        }
        return tuples;
    }

    /// <summary>去掉行注释与块注释</summary>
    private static string StripComments(string sql)
    {
        string s = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        s = Regex.Replace(s, @"--[^\n]*", " ");
        return s;
    }

    /// <summary>字符串字面量内容清空（保留引号，避免粘连其他 token）</summary>
    private static string MaskStringLiterals(string sql)
    {
        return Regex.Replace(sql, @"'(?:[^']|'')*'", "''");
    }

    /// <summary>方括号标识符内容清空（[delete] 这种列名不该触发关键字判定）</summary>
    private static string MaskBracketIdentifiers(string sql)
    {
        return Regex.Replace(sql, @"\[[^\]]*\]", "[]");
    }

    private static int SkipWhitespace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }

    private static string ReadWord(string s, int i, out int end)
    {
        end = i;
        while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '_' || s[end] == '@' || s[end] == '#')) end++;
        return s.Substring(i, end - i);
    }

    private static bool StartsWithWord(string s, int i, string word)
    {
        if (i + word.Length > s.Length) return false;
        if (!s.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase)) return false;
        int after = i + word.Length;
        return after >= s.Length || !(char.IsLetterOrDigit(s[after]) || s[after] == '_');
    }

    /// <summary>从 '(' 开始跳过配对的 ')'，返回 ')' 之后的位置</summary>
    private static int SkipBalanced(string s, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0) return i + 1;
            }
        }
        return s.Length;
    }
}
