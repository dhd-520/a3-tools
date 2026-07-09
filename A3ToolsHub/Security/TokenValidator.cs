using System;
using A3ToolsHub.Security;

namespace A3ToolsHub.Security
{
    /// <summary>
    /// Token + 时间戳校验
    /// 4 小时窗口：时间戳超过 4 小时直接拒绝（防重放攻击）
    /// </summary>
    public static class TokenValidator
    {
        /// <summary>
        /// 时间戳有效期（小时）
        /// </summary>
        public const int TokenValidityHours = 4;

        /// <summary>
        /// 校验 Token + 时间戳
        /// </summary>
        /// <param name="timestamp">ISO 8601 时间戳（UTC）</param>
        /// <param name="token">HMAC-SHA256(secretKey, timestamp)</param>
        /// <param name="secretKey">共享密钥</param>
        /// <returns>null=校验通过，string=错误消息</returns>
        public static string Validate(string timestamp, string token, string secretKey)
        {
            if (string.IsNullOrEmpty(timestamp))
                return "Missing timestamp";
            if (string.IsNullOrEmpty(token))
                return "Missing token";

            // 解析时间戳
            DateTime ts;
            if (!DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out ts))
            {
                return "Invalid timestamp format (expect ISO 8601)";
            }

            // 转 UTC 比较
            DateTime tsUtc = ts.Kind == DateTimeKind.Utc ? ts : ts.ToUniversalTime();
            DateTime nowUtc = DateTime.UtcNow;

            // 4 小时窗口
            var diff = nowUtc - tsUtc;
            if (Math.Abs(diff.TotalHours) > TokenValidityHours)
            {
                return string.Format("Token expired (timestamp {0} vs now {1}, max {2}h)", 
                    tsUtc.ToString("o"), nowUtc.ToString("o"), TokenValidityHours);
            }

            // HMAC 校验
            var expectedToken = CryptoHelper.GenerateToken(secretKey, timestamp);
            if (!CryptoHelper.ConstantTimeEquals(token, expectedToken))
            {
                return "Invalid token (HMAC mismatch)";
            }

            return null; // 通过
        }
    }
}
