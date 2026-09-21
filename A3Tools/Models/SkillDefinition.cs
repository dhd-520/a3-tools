using System.Collections.Generic;

namespace A3Tools.Models;

/// <summary>
/// ★ 2026-09-20 陛下需求：让 AI 能使用陛下自己编写的 Skill（多步骤流程）
///   Skill = Markdown 文件 + 简易 YAML frontmatter
///   存放在 DATA/skills/*.md
///   AI 通过 list_skills / load_skill 工具读取 Skill 内容，按步骤执行
/// </summary>
public class SkillDefinition
{
    /// <summary>唯一标识（也是文件名去掉 .md）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>人类可读标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>分类标签（如"账套升级"、"数据迁移"）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>AI 看这个判断何时调用</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Markdown 正文（步骤、注意事项等）</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>是否需要陛下二次确认（提示用，AI 在执行高危操作前会再次弹窗）</summary>
    public bool RequiresConfirmation { get; set; } = true;

    /// <summary>参数说明（key → 描述），多个用 ; 分隔 key=value</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>文件路径</summary>
    public string FilePath { get; set; } = string.Empty;
}