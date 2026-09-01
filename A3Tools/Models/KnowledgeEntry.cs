namespace A3Tools.Models;

/// <summary>
/// 知识条目 - 知识库中的单个知识点
/// ★ 2026-09-01 陛下要求:知识库系统
/// </summary>
public class KnowledgeEntry
{
    /// <summary>条目唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>条目标题(如 "Tab1 账套启动")</summary>
    public string Title { get; set; } = "";

    /// <summary>条目内容(Markdown 格式)</summary>
    public string Content { get; set; } = "";

    /// <summary>关键词标签(用于检索,如 ["账套", "启动", "A3Client"])</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>源文件路径(扫描导入时填,手动添加为空)</summary>
    public string SourceFile { get; set; } = "";

    /// <summary>内容哈希(检测内容是否变更,决定是否需要重新提取)</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>创建时间(UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间(UTC)</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}