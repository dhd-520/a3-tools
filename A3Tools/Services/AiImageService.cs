using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 图片消息服务（★ 2026-11-07 陛下要求：AI 助理增加图片消息支持）
/// <para>职责三件事：</para>
/// <list type="number">
///   <item><b>压缩</b>：把任意来源图片（截图/照片/文件）压成 ≤ MaxBytes 的 JPEG，控制 token 成本</item>
///   <item><b>落盘</b>：写进 <c>DATA/ai_chat/images/{sessionId}/</c>（AES 加密，与聊天记录同策略）</item>
///   <item><b>还原</b>：请求时现场生成 base64 data-URL 交给模型；界面渲染时生成缩略图</item>
/// </list>
/// <para>★ 为什么图片存文件而不是存进 messages JSON：</para>
/// <para>一张 1920×1080 截图 JPEG 约 200KB，base64 后膨胀 33% ≈ 270KB。塞进 JSON 会让
/// 历史文件几十倍膨胀，而 <c>LocalChatStore</c> 每次切会话都要整份反序列化。</para>
/// </summary>
public static class AiImageService
{
    /// <summary>压缩后最长边上限（像素）。vision 模型对超长边会内部切片，token 涨得不划算</summary>
    public const int MaxDimension = 1280;

    /// <summary>单张图片压缩后目标字节上限</summary>
    public const int MaxBytes = 800 * 1024;

    /// <summary>图片目录（相对程序根目录）</summary>
    public const string ImagesFolder = "DATA/ai_chat/images";

    /// <summary>单个会话最多保留的图片数（超出时提示陛下删旧图，避免磁盘无限膨胀）</summary>
    public const int MaxImagesPerSession = 200;

    /// <summary>
    /// 压缩图片：等比缩放到 <see cref="MaxDimension"/> 内，再按质量回退直到 ≤ <see cref="MaxBytes"/>
    /// </summary>
    /// <param name="source">源图（调用方负责释放）</param>
    /// <param name="jpegBytes">输出的 JPEG 字节</param>
    /// <param name="width">输出宽</param>
    /// <param name="height">输出高</param>
    /// <returns>成功返回 true</returns>
    public static bool TryCompress(Image source, out byte[] jpegBytes, out int width, out int height)
    {
        jpegBytes = Array.Empty<byte>();
        width = 0;
        height = 0;
        if (source == null) return false;

        try
        {
            // 1) 等比缩放
            int srcW = source.Width, srcH = source.Height;
            if (srcW <= 0 || srcH <= 0) return false;

            double scale = 1.0;
            int maxSide = Math.Max(srcW, srcH);
            if (maxSide > MaxDimension)
                scale = (double)MaxDimension / maxSide;

            int dstW = Math.Max(1, (int)Math.Round(srcW * scale));
            int dstH = Math.Max(1, (int)Math.Round(srcH * scale));

            using var bmp = new Bitmap(dstW, dstH, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);           // PNG 透明底转白，避免转 JPEG 后变黑
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, dstW, dstH));
            }

            width = dstW;
            height = dstH;

            // 2) 质量回退：85 → 70 → 55，直到进字节预算
            byte[] encoded = Array.Empty<byte>();
            foreach (int quality in new[] { 85, 70, 55 })
            {
                encoded = EncodeJpeg(bmp, quality);
                if (encoded.Length <= MaxBytes) break;
            }

            // 3) 极端情况（内容极复杂）：再缩一半重压一次
            if (encoded.Length > MaxBytes)
            {
                int halfW = Math.Max(1, dstW / 2);
                int halfH = Math.Max(1, dstH / 2);
                using var small = new Bitmap(halfW, halfH, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, new Rectangle(0, 0, halfW, halfH));
                }
                encoded = EncodeJpeg(small, 60);
                width = halfW;
                height = halfH;
            }

            jpegBytes = encoded;
            return jpegBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>JPEG 编码（指定质量）</summary>
    private static byte[] EncodeJpeg(Bitmap bmp, int quality)
    {
        using var ms = new MemoryStream();
        var codec = GetJpegCodec();
        if (codec == null)
        {
            bmp.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
        using (var encParams = new EncoderParameters(1))
        {
            encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            bmp.Save(ms, codec, encParams);
        }
        return ms.ToArray();
    }

    private static ImageCodecInfo? GetJpegCodec()
    {
        foreach (var c in ImageCodecInfo.GetImageEncoders())
            if (c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid)
                return c;
        return null;
    }

    /// <summary>
    /// 保存图片到磁盘（加密），返回元数据
    /// </summary>
    /// <param name="jpegBytes">已压缩的 JPEG 字节</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="originalFileName">原始文件名（仅展示）</param>
    /// <param name="width">宽</param>
    /// <param name="height">高</param>
    public static ChatImage Save(byte[] jpegBytes, string sessionId, string originalFileName, int width, int height)
    {
        string id = Guid.NewGuid().ToString("N");
        string relPath = $"{sessionId}/{id}.dat";     // 相对 images 根目录

        string absDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                     ImagesFolder.Replace('/', Path.DirectorySeparatorChar),
                                     sessionId);
        Directory.CreateDirectory(absDir);

        // ★ 与聊天记录同策略：AES 加密后落盘（base64 文本 → 加密 → base64 文本）
        //   ★ 扩展名用 .dat 而不是 .jpg：存的是密文，不是真正的 JPEG
        string plain = Convert.ToBase64String(jpegBytes);
        string cipher = EncryptionService.Encrypt(plain);
        File.WriteAllText(Path.Combine(absDir, id + ".dat"), cipher);

        return new ChatImage
        {
            Id = id,
            FileName = string.IsNullOrWhiteSpace(originalFileName) ? "图片.jpg" : originalFileName,
            MimeType = "image/jpeg",
            SizeBytes = jpegBytes.Length,
            Width = width,
            Height = height,
            StoredPath = relPath
        };
    }

    /// <summary>
    /// 只存内存（不落盘）—— 用于 Memory / Disabled 存储策略
    /// <para>★ 这两种策略下聊天记录本来就不落盘，图片跟着不落盘才一致</para>
    /// </summary>
    public static ChatImage SaveToMemory(byte[] jpegBytes, string originalFileName, int width, int height)
    {
        return new ChatImage
        {
            Id = Guid.NewGuid().ToString("N"),
            FileName = string.IsNullOrWhiteSpace(originalFileName) ? "图片.jpg" : originalFileName,
            MimeType = "image/jpeg",
            SizeBytes = jpegBytes.Length,
            Width = width,
            Height = height,
            StoredPath = string.Empty,
            Data = jpegBytes
        };
    }

    /// <summary>读回图片字节（解密）；内存态图片直接返回</summary>
    public static byte[]? LoadBytes(ChatImage image)
    {
        if (image == null) return null;
        // ★ Memory / Disabled 策略：图片没落盘，字节就在内存里
        if (image.Data is { Length: > 0 }) return image.Data;
        if (string.IsNullOrEmpty(image.StoredPath)) return null;
        try
        {
            string absPath = ResolveAbsPath(image.StoredPath);
            if (!File.Exists(absPath)) return null;
            string cipher = File.ReadAllText(absPath);
            string plain = EncryptionService.Decrypt(cipher);
            if (string.IsNullOrEmpty(plain)) return null;
            return Convert.FromBase64String(plain);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 生成 base64 data-URL（发给模型的格式）
    /// <para>★ 用完即弃：不在内存里长期缓存，避免多图会话吃掉几百 MB</para>
    /// </summary>
    public static string? BuildDataUrl(ChatImage image)
    {
        var bytes = LoadBytes(image);
        if (bytes == null || bytes.Length == 0) return null;
        string mime = string.IsNullOrEmpty(image.MimeType) ? "image/jpeg" : image.MimeType;
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>读出图片用于界面渲染（调用方负责释放返回的 Image）</summary>
    public static Image? LoadImage(ChatImage image)
    {
        var bytes = LoadBytes(image);
        if (bytes == null) return null;
        try
        {
            // ★ Image 持有流的引用 → 流不能在 Image 用完前释放，所以这里不 using
            //   调用方拿到 Image 后负责 Dispose
            return Image.FromStream(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读出图片并缩放到指定方框内（等比缩放，用于界面缩略图）
    /// <para>返回的 Bitmap 由调用方 Dispose</para>
    /// </summary>
    public static Bitmap? LoadThumb(ChatImage image, int maxBox)
    {
        var bytes = LoadBytes(image);
        if (bytes == null || maxBox <= 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            using var src = Image.FromStream(ms);

            double scale = Math.Min(1.0, (double)maxBox / Math.Max(src.Width, src.Height));
            int tw = Math.Max(1, (int)Math.Round(src.Width * scale));
            int th = Math.Max(1, (int)Math.Round(src.Height * scale));

            var bmp = new Bitmap(tw, th, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(0, 0, tw, th));
            }
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>删除一张图片的磁盘文件</summary>
    public static void Delete(ChatImage image)
    {
        if (image == null || string.IsNullOrEmpty(image.StoredPath)) return;
        try
        {
            string absPath = ResolveAbsPath(image.StoredPath);
            if (File.Exists(absPath)) File.Delete(absPath);
        }
        catch { }
    }

    /// <summary>删除整个会话的图片目录</summary>
    public static void DeleteSessionImages(string sessionId)
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                      ImagesFolder.Replace('/', Path.DirectorySeparatorChar),
                                      sessionId);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch { }
    }

    /// <summary>统计某会话已存图片数</summary>
    public static int CountSessionImages(string sessionId)
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                      ImagesFolder.Replace('/', Path.DirectorySeparatorChar),
                                      sessionId);
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir, "*.dat").Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 把相对路径解析成绝对路径（带越界防护）
    /// </summary>
    private static string ResolveAbsPath(string storedPath)
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                    ImagesFolder.Replace('/', Path.DirectorySeparatorChar)));
        string full = Path.GetFullPath(Path.Combine(root, storedPath));
        // ★ 防止 StoredPath 被篡改成 ../.. 越界读取
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("图片路径越界");
        return full;
    }
}
