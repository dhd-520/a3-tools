using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using A3Tools.Common.DataAccess;
using A3Tools.Models;

namespace A3Tools.Services.AiActions;

/// <summary>
/// ★ 2026-09-20 陛下需求：让 AI 拿视图定义（用于 Skill 升级账套时把源库视图搬到目标库）
///   SQL Server: OBJECT_DEFINITION(OBJECT_ID('view_name')) 从 sys.sql_modules 拿原文
/// </summary>
public class GetViewDefinitionAction : IAiAction
{
    public string Name => "get_view_definition";
    public string Description => "获取视图的 CREATE VIEW 定义语句（SQL Server: OBJECT_DEFINITION）。用于 Skill 升级账套时把源库视图搬到目标库。";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["view_name"] = new { type = "string", description = "视图名" }
        },
        required = new string[] { "code", "view_name" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var vn = arguments.TryGetValue("view_name", out var v) ? v?.ToString() ?? "" : "";
        return $"读取视图 [{vn}] 的 CREATE 定义（账套 [{code}]，只读）";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string viewName = arguments.TryGetValue("view_name", out var v) ? v?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(viewName)) return AiActionResult.Fail("请提供视图名");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);
            // SQL Server: 用 OBJECT_DEFINITION 拿视图定义（防注入：转义单引号）
            string safeName = viewName.Replace("'", "''");
            string sql = $"SELECT OBJECT_DEFINITION(OBJECT_ID('{safeName}')) AS definition";
            var queryResult = await dataAccess.ExecuteQueryAsync(sql, ct);

            if (!queryResult.Success)
                return AiActionResult.Fail($"读取视图定义失败：{queryResult.Message}");
            if (queryResult.Tables == null || queryResult.Tables.Count == 0 ||
                queryResult.Tables[0].Rows.Count == 0)
                return AiActionResult.Fail($"视图 [{viewName}] 不存在或无定义");

            string definition = queryResult.Tables[0].Rows[0][0]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(definition))
                return AiActionResult.Fail($"视图 [{viewName}] 没有可用的 CREATE 定义");

            return AiActionResult.Ok($"视图 [{viewName}] 定义已获取（{definition.Length} 字符）", new
            {
                code,
                view_name = viewName,
                definition,
                length = definition.Length
            });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"读取视图定义失败：{ex.Message}");
        }
    }
}

/// <summary>
/// ★ 2026-09-20 陛下需求：执行 DDL（CREATE/ALTER/DROP）
///   高危：必须陛下二次确认（执行前 AI 会弹窗要求陛下确认 DDL 内容）
///   用于 Skill 升级账套时创建表/视图/字段
/// </summary>
public class ExecuteDdlAction : IAiAction
{
    public string Name => "execute_ddl";
    public string Description => "执行 DDL 语句（CREATE TABLE/VIEW/INDEX、ALTER TABLE、DROP 等）。⚠️ 高危操作，必须陛下二次确认。建议先 dry_run=true 只看不执行。建议先在测试账套验证。";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["sql"] = new { type = "string", description = "DDL 语句（CREATE/ALTER/DROP/TRUNCATE），单语句" },
            ["dry_run"] = new { type = "boolean", description = "可选 true=只解析不执行（默认 false）" }
        },
        required = new string[] { "code", "sql" }
    };

    public bool RequiresConfirmation => true;
    private string? _confirmSnippet;
    public string? ConfirmationPrompt => _confirmSnippet;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
        // 提取前 80 字作为陛下二次确认时看到的提示
        _confirmSnippet = sql.Length > 80 ? sql.Substring(0, 80) + "..." : sql;
        return $"在账套 [{code}] 上执行 DDL：{_confirmSnippet}";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
            bool dryRun = arguments.TryGetValue("dry_run", out var d) &&
                          bool.TryParse(d?.ToString(), out var b) && b;

            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(sql)) return AiActionResult.Fail("请提供 DDL 语句");

            // ★ 安全检查 1：必须以 DDL 开头（CREATE/ALTER/DROP/TRUNCATE）
            string upper = sql.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(upper, @"^\s*(CREATE|ALTER|DROP|TRUNCATE)\b"))
            {
                return AiActionResult.Fail("不是 DDL 语句。execute_ddl 只允许 CREATE/ALTER/DROP/TRUNCATE，其他查询请用 execute_sql");
            }
            // ★ 安全检查 2：拦截数据修改
            if (Regex.IsMatch(upper, @"\b(INSERT\s+INTO|UPDATE\s+|DELETE\s+FROM|EXEC|EXECUTE|MERGE\s+)\b"))
            {
                return AiActionResult.Fail("包含禁止的关键字。execute_ddl 不允许 INSERT/UPDATE/DELETE/EXEC/MERGE");
            }
            // ★ 安全检查 3：拦截多语句（DDL 必须单条执行，避免误操作）
            var trimmed = sql.Trim().TrimEnd(';');
            int semicolonCount = trimmed.Count(c => c == ';');
            if (semicolonCount > 0)
                return AiActionResult.Fail($"execute_ddl 不允许多语句（检测到 {semicolonCount} 个分号），请拆分后逐条执行");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);

            if (dryRun)
            {
                // dry_run 模式：只回显 SQL，不执行（陛下可在执行前先看一遍）
                return AiActionResult.Ok("Dry-run 模式：未执行任何 DDL", new
                {
                    code,
                    mode = "dry_run",
                    sql_preview = sql,
                    sql_length = sql.Length
                });
            }

            // ★★★ 实际执行 DDL
            int affected = await dataAccess.ExecuteNonQueryAsync(sql, ct);
            return AiActionResult.Ok($"DDL 执行成功（受影响行数 {affected}）", new
            {
                code,
                mode = "executed",
                sql_preview = sql.Length > 200 ? sql.Substring(0, 200) + "..." : sql,
                affected_rows = affected
            });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"执行 DDL 失败：{ex.Message}");
        }
    }
}