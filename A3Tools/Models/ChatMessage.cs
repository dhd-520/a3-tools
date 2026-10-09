using System.Collections.Generic;
using A3Tools.Services;

namespace A3Tools.Models;

/// <summary>
/// 消息角色（OpenAI 协议标准）
/// </summary>
public enum ChatRole
{
    /// <summary>系统提示词（不展示给陛下）</summary>
    System = 0,
    /// <summary>陛下发的</summary>
    User = 1,
    /// <summary>AI 回的</summary>
    Assistant = 2
}

/// <summary>
/// 单条对话消息
/// </summary>
public class ChatMessage
{
    /// <summary>消息唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>所属会话 ID</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>角色</summary>
    public ChatRole Role { get; set; } = ChatRole.User;

    /// <summary>消息内容（Markdown）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>使用的厂商名（仅 Assistant 消息）</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>使用的模型名（仅 Assistant 消息）</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>本次调用消耗的 Token（仅 Assistant 消息，可选）</summary>
    public int TokensUsed { get; set; } = 0;

    /// <summary>是否出错（仅 Assistant 消息）</summary>
    public bool IsError { get; set; } = false;

    /// <summary>错误信息（仅 IsError=true 时）</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 工具调用记录列表（仅 Assistant 消息，调过工具才有）
    /// ★ 2026-10-09 加：之前只存 Content 不存 tool 列表，关掉重开后工具卡片丢失
    ///   持久化到 LocalChatStore，加载时由 CreateBubbleRow 渲染成 cards 进 #tools-stream
    /// </summary>
    public List<ChatToolCallRecord>? ToolCalls { get; set; }
}

/// <summary>
/// 会话（多个 ChatMessage 的容器）
/// </summary>
public class ChatSession
{
    /// <summary>会话唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>会话标题（默认取首条 User 消息前 30 字）</summary>
    public string Title { get; set; } = "新会话";

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>最后活跃时间（UTC）</summary>
    public DateTime LastActiveAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>消息列表（按时间正序）</summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>使用的厂商 ID（可中途切换）</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>使用的模型名（可中途切换）</summary>
    public string Model { get; set; } = string.Empty;
}
