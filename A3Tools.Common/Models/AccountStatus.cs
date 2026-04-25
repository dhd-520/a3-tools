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
    public List<int> ProcessIds { get; set; } = new();
    public string Status { get; set; } = "空闲";
    public string StatusColor { get; set; } = "#666666";
}
