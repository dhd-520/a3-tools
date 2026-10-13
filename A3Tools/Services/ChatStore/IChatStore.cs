using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 会话存储接口（策略模式：Local / Memory / Disabled 三选一）
/// <para>★ 2026-10-13 陛下要求：历史会话渲染优化（B+C 方案：按需加载 + 拆文件存储）</para>
/// <list type="bullet">
///   <item>GetSessions() / GetSessionMetadata() 只读 index，不加载消息内容 → 切侧边栏不卡</item>
///   <item>GetSessionTail(id, n) 只读最后 n 条消息 → 切会话时只渲染底部 N 条</item>
///   <item>GetSessionMessagesRange(id, skip, take) 按需切片 → 向上滚动加载更多</item>
///   <item>GetSession(id) 仍可用但会加载全部消息（导出/全量读取场景）</item>
/// </list>
/// </summary>
public interface IChatStore
{
    /// <summary>
    /// 获取所有会话（仅元数据：Title/CreatedAtUtc/LastActiveAtUtc/ProviderId/Model/MessagesCount；
    /// Messages 列表始终为空，需用 GetSessionTail/Range 拉消息）
    /// </summary>
    List<ChatSession> GetSessions();

    /// <summary>
    /// 获取单个会话的元数据（Messages 列表为空，Title/CreatedAt/LastActive/Provider/Model 都填）
    /// <para>用于「切会话时同步选中状态」无需加载任何消息</para>
    /// </summary>
    ChatSession? GetSessionMetadata(string sessionId);

    /// <summary>
    /// ★ 倒序渲染关键 API：只读目标会话的最后 N 条消息（按时间正序，最新在最后）
    /// <para>切会话初始渲染时调：GetSessionTail(id, InitialWindowSize=10) → 只解密 10 条</para>
    /// </summary>
    List<ChatMessage> GetSessionTail(string sessionId, int count);

    /// <summary>
    /// ★ 向上加载更多 API：按 skip/take 切片（skip 0 = 从第一条开始；take=-1 = 全部）
    /// <para>LoadOlderMessages 时调：GetSessionMessagesRange(id, newStart, count)</para>
    /// </summary>
    List<ChatMessage> GetSessionMessagesRange(string sessionId, int skip, int take);

    /// <summary>
    /// 获取单个会话的完整内容（含全部消息）—— 一般不用，建议用 GetSessionTail/Range 替代
    /// <para>仅在「导出/全量编辑」等需要完整数据的场景使用</para>
    /// </summary>
    ChatSession? GetSession(string sessionId);

    /// <summary>保存/更新会话（自动维护 index + messages 文件）</summary>
    void SaveSession(ChatSession session);

    /// <summary>删除会话（同时清 index + messages 文件）</summary>
    void DeleteSession(string sessionId);

    /// <summary>清空全部</summary>
    void ClearAll();
}
