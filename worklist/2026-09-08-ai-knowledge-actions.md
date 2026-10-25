# 2026-09-08 AI 助理直接管理知识库（8 个新 Action）

## 状态
✅ 完成 - R1-R4 全部通过（含 14/14 烟测）

## 陛下要求（9:43）
> 现在 AI 助理对话框无法直接新增知识库内容。比如我发给他一些知识点让他加进去做不到。或者在对话中自己学习有用加入知识库还做不到

## 🎯 设计决策

| 决策点 | 选择 | 理由 |
|---|---|---|
| 实现方式 | **a**（8 个新 IAiAction）| 复用现有 Function Calling + ConfirmActionForm 链路，零架构改动 |
| 写入确认 | **b**（ConfirmActionForm 普通确认）| 写入类操作要陛下眼睛看一眼；自学习场景也是先问后存 |
| 删除确认 | **c**（高危 + 输入标题前 4 字）| 跟现有 delete_account 同款，陛下已经习惯 |
| 知识库定位 | **d**（按名称 + 重复名报错）| AI 看到中文库名比看 GUID 友好，重名直接报错防误操作 |
| SourceType | **e**（ChatExtract，2026-09-01 留好的枚举）| UI 那边已经写好「💬 对话」显示，无需改 |
| 重复检测 | **f**（同 base + 同标题拒绝）| 引导 AI 改用 update，避免堆垃圾条目 |

## 🏗 架构

```
D:\work\A3Tools\A3Tools\
├── Services\AiActions\
│   ├── AiActions.cs                （已有 — 账套/日志类 12 个 Action）
│   └── KnowledgeActions.cs         （新增 — 知识库 8 个 Action）
├── Models\
│   ├── KnowledgeBase.cs            （已有）
│   ├── KnowledgeEntry.cs           （已有）
│   └── KnowledgeSourceType.cs      （已有 — ChatExtract 枚举 2026-09-01 留好）
└── Forms\AiChatForm.cs             （BuildSystemPrompt 加工具说明 + 互动工作流）
```

scratch\kb-actions-smoke\           （新增 — 8 个 Action 端到端烟测，14 项全通过）

## 📦 8 个新 Action

### 🟢 只读（无确认框）
| Name | Description | 用途 |
|---|---|---|
| `list_knowledge_bases` | 列所有知识库元数据 | AI 探索库结构 |
| `search_knowledge` | 跨库 BM25 检索（top K + fallback）| AI 回答前查参考 |
| `get_knowledge_entry` | 按 ID 读单条完整内容 | AI 检索后看完整内容 |
| `recommend_knowledge_target` | 按预览内容推荐合适库 + 现有相似条目 | AI 添加前必调 |

### 🟡 写入（普通确认）
| Name | Description | 用途 |
|---|---|---|
| `create_knowledge_base` | 新建库 | AI 帮陛下分类组织 |
| `add_knowledge_entry` | **核心**：加条目，SourceType=ChatExtract | 用户说「记住这个」/AI 自学习 |
| `update_knowledge_entry` | 更新条目（不改 SourceType）| AI 修正/完善 |

### 🔴 高危
| Name | Description | 用途 |
|---|---|---|
| `delete_knowledge_entry` | 删除条目 | 输入标题前 4 字确认 |

## 🎯 关键设计

### 1. add_knowledge_entry 是核心入口
```csharp
// AI 调用前会弹 ConfirmActionForm 给陛下预览：
// "向知识库「A3 操作手册」添加条目：
//  标题：Tab1 账套启动失败排查
//  预览：检查 DATA 目录权限..."

// 写入成功后自动设：
//   entry.SourceType = KnowledgeSourceType.ChatExtract
//   entry.SourceReference = 对话来源（如时间/标题）
// UI 那边直接显示「💬 对话」图标

// 防重：同库同标题 → 报错，引导改用 update
```

### 2. recommend_knowledge_target 防重复
```csharp
// AI 想加新条目 → 先调这个工具看是否有相似条目
// 返回：按 BM25 评分排名的候选库 + 每库最相似的 3 个现有标题
// 避免 AI 把「账套启动」重复存到「账套启动」里
```

### 3. delete 走高危确认（复用 DeleteAccountAction 模式）
```csharp
public string GetImpactDescription(...) {
    var title = entry.Title;
    _confirmText = title.Length >= 4 ? title.Substring(0, 4) : title;
    return $"删除条目「{title}」\n陛下必须输入「{_confirmText}」才能放行";
}
```

### 4. search 0 命中走 fallback
```csharp
// BM25 找不到时返回最近更新的 3 条（带 SourceType），AI 知道「这里没相关条目但有这些可参考」
// 避免 AI 编造知识库内容
```

## 🧠 系统提示词更新（AiChatForm.BuildSystemPrompt）

新增 8 行工具说明 + 「知识库互动工作流」章节（场景 1~5 + 知识整理原则）：
- 场景 1：陛下主动要求记录（触发词：记住这个/加到知识库/保存/记一下/存一下/加入 KB）
- 场景 2：陛下说完事实明确说「这个也要记住」
- 场景 3：AI 自我学习（**重要：先问陛下再存，不偷偷存**）
- 场景 4：陛下问技术问题（先 search → 命中优先用本地）
- 场景 5：陛下要求修正/删除

## ✅ 编译 + 烟测

```
cd D:\work\A3Tools\A3Tools; dotnet build --nologo
→ 0 errors / 32 warnings（全部原有）

cd D:\work\A3Tools\scratch\kb-actions-smoke; dotnet run
→ 14/14 通过：
  ✅ list_knowledge_bases 调用成功
  ✅ create_knowledge_base 成功
  ✅ add_knowledge_entry 成功
  ✅ 重复标题被拒绝
  ✅ search_knowledge 成功并返回 hits
  ✅ search 0 命中走 fallback
  ✅ recommend_knowledge_target 推荐了测试库
  ✅ get_knowledge_entry 取到完整内容
  ✅ update_knowledge_entry 成功
  ✅ 更新后 content 变了
  ✅ 更新后 tags 变了
  ✅ 更新后 SourceType 仍是 ChatExtract  ← 关键验证
  ✅ delete_knowledge_entry 执行成功

测试数据已自动清理（__smoke_test__ 临时库 + 1 个临时条目）
```

## 🚀 陛下测试用例建议

1. **基础添加**：发"记住这个：账套启动失败要先检查 DATA 目录权限" → AI 应调 list → recommend → add → 弹确认框 → 陛下确认 → 入「A3Tools 使用手册」库
2. **查重防呆**：同一句话再发一次 → AI 应发现已有相似条目，问陛下「要不要更新」
3. **自我学习**：陛下分享一段踩坑经验 → AI 应主动问「这条有价值，要不要加到 XX 库？」
4. **检索引用**：陛下问"账套启动失败怎么办" → AI 应调 search_knowledge → 用本地条目回答
5. **删除保护**：要求删某条目 → 弹高危确认框，陛下必须输入标题前 4 字

## 📝 关键文件改动

| 文件 | 改动 |
|---|---|
| `Services\AiActions\KnowledgeActions.cs` | 新增（650 行，8 个 Action 类）|
| `Services\AiActions\ActionRegistry.cs` | +8 行注册 |
| `Forms\AiChatForm.cs` | BuildSystemPrompt +30 行（工具列表 + 互动工作流）|
| `A3Tools.csproj` | +1 行 csproj 注册 |
| `scratch\kb-actions-smoke\` | 新增烟测项目（保留作为回归测试）|

## 🔮 后续可考虑（待陛下拍板）

- **R5**: add_knowledge_entry 成功后自动刷新 KnowledgeBaseForm（如果开着）
- **R6**: 在 KnowledgeBaseForm 加「💬 对话」筛选默认打开
- **R7**: search_knowledge 支持按 SourceType=ChatExtract 过滤（方便回顾对话中学到的知识）
- **R8**: AI 加条目时自动根据内容提取 tags（用 NLP 而非正则，更智能）