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

    /// <summary>把 RelatedTables 解析为字符串列表（去空白 + 去空）</summary>
    [JsonIgnore]
    public List<string> RelatedTableList =>
        string.IsNullOrWhiteSpace(RelatedTables)
            ? new List<string>()
            : RelatedTables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}