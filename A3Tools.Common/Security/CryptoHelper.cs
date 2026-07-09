using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace A3Tools.Common.Security
{
    /// <summary>
    /// 加密工具：AES-256-CBC + RSA + HMAC-SHA256
    /// 客户端和服务端共用同一套逻辑
    /// </summary>
    public static class CryptoHelper
    {
        // ==================== AES-256-CBC ====================

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

        public static byte[] RsaEncrypt(byte[] plainBytes, string publicKeyXml)
        {
            using (var rsa = RSA.Create(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(plainBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        public static byte[] RsaDecrypt(byte[] cipherBytes, string privateKeyXml)
        {
            using (var rsa = RSA.Create(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                return rsa.Decrypt(cipherBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        public static void GenerateRsaKeyPair(out string publicKeyXml, out string privateKeyXml)
        {
            using (var rsa = RSA.Create(2048))
            {
                publicKeyXml = rsa.ToXmlString(false);
                privateKeyXml = rsa.ToXmlString(true);
            }
        }

        // ==================== HMAC-SHA256 ====================

        public static string HmacSha256(string secretKey, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            return HmacSha256(keyBytes, dataBytes);
        }

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

        public static string GenerateToken(string secretKey, string timestamp)
        {
            return HmacSha256(secretKey, timestamp);
        }

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
