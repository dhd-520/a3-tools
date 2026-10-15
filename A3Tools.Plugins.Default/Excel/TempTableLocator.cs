using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// 从 SQL 文本中识别 ##xxx 临时表引用
/// 设计: 只做语法识别 (找 ##alias 字面量), 不做完整 SQL 解析
/// 识别规则:
///   - 形如 ##alias (SQL Server 全局临时表风格, ## 前缀)
///   - alias 必须符合标识符规范: 字母/下划线开头, 后跟字母/数字/下划线
///   - 排除字符串字面量内的 ## (例 '包含 ## 的注释')
///   - 排除单行注释 -- 内的 ##
///   - 排除多行注释 /* */ 内的 ##
///   - 多次引用同一个 alias 只返回一次 (保留首次出现顺序)
/// </summary>
public static class TempTableLocator
{
    // 匹配 ##alias 形式 (alias 必须符合标识符规范)
    private static readonly Regex _refPattern = new(
        @"##(?<alias>[A-Za-z_][A-Za-z_0-9]*)",
        RegexOptions.Compiled);

    // SQL 字符串字面量: '...' (含 SQL Server 风格的 '' 转义) 或 "..."
    // 用同长度的空格替换, 保留位置, 防止正则误匹配字符串里的 ##
    private static readonly Regex _stringLiteral = new(
        @"'(?:[^']|'')*'|""(?:[^""]|"""")*""",
        RegexOptions.Compiled);

    // 单行注释: -- ... 到行尾
    private static readonly Regex _lineComment = new(
        @"--[^\r\n]*",
        RegexOptions.Compiled);

    // 多行注释: /* ... */
    private static readonly Regex _blockComment = new(
        @"/\*.*?\*/",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// 从 SQL 中提取所有 ##alias 引用 (去重, 保留首次出现顺序)
    /// 返回的 alias 不带 ## 前缀
    /// </summary>
    public static List<string> ExtractAliases(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new List<string>();

        // 1. 剥字符串字面量 (用空格占位, 长度不变, 不影响正则位置)
        var stripped = _stringLiteral.Replace(sql, m => new string(' ', m.Length));
        // 2. 剥单行注释
        stripped = _lineComment.Replace(stripped, m => new string(' ', m.Length));
        // 3. 剥多行注释
        stripped = _blockComment.Replace(stripped, m => new string(' ', m.Length));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (Match m in _refPattern.Matches(stripped))
        {
            var alias = m.Groups["alias"].Value;
            if (seen.Add(alias))
                result.Add(alias);
        }
        return result;
    }

    /// <summary>
    /// SQL 是否包含任何 ##alias 引用
    /// </summary>
    public static bool ContainsTempTableRef(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var stripped = _stringLiteral.Replace(sql, m => new string(' ', m.Length));
        stripped = _lineComment.Replace(stripped, m => new string(' ', m.Length));
        stripped = _blockComment.Replace(stripped, m => new string(' ', m.Length));
        return _refPattern.IsMatch(stripped);
    }
}
