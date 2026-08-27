using System.IO;
using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services.ChatStore;

/// <summary>
/// 本地加密会话存储
/// <para>★ 2026-08-26 陛下要求：默认存储策略，对话内容 AES 加密落盘</para>
/// <para>存储路径：<c>DATA/ai_chat/sessions.json</c>（单文件存所有会话）</para>
/// </summary>
public class LocalChatStore : IChatStore
{
    private readonly string _folder;
    private readonly string _file;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();

    public LocalChatStore()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        _folder = Path.Combine(appDir, "DATA", "ai_chat");
        _file = Path.Combine(_folder, "sessions.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false, // 加密后不缩进，体积小
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureFolder();
    }

    private void EnsureFolder()
    {
        if (!Directory.Exists(_folder))
            Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// 读取所有会话（解密内容）
    /// </summary>
    private List<ChatSession> LoadAll()
    {
        if (!File.Exists(_file))
            return new List<ChatSession>();

        try
        {
            string encryptedJson = File.ReadAllText(_file);
            if (string.IsNullOrWhiteSpace(encryptedJson))
                return new List<ChatSession>();

            // 解密外层文件内容
            string json = EncryptionService.Decrypt(encryptedJson);
            if (string.IsNullOrEmpty(json))
                return new List<ChatSession>();

            var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json, _jsonOptions) ?? new List<ChatSession>();

            // 解密每条消息的内容
            foreach (var s in sessions)
            {
                foreach (var m in s.Messages)
                {
                    m.Content = DecryptField(m.Content);
                }
            }

            return sessions;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 读取失败: {ex.Message}");
            return new List<ChatSession>();
        }
    }

    /// <summary>
    /// 写入所有会话（加密内容）
    /// </summary>
    private void SaveAll(List<ChatSession> sessions)
    {
        try
        {
            // 复制出来加密后再序列化（不动原对象）
            var toSave = sessions.Select(s => new ChatSession
            {
                Id = s.Id,
                Title = s.Title,
                CreatedAtUtc = s.CreatedAtUtc,
                LastActiveAtUtc = s.LastActiveAtUtc,
                ProviderId = s.ProviderId,
                Model = s.Model,
                Messages = s.Messages.Select(m => new ChatMessage
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
                    ErrorMessage = m.ErrorMessage
                }).ToList()
            }).ToList();

            string json = JsonSerializer.Serialize(toSave, _jsonOptions);
            string encrypted = EncryptionService.Encrypt(json);

            // 原子写：先写临时文件再改名，避免崩溃时损坏
            string tmp = _file + ".tmp";
            File.WriteAllText(tmp, encrypted);
            if (File.Exists(_file))
                File.Delete(_file);
            File.Move(tmp, _file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalChatStore] 写入失败: {ex.Message}");
        }
    }

    public List<ChatSession> GetSessions()
    {
        lock (_lock)
        {
            return LoadAll()
                .OrderByDescending(s => s.LastActiveAtUtc)
                .ToList();
        }
    }

    public ChatSession? GetSession(string sessionId)
    {
        lock (_lock)
        {
            return LoadAll().FirstOrDefault(s => s.Id == sessionId);
        }
    }

    public void SaveSession(ChatSession session)
    {
        lock (_lock)
        {
            var all = LoadAll();
            var existing = all.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Title = session.Title;
                existing.LastActiveAtUtc = session.LastActiveAtUtc;
                existing.ProviderId = session.ProviderId;
                existing.Model = session.Model;
                existing.Messages = session.Messages;
            }
            else
            {
                all.Add(session);
            }
            SaveAll(all);
        }
    }

    public void DeleteSession(string sessionId)
    {
        lock (_lock)
        {
            var all = LoadAll();
            all.RemoveAll(s => s.Id == sessionId);
            SaveAll(all);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            if (File.Exists(_file))
                File.Delete(_file);
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
            var all = LoadAll();
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            int removed = all.RemoveAll(s => s.LastActiveAtUtc < cutoff);
            if (removed > 0)
                SaveAll(all);
            return removed;
        }
    }

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
