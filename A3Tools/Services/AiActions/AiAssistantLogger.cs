using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using A3Tools.Models;


namespace A3Tools.Services.AiActions.AiAssistantLogging;

/// <summary>
/// ★ 2026-10-06 陛下反馈：AI 助理在调用工具 / Skill 后经常戛然而止，问题没真处理完
/// <para>修法：</para>
/// <list type="number">
///   <item>每次 Function Calling 循环打详细日志（每轮 AI 响应 / 工具调用 / 退出原因）</item>
///   <item>检测"早退"风险：最后一轮没工具调用 + 内容过短 → 打 ⚠️ 警告</item>
///   <item>提供 read_ai_log 工具让 AI 也能自查日志（调试闭环）</item>
/// </list>
/// <para>日志路径：<c>DATA/logs/ai-assistant-YYYY-MM-DD.log</c></para>
/// </summary>
public static class AiAssistantLogger
{
    private static readonly object _fileLock = new();
    private static readonly string _logDir = Path.Combine(AppContext.BaseDirectory, "DATA", "logs");

    /// <summary>
    /// ★ 2026-10-06 陛下要求：AI 日志总开关（由 AiSettingsForm 保存后同步过来）
    /// <para>true=记录到 DATA/logs/ai-assistant-YYYY-MM-DD.log（默认）</para>
    /// <para>false=静默运行，不创建文件、不占用 IO</para>
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>当前会话 ID（每次 SendWithToolsAsync 开始设置，结束清理）</summary>
    [ThreadStatic]
    private static string? _currentSessionId;

    /// <summary>当前会话开始时间</summary>
    [ThreadStatic]
    private static DateTime _sessionStart;

    /// <summary>当前会话累计调用的工具数</summary>
    [ThreadStatic]
    private static int _totalToolCalls;

    /// <summary>当前会话累计 AI 响应字符数</summary>
    [ThreadStatic]
    private static int _totalResponseChars;

    /// <summary>
    /// 设置当前会话上下文（OpenAiCompatibleBackend.SendWithToolsAsync 开始时调）
    /// </summary>
    public static void BeginSession(string sessionId)
    {
        _currentSessionId = sessionId;
        _sessionStart = DateTime.Now;
        _totalToolCalls = 0;
        _totalResponseChars = 0;
        Log("SESSION_BEGIN", $"sessionId={sessionId}");
    }

    /// <summary>
    /// 清理当前会话上下文
    /// </summary>
    public static void EndSession(string exitReason)
    {
        var elapsed = DateTime.Now - _sessionStart;
        Log("SESSION_END",
            $"exitReason={exitReason} totalToolCalls={_totalToolCalls} totalResponseChars={_totalResponseChars} elapsedMs={elapsed.TotalMilliseconds:F0}");
        _currentSessionId = null;
    }

    /// <summary>
    /// 记录 Function Calling 循环的每一轮开始
    /// </summary>
    public static void LogLoopStart(int loop, int maxLoops, int messageCount)
    {
        Log("LOOP_START", $"loop={loop}/{maxLoops} messages={messageCount}");
    }

    /// <summary>
    /// 记录 AI 的响应（content + tool_calls）
    /// </summary>
    public static void LogAiResponse(int loop, string content, int toolCallCount)
    {
        _totalResponseChars += content?.Length ?? 0;
        string preview = (content ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
        if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
        Log("AI_RESPONSE", $"loop={loop} toolCalls={toolCallCount} contentLen={content?.Length ?? 0} preview=\"{preview}\"");
    }

    /// <summary>
    /// 记录工具调用
    /// </summary>
    public static void LogToolCall(int loop, string toolName, string argsJson, AiActionResult result)
    {
        _totalToolCalls++;
        string argsPreview = (argsJson ?? "{}").Replace('\n', ' ');
        if (argsPreview.Length > 300) argsPreview = argsPreview.Substring(0, 300) + "...";
        string resultSummary = result.Success
            ? $"OK: {result.Summary}"
            : $"FAIL: {result.Error}";
        if (resultSummary.Length > 300) resultSummary = resultSummary.Substring(0, 300) + "...";
        Log("TOOL_CALL", $"loop={loop} tool={toolName} args={argsPreview} → {resultSummary}");
    }

    /// <summary>
    /// 记录循环退出原因（详细分析是早退还是正常完成）
    /// </summary>
    /// <param name="prematureWarning">true = 检测到早退风险（最后一轮无工具调用 + 内容过短）</param>
    /// <param name="warningReason">早退的具体原因</param>
    public static void LogLoopEnd(int loop, string reason, string? finalContent, bool prematureWarning = false, string? warningReason = null)
    {
        string contentLen = (finalContent ?? string.Empty).Length.ToString();
        string warn = prematureWarning ? $" ⚠️ EARLY_END: {warningReason}" : "";
        Log("LOOP_END", $"loop={loop} reason={reason} finalContentLen={contentLen}{warn}");
    }

    /// <summary>
    /// 记录异常
    /// </summary>
    public static void LogError(string error, Exception? ex = null)
    {
        string details = ex != null ? $"{error} | {ex.GetType().Name}: {ex.Message}" : error;
        Log("ERROR", details);
    }

    /// <summary>
    /// ★ 2026-10-09 失败重试事件
    /// <para>OpenAiCompatibleBackend.SendChatRequestWithRetryAsync 每次等待重试前调一次</para>
    /// </summary>
    /// <param name="loop">Function Calling 循环的第几轮（0-based）</param>
    /// <param name="attempt">当前是第几次尝试（1-based，初始为 1）</param>
    /// <param name="totalAttempts">总尝试次数（= MaxRetries + 1）</param>
    /// <param name="intervalSeconds">重试前等待秒数</param>
    /// <param name="reason">触发重试的原因（"请稍后再试"原文 / "网络错误: xxx" / "请求超时"）</param>
    public static void LogRetry(int loop, int attempt, int totalAttempts, int intervalSeconds, string reason)
    {
        Log("RETRY", $"loop={loop} attempt={attempt}/{totalAttempts} wait={intervalSeconds}s reason=\"{reason}\"");
    }

    /// <summary>
    /// 基础日志写入（线程安全，自动按日期切文件）
    /// </summary>
    private static void Log(string eventType, string details)
    {
        // ★ 2026-10-06 陛下要求：总开关关闭时跳过所有写入
        if (!Enabled) return;

        try
        {
            Directory.CreateDirectory(_logDir);
            string file = Path.Combine(_logDir, $"ai-assistant-{DateTime.Now:yyyy-MM-dd}.log");
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{_currentSessionId ?? "-"}] [{eventType}] {details}";
            lock (_fileLock)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // 日志失败不影响 AI 助理运行
        }
    }

    /// <summary>
    /// ★ 给 read_ai_log Action 用：返回最近的 N 行日志
    /// </summary>
    public static string ReadRecentLines(int lines, string? date = null)
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            string? targetFile = null;
            if (!string.IsNullOrWhiteSpace(date))
            {
                string candidate = Path.Combine(_logDir, $"ai-assistant-{date}.log");
                if (File.Exists(candidate)) targetFile = candidate;
            }
            if (targetFile == null)
            {
                var files = Directory.GetFiles(_logDir, "ai-assistant-*.log")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count > 0) targetFile = files[0];
            }
            if (targetFile == null) return "（暂无 AI 助理日志）";

            var allLines = File.ReadAllLines(targetFile);
            var tail = allLines.TakeLast(Math.Clamp(lines, 1, 500)).ToArray();
            return $"📄 {Path.GetFileName(targetFile)}（共 {allLines.Length} 行，返回最后 {tail.Length} 行）\n\n" +
                   string.Join("\n", tail);
        }
        catch (Exception ex)
        {
            return $"读取失败：{ex.Message}";
        }
    }

    /// <summary>
    /// ★ 早退检测：最后一轮没工具调用 + 内容过短，可能是 AI 自作主张"搞定了"
    /// <para>返回 (isPremature, reason)</para>
    /// </summary>
    public static (bool IsPremature, string Reason) DetectPrematureEnd(int loop, string? finalContent, int totalToolCalls)
    {
        // 1. 一次工具都没调过 + 有内容 → 可能根本没用工具就回答
        if (totalToolCalls == 0 && !string.IsNullOrWhiteSpace(finalContent))
        {
            // 例外：明显是闲聊 / 打招呼 / 解释 → 不算早退
            string lower = finalContent.Trim().ToLower();
            if (lower.Length < 50 &&
                (lower.Contains("你好") || lower.Contains("您好") || lower.Contains("hello") ||
                 lower.Contains("hi") || lower.Contains("请问") || lower.Contains("需要")))
            {
                return (false, "");
            }
            return (true, "未调用任何工具就给出最终回复（陛下可能希望 AI 主动探索/查询后再回答）");
        }

        // 2. 调过工具但最后内容很短（< 30 字）→ 可能没充分利用结果
        if (totalToolCalls > 0 && !string.IsNullOrWhiteSpace(finalContent) && finalContent.Length < 30)
        {
            return (true, $"调了 {totalToolCalls} 个工具但最终回复仅 {finalContent.Length} 字（可能没充分组织结果给陛下）");
        }

        return (false, "");
    }
}