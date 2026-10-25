using System;
using System.Text;
using System.Security.Cryptography;

namespace A3Tools.Services;

/// <summary>
/// A3 客户端密文解析（DES / CBC / PKCS7）。
/// <para>
/// 算法与密钥取自 A3 客户端程序集 A3.Common.Util 中的 A3DESEncrypt：
/// 密钥 = Base64 解码实例字段 CKey（8 字节），IV = Base64 解码实例字段 CIV 后取前 8 字节。
/// </para>
/// <para>
/// 用途仅限于读取 A3 自身加密的数据（如 D:\A3\Account.info 账套连接串）。
/// 与 EncryptionService（工具箱自身的 AES 加密）互不相干，不要混用。
/// </para>
/// </summary>
public static class A3DesCrypto
{
    /// <summary>密钥（Base64 文本，来自 A3DESEncrypt.CKey）</summary>
    private const string CKeyBase64 = "jkHuIy9D/9i=";

    /// <summary>初始化向量（Base64 文本，来自 A3DESEncrypt.CIV，解码后取前 8 字节）</summary>
    private const string CivBase64 = "Mi9l/+7Zujhy12se6Yjy111A";

    private static readonly byte[] Key = Convert.FromBase64String(CKeyBase64);

    private static readonly byte[] Iv = Take(Convert.FromBase64String(CivBase64), 8);

    /// <summary>DES 块长度</summary>
    private const int BlockSize = 8;

    private static byte[] Take(byte[] source, int count)
    {
        if (source.Length <= count) return source;
        var result = new byte[count];
        Array.Copy(source, result, count);
        return result;
    }

    /// <summary>
    /// 尝试解密一段 A3 DES 密文。失败返回 false，不抛异常。
    /// </summary>
    public static bool TryDecrypt(string? cipherText, out string plainText)
    {
        plainText = string.Empty;
        if (string.IsNullOrWhiteSpace(cipherText)) return false;

        var trimmed = cipherText.Trim();
        if (!LooksEncrypted(trimmed)) return false;

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(trimmed);
        }
        catch (FormatException)
        {
            return false;
        }

        if (cipherBytes.Length == 0 || cipherBytes.Length % BlockSize != 0) return false;

        try
        {
            using var des = DES.Create();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.PKCS7;
            des.Key = Key;
            des.IV = Iv;

            using var decryptor = des.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            var text = Encoding.UTF8.GetString(plainBytes);

            // DES 解密成功但内容不是可打印文本，通常说明密钥/IV 不匹配或密文被截断
            if (!IsReadableText(text)) return false;

            plainText = text;
            return true;
        }
        catch (CryptographicException)
        {
            // 填充校验失败 = 密钥不对
            return false;
        }
    }

    /// <summary>
    /// 判断字符串是否具备 A3 DES 密文的外形：合法 Base64，且长度是 DES 块的整数倍且不为 0。
    /// </summary>
    public static bool LooksEncrypted(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        if (trimmed.Length < 12 || trimmed.Length % 4 != 0) return false;

        foreach (var c in trimmed)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                     || c == '+' || c == '/' || c == '=';
            if (!ok) return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(trimmed);
            return bytes.Length > 0 && bytes.Length % BlockSize == 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 明文合理性判断：可打印字符占比足够高。
    /// </summary>
    private static bool IsReadableText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var printable = 0;
        foreach (var c in text)
        {
            if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t') printable++;
        }
        return printable >= text.Length * 0.9;
    }
}