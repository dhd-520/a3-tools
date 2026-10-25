namespace A3Tools.Models;

/// <summary>
/// ★ 2026-10-24 陛下需求：AI 启动 A3 账套时可任意组合启动
///   （客户端 / 开发工具 / ERP网页 / 企业微信网页，四选一或任意组合）
///
/// 设计要点：
///   1. 用「显式解析 + 精确 token 匹配」代替旧的 <c>target.Contains("client")</c> 字符串包含判断。
///      旧写法有两个致命 bug：
///        · <c>"all".Contains("client") == false</c> → 「全启」实际把客户端关掉了
///        · 旧 forceTarget 只写 <c>settings.LaunchWeb</c>，而 <c>LaunchWeb</c> 是**死字段**
///          （启动逻辑真正读的是 LaunchErp / LaunchWechatWork）
///          → AI 无论指定什么，ERP 网页都跟着启（陛下反馈的根因）
///   2. 解析结果四个开关全量覆盖 settings，不做「部分覆盖」，杜绝漏改。
/// </summary>
public sealed class LaunchTargetSpec
{
    /// <summary>启动电脑端客户端（君则A3.exe）</summary>
    public bool LaunchDesktop { get; init; }

    /// <summary>启动集成开发工具（君则A3集成开发工具.exe）</summary>
    public bool LaunchDevTools { get; init; }

    /// <summary>启动 ERP 网页版（/h5comerp/#/login）</summary>
    public bool LaunchErp { get; init; }

    /// <summary>启动企业微信网页版（/h5apperp/#/index/home）</summary>
    public bool LaunchWechatWork { get; init; }

    /// <summary>一个组件都没选（相当于「只做前置准备，不启任何程序」）</summary>
    public bool IsEmpty =>
        !LaunchDesktop && !LaunchDevTools && !LaunchErp && !LaunchWechatWork;

    /// <summary>
    /// AI 未指定 target 时的默认组合：客户端 + H5 网页版。
    /// 与 2026-08-27 旧行为（forceTarget 默认 "client+web"）保持一致，避免存量用法突变。
    /// </summary>
    public static LaunchTargetSpec Default => new() { LaunchDesktop = true, LaunchErp = true };

    /// <summary>全部组件都没选时，等价于「按用户保存的设置走」——由调用方决定语义</summary>
    public bool IsAll =>
        LaunchDesktop && LaunchDevTools && LaunchErp && LaunchWechatWork;

    /// <summary>
    /// 解析 target 字符串。分隔符支持 <c>+ , ; / |</c> 和空白。
    /// token 精确匹配（忽略大小写），未识别的 token 视为错误并返回给调用方，
    /// 让 AI 能拿到明确反馈后自行纠正，而不是静默启动错误组合。
    /// </summary>
    /// <param name="raw">形如 <c>"client"</c>、<c>"client+devtools"</c>、<c>"all"</c>、<c>"none"</c></param>
    /// <param name="spec">解析结果</param>
    /// <param name="error">失败原因（成功时为空串）</param>
    public static bool TryParse(string? raw, out LaunchTargetSpec spec, out string error)
    {
        spec = new LaunchTargetSpec();
        error = "";

        if (raw == null)
        {
            error = "target 为 null";
            return false;
        }

        var tokens = SplitTokens(raw);
        if (tokens.Count == 0)
        {
            error = "target 为空";
            return false;
        }

        bool desktop = false, devTools = false, erp = false, wechat = false;
        var unknown = new List<string>();

        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "client":
                case "desktop":
                case "pc":
                case "客户端":
                case "电脑端":
                case "君则a3":
                    desktop = true;
                    break;

                case "devtools":
                case "dev":
                case "ide":
                case "开发工具":
                case "集成开发工具":
                    devTools = true;
                    break;

                case "web":
                case "erp":
                case "h5":
                case "网页":
                case "网页版":
                    erp = true;
                    break;

                case "wechat":
                case "wx":
                case "企业微信":
                case "企业微信网页":
                    wechat = true;
                    break;

                case "all":
                case "全部":
                case "全选":
                    desktop = devTools = erp = wechat = true;
                    break;

                case "none":
                case "无":
                case "不启动":
                    desktop = devTools = erp = wechat = false;
                    break;

                default:
                    unknown.Add(token);
                    break;
            }
        }

        if (unknown.Count > 0)
        {
            error = $"无法识别的 target「{string.Join(", ", unknown)}」。"
                  + "可用值：client(客户端)、devtools(开发工具)、erp(ERP网页)、wechat(企业微信网页)、all(全启)、none(全不启)；"
                  + "多个组合用 + 或 , 连接，如 client+devtools";
            return false;
        }

        spec = new LaunchTargetSpec
        {
            LaunchDesktop = desktop,
            LaunchDevTools = devTools,
            LaunchErp = erp,
            LaunchWechatWork = wechat
        };
        return true;
    }

    /// <summary>
    /// 解析多个 token（AI 有时会把 target 传成数组）。任一 token 无法识别即失败。
    /// </summary>
    public static bool TryParseTokens(IEnumerable<string>? rawTokens, out LaunchTargetSpec spec, out string error)
    {
        spec = new LaunchTargetSpec();
        error = "";

        if (rawTokens == null)
        {
            error = "target 为 null";
            return false;
        }

        var list = rawTokens.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
        if (list.Count == 0)
        {
            error = "target 为空";
            return false;
        }

        return TryParse(string.Join("+", list), out spec, out error);
    }

    /// <summary>按 + 分隔符切分 token（同时兼容 , ; / | 与空白）</summary>
    private static List<string> SplitTokens(string raw)
    {
        var seps = new[] { '+', ',', ';', '/', '|', ' ', '\t', '\r', '\n' };
        return raw.Split(seps, StringSplitOptions.RemoveEmptyEntries)
                  .Select(t => t.Trim())
                  .Where(t => t.Length > 0)
                  .ToList();
    }

    /// <summary>
    /// 规范化为可回传给 <see cref="TryParse(string?, out LaunchTargetSpec, out string)"/> 的字符串。
    /// 用途：AI 侧先解析校验、再把规范串交给 MainForm，避免两处解析规则漂移。
    /// </summary>
    public string ToCanonicalString()
    {
        if (IsEmpty) return "none";
        var parts = new List<string>();
        if (LaunchDesktop) parts.Add("client");
        if (LaunchDevTools) parts.Add("devtools");
        if (LaunchErp) parts.Add("erp");
        if (LaunchWechatWork) parts.Add("wechat");
        return string.Join("+", parts);
    }

    /// <summary>人类可读描述，用于 toast / AI 回复</summary>
    public string Describe()
    {
        if (IsEmpty) return "不启动任何组件";
        var parts = new List<string>();
        if (LaunchDesktop) parts.Add("客户端");
        if (LaunchDevTools) parts.Add("开发工具");
        if (LaunchErp) parts.Add("ERP网页");
        if (LaunchWechatWork) parts.Add("企业微信网页");
        return string.Join("+", parts);
    }

    /// <summary>便于日志排查</summary>
    public override string ToString() => Describe();
}
