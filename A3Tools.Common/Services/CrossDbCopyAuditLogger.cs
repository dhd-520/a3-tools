using System.IO;

namespace A3Tools.Common.Services;

/// <summary>
/// ★ 2026-10-10 方案 4 陛下要求：跨库复制审计日志
/// <para>每次跨库复制操作（成功/失败）都追加一行到 <c>DATA/logs/ai_cross_db_copy_audit.log</c></para>
/// <para>字段：时间 | 源账套 | 目标账套 | 对象类型 | 对象名 | 状态 | 错误信息</para>
/// <para>用 lock 防并发写入；写入失败不影响业务（吞掉异常）</para>
/// <para>陛下可随时用文本编辑器查看审计日志，追溯 AI 所有跨库复制行为</para>
/// </summary>
public static class CrossDbCopyAuditLogger
{
    private static readonly object _lock = new();
    private static readonly string _logDir = Path.Combine(AppContext.BaseDirectory, "DATA", "logs");
    private static readonly string _logPath = Path.Combine(_logDir, "ai_cross_db_copy_audit.log");

    /// <summary>
    /// 写一条审计日志
    /// </summary>
    /// <param name="sourceAccount">源账套编码</param>
    /// <param name="targetAccount">目标账套编码</param>
    /// <param name="objectType">对象类型（U/V/TF/FN/P 或业务类型）</param>
    /// <param name="objectNames">对象名（多个用分号分隔）</param>
    /// <param name="success">是否成功</param>
    /// <param name="error">错误信息（成功时传 null）</param>
    /// <param name="warning">警告信息（如某些对象跳过）</param>
    public static void Log(
        string sourceAccount,
        string targetAccount,
        string objectType,
        string objectNames,
        bool success,
        string? error = null,
        string? warning = null)
    {
        try
        {
            var line = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff} | src={1} | tgt={2} | type={3} | objects={4} | status={5} | warning={6} | error={7}",
                DateTime.Now,
                sourceAccount,
                targetAccount,
                objectType,
                objectNames,
                success ? "OK" : "FAIL",
                warning ?? "",
                error ?? "");

            lock (_lock)
            {
                Directory.CreateDirectory(_logDir);
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // 审计日志写入失败不影响业务（吞掉异常）
        }
    }
}
