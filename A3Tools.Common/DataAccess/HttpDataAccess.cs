using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A3Tools.Common.Security;

namespace A3Tools.Common.DataAccess
{
    /// <summary>
    /// HTTP 代理数据访问实现（走 A3ToolsHub 服务端转发）
    /// 用于不能直连数据库的账套
    /// 
    /// 安全机制：
    /// 1. HMAC-SHA256 Token + 时间戳（4h 窗口）
    /// 2. AES-256-CBC 加密 payload（含连接串 + SQL）
    /// 3. RSA 加密 AES session key（每次请求随机）
    /// </summary>
    public class HttpDataAccess : IDataAccess
    {
        private readonly string _endpoint;       // http://账套地址/A3ToolsHub
        private readonly string _connStr;        // SQL Server 连接串（加密后传给服务端）
        private readonly string _secretKey;      // HMAC 共享密钥
        private readonly string _serverPublicKey; // 服务端 RSA 公钥
        private readonly HttpClient _httpClient;

        /// <summary>
        /// ★ 2026-11-12：是否已降级到「不发 SNI」通道。
        /// null = 还没试过；true = 常规通道握手会被链路层 RST，已固定走降级通道。
        /// </summary>
        private bool? _preferRawTransport;

        /// <summary>
        /// ★ 2026-11-12：当前是否正在使用「不发 SNI」降级通道（诊断用）。
        /// </summary>
        public bool UsingRawTransport => _preferRawTransport == true;

        /// <summary>
        /// ★ 2026-11-12：最近一次失败的详细原因。
        /// 以前 TestConnectionAsync 只返回 bool、错误被彻底吞掉，调用方无从判断
        /// 「地址写错了 / 密钥不对 / 网络不通 / 服务端 500」。
        /// </summary>
        public string? LastError { get; private set; }

        public DataAccessMode Mode => DataAccessMode.Http;
        public string DisplayName { get; }

        public HttpDataAccess(string endpoint, string connStr, string secretKey, string serverPublicKey, string displayName = "")
        {
            _endpoint = endpoint.TrimEnd('/');
            _connStr = connStr;
            _secretKey = secretKey;
            _serverPublicKey = serverPublicKey;
            DisplayName = string.IsNullOrEmpty(displayName) ? $"代理: {_endpoint}" : displayName;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                var result = await ExecuteQueryAsync("SELECT 1", ct);
                if (!result.Success) LastError = result.Message;
                return result.Success;
            }
            catch (Exception ex)
            {
                // 以前这里是 catch { return false; }，错误信息全丢
                LastError = DescribeException(ex);
                return false;
            }
        }

        public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)
        {
            return await SendRequestAsync("query", sql, ct);
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
        {
            // ★ 2026-09-21 陛下反馈：HTTP 代理模式下复制表值函数等 DDL 失败时未报错
            //   根因：之前只返回 result.AffectedRows，没检查 result.Success
            //         → SQL 失败时返回 0，调用方分不清「成功但无影响行」和「失败」
            //   修法：失败时抛 InvalidOperationException，与 Direct 模式行为一致（SqlException）
            //         这样 BulkCopy / CREATE FUNCTION / ALTER TABLE 等所有调用方不用改代码就能感知失败
            var result = await SendRequestAsync("nonquery", sql, ct);
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"HTTP 代理执行失败：{result.Message ?? "Unknown error"}");
            }
            return result.AffectedRows;
        }

        public async Task<QueryResult> ExecuteBatchAsync(string batchSql, CancellationToken ct = default)
        {
            return await SendRequestAsync("batch", batchSql, ct);
        }

        public async Task<List<TableInfo>> GetTablesAsync(string? schemaFilter = null, CancellationToken ct = default)
        {
            var result = await SendRequestAsync("tables", "", ct, schema: schemaFilter);
            if (!result.Success)
            {
                // 失败原因记下来，别让调用方只拿到一个空 List
                LastError = result.Message;
                return new List<TableInfo>();
            }
            if (result.Tables.Count == 0) return new List<TableInfo>();

            var tables = new List<TableInfo>();
            var table = result.Tables[0];
            foreach (var row in table.Rows)
            {
                tables.Add(new TableInfo
                {
                    Schema = row[0]?.ToString() ?? "",
                    Name = row[1]?.ToString() ?? "",
                    Type = row[2]?.ToString() ?? ""
                });
            }
            return tables;
        }

        public async Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName, CancellationToken ct = default)
        {
            var result = await SendRequestAsync("schema", "", ct, tableName: tableName);
            if (!result.Success)
            {
                LastError = result.Message;
                return new List<ColumnInfo>();
            }
            if (result.Tables.Count == 0) return new List<ColumnInfo>();

            var columns = new List<ColumnInfo>();
            var table = result.Tables[0];
            foreach (var row in table.Rows)
            {
                columns.Add(new ColumnInfo
                {
                    Name = row[0]?.ToString() ?? "",
                    TypeName = row[1]?.ToString() ?? ""
                });
            }
            return columns;
        }

        // ==================== 核心通讯 ====================

        private async Task<QueryResult> SendRequestAsync(string action, string sql, CancellationToken ct,
            string? tableName = null, string? schema = null)
        {
            try
            {
                // 1. 生成随机 AES session key
                byte[] sessionKey = CryptoHelper.GenerateAesKey();

                // 2. 构造 payload（明文 JSON）
                var payload = new
                {
                    action,
                    connStr = _connStr,
                    sql,
                    timeout = 60,
                    maxRows = 100000,
                    tableName,
                    schema
                };
                string payloadJson = JsonSerializer.Serialize(payload);

                // 3. AES 加密 payload
                byte[] iv;
                string encData = CryptoHelper.AesEncrypt(payloadJson, sessionKey, out iv);

                // 4. RSA 加密 session key
                byte[] encKeyBytes = CryptoHelper.RsaEncrypt(sessionKey, _serverPublicKey);
                string encKey = Convert.ToBase64String(encKeyBytes);

                // 5. 生成 HMAC Token
                string timestamp = DateTime.UtcNow.ToString("o");
                string token = CryptoHelper.GenerateToken(_secretKey, timestamp);

                // 6. 构造请求
                var requestBody = new
                {
                    timestamp,
                    token,
                    encKey,
                    encData
                };
                string requestJson = JsonSerializer.Serialize(requestBody);

                // 7. POST（常规通道握手被链路层重置时，自动降级到「不发 SNI」通道）
                string requestUrl = $"{_endpoint}/api/exec";
                var (statusCode, responseJson) = await PostJsonWithFallbackAsync(requestUrl, requestJson, ct);

                if (statusCode < 200 || statusCode >= 300)
                {
                    LastError = $"HTTP {statusCode}: {responseJson}";
                    return new QueryResult
                    {
                        Success = false,
                        Message = LastError
                    };
                }

                // 8. 解析响应
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // 错误响应（明文 JSON）
                if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                {
                    string msg = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "Unknown error" : "Unknown error";
                    LastError = msg;
                    return new QueryResult
                    {
                        Success = false,
                        Message = msg
                    };
                }

                // 成功响应（加密 JSON）
                // 服务端 ASP.NET Web API 默认 PascalCase，显式设了 CamelCase 后应是 encData；
                // 但客户端做大小写不敏感查找以兼容两种部署。
                string respEncData = GetPropertyIgnoreCase(root, "encData")?.GetString() ?? "";
                string respJson = CryptoHelper.AesDecrypt(respEncData, sessionKey);

                // 用 Newtonsoft.Json 反序列化：服务端用 Newtonsoft 序列化，DateTime 会输出为 "2024-11-16T00:00:00"。
                // System.Text.Json 会把所有值解析成 JsonElement，填 DataTable 的 DateTime 列时报 InvalidCastException。
                // Newtonsoft.Json 能正确把 JSON 字符串还原为 DateTime。
                QueryResult? result;
                try
                {
                    result = Newtonsoft.Json.JsonConvert.DeserializeObject<QueryResult>(respJson,
                        new Newtonsoft.Json.JsonSerializerSettings
                        {
                            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                        });
                }
                catch
                {
                    // 退回到 System.Text.Json（保留旧行为兜底）
                    result = JsonSerializer.Deserialize<QueryResult>(respJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                return result ?? new QueryResult { Success = false, Message = "Failed to deserialize response" };
            }
            catch (Exception ex)
            {
                // ★ 以前只写 ex.Message，但 .NET 的 HttpRequestException.Message
                //   是 "The SSL connection could not be established, see inner exception."
                //   真正原因（SocketException 10054 等）在 InnerException 里，被丢了。
                //   展开整条异常链，陛下才能一眼看出是网络断了还是密钥错了。
                LastError = DescribeException(ex);
                return new QueryResult { Success = false, Message = LastError };
            }
        }

        /// <summary>
        /// ★ 2026-11-12：发送 POST JSON，带「不发 SNI」降级通道。
        ///
        /// 背景：部分网络链路会在 ClientHello 里读到 SNI=某域名 时直接 RST 掉连接，
        ///   .NET 表现为 HttpRequestException("The SSL connection could not be established")
        ///   → SocketException(10054)。同一台机器上 Chromium / Node 能通，小 ClientHello 的客户端被打掉。
        ///   降级通道：TLS 握手连 IP（不发 SNI）+ HTTP Host 头带真实域名，证书仍按域名校验。
        ///
        /// 只在「连接级失败」时降级；超时（TaskCanceledException）不降级，避免白等两倍时间。
        /// </summary>
        private async Task<(int StatusCode, string Body)> PostJsonWithFallbackAsync(
            string requestUrl, string requestJson, CancellationToken ct)
        {
            Exception? normalError = null;

            // 已确认降级通道可用 → 直接走降级，省掉一次必然失败的重置往返
            if (_preferRawTransport == true)
            {
                try
                {
                    return await PostRawAsync(requestUrl, requestJson, ct);
                }
                catch (Exception ex) when (IsConnectionLevelFailure(ex))
                {
                    normalError = ex;
                }

                try
                {
                    return await PostNormalAsync(requestUrl, requestJson, ct);
                }
                catch (Exception ex)
                {
                    throw new AggregateException(
                        $"降级通道与常规通道都失败。降级通道: {DescribeException(normalError!)}；常规通道: {DescribeException(ex)}");
                }
            }

            try
            {
                return await PostNormalAsync(requestUrl, requestJson, ct);
            }
            catch (Exception ex) when (IsConnectionLevelFailure(ex))
            {
                normalError = ex;
            }

            try
            {
                var result = await PostRawAsync(requestUrl, requestJson, ct);
                _preferRawTransport = true;
                return result;
            }
            catch (Exception rawEx)
            {
                throw new AggregateException(
                    $"常规通道与降级通道都失败。常规通道: {DescribeException(normalError!)}；降级通道: {DescribeException(rawEx)}");
            }
        }

        /// <summary>常规通道：正常发 SNI 的 HTTPS 请求。</summary>
        private async Task<(int StatusCode, string Body)> PostNormalAsync(
            string requestUrl, string requestJson, CancellationToken ct)
        {
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, body);
        }

        /// <summary>降级通道：连 IP + Host 头（TLS 不发 SNI）。</summary>
        private async Task<(int StatusCode, string Body)> PostRawAsync(
            string requestUrl, string requestJson, CancellationToken ct)
        {
            return await RawHttpTransport.PostJsonAsync(requestUrl, requestJson, 300, ct);
        }

        /// <summary>
        /// 是否「连接级失败」（握手被 RST、连接被拒等）——这类才值得降级重试。
        /// 超时（TaskCanceledException）不算，否则一次超时会白等两倍时间。
        /// </summary>
        private static bool IsConnectionLevelFailure(Exception ex)
        {
            for (Exception? cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is System.Net.Sockets.SocketException) return true;
                if (cur is System.Net.Http.HttpRequestException) return true;
                if (cur is IOException) return true;
            }
            return false;
        }

        /// <summary>
        /// 把异常链压成一行可读文本：Type: message → Type: message ...
        /// .NET 默认 Message 常常只说 "see inner exception"，不给用户看没意义。
        /// </summary>
        private static string DescribeException(Exception ex)
        {
            var sb = new StringBuilder();
            var cur = ex;
            int depth = 0;
            while (cur != null && depth < 6)
            {
                if (depth > 0) sb.Append(" → ");
                sb.Append(cur.GetType().Name).Append(": ").Append(cur.Message);
                cur = cur.InnerException;
                depth++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 大小写不敏感地获取 JSON 属性（兼容服务端 PascalCase / camelCase 两种序列化）
        /// </summary>
        private static JsonElement? GetPropertyIgnoreCase(JsonElement root, string name)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop.Value;
            }
            return null;
        }

        /// <summary>
        /// Http 代理模式 BulkCopy：每 BATCH_SIZE 行一条 INSERT INTO ... VALUES (...),(...),...
        /// 一次 HTTP roundtrip 插 BATCH_SIZE 行（默认 500），几万行只要 100 次 roundtrip（10 秒级别）。
        /// 如果一行 INSERT 失败（字符串超长/类型转换），整批回滚，调用方应捕获并定位数据。
        /// </summary>
        public async Task<int> BulkCopyAsync(ResultTable table, string tableName, CancellationToken ct = default)
        {
            if (table.Rows.Count == 0) return 0;

            const int BATCH_SIZE = 500;
            var colNames = string.Join(", ", table.Columns.Select(c => $"[{c.Name}]"));
            int totalCopied = 0;

            for (int offset = 0; offset < table.Rows.Count; offset += BATCH_SIZE)
            {
                ct.ThrowIfCancellationRequested();
                int take = Math.Min(BATCH_SIZE, table.Rows.Count - offset);

                var valuesList = new List<string>(take);
                for (int i = 0; i < take; i++)
                {
                    var row = table.Rows[offset + i];
                    valuesList.Add("(" + string.Join(", ", row.Select(FormatSqlValueForBulk)) + ")");
                }

                var sql = $"INSERT INTO dbo.[{tableName}] ({colNames}) VALUES {string.Join(", ", valuesList)}";
                await ExecuteNonQueryAsync(sql, ct);
                totalCopied += take;
            }

            return totalCopied;
        }

        /// <summary>
        /// 把 .NET 值格式化为 SQL 字面量（与 ProxyHelper.FormatSqlValue 同款，BulkCopy 专用内联避免跨包调用）
        /// </summary>
        private static string FormatSqlValueForBulk(object? val)
        {
            if (val == null || val == DBNull.Value)
                return "NULL";
            if (val is bool b)
                return b ? "1" : "0";
            if (val is DateTime dt)
                return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
            if (val is decimal || val is double || val is float || val is int || val is long || val is short || val is byte || val is sbyte || val is uint || val is ulong || val is ushort)
                return Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture)!;
            if (val is Guid g)
                return $"'{g}'";
            if (val is byte[] bytes)
                return "0x" + Convert.ToHexString(bytes);
            // 默认按字符串处理（转义单引号）
            return $"'{val.ToString()!.Replace("'", "''")}'";
        }
    }
}
