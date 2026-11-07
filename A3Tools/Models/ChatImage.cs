using System.Text.Json.Serialization;

namespace A3Tools.Models;

/// <summary>
/// 聊天消息附带的图片
/// <para>★ 2026-11-07 陛下要求：AI 助理增加图片消息支持</para>
/// <para>设计要点：</para>
/// <list type="bullet">
///   <item>图片本体落盘到 <c>DATA/ai_chat/images/{sessionId}/</c>，消息里只留元数据</item>
///   <item>原因：base64 塞进 messages JSON 会让历史文件膨胀几十倍（一张截图 ≈ 200KB → base64 ≈ 270KB）</item>
///   <item>base64 data-URL 只在「构造请求那一刻」由 <c>AiImageStore</c> 现场生成，用完即弃</item>
/// </list>
/// </summary>
public class ChatImage
{
    /// <summary>图片唯一 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>原始文件名（仅界面展示用，例如「截图 2026-11-07.png」）</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>实际存储的 MIME 类型（image/jpeg / image/png）</summary>
    public string MimeType { get; set; } = "image/jpeg";

    /// <summary>压缩后的字节数</summary>
    public long SizeBytes { get; set; }

    /// <summary>压缩后的像素宽</summary>
    public int Width { get; set; }

    /// <summary>压缩后的像素高</summary>
    public int Height { get; set; }

    /// <summary>
    /// 存储路径（相对 <c>DATA/ai_chat/images/</c>，形如 <c>{sessionId}/{id}.jpg</c>）
    /// <para>★ 只存相对路径：换机器 / 换部署目录不会失效</para>
    /// <para>★ Memory / Disabled 存储策略下图片不落盘，此字段为空，字节留在 <see cref="Data"/> 里</para>
    /// </summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>
    /// 内存态图片字节（仅 Memory / Disabled 存储策略使用）
    /// <para>★ <see cref="JsonIgnoreAttribute"/>：绝不进 JSON —— 否则历史文件会暴涨几十倍</para>
    /// <para>Local 策略下图片已落盘，此字段保持 null</para>
    /// </summary>
    [JsonIgnore]
    public byte[]? Data { get; set; }
}
