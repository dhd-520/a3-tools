using A3Tools.Models;
using A3Tools.Plugins.Default.Forms;

namespace A3Tools.Services.AiActions;

/// <summary>
/// 跨库复制数据库对象（表/视图/函数/存储过程）
/// <para>★ 2026-10-10 陛下要求：把工具箱 8 个跨库复制工具全部开放给 AI</para>
/// <list type="bullet">
///   <item>设计原则：AI 只负责「打开对应窗口 + 预填源账套」</item>
///   <item>目标账套 / 对象由陛下在窗口内选择（AI 不擅自决定）</item>
///   <item>窗口内还需点 [开始复制] → 二次保护</item>
///   <item>所有 8 个 Action 走同一模式：构造函数传入 MainForm.Instance（实现 IToolContext）+ 源账套</item>
/// </list>
/// </summary>
public class CopyDataObjectAction : IAiAction
{
    public string Name => "copy_data_object";
    public string Description => "跨库复制数据库对象（表/视图/函数/存储过程）到目标数据库。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的对象后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["object_name"] = new { type = "string", description = "要复制的对象名（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var obj = arguments.TryGetValue("object_name", out var o) ? o?.ToString() : null;
        return $"打开「跨库复制数据库对象」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  复制对象：{obj ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments,
                out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyTableForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制数据库对象」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制配置数据（标准查询/系统参数/自定义数据源）
/// </summary>
public class CopyConfigDataAction : IAiAction
{
    public string Name => "copy_config_data";
    public string Description => "跨库复制配置数据（标准查询 / 系统参数 / 自定义数据源）到目标数据库。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的数据类型后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        return $"打开「跨库复制配置数据」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"（请在窗口内选择数据类型后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyConfigDataForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制配置数据」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制 Win 表单（通过 OBJECTGUID）
/// </summary>
public class CopyWinFormAction : IAiAction
{
    public string Name => "copy_win_form";
    public string Description => "跨库复制 Win 表单（通过 OBJECTGUID）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的表单后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["object_guid"] = new { type = "string", description = "Win 表单的 OBJECTGUID（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var guid = arguments.TryGetValue("object_guid", out var g) ? g?.ToString() : null;
        return $"打开「跨库复制 Win 表单」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  表单 GUID：{guid ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyFormForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制 Win 表单」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制 APP 表单（移动端表单）
/// </summary>
public class CopyAppFormAction : IAiAction
{
    public string Name => "copy_app_form";
    public string Description => "跨库复制 APP 表单（移动端表单）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的表单后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        return $"打开「跨库复制 APP 表单」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"（请在窗口内搜索选择表单后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyAppFormForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制 APP 表单」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制单据流转（ObjectLink，通过 CODE）
/// </summary>
public class CopyObjectLinkAction : IAiAction
{
    public string Name => "copy_object_link";
    public string Description => "跨库复制单据流转（通过 CODE）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的单据流转后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["code"] = new { type = "string", description = "单据流转的 CODE（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() : null;
        return $"打开「跨库复制单据流转」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  单据流转 CODE：{code ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyObjectLinkForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制单据流转」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制报表（通过 CODE）
/// </summary>
public class CopyReportAction : IAiAction
{
    public string Name => "copy_report";
    public string Description => "跨库复制报表（通过 CODE）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的报表后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["code"] = new { type = "string", description = "报表的 CODE（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() : null;
        return $"打开「跨库复制报表」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  报表 CODE：{code ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyReportForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制报表」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制 Web 看板（通过 CODE）
/// </summary>
public class CopyWebDashboardAction : IAiAction
{
    public string Name => "copy_web_dashboard";
    public string Description => "跨库复制 Web 看板（通过 CODE）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的 Web 看板后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["code"] = new { type = "string", description = "Web 看板的 CODE（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() : null;
        return $"打开「跨库复制 Web 看板」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  Web 看板 CODE：{code ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyWebObjectForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制 Web 看板」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 跨库复制移动看板（AppChart，通过 CODE）
/// </summary>
public class CopyAppChartAction : IAiAction
{
    public string Name => "copy_app_chart";
    public string Description => "跨库复制移动看板（通过 CODE）到目标账套。会打开工具箱复制窗口，请在窗口内选择目标账套、要复制的移动看板后点击「开始复制」。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（可选，默认在打开的窗口内选择）" },
            ["code"] = new { type = "string", description = "移动看板的 CODE（可选，默认在窗口内搜索选择）" }
        }
    };
    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var dst = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() : null;
        return $"打开「跨库复制移动看板」窗口\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{dst ?? "窗口内选择"}\n" +
               $"  移动看板 CODE：{code ?? "窗口内搜索选择"}\n" +
               $"（请在窗口内确认后点击「开始复制」按钮）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var error);
            if (sourceAccount == null) return Task.FromResult(AiActionResult.Fail(error!));

            main.Invoke(() =>
            {
                var form = new CrossDbCopyAppChartForm(main, sourceAccount);
                form.Show();
            });

            return Task.FromResult(AiActionResult.Ok(
                $"已打开「跨库复制移动看板」窗口（源账套：{sourceAccount.Code} {sourceAccount.Name}）",
                new { source_account = sourceAccount.Code, form_opened = true }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"打开复制窗口失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// 8 个跨库复制 Action 共用辅助方法
/// <para>★ 2026-10-10 新增</para>
/// </summary>
internal static class CrossDbCopyHelper
{
    /// <summary>
    /// 解析源账套：
    ///   1. 优先用参数 source_account（DataService.FindAccount 校验存在）
    ///   2. fallback 到 MainForm 当前选中账套（AI 助理面板选的）
    ///   3. 都没有则报错
    /// </summary>
    public static Account? ResolveSourceAccount(
        A3Tools.Plugins.IToolContext main,
        Dictionary<string, object?> arguments,
        out string? error)
    {
        error = null;

        string? sourceCode = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        if (!string.IsNullOrWhiteSpace(sourceCode))
        {
            var ds = new DataService();
            var acc = ds.FindAccount(sourceCode);
            if (acc == null)
            {
                error = $"源账套 [{sourceCode}] 不存在，请先 list_accounts 查看已配置账套";
                return null;
            }
            return acc;
        }

        var selected = main.GetSelectedAccount();
        if (selected != null) return selected;

        error = "请先在 AI 助理面板选择账套，或在调用时传 source_account 参数";
        return null;
    }
}
