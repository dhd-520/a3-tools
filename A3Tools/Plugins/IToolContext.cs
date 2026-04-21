using A3Tools.Models;

namespace A3Tools.Plugins;

/// <summary>
/// 工具上下文接口 - 供工具箱开发者使用
/// </summary>
public interface IToolContext
{
    /// <summary>
    /// 获取当前选中的账套
    /// </summary>
    Account? GetSelectedAccount();
    
    /// <summary>
    /// 获取当前选中的账套代码
    /// </summary>
    string? GetSelectedAccountCode();
    
    /// <summary>
    /// 获取所有账套
    /// </summary>
    List<Account> GetAllAccounts();
    
    /// <summary>
    /// 显示消息提示
    /// </summary>
    void ShowMessage(string message);
    
    /// <summary>
    /// 显示错误提示
    /// </summary>
    void ShowError(string message);
}
