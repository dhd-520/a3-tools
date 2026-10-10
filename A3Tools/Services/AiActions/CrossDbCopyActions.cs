using A3Tools.Models;
using A3Tools.Plugins.Default.Forms;

namespace A3Tools.Services.AiActions;

/// <summary>
/// 跨库复制数据库对象（表/视图/函数/存储过程）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyDataObjectAction : IAiAction
{
    /// <summary>★ 2026-10-10 方案 4：动态二次确认（GetImpactDescription 时设置，ConfirmationPrompt 返回）</summary>
    private static string? _pendingTargetCode;

    public string Name => "copy_data_object";
    public string Description => "跨库复制数据库对象（表/视图/函数/存储过程）到目标账套。要求：(1) 目标账套在白名单内只需点「允许」，不在白名单内需要输入目标账套编码二次确认 (2) 必须指定对象类型和对象名列表。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（可选，默认 AI 助理当前选中账套）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填，必须在白名单内）" },
            ["object_type"] = new { type = "string", description = "对象类型：U=表 V=视图 TF=表函数 FN=标量函数 P=存储过程（默认 U）" },
            ["object_names"] = new { type = "string", description = "对象名列表（必填，多个用分号分隔，如 S_Order;S_OrderLine）" },
            ["delete_if_exists"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" }
        },
        required = new[] { "target_account", "object_names" }
    };
    public bool RequiresConfirmation => true;

    /// <summary>★ 2026-10-10 方案 4：动态二次确认（陛下必须输入目标账套编码）</summary>
    public string? ConfirmationPrompt => _pendingTargetCode;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-10 15:55 陛下简化设计：白名单内只点允许，白名单外二次确认
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

        // 读白名单 + 检查 target 是否在白名单
        bool inWhitelist = false;
        List<string> whitelist = new();
        try
        {
            var ds = new DataService();
            var settings = ds.LoadSettings();
            whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            if (!string.IsNullOrEmpty(targetCode))
                inWhitelist = whitelist.Contains(targetCode);
        }
        catch { /* 白名单读不到走「全部要二次确认」路径 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）
        _pendingTargetCode = inWhitelist ? null : targetCode;

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var type = arguments.TryGetValue("object_type", out var ot) ? ot?.ToString() ?? "U" : "U";
        var names = arguments.TryGetValue("object_names", out var n) ? n?.ToString() ?? "" : "";
        var del = arguments.TryGetValue("delete_if_exists", out var d) && bool.TryParse(d?.ToString(), out var b) && b;

        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制数据库对象\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  对象类型：{type}\n" +
               $"  对象名：{names}\n" +
               $"  目标已存在时{(del ? "「先删除」" : "「跳过」")}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist
                   ? "（白名单内：点「允许」即可直接执行）"
                   : "（不在白名单：请输入目标账套编码二次确认）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // 1. 解析目标账套（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");

            // 2. 解析对象名（必填）
            string? objectNamesStr = arguments.TryGetValue("object_names", out var n) ? n?.ToString() : null;
            if (string.IsNullOrWhiteSpace(objectNamesStr))
                return AiActionResult.Fail("必须指定 object_names 参数");

            // 3. 解析对象类型（默认 U）
            string objectType = arguments.TryGetValue("object_type", out var ot) ? ot?.ToString() ?? "U" : "U";
            if (!new[] { "U", "V", "TF", "FN", "P" }.Contains(objectType))
                return AiActionResult.Fail($"对象类型 {objectType} 非法，必须是 U/V/TF/FN/P 之一");

            // 4. 解析 delete_if_exists
            bool deleteIfExists = arguments.TryGetValue("delete_if_exists", out var d) && bool.TryParse(d?.ToString(), out var b) && b;

            // 5. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 6. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 7. ★ 白名单检查（陛下简化设计：白名单内只点允许，白名单外二次确认）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);
            if (!inWhitelist && _pendingTargetCode == null)
            {
                // 不在白名单 + 没触发二次确认（防御 GetImpactDescription 没被调用的边界）
                return AiActionResult.Fail(
                    $"目标账套 [{targetCode}] 不在白名单内，且未通过二次确认。\n" +
                    $"请在 DATA/settings.json 的 CrossDbCopyWhitelist 字段配置后才能直接调用，\n" +
                    $"或在系统确认弹窗中输入目标账套编码二次确认。\n" +
                    $"当前白名单：{(whitelist.Count == 0 ? "(空)" : string.Join(", ", whitelist))}");
            }

            // 8. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            var objectNames = objectNamesStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            if (objectNames.Count == 0)
                return AiActionResult.Fail("object_names 解析后为空");

            bool success;
            var form = new CrossDbCopyTableForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, objectNames, objectType, deleteIfExists);
            }
            finally
            {
                form.Dispose();
            }

            // 9. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制 {objectNames.Count} 个 [{objectType}] 类型对象到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, type = objectType, count = objectNames.Count, objects = objectNames })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
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
