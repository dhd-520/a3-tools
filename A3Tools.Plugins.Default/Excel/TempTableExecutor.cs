using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using A3Tools.Common.DataAccess;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// 本地临时表 SQL 执行器 (v1: 简单实现)
/// 走 DataView.RowFilter / DataView.Sort, 不引入完整 SQL parser
///
/// 支持语法 (大写小写无所谓, 关键字宽松匹配):
///   SELECT [* | col1, col2, ...] FROM ##alias
///       [WHERE 过滤表达式]
///       [ORDER BY col [ASC|DESC] [, col2 ...]]
///
/// 不支持 (v1 抛 NotSupportedException, 提示用户简化):
///   - JOIN (## 和 ## / ## 和 real_table)
///   - 子查询
///   - GROUP BY / HAVING
///   - DISTINCT
///   - 聚合函数 (COUNT/SUM/AVG/...)
///   - UNION
///
/// v1 简化: 列名不解析, 一律返回全部列 (顺序按 source.Columns 保持)
///         如果用户写 SELECT col1, col2 也忽略, 全列返回 (后续 v1.1 可加)
/// </summary>
public static class TempTableExecutor
{
    // ##alias (大小写不敏感, alias 必须符合标识符)
    private static readonly Regex _fromRef = new(
        @"\bFROM\s+##(?<alias>[A-Za-z_][A-Za-z_0-9]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // WHERE 子句
    private static readonly Regex _whereRef = new(
        @"\bWHERE\s+(?<expr>.+?)(?=\bORDER\s+BY\b|;|\Z)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // ORDER BY 子句
    private static readonly Regex _orderRef = new(
        @"\bORDER\s+BY\s+(?<expr>.+?)(?=;|\Z)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// 执行 SQL (只支持 ##alias 临时表查询)
    /// 成功时返回 ResultTable, 失败时抛清晰异常
    /// </summary>
    public static ResultTable Execute(string sql, TempTableStore store)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 为空", nameof(sql));

        // 1. 找 FROM ##alias
        var fromMatch = _fromRef.Match(sql);
        if (!fromMatch.Success)
            throw new NotSupportedException(
                "v1 临时表查询必须包含 FROM ##alias。\n" +
                "暂不支持 JOIN / 子查询 / 聚合 / 视图等其他语法。");

        var alias = fromMatch.Groups["alias"].Value;
        if (!store.TryGet(alias, out var source) || source is null)
        {
            var available = store.ListAliases();
            var hint = available.Count == 0
                ? "当前没有已导入的临时表, 请先 [导入 Excel]。"
                : $"已导入的临时表: {string.Join(", ", available)}";
            throw new InvalidOperationException(
                $"找不到临时表 ##{alias}。{hint}");
        }

        // 2. WHERE 解析
        var whereMatch = _whereRef.Match(sql);
        string? rowFilter = null;
        if (whereMatch.Success)
        {
            rowFilter = whereMatch.Groups["expr"].Value.Trim();
            // 删尾部分号
            if (rowFilter.EndsWith(";")) rowFilter = rowFilter.Substring(0, rowFilter.Length - 1).Trim();
            // 列名加方括号 (DataView.RowFilter 要求): 价格 → [价格]
            rowFilter = QuoteColumnNames(rowFilter, source);
        }

        // 3. ORDER BY 解析
        var orderMatch = _orderRef.Match(sql);
        string? sort = null;
        if (orderMatch.Success)
        {
            var orderExpr = orderMatch.Groups["expr"].Value.Trim();
            if (orderExpr.EndsWith(";")) orderExpr = orderExpr.Substring(0, orderExpr.Length - 1).Trim();
            // 转成 [col1] ASC, [col2] DESC
            sort = ConvertOrderBy(orderExpr, source);
        }

        // 4. 用 DataView 过滤 + 排序
        var dt = ToDataTable(source);
        var dv = dt.DefaultView;
        if (!string.IsNullOrEmpty(rowFilter))
            dv.RowFilter = rowFilter;
        if (!string.IsNullOrEmpty(sort))
            dv.Sort = sort;

        // 5. 收集结果
        var rows = new List<object?[]>(dv.Count);
        foreach (DataRowView drv in dv)
        {
            var arr = new object?[drv.Row.ItemArray.Length];
            drv.Row.ItemArray.CopyTo(arr, 0);
            rows.Add(arr);
        }

        return new ResultTable
        {
            Columns = source.Columns.Select(c => new ColumnInfo { Name = c.Name, TypeName = c.TypeName }).ToList(),
            Rows = rows
        };
    }

    private static DataTable ToDataTable(ResultTable source)
    {
        var dt = new DataTable();
        foreach (var col in source.Columns)
        {
            // 列类型用 typeof(object), 这样能保留上游 (Excel 导入 / 真实查询) 的原始类型
            // DataView.RowFilter 在 object 列上能正确做 int/decimal/datetime 比较
            dt.Columns.Add(col.Name, typeof(object));
        }
        foreach (var row in source.Rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < source.Columns.Count; i++)
            {
                var v = row[i];
                dr[i] = (v is null || v is DBNull) ? DBNull.Value : v;
            }
            dt.Rows.Add(dr);
        }
        return dt;
    }

    /// <summary>
    /// 把 WHERE 表达式里裸列名加方括号, 避免含中文/数字开头/关键字的列名报错
    /// 例: 价格 > 100 → [价格] > 100
    /// 例: 价格 = 'X' OR 日期 > '2026-01-01' → [价格] = 'X' OR [日期] > '2026-01-01'
    /// 例: [已带方括号] 保持原样
    /// 字符串字面量 ('...' 或 "...") 内的内容不处理
    /// </summary>
    private static string QuoteColumnNames(string expr, ResultTable source)
    {
        // 简单策略: 把 expr 按字符串字面量切段, 只对非字符串段做替换
        var result = new System.Text.StringBuilder();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (c == '\'')
            {
                // 跳到匹配的 ' (考虑 '' 转义)
                int start = i;
                i++;
                while (i < expr.Length)
                {
                    if (expr[i] == '\'')
                    {
                        if (i + 1 < expr.Length && expr[i + 1] == '\'') { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                result.Append(expr, start, i - start);
            }
            else
            {
                // 找下一个 [A-Za-z_] 开头的 token
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                    var token = expr.Substring(start, i - start);
                    // 是已知列名? → 加方括号
                    if (source.Columns.Any(col => col.Name.Equals(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Append('[').Append(token).Append(']');
                    }
                    else
                    {
                        result.Append(token);
                    }
                }
                else
                {
                    result.Append(c);
                    i++;
                }
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// 把 ORDER BY col1 ASC, col2 DESC 转成 [col1] ASC, [col2] DESC
    /// </summary>
    private static string ConvertOrderBy(string orderExpr, ResultTable source)
    {
        var parts = orderExpr.Split(',');
        for (int p = 0; p < parts.Length; p++)
        {
            var token = parts[p].Trim();
            // 拆 列名 + 方向
            var segs = token.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0) continue;
            var colName = segs[0];
            if (source.Columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)))
            {
                colName = "[" + colName + "]";
            }
            if (segs.Length == 1)
                parts[p] = colName;
            else
                parts[p] = colName + " " + segs[1]; // ASC / DESC
        }
        return string.Join(", ", parts);
    }
}
