using System.Text.Json;
using System.Text.Json.Serialization;

namespace A3Tools.Models;

/// <summary>
/// AI 可调用的操作权限
/// </summary>
[Flags]
public enum AiActionPermission
{
    /// <summary>读本地文件</summary>
    ReadLocal = 1,
    /// <summary>写本地文件</summary>
    WriteLocal = 2,
    /// <summary>访问网络</summary>
    Network = 4,
    /// <summary>启动 / 停止进程</summary>
    Process = 8,
    /// <summary>只读本地 + 网络（默认安全档）</summary>
    Safe = ReadLocal | Network
}

/// <summary>
/// Action 执行结果
/// </summary>
public class AiActionResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; } = true;

    /// <summary>结果数据（JSON 字符串，AI 会看到这些数据来组织回复）</summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>简短描述（给陛下看 + 写日志用）</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>错误信息（失败时）</summary>
    public string Error { get; set; } = string.Empty;

    public static AiActionResult Ok(string summary, object? data = null) => new()
    {
        Success = true,
        Summary = summary,
        DataJson = data == null ? "{}" : JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false })
    };

    public static AiActionResult Fail(string error) => new()
    {
        Success = false,
        Error = error,
        Summary = $"失败：{error}"
    };
}

/// <summary>
/// AI 可调用的本地操作
/// </summary>
public interface IAiAction
{
    /// <summary>Action 唯一名（英文，OpenAI tools.function.name 用）</summary>
    string Name { get; }

    /// <summary>中文描述（给陛下看）</summary>
    string Description { get; }

    /// <summary>权限范围</summary>
    AiActionPermission Permission { get; }

    /// <summary>参数 Schema（JSON Schema 格式，简化版）</summary>
    object ParametersSchema { get; }

    /// <summary>是否需要陛下二次确认（默认 true，写操作 / 进程操作必须确认）</summary>
    bool RequiresConfirmation => true;

    /// <summary>高危操作：需要陛下输入指定文本确认（null = 普通确认）</summary>
    /// <example>delete_account 返回 "002"，陛下必须输入 002 才放行</example>
    string? ConfirmationPrompt => null;

    /// <summary>中文影响描述（弹窗里展示给陛下看）</summary>
    string GetImpactDescription(Dictionary<string, object?> arguments);

    /// <summary>执行（参数是 AI 传来的 JSON 字典）</summary>
    Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default);
}
