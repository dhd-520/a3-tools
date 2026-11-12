using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A3Tools.Common.DataAccess
{
    /// <summary>
    /// ★ 2026-11-12：不发 SNI 的 HTTPS 通道（降级通道）。
    ///
    /// 背景：部分网络链路（电信出口的 TLS DPI）会在 ClientHello 里读到
    ///   SNI=某域名 时直接 RST 掉连接。表现是 .NET 抛
    ///   HttpRequestException("The SSL connection could not be established")
    ///   → SocketException(10054 远程主机强迫关闭了一个现有的连接)。
    ///   同一台机器上 Chromium / Node（ClientHello 较大，SNI 落在第二个 TCP 段）
    ///   却能连通，只有 OpenSSL/SChannel 这类小 ClientHello 会被打掉。
    ///
    /// 原理：TLS 握手时把 TargetHost 写成 IP 地址 → .NET 不发 SNI 扩展 →
    ///   链路层的域名匹配失效 → 握手正常；随后用 HTTP 的 Host 头带真实域名，
    ///   服务端仍按 Host 头路由到 A3ToolsHub。
    ///   证书照常按域名校验（证书 SAN 里含该域名），安全性不降级。
    ///
    /// 仅在常规通道握手失败时作为降级使用，不作为默认路径。
    /// </summary>
    internal static class RawHttpTransport
    {
        /// <summary>
        /// 以「连 IP + Host 头」方式 POST JSON，返回 (状态码, 响应正文)。
        /// </summary>
        public static async Task<(int StatusCode, string Body)> PostJsonAsync(
            string requestUrl, string json, int timeoutSeconds, CancellationToken ct)
        {
            var uri = new Uri(requestUrl);
            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"RawHttpTransport 只支持 https，当前为 {uri.Scheme}");

            string host = uri.Host;
            int port = uri.IsDefaultPort ? 443 : uri.Port;
            string path = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(uri.Query)) path += uri.Query;

            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ip == null)
                throw new InvalidOperationException($"域名 {host} 未解析到 IPv4 地址");

            using var tcp = new TcpClient { NoDelay = true };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await tcp.ConnectAsync(ip, port, timeoutCts.Token).ConfigureAwait(false);

            using var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
                (_, cert, chain, errors) => ValidateCertificate(cert, chain, host));

            // ★ 关键：TargetHost 传 IP → .NET 不发送 SNI 扩展 → 绕开按 SNI 的链路干扰
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = ip.ToString(),
                EnabledSslProtocols = SslProtocols.None,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, timeoutCts.Token).ConfigureAwait(false);

            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
            var head = new StringBuilder();
            head.Append($"POST {path} HTTP/1.1\r\n");
            head.Append($"Host: {host}\r\n");
            head.Append("Content-Type: application/json; charset=utf-8\r\n");
            head.Append($"Content-Length: {bodyBytes.Length}\r\n");
            head.Append("Accept: application/json\r\n");
            head.Append("User-Agent: A3Tools\r\n");
            head.Append("Connection: close\r\n");
            head.Append("\r\n");

            byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
            await ssl.WriteAsync(headBytes.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
            await ssl.WriteAsync(bodyBytes.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
            await ssl.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

            byte[] raw = await ReadAllAsync(ssl, timeoutCts.Token).ConfigureAwait(false);
            return ParseResponse(raw);
        }

        private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (n <= 0) break;
                ms.Write(buffer, 0, n);
            }
            return ms.ToArray();
        }

        private static (int StatusCode, string Body) ParseResponse(byte[] raw)
        {
            string text = Encoding.UTF8.GetString(raw);
            int sep = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (sep < 0) throw new InvalidOperationException("HTTP 响应格式异常（缺少头分隔符）");

            string head = text.Substring(0, sep);
            string body = text.Substring(sep + 4);

            string[] lines = head.Split("\r\n");
            string[] statusParts = lines[0].Split(' ');
            int status = statusParts.Length >= 2 && int.TryParse(statusParts[1], out int sc) ? sc : 0;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            if (headers.TryGetValue("Transfer-Encoding", out var te) && te.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                body = DecodeChunked(body);

            return (status, body);
        }

        private static string DecodeChunked(string body)
        {
            var sb = new StringBuilder();
            int pos = 0;
            while (true)
            {
                int lineEnd = body.IndexOf("\r\n", pos, StringComparison.Ordinal);
                if (lineEnd < 0) break;
                string sizeLine = body.Substring(pos, lineEnd - pos).Trim();
                int semi = sizeLine.IndexOf(';');
                if (semi >= 0) sizeLine = sizeLine.Substring(0, semi);
                if (!int.TryParse(sizeLine, out int size) || size == 0) break;
                int start = lineEnd + 2;
                if (start + size > body.Length) size = body.Length - start;
                sb.Append(body, start, size);
                pos = start + size + 2;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 证书校验：链有效性 + 域名匹配（域名用真实主机名，不是 IP）。
        /// 不接受「证书有问题也放行」——安全性不降级。
        /// </summary>
        private static bool ValidateCertificate(X509Certificate? cert, X509Chain? chain, string host)
        {
            if (cert == null) return false;
            try
            {
                using var c2 = cert as X509Certificate2 ?? new X509Certificate2(cert);

                bool nameOk = false;
                string dnsNames = c2.GetNameInfo(X509NameType.DnsName, false);
                foreach (string n in dnsNames.Split('\n'))
                {
                    if (n.Trim().Equals(host, StringComparison.OrdinalIgnoreCase)) { nameOk = true; break; }
                }
                if (!nameOk)
                    nameOk = c2.GetNameInfo(X509NameType.SimpleName, false).Equals(host, StringComparison.OrdinalIgnoreCase);

                using var build = new X509Chain();
                build.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                bool chainOk = build.Build(c2);

                return nameOk && chainOk;
            }
            catch
            {
                return false;
            }
        }
    }
}
