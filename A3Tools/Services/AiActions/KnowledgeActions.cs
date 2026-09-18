using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Services.AiActions;

/// <summary>
/// ★ 2026-09-08 陛下要求：让 AI 助理能在对话中直接管理知识库
/// <para>8 个新 Action，覆盖：列表/检索/读取/推荐位置/创建库/添加条目/更新条目/删除条目</para>
/// <para>设计要点：</para>
/// <list type="bullet">
///   <item>只读操作（list/search/get/recommend）无需确认，AI 可自由探索</item>
///   <item>写入操作（create/add/update）走 ConfirmActionForm 普通确认，陛下能看到预览再点确认</item>
///   <item>删除操作（delete）走高危确认：陛下必须输入条目标题前 4 字才能放行</item>
///   <item>所有写操作 SourceType=ChatExtract，让知识库 UI 能清晰显示「💬 对话」来源</item>
/// </list>
/// </summary>

/// <summary>
/// 列出所有知识库（只读，AI 探索用）
/// </summary>
public class ListKnowledgeBasesAction : IAiAction
{
    public string Name => "list_knowledge_bases";
    public string Description => "列出所有知识库的 ID、名称、描述、条目数（用于 AI 探索知识库结构，方便后续添加/检索）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取知识库列表（只读）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var mgr = new KnowledgeBaseManager();
            var bases = mgr.ListBases();
            var data = bases.Select(b => new
            {
                b.Id,
                b.Name,
                b.Description,
                EntryCount = mgr.GetBase(b.Id)?.Entries.Count ?? 0,
                WatchFolder = string.IsNullOrEmpty(b.WatchFolder) ? null : b.WatchFolder,
            }).ToList();

            return Task.FromResult(AiActionResult.Ok($"共 {data.Count} 个知识库", new
            {
                count = data.Count,
                bases = data
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 跨所有知识库检索（BM25-lite，AI 用于回答陛下问题时定位参考资料）
/// </summary>
public class SearchKnowledgeAction : IAiAction
{
    public string Name => "search_knowledge";
    public string Description => "跨所有知识库检索内容（BM25 评分），返回 top K 个最相关条目。AI 回答陛下技术问题前，可调用此工具查找参考。";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "string", description = "检索关键词（支持中英文，BM25 自动分词）" },
            ["top_k"] = new { type = "integer", description = "返回数量，默认 5，最大 20" }
        },
        required = new string[] { "query" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var q = arguments.TryGetValue("query", out var v) ? v?.ToString() ?? "" : "";
        return $"检索知识库：{q}";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string query = arguments.TryGetValue("query", out var v) ? v?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(query)) return Task.FromResult(AiActionResult.Fail("请提供检索关键词"));
            int topK = 5;
            if (arguments.TryGetValue("top_k", out var tk) && int.TryParse(tk?.ToString(), out var n))
                topK = Math.Clamp(n, 1, 20);

            var mgr = new KnowledgeBaseManager();
            var hits = mgr.SearchAll(query, topK);
            if (hits.Count == 0)
            {
                // Fallback: 告诉 AI 没命中，但返回最近更新的 3 条作为候选
                var recent = mgr.GetRecentEntries(3);
                var recentData = recent.Select(h => new
                {
                    h.BaseId,
                    h.BaseName,
                    h.Entry.Id,
                    h.Entry.Title,
                    h.Entry.SourceType,
                    Preview = (h.Entry.Content ?? "").Length > 200
                        ? h.Entry.Content.Substring(0, 200) + "..."
                        : h.Entry.Content ?? ""
                }).ToList();
                return Task.FromResult(AiActionResult.Ok($"BM25 0 命中,返回最近 {recent.Count} 条", new
                {
                    matched = 0,
                    fallback_recent = recentData
                }));
            }
            var data = hits.Select(h => new
            {
                h.BaseId,
                h.BaseName,
                h.Entry.Id,
                h.Entry.Title,
                h.Entry.SourceType,
                Score = Math.Round(h.Score, 2),
                MatchedTerms = h.MatchedTerms,
                Preview = (h.Entry.Content ?? "").Length > 300
                    ? h.Entry.Content.Substring(0, 300) + "..."
                    : h.Entry.Content ?? ""
            }).ToList();

            return Task.FromResult(AiActionResult.Ok($"检索到 {data.Count} 条相关条目", new
            {
                query,
                matched = data.Count,
                hits = data
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 按 ID 读取单条知识条目（AI 拿到 hit 后想看完整内容时用）
/// </summary>
public class GetKnowledgeEntryAction : IAiAction
{
    public string Name => "get_knowledge_entry";
    public string Description => "根据知识库 ID + 条目 ID 读取单条知识的完整内容（AI 检索后想看完整内容用）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["base_id"] = new { type = "string", description = "知识库 ID（先 list_knowledge_bases 拿到）" },
            ["entry_id"] = new { type = "string", description = "条目 ID（先 search_knowledge 拿到）" }
        },
        required = new string[] { "base_id", "entry_id" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取单条知识（只读）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string baseId = arguments.TryGetValue("base_id", out var b) ? b?.ToString() ?? "" : "";
            string entryId = arguments.TryGetValue("entry_id", out var e) ? e?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(baseId) || string.IsNullOrEmpty(entryId))
                return Task.FromResult(AiActionResult.Fail("请提供 base_id 和 entry_id"));

            var mgr = new KnowledgeBaseManager();
            var kb = mgr.GetBase(baseId);
            if (kb == null) return Task.FromResult(AiActionResult.Fail($"知识库 [{baseId}] 不存在"));

            var entry = kb.Entries.FirstOrDefault(x => x.Id == entryId);
            if (entry == null) return Task.FromResult(AiActionResult.Fail($"条目 [{entryId}] 不存在"));

            return Task.FromResult(AiActionResult.Ok($"条目「{entry.Title}」", new
            {
                kb.Name,
                entry.Id,
                entry.Title,
                entry.Content,
                entry.Tags,
                entry.SourceType,
                entry.SourceFile,
                entry.SourceReference,
                entry.UpdatedAt
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 推荐合适的知识库（AI 想添加条目时，先调用此工具看是否有匹配的库）
/// </summary>
public class RecommendKnowledgeTargetAction : IAiAction
{
    public string Name => "recommend_knowledge_target";
    public string Description => "根据待保存的文字内容，推荐最合适的知识库（按 BM25 评分所有已有条目，找最相关的库），同时返回 3 条最相似的现有条目（避免重复添加）。AI 添加知识前先调此工具。";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["preview"] = new { type = "string", description = "待保存的内容预览（前 200 字足够，AI 用来选知识库）" }
        },
        required = new string[] { "preview" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "分析内容并推荐知识库（只读）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string preview = arguments.TryGetValue("preview", out var p) ? p?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(preview)) return Task.FromResult(AiActionResult.Fail("请提供预览文本"));

            var mgr = new KnowledgeBaseManager();
            var allBases = mgr.ListBases();
            if (allBases.Count == 0)
            {
                return Task.FromResult(AiActionResult.Ok("当前没有任何知识库，建议先 create_knowledge_base", new
                {
                    candidates = Array.Empty<object>(),
                    suggestion = "create_knowledge_base"
                }));
            }

            // 找最相似的 5 条，按 base 分组统计
            var hits = mgr.SearchAll(preview, topK: 10);
            var byBase = hits
                .GroupBy(h => new { h.BaseId, h.BaseName })
                .Select(g => new
                {
                    g.Key.BaseId,
                    g.Key.BaseName,
                    TopScore = g.Max(x => x.Score),
                    HitCount = g.Count(),
                    SampleTitles = g.Take(3).Select(x => x.Entry.Title).ToList()
                })
                .OrderByDescending(x => x.TopScore)
                .ToList();

            return Task.FromResult(AiActionResult.Ok($"推荐了 {byBase.Count} 个候选知识库", new
            {
                preview_length = preview.Length,
                candidates = byBase,
                total_bases = allBases.Count
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 创建新知识库（普通确认）
/// </summary>
public class CreateKnowledgeBaseAction : IAiAction
{
    public string Name => "create_knowledge_base";
    public string Description => "创建一个新的知识库（按名称+描述），返回新库的 ID。AI 帮陛下分类组织知识时使用。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["name"] = new { type = "string", description = "知识库名称（如 A3 账套操作手册）" },
            ["description"] = new { type = "string", description = "知识库描述（可选，如：覆盖账套启动、备份、常见问题等）" }
        },
        required = new string[] { "name" }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var name = arguments.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
        return $"新建知识库「{name}」";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string name = arguments.TryGetValue("name", out var n) ? n?.ToString()?.Trim() ?? "" : "";
            string desc = arguments.TryGetValue("description", out var d) ? d?.ToString()?.Trim() ?? "" : "";
            if (string.IsNullOrEmpty(name)) return Task.FromResult(AiActionResult.Fail("请提供知识库名称"));

            var mgr = new KnowledgeBaseManager();
            // 检查是否重名
            var existing = mgr.ListBases().FirstOrDefault(b =>
                string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return Task.FromResult(AiActionResult.Fail($"知识库「{name}」已存在（ID={existing.Id}），直接用 add_knowledge_entry 添加条目即可"));

            var kb = mgr.CreateBase(name, desc);
            return Task.FromResult(AiActionResult.Ok($"已创建知识库「{name}」", new
            {
                kb.Id,
                kb.Name,
                kb.Description
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 添加知识条目（普通确认，AI 对话提取的主入口）
/// <para>★ 2026-09-08 陛下要求核心：用户说"记住这个"/"加到知识库"时，AI 直接调用此 Action</para>
/// </summary>
public class AddKnowledgeEntryAction : IAiAction
{
    public string Name => "add_knowledge_entry";
    // ★ 2026-09-18 陛下需求：加入知识不再弹确认框（每次点确认很麻烦）。add/update 同时改都不会自动覆盖：
    //   - add：add 之前会查重（同名条目 → 拒绝，要求换标题或 update）；所以误调最多「条目建到错误库里」，可手动删
    //   - 写本地 JSON 文件，下次启动还能看到，删一条无伤大雅
    // ★ 仍保留二次确认的：高危 delete（输入前 4 字）/ create_knowledge_base / update_knowledge_entry / export / import
    public string Description => "在指定知识库添加一条知识条目。AI 在对话中识别到陛下想保存的知识点，或识别到对话中产生的重要事实时，调用此工具。SourceType 自动标记为「对话提取」，知识库 UI 会显示 💬 来源。陛下已设此工具免确认，AI 直接调用即可。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["base_name"] = new { type = "string", description = "目标知识库名称（先 list_knowledge_bases 确认名称）" },
            ["title"] = new { type = "string", description = "条目标题，简洁明了（如「Tab1 账套启动失败排查」）" },
            ["content"] = new { type = "string", description = "条目内容，Markdown 格式（重要事实 + 关键步骤 + 参数）" },
            ["tags"] = new { type = "array", description = "可选，标签数组（用于检索，如 [\"\"\"账套\"\"\", \"\"\"启动\"\"\", \"\"\"故障\"\"\"]）",
                items = new { type = "string" } },
            ["source_reference"] = new { type = "string", description = "可选，对话来源说明（如对话标题或时间）" }
        },
        required = new string[] { "base_name", "title", "content" }
    };

    // ★ 2026-09-18 陛下需求：加入知识不再弹确认。陛下原话「每次点确认挺麻烦的」。
    //   安全靠 add 内部的查重（同名条目→拒绝要求换名）+ entry content 上限校验兜底
    public bool RequiresConfirmation => false;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString() ?? "" : "";
        var title = arguments.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        var preview = arguments.TryGetValue("content", out var c) ? c?.ToString() ?? "" : "";
        if (preview.Length > 60) preview = preview.Substring(0, 60) + "...";
        return $"向知识库「{baseName}」添加条目：\n标题：{title}\n预览：{preview}";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString()?.Trim() ?? "" : "";
            string title = arguments.TryGetValue("title", out var t) ? t?.ToString()?.Trim() ?? "" : "";
            string content = arguments.TryGetValue("content", out var c) ? c?.ToString()?.Trim() ?? "" : "";
            string sourceRef = arguments.TryGetValue("source_reference", out var sr) ? sr?.ToString()?.Trim() ?? "" : "";

            if (string.IsNullOrEmpty(baseName)) return Task.FromResult(AiActionResult.Fail("请提供 base_name"));
            if (string.IsNullOrEmpty(title)) return Task.FromResult(AiActionResult.Fail("请提供 title"));
            if (string.IsNullOrEmpty(content)) return Task.FromResult(AiActionResult.Fail("请提供 content"));

            // 解析 tags
            List<string> tags = new();
            if (arguments.TryGetValue("tags", out var tagsObj) && tagsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    var s = item.ToString().Trim();
                    if (!string.IsNullOrEmpty(s)) tags.Add(s);
                }
            }

            var mgr = new KnowledgeBaseManager();
            var kb = mgr.ListBases().FirstOrDefault(x =>
                string.Equals(x.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (kb == null)
                return Task.FromResult(AiActionResult.Fail($"知识库「{baseName}」不存在，请先 list_knowledge_bases 查看现有名称，或调 create_knowledge_base 新建"));

            // 查重：同 base 内同标题 → 拒绝，建议用 update
            var fullKb = mgr.GetBase(kb.Id);
            var dup = fullKb?.Entries.FirstOrDefault(e =>
                string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
            if (dup != null)
                return Task.FromResult(AiActionResult.Fail(
                    $"知识库「{baseName}」里已有同名条目「{title}」（ID={dup.Id}），请用 update_knowledge_entry 更新或换一个标题"));

            var entry = mgr.AddEntry(kb.Id, KnowledgeSourceType.ChatExtract, title, content,
                sourceFile: "", sourceReference: sourceRef, tags: tags);

            return Task.FromResult(AiActionResult.Ok($"已添加条目「{title}」到「{baseName}」", new
            {
                kb.Name,
                entry.Id,
                entry.Title,
                entry.SourceType,
                entry.Tags
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 更新知识条目（普通确认，AI 修正 / 完善 / 合并时用）
/// </summary>
public class UpdateKnowledgeEntryAction : IAiAction
{
    public string Name => "update_knowledge_entry";
    public string Description => "更新已有知识条目的标题/内容/标签（不能改 SourceType）。AI 发现现有条目需要补充或修正时调用。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["base_name"] = new { type = "string", description = "知识库名称" },
            ["entry_id"] = new { type = "string", description = "条目 ID（先 search_knowledge 拿）" },
            ["title"] = new { type = "string", description = "新标题（不传则保留）" },
            ["content"] = new { type = "string", description = "新内容（不传则保留）" },
            ["tags"] = new { type = "array", description = "新标签（不传则保留）",
                items = new { type = "string" } }
        },
        required = new string[] { "base_name", "entry_id" }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString() ?? "" : "";
        var id = arguments.TryGetValue("entry_id", out var i) ? i?.ToString() ?? "" : "";
        return $"更新「{baseName}」中条目 {id.Substring(0, Math.Min(8, id.Length))}（请确认是正确条目）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString()?.Trim() ?? "" : "";
            string entryId = arguments.TryGetValue("entry_id", out var i) ? i?.ToString()?.Trim() ?? "" : "";
            if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(entryId))
                return Task.FromResult(AiActionResult.Fail("请提供 base_name 和 entry_id"));

            var mgr = new KnowledgeBaseManager();
            var kb = mgr.ListBases().FirstOrDefault(x =>
                string.Equals(x.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (kb == null) return Task.FromResult(AiActionResult.Fail($"知识库「{baseName}」不存在"));

            var fullKb = mgr.GetBase(kb.Id);
            var existing = fullKb?.Entries.FirstOrDefault(e => e.Id == entryId);
            if (existing == null) return Task.FromResult(AiActionResult.Fail($"条目 [{entryId}] 不存在"));

            // 浅拷贝并应用更改
            var updated = new KnowledgeEntry
            {
                Id = existing.Id,
                Title = arguments.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t?.ToString())
                    ? t.ToString()!.Trim() : existing.Title,
                Content = arguments.TryGetValue("content", out var c) && !string.IsNullOrWhiteSpace(c?.ToString())
                    ? c.ToString()! : existing.Content,
                Tags = existing.Tags,
                SourceFile = existing.SourceFile,
                SourceType = existing.SourceType,
                SourceReference = existing.SourceReference,
            };

            if (arguments.TryGetValue("tags", out var tagsObj) && tagsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                updated.Tags = new List<string>();
                foreach (var item in je.EnumerateArray())
                {
                    var s = item.ToString().Trim();
                    if (!string.IsNullOrEmpty(s)) updated.Tags.Add(s);
                }
            }

            bool ok = mgr.UpdateEntry(kb.Id, updated);
            if (!ok) return Task.FromResult(AiActionResult.Fail("更新失败（未知原因）"));

            return Task.FromResult(AiActionResult.Ok($"已更新条目「{updated.Title}」", new
            {
                kb.Name,
                updated.Id,
                updated.Title
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 删除知识条目（高危：陛下必须输入条目标题前 4 字确认）
/// </summary>
public class DeleteKnowledgeEntryAction : IAiAction
{
    public string Name => "delete_knowledge_entry";
    public string Description => "删除指定知识条目（高危）。陛下必须输入条目标题前 4 字确认才能执行。AI 极少调用，仅在陛下明确要求删除时使用。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["base_name"] = new { type = "string", description = "知识库名称" },
            ["entry_id"] = new { type = "string", description = "条目 ID" }
        },
        required = new string[] { "base_name", "entry_id" }
    };

    public bool RequiresConfirmation => true;
    private string? _confirmText;
    public string? ConfirmationPrompt => _confirmText;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString() ?? "" : "";
        var entryId = arguments.TryGetValue("entry_id", out var i) ? i?.ToString() ?? "" : "";
        var mgr = new KnowledgeBaseManager();
        var kb = mgr.ListBases().FirstOrDefault(x =>
            string.Equals(x.Name, baseName, StringComparison.OrdinalIgnoreCase));
        string title = "(条目不存在)";
        if (kb != null)
        {
            var fullKb = mgr.GetBase(kb.Id);
            var entry = fullKb?.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null) title = entry.Title;
        }
        _confirmText = title.Length >= 4 ? title.Substring(0, 4) : title;
        return $"删除条目「{title}」从「{baseName}」\n陛下必须输入「{_confirmText}」才能放行";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString()?.Trim() ?? "" : "";
            string entryId = arguments.TryGetValue("entry_id", out var i) ? i?.ToString()?.Trim() ?? "" : "";
            if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(entryId))
                return Task.FromResult(AiActionResult.Fail("请提供 base_name 和 entry_id"));

            var mgr = new KnowledgeBaseManager();
            var kb = mgr.ListBases().FirstOrDefault(x =>
                string.Equals(x.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (kb == null) return Task.FromResult(AiActionResult.Fail($"知识库「{baseName}」不存在"));

            int removed = mgr.DeleteEntry(kb.Id, entryId) ? 1 : 0;
            if (removed == 0) return Task.FromResult(AiActionResult.Fail($"条目 [{entryId}] 不存在或删除失败"));

            return Task.FromResult(AiActionResult.Ok($"已删除「{entryId}」", new
            {
                kb.Name,
                entry_id = entryId
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 导出知识库到 JSON 文件（普通确认）
/// <para>★ 2026-09-16 陛下要求：AI 助理能导出/导入知识库，便于跨机器迁移</para>
/// <para>调用时机：陛下让 AI "把 XX 知识库导出给我"、"备份所有知识库"、"打个 json 包" 时</para>
/// </summary>
public class ExportKnowledgeAction : IAiAction
{
    public string Name => "export_knowledge";
    public string Description => "导出知识库到 JSON 文件。支持两种范围：single(导出单个库,按名称) 或 all(导出全部库到一个文件)。AI 帮陛下备份/迁移知识库时使用。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["scope"] = new { type = "string", description = "导出范围: single=单个库, all=全部库(默认 single)", @enum = new[] { "single", "all" } },
            ["base_name"] = new { type = "string", description = "scope=single 时必填,按名称定位(先 list_knowledge_bases 拿到名称)" },
            ["output_dir"] = new { type = "string", description = "可选,输出目录(默认程序运行目录下的 DATA\\knowledge\\exports\\)" }
        },
        required = new string[] { }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var scope = arguments.TryGetValue("scope", out var s) ? s?.ToString() ?? "single" : "single";
        var baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString() ?? "" : "";
        if (scope == "all") return "导出全部知识库到 JSON 备份文件";
        return $"导出知识库「{baseName}」到 JSON 文件";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string scope = arguments.TryGetValue("scope", out var s) ? s?.ToString() ?? "single" : "single";
            string baseName = arguments.TryGetValue("base_name", out var b) ? b?.ToString()?.Trim() ?? "" : "";
            string outputDir = arguments.TryGetValue("output_dir", out var d) ? d?.ToString()?.Trim() ?? "" : "";

            if (string.IsNullOrEmpty(outputDir))
                outputDir = Path.Combine(AppContext.BaseDirectory, "DATA", "knowledge", "exports");
            Directory.CreateDirectory(outputDir);

            var mgr = new KnowledgeBaseManager();
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (scope == "all")
            {
                var allBases = mgr.ListBases();
                if (allBases.Count == 0)
                    return Task.FromResult(AiActionResult.Fail("当前没有任何知识库可导出"));

                var filePath = Path.Combine(outputDir, $"A3Tools_知识库备份_{ts}.json");
                var summary = mgr.ExportAllToFile(filePath);
                var sizeKb = summary.FileSizeBytes / 1024.0;

                return Task.FromResult(AiActionResult.Ok($"已导出全部 {summary.TotalBases} 个库 / {summary.TotalEntries} 条目", new
                {
                    scope = "all",
                    file_path = filePath,
                    total_bases = summary.TotalBases,
                    total_entries = summary.TotalEntries,
                    size_kb = Math.Round(sizeKb, 1)
                }));
            }
            else
            {
                if (string.IsNullOrEmpty(baseName))
                    return Task.FromResult(AiActionResult.Fail("scope=single 时必须提供 base_name(先调 list_knowledge_bases 看名称)"));

                var kb = mgr.ListBases().FirstOrDefault(x =>
                    string.Equals(x.Name, baseName, StringComparison.OrdinalIgnoreCase));
                if (kb == null)
                    return Task.FromResult(AiActionResult.Fail($"知识库「{baseName}」不存在"));

                var safeName = SanitizeFileName(kb.Name);
                var filePath = Path.Combine(outputDir, $"{safeName}_{ts}.json");
                var summary = mgr.ExportBaseToFile(kb.Id, filePath);
                var sizeKb = summary.FileSizeBytes / 1024.0;

                return Task.FromResult(AiActionResult.Ok($"已导出「{kb.Name}」{summary.TotalEntries} 条目", new
                {
                    scope = "single",
                    base_name = kb.Name,
                    file_path = filePath,
                    total_entries = summary.TotalEntries,
                    size_kb = Math.Round(sizeKb, 1)
                }));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }

    /// <summary>清理文件名中的非法字符(与 KnowledgeBaseForm 保持一致)</summary>
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "未命名";
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "未命名" : clean;
    }
}

/// <summary>
/// 从 JSON 文件导入知识库(普通确认)
/// <para>★ 2026-09-16 陛下要求:跨机器迁移、AI 生成的备份都能导入</para>
/// <para>规则:重名自动加「(导入)」后缀;条目 ID 重生成;WatchFolder 跨机器自动清空</para>
/// </summary>
public class ImportKnowledgeAction : IAiAction
{
    public string Name => "import_knowledge";
    public string Description => "从 JSON 文件导入知识库(支持单库/多库格式)。自动处理重名(加「(导入)」后缀)、重新生成条目 ID、清空 WatchFolder 跨机器路径。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["file_path"] = new { type = "string", description = "JSON 文件绝对路径(一般是 export_knowledge 返回的 file_path)" }
        },
        required = new string[] { "file_path" }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var path = arguments.TryGetValue("file_path", out var p) ? p?.ToString() ?? "" : "";
        // 先预览,让陛下看到几个库/几条目
        var (type, baseCount, entryCount, warning) = KnowledgeBaseManager.PreviewA3KbFile(path);
        if (warning != null)
            return $"⚠ 这不是合法的知识库导出文件:{warning}";

        string typeLabel = type == "a3kb-base-v1" ? "单库" : type == "a3kb-all-v1" ? "多库" : type;
        return $"从 JSON 文件导入知识库\n类型:{typeLabel}\n知识库:{baseCount} 个\n条目:{entryCount} 条\n\n⚠ 重名会自动加「(导入)」后缀\n⚠ 条目 ID 会重新生成\n⚠ WatchFolder 会被清空";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string filePath = arguments.TryGetValue("file_path", out var p) ? p?.ToString()?.Trim() ?? "" : "";
            if (string.IsNullOrEmpty(filePath))
                return Task.FromResult(AiActionResult.Fail("请提供 file_path"));

            if (!File.Exists(filePath))
                return Task.FromResult(AiActionResult.Fail($"文件不存在:{filePath}"));

            var mgr = new KnowledgeBaseManager();
            var summary = mgr.ImportFromFile(filePath);

            if (!summary.Success)
            {
                var errMsg = summary.Errors.Count > 0 ? string.Join(";", summary.Errors) : "未知错误";
                return Task.FromResult(AiActionResult.Fail($"导入失败:{errMsg}"));
            }

            var conflicts = summary.Conflicts.Select(c => new
            {
                c.OriginalName,
                c.FinalName,
                c.NewBaseId,
                c.Reason,
                c.EntryCount
            }).ToList();

            return Task.FromResult(AiActionResult.Ok(
                $"导入完成:{summary.SuccessBases}/{summary.TotalBases} 个库" +
                (summary.RenamedBases > 0 ? $",{summary.RenamedBases} 个重命名" : ""),
                new
                {
                    success = summary.Success,
                    total_bases = summary.TotalBases,
                    success_bases = summary.SuccessBases,
                    renamed_bases = summary.RenamedBases,
                    conflicts
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}