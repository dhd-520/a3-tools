using System.Security.Cryptography;
using System.Text;

namespace A3Tools.Services;

/// <summary>
/// AES加密服务（基于机器相关密钥）
/// </summary>
public static class EncryptionService
{
    private static readonly byte[] _key;
    private static readonly byte[] _iv;

    static EncryptionService()
    {
        // 使用机器名+用户名生成密钥（跨进程但同机器安全）
        string keySource = $"{Environment.MachineName}-{Environment.UserName}-A3Tools-2026";
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(Encoding.UTF8.GetBytes(keySource));
        _iv = new byte[16];
        Array.Copy(_key, _iv, 16);
    }

    /// <summary>
    /// 加密字符串
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 解密字符串
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 判断字符串是否已是本程序加密过的 AES 密文。
    /// 判定逻辑：AES-CBC + PKCS7 密文必须是 16 字节倍数且能用当前机器密钥成功解密。
    /// </summary>
    public static bool IsEncrypted(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            byte[] cipherBytes = Convert.FromBase64String(text);
            if (cipherBytes.Length < 16 || cipherBytes.Length % 16 != 0) return false;
            var plain = Decrypt(text);
            return !string.IsNullOrEmpty(plain);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 仅当确认为加密密文时才解密；明文则原样返回。
    /// 用于账号账套密码在 "密文状态" 跟 "明文状态" 之间透明传递的场景。
    /// </summary>
    public static string TryDecrypt(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return IsEncrypted(text) ? Decrypt(text) : text;
    }
}
