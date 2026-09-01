# 2026-09-01 知识库系统 - 数据模型 + 存储 + 服务 + UI(基础版)

## 状态
🟡 进行中 - R1 模型+存储已完成,R2 UI 进行中

## 陛下要求(10:02 + 10:05)
1. **文件夹扫描提取**: 设定文件夹 → 扫描 `.md/.txt/.docx` → 入库
2. **知识库管理页面**: 手动添加 + 导入导出
3. **对话中自动检索**: 按用户消息检索相关条目注入 System Prompt
4. **新增功能(10:05)**: AI 提取按钮 → 调用 AI 把文档提炼成 md 知识库

## 🎯 设计决策

| 决策点 | 选择 | 理由 |
|---|---|---|
| 文件格式 | **b**(md + txt + docx)| 陛下明确说可能有 docx |
| 知识粒度 | **a**(每个文件=1 条目)| 简单;AI 提取的 md 内部自带 H2/H3 结构,检索按段落匹配 |
| 存储 | JSON 平铺 | 陛下已有 DATA/accounts.json 经验 |
| 检索算法 | BM25 简化版(后续轮) | 无依赖,纯 C#,效果 80% 够用 |
| AI 提取 | OpenAiCompatibleBackend(已有) | 复用,不引新依赖 |

## 🏗 架构

```
DATA/knowledge/                          # 知识库根目录
  ├─ bases.json                          # 所有知识库元数据列表
  └─ {base-id}/                          # 单个知识库目录
      ├─ meta.json                       # 知识库元数据
      └─ entries.json                    # 该库条目列表

Models/
  ├─ KnowledgeBase.cs                    # 知识库实体
  └─ KnowledgeEntry.cs                   # 知识条目实体

Services/
  └─ KnowledgeBaseManager.cs             # CRUD + ScanFolder + Search(后续)+ AI 提取(后续)

Forms/
  └─ KnowledgeBaseForm.cs                # 管理 UI(列表+详情+工具栏)

References/                               # 后续添加 DocumentFormat.OpenXml 用于 .docx 解析
```

## 📦 数据模型

```csharp
public class KnowledgeBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string WatchFolder { get; set; } = "";   // 扫描文件夹路径
    public List<string> FilePatterns { get; set; } = new() { "*.md", "*.txt", "*.docx" };
    public bool Recursive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<KnowledgeEntry> Entries { get; set; } = new();
}

public class KnowledgeEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string SourceFile { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

## 🛠 分轮进度

- **R1** [本次]: 数据模型 + JSON 存储 + 服务层(CRUD + ScanFolder)
- **R2** [本次]: KnowledgeBaseForm UI(完整 CRUD + 扫描按钮 + 手动新建)
- **R3** [下次]: AI 提取按钮 + 导入导出
- **R4** [下次]: 对话集成(BM25 检索 + 注入)

## ⚠️ 待陛下确认
- 设计决策已锁定:b + a + AI 提取
- 暂不做的:向量检索、版本对比、自动 watch(后续轮)

## 🔧 实施步骤

### R1 - 数据层(本轮)
1. 新建 `Models/KnowledgeBase.cs`
2. 新建 `Models/KnowledgeEntry.cs`
3. 新建 `Services/KnowledgeBaseManager.cs`(CRUD + ScanFolder)
4. csproj 注册新文件

### R2 - UI(本轮)
5. 新建 `Forms/KnowledgeBaseForm.cs` + `.Designer.cs`
6. 主界面加按钮(选项卡 or 工具栏)打开知识库管理
7. 实现:知识库列表 / 条目列表 / 条目编辑 / 工具栏(新建/删除/扫描文件夹)
8. Build + 手动测试

### R3 - AI 提取 + 导入导出(下次)
9. csproj 加 `DocumentFormat.OpenXml` 包
10. KnowledgeBaseManager 加 `ReadDocx()` 方法
11. KnowledgeBaseForm 加 "AI 提取" 按钮 → 调 AI + 保存 md
12. 加导出 zip / 导入 zip 功能

### R4 - 对话集成(下次)
13. KnowledgeBaseManager 加 `BM25Search(query, topK)`
14. AiChatForm L1248 集成检索 + 注入
15. 手动测试(问 AI 一个问题看是否注入相关知识)