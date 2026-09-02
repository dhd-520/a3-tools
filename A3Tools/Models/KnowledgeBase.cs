namespace A3Tools.Models;

/// <summary>
/// 知识库 - 一组相关知识条目的容器
/// ★ 2026-09-01 陛下要求:知识库系统(文件夹扫描 + 管理页面 + 对话检索)
/// </summary>
public class KnowledgeBase
{
    /// <summary>知识库唯一 ID(目录名)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>知识库名称(如 "A3Tools 使用手册")</summary>
    public string Name { get; set; } = "";

    /// <summary>描述(可选,用于显示)</summary>
    public string Description { get; set; } = "";

    /// <summary>扫描文件夹路径(可选,设为空则只能手动添加)</summary>
    public string WatchFolder { get; set; } = "";

    /// <summary>扫描文件模式(默认 .md/.txt/.docx/.pdf/.xls/.xlsx)</summary>
    public List<string> FilePatterns { get; set; } = new()
    {
        "*.md", "*.txt", "*.docx", "*.pdf", "*.xls", "*.xlsx"
    };

    /// <summary>是否递归扫描子文件夹</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>创建时间(UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间(UTC)</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>该知识库的所有条目</summary>
    public List<KnowledgeEntry> Entries { get; set; } = new();
}