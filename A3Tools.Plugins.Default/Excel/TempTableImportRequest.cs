using System.Collections.Generic;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// Excel 导入请求 — ExcelImportForm 通过 DialogResult.OK 返回给调用方
/// 调用方拿到这个后, 负责解析 RawRows + Columns 里的字符串值成实际类型,
/// 拼成 <see cref="A3Tools.Common.DataAccess.ResultTable"/> 加进 <see cref="TempTableStore"/>
/// </summary>
public class TempTableImportRequest
{
    /// <summary>用户填的别名 (SQL 里用 ##alias 引用, 不带 ## 前缀)</summary>
    public string Alias { get; set; } = "";

    /// <summary>源 Excel 路径 (日志用)</summary>
    public string SourceFile { get; set; } = "";

    /// <summary>Sheet 名 (日志用)</summary>
    public string SheetName { get; set; } = "";

    /// <summary>列映射列表 (按用户最终选定的顺序)</summary>
    public List<TempTableColumn> Columns { get; set; } = new();

    /// <summary>每行原始数据 (按源列顺序, 一行一个 object?[] 字符串值)
    /// 调用方根据 Columns.TypeName 把字符串解析成 int/decimal/datetime/string</summary>
    public List<object?[]> RawRows { get; set; } = new();
}

/// <summary>导入列定义 (用户在 ExcelImportForm DataGridView 里编辑)</summary>
public class TempTableColumn
{
    /// <summary>Excel 原始列名 (read-only)</summary>
    public string SourceName { get; set; } = "";

    /// <summary>用户编辑后的目标列名 (editable, 默认 = SourceName)</summary>
    public string TargetName { get; set; } = "";

    /// <summary>数据类型: NVARCHAR / INT / DECIMAL / DATETIME (editable, 默认 TypeInferrer 推断)</summary>
    public string TypeName { get; set; } = "NVARCHAR";

    /// <summary>在源 DataTable 的列索引 (read-only, 用于从 RawRows 取值)</summary>
    public int OriginalIndex { get; set; }
}
