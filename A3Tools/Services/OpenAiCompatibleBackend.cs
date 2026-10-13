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
        Action<SkillProgressEvent>? onSkillProgress = null,
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
        // ★ 2026-10-13 增：Skill 流程可视化（1+2 层）追踪
        string currentSkillName = "";                       // Skill name（用于事件 payload）
        string currentSkillTitle = "";                      // Skill title
        List<string> currentSkillSteps = new();             // 解析的步骤标题列表
        int currentSkillStepIndex = -1;                     // 当前步骤索引（-1=未开始）
        // ★ 2026-10-13 增：进入 Skill 模式后已发过的 progress 事件数（防止一轮跑出多个 tool call 时重复发火）
        bool brokeEarly = false;             // 是否 break（修双重日志 bug）
        // ★ 2026-10-06 增：当前加载的 Skill 是否需要写工具（read-only Skill 跳过续命检查，避免死循环到 maxLoops）
        bool currentSkillWriteRequired = true;  // 默认 true（安全起见，未明确标记的 Skill 仍走严格检查）

        // ★ 2026-10-10 陛下反馈：「我感觉是没提取」—— V1（a55d8b3/4abe607）只关键词匹配不提工具名
        //   V2：预拉所有可用工具名 + 提取 AI 文本里提的具体工具 + Skill 风格步骤 prompt
        //   关键洞察（陛下）：Skill 模式 = 模型「按步骤执行」= 真发 tool_calls
        //                  追问模式 = 模型「按承诺执行」= 0 tool_calls
        //   修法：让追问 prompt 长得像 Skill 步骤，模型才能照着做
        var availableToolNames = _actions.All().Select(a => a.Name).ToList();
        int intentContinuationCount = 0;     // 注入过几次结构化续命 prompt（限 2 次防环）

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

                    // ★ 2026-10-10 修「AI 嘴上说要干但没真干」V2 版（提取工具名 + Skill 风格 prompt）
                    //   V1（a55d8b3/4abe607）只词匹配不提具体工具 → 陛下反馈「没提取」→ 没用
                    //   V2: 预拉所有可用工具名 + 提取 AI 文本里的具体工具 + Skill 风格步骤 prompt
                    //   关键：Skill 模式 = 模型「按步骤走」= 真发 tool_calls
                    //        追问模式 = 模型「按承诺走」= 0 tool_calls
                    //   修法：追问 prompt 长得像 Skill 步骤，模型才会照着做
                    //   限 2 次：超过则不再注入（防止无限环），跳进 break 路径 → no_action_taken
                    var intentResult = DetectIntentAndExtractTools(
                        message.Content ?? string.Empty,
                        totalToolCallsInSession,
                        availableToolNames);
                    if (intentResult.Detected
                        && intentContinuationCount < 2
                        && loop + 1 < maxLoops)
                    {
                        string prompt = BuildStructuredContinuationPrompt(
                            message.Content ?? string.Empty,
                            intentResult.Reason,
                            intentResult.ExtractedTools,
                            availableToolNames);
                        msgs.Add(new { role = "user", content = prompt });
                        AiAssistantLogger.LogLoopEnd(loop, "intent_but_no_action_v2", sb.ToString(), true,
                            $"AI 文本含行动意图但未真发 tool_calls（{intentResult.Reason}），已提取工具 [{string.Join(",", intentResult.ExtractedTools)}]，注入 Skill 风格续命 prompt（第 {intentContinuationCount + 1}/2 次）");
                        intentContinuationCount++;
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
                            if (sk != null)
                            {
                                currentSkillWriteRequired = sk.WriteRequired;
                                // ★ 2026-10-13 优化：同一个 Skill 重复 load 不重建横幅、不重置步骤
                                //   之前每次 load_skill 都触发 loaded 事件 → JS 删掉旧横幅 + 重建 → 闪一下
                                //   现在：同 name 的二次 load 只更新元数据（write_required），步骤索引也不重置
                                //   跨 Skill 调 load_skill 仍重建（新横幅 + 步骤 0）
                                bool isNewSkill = string.IsNullOrEmpty(currentSkillName) || currentSkillName != sk.Name;
                                currentSkillName = sk.Name;
                                currentSkillTitle = sk.Title ?? "";
                                if (isNewSkill)
                                {
                                    currentSkillSteps = SkillProgressEvent.ParseStepTitles(sk.Body);
                                    currentSkillStepIndex = -1;  // -1 = 未开始（首次 load 才重置）
                                }
                                if (isNewSkill && onSkillProgress != null)
                                {
                                    onSkillProgress(new SkillProgressEvent
                                    {
                                        Phase = "loaded",
                                        SkillName = currentSkillName,
                                        SkillTitle = currentSkillTitle,
                                        StepTitles = new List<string>(currentSkillSteps),
                                        CurrentStepIndex = -1
                                    });
                                }
                            }
                        }
                    }
                    catch { /* 解析失败保持默认 true */ }
                }
                if (SkillWorkflowEnforcer.IsWriteTool(toolName)) calledWriteTools.Add(toolName);

                // ★ 2026-10-13 增：Skill 流程可视化 progress 事件
                //   每次 tool call 都推进一步（粗略：AI 调 N 个工具 = N 个步骤；最坏情况最后一步会被压缩）
                //   真正准确的步骤-工具映射留给 Layer 3（Skill frontmatter 加 step_tool_mapping 字段）
                if (skillMode && currentSkillSteps.Count > 0 && onSkillProgress != null)
                {
                    // load_skill 自己不算步骤推进（步骤 0 才算开始），其他所有 tool call 都推进一步
                    if (toolName != "load_skill")
                    {
                        currentSkillStepIndex = Math.Min(currentSkillStepIndex + 1, currentSkillSteps.Count - 1);
                    }
                    onSkillProgress(new SkillProgressEvent
                    {
                        Phase = "progress",
                        SkillName = currentSkillName,
                        SkillTitle = currentSkillTitle,
                        StepTitles = new List<string>(currentSkillSteps),
                        CurrentStepIndex = currentSkillStepIndex,
                        LastToolName = toolName
                    });
                }

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

        // ★ 2026-10-13 增：Skill 流程可视化 completed 事件
        //   正常结束（maxLoops 自然耗尽或 break）时发火，UI 把所有步骤标为 ✅
        if (skillMode && onSkillProgress != null)
        {
            onSkillProgress(new SkillProgressEvent
            {
                Phase = brokeEarly ? "completed" : "aborted",
                SkillName = currentSkillName,
                SkillTitle = currentSkillTitle,
                StepTitles = new List<string>(currentSkillSteps),
                CurrentStepIndex = currentSkillSteps.Count  // 表示全部完成
            });
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
    /// ★ 2026-10-12 修「AI 0 tool_calls 收工」陛下反馈：AI 把工具调用意图写进 <thinking> 块
    ///   现象：content 第一个字符是 <，被 <think>...</think> 包裹，ParseTextToolCalls 全文匹配 XML 失败
    ///   修法：解析前先剥离 <think>...</think> 块
    /// </summary>
    private static string StripThinkingBlocks(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        // 剥离 <think>...</think> 块（some 模型用 <thinking> 也兼容）
        var result = Regex.Replace(content, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<thinking[\s\S]*?</thinking>", "", RegexOptions.IgnoreCase);
        return result.Trim();
    }

    /// <summary>
    /// ★ 2026-09-20 解析文本格式的工具调用（某些 AI 模型不支持标准 function calling）
    ///   支持格式: XML style <function_calls><invoke>...</invoke></function_calls>
    ///   ★ 2026-10-12 修：解析前先剥思考块（陛下反馈 AI 把工具调用意图写进 thinking 不真发）
    /// </summary>
    private static List<OpenAiToolCall> ParseTextToolCalls(string content)
    {
        var result = new List<OpenAiToolCall>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        // ★ 2026-10-12 陛下反馈：AI 把工具调用意图写进 <think> 块没真发 → 先剥
        content = StripThinkingBlocks(content);
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

        // ★ 2026-10-12 V2/V3：部分 AI 写 JSON 数组格式（替代 XML）
        //   陛下反馈日志里 AI 写的：「tool_calls = [{name:"copy_win_form", arguments:{... 填必要参数 ...}}]」
        //   V2 bug：regex 要求 key 带引号（"name"），但 AI 实际写的是 JS 风格不带引号（name:）→ regex 匹配失败 → args={}
        //   V3 修法：让 key 的引号可有可无（同时支持 "name" 和 name 两种写法）
        if (result.Count == 0)
        {
            // 引号可有可无：兼容 [{name:"X", arguments:{...}}] 和 [{"name":"X","arguments":{...}}] 两种风格
            var jsonArrPattern = @"\[\s*\{\s*""?name""?\s*:\s*""?(\w[\w\-]*)""?\s*,\s*""?arguments""?\s*:\s*(\{[\s\S]*?\})\s*\}\s*\]";
            var jsonMatches = Regex.Matches(content, jsonArrPattern, RegexOptions.IgnoreCase);
            foreach (Match m in jsonMatches)
            {
                string toolName = m.Groups[1].Value.Trim();
                string argsJsonRaw = m.Groups[2].Value.Trim();
                // AI 写的可能不是标准 JSON（key 没引号），尝试宽松解析
                Dictionary<string, object?> argsDict = ParseLooseJson(argsJsonRaw);
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
        }

        return result;
    }

    /// <summary>
    /// ★ 2026-10-12 V2：宽松解析 JS 风格的 JSON（key 没引号：{name:"x"} 而不是 {"name":"x"}）
    ///   AI 写的工具调用参数常是 JS 对象字面量，不是标准 JSON
    /// </summary>
    private static Dictionary<string, object?> ParseLooseJson(string raw)
    {
        var result = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        // 先试标准解析
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(raw) ?? new();
        }
        catch { /* 不是标准 JSON，走宽松解析 */ }

        // 宽松解析：key 可有可无引号、value 是字符串/数字/布尔/null
        // 格式：name: "value" 或 name: value
        var kvPattern = @"(\w+)\s*:\s*(?:""([^""]*)""|(\d+\.?\d*|false|true|null))";
        var matches = Regex.Matches(raw, kvPattern, RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            string key = m.Groups[1].Value;
            string strVal = m.Groups[2].Value;
            string litVal = m.Groups[3].Value;
            if (!string.IsNullOrEmpty(strVal))
                result[key] = strVal;
            else if (litVal == "true") result[key] = true;
            else if (litVal == "false") result[key] = false;
            else if (litVal == "null") result[key] = null;
            else if (double.TryParse(litVal, out var d)) result[key] = d;
            else result[key] = litVal;
        }
        return result;
    }

    /// <summary>
    /// ★ 2026-09-20 移除文本中的工具调用文本（function_calls/invoke/parameter 标签）
    /// ★ 2026-10-12 V2：扩展移除 JSON 数组和 Markdown 代码块格式
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

        // ★ 2026-10-12 V2：移除 JSON 数组格式（tool_calls = [{...}]）
        result = Regex.Replace(result,
            @"\[\s*\{[\s\S]*?""name""[\s\S]*?\}[\s\S]*?\]",
            string.Empty,
            RegexOptions.IgnoreCase);

        // ★ 2026-10-12 V2：移除 Markdown ```json [...]``` 代码块
        result = Regex.Replace(result,
            @"```(?:json)?\s*[\s\S]*?```",
            string.Empty,
            RegexOptions.IgnoreCase);

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

    // ★ 2026-10-10 V2 续命器（陛下 11:26 反馈「我感觉是没提取」）
    //   2f9a14d 删了 V1（a55d8b3/4abe607），2f9a14d 加 BuildSystemPrompt 顶部 CRITICAL 规则 + EndSession 3 状态分流
    //   但 AI 还是 0 tool_calls（Session 110853/111718 验证），顶部 prompt 规则不起作用
    //   陛下 11:26 进一步反馈：「为什么命中 Skill 就能有条不紊调工具，但继续追问就不行」
    //   原因：
    //     Skill 模式 = 模型「按步骤走」= 真发 tool_calls
    //     追问模式 = 模型「按承诺走」= 0 tool_calls（LLM 普遍特性，不看模型）
    //   V2 修法：追问 prompt 长得像 Skill 样 —— 给具体步骤 + 提取的工具名
    //   关键代码路径：DetectIntentAndExtractTools() → ExtractToolNamesFromContent() → BuildStructuredContinuationPrompt()
    //   限 2 次（intentContinuationCount 计数）：超过进 break 路径 → no_action_taken
    //   保留 DetectClaimedButNotCalled（撒谎检测，不是追问）

    /// <summary>
    /// ★ 2026-10-10 V2 续命器：检测意图 + 提取 AI 实际提到的工具名
    /// <para>现象：AI 文本说"我打算 search_knowledge + update_knowledge_entry" + 0 tool_calls</para>
    /// <para>根因（V1）：只检测关键词（必须/立即/按系统约束），不提取 AI 说的具体工具 → 追问 prompt 太抽象</para>
    /// <para>修法：正则 \b 词边界匹配 availableToolNames（_actions.All()） → 拿到 AI 说的具体工具</para>
    /// <para>返回：detected=true/false + reason（触发原因） + extractedTools（AI 提到的工具名列表）</para>
    /// </summary>
    private static (bool Detected, string Reason, List<string> ExtractedTools) DetectIntentAndExtractTools(
        string content, int totalToolCallsInSession, List<string> availableToolNames)
    {
        if (string.IsNullOrWhiteSpace(content)) return (false, "", new List<string>());
        // 关键：已真调过 → AI 后续描述是对前序调用的汇报，不是意图
        if (totalToolCallsInSession > 0) return (false, "", new List<string>());

        // 1) 元意识强信号（单条即触发）：AI 自己承认没真做
        var metaAwarePatterns = new[]
        {
            "我意识到", "我刚才犯了", "我刚才漏了", "我前面漏了", "我上轮漏了", "我刚才忘了",
            "我没真正", "我并没有真正", "我并未真正", "我实际没有",
            "说了但没", "空口说", "嘴上说", "说但没真", "但没真调", "但没真做",
            "上轮回复", "上轮我", "上一轮我", "我前面说的", "我刚才没"
        };
        foreach (var p in metaAwarePatterns)
        {
            if (content.Contains(p))
            {
                var tools = ExtractToolNamesFromContent(content, availableToolNames);
                return (true, $"AI 元意识承认未真调（{p}）", tools);
            }
        }

        // 2) 意图关键词 + 行动动词（双条才触发）
        //   3 层关键词（计划 / 承诺紧急 / 引用规则 / 顺序连接）
        var intentKeywords = new[]
        {
            "让我先", "让我", "打算", "准备", "我需要", "我应该", "我将", "我会", "我得", "我要",
            "必须", "立即", "立刻", "马上", "赶紧", "right now", "asap",
            "按系统约束", "按规则", "按要求", "系统约束", "规则要求", "约束是",
            "然后", "接下来", "接着"
        };
        var actionVerbs = new[]
        {
            "调", "调用", "查", "查一下", "查一查", "搜索", "搜", "看",
            "改", "更新", "修正", "替换", "写", "写入", "添加", "删", "删除",
            "search", "update", "query", "find", "list", "delete", "remove"
        };
        int intentHit = 0, actionHit = 0;
        foreach (var k in intentKeywords) if (content.Contains(k)) intentHit++;
        foreach (var v in actionVerbs) if (content.Contains(v)) actionHit++;

        if (intentHit > 0 && actionHit > 0)
        {
            var tools = ExtractToolNamesFromContent(content, availableToolNames);
            return (true, $"AI 文本含意图（{intentHit} 词）+ 行动动词（{actionHit} 词）", tools);
        }
        return (false, "", new List<string>());
    }

    /// <summary>
    /// 从 AI 文本中提取实际提到的工具名（正则 \b 词边界匹配）
    /// <para>例：「我打算调 search_knowledge 拿 4 条」 → ["search_knowledge"]</para>
    /// <para>例：「先 list_tables 然后 get_table_schema」 → ["list_tables", "get_table_schema"]</para>
    /// <para>空例：「我必须立即执行 update」 → []（"update" 不是工具名，"update_knowledge_entry" 才是）</para>
    /// </summary>
    private static List<string> ExtractToolNamesFromContent(string content, List<string> availableToolNames)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(content) || availableToolNames == null) return found;
        foreach (var tool in availableToolNames)
        {
            if (string.IsNullOrEmpty(tool)) continue;
            // \b 词边界匹配（工具名是英文 snake_case，中文文本不干扰）
            if (Regex.IsMatch(content, $@"\b{Regex.Escape(tool)}\b", RegexOptions.IgnoreCase))
            {
                found.Add(tool);
            }
        }
        return found;
    }

    /// <summary>
    /// ★ 2026-10-10 V2 prompt 构造器：Skill 风格步骤模板
    /// <para>陛下 11:24 反馈：「为什么命中 Skill 就能有条不紊调工具，但继续追问就不行」</para>
    /// <para>原因：Skill 给结构（Step 1 / Step 2），追问给承诺（"请继续"）</para>
    /// <para>修法：让追问 prompt 长成 Skill 样 —— 给具体步骤 + 提取的工具名</para>
    /// <para>输出：中文 Skill 风格 prompt（Step 1/N + 具体 tool 名）</para>
    /// </summary>
    private static string BuildStructuredContinuationPrompt(
        string originalContent, string reason, List<string> extractedTools, List<string> availableToolNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你刚才说要执行以下操作，但 tool_calls=[]（没真发工具调用）。");
        sb.AppendLine($"检测理由：{reason}");
        sb.AppendLine();

        if (extractedTools.Count > 0)
        {
            sb.AppendLine($"**已从你文本里提取的工具**（用 \\\\b 词边界正则匹配）：{string.Join(", ", extractedTools)}");
            sb.AppendLine();
            sb.AppendLine("**请按以下 Skill 风格步骤直接发 tool_calls（不要在 content 里描述）**：");
            sb.AppendLine();
            for (int i = 0; i < extractedTools.Count; i++)
            {
                var tool = extractedTools[i];
                sb.AppendLine($"  Step {i+1}: tool_calls = [{{name:\"{tool}\", arguments:{{... 填必要参数}}}}]");
                sb.AppendLine($"  Step {i+1}.5: 等待结果");
            }
            sb.AppendLine($"  Step {extractedTools.Count+1}: content 写「已完成 X / 已更新 N 条」给陛下");
            sb.AppendLine();
            sb.AppendLine("**如果不知道参数怎么填**：");
            sb.AppendLine("  - 先调 list_xxx / get_xxx 查（例：list_accounts / list_tables / get_table_schema）");
            sb.AppendLine("  - 等查到了再调真正的 write tool");
        }
        else
        {
            // 未提到具体工具名 —— 列出所有 list/get/search 类读工具让 AI 自己选
            sb.AppendLine("**未从你文本里提取到具体工具名**（你只说了「我打算 / 必须」等空话，没说调啥）。");
            sb.AppendLine();
            sb.AppendLine("**当前可用「读」工具清单**（先调这些查数据）：");
            var readTools = availableToolNames
                .Where(n => n.StartsWith("list_") || n.StartsWith("get_") || n.StartsWith("search_") || n.StartsWith("read_"))
                .ToList();
            foreach (var t in readTools) sb.AppendLine($"  - {t}");
            sb.AppendLine();
            sb.AppendLine("**通用 Skill 步骤**：");
            sb.AppendLine("  Step 1: 调上面 list_xxx / get_xxx 之一查数据");
            sb.AppendLine("  Step 2: 调真正的 write tool（update/add/delete/execute）");
            sb.AppendLine("  Step 3: content 写结果给陛下");
        }

        sb.AppendLine();
        sb.AppendLine("**关键**：从现在起不要在 content 里写「我打算 / 让我 / 必须 / 立即」等承诺。直接发 tool_calls。");
        return sb.ToString();
    }

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
        Action<SkillProgressEvent>? onSkillProgress = null,
        CancellationToken ct = default);
}
