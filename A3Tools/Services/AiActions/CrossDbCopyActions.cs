using System.Text.Json;
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

    public string Name => "copy_data_object";
    public string Description => "跨库复制数据库对象（表/视图/函数/存储过程）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定对象类型和对象名列表。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" },
            ["object_type"] = new { type = "string", description = "对象类型：U=表 V=视图 TF=表函数 FN=标量函数 P=存储过程（默认 U）" },
            ["object_names"] = new { type = "string", description = "对象名列表（必填，多个用分号分隔，如 S_Order;S_OrderLine）" },
            ["delete_if_exists"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" }
        },
        required = new[] { "target_account", "object_names", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

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
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

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

            // 7. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

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
/// <summary>
/// 跨库复制（copy_config_data）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyConfigDataAction : IAiAction
{

    public string Name => "copy_config_data";
    public string Description => "跨库复制配置数据到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定type、keys。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["type"] = new { type = "string", description = "standard_query / system_param / custom_datasource" },
            ["keys"] = new { type = "string", description = "数据键名列表（多个用分号分隔）" },
            ["delete_if_exists"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "type", "keys", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制配置数据\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string type = arguments.TryGetValue("type", out var at) ? at?.ToString() : "";
            string keys = arguments.TryGetValue("keys", out var ak) ? ak?.ToString() : "";
            bool deleteIfExists = arguments.TryGetValue("delete_if_exists", out var ad) && bool.TryParse(ad?.ToString(), out var bd) && bd;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(type))
                return AiActionResult.Fail("必须指定 type 参数");
            if (string.IsNullOrWhiteSpace(keys))
                return AiActionResult.Fail("必须指定 keys 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyConfigDataForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, typeStr: type, keys: keys.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList(), deleteIfExists: deleteIfExists);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_win_form）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyWinFormAction : IAiAction
{

    public string Name => "copy_win_form";
    public string Description => "跨库复制Win 表单（通过 OBJECTGUID）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定object_guids。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["object_guids"] = new { type = "string", description = "Win 表单的 OBJECTGUID 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["copy_stored_procs"] = new { type = "boolean", description = "是否复制关联的存储过程（默认 false）" },
            ["copy_table_structures"] = new { type = "boolean", description = "是否复制关联的表结构（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "object_guids", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制Win 表单（通过 OBJECTGUID）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string objectGuids = arguments.TryGetValue("object_guids", out var ao) ? ao?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            bool copyStoredProcs = arguments.TryGetValue("copy_stored_procs", out var acs) && bool.TryParse(acs?.ToString(), out var bcs) && bcs;
            bool copyTableStructures = arguments.TryGetValue("copy_table_structures", out var act) && bool.TryParse(act?.ToString(), out var bct) && bct;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(objectGuids))
                return AiActionResult.Fail("必须指定 object_guids 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyFormForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, objectGuids: objectGuids, deleteFirst: deleteFirst, copyStoredProcs: copyStoredProcs, copyTableStructures: copyTableStructures);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_app_form）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyAppFormAction : IAiAction
{

    public string Name => "copy_app_form";
    public string Description => "跨库复制APP 表单（通过 OBJECTGUID）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定object_guids。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["object_guids"] = new { type = "string", description = "APP 表单的 OBJECTGUID 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "object_guids", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制APP 表单（通过 OBJECTGUID）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string objectGuids = arguments.TryGetValue("object_guids", out var ao) ? ao?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(objectGuids))
                return AiActionResult.Fail("必须指定 object_guids 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyAppFormForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, objectGuids: objectGuids, deleteFirst: deleteFirst);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_object_link）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyObjectLinkAction : IAiAction
{

    public string Name => "copy_object_link";
    public string Description => "跨库复制单据流转（通过 CODE）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定codes。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["codes"] = new { type = "string", description = "单据流转的 CODE 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "codes", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制单据流转（通过 CODE）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string codesStr = arguments.TryGetValue("codes", out var ac) ? ac?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(codesStr))
                return AiActionResult.Fail("必须指定 codes 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyObjectLinkForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, codes: codesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray(), deleteFirst: deleteFirst);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_report）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyReportAction : IAiAction
{

    public string Name => "copy_report";
    public string Description => "跨库复制报表（通过 CODE）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定codes。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["codes"] = new { type = "string", description = "报表的 CODE 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "codes", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制报表（通过 CODE）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string codesStr = arguments.TryGetValue("codes", out var ac) ? ac?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(codesStr))
                return AiActionResult.Fail("必须指定 codes 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyReportForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, codes: codesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray(), deleteFirst: deleteFirst);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_web_dashboard）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyWebDashboardAction : IAiAction
{

    public string Name => "copy_web_dashboard";
    public string Description => "跨库复制Web 看板（通过 CODE）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定codes。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["codes"] = new { type = "string", description = "Web 看板的 CODE 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "codes", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制Web 看板（通过 CODE）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string codesStr = arguments.TryGetValue("codes", out var ac) ? ac?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(codesStr))
                return AiActionResult.Fail("必须指定 codes 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyWebObjectForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, codes: codesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray(), deleteFirst: deleteFirst);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 跨库复制（copy_app_chart）
/// <para>★ 2026-10-10 方案 4 陛下要求：白名单 + 二次确认 + 静默执行</para>
/// <list type="bullet">
///   <item>设计原则：AI 静默执行，不再弹窗让陛下手动操作</item>
///   <item>白名单机制：目标账套必须在 DATA/settings.json 的 CrossDbCopyWhitelist 中</item>
///   <item>二次确认：陛下必须输入目标账套编码才放行</item>
///   <item>审计日志：每次执行写 DATA/logs/ai_cross_db_copy_audit.log</item>
///   <item>陛下约束：不影响工具那部分代码（DefaultTools.cs 100% 不动），手动流程 100% 等价（Form 内 _isHeadlessMode 默认 false）</item>
/// </list>
/// </summary>
public class CopyAppChartAction : IAiAction
{

    public string Name => "copy_app_chart";
    public string Description => "跨库复制移动看板（通过 CODE）到目标账套。要求：(1) 必须指定目标账套编码（调用此工具后会弹窗让陛下确认） (2) 必须指定codes。复制操作会修改目标数据库，请谨慎操作。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["codes"] = new { type = "string", description = "移动看板的 CODE 列表（多个用分号分隔）" },
            ["delete_first"] = new { type = "boolean", description = "目标已存在时是否先删除（默认 false）" },
            ["source_account"] = new { type = "string", description = "源账套编码（★ 必填——陛下消息里指定了源账套就用指定的，没指定就用 AI 助理顶部下拉框的当前账套，绝不能省略）" },
            ["target_account"] = new { type = "string", description = "目标账套编码（必填）" }
        },
        required = new[] { "target_account", "codes", "source_account" }
    };
    public bool RequiresConfirmation => true;

        /// <summary>★ 2026-10-12 陛下简化：取消输入账套编码，直接点 [允许] 即可</summary>
    public string? ConfirmationPrompt => null;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        // ★ 2026-10-12 陛下要求：取消输入账套编码，直接点 [允许] 即可（白名单只作审计/提示）
        var targetCode = arguments.TryGetValue("target_account", out var t) ? t?.ToString() : null;

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
        catch { /* 白名单读不到走「默认非白名单」提示 */ }

        // ★ 动态确认：白名单内 → null（只点允许），不在白名单 → targetCode（输入编码）

        var src = arguments.TryGetValue("source_account", out var s) ? s?.ToString() : null;
        var whitelistInfo = whitelist.Count == 0
            ? "(白名单为空，所有目标账套都需要二次确认)"
            : (inWhitelist
                ? $"白名单内（共 {whitelist.Count} 个账套）"
                : $"不在白名单（已配：{string.Join(", ", whitelist)}）");

        return $"静默执行：跨库复制移动看板（通过 CODE）\n" +
               $"  源账套：{src ?? "AI 当前选中账套"}\n" +
               $"  目标账套：{targetCode ?? "(未指定)"}\n" +
               $"  白名单状态：{whitelistInfo}\n" +
               (inWhitelist ? "（白名单内）" : "（不在白名单）");
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var main = A3Tools.Forms.MainForm.Instance;
            if (main == null) return AiActionResult.Fail("MainForm 未初始化");

            // ★ 2026-10-12 陛下反馈：AI 模型把 object_guids 写成对象/target_account 包 XML 标签
            //   先校验参数类型，错误时返回带「正确格式示例」的错误，让 AI 自我纠正
            var validationError = CopyArgsValidator.Validate(arguments, Name);
            if (validationError != null) return AiActionResult.Fail(validationError);

            // 1. 解析 target_account（必填）
            string? targetCode = arguments.TryGetValue("target_account", out var tac) ? tac?.ToString() : null;

            // 2. 解析业务参数
            string codesStr = arguments.TryGetValue("codes", out var ac) ? ac?.ToString() : "";
            bool deleteFirst = arguments.TryGetValue("delete_first", out var adf) && bool.TryParse(adf?.ToString(), out var bdf) && bdf;
            // 3. 参数校验
            if (string.IsNullOrWhiteSpace(targetCode))
                return AiActionResult.Fail("必须指定 target_account 参数");
            if (string.IsNullOrWhiteSpace(codesStr))
                return AiActionResult.Fail("必须指定 codes 参数");

            // 4. 解析源账套
            Account? sourceAccount = CrossDbCopyHelper.ResolveSourceAccount(main, arguments, out var srcError);
            if (sourceAccount == null) return AiActionResult.Fail(srcError!);

            // 5. 解析目标账套
            var ds = new DataService();
            var targetAccount = ds.FindAccount(targetCode);
            if (targetAccount == null)
                return AiActionResult.Fail($"目标账套 [{targetCode}] 不存在，请先 list_accounts 查看已配置账套");

            // 6. ★ 2026-10-12 陛下简化：白名单只作审计/提示，不再阻塞执行（取消输入账套编码）
            var settings = ds.LoadSettings();
            var whitelist = settings.CrossDbCopyWhitelist ?? new List<string>();
            bool inWhitelist = whitelist.Contains(targetCode);

            // 7. ★ 静默执行：构造 Form（不 Show）→ ExecuteHeadlessAsync → Dispose
            bool success;
            var form = new CrossDbCopyAppChartForm(main, sourceAccount);
            try
            {
                success = await form.ExecuteHeadlessAsync(sourceAccount, targetAccount, codes: codesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray(), deleteFirst: deleteFirst);
            }
            finally
            {
                form.Dispose();
            }

            // 8. 返回结果（审计日志在 ExecuteHeadlessAsync finally 里写过了）
            return success
                ? AiActionResult.Ok(
                    $"已复制到 [{targetCode}]",
                    new { source = sourceAccount.Code, target = targetCode, success = true })
                : AiActionResult.Fail("复制失败，请查看 DATA/logs/ai_cross_db_copy_audit.log 详细原因");
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"静默执行失败：{ex.Message}");
        }
    }
}

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

/// <summary>
/// ★ 2026-10-12 陛下反馈：AI 模型在 tool_call 里把 object_guids 写成对象、target_account 值包 XML 标签
///   根因：模型层 schema 理解错 / 模型本身在产出垃圾
///   修法：每个 CopyXxxAction.ExecuteAsync 开头调一次 Validate，类型错就返回带「正确格式示例」的错误
///   效果：AI 看到「正确格式示例」后能自我纠正（下一轮调用就传对）
/// </summary>
internal static class CopyArgsValidator
{
    /// <summary>校验参数：string 必填字段必须是 string（或 JsonElement 字符串），否则返回错误信息（带格式示例）</summary>
    /// <returns>null = 通过；非 null = 错误信息</returns>
    public static string? Validate(Dictionary<string, object?> args, string toolName)
    {
        var stringFields = GetStringFields(toolName);
        var errors = new List<string>();

        foreach (var field in stringFields)
        {
            if (!args.TryGetValue(field, out var val) || val == null)
            {
                errors.Add($"缺少必填参数 {field}");
                continue;
            }
            if (val is string) continue;
            // JsonElement 字符串：转回 C# string（AI 有时把字符串也包成 JsonElement）
            if (val is JsonElement je && je.ValueKind == JsonValueKind.String)
            {
                args[field] = je.GetString();
                continue;
            }
            // 类型错：val 是对象、数组、bool、数字、JsonElement(object/array) 等
            string actualType = val is JsonElement je2 ? je2.ValueKind.ToString().ToLower() : val.GetType().Name;
            string snippet = val.ToString() ?? "";
            if (snippet.Length > 80) snippet = snippet.Substring(0, 80) + "...";
            errors.Add($"{field} 必须是 string，但收到的是 {actualType}：{snippet}");
        }

        if (errors.Count == 0) return null;
        return $"{toolName} 参数类型错误：\n  - " + string.Join("\n  - ", errors) +
               $"\n\n正确格式示例：\n  {GetFormatExample(toolName)}";
    }

    /// <summary>各工具的 string 必填字段（其他类型字段不校验）</summary>
    private static string[] GetStringFields(string toolName) => toolName switch
    {
        "copy_data_object" => new[] { "source_account", "target_account", "object_names" },
        "copy_config_data" => new[] { "source_account", "target_account", "type", "keys" },
        "copy_win_form" => new[] { "source_account", "target_account", "object_guids" },
        "copy_app_form" => new[] { "source_account", "target_account", "object_guids" },
        "copy_object_link" => new[] { "source_account", "target_account", "codes" },
        "copy_report" => new[] { "source_account", "target_account", "codes" },
        "copy_web_dashboard" => new[] { "source_account", "target_account", "codes" },
        "copy_app_chart" => new[] { "source_account", "target_account", "codes" },
        _ => new[] { "source_account", "target_account" }
    };

    /// <summary>各工具的「正确格式示例」（错误信息会带，让 AI 自我纠正）</summary>
    public static string GetFormatExample(string toolName) => toolName switch
    {
        "copy_data_object" => "{\"object_names\": \"S_Order;S_OrderLine\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_if_exists\": false}",
        "copy_config_data" => "{\"type\": \"U\", \"keys\": \"B1;B2\", \"source_account\": \"0109\", \"target_account\": \"0080\"}",
        "copy_win_form" => "{\"object_guids\": \"S_SCM_OLITEMFORBIDDEN_INPUT\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": true}",
        "copy_app_form" => "{\"object_guids\": \"S_APP_001\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": true}",
        "copy_object_link" => "{\"codes\": \"OL001;OL002\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": false}",
        "copy_report" => "{\"codes\": \"R001;R002\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": false}",
        "copy_web_dashboard" => "{\"codes\": \"DASH001\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": false}",
        "copy_app_chart" => "{\"codes\": \"CHART001\", \"source_account\": \"0109\", \"target_account\": \"0080\", \"delete_first\": false}",
        _ => "{}"
    };
}
