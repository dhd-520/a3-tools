namespace A3Tools.Models;

/// <summary>
/// 应用程序设置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// A3应用程序目录
    /// </summary>
    public string AppDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 上次选择的账套代码
    /// </summary>
    public string LastSelectedAccount { get; set; } = string.Empty;

    /// <summary>
    /// 是否启动电脑端
    /// </summary>
    public bool LaunchDesktop { get; set; } = true;

    /// <summary>
    /// 是否启动开发工具
    /// </summary>
    public bool LaunchDevTools { get; set; } = true;

    /// <summary>
    /// 是否启动网页版
    /// </summary>
    public bool LaunchWeb { get; set; } = false;

    /// <summary>
    /// 选择的浏览器 (chrome, msedge, firefox, 360se, default)
    /// </summary>
    public string SelectedBrowser { get; set; } = "chrome";

    /// <summary>
    /// 浏览器启动方式：true=新窗口，false=在当前Tab打开
    /// </summary>
    public bool BrowserNewWindow { get; set; } = true;

    /// <summary>
    /// 启动时是否弹出启动选项对话框（默认true）
    /// </summary>
    public bool ShowLaunchOptionsDialog { get; set; } = true;

    /// <summary>
    /// SSMS可执行文件路径（为空则自动查找）
    /// </summary>
    public string SsmsPath { get; set; } = string.Empty;

    /// <summary>
    /// 从托盘恢复显示的快捷键（如 "Ctrl+Shift+Z"，为空表示不启用）
    /// </summary>
    public string TrayShowHotkey { get; set; } = "Ctrl+Shift+Z";

    /// <summary>新增账套快捷键（如 "Ctrl+N"，为空表示不启用）</summary>
    public string AddHotkey { get; set; } = string.Empty;

    /// <summary>删除账套快捷键（如 "Delete"，为空表示不启用）</summary>
    public string DeleteHotkey { get; set; } = string.Empty;

    /// <summary>启动账套快捷键（如 "Enter"，为空表示不启用）</summary>
    public string LaunchHotkey { get; set; } = string.Empty;


    /// <summary>设置快捷键（如 "Ctrl+, "，为空表示不启用）</summary>
    public string SettingsHotkey { get; set; } = string.Empty;

    /// <summary>链接数据库快捷键（如 "Ctrl+K"，为空表示不启用）</summary>
    public string ConnectDBHotkey { get; set; } = string.Empty;

    /// <summary>远程连接快捷键（如 "Ctrl+R"，为空表示不启用）</summary>
    public string RemoteHotkey { get; set; } = string.Empty;
}
