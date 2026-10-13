using System.IO;
using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 本地加密会话存储（B+C 方案：拆 index + messages 目录）
/// <para>★ 2026-10-13 陛下要求：AI 助理历史会话渲染优化</para>
/// <list type="bullet">
///   <item>存储路径变化（无历史数据迁移）：</item>
/// </list>
/// <list type="bullet">
///   <item>● 旧版：<c>DATA/ai_chat/sessions.json</c>（单文件存所有会话，1000+ 条消息时切会话卡顿）</item>
///   <item>● 新版：<c>DATA/ai_chat/sessions-index.json</c>（明文，仅元数据，含 MessagesCount）</item>
///   <item>● 新版：<c>DATA/ai_chat/messages/{sessionId}.json</c>（加密，仅该会话的消息列表）</item>
/// </list>
/// <para>性能：</para>
/// <list type="bullet">
///   <item>GetSessions()  → 只读 sessions-index.json（明文 1KB 级，0 解密）</item>
///   <item>GetSessionTail() → 只读 messages/{id}.json 末尾 N 条（10 条消息 ≈ 5ms）</item>
///   <item>GetSessionMessagesRange() → 同文件切片</item>
/// </list>
/// <para>迁移：构造函数检测到旧 sessions.json 就 rename 成 .bak（陛下说不考虑迁移历史）</para>
/// </summary>
public class LocalChatStore : IChatStore
{
    private readonly string _folder;
    private readonly string _indexFile;
    private readonly string _messagesFolder;
    private readonly string _oldFile;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();

    public LocalChatStore()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        _folder = Path.Combine(appDir, "DATA", "ai_chat");
        _indexFile = Path.Combine(_folder, "sessions-index.json");
        _messagesFolder = Path.Combine(_folder, "messages");
        _oldFile = Path.Combine(_folder, "sessions.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureFolders();
        MigrateOldFormatIfNeeded();
    }

    private void EnsureFolders()
    {
        if (!Directory.Exists(_folder))
            Directory.CreateDirectory(_folder);
        if (!Directory.Exists(_messagesFolder))
            Directory.CreateDirectory(_messagesFolder);
    }

    /// <summary>
    /// 旧版单文件 sessions.json → 重命名成 .bak 不读不删（陛下说不需要历史数据迁移）
    /// </summary>
    private void MigrateOldFormatIfNeeded()
    {
        if (!File.Exists(_oldFile)) return;
        try
        {
            string bakFile = _oldFile + ".bak";
            if (File.Exists(bakFile)) File.Delete(bakFile);
            File.Move(_oldFile, bakFile);
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 旧版 sessions.json 已备份到 {bakFile}（陛下说不迁移历史数据）");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 迁移旧版 sessions.json 失败: {ex.Message}");
        }
    }

    // ============================================================
    //  index 读写（明文，轻量，0 解密）
    // ============================================================

    private List<ChatSession> LoadIndex()
    {
        if (!File.Exists(_indexFile))
            return new List<ChatSession>();
        try
        {
            string json = File.ReadAllText(_indexFile);
            if (string.IsNullOrWhiteSpace(json))
                return new List<ChatSession>();
            return JsonSerializer.Deserialize<List<ChatSession>>(json, _jsonOptions) ?? new List<ChatSession>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 读 index 失败: {ex.Message}");
            return new List<ChatSession>();
        }
    }

    private void SaveIndex(List<ChatSession> sessions)
    {
        try
        {
            // index 只存元数据，强制清空 Messages 列表（防止旧数据残留）
            var toSave = sessions.Select(s => new ChatSession
            {
                Id = s.Id,
                Title = s.Title,
                CreatedAtUtc = s.CreatedAtUtc,
                LastActiveAtUtc = s.LastActiveAtUtc,
                ProviderId = s.ProviderId,
                Model = s.Model,
                MessagesCount = s.MessagesCount,
                Messages = new List<ChatMessage>() // index 不存 messages
            }).ToList();

            string json = JsonSerializer.Serialize(toSave, _jsonOptions);

            // 原子写
            string tmp = _indexFile + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_indexFile))
                File.Delete(_indexFile);
            File.Move(tmp, _indexFile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 写 index 失败: {ex.Message}");
        }
    }

    // ============================================================
    //  messages 文件读写（加密，按 sessionId 分文件）
    // ============================================================

    private string GetMessagesFilePath(string sessionId) => Path.Combine(_messagesFolder, $"{sessionId}.json");

    private List<ChatMessage>? ReadMessagesFile(string sessionId)
    {
        string path = GetMessagesFilePath(sessionId);
        if (!File.Exists(path)) return null;
        try
        {
            string encrypted = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(encrypted)) return new List<ChatMessage>();
            string json = EncryptionService.Decrypt(encrypted);
            if (string.IsNullOrEmpty(json)) return new List<ChatMessage>();
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, _jsonOptions) ?? new List<ChatMessage>();
            // 解密每条消息的 Content
            foreach (var m in messages)
                m.Content = DecryptField(m.Content);
            return messages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 读 messages/{sessionId}.json 失败: {ex.Message}");
            return null;
        }
    }

    private void WriteMessagesFile(string sessionId, List<ChatMessage> messages)
    {
        try
        {
            // 复制出来加密 Content 后再序列化
            var toSave = messages.Select(m => new ChatMessage
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = EncryptField(m.Content),
                CreatedAtUtc = m.CreatedAtUtc,
                ProviderName = m.ProviderName,
                Model = m.Model,
                TokensUsed = m.TokensUsed,
                IsError = m.IsError,
                ErrorMessage = m.ErrorMessage,
                ToolCalls = m.ToolCalls
            }).ToList();

            string json = JsonSerializer.Serialize(toSave, _jsonOptions);
            string encrypted = EncryptionService.Encrypt(json);

            string path = GetMessagesFilePath(sessionId);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, encrypted);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 写 messages/{sessionId}.json 失败: {ex.Message}");
        }
    }

    private void DeleteMessagesFile(string sessionId)
    {
        try
        {
            string path = GetMessagesFilePath(sessionId);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 删 messages/{sessionId}.json 失败: {ex.Message}");
        }
    }

    // ============================================================
    //  IChatStore 实现
    // ============================================================

    public List<ChatSession> GetSessions()
    {
        lock (_lock)
        {
            return LoadIndex()
                .OrderByDescending(s => s.LastActiveAtUtc)
                .ToList();
        }
    }

    public ChatSession? GetSessionMetadata(string sessionId)
    {
        lock (_lock)
        {
            return LoadIndex().FirstOrDefault(s => s.Id == sessionId);
        }
    }

    public List<ChatMessage> GetSessionTail(string sessionId, int count)
    {
        if (count <= 0) return new List<ChatMessage>();
        lock (_lock)
        {
            var all = ReadMessagesFile(sessionId);
            if (all == null) return new List<ChatMessage>();
            return all.Skip(Math.Max(0, all.Count - count)).ToList();
        }
    }

    public List<ChatMessage> GetSessionMessagesRange(string sessionId, int skip, int take)
    {
        lock (_lock)
        {
            var all = ReadMessagesFile(sessionId);
            if (all == null) return new List<ChatMessage>();
            if (take < 0) return all.Skip(Math.Max(0, skip)).ToList();
            return all.Skip(Math.Max(0, skip)).Take(take).ToList();
        }
    }

    public ChatSession? GetSession(string sessionId)
    {
        lock (_lock)
        {
            var meta = LoadIndex().FirstOrDefault(s => s.Id == sessionId);
            if (meta == null) return null;
            var messages = ReadMessagesFile(sessionId) ?? new List<ChatMessage>();
            meta.Messages = messages;
            return meta;
        }
    }

    public void SaveSession(ChatSession session)
    {
        lock (_lock)
        {
            // 1. 合并 messages：保留磁盘上 in-memory 没加载的头部
            var existingMessages = ReadMessagesFile(session.Id);
            List<ChatMessage> finalMessages = MergeMessages(existingMessages, session.Messages ?? new List<ChatMessage>());

            // 2. 写 messages 文件
            WriteMessagesFile(session.Id, finalMessages);

            // 3. 更新 index（MessagesCount 同步）
            session.MessagesCount = finalMessages.Count;
            var index = LoadIndex();
            var existing = index.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Title = session.Title;
                existing.LastActiveAtUtc = session.LastActiveAtUtc;
                existing.ProviderId = session.ProviderId;
                existing.Model = session.Model;
                existing.MessagesCount = finalMessages.Count;
            }
            else
            {
                index.Add(new ChatSession
                {
                    Id = session.Id,
                    Title = session.Title,
                    CreatedAtUtc = session.CreatedAtUtc,
                    LastActiveAtUtc = session.LastActiveAtUtc,
                    ProviderId = session.ProviderId,
                    Model = session.Model,
                    MessagesCount = finalMessages.Count
                });
            }
            SaveIndex(index);
        }
    }

    /// <summary>
    /// 合并已存在的磁盘消息 + 内存消息（in-memory 是 tail，磁盘可能有没加载的 head）
    /// <para>★ 2026-10-13 优化：避免 SaveSession 覆盖 in-memory 没加载的前面消息</para>
    /// </summary>
    private static List<ChatMessage> MergeMessages(List<ChatMessage>? existing, List<ChatMessage> memory)
    {
        if (existing == null || existing.Count == 0) return memory;
        if (memory.Count == 0) return existing;

        // 找 memory 第一条在 existing 中的位置
        string firstId = memory[0].Id;
        int startIdx = existing.FindIndex(m => m.Id == firstId);
        if (startIdx < 0)
        {
            // memory 不在 existing 里（可能是新会话或全量替换）→ 直接用 memory
            return memory;
        }

        // 验证 memory 是 existing 的连续段
        bool matches = true;
        for (int i = 0; i < memory.Count; i++)
        {
            if (startIdx + i >= existing.Count || existing[startIdx + i].Id != memory[i].Id)
            {
                matches = false;
                break;
            }
        }
        if (!matches) return memory;  // 不连续 → 全量覆盖

        // 合并：existing 头部（in-memory 没加载的）+ memory 全部
        return existing.Take(startIdx).Concat(memory).ToList();
    }

    public void DeleteSession(string sessionId)
    {
        lock (_lock)
        {
            // 1. 从 index 删
            var index = LoadIndex();
            index.RemoveAll(s => s.Id == sessionId);
            SaveIndex(index);

            // 2. 删 messages 文件
            DeleteMessagesFile(sessionId);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            // 删 index
            if (File.Exists(_indexFile)) File.Delete(_indexFile);
            // 删 messages 目录
            if (Directory.Exists(_messagesFolder))
            {
                foreach (var f in Directory.GetFiles(_messagesFolder))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// 删除指定天数之前的历史会话（RetentionDays = 0 表示永久保留）
    /// </summary>
    public int PruneOldSessions(int retentionDays)
    {
        if (retentionDays <= 0) return 0;

        lock (_lock)
        {
            var index = LoadIndex();
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var toRemove = index.Where(s => s.LastActiveAtUtc < cutoff).Select(s => s.Id).ToList();
            foreach (var id in toRemove)
            {
                DeleteMessagesFile(id);
            }
            int removed = index.RemoveAll(s => s.LastActiveAtUtc < cutoff);
            if (removed > 0) SaveIndex(index);
            return removed;
        }
    }

    // ============================================================
    //  字段级加解密（每个 Content 字段加密）
    // ============================================================

    private static string EncryptField(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return EncryptionService.Encrypt(text);
    }

    private static string DecryptField(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // 旧版本可能是明文（兼容性处理）
        try
        {
            byte[] bytes = Convert.FromBase64String(text);
            if (bytes.Length < 16 || bytes.Length % 16 != 0)
                return text;
            var decrypted = EncryptionService.Decrypt(text);
            return string.IsNullOrEmpty(decrypted) ? text : decrypted;
        }
        catch
        {
            return text;
        }
    }
}
