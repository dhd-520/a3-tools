namespace A3Tools.Services;

/// <summary>
/// SQL Server 数据类型格式化工具。
/// 专门处理 <c>sys.columns.max_length</c> 在不同类型下的语义差异：
/// <list type="bullet">
///   <item><description>NVARCHAR / NCHAR：<c>max_length</c> 是**字节数**（2 字节/字符），输出字符长度需除以 2</description></item>
///   <item><description>VARCHAR / CHAR / VARBINARY / BINARY：<c>max_length</c> 是字节数（1 字节/字符 = 字符数）</description></item>
///   <item><description>DECIMAL / NUMERIC：使用 <c>precision</c> 和 <c>scale</c>，<c>max_length</c> 含义不同</description></item>
/// </list>
/// MAX 类型（<c>max_length = -1</c>）单独处理为 <c>(MAX)</c>。
/// </summary>
public static class SqlDataTypeFormatter
{
    /// <summary>
    /// 根据 <c>sys.columns</c> 的元数据列格式化 SQL 数据类型字符串（含长度/精度）。
    /// </summary>
    /// <param name="dataType">来自 <c>sys.types.name</c>，例如 NVARCHAR、VARCHAR、DECIMAL。</param>
    /// <param name="maxLength">来自 <c>sys.columns.max_length</c>，-1 表示 MAX。</param>
    /// <param name="precision">来自 <c>sys.columns.precision</c>。</param>
    /// <param name="scale">来自 <c>sys.columns.scale</c>。</param>
    /// <returns>可用于 CREATE TABLE / ALTER TABLE 脚本的类型声明，如 <c>NVARCHAR(200)</c>。</returns>
    public static string Format(string dataType, int maxLength, int precision, int scale)
    {
        if (string.IsNullOrEmpty(dataType)) return string.Empty;
        return dataType.ToLowerInvariant() switch
        {
            "varchar" => maxLength == -1 ? "VARCHAR(MAX)" : $"VARCHAR({maxLength})",
            // NVARCHAR 字节数 = 字符数 × 2，需除以 2 输出字符长度
            "nvarchar" => maxLength == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({maxLength / 2})",
            "char" => $"CHAR({maxLength})",
            // NCHAR 同 NVARCHAR：字节数 = 字符数 × 2
            "nchar" => $"NCHAR({maxLength / 2})",
            "varbinary" => maxLength == -1 ? "VARBINARY(MAX)" : $"VARBINARY({maxLength})",
            "binary" => $"BINARY({maxLength})",
            "decimal" => $"DECIMAL({precision}, {scale})",
            "numeric" => $"NUMERIC({precision}, {scale})",
            _ => dataType
        };
    }
}