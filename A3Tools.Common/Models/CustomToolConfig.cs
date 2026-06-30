using System.Text.Json.Serialization;

namespace A3Tools.Models;

/// <summary>
/// 自定义复制工具配置。
/// 由用户在「自定义工具」配置窗体填写并保存到 DATA\custom-tools.json，
/// 启动时由 MainForm 加载并生成工具箱按钮，点击后调用通用复制窗体执行。
/// </summary>
public class CustomToolConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;          // 工具名（按钮显示）
    public string Description { get; set; } = string.Empty;   // 描述（按钮副标题）
    public string MainTable { get; set; } = string.Empty;     // 主表（如 S_REPORT）
    public string PrimaryKey { get; set; } = string.Empty;    // 主键字段（如 CODE）
    public string RelatedTables { get; set; } = string.Empty; // 关联表，英文分号分隔（如 S_REPORTLINKDEFINE;S_REPORTDOUBLECLICK）
    public string ForeignKey { get; set; } = string.Empty;   // 关联表外键字段（如 REPORTGUID）
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>搜索列名，英文分号分隔（如 GUID;CODE;SUBSYSTEMGUID;NAME;NOTES）。空表示用旧行为：主键列 + 存在 NAME 则加上。</summary>
    public string SearchColumns { get; set; } = string.Empty;

    /// <summary>列显示名称，英文分号分隔（如 GUID;代码;分类;名称;备注）。数量必须与 SearchColumns 完全一致；缺失则保留数据库原列名。</summary>
    public string ColumnDisplayNames { get; set; } = string.Empty;

    /// <summary>隐藏列，英文分号分隔（如 GUID;SUBSYSTEMGUID）。即便在 SearchColumns 中出现也会强制隐藏；但 PrimaryKey 不受此影响（永远显示）。</summary>
    public string HiddenColumns { get; set; } = string.Empty;

    /// <summary>把 RelatedTables 解析为字符串列表（去空白 + 去空）</summary>
    [JsonIgnore]
    public List<string> RelatedTableList =>
        string.IsNullOrWhiteSpace(RelatedTables)
            ? new List<string>()
            : RelatedTables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 SearchColumns 解析为字符串列表</summary>
    [JsonIgnore]
    public List<string> SearchColumnList =>
        string.IsNullOrWhiteSpace(SearchColumns)
            ? new List<string>()
            : SearchColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 ColumnDisplayNames 解析为字符串列表</summary>
    [JsonIgnore]
    public List<string> ColumnDisplayNameList =>
        string.IsNullOrWhiteSpace(ColumnDisplayNames)
            ? new List<string>()
            : ColumnDisplayNames.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 HiddenColumns 解析为字符串列表（转大写用于大小写不敏感比较）</summary>
    [JsonIgnore]
    public HashSet<string> HiddenColumnSet =>
        string.IsNullOrWhiteSpace(HiddenColumns)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                HiddenColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
}