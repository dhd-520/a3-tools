using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// 从 Excel 读出的原始 DataTable (所有列都是 string) 推断每列的目标类型
/// 规则:
///   - 采样前 N 行 (默认 100), 跳过空白
///   - 90% 以上样本能 Parse 为 INT → INT
///   - 否则 90% 以上能 Parse 为 DECIMAL → DECIMAL
///   - 否则 90% 以上能 Parse 为 DATETIME → DATETIME
///   - 否则 NVARCHAR
/// 注: 全空列也返回 NVARCHAR (没数据不能瞎猜)
/// </summary>
public static class TypeInferrer
{
    public const double ConfidenceThreshold = 0.9;
    public const int DefaultSampleSize = 100;

    public static Dictionary<string, string> Infer(DataTable? dt, int sampleSize = DefaultSampleSize)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (dt is null || dt.Rows.Count == 0) return result;

        int sample = Math.Min(sampleSize, dt.Rows.Count);
        foreach (DataColumn col in dt.Columns)
        {
            result[col.ColumnName] = InferColumnType(dt, col, sample);
        }
        return result;
    }

    private static string InferColumnType(DataTable dt, DataColumn col, int sample)
    {
        int intCount = 0, decCount = 0, dateCount = 0, totalNonNull = 0;
        for (int i = 0; i < sample; i++)
        {
            var v = dt.Rows[i][col];
            if (v is null || v is DBNull) continue;
            var s = v.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) continue;

            totalNonNull++;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                intCount++;
            }
            else if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                decCount++;
            }
            else if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out _))
            {
                dateCount++;
            }
        }
        if (totalNonNull == 0) return "NVARCHAR";

        if ((double)intCount / totalNonNull >= ConfidenceThreshold) return "INT";
        if ((double)(intCount + decCount) / totalNonNull >= ConfidenceThreshold) return "DECIMAL";
        if ((double)dateCount / totalNonNull >= ConfidenceThreshold) return "DATETIME";
        return "NVARCHAR";
    }
}
