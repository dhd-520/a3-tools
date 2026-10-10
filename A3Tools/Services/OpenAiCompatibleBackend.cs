using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using A3Tools.Models;
using A3Tools.Services.AiActions;

using A3Tools.Services.AiActions.AiAssistantLogging;

namespace A3Tools.Services;

/// <summary>
/// OpenAI 兼容协议的 AI 后端调用器（支持流式 + Function Calling）
/// <para>★ 2026-08-26 支持厂商：OpenAI / DeepSeek / 通义千问 / 智谱 / 月之暗面 / MiniMax / Ollama / 自定义</para>
/// </summary>
public class OpenAiCompatibleBackend : IAiChatBackend
{
    private readonly HttpClient _http;
    private readonly ActionRegistry _actions = ActionRegistry.Instance;

    /// <summary>
    /// ★ 2026-10-09 陛下要求：失败重试配置
    /// <para>由 AiChatForm.LoadConfig 从 AiChatConfig.MaxRetryCount / RetryIntervalSeconds 同步过来</para>
    /// <para>MaxRetries=0 表示不重试，默认 3</para>
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>每次重试前等待秒数，默认 5</summary>
    public int RetryIntervalSeconds { get; set; } = 5;

    public OpenAiCompatibleBackend()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // 流式输出需要更长超时
        };
    }

    /// <summary>
    /// 传统一次性调用（保留兼容性）
    /// </summary>
    public async Task<ChatReply> SendAsync(AiProviderConfig provider, List<ChatMessage> messages, CancellationToken ct = default)
    {
        var url = provider.ApiUrl.TrimEnd('/') + "/chat/completions";

        var requestBody = new
        {
            model = provider.Model,
            messages = messages.Select(m => new
            {
                role = RoleToString(m.Role),
                content = m.Content
            }).ToArray(),
            temperature = 0.7,
            stream = false
        };

        string json = JsonSerializer.Serialize(requestBody);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        string body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractErrorMessage(body) ?? $"HTTP {(int)resp.StatusCode}");

        var parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body);
        if (parsed?.Choices == null || parsed.Choices.Count == 0)
            throw new InvalidOperationException("响应中没有 choices");

        var choice = parsed.Choices[0];
        return new ChatReply
        {
            Content = choice.Message?.Content ?? string.Empty,
            Model = parsed.Model ?? provider.Model,
            TokensUsed = parsed.Usage?.TotalTokens ?? 0
        };
    }

    /// <summary>
    /// 流式输出（SSE）：每个 chunk 是一次 yield 的内容片段
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfig provider,
        List<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = provider.ApiUrl.TrimEnd('/') + "/chat/completions";

        var requestBody = new
        {
            model = provider.Model,
            messages = messages.Select(m => new
            {
                role = RoleToString(m.Role),
                content = m.Content
            }).ToArray(),
            temperature = 0.7,
            stream = true
        };

        string json = JsonSerializer.Serialize(requestBody);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            string errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(ExtractErrorMessage(errBody) ?? $"HTTP {(int)resp.StatusCode}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;

            string payload = line.Substring(5).Trim();
            if (payload == "[DONE]") yield break;

            string? chunkContent = ParseStreamChunk(payload);
            if (!string.IsNullOrEmpty(chunkContent))
                yield return chunkContent;
        }
    }

    /// <summary>
    /// 完整对话：带 Function Calling 循环（带陛下二次确认）
    /// </summary>
    /// <param name="onToolNeedsConfirm">AI 想执行危险 Action 时调用，陛下点 [允许] 返回 true，点 [拒绝] 返回 false</param>
    public async Task<AiChatTurnResult> SendWithToolsAsync(
        AiProviderConfig provider,
        List<ChatMessage> messages,
        List<ChatToolCallRecord>? toolCallLog,
        Func<AiActionResult, Task<bool>>? onToolExecuted,
        Func<string, string, string?, Task<bool>>? onToolNeedsConfirm,
        CancellationToken ct = default)
    {
        var url = provider.ApiUrl.TrimEnd('/') + "/chat/completions";
        var toolsSchema = _actions.ToOpenAiToolsSchema();

        // ★ 2026-10-06 AI 助理日志：会话开始（用最近一条用户消息前 30 字作 sessionId，便于陛下在日志里搜）
        string lastUserPreview = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? "";
        if (lastUserPreview.Length > 30) lastUserPreview = lastUserPreview.Substring(0, 30) + "...";
        string sessionId = $"{DateTime.Now:HHmmss}-{lastUserPreview.Replace(" ", "_")}";
        AiAssistantLogger.BeginSession(sessionId);

        // 复制消息列表（Function Calling 循环会追加 tool 消息）
        // 用 List<object> 避免 anonymous type shape 不一致
        var msgs = new List<object>();
        foreach (var m in messages)
        {
            msgs.Add(new { role = RoleToString(m.Role), content = m.Content });
        }

        const int maxLoops = 30; // 防止 AI 无限循环（2026-10-06 5→15→30：多步骤 Skill + 续命器注入需要更多轮次）
        var sb = new StringBuilder();
        int totalToolCallsInSession = 0;  // 本会话累计工具调用次数（早退检测用）

        // ★ 2026-10-06 Skill 工作流强制续命器追踪
        bool skillMode = false;             // load_skill 是否调过
        var calledWriteTools = new HashSet<string>();  // 调过的 write-tools
        bool brokeEarly = false;             // 是否 break（修双重日志 bug）
        // ★ 2026-10-06 增：当前加载的 Skill 是否需要写工具（read-only Skill 跳过续命检查，避免死循环到 maxLoops）
        bool currentSkillWriteRequired = true;  // 默认 true（安全起见，未明确标记的 Skill 仍走严格检查）

        try
        {
        for (int loop = 0; loop < maxLoops; loop++)
        {
            ct.ThrowIfCancellationRequested();

            AiAssistantLogger.LogLoopStart(loop, maxLoops, msgs.Count);

            var requestBody = new
            {
                model = provider.Model,
                messages = msgs,
                temperature = 0.7,
                tools = toolsSchema,
                tool_choice = "auto"
            };

            string json = JsonSerializer.Serialize(requestBody);
            // ★ 2026-10-09 失败重试：HTTP 错误含"请稍后再试" / 网络异常 / 超时会自动重试
            var (ok, error, parsed) = await SendChatRequestWithRetryAsync(
                url, json, provider.ApiKey, MaxRetries, RetryIntervalSeconds, loop, ct);
            if (!ok) throw new InvalidOperationException(error);

            var choice = parsed.Choices[0];
            var message = choice.Message ?? new OpenAiMessage();

            // 把 assistant 消息压回历史
            if (message.ToolCalls != null && message.ToolCalls.Count > 0)
            {
                msgs.Add(new
                {
                    role = "assistant",
                    content = message.Content ?? string.Empty,
                    tool_calls = message.ToolCalls
                });
            }
            else
            {
                msgs.Add(new { role = "assistant", content = message.Content ?? string.Empty });
            }

            // 累积 AI 自然语言回复（如果有）
            if (!string.IsNullOrEmpty(message.Content))
                sb.Append(message.Content);

            // ★ 2026-10-06 日志：记录 AI 响应
            AiAssistantLogger.LogAiResponse(loop, message.Content ?? string.Empty, message.ToolCalls?.Count ?? 0);

            // ★ 2026-09-20 修复：某些 AI 模型不支持标准 function calling，
            //   会以纯文本形式输出工具调用（如 <function_calls>...</function_calls>）
            //   需要解析文本格式的工具调用并执行
            if (message.ToolCalls == null || message.ToolCalls.Count == 0)
            {
                var textToolCalls = ParseTextToolCalls(message.Content ?? string.Empty);
                if (textToolCalls.Count > 0)
                {
                    // 找到文本格式的工具调用，替换为空的 tool_calls 并继续循环
                    message.ToolCalls = textToolCalls;
                    // 从累积内容中移除工具调用文本
                    string cleanedContent = RemoveToolCallText(message.Content ?? string.Empty);
                    if (!string.IsNullOrEmpty(cleanedContent))
                    {
                        sb.Clear(); // 清除之前累积的
                        sb.Append(cleanedContent);
                    }
                    // 重新添加 assistant 消息（带 tool_calls）
                    msgs.RemoveAt(msgs.Count - 1); // 移除之前添加的
                    msgs.Add(new
                    {
                        role = "assistant",
                        content = cleanedContent,
                        tool_calls = message.ToolCalls
                    });
                }
                else
                {
                    // ★ 2026-10-06 早退检测：最后一轮没工具调用 + 内容异常短 → 打警告
                    string earlyContent = sb.ToString();
                    var (isPremature, warnReason) = AiAssistantLogger.DetectPrematureEnd(
                        loop, earlyContent, totalToolCallsInSession);
                    // ★ 2026-10-06 Skill 工作流强制续命器：
                    //   条件：在 Skill 模式 + write-tools 未调 + 还有循环余量
                    //   动作：注入续命消息，continue 而不是 break
                    var missingWriteTools = SkillWorkflowEnforcer.GetMissingWriteTools(skillMode, calledWriteTools, currentSkillWriteRequired);
                    if (missingWriteTools.Count > 0 && loop + 1 < maxLoops)
                    {
                        string prompt = SkillWorkflowEnforcer.BuildContinuationPrompt(missingWriteTools);
                        msgs.Add(new { role = "user", content = prompt });
                        AiAssistantLogger.LogLoopEnd(loop, "skill_continuation_injected", sb.ToString(), true,
                            $"Skill 工作流未完成，还需调用 write-tools。已注入续命 prompt，强制下一轮。");
                        continue;
                    }

                    // ★ 2026-10-09 修「AI 嘴上说调了但 tool_calls 实际空」双重幻觉
                    //   现象：AI 文本说"我调了 14 次 add_knowledge_entry 全部失败"，
                    //         但 message.ToolCalls 实际空 + totalToolCallsInSession == 0
                    //   根因：模型在 final 轮既不发 tool_calls 又假装"我已调完"
                    //   修法：检测文本含工具名/entryId + 已调类措辞 → 强制重发
                    string? claimed = DetectClaimedButNotCalled(message.Content ?? string.Empty, totalToolCallsInSession);
                    if (claimed != null && loop + 1 < maxLoops)
                    {
                        string prompt = $"⚠️ 系统检测：你刚才的回复里描述了「{claimed}」，但本次循环你实际没有发出任何 tool_calls。\n" +
                                        "请重新审视：如果你真的需要执行该操作，请在 tool_calls 里真正调用；如果不需要，请直接告诉陛下「我不会调用 X，因为 Y」。\n" +
                                        "禁止用文字描述「我调用了 X」但实际不发的行为——这是幻觉。";
                        msgs.Add(new { role = "user", content = prompt });
                        AiAssistantLogger.LogLoopEnd(loop, "claimed_call_but_empty_toolcalls", sb.ToString(), true,
                            $"AI 文本描述了工具调用（{claimed}）但 tool_calls 为空，已注入强制重发 prompt");
                        continue;
                    }

                    AiAssistantLogger.LogLoopEnd(loop, "no_tool_calls_response", earlyContent, isPremature,
                        isPremature ? warnReason : null);
                    brokeEarly = true;  // 修双重日志 bug：必须在 break 之前（之前写在 break 后是死代码）
                    break; // 真正没有工具调用，结束循环
                }
            }

            // 执行每个 tool call
            foreach (var tc in message.ToolCalls)
            {
                ct.ThrowIfCancellationRequested();
                string toolName = tc.Function?.Name ?? string.Empty;
                string argsJson = tc.Function?.Arguments ?? "{}";

                Dictionary<string, object?> argsDict = new();
                try
                {
                    argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? new();
                }
                catch { }

                AiActionResult result;
                var action = _actions.Get(toolName);
                if (action == null)
                {
                    result = AiActionResult.Fail($"未知操作：{toolName}");
                }
                else
                {
                    // ★ 2026-08-26 R2-C：危险 Action 需陛下二次确认
                    bool needConfirm = action.RequiresConfirmation;
                    bool allowed = true;
                    if (needConfirm && onToolNeedsConfirm != null)
                    {
                        string impact = action.GetImpactDescription(argsDict);
                        string? confirmText = action.ConfirmationPrompt;
                        allowed = await onToolNeedsConfirm(toolName, impact, confirmText);
                    }

                    if (!allowed)
                    {
                        result = AiActionResult.Fail("陛下拒绝执行此操作");
                    }
                    else
                    {
                        try
                        {
                            result = await action.ExecuteAsync(argsDict, ct);
                        }
                        catch (Exception ex)
                        {
                            result = AiActionResult.Fail($"执行 {toolName} 异常：{ex.Message}");
                        }
                    }
                }

                toolCallLog?.Add(new ChatToolCallRecord
                {
                    ToolName = toolName,
                    ArgumentsJson = argsJson,
                    Result = result
                });

                // ★ 2026-10-06 日志：记录工具调用结果
                AiAssistantLogger.LogToolCall(loop, toolName, argsJson, result);
                totalToolCallsInSession++;

                // ★ 2026-10-06 Skill 工作流追踪：load_skill 进入 Skill 模式 + 记录 write-tools
                if (toolName == "load_skill")
                {
                    skillMode = true;
                    // ★ 2026-10-06 增：读取该 Skill 的 write_required 标记，read-only Skill 不强制调写工具
                    try
                    {
                        string? skillName = null;
                        if (argsDict.TryGetValue("name", out var n) && n != null) skillName = n.ToString();
                        if (!string.IsNullOrWhiteSpace(skillName))
                        {
                            var sk = SkillRegistry.Instance.Get(skillName);
                            if (sk != null) currentSkillWriteRequired = sk.WriteRequired;
                        }
                    }
                    catch { /* 解析失败保持默认 true */ }
                }
                if (SkillWorkflowEnforcer.IsWriteTool(toolName)) calledWriteTools.Add(toolName);

                if (onToolExecuted != null)
                    await onToolExecuted(result);

                msgs.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = tc.Id,
                    ["content"] = result.Success ? result.DataJson : $"Error: {result.Error}"
                });
            }
        }

        // ★ 2026-10-06 正常退出循环（跑到这里说明循环跑完了，没自然结束）
        // ★ 2026-10-06 修误导性日志：max_loops_natural_exit 听起来像正常完成，实际是被循环上限强制打断
        // 注：skill 步骤进度日志在 AiAssistantLogger.MarkSkillStepCompleted 单独追
        if (!brokeEarly)
            {
                AiAssistantLogger.LogLoopEnd(maxLoops, "max_loops_ceiling_reached", sb.ToString(), true,
                    $"达到 maxLoops={maxLoops} 上限，AI 工具调用已耗尽额度（可能还在 Skill 流程中途）。如未完成，请陛下手动或下条消息继续追问。");
            }

        return new AiChatTurnResult
        {
            FinalContent = sb.ToString(),
            TokensUsed = 0 // 多轮不好统计，简化
        };
        }
        finally
        {
            // ★ 2026-10-10 修误导性 exitReason
            //   之前无条件打"completed"——即使 AI 0 tool_calls + 全文分析也显示 completed
            //   陛下反馈：「还没做为什么就显示完成啊」—— AI 0 工具调用时打 completed 误导
            //   修正：3 状态分流
            //     totalToolCallsInSession > 0     → "completed"        （AI 实际干活了）
            //     brokeEarly  && == 0             → "no_action_taken"   （AI 说要做但 0 调用，说了不做）
            //     !brokeEarly && == 0             → "aborted_ceiling"   （30 轮耗光 0 调用）
            string exitReason = totalToolCallsInSession > 0
                ? "completed"
                : (brokeEarly ? "no_action_taken" : "aborted_ceiling");
            AiAssistantLogger.EndSession(exitReason);
        }
    }

    private static string? ParseStreamChunk(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                return null;
            if (choices.GetArrayLength() == 0) return null;

            var first = choices[0];
            if (!first.TryGetProperty("delta", out var delta)) return null;
            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                return content.GetString();
        }
        catch
        {
            // 解析失败忽略
        }
        return null;
    }

    private static string RoleToString(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => "user"
    };

    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var msg))
                    return msg.GetString();
                if (err.ValueKind == JsonValueKind.String)
                    return err.GetString();
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// ★ 2026-09-20 解析文本格式的工具调用（某些 AI 模型不支持标准 function calling）
    ///   支持格式: XML style <function_calls><invoke>...</invoke></function_calls>
    /// </summary>
    private static List<OpenAiToolCall> ParseTextToolCalls(string content)
    {
        var result = new List<OpenAiToolCall>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        // 匹配 <invoke name="toolName">...<parameter ...</invoke>
        var invokePattern = @"<invoke\s+name=""([^""]+)"">([\s\S]*?)</invoke>";
        var matches = Regex.Matches(content, invokePattern, RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            string toolName = m.Groups[1].Value.Trim();
            string paramsBlock = m.Groups[2].Value;

            // 解析 <parameter name="k">v</parameter>
            var paramPattern = @"<parameter\s+name=""([^""]+)"">([^<]*)</parameter>";
            var paramMatches = Regex.Matches(paramsBlock, paramPattern, RegexOptions.IgnoreCase);

            var argsDict = new Dictionary<string, object?>();
            foreach (Match p in paramMatches)
            {
                argsDict[p.Groups[1].Value.Trim()] = p.Groups[2].Value.Trim();
            }

            var argsJson = JsonSerializer.Serialize(argsDict);
            result.Add(new OpenAiToolCall
            {
                Id = "txt_tool_call_" + result.Count,
                Type = "function",
                Function = new OpenAiFunction
                {
                    Name = toolName,
                    Arguments = argsJson
                }
            });
        }

        return result;
    }

    /// <summary>
    /// ★ 2026-09-20 移除文本中的工具调用文本（function_calls/invoke/parameter 标签）
    /// </summary>
    private static string RemoveToolCallText(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        // 移除 <function_calls>...</function_calls>
        var result = Regex.Replace(content,
            @"<function_calls>[\s\S]*?</function_calls>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 移除 XML style: <function_calls><invoke...</invoke></function_calls>
        result = Regex.Replace(result,
            @"<function_calls>[\s\S]*?</function_calls>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return result.Trim();
    }

    /// <summary>
    /// ★ 2026-10-09 修「AI 嘴上说调了但 tool_calls 实际空」双重幻觉
    /// <para>陛下反馈：AI 文本说"我调了 14 次 add_knowledge_entry 全部失败"，
    /// 但本次循环 totalToolCallsInSession == 0、AI 实际一个 tool_call 都没发。
    /// 表现：AI 既骗陛下（说调了）又骗自己（说失败/返回成功）。</para>
    /// <para>检测策略（任一命中即返回非空）：</para>
    /// <list type="number">
    ///   <item>含 32 位 hex（entryId 格式）</item>
    ///   <item>含 KB Action 名 + 已调/调用了/调用成功/调用失败/添加成功/添加失败 等"过去式/结果"措辞</item>
    /// </list>
    /// <para>前置：仅当本会话累计真实 tool_calls == 0 时才检查（已真调过的不算幻觉）</para>
    /// </summary>
    /// <returns>命中则返回"幻觉描述"（会写日志 + 注入重发 prompt）；未命中返回 null</returns>
    private static string? DetectClaimedButNotCalled(string content, int totalToolCallsInSession)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        // 关键：已真调过 → AI 后续描述可视为对前序调用的汇报，不是幻觉
        if (totalToolCallsInSession > 0) return null;

        // 强信号 1：含 32 位 hex（entryId / baseId 格式），一定在描述具体某次调用结果
        if (Regex.IsMatch(content, @"[a-f0-9]{32}"))
            return "文本含 32 位 entryId/baseId 但实际未调用任何工具";

        // 强信号 2：含 KB Action 名 + 过去式/结果类措辞
        var knowledgeTools = new[]
        {
            "add_knowledge_entry", "update_knowledge_entry", "delete_knowledge_entry",
            "list_knowledge_bases", "search_knowledge", "get_knowledge_entry"
        };
        var claimPatterns = new[]
        {
            "已调", "我调", "调用了", "刚刚调", "刚才调", "成功调",
            "调用成功", "调用失败", "调失败", "调用未成功", "调用返回", "调用结果",
            "添加成功", "添加失败", "写入成功", "写入失败",
            "删除成功", "删除失败", "搜索成功", "搜索失败",
            "查询成功", "查询失败", "获取成功", "获取失败"
        };
        foreach (var tool in knowledgeTools)
        {
            if (!content.Contains(tool)) continue;
            foreach (var p in claimPatterns)
            {
                if (content.Contains(p))
                    return $"文本含「{p} {tool}」但实际未调用任何工具";
            }
        }
        return null;
    }

    // ★ 2026-10-10 删除 DetectIntentButNoAction + ExtractSnippet
    //   陛下反馈：「正常AI可不用这样一直追问，并且还不做」——
    //     a55d8b3 + 4abe607 续命机制是打地鼠（AI 永远比关键词列表聪明）
    //     而且实际不起作用（Session 110853 已验证：AI 收到 prompt 后还是只写计划不真调）
    //   修法：
    //     1) 删 DetectIntentButNoAction + ExtractSnippet + 主循环调用（已分两步删）
    //     2) BuildSystemPrompt 顶部加 CRITICAL 硬规则让 AI 一次就真调（步骤 4）
    //     3) EndSession 区分 completed / no_action_taken / aborted_ceiling（已做）
    //   保留 DetectClaimedButNotCalled：它是「撒谎」检测（AI 说「我调了 X」但 0 tool_calls），
    //     这是 hallucination，不是一般 UX 问题，不属于"打地鼠"

    /// <summary>
    /// ★ 2026-10-09 陛下要求：失败重试机制
    /// <para>触发：HTTP 错误信息含"请稍后再试" / 网络异常（HttpRequestException）/ 请求超时（排除用户主动取消）</para>
    /// <para>每次重试前等待 RetryIntervalSeconds 秒，初始 1 次 + 重试 MaxRetries 次（总计 = MaxRetries + 1）</para>
    /// <para>不重试：其他错误信息（非"请稍后再试"、非网络异常、非超时）→ 立即返回错误</para>
    /// <para>用户主动取消（ct.IsCancellationRequested=true）→ 任何 catch 都不重试，向上抛 TaskCanceledException</para>
    /// </summary>
    /// <returns>(ok, error, response) — ok=true 时 response 非空；ok=false 时 error 非空</returns>
    private async Task<(bool ok, string? error, OpenAiChatResponse? response)> SendChatRequestWithRetryAsync(
        string url, string json, string apiKey,
        int maxRetries, int intervalSeconds,
        int loopNum, CancellationToken ct)
    {
        int totalAttempts = maxRetries + 1;  // 初始 1 次 + 重试 maxRetries 次
        for (int attempt = 1; attempt <= totalAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // HttpRequestMessage 只能 send 一次，每次重试都重新 new
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var resp = await _http.SendAsync(req, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    string errMsg = ExtractErrorMessage(body) ?? $"HTTP {(int)resp.StatusCode}";
                    if (attempt < totalAttempts && errMsg.Contains("请稍后再试"))
                    {
                        AiAssistantLogger.LogRetry(loopNum, attempt, totalAttempts, intervalSeconds, errMsg);
                        await Task.Delay(intervalSeconds * 1000, ct);
                        continue;
                    }
                    return (false, errMsg, null);
                }

                var parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body);
                if (parsed?.Choices == null || parsed.Choices.Count == 0)
                    return (false, "响应中没有 choices", null);

                return (true, null, parsed);
            }
            catch (HttpRequestException ex) when (attempt < totalAttempts)
            {
                // 网络异常（DNS / 连接拒绝 / TLS 等）→ 可重试
                AiAssistantLogger.LogRetry(loopNum, attempt, totalAttempts, intervalSeconds, $"网络错误: {ex.Message}");
                await Task.Delay(intervalSeconds * 1000, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < totalAttempts)
            {
                // 请求超时（非用户主动取消）→ 可重试
                AiAssistantLogger.LogRetry(loopNum, attempt, totalAttempts, intervalSeconds, "请求超时");
                await Task.Delay(intervalSeconds * 1000, ct);
            }
        }
        return (false, "重试耗尽", null);
    }

// ========== OpenAI 响应 DTO ==========

    private class OpenAiChatResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<OpenAiChoice>? Choices { get; set; }
        [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("message")] public OpenAiMessage? Message { get; set; }
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private class OpenAiMessage
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("tool_calls")] public List<OpenAiToolCall>? ToolCalls { get; set; }
    }

    private class OpenAiToolCall
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("function")] public OpenAiFunction? Function { get; set; }
    }

    private class OpenAiFunction
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("arguments")] public string? Arguments { get; set; }
    }

    private class OpenAiUsage
    {
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }
}

/// <summary>
/// AI 后端调用结果（一次性）
/// </summary>
public class ChatReply
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public bool IsError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// 完整对话回合结果（带 tool calls 循环）
/// </summary>
public class AiChatTurnResult
{
    public string FinalContent { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}

/// <summary>
/// 一次工具调用记录（用于 UI 显示）
/// </summary>
public class ChatToolCallRecord
{
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public AiActionResult Result { get; set; } = new();
}

/// <summary>
/// AI 后端接口
/// </summary>
public interface IAiChatBackend
{
    Task<ChatReply> SendAsync(AiProviderConfig provider, List<ChatMessage> messages, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(AiProviderConfig provider, List<ChatMessage> messages, CancellationToken ct = default);

    Task<AiChatTurnResult> SendWithToolsAsync(
        AiProviderConfig provider,
        List<ChatMessage> messages,
        List<ChatToolCallRecord>? toolCallLog,
        Func<AiActionResult, Task<bool>>? onToolExecuted,
        Func<string, string, string?, Task<bool>>? onToolNeedsConfirm,
        CancellationToken ct = default);
}
