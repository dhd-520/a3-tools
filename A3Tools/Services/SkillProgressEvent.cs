using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace A3Tools.Services;

/// <summary>
/// ★ 2026-10-13 陛下需求：Skill 流程可视化（1+2 层）
///   当 AI 调 load_skill 进入 Skill 模式时，OpenAiCompatibleBackend 触发本事件，
///   AiChatForm 订阅后通过 WebView2 JS 在气泡顶部渲染 Skill 横幅 + 步骤清单。
///   后续每次 tool call 触发 progress 事件，UI 把当前步骤从 ⏸️ → ⏳ → ✅。
/// </summary>
public class SkillProgressEvent
{
    /// <summary>
    /// 阶段：loaded（刚加载完 Skill）/ progress（执行了 1 个工具，步骤推进）/ completed（Skill 流程结束）/ failed
    /// </summary>
    public string Phase { get; set; } = "";

    /// <summary>Skill 内部 name（list_skills 返回的 name 字段）</summary>
    public string SkillName { get; set; } = "";

    /// <summary>Skill 中文标题（frontmatter title 字段）</summary>
    public string SkillTitle { get; set; } = "";

    /// <summary>
    /// 解析自 Skill Markdown 正文的 "## 步骤 N: 标题" 列表
    /// 索引 0 = 第一步；CurrentStepIndex 指向当前正在执行的那一步（-1 = 未开始）
    /// </summary>
    public List<string> StepTitles { get; set; } = new();

    /// <summary>当前正在执行的步骤索引（0-based；-1 = 未开始；StepTitles.Count = 已全部完成）</summary>
    public int CurrentStepIndex { get; set; } = -1;

    /// <summary>最近一次 tool call 名称（progress 阶段用，方便日志 / 调试）</summary>
    public string? LastToolName { get; set; }

    /// <summary>失败信息（failed 阶段用）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 从 Skill Markdown 正文解析 "## 步骤 N: 标题" / "## Step N: 标题" 列表
    /// <para>★ 2026-10-13 用于 1+2 层可视化</para>
    /// <para>支持格式：
    /// <list type="bullet">
    ///   <item>## 步骤 1：xxx</item>
    ///   <item>## 步骤 1: xxx</item>
    ///   <item>## Step 1: xxx</item>
    ///   <item>## 步骤 0: 确认输入参数（0-based 也算）</item>
    /// </list>
    /// </para>
    /// </summary>
    public static List<string> ParseStepTitles(string? markdownBody)
    {
        var steps = new List<string>();
        if (string.IsNullOrEmpty(markdownBody)) return steps;

        // 匹配 "## 步骤 1：xxx" 或 "## Step 1: xxx"（冒号半全角都行）
        var rx = new Regex(
            @"^##\s*(?:步骤|Step)\s*\d+\s*[:：]\s*(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match m in rx.Matches(markdownBody))
        {
            string title = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(title))
                steps.Add(title);
        }
        return steps;
    }
}
