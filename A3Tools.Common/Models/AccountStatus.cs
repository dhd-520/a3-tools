namespace A3Tools.Models;

/// <summary>
/// 账套运行状态
/// </summary>
public class AccountStatus
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsWebRunning { get; set; }
    public bool IsClientRunning { get; set; }
    public bool IsDevToolsRunning { get; set; }
    public bool IsDbConnected { get; set; }
    public bool IsRemoteConnected { get; set; }

    /// <summary>
    /// 所有类型合并的 PID 列表（用于 DataGridView 进程数列显示 + 一键关闭）
    /// </summary>
    public List<int> ProcessIds { get; set; } = new();

    /// <summary>
    /// 按类型分别存储的 PID 列表（用于准确判断"哪种进程是否仍在运行"）
    /// 修复 bug：之前 ProcessIds 把 client / dev / web 全混一起，靠 IsXxxRunning bool 区分，
    /// 但死进程清理只清 PID 不重置 bool，导致手动关掉开发工具后再点启动，
    /// 会误把客户端 PID 当成开发工具 PID 切到前台，开发工具永远起不来。
    /// 现在按类型分开存，清理死 PID 时同步重算对应 bool 标记。
    /// </summary>
    public List<int> ClientProcessIds { get; set; } = new();
    public List<int> DevToolsProcessIds { get; set; } = new();
    public List<int> WebProcessIds { get; set; } = new();
    public List<int> DbProcessIds { get; set; } = new();
    public List<int> RemoteProcessIds { get; set; } = new();

    public string Status { get; set; } = "空闲";
    public string StatusColor { get; set; } = "#666666";
}
