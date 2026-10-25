# 2026-10-06 A3Tools AI 助理 - 详细日志 + 防半截断

## 陛下反馈（背景）

> 「现在让 AI 助理处理一个问题，经常断掉，需要不断追问才能完成，不连贯。主要是在调用工具或者 Skill 就经常结束了。但问题没有处理」

**症状**：
- AI 调一个工具（甚至零个工具）→ 立即回"搞定了"
- 问题其实没真处理完
- 陛下得反复追问"然后呢？""再呢？"
- 没详细执行日志，看不出 AI 每一步在想啥

## 根因

1. **`OpenAiCompatibleBackend.SendWithToolsAsync`** 的循环 `maxLoops=5` 一旦没新工具调用就 `break`，AI 自作主张"调一个就够了"
3. **系统提示词**没强约束"先验证再结束"
4. **没 AI 助理专属日志**，只有 launcher 通用日志（read_log 读那个），区分不开

## 修法

### 1. 新建 `Services/AiActions/AiAssistantLogger.cs`

完整日志 + 早退检测，路径 `DATA/logs/ai-assistant-YYYY-MM-DD.log`。

API：
- `BeginSession(sessionId)` / `EndSession(exitReason)` — 会话生命周期
- `LogLoopStart(loop, maxLoops, messageCount)` — 每轮迭代开始
- `LogAiResponse(loop, content, toolCallCount)` — AI 响应（content 截 200 字预览）
- `LogToolCall(loop, toolName, argsJson, result)` — 工具调用 + 结果（args 截 300 字）
- `LogLoopEnd(loop, reason, finalContent, prematureWarning, warningReason)` — 退出原因
- `LogError(error, ex)` — 异常
- `DetectPrematureEnd(loop, content, totalToolCalls)` → `(bool, string)` — 早退检测

**早退检测规则**：
- 一次工具都没调过 + 有非空内容 + 不是闲聊打招呼 → ⚠️ 早退
- 调过工具但最终回复 < 30 字 → ⚠️ 早退（可能没充分利用结果）

### 2. 改 `Services/OpenAiCompatibleBackend.cs`

`SendWithToolsAsync` 方法：
- 入口：组装 `sessionId = HHmmss-用户消息前30字` + `BeginSession`
- 循环开头：`LogLoopStart(loop, maxLoops, msgs.Count)`
- AI 响应回来：`LogAiResponse(loop, content, toolCallCount)`
- 每条工具调用：`LogToolCall(...)` + `totalToolCallsInSession++`
- 无工具调用退出：`DetectPrematureEnd` → `LogLoopEnd(..., prematureWarning)`
- maxLoops 兜底：`LogLoopEnd(loop, "max_loops_reached", ...)`
- 整个方法包 `try/finally`，确保 `EndSession` 一定打

### 3. 改 `Forms/AiChatForm.cs` 系统提示词

`BuildSystemPrompt` 新增「## ⚠️ 完成验证规则（重要，2026-10-06 陛下要求）」章节：

```
## ⚠️ 完成验证规则（重要，2026-10-06 陛下要求）
在输出最终回复前，必须自检陛下的所有问题点是否都答到了。如果还有未完成项，调更多工具/Skill 继续。
  1. 不自欺：不要调一个工具就报『搞定了』，陛下问的是『完成 X』不是『看一眼 X 的数据』
  2. 多个问题点逐个核：陛下说『帮我看看 A，顺便对比 B，再确认 C』，必须 3 件事都做完
  3. Skill 步骤不偷工：调 load_skill 后正文步骤必须全部执行（不能跳过 dry_run / 跳过陛下确认）
  4. 数据查询要给真结论：execute_sql 拿到数据后必须把关键字段、对比、异常值说清楚
  5. 不调工具就回答要谨慎：陛下要求查账套/看表/读日志，必须先调工具拿真实数据再回答
  6. 卡住时可自查日志：调 read_ai_log（最近 N 行）看自己的执行历史
```

### 4. 新建 `Services/AiActions/AiLogActions.cs`

- `read_ai_log` — 读 AI 助理最近 N 行日志（lines 默认 50，最大 500，可指定 date YYYY-MM-DD）
- `list_ai_logs` — 列 DATA/logs 下所有 AI 助理日志文件清单

### 5. 改 `Services/AiActions/ActionRegistry.cs`

- 加 `using A3Tools.Services.AiActions.AiAssistantLogging;`
- 在构造函数末尾注册两个新 action

## 编译验证

```
A3Tools.Common -> ...\A3Tools.Common.dll
A3Tools.Plugins.Default -> ...\Plugins\A3Tools.Plugins.Default.dll
A3Tools -> ...\A3Tools.dll
已成功生成
4 个警告（NuGet 包版本不匹配，与本次无关）
0 个错误
```

## 文件变更清单

| 文件 | 类型 | 行数估算 |
|------|------|---------|
| `Services/AiActions/AiAssistantLogger.cs` | 新增 | ~210 |
| `Services/AiActions/AiLogActions.cs` | 新增 | ~110 |
| `Services/OpenAiCompatibleBackend.cs` | 改 | +45 / -0 |
| `Forms/AiChatForm.cs` | 改 | +9 / -0 |
| `Services/AiActions/ActionRegistry.cs` | 改 | +6 / -0 |

## 验证方法（手动跑一遍）

1. 跑发布版本：`dotnet publish D:\work\A3Tools\A3Tools\A3Tools.csproj -c Debug -o D:\work\A3Tools\publish\DebugAI -r win-x64 --self-contained false`
2. 启动 A3Tools，进 AI 聊天
3. 故意问个多步任务：「列出账套 + 选一个查它的表 + 对比其中两个表结构」
4. 观察：
   - 聊天窗口 AI 是不是逐步推进，没中途"搞定了"
   - `DATA/logs/ai-assistant-2026-10-06.log` 是不是每步都有 `LOOP_START / AI_RESPONSE / TOOL_CALL / LOOP_END` 记录
   - 如果 AI 提早结束，日志里会打 `⚠️ EARLY_END: ...`
5. 再问个复杂的：「加载 skill xxx 并完整执行所有步骤」
6. 让 AI 调 `read_ai_log` 自查执行轨迹

## 风险 & 注意事项

- **namespace 对齐坑**：AiAssistantLogger.cs 必须放在 `A3Tools.Services.AiActions.AiAssistantLogging` 子 namespace，OpenAiCompatibleBackend.cs 和 ActionRegistry.cs 都得 `using` 这个子 namespace，否则编译错 CS0246
- **PowerShell 转义坑**：`""` 在 PowerShell 双引号字符串里会变成单个 `"`，写 C# `?? "";` 时要小心，用字节级替换最稳
- **日志文件位置**：`DATA/logs/` 目录不存在时自动创建，写失败不影响 AI 助理运行
- **sessionId 设计**：`HHmmss-用户消息前30字`，短时间多次同问题也能看出区别，便于陛下在日志里 grep

## 后续 TODO

- [ ] 跑一次手动测试，确认早退警告真的出现
- [ ] 如果实测还有别的"半截断"模式（比如 AI 跳步骤、读 Skill 不执行），再加新的早退检测规则
- [ ] 考虑把 AI 助理日志接到 launcher 主日志面板（DataGridView 显示）

---

## 续：陛下实测反馈 + maxLoops 拉高（2026-10-06 10:05）

### 陛下反馈

> 「还是没有操作完，看看什么问题」

附上日志 DATA/logs/ai-assistant-2026-10-06.log，sessionId 100259-从表单提取表结构加入知识库____账套___标准账套8088...，共 7 步工具调用，最后 LOOP_END reason=max_loops_natural_exit。

### 日志揭示 3 件事

- **maxLoops=5 不够**：实际跑 7 个工具调用就撞天花板，但 Skill dd_form_schema_to_kb 共 7 个步骤（0-7），平均每步 1-2 个工具 → 需要 **10-15 轮**才走得完
- **AI 实际只跑到第 3 步**：list_sql → load_skill → list_accounts → execute_sql×2 → get_table_schema×2，但**关键步骤 5-7 没做**（recommend KB、add_knowledge_entry、写报告）
- **Log 名字误导**：max_loops_natural_exit 听起来像"正常完成"，实际是"循环到顶被强制打断"

### Skill 步骤 vs AI 实际进度（对照表）

| Skill 步骤 | AI 做了没 | 工具调用 |
|-----------|----------|---------|
| 步骤 0：确认输入参数 | ✅ | (内省) |
| 步骤 1：get_account_detail | ⚠️ 用 list_accounts 代替 | loop=2 |
| 步骤 2：execute_sql 查 S_CONTROL | ✅ | loop=2/3 |
| 步骤 3：按 VIEWNAME 分组 | ✅ | (内省) |
| 步骤 4：get_table_schema 过滤关联字段 | ⚠️ 半做（get_table_schema 调了，但没过滤 DATANAME）| loop=4 |
| **步骤 5：recommend_knowledge_target** | ❌ 没做 | - |
| **步骤 6：add_knowledge_entry** | ❌ 没做 | - |
| **步骤 7：报告** | ❌ 没做 | - |

**结论：AI 调了 7 个工具就撞 maxLoops=5，被强制中断。Skill 整个入库流程根本没跑完，陛下的 KB 一条没加。**

### 修法

1. **maxLoops 5 → 15**
   - 文件：Services/OpenAiCompatibleBackend.cs
   - 注释写明"add_form_schema_to_kb 等多步骤 Skill 需要 8-10 轮"
2. **Log 改名 + 警告升级**
   - max_loops_natural_exit → max_loops_ceiling_reached
   - 不再传 alse 早退警告，改传 	rue + 警告语"AI 工具调用已耗尽额度（可能还在 Skill 流程中途）。如未完成，请陛下手动或下条消息继续追问"
3. **系统提示词加规则 #7**
   - 「跑不完别装跑完：如果循环上限（maxLoops=15）用尽仍未完成 Skill/任务，在最终回复里**明说『未完成』**让陛下决定是否继续。**绝对不要假装『已完成』**」

### 编译验证

`
D:\work\A3Tools\A3Tools\bin\Debug\net7.0-windows\A3Tools.dll
LastWriteTime: 2026/10/6 10:09:37  Size: 778752 bytes
358 个警告（历史遗留，与本次无关）
0 个错误
`

### 下一步验证

请陛下重跑一次同一个任务（"从表单提取表结构加入知识库，账套 001/0080/0082，表单内码 S_SCM_OLITEM_INPUT"）：
- 应该看到 AI 完整执行到步骤 7，给出报告
- 日志末尾应是 LOOP_END reason=max_loops_ceiling_reached 但不再有 ⚠️ 早退警告（因为确实走完了）
- 或者更理想：AI 走完后自己 break，日志显示 LOOP_END reason=no_tool_calls_response 且 inalContentLen > 500

### TODO（后续）

- [ ] 给 Skill 加"步骤进度日志"（load_skill 后解析"步骤 N"提取总步骤，每完成一个工具调用打 SKILL_PROGRESS: M/N）
- [ ] 让 AI 在循环快到顶时主动提醒"还剩 2 轮，要不要继续"，避免一次性撞墙
- [ ] 考虑把 maxLoops 做成配置项（AiChatConfig），不同 Skill 给不同上限
- [ ] 进一步观察：实际跑完后是否还会遇到 Skill 步骤没做完的情况

---

## 续 2：陛下第二轮实测 + Skill 强制续命器（2026-10-06 10:26）

### 陛下反馈（第二轮）

> 「hai 是没加进去」（附 101434 + 101626 两个 session 的 AI 助理日志）

### 这次日志暴露的真问题

maxLoops=15 改对了，但**只改 maxLoops 不够**！即使有 15 轮额度，AI 在 loop 5 就主动 break：

`
session 101434（从表单提取表结构加入知识库）:
  loop 0: list_skills + list_accounts
  loop 1: load_skill  ← skillMode=true（之前没有续命器）
  loop 2: get_account_detail + execute_sql
  loop 3: get_table_schema×2 + list_knowledge_bases
  loop 4: search_knowledge×2
  loop 5: toolCalls=0, contentLen=2980  ← AI 写一大段"整理过滤统计"就 break ❌
  ⚠️ 真正的问题：调了 9 个工具（list_skills + load_skill + ... + search_knowledge）
     但 add_knowledge_entry / recommend_knowledge_target 一个都没调 → KB 一条没加！

session 101626（"确认"）:
  loop 0: toolCalls=0, contentLen=12678  ← AI 写了 12678 字"我会怎么怎么调"
     但 0 工具调用！⚠️ 早退警告正确触发
  但 AI 还是没真调 add_knowledge_entry
`

### 根因（更深层）

**DeepSeek R1 这种推理模型把"想清楚 = 任务完成"**：
- 看到所有数据 → 写总结 → break → 把"分析了"当成"做完了"
- 不会主动再调写动作工具

maxLoops 再大都没用，因为 AI **主动选择提前 break**。

### 修法：Skill 工作流强制续命器（2026-10-06 10:30 上线）

#### 1. 新建 Services/AiActions/SkillWorkflowEnforcer.cs

`csharp
public static class SkillWorkflowEnforcer
{
    // 写动作工具白名单（调用后会真正产生持久化副作用）
    public static readonly HashSet<string> WriteTools = new(...)
    {
        "add_knowledge_entry", "update_knowledge_entry",
        "create_knowledge_base", "move_knowledge_entry",
        "execute_ddl",
        "add_account", "delete_account",
        "start_account", "stop_account", "launch_link_db",
        "recommend_knowledge_target",
    };

    public static bool IsWriteTool(string toolName) { ... }

    // ★ 核心：返回当前未调用的必需 write-tools
    public static List<string> GetMissingWriteTools(bool skillMode, HashSet<string> calledWriteTools);

    // 构造续命 prompt（注入到 messages 强制下一轮）
    public static string BuildContinuationPrompt(List<string> missingTools);
}
`

#### 2. 改 Services/OpenAiCompatibleBackend.cs

**加 3 个追踪变量**：
`csharp
bool skillMode = false;             // load_skill 是否调过
var calledWriteTools = new HashSet<string>();
bool brokeEarly = false;              // 修双重日志 bug
`

**工具调用后更新追踪**：
`csharp
if (toolName == "load_skill") skillMode = true;
if (SkillWorkflowEnforcer.IsWriteTool(toolName)) calledWriteTools.Add(toolName);
`

**AI 返回 0 工具调用时（关键的早退拦截点）**：
`csharp
// 原本直接 break，现在先检查 write-tools
var missingWriteTools = SkillWorkflowEnforcer.GetMissingWriteTools(skillMode, calledWriteTools);
if (missingWriteTools.Count > 0 && loop + 1 < maxLoops)
{
    string prompt = SkillWorkflowEnforcer.BuildContinuationPrompt(missingWriteTools);
    msgs.Add(new { role = "user", content = prompt });
    LogLoopEnd(loop, "skill_continuation_injected", ...);
    continue;  // ★ 不 break！
}
else
{
    LogLoopEnd(loop, "no_tool_calls_response", ...);
    brokeEarly = true;
    break;
}
`

**修双重 LOOP_END bug**：
`csharp
if (!brokeEarly)  // 只有循环跑完才打 ceiling 日志
{
    LogLoopEnd(maxLoops, "max_loops_ceiling_reached", ...);
}
`

#### 3. 改 Forms/AiChatForm.cs 系统提示词

加规则 #8：
`
8. **写动作必须真调**：add_knowledge_entry / execute_ddl / create_knowledge_base /
   update_knowledge_entry / move_knowledge_entry / add_account / delete_account 这些工具
   调用后会真正产生持久化副作用（写入 KB / 改 DB / 改账套）。绝对不要在回复里"描述你会
   怎么调"代替真调——只要还没调用这些工具，Skill 工作流就没完成
`

### 编译验证

`
dotnet build -c Debug --no-incremental
→ 已成功生成
→ 359 个警告（历史遗留，与本次无关）
→ 0 个错误
→ A3Tools.dll: 781,312 bytes（+2,560 vs 上版）
`

### 实测预期行为

下次陛下重跑"从表单提取表结构加入知识库"：
- loop 5 时 toolCalls=0 → 触发 skill_continuation_injected 日志
- 注入 prompt："你还没调 add_knowledge_entry，Skill 工作流未完成，继续！"
- loop 6 AI 被逼调 recommend_knowledge_target
- loop 7 AI 调 add_knowledge_entry × 2 张表
- loop 8 AI 返回 toolCalls=0 + 完整报告
- 日志结尾 LOOP_END reason=no_tool_calls_response 正常结束

### 后续 TODO

- [ ] 陛下重测拿同一个 Skill 验证续命器真触发
- [ ] 如果续命器仍然失败（AI 不响应注入的 prompt），考虑：
  - 在 prompt 里加更明确的工具调用示范
  - 给 write-tools 加独立的 system message reminder
- [ ] 给 Skill 步骤追踪（之前想做 SetSkillSteps/MarkSkillStepCompleted，能跳更对但能精确到哪一步没做）
- [ ] 给 DDL 类 Skill 单独的 dry_run → 确认 → 执行三步走（陛下确认后才能真调 execute_ddl）

---

## 续 3：高危操作改成"只点确认，不要输入"（2026-10-06 10:47）

### 陛下反馈

> 「高危操作没法输入，能改成不要输入么，只要确认就行」

### 改动范围

之前 3 个高危 action 要陛下输入指定文本才能放行：

| Action | 之前 | 之后 |
|--------|------|------|
| DeleteAccountAction | 输入账套编码 | 点 [允许] 即可 |
| ExecuteDdlAction | 输入 DDL 前 80 字 | 点 [允许] 即可 |
| DeleteKnowledgeEntryAction | 输入条目标题前 4 字 | 点 [允许] 即可 |

**安全机制**：弹窗还在，【允许】/【拒绝】两个按钮不变，只是去掉了"输入文本验证"环节。

### 改动详情

3 个文件的修改：

**1. Services/AiActions/AiActions.cs（DeleteAccountAction）**
- 删除私有字段 private string? _confirmCode;
- 删除赋值 _confirmCode = code;
- ConfirmationPrompt => _confirmCode → ConfirmationPrompt => null

**2. Services/AiActions/DdlActions.cs（ExecuteDdlAction）**
- 删除私有字段 private string? _confirmSnippet;
- 删除赋值（改成 inline string）：string snippet = sql.Length > 80 ? sql.Substring(0, 80) + "..." : sql;
- ConfirmationPrompt => _confirmSnippet → ConfirmationPrompt => null
- 影响描述去掉 ${_confirmSnippet} 引用

**3. Services/AiActions/KnowledgeActions.cs（DeleteKnowledgeEntryAction）**
- 删除私有字段 private string? _confirmText;
- 删除赋值 _confirmText = title.Length >= 4 ? title.Substring(0, 4) : title;
- ConfirmationPrompt => _confirmText → ConfirmationPrompt => null
- 影响描述去掉 \n陛下必须输入「{_confirmText}」才能放行 部分
- 更新 4 处过期文档（class 注释、Description 字符串等）

### 实现机制

ConfirmActionForm.cs 已经支持两种模式（普通/高危输入），代码逻辑不用动：
- confirmText == null → 只显示 [允许] / [拒绝]（橙色顶条）
- confirmText != null → 显示输入框 + [允许]（需输入正确才启用）/ [拒绝]（红色顶条）

把 3 个 action 的 ConfirmationPrompt 改成 
ull 后，弹窗自动走普通确认模式。

### 编译验证

`
dotnet build -c Debug --no-incremental
→ 0 个错误
→ A3Tools.dll: 781,312 bytes（与上一版同尺寸，因为字段删除 + 代码重构 IL 体积相当）
→ LastWriteTime: 2026/10/6 10:52:45
`

### 中间插曲（坑）

第一次批量改完报错 error CS1002: 应输入 ; — 原因是我的"删除私有字段"逻辑里 startIdx =  - 4 多减了 4 字节，把**上一行** 	rue;\r\n 的 e;\r\n 一并吃掉了，导致文件变成：

`csharp
public bool RequiresConfirmation => tr    public string? ConfirmationPrompt => null;
`

修复方式：byte-level 找 RequiresConfirmation => tr    public string? 模式，替换成 RequiresConfirmation => true;\r\n    public string?，3 个文件全部修好。

### 后续 TODO

- [ ] 陛下下次重测同样的任务，确认 3 个高危操作都是弹窗点 [允许] 即可，不用输入
- [ ] 如果陛下想恢复输入确认（安全更保守），把 ConfirmationPrompt => null 改回对应变量即可
- [ ] 考虑在 AiConfigService 加配置项 HighRiskRequireInput = false，让陛下能切换模式