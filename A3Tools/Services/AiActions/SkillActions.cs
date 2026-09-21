using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using A3Tools.Models;

namespace A3Tools.Services.AiActions;

/// <summary>
/// ★ 2026-09-20 陛下需求：让 AI 列出所有可用 Skill
/// </summary>
public class ListSkillsAction : IAiAction
{
    public string Name => "list_skills";
    public string Description => "列出所有可用 Skill（陛下自己编写的工作流，存放在 DATA/skills/*.md）。AI 根据 description 判断何时该调哪个 Skill。";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "列出可用 Skill";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var skills = SkillRegistry.Instance.List();
            var data = skills.Select(s => new
            {
                name = s.Name,
                title = s.Title,
                category = s.Category,
                description = s.Description,
                parameters = s.Parameters.Count == 0 ? null : s.Parameters,
                requires_confirmation = s.RequiresConfirmation
            }).ToList();
            return Task.FromResult(AiActionResult.Ok($"找到 {skills.Count} 个 Skill", new
            {
                count = skills.Count,
                skills_directory = SkillRegistry.Instance.SkillsDirectory,
                skills = data
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// ★ 2026-09-20 陛下需求：让 AI 读取 Skill 完整内容
/// </summary>
public class LoadSkillAction : IAiAction
{
    public string Name => "load_skill";
    public string Description => "读取 Skill 完整 Markdown 内容（含步骤、注意事项）。AI 按正文中的步骤，调用现有工具（list_tables/compare_table_schemas/execute_ddl 等）完成工作流。";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["name"] = new { type = "string", description = "Skill 名称（list_skills 返回的 name 字段）" }
        },
        required = new string[] { "name" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var name = arguments.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
        return $"读取 Skill [{name}] 完整内容（只读）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string name = arguments.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult(AiActionResult.Fail("请提供 Skill 名称"));

            var skill = SkillRegistry.Instance.Get(name);
            if (skill == null)
                return Task.FromResult(AiActionResult.Fail(
                    $"Skill [{name}] 不存在。陛下可以用 list_skills 查看可用 Skill，或在目录 " +
                    $"{SkillRegistry.Instance.SkillsDirectory} 下创建 {name}.md"));

            return Task.FromResult(AiActionResult.Ok($"Skill [{name}] 加载成功", new
            {
                name = skill.Name,
                title = skill.Title,
                category = skill.Category,
                requires_confirmation = skill.RequiresConfirmation,
                parameters = skill.Parameters,
                file_path = skill.FilePath,
                body = skill.Body  // Markdown 正文（含步骤 + 注意事项）
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}