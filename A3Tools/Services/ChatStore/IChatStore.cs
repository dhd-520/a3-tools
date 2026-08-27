using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 会话存储接口（策略模式：Local / Memory / Disabled 三选一）
/// </summary>
public interface IChatStore
{
    /// <summary>获取所有会话（按 LastActiveAtUtc 倒序）</summary>
    List<ChatSession> GetSessions();

    /// <summary>按 ID 获取单个会话</summary>
    ChatSession? GetSession(string sessionId);

    /// <summary>保存/更新会话</summary>
    void SaveSession(ChatSession session);

    /// <summary>删除会话</summary>
    void DeleteSession(string sessionId);

    /// <summary>清空全部</summary>
    void ClearAll();
}
