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

    // ━━━━━━━━━━━━━━━━ AI 提取（R3 2026-09-01 陛下要求）━━━━━━━━━━━━━━━

    /// <summary>AI 提取结果汇总</summary>
    public class AiExtractSummary
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>单文件提取进度</summary>
    public class AiExtractProgress
    {
        public int Index { get; set; }      // 当前处理第几个(1-based)
        public int Total { get; set; }       // 总文件数
        public string FileName { get; set; } = "";
        public string Status { get; set; } = "";  // "读取中" / "提取中" / "完成" / "失败"
    }

    /// <summary>AI 提取提示词模板（强调提炼 + 尊重原始内容）</summary>
    private const string AiExtractSystemPrompt = @"你是 A3Tools 知识库提取助手。请把以下文档提炼为结构化 Markdown 知识条目。

【要求】
1. 提炼关键概念、操作步骤、参数、注意事项 — 不要大段复制原文
2. 重要控制控控在原文 30% 长度以内，以精炼为优先
3. 使用 `##` 二级标题 + `###` 三级标题组织章节
4. 列表用 `-` 5. 关键术语加 `【重点】` 标记6. 如有原始表格，用 `│` `─┼─` 制表符重新对齐
7. 开头输出一行 `> 来源：<filename>` 标记出处

【输出】仅输出提炼后的 Markdown，不要额外说明。";

    /// <summary>从文件夹 AI 提取为知识库条目</summary>
    /// <param name="baseId">目标知识库 ID</param>
    /// <param name="folderPath">要扫描的文件夹路径（为空则用 KB 的 WatchFolder）</param>
    /// <param name="provider">AI 供应商配置</param>
    /// <param name="backend">AI 后端</param>
    /// <param name="progress">进度回调</param>
    /// <param name="ct">取消令牌</param>
    public async Task<AiExtractSummary> AiExtractFromFolderAsync(
        string baseId,
        string? folderPath,
        Models.AiProviderConfig provider,
        OpenAiCompatibleBackend backend,
        IProgress<AiExtractProgress>? progress = null,
        CancellationToken ct = default)
    {
        var summary = new AiExtractSummary();
        var kb = GetBase(baseId);
        if (kb == null)
        {
            summary.Errors.Add($"知识库 {baseId} 不存在");
            return summary;
        }

        var folder = folderPath ?? kb.WatchFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            summary.Errors.Add($"文件夹不存在: {folder}");
            return summary;
        }

        var files = ScanFolder(folder, kb.FilePatterns, kb.Recursive);
        summary.Total = files.Count;
        if (files.Count == 0)
        {
            summary.Errors.Add("文件夹下未找到任何匹配文件");
            return summary;
        }

        // extracted 子目录存生成的 .md
        var extractedDir = Path.Combine(DataDir, kb.Id, "extracted");
        Directory.CreateDirectory(extractedDir);

        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "读取中" });

            try
            {
                var content = ReadFileContent(file.FullName);
                if (string.IsNullOrWhiteSpace(content))
                {
                    summary.Skipped++;
                    progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "跳过(空内容)" });
                    continue;
                }

                progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "AI 提炼中" });

                // 构造提示词
                var userPrompt = $"【文件名】{file.Name}\n【文档内容】\n{content}";
                var messages = new List<Models.ChatMessage>
                {
                    new() { Role = Models.ChatRole.System, Content = AiExtractSystemPrompt },
                    new() { Role = Models.ChatRole.User, Content = userPrompt },
                };

                var reply = await backend.SendAsync(provider, messages, ct);
                if (string.IsNullOrWhiteSpace(reply?.Content))
                {
                    summary.Failed++;
                    summary.Errors.Add($"{file.Name}: AI 返回为空");
                    progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "失败(空返回)" });
                    continue;
                }

                var aiContent = reply.Content.Trim();

                // 保存 .md 到 extracted 目录
                var mdName = Path.GetFileNameWithoutExtension(file.Name) + ".md";
                var mdPath = Path.Combine(extractedDir, mdName);
                await File.WriteAllTextAsync(mdPath, aiContent, ct);

                // 添加/更新 KB 条目
                var hash = ComputeHash(aiContent);
                var existing = kb.Entries.FirstOrDefault(e =>
                    e.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Title = Path.GetFileNameWithoutExtension(file.Name);
                    existing.Content = aiContent;
                    existing.ContentHash = hash;
                    existing.UpdatedAt = DateTime.UtcNow;
                    UpdateEntry(kb.Id, existing);
                }
                else
                {
                    AddEntry(kb.Id, new KnowledgeEntry
                    {
                        Title = Path.GetFileNameWithoutExtension(file.Name),
                        Content = aiContent,
                        SourceFile = file.FullName,
                        ContentHash = hash,
                        Tags = ExtractTagsFromContent(aiContent),
                    });
                }

                summary.Success++;
                progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "✅ 完成" });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.Failed++;
                summary.Errors.Add($"{file.Name}: {ex.Message}");
                progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = "❌ 失败" });
            }
        }

        return summary;
    }

    /// <summary>读取文件内容(.md/.txt 直接读,.docx 用 BCL ZipFile 解 XML 提取文本)</summary>
    public string ReadFileContent(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".md" || ext == ".txt")
            return File.ReadAllText(filePath);

        if (ext == ".docx")
        {
            // ★ 2026-09-01 陛下要求:不需要 OpenXml NuGet
            //   docx = zip,word/document.xml 里所有段落文字都在 <w:t> 标签里
            return ReadDocxText(filePath);
        }
        return File.ReadAllText(filePath);
    }

    /// <summary>从 .docx(zip)解压 word/document.xml 并拼接所有 <w:t> 文本(BCL only)</summary>
    private static string ReadDocxText(string filePath)
    {
        const string DocXml = "word/document.xml";
        const string WmlNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        using var zip = System.IO.Compression.ZipFile.OpenRead(filePath);
        var entry = zip.GetEntry(DocXml);
        if (entry == null) return $"[docx 文件 {Path.GetFileName(filePath)} 缺少 {DocXml}]";

        using var stream = entry.Open();
        var xdoc = System.Xml.Linq.XDocument.Load(stream);
        if (xdoc.Root == null) return "";

        var sb = new System.Text.StringBuilder();
        // 每个段落 <w:p> 输出一行,用 <w:t> 拼接
        foreach (var p in xdoc.Root.Descendants(System.Xml.Linq.XName.Get("p", WmlNs)))
        {
            var line = string.Concat(p.Descendants(System.Xml.Linq.XName.Get("t", WmlNs))
                                       .Select(t => (string?)t.Value ?? ""));
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
        }
        return sb.ToString();
    }

    // ━━━━━━━━━━━━━━━━ 工具方法 ━━━━━━━━━━━━━━━━

    private static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>从 Markdown 内容自动生成标签（取 ## 二级标题前 5 个）</summary>
    public static List<string> ExtractTagsFromContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new List<string>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var t = line.TrimStart();
            if (t.StartsWith("## ") && t.Length > 3)
            {
                var title = t[3..].Trim().Split(' ', '\t', '|')[0];
                if (title.Length > 0 && title.Length < 30)
                    tags.Add(title);
                if (tags.Count >= 5) break;
            }
        }
        return tags.ToList();
    }
}