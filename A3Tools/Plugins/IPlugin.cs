using A3Tools.Models;

namespace A3Tools.Plugins;

/// <summary>
/// 工具箱插件接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 执行插件
    /// </summary>
    void Execute(Account? currentAccount);
}
