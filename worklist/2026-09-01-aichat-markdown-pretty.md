# 2026-09-01 AI 气泡 - Markdown 完整美化(表格 + 标题 + 粗体 + 列表 + 代码 + think 标签)

## 状态
✅ Build 成功 0 错；陛下手动测试通过(9:53「勉强可以看了」)→ 待 commit

## 陛下要求(全天多次)
- 09:13 「昨天改到一半吧,气泡用 label + 简单 markdown 支持,现在不能编译了看看」
- 09:19 「气泡支持 label+简单 markdown 文本,比如 table,现在 table 展示非常乱」
- 09:35 「标题或者字体能支持上么 比如 ## ** 这些」
- 09:47 「`<think></think>` 的标签再 markdown 代表什么,能不能也处理下」

## 🎯 核心问题
8-29 commit 的气泡用 **Panel + Label**(`AutoSize=true` 自撑开),Label 是纯文本控件:
- ❌ 渲染不出 HTML 表格 → 看到 `<table><tr><td>` 乱码
- ❌ 局部加粗/字号做不到 → `##` `**` `-` 等 Markdown 标记原样显示
- ❌ DeepSeek-R1/Qwen-QwQ 推理模型的 `<think>...` 思考过程无价值但显示出来

## 🛠 解决方案总览

### 转换映射表
| Markdown 输入 | Label 显示 | 处理函数 |
|---|---|---|
| `# 一级标题` | `━━━━ 一级标题 ━━━━` | MarkdownToPrettyText |
| `## 二级标题` | `━━ 二级标题 ━━` | MarkdownToPrettyText |
| `### 三级` | `▌ 三级` | MarkdownToPrettyText |
| `####+` | `▸ 标题` | MarkdownToPrettyText |
| `**粗体**` | `【粗体】` | MarkdownToPrettyText(行内正则) |
| `` `代码` `` | `「代码」` | MarkdownToPrettyText(行内正则) |
| `- 列表项` | `• 列表项` | MarkdownToPrettyText |
| `\| col \| col \|` | `名称 │ 值`(对齐) | MarkdownTableToText |
| `col │ col` 预格式化 | 重新对齐 | MarkdownTableToText |
| `<think>...</think>` | (删除) | RemoveThinkTags |

### 代码流程(L290 起)
```csharp
if (!isUser && !isSystem)
{
    displayContent = displayContent ?? string.Empty;

    // 1. 删掉 <think> 块(DeepSeek/Qwen 推理过程)
    displayContent = RemoveThinkTags(displayContent);

    // 2. 全局处理标题/粗体/列表/代码(Unicode 符号)
    displayContent = MarkdownToPrettyText(displayContent);

    // 3. 局部处理表格(对齐纯文本)
    if (ContainsMarkdownTable(displayContent))
        displayContent = MarkdownTableToText(displayContent);
    else
    {
        // 4. 走 Markdig 处理代码块/链接,再去标签
        var html = Markdown.ToHtml(displayContent, _mdPipeline);
        // ... 字符级去标签
        displayContent = DecodeHtmlEntities(sb.ToString()).Trim();
    }
}
```

## 改动文件
`A3Tools/Forms/AiChatForm.cs` (+270/-8 累计)

| 位置 | 内容 |
|---|---|
| L290-302 | if 块:接入 RemoveThinkTags + MarkdownToPrettyText + MarkdownTableToText 三级处理 |
| L905-919 新增 | `RemoveThinkTags()` 正则删除 <think> 块 |
| L921-932 增强 | `ContainsMarkdownTable()` 支持 `│` `┼` 风格 |
| L935-1010 重写 | `MarkdownTableToText()` 支持 `│` 输入 |
| L1019-1067 新增 | `MarkdownToPrettyText()` Unicode 符号模拟 |

## ⚠️ 重要踩坑(必须记下来!)

### 坑 #1 — `git restore` 撤掉未提交代码(差点丢光今天的工作!)
09:35 我用 PowerShell 字节数组清理 BOM,`[..]` 范围运算符对 byte[] 有 bug,文件状态异常。我**错误地**用 `git restore A3Tools/Forms/AiChatForm.cs`,结果把 8-31 写的 Markdown 表格代码全部撤掉了。

`git fsck --dangling` 找了一圈 → **没有任何 dangling blob 是 AiChatForm.cs** → 完全丢失。

**教训**:
1. **永远不要用 `git restore <file>`** — 不可逆
2. **改字节前必须备份**:`cp 原文件 worklist/YYYY-MM-DD-baseline-xxx.cs`
3. **清理 BOM 用 `read` + `write` 工具重写文件**(read 出来的内容自带干净 BOM)
4. Baseline 备份在 `worklist/2026-09-01-baseline-AiChatForm.cs`(51704 字节,8-29 commit 状态)

### 坑 #2 — C# 12 集合表达式在 .NET 7 编译不过
代码用了 `[char]10`、`['│', '┼']` 这种 C# 12 集合表达式(.NET 8 SDK 才支持),但项目是 `<TargetFramework>net7.0-windows</TargetFramework>`,默认 C# 11。

**统一改用**:
- `content.Split('\n')` 代替 `content.Split([char]10)`
- `trimmed.Split(new[] { '│', '┼' }, ...)` 代替 `trimmed.Split(['│', '┼'], ...)`

### 坑 #3 — 处理顺序错(L282 if 块)
最初写成「表格优先」:检测到表格 → 只走 `MarkdownTableToText`,**完全跳过** `MarkdownToPrettyText`。
结果:含表格的 AI 消息里,表格外的 `##` `**` `-` 全部没替换。

**修复**:交换顺序 — Pretty 先跑(全局生效)→ 再处理表格(局部)

### 坑 #4 — `│` 表格检测漏掉(陛下 9:40 反馈)
最初 `ContainsMarkdownTable` 只检测 `|` 开头的标准 Markdown 表格,不检测 AI 经常输出的 `│` `┼` 预格式化表格 → 走 `MarkdownToPrettyText` → 表格原样显示(没对齐)。

`MarkdownTableToText.isPreAligned` 还要求 `>= 2` 个分隔符,但 2 列只有 1 个 → 改成 `>= 1`(`Contains('│') || Contains('┼')`)。

## Build 验证
- 0 错
- 343 warning(全是项目原有 nullable/CS8604 警告,跟本次改动无关)

## 验证步骤(已通过)
1. F5 启动 A3Tools
2. 打开 AI 助力窗口
3. 让 AI 回复含 `##` `**` `-` `` ` `` + 表格的消息
4. 看气泡:`━━ ... ━━` `【...】` `•` `「...」` + 对齐 `│` 表格
5. 用 DeepSeek-R1 测 `<think>` 块 → 思考过程消失,只显示最终答案

## 已知遗留(暂不处理)
- 代码块 ``` 里的 `|` 会被误判为表格(概率低)
- 表格里的 `**` 已走 Pretty 处理(`**` → `【】`),但表格单元格内的链接 `[text](url)` 不处理(交给 Markdig 处理时,Markdig 在去标签阶段被剥离)
- emoji 显示成 □(Microsoft YaHei UI 字体不带 emoji 字体) → 陛下看不到 📊 等,只能看到方框

## 临时文件(不 commit)
- `worklist/2026-09-01-baseline-AiChatForm.cs` — 8-29 commit 备份,51704 字节,回滚用
- `worklist/2026-09-01-baseline-backup.diff` — 空的,废弃
- `worklist/2026-09-01-test-pretty.csx` — 单文件测试脚本
- `worklist/test-pretty/` — 独立测试项目(Program.cs 多次改写验证逻辑)

## 下一步:知识库部分(09:54 陛下指示)
- 具体范围待陛下澄清:
  1. RAG/向量检索?
  2. FAQ 文档系统?
  3. 扩展现有 `Resources\A3ToolsKnowledge.md`(csproj 里已有)?
  4. AI 工具调用对接外部知识库 API?