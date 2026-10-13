using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 内存会话存储（关闭程序后全部丢失）
/// <para>★ 2026-08-26 备选策略：适合敏感对话不愿落盘的场景</para>
/// <para>★ 2026-10-13 同步 IChatStore 新接口：GetSessionTail/GetSessionMessagesRange/GetSessionMetadata</para>
/// </summary>
public class MemoryChatStore : IChatStore
{
    private readonly List<ChatSession> _sessions = new();
    private readonly object _lock = new();

    public List<ChatSession> GetSessions()
    {
        lock (_lock)
        {
            return _sessions
                .Select(CloneSessionMeta)
                .OrderByDescending(s => s.LastActiveAtUtc)
                .ToList();
        }
    }

    public ChatSession? GetSessionMetadata(string sessionId)
    {
        lock (_lock)
        {
            var s = _sessions.FirstOrDefault(x => x.Id == sessionId);
            return s == null ? null : CloneSessionMeta(s);
        }
    }

    public List<ChatMessage> GetSessionTail(string sessionId, int count)
    {
        if (count <= 0) return new List<ChatMessage>();
        lock (_lock)
        {
            var s = _sessions.FirstOrDefault(x => x.Id == sessionId);
            if (s == null) return new List<ChatMessage>();
            return s.Messages.Skip(Math.Max(0, s.Messages.Count - count))
                .Select(CloneMessage).ToList();
        }
    }

    public List<ChatMessage> GetSessionMessagesRange(string sessionId, int skip, int take)
    {
        lock (_lock)
        {
            var s = _sessions.FirstOrDefault(x => x.Id == sessionId);
            if (s == null) return new List<ChatMessage>();
            var query = s.Messages.Skip(Math.Max(0, skip));
            if (take >= 0) query = query.Take(take);
            return query.Select(CloneMessage).ToList();
        }
    }

    public ChatSession? GetSession(string sessionId)
    {
        lock (_lock)
        {
            var s = _sessions.FirstOrDefault(x => x.Id == sessionId);
            return s == null ? null : CloneSession(s);
        }
    }

    public void SaveSession(ChatSession session)
    {
        lock (_lock)
        {
            var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Title = session.Title;
                existing.LastActiveAtUtc = session.LastActiveAtUtc;
                existing.ProviderId = session.ProviderId;
                existing.Model = session.Model;
                existing.MessagesCount = session.Messages?.Count ?? 0;
                existing.Messages = CloneMessages(session.Messages);
            }
            else
            {
                var copy = CloneSession(session);
                copy.MessagesCount = copy.Messages.Count;
                _sessions.Add(copy);
            }
        }
    }

    public void DeleteSession(string sessionId)
    {
        lock (_lock)
        {
            _sessions.RemoveAll(s => s.Id == sessionId);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _sessions.Clear();
        }
    }

    /// <summary>复制元数据（Messages 列表为空）</summary>
    private static ChatSession CloneSessionMeta(ChatSession src) => new()
    {
        Id = src.Id,
        Title = src.Title,
        CreatedAtUtc = src.CreatedAtUtc,
        LastActiveAtUtc = src.LastActiveAtUtc,
        ProviderId = src.ProviderId,
        Model = src.Model,
        MessagesCount = src.MessagesCount,
        Messages = new List<ChatMessage>()
    };

    /// <summary>完整复制（含 Messages 全部）</summary>
    private static ChatSession CloneSession(ChatSession src) => new()
    {
        Id = src.Id,
        Title = src.Title,
        CreatedAtUtc = src.CreatedAtUtc,
        LastActiveAtUtc = src.LastActiveAtUtc,
        ProviderId = src.ProviderId,
        Model = src.Model,
        MessagesCount = src.Messages?.Count ?? 0,
        Messages = CloneMessages(src.Messages)
    };

    private static List<ChatMessage> CloneMessages(IEnumerable<ChatMessage> src) =>
        src.Select(CloneMessage).ToList();

    private static ChatMessage CloneMessage(ChatMessage m) => new()
    {
        Id = m.Id,
        SessionId = m.SessionId,
        Role = m.Role,
        Content = m.Content,
        CreatedAtUtc = m.CreatedAtUtc,
        ProviderName = m.ProviderName,
        Model = m.Model,
        TokensUsed = m.TokensUsed,
        IsError = m.IsError,
        ErrorMessage = m.ErrorMessage,
        ToolCalls = m.ToolCalls
    };
}
