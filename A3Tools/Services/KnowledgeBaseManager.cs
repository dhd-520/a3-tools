using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 知识库管理服务
/// ★ 2026-09-01 陛下要求:知识库系统(CRUD + 文件夹扫描 + 后续 AI 提取 + 检索)
///   存储位置:DATA/knowledge/{base-id}/meta.json + entries.json
/// </summary>
public class KnowledgeBaseManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>知识库根目录(相对于程序运行目录)</summary>
    public string DataDir { get; }

    public KnowledgeBaseManager(string? dataDir = null)
    {
        DataDir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "DATA", "knowledge");
        Directory.CreateDirectory(DataDir);
    }

    // ━━━━━━━━━━━━━━━━ 知识库 CRUD ━━━━━━━━━━━━━━━━

    /// <summary>列出所有知识库(不含 entries,只含元数据)</summary>
    public List<KnowledgeBase> ListBases()
    {
        var result = new List<KnowledgeBase>();
        if (!Directory.Exists(DataDir)) return result;

        foreach (var dir in Directory.GetDirectories(DataDir))
        {
            var metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath)) continue;
            try
            {
                var meta = JsonSerializer.Deserialize<KnowledgeBase>(File.ReadAllText(metaPath), JsonOpts);
                if (meta != null)
                {
                    // 列表只读元数据,entries 留空(节省内存)
                    meta.Entries = new List<KnowledgeEntry>();
                    result.Add(meta);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Knowledge] 读取 {metaPath} 失败:{ex.Message}");
            }
        }
        return result.OrderBy(b => b.Name).ToList();
    }

    /// <summary>读取单个知识库(含所有 entries)</summary>
    public KnowledgeBase? GetBase(string id)
    {
        var metaPath = Path.Combine(DataDir, id, "meta.json");
        var entriesPath = Path.Combine(DataDir, id, "entries.json");
        if (!File.Exists(metaPath)) return null;

        try
        {
            var kb = JsonSerializer.Deserialize<KnowledgeBase>(File.ReadAllText(metaPath), JsonOpts);
            if (kb == null) return null;

            if (File.Exists(entriesPath))
            {
                var entries = JsonSerializer.Deserialize<List<KnowledgeEntry>>(
                    File.ReadAllText(entriesPath), JsonOpts);
                kb.Entries = entries ?? new List<KnowledgeEntry>();
            }
            return kb;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Knowledge] 读取知识库 {id} 失败:{ex.Message}");
            return null;
        }
    }

    /// <summary>创建新知识库</summary>
    public KnowledgeBase CreateBase(string name, string description = "")
    {
        var kb = new KnowledgeBase
        {
            Name = name,
            Description = description,
        };
        SaveBase(kb);
        return kb;
    }

    /// <summary>保存知识库(写入 meta.json,entries 单独存)</summary>
    public void SaveBase(KnowledgeBase kb)
    {
        var dir = Path.Combine(DataDir, kb.Id);
        Directory.CreateDirectory(dir);

        kb.UpdatedAt = DateTime.UtcNow;

        // 拆分:meta 不含 entries,entries 单独存
        var metaCopy = new KnowledgeBase
        {
            Id = kb.Id,
            Name = kb.Name,
            Description = kb.Description,
            WatchFolder = kb.WatchFolder,
            FilePatterns = kb.FilePatterns,
            Recursive = kb.Recursive,
            CreatedAt = kb.CreatedAt,
            UpdatedAt = kb.UpdatedAt,
        };
        File.WriteAllText(Path.Combine(dir, "meta.json"),
            JsonSerializer.Serialize(metaCopy, JsonOpts));
        File.WriteAllText(Path.Combine(dir, "entries.json"),
            JsonSerializer.Serialize(kb.Entries, JsonOpts));
    }

    /// <summary>删除知识库(整个目录)</summary>
    public bool DeleteBase(string id)
    {
        var dir = Path.Combine(DataDir, id);
        if (!Directory.Exists(dir)) return false;
        try
        {
            Directory.Delete(dir, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Knowledge] 删除 {id} 失败:{ex.Message}");
            return false;
        }
    }

    // ━━━━━━━━━━━━━━━━ 条目 CRUD ━━━━━━━━━━━━━━━━

    /// <summary>添加条目(自动保存)</summary>
    public KnowledgeEntry AddEntry(string baseId, KnowledgeEntry entry)
    {
        var kb = GetBase(baseId);
        if (kb == null) throw new InvalidOperationException($"知识库 {baseId} 不存在");
        if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString("N");
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(entry.ContentHash) && !string.IsNullOrEmpty(entry.Content))
            entry.ContentHash = ComputeHash(entry.Content);

        kb.Entries.Add(entry);
        SaveBase(kb);
        return entry;
    }

    /// <summary>更新条目(自动保存)</summary>
    public bool UpdateEntry(string baseId, KnowledgeEntry entry)
    {
        var kb = GetBase(baseId);
        if (kb == null) return false;
        var existing = kb.Entries.FirstOrDefault(e => e.Id == entry.Id);
        if (existing == null) return false;

        existing.Title = entry.Title;
        existing.Content = entry.Content;
        existing.Tags = entry.Tags;
        existing.SourceFile = entry.SourceFile;
        if (!string.IsNullOrEmpty(entry.Content))
            existing.ContentHash = ComputeHash(entry.Content);
        existing.UpdatedAt = DateTime.UtcNow;

        SaveBase(kb);
        return true;
    }

    /// <summary>删除条目</summary>
    public bool DeleteEntry(string baseId, string entryId)
    {
        var kb = GetBase(baseId);
        if (kb == null) return false;
        var removed = kb.Entries.RemoveAll(e => e.Id == entryId);
        if (removed == 0) return false;
        SaveBase(kb);
        return true;
    }

    // ━━━━━━━━━━━━━━━━ 文件夹扫描 ━━━━━━━━━━━━━━━━

    /// <summary>扫描文件夹,返回匹配的文件列表(不读内容)</summary>
    public List<FileInfo> ScanFolder(string folderPath, IEnumerable<string> patterns, bool recursive = true)
    {
        var result = new List<FileInfo>();
        if (!Directory.Exists(folderPath)) return result;

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in patterns)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(folderPath, pattern, option);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Knowledge] 扫描 {pattern} 失败:{ex.Message}");
                continue;
            }
            foreach (var f in files)
            {
                if (seen.Add(f)) result.Add(new FileInfo(f));
            }
        }
        return result.OrderBy(f => f.FullName).ToList();
    }

    /// <summary>扫描知识库的 WatchFolder(如有)</summary>
    public List<FileInfo> ScanKnowledgeBaseFolder(KnowledgeBase k)
    {
        if (string.IsNullOrWhiteSpace(k.WatchFolder)) return new List<FileInfo>();
        return ScanFolder(k.WatchFolder, k.FilePatterns, k.Recursive);
    }

    /// <summary>读取文件内容(.md/.txt 直接读,.docx 待 R3 用 OpenXML SDK 实现)</summary>
    public string ReadFileContent(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".md" || ext == ".txt")
            return File.ReadAllText(filePath);

        if (ext == ".docx")
        {
            // ★ R3 待实现:用 DocumentFormat.OpenXml 提取纯文本
            //   暂时返回文件名提示
            return $"[docx 文件:{Path.GetFileName(filePath)} - 待 R3 实现 OpenXML 解析]";
        }
        return File.ReadAllText(filePath);
    }

    // ━━━━━━━━━━━━━━━━ 工具方法 ━━━━━━━━━━━━━━━━

    private static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}