namespace A3Tools.Models;

/// <summary>
/// 工具箱配置项
/// </summary>
public class ToolConfig
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 工具说明
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 执行的类库文件名（含扩展名，如 MyTool.dll）
    /// </summary>
    public string Library { get; set; } = string.Empty;
    
    /// <summary>
    /// 完整类名（如 Namespace.ClassName）
    /// </summary>
    public string ClassName { get; set; } = string.Empty;
    
    /// <summary>
    /// 方法名
    /// </summary>
    public string MethodName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 图标（可选，留空使用默认图标）
    /// </summary>
    public string Icon { get; set; } = string.Empty;
    
    /// <summary>
    /// 工具分类（可选）
    /// </summary>
    public string Category { get; set; } = "其他";
}

/// <summary>
/// 工具箱配置
/// </summary>
public class ToolsConfiguration
{
    public List<ToolConfig> Tools { get; set; } = new();
}
