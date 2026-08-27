using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 内存会话存储（关闭程序后全部丢失）
/// <para>★ 2026-08-26 备选策略：适合敏感对话不愿落盘的场景</para>
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
                .Select(CloneSession)
                .OrderByDescending(s => s.LastActiveAtUtc)
                .ToList();
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
                existing.Messages = CloneMessages(session.Messages);
            }
            else
            {
                _sessions.Add(CloneSession(session));
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

    private static ChatSession CloneSession(ChatSession src) => new()
    {
        Id = src.Id,
        Title = src.Title,
        CreatedAtUtc = src.CreatedAtUtc,
        LastActiveAtUtc = src.LastActiveAtUtc,
        ProviderId = src.ProviderId,
        Model = src.Model,
        Messages = CloneMessages(src.Messages)
    };

    private static List<ChatMessage> CloneMessages(IEnumerable<ChatMessage> src) =>
        src.Select(m => new ChatMessage
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
            ErrorMessage = m.ErrorMessage
        }).ToList();
}
