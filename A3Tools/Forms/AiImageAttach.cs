using System.Drawing;
using System.Drawing.Imaging;
using A3Tools.Services;

namespace A3Tools.Forms;

/// <summary>
/// ★ 2026-11-07 陛下要求：AI 助理增加图片消息支持
/// <para>待发送图片（在用户点发送之前暂存在内存里，还没落盘）</para>
/// </summary>
public sealed class PendingImage
{
    /// <summary>压缩后的 JPEG 字节（发送时落盘）</summary>
    public byte[] JpegBytes { get; init; } = Array.Empty<byte>();

    /// <summary>原始文件名（仅展示用）</summary>
    public string FileName { get; init; } = "图片.jpg";

    /// <summary>压缩后的宽</summary>
    public int Width { get; init; }

    /// <summary>压缩后的高</summary>
    public int Height { get; init; }
}

/// <summary>
/// 图片附加辅助（★ 2026-11-07 图片消息支持）
/// <para>职责：把各种来源的图片（文件 / 剪贴板 / 拖拽）统一压成 JPEG 字节，
/// 并生成预览缩略图。</para>
/// </summary>
public static class AiImageAttach
{
    /// <summary>预览缩略图最长边</summary>
    public const int ThumbMax = 160;

    /// <summary>支持的图片扩展名</summary>
    private static readonly string[] SupportedExt =
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff"
    };

    /// <summary>判断是不是支持的图片文件</summary>
    public static bool IsImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(SupportedExt, ext) >= 0;
    }

    /// <summary>从文件读图并压缩</summary>
    public static PendingImage? FromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            // ★ 用 FileStream + ReadWrite 共享，避免 Image.FromFile 长期锁住文件
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                                                   System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            using var img = Image.FromStream(fs);
            return FromImage(img, System.IO.Path.GetFileName(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从剪贴板取图并压缩</summary>
    public static PendingImage? FromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return null;
            using var img = Clipboard.GetImage();
            if (img == null) return null;
            return FromImage(img, "剪贴板图片.png");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>压缩任意 Image（调用方负责释放源图）</summary>
    public static PendingImage? FromImage(Image img, string fileName)
    {
        if (!AiImageService.TryCompress(img, out var jpeg, out int w, out int h)) return null;
        return new PendingImage
        {
            JpegBytes = jpeg,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "图片.jpg" : fileName,
            Width = w,
            Height = h
        };
    }

    /// <summary>生成预览缩略图（返回的 Bitmap 由调用方释放）</summary>
    public static Bitmap? MakeThumb(byte[] jpegBytes)
    {
        if (jpegBytes == null || jpegBytes.Length == 0) return null;
        try
        {
            using var ms = new System.IO.MemoryStream(jpegBytes);
            using var src = Image.FromStream(ms);

            double scale = 1.0;
            int maxSide = Math.Max(src.Width, src.Height);
            if (maxSide > ThumbMax) scale = (double)ThumbMax / maxSide;

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
}
