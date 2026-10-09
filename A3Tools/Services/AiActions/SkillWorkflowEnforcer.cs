using System;
using System.Collections.Generic;
using System.Linq;

namespace A3Tools.Services.AiActions.AiAssistantLogging;

/// <summary>
/// ★ 2026-10-06 陛下反馈：AI 调 load_skill 后在准备阶段就 break，假装"分析完了"，
///   但 add_knowledge_entry / execute_ddl / create_knowledge_base 这些 write-tools 没调 → KB 一条没加
/// <para>本类是 <b>Skill 工作流强制续命器</b>：</para>
/// <list type="number">
///   <item>列出所有"必须真调"的写动作工具（write-tools）</item>
///   <item>OpenAiCompatibleBackend 在循环里追踪已调用的 write-tools</item>
///   <item>AI 返回 0 工具调用 但 write-tools 还有没调过的 → 注入续命 prompt 强制下一轮</item>
/// </list>
/// </summary>
public static class SkillWorkflowEnforcer
{
    /// <summary>
    /// 所有"完成 Skill 必须真调"的写动作工具
    /// <para>判断标准：调用后会产生持久化副作用（写 KB / 改 DB / 改账套 / 落盘）</para>
    /// </summary>
    public static readonly HashSet<string> WriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // 知识库写入
        "add_knowledge_entry",
        "update_knowledge_entry",
        "create_knowledge_base",
        "move_knowledge_entry",
        // DDL/写库
        "execute_ddl",
        // 账套变更
        "add_account",
        "delete_account",
        // 进程变更（启动/停止算"工作流改变")
        "start_account",
        "stop_account",
        "launch_link_db",
        // Skill 工作流标记（load_skill 后必须后续调这些才算完成）
        "add_knowledge_entry",
        "recommend_knowledge_target",
    };

    /// <summary>
    /// 判断某个工具是否是 write-tool
    /// </summary>
    public static bool IsWriteTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return false;
        return WriteTools.Contains(toolName);
    }

    /// <summary>
    /// ★ 核心：返回当前 Skill 未调用的必需 write-tools 列表
    /// <para>在 Skill 模式下（load_skill 已调），如果 AI 还没调这些工具就 break，
    ///   说明 Skill 工作流没完成，必须续命</para>
    /// </summary>
    /// <param name="skillMode">是否在 Skill 执行模式（load_skill 是否调过）</param>
    /// <param name="calledWriteTools">本会话已调过的 write-tools 集合</param>
    /// <param name="writeRequired">
    ///   ★ 2026-10-06 增：该 Skill 是否需要写工具
    ///   <para>read-only Skill（如 find_form_location、check_form_health）传 false，跳过检查避免死循环</para>
    /// </param>
    /// <returns>还没调过的必需 write-tools</returns>
    public static List<string> GetMissingWriteTools(bool skillMode, HashSet<string> calledWriteTools, bool writeRequired = true)
    {
        if (!skillMode) return new List<string>();

        // ★ 2026-10-06 增：read-only Skill 不强制写工具
        //   不然 find_form_location 这种只读 Skill 会一直续命到 maxLoops=30 卡好久
        if (!writeRequired) return new List<string>();

        // Skill 模式下，必须调过的最少子集
        // 知识库类 Skill → 必须调 add_knowledge_entry 或 create_knowledge_base
        // DDL 类 Skill → 必须调 execute_ddl
        // 通用兜底 → 至少要有一个 write-tool 被调过
        var mustCallTools = new List<string>();

        if (!calledWriteTools.Contains("add_knowledge_entry") &&
            !calledWriteTools.Contains("create_knowledge_base") &&
            !calledWriteTools.Contains("update_knowledge_entry") &&
            !calledWriteTools.Contains("move_knowledge_entry") &&
            !calledWriteTools.Contains("execute_ddl"))
        {
            mustCallTools.Add("add_knowledge_entry / create_knowledge_base / execute_ddl（至少一个）");
        }

        return mustCallTools;
    }

    /// <summary>
    /// ★ 构造续命 prompt（注入到 messages 里强制 AI 继续）
    /// </summary>
    public static string BuildContinuationPrompt(List<string> missingTools)
    {
        if (missingTools == null || missingTools.Count == 0) return string.Empty;

        return "⚠️ **续命提示**：检测到你刚 load_skill 进入了 Skill 执行模式，\n" +
               "但当前会话还没调用任何「写动作」工具（如 " + string.Join(" / ", missingTools) + "）。\n\n" +
               "**Skill 工作流未完成**——光看数据 / 整理统计不算完成，必须**真的调用**对应的写工具：\n" +
               "- 知识库类 Skill → 调 `recommend_knowledge_target` 选 KB + `add_knowledge_entry` 写入\n" +
               "- DDL 类 Skill → 调 `execute_ddl`（记得先 dry_run=true 给陛下看预览）\n" +
               "- 账套类 Skill → 调 `add_account` / `delete_account` 等\n\n" +
               "**不要**在最终回复里\"描述你会怎么写\"——必须真的调工具执行。\n" +
               "现在请继续 Skill 的剩余步骤，调用必需工具。";
    }
}