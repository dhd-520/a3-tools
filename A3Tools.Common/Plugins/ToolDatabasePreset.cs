using A3Tools.Models;

namespace A3Tools.Plugins;

/// <summary>
/// 工具箱预设数据库上下文。
/// 主窗体工具箱 Tab 可统一选择源/目标账套，工具窗体打开时自动带入；
/// 工具窗体内仍可自行修改或重新选择账套。
/// </summary>
public class ToolDatabasePreset
{
    /// <summary>源数据库账套</summary>
    public Account? SourceAccount { get; set; }

    /// <summary>目标数据库账套</summary>
    public Account? TargetAccount { get; set; }
}
