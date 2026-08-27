using System.Collections.Generic;
using System.Linq;
using A3Tools.Models;

namespace A3Tools.Services.AiActions;

/// <summary>
/// AI Action 注册表（单例）
/// <para>★ 2026-08-26 R2-B：内置 5 个 Action，全部为读操作，AI 无法写文件 / 杀进程</para>
/// </summary>
public class ActionRegistry
{
    private static readonly Lazy<ActionRegistry> _instance = new(() => new ActionRegistry());
    public static ActionRegistry Instance => _instance.Value;

    private readonly Dictionary<string, IAiAction> _actions = new();

    private ActionRegistry()
    {
        // 🟢 只读（不弹窗）
        Register(new ListAccountsAction());
        Register(new GetAppInfoAction());
        Register(new CheckUpdateAction());
        Register(new ReadLogAction());
        Register(new GetSystemStatusAction());
        Register(new GetAccountDetailAction());
        Register(new ListRunningAccountsAction());
        // 🟡 写本地 + 🟠 进程（普通确认）
        Register(new AddAccountAction());
        Register(new StartAccountAction());
        Register(new StopAccountAction());
        Register(new LaunchLinkDbAction());
        Register(new OpenDataFolderAction());
        Register(new CleanLogsAction());
        // 🔴 高危（输入账套编码确认）
        Register(new DeleteAccountAction());
        // ★ 2026-08-26 R2-D 业务查询（list_tables / get_table_schema 只读；execute_sql 高危）
        Register(new ListTablesAction());
        Register(new GetTableSchemaAction());
        Register(new ExecuteSqlAction());
    }

    private void Register(IAiAction action)
    {
        _actions[action.Name] = action;
    }

    public IAiAction? Get(string name) =>
        _actions.TryGetValue(name, out var a) ? a : null;

    public IEnumerable<IAiAction> All() => _actions.Values;

    /// <summary>
    /// 把所有 Action 转成 OpenAI Function Calling 协议要求的 tools 数组
    /// </summary>
    public object[] ToOpenAiToolsSchema()
    {
        return _actions.Values.Select(a => new
        {
            type = "function",
            function = new
            {
                name = a.Name,
                description = a.Description,
                parameters = a.ParametersSchema
            }
        }).ToArray();
    }
}
