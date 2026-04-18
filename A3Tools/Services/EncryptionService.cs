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
}
