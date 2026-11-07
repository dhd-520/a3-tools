namespace A3Tools.Models;

/// <summary>
/// AI 厂商类型
/// </summary>
public enum AiProviderType
{
    /// <summary>OpenAI（含官方及兼容协议）</summary>
    OpenAI = 0,
    /// <summary>DeepSeek</summary>
    DeepSeek = 1,
    /// <summary>阿里通义千问（DashScope 兼容模式）</summary>
    Qwen = 2,
    /// <summary>智谱 GLM</summary>
    Zhipu = 3,
    /// <summary>月之暗面 Kimi</summary>
    Moonshot = 4,
    /// <summary>MiniMax</summary>
    MiniMax = 5,
    /// <summary>Ollama 本地</summary>
    Ollama = 6,
    /// <summary>自定义 OpenAI 兼容服务</summary>
    Custom = 99
}

/// <summary>
/// 单个 AI 厂商配置
/// </summary>
public class AiProviderConfig
{
    /// <summary>厂商唯一标识（GUID 字符串，新增时生成）</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>厂商显示名称（陛下可改名）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>厂商类型（决定默认 URL/模型）</summary>
    public AiProviderType Type { get; set; } = AiProviderType.OpenAI;

    /// <summary>API Base URL（OpenAI 兼容协议根地址，含 /v1）</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>API Key（落盘时 AES 加密）</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>默认模型名（如 gpt-4o-mini / deepseek-chat）</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否为当前默认厂商</summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// ★ 2026-11-07 图片消息支持：该厂商的模型是否支持视觉（看图）
    /// <para>null = 自动（按模型名判断，见 <see cref="GuessSupportsVision"/>）</para>
    /// <para>true = 明确支持；false = 明确不支持</para>
    /// </summary>
    public bool? SupportsVision { get; set; } = null;

    /// <summary>
    /// 实际是否支持视觉：显式设置优先，否则按模型名自动判断
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EffectiveSupportsVision =>
        SupportsVision ?? GuessSupportsVision(Type, Model);

    /// <summary>备注（可选）</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// ★ 2026-11-07：根据厂商类型 + 模型名猜是否支持视觉
    /// <para>只是初次添加时的默认值，陛下可在设置页改。</para>
    /// </summary>
    public static bool GuessSupportsVision(AiProviderType type, string model)
    {
        string m = (model ?? string.Empty).ToLowerInvariant().Trim();
        if (m.Length == 0) return false;

        // 明确不支持视觉的（避免误报）
        if (m.Contains("deepseek-chat") || m.Contains("deepseek-reasoner")) return false;
        if (m.StartsWith("qwen-plus") || m.StartsWith("qwen-turbo") || m.StartsWith("qwen-max")) return false;
        if (m.StartsWith("moonshot-v1") || m.StartsWith("moonshot-v2")) return false;
        if (m.StartsWith("glm-4-flash") || m.StartsWith("glm-4-plus") || m.StartsWith("glm-4-air")) return false;
        if (m.Contains("abab")) return false;

        // 支持视觉的
        if (m.Contains("gpt-4o") || m.Contains("gpt-4.1") || m.Contains("gpt-4-turbo")
            || m.Contains("gpt-4-vision") || m.StartsWith("gpt-5")) return true;
        if (m.Contains("vl") || m.Contains("vision")) return true;      // qwen-vl / qwen2.5-vl / kimi-vl / doubao-vision ...
        // ★ 「数字 + v」也算：glm-4v / step-1v / ernie-4.5-vl（GLM 的写法是 4v 不是 vl）
        if (System.Text.RegularExpressions.Regex.IsMatch(m, @"\d+v")) return true;
        if (m.Contains("llava") || m.Contains("bakllava") || m.Contains("moondream")
            || m.Contains("minicpm-v") || m.Contains("internvl") || m.Contains("pixtral")) return true;
        if (m.StartsWith("minimax-vl") || m.StartsWith("abab6.5s-v")) return true;
        if (m.Contains("gemma-3")) return true;                        // gemma-3 支持视觉

        return false;
    }
}

/// <summary>
/// 厂商预设（用于 AI 设置页「快速添加」按钮）
/// </summary>
public class AiProviderPreset
{
    public AiProviderType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public static List<AiProviderPreset> Defaults => new()
    {
        new() { Type = AiProviderType.OpenAI,   Name = "OpenAI",       ApiUrl = "https://api.openai.com/v1",                          Model = "gpt-4o-mini" },
        new() { Type = AiProviderType.DeepSeek, Name = "DeepSeek",     ApiUrl = "https://api.deepseek.com/v1",                        Model = "deepseek-chat" },
        new() { Type = AiProviderType.Qwen,     Name = "通义千问",     ApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",   Model = "qwen-plus" },
        new() { Type = AiProviderType.Zhipu,    Name = "智谱 GLM",     ApiUrl = "https://open.bigmodel.cn/api/paas/v4",              Model = "glm-4-flash" },
        new() { Type = AiProviderType.Moonshot, Name = "月之暗面 Kimi",ApiUrl = "https://api.moonshot.cn/v1",                         Model = "moonshot-v1-8k" },
        new() { Type = AiProviderType.MiniMax,  Name = "MiniMax",     ApiUrl = "https://api.MiniMax.chat/v1",                       Model = "MiniMax-Text-01" },
        new() { Type = AiProviderType.Ollama,   Name = "Ollama 本地",  ApiUrl = "http://localhost:11434/v1",                          Model = "qwen2.5:7b" }
    };
}
