using System;
using System.Globalization;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// 把 Excel 读出的 string 解析成实际类型 (long / decimal / DateTime / string)
/// 解析失败返 null (UI 层决定如何提示, 这里不抛异常)
/// 类型映射: 见 <see cref="Parse"/>
/// </summary>
public static class TempTableValueParser
{
    /// <summary>
    /// 解析单个单元格值
    /// </summary>
    /// <param name="value">原始字符串 (来自 Excel 单元格, 已 Trim)</param>
    /// <param name="typeName">目标类型: NVARCHAR / INT / DECIMAL / DATETIME 等</param>
    /// <returns>解析成功返 boxed 值, 失败或空返 null</returns>
    public static object? Parse(string? value, string? typeName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = (typeName ?? "NVARCHAR").Trim().ToUpperInvariant();

        try
        {
            switch (t)
            {
                // ===== 整数族 =====
                case "INT":
                case "INTEGER":
                case "BIGINT":
                case "SMALLINT":
                case "TINYINT":
                    if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return l;
                    return null;

                // ===== 小数族 =====
                case "DECIMAL":
                case "NUMERIC":
                case "MONEY":
                case "SMALLMONEY":
                case "FLOAT":
                case "REAL":
                    if (decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        return d;
                    return null;

                // ===== 时间族 =====
                case "DATETIME":
                case "DATETIME2":
                case "DATE":
                case "TIME":
                case "SMALLDATETIME":
                case "TIMESTAMP":
                    // 优先当前 culture (匹配用户 Excel 本地化)
                    if (DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt))
                        return dt;
                    // 兜底: ISO 格式
                    if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        return dt;
                    return null;

                // ===== 文本族 (默认) =====
                case "NVARCHAR":
                case "VARCHAR":
                case "CHAR":
                case "NCHAR":
                case "TEXT":
                case "NTEXT":
                case "STRING":
                default:
                    return value.Trim();
            }
        }
        catch
        {
            // 解析异常一律兜底 null, 不让单个坏单元格挂掉整个导入
            return null;
        }
    }
}
