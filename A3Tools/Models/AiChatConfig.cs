namespace A3Tools.Models;

/// <summary>
/// 对话历史存储策略
/// </summary>
public enum ChatStoreStrategy
{
    /// <summary>本地加密存储（默认，AES 加密 JSON）</summary>
    Local = 0,
    /// <summary>仅内存（关闭程序后清除）</summary>
    Memory = 1,
    /// <summary>不存储对话历史（每次新开窗口是空白）</summary>
    Disabled = 2
}

/// <summary>
/// AI 客服全局配置
/// </summary>
public class AiChatConfig
{
    /// <summary>已配置的厂商列表</summary>
    public List<AiProviderConfig> Providers { get; set; } = new();

    /// <summary>默认厂商 ID（为空则用第一个启用的）</summary>
    public string DefaultProviderId { get; set; } = string.Empty;

    /// <summary>对话历史存储策略</summary>
    public ChatStoreStrategy StoreStrategy { get; set; } = ChatStoreStrategy.Local;

    /// <summary>历史保留天数（0 = 永久，仅 Local 策略生效）</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>单次会话最大消息数（超出自动截断最早的）</summary>
    public int MaxMessagesPerSession { get; set; } = 200;

    /// <summary>单次请求最大 Token 数（发给模型的提示词+历史总长）</summary>
    public int MaxContextTokens { get; set; } = 8000;

    /// <summary>温度（0-2，越高越发散）</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>系统提示词追加内容（陛下可自定义，比如"回答简洁，少废话"）</summary>
    public string SystemPromptAppend { get; set; } = string.Empty;

    /// <summary>是否在启动时自动打开上次未关闭的会话</summary>
    public bool AutoResumeLastSession { get; set; } = true;
}
