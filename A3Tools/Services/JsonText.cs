using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace A3Tools.Services;

/// <summary>
/// ★ 2026-10-25 新增：JSON 文本展示辅助
/// <para>背景：陛下反馈「AI 助理工具调用里显示的中文变成 \uXXXX 转义，看不了」，
/// 贴出来的实际内容是 "A3 \u5546\u57CE\u8868\u7ED3\u6784"，本该是「A3 商城表结构」。</para>
/// <para><b>病根</b>：<see cref="JsonSerializer"/> 默认编码器 <c>JavaScriptEncoder.Default</c>
/// 会把所有非 ASCII 字符转义成 \uXXXX。
/// 厂商返回的 tool_call arguments 本身就是转义串，而 C# 侧历史卡片在
/// <c>BuildToolArgsHtml</c> 里把 JsonElement 重新序列化时又用默认编码器 → 二次转义 → 界面显示成乱码。</para>
/// <para><b>修法</b>：展示用 <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>，
/// 不转义非 ASCII 与 &lt; &gt; &amp;。展示前还有 <c>WebUtility.HtmlEncode</c> 兜底，
/// 所以 XSS 风险与改动前完全一致（不增不减）。</para>
/// </summary>
public static class JsonText
{
    /// <summary>
    /// 展示用序列化选项：保留中文 + 2 空格缩进
    /// <para>与 JS 端 <c>JSON.stringify(obj, null, 2)</c> 输出格式一致 → 实时卡片 / 历史卡片视觉统一</para>
    /// </summary>
    public static readonly JsonSerializerOptions DisplayOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 紧凑版展示选项（单行）：用于存盘 / 日志，保持体积小同时中文可读
    /// </summary>
    public static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 把一段 JSON 规范化成「人可直接读的中文 JSON」
    /// <para>解析成功 → 用展示选项重新序列化（中文原样保留）</para>
    /// <para>解析失败 → 兜底做 \uXXXX 还原（尽力而为，原始串已非法，不保证完美）</para>
    /// </summary>
    public static string Relax(string? json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return json ?? string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, options ?? DisplayOptions);
        }
        catch
        {
            return DecodeUnicodeEscapes(json);
        }
    }

    /// <summary>
    /// 兜底：把文本里的 JSON <c>\uXXXX</c> 转义还原成真实字符
    /// <para>★ 只在「整段不是合法 JSON」时才会走到这里，所以误伤概率极低
    /// （唯一理论风险：非法 JSON 里出现形如 <c>C:\deadbeef</c> 的路径会被当成转义）</para>
    /// <para>支持代理对：<c>\uD83D\uDE00</c> 会自然拼成一对，正确还原为 emoji</para>
    /// </summary>
    public static string DecodeUnicodeEscapes(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (text.IndexOf("\\u", StringComparison.Ordinal) < 0) return text;

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            // \uXXXX：反斜杠 + u + 4 位十六进制
            if (text[i] == '\\' && i + 5 < text.Length && (text[i + 1] == 'u' || text[i + 1] == 'U')
                && IsHex(text[i + 2]) && IsHex(text[i + 3]) && IsHex(text[i + 4]) && IsHex(text[i + 5]))
            {
                int code = (HexVal(text[i + 2]) << 12) | (HexVal(text[i + 3]) << 8)
                         | (HexVal(text[i + 4]) << 4) | HexVal(text[i + 5]);
                sb.Append((char)code);
                i += 5;
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return c - 'A' + 10;
    }
}
