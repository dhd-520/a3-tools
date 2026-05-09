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
    public string Description => "快速连接到账套服务器";

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
/// 跨库复制表结构工具
/// </summary>
public class CrossDbCopyTableTool
{
    public string Name => "跨库复制表结构";
    public string Description => "复制指定表的结构到目标数据库";

    public void Execute(Account? account, A3Tools.Plugins.IToolContext context)
    {
        var form = new A3Tools.Plugins.Default.Forms.CrossDbCopyTableForm(context, account);
        form.ShowDialog();
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
        form.ShowDialog();
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
        form.ShowDialog();
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
        form.ShowDialog();
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
        form.ShowDialog();
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
        form.ShowDialog();
    }
}
