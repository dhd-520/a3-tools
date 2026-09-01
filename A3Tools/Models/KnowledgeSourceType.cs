namespace A3Tools.Models;

/// <summary>
/// 知识条目来源类型
/// ★ 2026-09-01 陛下要求:知识库可见出处,支持按来源筛选/批量删除
/// </summary>
public enum KnowledgeSourceType
{
    /// <summary>手动添加(陛下在 UI 里直接编辑)</summary>
    Manual = 0,

    /// <summary>文件夹扫描导入(.md/.txt/.docx 原文件)</summary>
    FileImport = 1,

    /// <summary>AI 提取(从文件夹提炼的 md)</summary>
    AiExtract = 2,

    /// <summary>从对话中提取(后续 R4 实现)</summary>
    ChatExtract = 3,
}