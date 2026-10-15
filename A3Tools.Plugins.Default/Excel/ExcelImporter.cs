using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// NPOI 包装: 读 .xlsx / .xls → DataTable
/// 不做类型推断 (那是 <see cref="TypeInferrer"/> 的事), 不做列映射 (那是 UI 的事)
/// 这里只负责: 拿到 sheet 的原始字符串数据 + 头部行
/// </summary>
public static class ExcelImporter
{
    public static List<string> ListSheets(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("filePath 为空", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel 文件不存在", filePath);

        using var wb = OpenWorkbook(filePath);
        var result = new List<string>(wb.NumberOfSheets);
        for (int i = 0; i < wb.NumberOfSheets; i++)
        {
            result.Add(wb.GetSheetName(i));
        }
        return result;
    }

    /// <summary>
    /// 读整个 sheet 到 DataTable (列名 = 第一行, 之后行都是数据)
    /// 所有单元格统一转成 string, 公式取缓存结果
    /// </summary>
    /// <param name="maxRows">最多读多少数据行 (不含表头), 0 = 不限</param>
    public static DataTable ReadSheet(string filePath, string sheetName, int maxRows = 0)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("filePath 为空", nameof(filePath));
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException("sheetName 为空", nameof(sheetName));

        using var wb = OpenWorkbook(filePath);
        var sheet = wb.GetSheet(sheetName)
            ?? throw new InvalidOperationException($"Sheet 不存在: {sheetName}");
        return ReadSheetInternal(sheet, maxRows);
    }

    private static IWorkbook OpenWorkbook(string path)
    {
        // NPOI: XSSFWorkbook(.xlsx) / HSSFWorkbook(.xls)
        // 用 using stream, workbook 构造时把全部内容读进内存, stream 关闭不影响
        var ext = Path.GetExtension(path);
        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.OpenRead(path);
            return new XSSFWorkbook(fs);
        }
        if (ext.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            using var fs = File.OpenRead(path);
            return new HSSFWorkbook(fs);
        }
        throw new NotSupportedException(
            $"不支持的 Excel 格式: {ext}（仅支持 .xlsx / .xls）");
    }

    private static DataTable ReadSheetInternal(ISheet sheet, int maxRows)
    {
        var dt = new DataTable();
        if (sheet.PhysicalNumberOfRows == 0) return dt;

        // ===== 第一行作为列名 =====
        int headerRowIdx = sheet.FirstRowNum;
        var headerRow = sheet.GetRow(headerRowIdx);
        if (headerRow is null || headerRow.LastCellNum <= 0) return dt;

        for (int c = 0; c < headerRow.LastCellNum; c++)
        {
            var cell = headerRow.GetCell(c);
            var colName = (ReadCellAsString(cell) ?? $"Col{c + 1}").Trim();
            if (string.IsNullOrEmpty(colName)) colName = $"Col{c + 1}";

            // 处理重复列名: Col, Col_1, Col_2...
            var orig = colName;
            int dup = 1;
            while (dt.Columns.Contains(colName))
                colName = $"{orig}_{dup++}";

            dt.Columns.Add(colName, typeof(string));
        }

        // ===== 数据行 =====
        int dataRowCount = 0;
        for (int r = headerRowIdx + 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;

            var dr = dt.NewRow();
            bool allEmpty = true; // 跳过完全空白的行
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                var cell = row.GetCell(c);
                var val = ReadCellAsString(cell);
                if (!string.IsNullOrEmpty(val)) allEmpty = false;
                dr[c] = val ?? (object)DBNull.Value;
            }
            if (allEmpty) continue; // 全空行不要

            dt.Rows.Add(dr);
            dataRowCount++;
            if (maxRows > 0 && dataRowCount >= maxRows) break;
        }
        return dt;
    }

    /// <summary>
    /// 把任意 cell 转成 string (含公式/日期/数字/布尔)
    /// 公式取 CachedFormulaResultType 而不是公式文本
    /// </summary>
    private static string? ReadCellAsString(ICell? cell)
    {
        if (cell is null) return null;

        // 公式: 取缓存的结果值
        if (cell.CellType == CellType.Formula)
        {
            switch (cell.CachedFormulaResultType)
            {
                case CellType.String: return cell.StringCellValue?.Trim();
                case CellType.Numeric: return FormatNumericCell(cell);
                case CellType.Boolean: return cell.BooleanCellValue ? "True" : "False";
                case CellType.Blank: return null;
                default: return cell.ToString()?.Trim();
            }
        }

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue?.Trim(),
            CellType.Numeric => FormatNumericCell(cell),
            CellType.Boolean => cell.BooleanCellValue ? "True" : "False",
            CellType.Blank => null,
            _ => cell.ToString()?.Trim()
        };
    }

    private static string? FormatNumericCell(ICell cell)
    {
        if (cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric
            || cell.CellType == CellType.Numeric)
        {
            // 日期格式: 用 DateUtil 判断
            if (DateUtil.IsCellDateFormatted(cell))
            {
                try
                {
                    // 显式 cast + InvariantCulture, 避免 NPOI 不同版本 DateCellValue 返回类型不一致
                    var dt = (DateTime)cell.DateCellValue;
                    return dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch { /* 兜底走数字格式 */ }
            }
            double v = cell.NumericCellValue;
            // 整数不要带 .0
            if (!double.IsNaN(v) && !double.IsInfinity(v) && v == Math.Floor(v)
                && Math.Abs(v) < 1e15)
            {
                return ((long)v).ToString();
            }
            return v.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);
        }
        return null;
    }
}
