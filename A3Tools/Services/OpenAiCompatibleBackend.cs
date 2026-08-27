using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using A3Tools.Models;
using A3Tools.Services.AiActions;

namespace A3Tools.Services;

/// <summary>
/// OpenAI 兼容协议的 AI 后端调用器（支持流式 + Function Calling）
/// <para>★ 2026-08-26 支持厂商：OpenAI / DeepSeek / 通义千问 / 智谱 / 月之暗面 / MiniMax / Ollama / 自定义</para>
/// </summary>
public class OpenAiCompatibleBackend : IAiChatBackend
{
    private readonly HttpClient _http;
    private readonly ActionRegistry _actions = ActionRegistry.Instance;

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

        // 复制消息列表（Function Calling 循环会追加 tool 消息）
        // 用 List<object> 避免 anonymous type shape 不一致
        var msgs = new List<object>();
        foreach (var m in messages)
        {
            msgs.Add(new { role = RoleToString(m.Role), content = m.Content });
        }

        const int maxLoops = 5; // 防止 AI 无限循环
        var sb = new StringBuilder();

        for (int loop = 0; loop < maxLoops; loop++)
        {
            ct.ThrowIfCancellationRequested();

            var requestBody = new
            {
                model = provider.Model,
                messages = msgs,
                temperature = 0.7,
                tools = toolsSchema,
                tool_choice = "auto"
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

            // 没有 tool_calls → 结束
            if (message.ToolCalls == null || message.ToolCalls.Count == 0)
                break;

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

        return new AiChatTurnResult
        {
            FinalContent = sb.ToString(),
            TokensUsed = 0 // 多轮不好统计，简化
        };
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
