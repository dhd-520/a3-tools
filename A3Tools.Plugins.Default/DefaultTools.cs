using A3Tools.Models;
using A3Tools.Plugins;

namespace A3Tools.Plugins.Default;

/// <summary>
/// 数据库备份工具
/// </summary>
public class DatabaseBackupTool
{
    public string Name => "数据库备份";
    public string Description => "备份当前账套数据库到指定位置";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        if (account == null)
        {
            context.ShowError("请先选择一个账套！");
            return;
        }

        if (string.IsNullOrEmpty(account.Database) || string.IsNullOrEmpty(account.DbUser))
        {
            context.ShowError("账套数据库信息不完整！");
            return;
        }

        var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Title = "保存数据库备份",
            Filter = "SQL文件(*.sql)|*.sql|所有文件(*.*)|*.*",
            FileName = $"{account.Code}_{account.Name}_{DateTime.Now:yyyyMMddHHmmss}.sql"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            context.ShowMessage($"备份路径：{dialog.FileName}\n数据库：{account.Database}");
        }
    }
}

/// <summary>
/// 账套信息查看工具
/// </summary>
public class AccountInfoTool
{
    public string Name => "账套信息";
    public string Description => "查看当前账套的详细信息";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        if (account == null)
        {
            context.ShowError("请先选择一个账套！");
            return;
        }

        var info = new System.Text.StringBuilder();
        info.AppendLine($"代码：{account.Code}");
        info.AppendLine($"名称：{account.Name}");
        info.AppendLine($"账套地址：{account.Server}");
        info.AppendLine($"数据库地址：{account.Database}");
        info.AppendLine($"数据库名称：{account.DatabaseName}");
        info.AppendLine($"DB用户：{account.DbUser}");
        info.AppendLine($"远程方式：{account.RemoteType}");
        info.AppendLine($"远程地址：{account.RemoteAddress}");

        System.Windows.Forms.MessageBox.Show(info.ToString(), "账套信息",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }
}

/// <summary>
/// 远程连接工具
/// </summary>
public class RemoteConnectTool
{
    public string Name => "远程连接";
    public string Description => "快捷连接到账套服务器";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        if (account == null)
        {
            context.ShowError("请先选择一个账套！");
            return;
        }

        if (string.IsNullOrEmpty(account.RemoteAddress))
        {
            context.ShowError("账套远程地址为空！");
            return;
        }

        System.Windows.Forms.Clipboard.SetText(account.RemoteAddress);
        context.ShowMessage($"远程地址已复制到剪贴板：{account.RemoteAddress}");
    }
}

/// <summary>
/// 跨库复制数据库对象工具
/// </summary>
public class CrossDbCopyTableTool
{
    public string Name => "跨库复制数据库对象";
    public string Description => "复制表/视图/函数/存储过程等数据库对象到目标数据库（支持搜索选源库对象）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyTableForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制配置数据工具
/// </summary>
public class CrossDbCopyConfigDataTool
{
    public string Name => "跨库复制配置数据";
    public string Description => "复制配置数据（标准查询/系统参数/自定义数据源）到目标数据库";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyConfigDataForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制Win表单工具
/// </summary>
public class CrossDbCopyWinFormTool
{
    public string Name => "跨库复制Win表单";
    public string Description => "复制Win表单到目标账套（通过OBJECTGUID）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyFormForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制APP表单工具
/// </summary>
public class CrossDbCopyAppFormTool
{
    public string Name => "跨库复制APP表单";
    public string Description => "复制APP表单到目标账套";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyAppFormForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 搜索后台表单工具
/// </summary>
public class SearchBackendFormTool
{
    public string Name => "搜索后台表单";
    public string Description => "搜索后台表单";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.SearchBackendForm(context);
        form.Show();
    }
}

/// <summary>
/// 搜索前台菜单工具
/// </summary>
public class SearchFrontendMenuTool
{
    public string Name => "搜索前台菜单";
    public string Description => "搜索前台菜单";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.SearchFrontendMenuForm(context);
        form.Show();
    }
}

/// <summary>
/// 跨库复制单据流转工具
/// </summary>
public class CrossDbCopyObjectLinkTool
{
    public string Name => "复制单据流转";
    public string Description => "复制单据流转到目标账套（通过CODE）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyObjectLinkForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制报表工具
/// </summary>
public class CrossDbCopyReportTool
{
    public string Name => "复制报表";
    public string Description => "复制报表到目标账套（通过CODE）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyReportForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制WEB看板工具
/// </summary>
public class CrossDbCopyWebObjectTool
{
    public string Name => "复制WEB看板";
    public string Description => "复制WEB看板到目标账套（通过CODE）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyWebObjectForm(context, account);
        form.Show();
    }
}

/// <summary>
/// 跨库复制移动看板工具
/// </summary>
public class CrossDbCopyAppChartTool
{
    public string Name => "复制移动看板";
    public string Description => "复制移动看板到目标账套（通过CODE）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyAppChartForm(context, account);
        form.Show();
    }
}

/// <summary>
/// SQL 查询工具（轻量级 SQL 编辑器，替代 SSMS 常用功能）。
/// 按账套单实例：每个账套最多一个 SqlQueryForm 窗体，跨账套用 OS 任务栏切换。
/// 后期会被「复制数据库对象」等工具双击穿透：右键 ALTER 脚本 / 函数编辑。
/// </summary>
public class SqlQueryTool
{
    // 按账套+服务器+库名隔离
    private static readonly Dictionary<string, A3Tools.Plugins.Default.Forms.SqlQueryForm> _instances = new();
    private static readonly object _lock = new();

    public string Name => "SQL查询";
    public string Description => "SQL 查询编辑器（替代 SSMS 常用功能，支持多 Tab、切换数据库、对象脚本穿透）";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        // 代入账套优先级：
        //   1. 外部传入的 account（如「链接数据库」按钮传入选中账套）
        //   2. 工具箱上方的「源账套」预选器（账套列表选中行不适用）
        // 工具箱按钮调用时不传 account（传入 null），走 fallback 走源账套
        var preset = context.GetToolDatabasePreset();
        var source = account ?? preset.SourceAccount;

        if (source == null)
        {
            context.ShowError("请先在工具箱上方【选择源账套】，或选择账套后点击「链接数据库」！");
            return;
        }
        if (string.IsNullOrEmpty(source.Database) || string.IsNullOrEmpty(source.DatabaseName) || string.IsNullOrEmpty(source.DbUser))
        {
            context.ShowError("账套数据库信息不完整（需要 Database/DatabaseName/DbUser）！");
            return;
        }

        var key = $"{source.Code}|{source.Database}|{source.DatabaseName}";
        A3Tools.Plugins.Default.Forms.SqlQueryForm? form;
        lock (_lock)
        {
            if (!_instances.TryGetValue(key, out form) || form.IsDisposed)
            {
                form = new A3Tools.Plugins.Default.Forms.SqlQueryForm(source);
                form.FormClosed += (s, e) =>
                {
                    lock (_lock) { _instances.Remove(key); }
                };
                _instances[key] = form;
            }
        }
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Show();
        form.BringToFront();
        form.Activate();
    }
}