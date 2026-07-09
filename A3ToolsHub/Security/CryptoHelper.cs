using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace A3ToolsHub.Security
{
    /// <summary>
    /// 加密工具：AES-256-CBC + RSA + HMAC-SHA256
    /// 客户端和服务端共用同一套逻辑（.NET Framework 4.5+ / .NET 7 兼容）
    /// </summary>
    public static class CryptoHelper
    {
        // ==================== AES-256-CBC ====================

        /// <summary>
        /// AES-256-CBC 加密
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="key">32 字节密钥</param>
        /// <param name="iv">16 字节 IV（每次随机生成）</param>
        /// <returns>密文（IV + cipher 拼接，base64）</returns>
        public static string AesEncrypt(string plainText, byte[] key, out byte[] iv)
        {
            if (key.Length != 32) throw new ArgumentException("AES key must be 32 bytes (256-bit)");

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.GenerateIV();
                iv = aes.IV;

                using (var ms = new MemoryStream())
                {
                    // 先写 IV（16 字节），再写密文
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        var plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// AES-256-CBC 解密（密文格式：IV + cipher，base64）
        /// </summary>
        public static string AesDecrypt(string cipherBase64, byte[] key)
        {
            if (key.Length != 32) throw new ArgumentException("AES key must be 32 bytes (256-bit)");

            var allBytes = Convert.FromBase64String(cipherBase64);
            if (allBytes.Length < 16) throw new ArgumentException("Cipher too short (missing IV)");

            var iv = new byte[16];
            var cipher = new byte[allBytes.Length - 16];
            Buffer.BlockCopy(allBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(allBytes, 16, cipher, 0, cipher.Length);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// 生成随机 32 字节 AES key
        /// </summary>
        public static byte[] GenerateAesKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var key = new byte[32];
                rng.GetBytes(key);
                return key;
            }
        }

        // ==================== RSA ====================

        /// <summary>
        /// RSA 加密（用于加密 AES session key）
        /// </summary>
        /// <param name="plainBytes">明文字节（最长 245 字节 for RSA-2048-PKCS1）</param>
        /// <param name="publicKeyXml">服务端 RSA 公钥（XML 格式）</param>
        /// <returns>加密后的字节</returns>
        public static byte[] RsaEncrypt(byte[] plainBytes, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(plainBytes, false);
            }
        }

        /// <summary>
        /// RSA 解密
        /// </summary>
        /// <param name="cipherBytes">密文字节</param>
        /// <param name="privateKeyXml">服务端 RSA 私钥（XML 格式）</param>
        /// <returns>明文字节</returns>
        public static byte[] RsaDecrypt(byte[] cipherBytes, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                return rsa.Decrypt(cipherBytes, false);
            }
        }

        /// <summary>
        /// 生成 RSA-2048 密钥对（XML 格式）
        /// </summary>
        public static void GenerateRsaKeyPair(out string publicKeyXml, out string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                publicKeyXml = rsa.ToXmlString(false);
                privateKeyXml = rsa.ToXmlString(true);
            }
        }

        // ==================== HMAC-SHA256 ====================

        /// <summary>
        /// 生成 HMAC-SHA256 签名
        /// </summary>
        /// <param name="secretKey">共享密钥</param>
        /// <param name="data">待签名数据</param>
        /// <returns>签名（hex 小写）</returns>
        public static string HmacSha256(string secretKey, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            return HmacSha256(keyBytes, dataBytes);
        }

        /// <summary>
        /// 生成 HMAC-SHA256 签名
        /// </summary>
        public static string HmacSha256(byte[] keyBytes, byte[] dataBytes)
        {
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hash = hmac.ComputeHash(dataBytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 生成时间安全的 Token：HMAC-SHA256(secretKey, timestamp)
        /// </summary>
        public static string GenerateToken(string secretKey, string timestamp)
        {
            return HmacSha256(secretKey, timestamp);
        }

        /// <summary>
        /// 常量时间比较字符串（防止时序攻击）
        /// </summary>
        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var bytesA = Encoding.UTF8.GetBytes(a);
            var bytesB = Encoding.UTF8.GetBytes(b);
            var diff = 0;
            for (int i = 0; i < bytesA.Length; i++)
            {
                diff |= bytesA[i] ^ bytesB[i];
            }
            return diff == 0;
        }
    }
}
