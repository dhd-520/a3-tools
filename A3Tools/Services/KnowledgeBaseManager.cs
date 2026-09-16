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
            entry.ContentHash = ComputeHashStatic(entry.Content);

        kb.Entries.Add(entry);
        SaveBase(kb);
        return entry;
    }

    /// <summary>快捷添加条目并指定来源类型(供 UI/扫描/AI 提取使用)</summary>
    public KnowledgeEntry AddEntry(string baseId, KnowledgeSourceType sourceType, string title, string content,
        string sourceFile = "", string sourceReference = "", List<string>? tags = null)
    {
        return AddEntry(baseId, new KnowledgeEntry
        {
            Title = title,
            Content = content,
            SourceFile = sourceFile,
            SourceType = sourceType,
            SourceReference = sourceReference,
            Tags = tags ?? new List<string>(),
        });
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
            existing.ContentHash = ComputeHashStatic(entry.Content);
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

    /// <summary>批量删除条目。返回实际删除数。</summary>
    public int DeleteEntries(string baseId, IEnumerable<string> entryIds)
    {
        var kb = GetBase(baseId);
        if (kb == null) return 0;
        var ids = new HashSet<string>(entryIds);
        var removed = kb.Entries.RemoveAll(e => ids.Contains(e.Id));
        if (removed == 0) return 0;
        SaveBase(kb);
        return removed;
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

    // ━━━━━━━━━━━━━━━━ 导入导出（R1 2026-09-08 陛下要求：跨机器）━━━━━━━━━━━━━━━

    /// <summary>JSON 中用于识别导出类型的字段名</summary>
    private const string DiscriminatorField = "$type";
    private const string SingleBaseType = "a3kb-base-v1";
    private const string AllBasesType = "a3kb-all-v1";

    /// <summary>单库导出顶层 DTO（绕过 base 是 C# 关键字的问题）</summary>
    private class SingleExportDto
    {
        [JsonPropertyName("$type")] public string Type { get; set; } = "";
        [JsonPropertyName("exportedAt")] public string ExportedAt { get; set; } = "";
        [JsonPropertyName("exportedFromVersion")] public string ExportedFromVersion { get; set; } = "";
        [JsonPropertyName("library")] public KnowledgeBase? Library { get; set; }
    }

    /// <summary>多库导出顶层 DTO</summary>
    private class AllExportDto
    {
        [JsonPropertyName("$type")] public string Type { get; set; } = "";
        [JsonPropertyName("exportedAt")] public string ExportedAt { get; set; } = "";
        [JsonPropertyName("exportedFromVersion")] public string ExportedFromVersion { get; set; } = "";
        [JsonPropertyName("libraries")] public List<KnowledgeBase> Libraries { get; set; } = new();
    }

    /// <summary>导出汇总</summary>
    public class ExportSummary
    {
        public int TotalBases { get; set; }
        public int TotalEntries { get; set; }
        public long FileSizeBytes { get; set; }
    }

    /// <summary>单个知识库的导入冲突详情</summary>
    public class ImportConflict
    {
        /// <summary>原知识库名称（导出文件里的）</summary>
        public string OriginalName { get; set; } = "";
        /// <summary>导入后最终名称（重名时加 "(导入)" 后缀或 (导入 N) 后缀）</summary>
        public string FinalName { get; set; } = "";
        /// <summary>导入后该库的新 ID</summary>
        public string NewBaseId { get; set; } = "";
        /// <summary>重名原因：Name（同名）、Id（同一个库重复导入）</summary>
        public string Reason { get; set; } = "";
        /// <summary>该库包含的条目数</summary>
        public int EntryCount { get; set; }
    }

    /// <summary>导入汇总</summary>
    public class ImportSummary
    {
        public bool Success { get; set; }
        public int TotalBases { get; set; }
        public int SuccessBases { get; set; }
        public int RenamedBases { get; set; }
        public List<ImportConflict> Conflicts { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 导出单个知识库到 JSON 字符串。
    /// 设计：扁平化单 json（陛下拍板，便于 Git/diff/邮件）。
    ///   - $type=a3kb-base-v1 标识单库导出
    ///   - watchFolder 跨机器时手动重设（不在导出阶段动，保留原值供陛下参考）
    ///   - entries 全部序列化（包括 content/tags/sourceType 等）
    /// </summary>
    public string ExportBaseToJson(string baseId)
    {
        var kb = GetBase(baseId);
        if (kb == null) throw new InvalidOperationException($"知识库 [{baseId}] 不存在");

        var dto = new SingleExportDto
        {
            Type = SingleBaseType,
            ExportedAt = DateTime.UtcNow.ToString("o"),
            ExportedFromVersion = "A3Tools/2.5.0",
            Library = kb,
        };
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    /// <summary>导出所有知识库到 JSON 字符串</summary>
    public string ExportAllToJson()
    {
        var bases = new List<KnowledgeBase>();
        foreach (var meta in ListBases())
        {
            var kb = GetBase(meta.Id);
            if (kb != null) bases.Add(kb);
        }

        var dto = new AllExportDto
        {
            Type = AllBasesType,
            ExportedAt = DateTime.UtcNow.ToString("o"),
            ExportedFromVersion = "A3Tools/2.5.0",
            Libraries = bases,
        };
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    /// <summary>导出单个知识库到文件，返回摘要</summary>
    public ExportSummary ExportBaseToFile(string baseId, string filePath)
    {
        var json = ExportBaseToJson(baseId);
        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        var kb = GetBase(baseId);
        return new ExportSummary
        {
            TotalBases = 1,
            TotalEntries = kb?.Entries.Count ?? 0,
            FileSizeBytes = new FileInfo(filePath).Length,
        };
    }

    /// <summary>导出所有知识库到文件</summary>
    public ExportSummary ExportAllToFile(string filePath)
    {
        var json = ExportAllToJson();
        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

        int totalBases = 0, totalEntries = 0;
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("libraries", out var basesArr))
        {
            totalBases = basesArr.GetArrayLength();
            foreach (var b in basesArr.EnumerateArray())
                if (b.TryGetProperty("entries", out var entries))
                    totalEntries += entries.GetArrayLength();
        }

        return new ExportSummary
        {
            TotalBases = totalBases,
            TotalEntries = totalEntries,
            FileSizeBytes = new FileInfo(filePath).Length,
        };
    }

    /// <summary>
    /// 从 JSON 字符串导入。
    /// 规则（陛下拍板 2026-09-08）：
    ///   1. 自动重生成 ID（避免与现有库 ID 冲突）
    ///   2. 重名自动加 " (导入)" / " (导入 N)" 后缀（库名作为查找键）
    ///   3. WatchFolder 跨机器自动清空，让陛下手动重设
    ///   4. SourceFile 路径保留（参考用，在新机器上扫描不到是预期）
    ///   5. 条目 ID 也重生成（避免与其他库的 ID 冲突）
    /// </summary>
    public ImportSummary ImportFromJson(string json)
    {
        var summary = new ImportSummary();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("$type", out var typeProp))
            {
                summary.Errors.Add("JSON 缺少 $type 字段，不是合法的 A3Tools 知识库导出文件");
                return summary;
            }
            string type = typeProp.GetString() ?? "";

            // 收集现有库名（用于重名检测）
            var existing = ListBases();
            var existingNames = new HashSet<string>(existing.Select(b => b.Name), StringComparer.OrdinalIgnoreCase);

            if (type == SingleBaseType)
            {
                if (!doc.RootElement.TryGetProperty("library", out var baseEl))
                {
                    summary.Errors.Add("a3kb-base-v1 缺少 library 字段");
                    return summary;
                }
                summary.TotalBases = 1;
                ImportOneBase(baseEl, existingNames, summary);
                summary.SuccessBases = summary.Conflicts.Count;
            }
            else if (type == AllBasesType)
            {
                if (!doc.RootElement.TryGetProperty("libraries", out var basesEl))
                {
                    summary.Errors.Add("a3kb-all-v1 缺少 libraries 字段");
                    return summary;
                }
                summary.TotalBases = basesEl.GetArrayLength();
                foreach (var baseEl in basesEl.EnumerateArray())
                {
                    ImportOneBase(baseEl, existingNames, summary);
                }
                summary.SuccessBases = summary.Conflicts.Count;
            }
            else
            {
                summary.Errors.Add($"未知的 $type：{type}（只支持 a3kb-base-v1 / a3kb-all-v1）");
                return summary;
            }

            summary.Success = summary.Errors.Count == 0 && summary.SuccessBases == summary.TotalBases;
            return summary;
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"解析 JSON 失败：{ex.Message}");
            return summary;
        }
    }

    /// <summary>从文件导入</summary>
    public ImportSummary ImportFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImportSummary
            {
                Errors = { $"文件不存在：{filePath}" }
            };
        }
        string json;
        try
        {
            json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return new ImportSummary
            {
                Errors = { $"读取文件失败：{ex.Message}" }
            };
        }
        return ImportFromJson(json);
    }

    /// <summary>
    /// 导入单个知识库（内部使用），返回冲突信息。
    /// ★ 2026-09-08 关键决策：
    ///   - ID 始终重生成（跨机器安全 + 避免与现有 ID 冲突）
    ///   - Name 冲突时加 " (导入)" / " (导入 2)" / " (导入 3)" ...
    ///   - watchFolder 强制清空（跨机器路径不存在）
    ///   - entries 内部 ID 也重生成
    /// </summary>
    private void ImportOneBase(JsonElement baseEl, HashSet<string> existingNames, ImportSummary summary)
    {
        try
        {
            var kb = JsonSerializer.Deserialize<KnowledgeBase>(baseEl.GetRawText(), JsonOpts);
            if (kb == null)
            {
                summary.Errors.Add("反序列化失败（base 节点）");
                return;
            }

            string originalName = kb.Name ?? "";
            string reason = "";

            // 1. 重名检测
            string finalName = originalName;
            if (existingNames.Contains(finalName))
            {
                reason = "Name";
                int suffix = 1;
                while (true)
                {
                    string candidate = suffix == 1
                        ? $"{originalName} (导入)"
                        : $"{originalName} (导入 {suffix})";
                    if (!existingNames.Contains(candidate))
                    {
                        finalName = candidate;
                        break;
                    }
                    suffix++;
                    if (suffix > 99)
                    {
                        summary.Errors.Add($"库名「{originalName}」冲突超过 99 次，放弃");
                        return;
                    }
                }
                summary.RenamedBases++;
                existingNames.Add(finalName);
            }

            // 2. ID 重生成
            string newId = Guid.NewGuid().ToString("N");

            // 3. WatchFolder 跨机器清空（陛下拍板）
            kb.WatchFolder = "";

            // 4. 条目 ID 重生成（避免 ID 冲突）
            if (kb.Entries != null)
            {
                foreach (var e in kb.Entries)
                {
                    e.Id = Guid.NewGuid().ToString("N");
                }
            }

            kb.Id = newId;
            kb.Name = finalName;
            kb.CreatedAt = DateTime.UtcNow;
            kb.UpdatedAt = DateTime.UtcNow;

            SaveBase(kb);

            summary.Conflicts.Add(new ImportConflict
            {
                OriginalName = originalName,
                FinalName = finalName,
                NewBaseId = newId,
                Reason = reason,
                EntryCount = kb.Entries?.Count ?? 0,
            });
        }
        catch (Exception ex)
        {
            summary.Errors.Add($"导入失败：{ex.Message}");
        }
    }

    /// <summary>检测 JSON 是否为合法的 A3KB 导出文件</summary>
    public static bool IsValidA3KbFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using var fs = File.OpenRead(filePath);
            using var doc = JsonDocument.Parse(fs);
            return doc.RootElement.TryGetProperty("$type", out var t)
                && (t.GetString() == SingleBaseType || t.GetString() == AllBasesType);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>快速预览 A3KB 文件信息（不导入）</summary>
    public static (string type, int baseCount, int entryCount, string? warning) PreviewA3KbFile(string filePath)
    {
        if (!File.Exists(filePath)) return ("unknown", 0, 0, "文件不存在");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            if (!doc.RootElement.TryGetProperty("$type", out var typeProp))
                return ("unknown", 0, 0, "缺少 $type 字段，不是 A3KB 文件");
            string type = typeProp.GetString() ?? "unknown";
            int baseCount = 0, entryCount = 0;

            if (type == SingleBaseType && doc.RootElement.TryGetProperty("library", out var baseEl))
            {
                baseCount = 1;
                if (baseEl.TryGetProperty("entries", out var entries))
                    entryCount = entries.GetArrayLength();
            }
            else if (type == AllBasesType && doc.RootElement.TryGetProperty("libraries", out var basesEl))
            {
                baseCount = basesEl.GetArrayLength();
                foreach (var b in basesEl.EnumerateArray())
                    if (b.TryGetProperty("entries", out var entries))
                        entryCount += entries.GetArrayLength();
            }

            string? warn = null;
            if (type != SingleBaseType && type != AllBasesType)
                warn = $"未知的 $type：{type}";

            return (type, baseCount, entryCount, warn);
        }
        catch (Exception ex)
        {
            return ("unknown", 0, 0, $"解析失败：{ex.Message}");
        }
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

    /// <summary>AI 提取提示词模板（强调提炼 + 按主题拆分）</summary>
    private const string AiExtractSystemPrompt = @"你是 A3Tools 知识库提取助手。请把以下文档提炼为结构化 Markdown 知识条目。

【关键要求 - 按主题拆分】
1. **一个文档如果含多个主题，请拆为多个 `##` 二级标题**（每个主题独立一条知识）
2. **每个 `##` 二级标题代表一个独立的知识点**，不能把整个文档合成一个大块
3. 提炼关键概念、操作步骤、参数、注意事项 — 不要大段复制原文
4. 重要控制在原文 30% 长度以内，以精炼为优先
5. 使用 `###` 三级标题组织子章节
6. 列表用 `-`
7. 关键术语加 `【重点】` 标记
8. 如有原始表格，用 `│` `─┼─` 制表符重新对齐
9. 开头输出一行 `> 来源：<filename>` 标记出处

【输出示例】
> 来源：xxx手册.md

## 账套启动
账套启动有三种方式：ERP 网页版、A3 客户端、软件开发工具...

## 账套备份
备份路径位于 DATA 目录下的 backup 文件夹...

## 常见问题
如果启动后黑屏，请检查...

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
                var hash = ComputeHashStatic(aiContent);
                var existing = kb.Entries.FirstOrDefault(e =>
                    e.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase));
                if (existing != null && existing.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    // 同一文件已提取过 → 全部重新拆分(以新 AI 输出为准)
                    kb.Entries.RemoveAll(e => e.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase));
                }

                // ★ 2026-09-01 按 H2 拆分 AI 输出为多条独立条目
                var sections = SplitByH2Static(aiContent);
                var fileBaseName = Path.GetFileNameWithoutExtension(file.Name);
                int addedCount = 0;

                if (sections.Count == 0)
                {
                    // 没有 H2 拆分点（AI 输出了纯文本）→ 退化为 1 条
                    AddEntry(kb.Id, new KnowledgeEntry
                    {
                        Title = fileBaseName,
                        Content = aiContent,
                        SourceFile = file.FullName,
                        SourceType = Models.KnowledgeSourceType.AiExtract,
                        ContentHash = hash,
                        Tags = ExtractTagsFromContent(aiContent),
                    });
                    addedCount = 1;
                }
                else
                {
                    // 第一个 H2 之前的内容（如 > 来源：xxx）作为前缀
                    var header = sections[0].Header;
                    var prefix = string.IsNullOrEmpty(header) ? "" : header;
                    // 为每个 H2 创建独立条目
                    foreach (var section in sections)
                    {
                        var entry = new KnowledgeEntry
                        {
                            Title = section.Title,
                            Content = prefix + "\n" + section.Content,
                            SourceFile = file.FullName,
                            SourceType = Models.KnowledgeSourceType.AiExtract,
                            ContentHash = ComputeHashStatic(file.FullName + section.Title),
                            Tags = ExtractTagsFromContent(section.Content),
                        };
                        AddEntry(kb.Id, entry);
                        addedCount++;
                    }
                }

                summary.Success++;
                progress?.Report(new AiExtractProgress { Index = i + 1, Total = files.Count, FileName = file.Name, Status = $"✅ 完成(拆为 {addedCount} 条)" });
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

    /// <summary>读取文件内容(.md/.txt/.docx BCL; .pdf/.doc/.xls/.xlsx 用 NuGet)</summary>
    public string ReadFileContent(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".md" or ".txt" => File.ReadAllText(filePath),
            ".docx" => ReadDocxText(filePath),
            ".pdf" => ReadPdfText(filePath),
            ".doc" => ReadDocText(filePath),
            ".xls" => ReadXlsText(filePath),
            ".xlsx" => ReadXlsxText(filePath),
            _ => File.ReadAllText(filePath),
        };
    }

    /// <summary>从 .pdf 提取所有页文本(PdfPig)</summary>
    private static string ReadPdfText(string filePath)
    {
        try
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath);
            var sb = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                var text = page.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[PDF 读取失败 {Path.GetFileName(filePath)}: {ex.Message}]";
        }
    }

    /// <summary>从 .doc(二进制 Word)提取文本(NPOI 2.7+ 拆出 HWPF 独立包,暂不支持)</summary>
    private static string ReadDocText(string filePath)
    {
        return $"[.doc 暂不支持自动读取,请用 Word/WPS 另存为 .docx 后再导入: {Path.GetFileName(filePath)}]";
    }

    /// <summary>从 .xls(二进制 Excel)提取所有 sheet 文本(NPOI HSSF)</summary>
    private static string ReadXlsText(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var wb = new NPOI.HSSF.UserModel.HSSFWorkbook(fs);
            return ExtractSheetsText(wb);
        }
        catch (Exception ex)
        {
            return $"[XLS 读取失败 {Path.GetFileName(filePath)}: {ex.Message}]";
        }
    }

    /// <summary>从 .xlsx(OOXML Excel)提取所有 sheet 文本(NPOI XSSF)</summary>
    private static string ReadXlsxText(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var wb = new NPOI.XSSF.UserModel.XSSFWorkbook(fs);
            return ExtractSheetsText(wb);
        }
        catch (Exception ex)
        {
            return $"[XLSX 读取失败 {Path.GetFileName(filePath)}: {ex.Message}]";
        }
    }

    /// <summary>遍历 NPOI 工作簿所有 sheet,每行用 Tab 分隔</summary>
    private static string ExtractSheetsText(NPOI.SS.UserModel.IWorkbook wb)
    {
        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < wb.NumberOfSheets; s++)
        {
            var sheet = wb.GetSheetAt(s);
            if (sheet == null) continue;
            sb.AppendLine($"=== Sheet: {sheet.SheetName} ===");
            for (int r = 0; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                var cells = new List<string>();
                for (int c = 0; c < row.LastCellNum; c++)
                {
                    var cell = row.GetCell(c);
                    cells.Add(cell?.ToString()?.Trim() ?? "");
                }
                if (cells.Any(x => !string.IsNullOrWhiteSpace(x)))
                    sb.AppendLine(string.Join("\t", cells));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// 从 .docx(zip)解压 word/document.xml 提取段落文本
    /// ★ 2026-09-01 增强:检测 Heading 样式(Heading1/2/3、标题1/2/3)并插入 `## ` 前缀
    /// 使拆分逻辑(SplitByH2)能像 .md 一样识别 Word 章节
    /// </summary>
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
        foreach (var p in xdoc.Root.Descendants(System.Xml.Linq.XName.Get("p", WmlNs)))
        {
            // 检查段落样式名(Heading2 / 标题2 / Title 等)
            var pStyle = p.Descendants(System.Xml.Linq.XName.Get("pStyle", WmlNs)).FirstOrDefault();
            var styleVal = pStyle?.Attribute(System.Xml.Linq.XName.Get("val", WmlNs))?.Value ?? "";
            bool isHeading = IsHeadingStyle(styleVal);

            var line = string.Concat(p.Descendants(System.Xml.Linq.XName.Get("t", WmlNs))
                                       .Select(t => (string?)t.Value ?? ""));
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (isHeading)
            {
                sb.AppendLine();
                sb.AppendLine("## " + line.Trim());
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    /// <summary>判断 Word 样式名是否是标题（Heading1-5/标题1-5/Title/副标题）</summary>
    private static bool IsHeadingStyle(string styleVal)
    {
        if (string.IsNullOrEmpty(styleVal)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
            styleVal,
            @"^(Heading|title|标题|副标题)\d?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // ━━━━━━━━━━━━━━━━ 工具方法 ━━━━━━━━━━━━━━━━

    public static string ComputeHashStatic(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>Markdown H2 章节片段</summary>
    public class H2Section
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Header { get; set; } = "";  // 首个 H2 之前的内容（如 > 来源：xxx）
    }

    /// <summary>把 Markdown 按 ## 二级标题拆分为多个段落
    /// 返回第一个 H2 之前的 header + 多个 H2 片段
    /// </summary>
    public static List<H2Section> SplitByH2Static(string markdown)
    {
        var result = new List<H2Section>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;

        var lines = markdown.Split('\n');
        H2Section? current = null;
        string headerAccum = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            // 匹配 ## 开头的二级标题（不能是 ### 或更多 #）
            if (line.StartsWith("## ") && !line.StartsWith("### "))
            {
                // 闭合上一个
                if (current != null) result.Add(current);
                // 开始新章节
                current = new H2Section
                {
                    Title = line[3..].Trim(),
                    Content = line + "\n",
                };
            }
            else if (current == null)
            {
                // 第一个 H2 之前的内容
                headerAccum += rawLine + "\n";
            }
            else
            {
                current.Content += rawLine + "\n";
            }
        }
        if (current != null) result.Add(current);

        // 第一个 section 的 Header 填前面
        if (result.Count > 0)
            result[0].Header = headerAccum.Trim();

        return result;
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

    // ━━━━━━━━━━━━━━━━ BM25 检索（R4 2026-09-01 陛下要求）━━━━━━━━━━━━━━━

    /// <summary>检索匹配项（含知识库来源信息）</summary>
    public class SearchHit
    {
        public string BaseId { get; set; } = "";
        public string BaseName { get; set; } = "";
        public KnowledgeEntry Entry { get; set; } = null!;
        public double Score { get; set; }
        public List<string> MatchedTerms { get; set; } = new();
    }

    /// <summary>跨所有知识库检索（BM25-lite）</summary>
    public List<SearchHit> SearchAll(string query, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<SearchHit>();
        var hits = new List<SearchHit>();

        foreach (var meta in ListBases())
        {
            var kb = GetBase(meta.Id);
            if (kb == null || kb.Entries.Count == 0) continue;
            var kbHits = SearchInBase(kb, query, topK);
            hits.AddRange(kbHits);
        }

        // 跨库按分数排序取 topK
        return hits.OrderByDescending(h => h.Score).Take(topK).ToList();
    }

    /// <summary>在指定知识库中检索</summary>
    public List<SearchHit> SearchInBase(KnowledgeBase kb, string query, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || kb == null) return new List<SearchHit>();
        var terms = Tokenize(query);
        if (terms.Count == 0) return new List<SearchHit>();

        var hits = new List<SearchHit>();
        foreach (var entry in kb.Entries)
        {
            var (score, matched) = ScoreEntry(entry, terms);
            if (score > 0)
            {
                hits.Add(new SearchHit
                {
                    BaseId = kb.Id,
                    BaseName = kb.Name,
                    Entry = entry,
                    Score = score,
                    MatchedTerms = matched,
                });
            }
        }
        return hits.OrderByDescending(h => h.Score).Take(topK).ToList();
    }

    /// <summary>Fallback：跨所有库取最近更新的 N 条（BM25 未命中时用）</summary>
    public List<SearchHit> GetRecentEntries(int topN = 3)
    {
        var allEntries = new List<(KnowledgeBase kb, KnowledgeEntry entry)>();
        foreach (var meta in ListBases())
        {
            var kb = GetBase(meta.Id);
            if (kb == null) continue;
            foreach (var e in kb.Entries)
                allEntries.Add((kb, e));
        }
        return allEntries
            .OrderByDescending(x => x.entry.UpdatedAt)
            .Take(topN)
            .Select(x => new SearchHit
            {
                BaseId = x.kb.Id,
                BaseName = x.kb.Name,
                Entry = x.entry,
                Score = 0,
                MatchedTerms = new List<string>(),
            })
            .ToList();
    }

    // ━━━━━━━━━━━━━━━━ AI 智能拆分（R4 增强 2026-09-01）━━━━━━━━━━━━━━━

    /// <summary>AI 拆分提示词模板（接现有 entry 拆分为多主题）</summary>
    private const string AiSplitSystemPrompt = @"你是 A3Tools 知识拆分助手。陛下现有 1 个条目包含多个主题，请拆为多个独立 `##` 二级标题。

【要求】
1. **按主题拆为多个 `##` 二级标题**（每个主题独立一条知识）
2. 提炼关键概念、操作步骤、参数、注意事项 — 不要大段复制
3. 重要控制在原文 30% 长度以内，以精炼为优先
4. 使用 `###` 三级标题组织子章节
5. 列表用 `-`
6. 关键术语加 `【重点】` 标记
7. 开头输出 `> 来源：<original_source>`

【输出格式示例】
> 来源：<原始文件路径>

## 主题 A 标题
主题 A 内容...

## 主题 B 标题
主题 B 内容...

【输出】仅输出拆分后的 Markdown，不要额外说明。";

    /// <summary>检测「未拆分」条目(内容长且无 ## 章节划分)</summary>
    public List<KnowledgeEntry> DetectUnsplitEntries(string baseId, int minLength = 200)
    {
        var kb = GetBase(baseId);
        if (kb == null) return new List<KnowledgeEntry>();
        return kb.Entries.Where(e =>
            !string.IsNullOrEmpty(e.Content)
            && e.Content.Length >= minLength
            && !e.Content.Contains("## ")  // 没有 ## 拆分
        ).ToList();
    }

    /// <summary>AI 智能拆分选中条目：调 AI 拆为多主题，返回拆分后的新条目列表</summary>
    public async Task<(int success, int failed, List<string> errors)> AiSplitEntriesAsync(
        string baseId,
        IEnumerable<string> entryIds,
        Models.AiProviderConfig provider,
        OpenAiCompatibleBackend backend,
        IProgress<AiExtractProgress>? progress = null,
        CancellationToken ct = default)
    {
        int success = 0, failed = 0;
        var errors = new List<string>();

        foreach (var entryId in entryIds)
        {
            ct.ThrowIfCancellationRequested();
            var kb = GetBase(baseId);
            if (kb == null) { failed++; errors.Add("知识库不存在"); continue; }
            var entry = kb.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) { failed++; errors.Add($"条目不存在: {entryId}"); continue; }

            progress?.Report(new AiExtractProgress
            {
                Index = success + failed + 1,
                Total = entryIds.Count(),
                FileName = entry.Title,
                Status = "AI 拆分中"
            });

            try
            {
                // 调用 AI 拆分
                var userPrompt = $"【现有条目标题】{entry.Title}\n【内容】\n{entry.Content}";
                var messages = new List<Models.ChatMessage>
                {
                    new() { Role = Models.ChatRole.System, Content = AiSplitSystemPrompt },
                    new() { Role = Models.ChatRole.User, Content = userPrompt },
                };
                var reply = await backend.SendAsync(provider, messages, ct);
                if (string.IsNullOrWhiteSpace(reply?.Content))
                {
                    failed++; errors.Add($"{entry.Title}: AI 返回为空");
                    continue;
                }

                // 拆分 H2 章节
                var sections = SplitByH2Static(reply.Content);
                var realSections = sections.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
                if (realSections.Count == 0)
                {
                    failed++; errors.Add($"{entry.Title}: AI 未拆分出主题");
                    continue;
                }

                // 删除原条目,创建多个新条目
                kb.Entries.Remove(entry);
                var fileBaseName = entry.Title;
                foreach (var section in realSections)
                {
                    var newEntry = new KnowledgeEntry
                    {
                        Title = section.Title,
                        Content = section.Content,
                        SourceFile = entry.SourceFile,
                        SourceType = entry.SourceType,
                        SourceReference = entry.SourceReference,
                        ContentHash = ComputeHashStatic(entry.SourceFile + section.Title),
                        Tags = ExtractTagsFromContent(section.Content),
                    };
                    kb.Entries.Add(newEntry);
                }
                SaveBase(kb);
                success++;
                progress?.Report(new AiExtractProgress
                {
                    Index = success + failed,
                    Total = entryIds.Count(),
                    FileName = entry.Title,
                    Status = $"✅ 拆为 {realSections.Count} 条"
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++; errors.Add($"{entry.Title}: {ex.Message}");
                progress?.Report(new AiExtractProgress
                {
                    Index = success + failed,
                    Total = entryIds.Count(),
                    FileName = entry.Title,
                    Status = "❌ 失败"
                });
            }
        }
        return (success, failed, errors);
    }

    /// <summary>简单分词（支持中文:2 字一组 + 英文按空格拆）</summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        // 提取英文单词
        var wordMatches = System.Text.RegularExpressions.Regex.Matches(text, @"[a-zA-Z0-9]+");
        foreach (System.Text.RegularExpressions.Match m in wordMatches)
            tokens.Add(m.Value.ToLowerInvariant());

        // 中文 2 字切词（简易版,类似 elasticsearch ik）
        var chineseMatches = System.Text.RegularExpressions.Regex.Matches(text, @"[\u4e00-\u9fa5]+");
        foreach (System.Text.RegularExpressions.Match m in chineseMatches)
        {
            var s = m.Value;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (i + 2 <= s.Length)
                    tokens.Add(s.Substring(i, 2));
            }
        }

        return tokens.Distinct().ToList();
    }

    /// <summary>BM25-lite 评分:term 频次 × log(N/df) × 字段加权</summary>
    private (double score, List<string> matched) ScoreEntry(KnowledgeEntry entry, List<string> queryTerms)
    {
        // 字段:Title × 3.0, Tags × 2.0, Content × 1.0
        var title = Tokenize(entry.Title ?? "");
        var tags = (entry.Tags ?? new()).SelectMany(Tokenize).ToList();
        var content = Tokenize(entry.Content ?? "");

        double score = 0;
        var matched = new List<string>();

        foreach (var qt in queryTerms)
        {
            int tfTitle = title.Count(t => t == qt);
            int tfTags = tags.Count(t => t == qt);
            int tfContent = content.Count(t => t == qt);

            if (tfTitle + tfTags + tfContent == 0) continue;
            matched.Add(qt);

            // TF 饱和 + IDF 简化（库内文档数近似 N=1）
            double fieldScore = tfTitle * 3.0 + tfTags * 2.0 + tfContent * 1.0;
            // 标题/标签命中权重加倍
            if (tfTitle > 0) fieldScore *= 1.5;
            if (tfTags > 0) fieldScore *= 1.2;

            score += fieldScore;
        }

        return (score, matched);
    }
}