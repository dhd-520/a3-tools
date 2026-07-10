using System;
using System.Collections.Generic;
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
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)
        {
            return await SendRequestAsync("query", sql, ct);
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
        {
            var result = await SendRequestAsync("nonquery", sql, ct);
            return result.AffectedRows;
        }

        public async Task<QueryResult> ExecuteBatchAsync(string batchSql, CancellationToken ct = default)
        {
            return await SendRequestAsync("batch", batchSql, ct);
        }

        public async Task<List<TableInfo>> GetTablesAsync(string? schemaFilter = null, CancellationToken ct = default)
        {
            var result = await SendRequestAsync("tables", "", ct, schema: schemaFilter);
            if (!result.Success || result.Tables.Count == 0) return new List<TableInfo>();

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
            if (!result.Success || result.Tables.Count == 0) return new List<ColumnInfo>();

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

                // 7. POST
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_endpoint}/api/exec", content, ct);
                string responseJson = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    return new QueryResult
                    {
                        Success = false,
                        Message = $"HTTP {(int)response.StatusCode}: {responseJson}"
                    };
                }

                // 8. 解析响应
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // 错误响应（明文 JSON）
                if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                {
                    return new QueryResult
                    {
                        Success = false,
                        Message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "Unknown error" : "Unknown error"
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
                return new QueryResult { Success = false, Message = ex.Message };
            }
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
    }
}
