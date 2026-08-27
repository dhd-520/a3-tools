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

    /// <summary>备注（可选）</summary>
    public string Remark { get; set; } = string.Empty;
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
