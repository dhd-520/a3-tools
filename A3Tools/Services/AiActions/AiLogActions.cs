using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using A3Tools.Models;

namespace A3Tools.Services.AiActions.AiAssistantLogging;

/// <summary>
/// ★ 2026-10-06 陛下反馈：AI 助理调完工具/Skill 后经常戛然而止
/// <para>配套 read_ai_log 工具：让 AI 自己读自己的执行日志，调试闭环 + 自我审计</para>
/// <para>日志路径：<c>DATA/logs/ai-assistant-YYYY-MM-DD.log</c></para>
/// </summary>
public class ReadAiLogAction : IAiAction
{
    public string Name => "read_ai_log";
    public string Description => "读取 AI 助理最近的执行日志（每轮迭代 / AI 响应 / 工具调用 / 退出原因）。用于卡住时自我审计，或陛下要求时报告执行轨迹";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["lines"] = new { type = "integer", description = "读取行数，默认 50，最大 500" },
            ["date"] = new { type = "string", description = "可选，指定日期 YYYY-MM-DD，不传则读最新的" }
        },
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        int lines = arguments.TryGetValue("lines", out var l) && int.TryParse(l?.ToString(), out var n) ? Math.Clamp(n, 1, 500) : 50;
        return $"读取 AI 助理最近 {lines} 行日志（只读）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            int lines = 50;
            if (arguments.TryGetValue("lines", out var l) && int.TryParse(l?.ToString(), out var n))
                lines = Math.Clamp(n, 1, 500);

            string? date = arguments.TryGetValue("date", out var d) ? d?.ToString() : null;
            string content = AiAssistantLogger.ReadRecentLines(lines, date);

            return Task.FromResult(AiActionResult.Ok($"AI 助理日志（{lines} 行）", new
            {
                requested_lines = lines,
                date = date ?? "today",
                content
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail($"读取 AI 日志失败：{ex.Message}"));
        }
    }
}

/// <summary>
/// ★ 2026-10-06 调试辅助：列出当前可用的 AI 助理日志文件
/// </summary>
public class ListAiLogsAction : IAiAction
{
    public string Name => "list_ai_logs";
    public string Description => "列出 DATA/logs 下所有 AI 助理日志文件（按日期），用于陛下查看历史会话调试轨迹";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "列出 AI 助理日志文件清单";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string logDir = Path.Combine(AppContext.BaseDirectory, "DATA", "logs");
            if (!Directory.Exists(logDir))
                return Task.FromResult(AiActionResult.Ok("日志目录不存在", new { logDir, files = Array.Empty<string>() }));

            var files = Directory.GetFiles(logDir, "ai-assistant-*.log")
                .Select(f => new
                {
                    file = Path.GetFileName(f),
                    date = Path.GetFileNameWithoutExtension(f).Replace("ai-assistant-", ""),
                    size_kb = new FileInfo(f).Length / 1024,
                    modified = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
                })
                .OrderByDescending(x => x.date)
                .ToList();

            return Task.FromResult(AiActionResult.Ok($"找到 {files.Count} 个 AI 助理日志文件", new
            {
                log_directory = logDir,
                count = files.Count,
                files
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}