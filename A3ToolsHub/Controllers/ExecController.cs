using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using A3ToolsHub.Security;
using A3ToolsHub.Sql;
using Newtonsoft.Json;

namespace A3ToolsHub.Controllers
{
    /// <summary>
    /// 唯一端点：POST /api/exec
    /// 请求体（JSON）：
    /// {
    ///   "timestamp": "2026-07-09T11:42:00Z",       // ISO 8601 UTC
    ///   "token": "HMAC-SHA256(secretKey, timestamp)", // hex
    ///   "encKey": "RSA加密的AES session key",       // base64
    ///   "encData": "AES加密的payload JSON"          // base64
    /// }
    /// 
    /// AES 解密后的 payload JSON：
    /// {
    ///   "action": "query" | "nonquery" | "batch" | "tables" | "schema",
    ///   "connStr": "Server=...;Database=...;Password=***",
    ///   "sql": "SELECT * FROM ...",
    ///   "timeout": 60,
    ///   "maxRows": 100000,
    ///   "tableName": "BigTable"   // schema action 用
    ///   "schema": "dbo"           // tables action 用
    /// }
    /// </summary>
    public class ExecController : ApiController
    {
        private static readonly SqlExecutor _executor = new SqlExecutor();

        [HttpPost]
        [Route("api/exec")]
        public HttpResponseMessage Exec([FromBody] ExecRequest req)
        {
            // 1. 基本参数校验
            if (req == null || string.IsNullOrEmpty(req.Timestamp) ||
                string.IsNullOrEmpty(req.Token) ||
                string.IsNullOrEmpty(req.EncKey) ||
                string.IsNullOrEmpty(req.EncData))
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, "Missing required fields");
            }

            // 2. 读取配置
            string secretKey = WebApiConfig.SecretKey;
            string rsaPrivateKey = WebApiConfig.RsaPrivateKey;

            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(rsaPrivateKey))
            {
                return CreateErrorResponse(HttpStatusCode.InternalServerError, "Server not configured (missing secretKey or RSA private key)");
            }

            // 3. Token + 时间戳校验
            string tokenError = TokenValidator.Validate(req.Timestamp, req.Token, secretKey);
            if (tokenError != null)
            {
                return CreateErrorResponse(HttpStatusCode.Unauthorized, tokenError);
            }

            // 4. RSA 解密 AES session key
            byte[] sessionKey;
            try
            {
                byte[] encKeyBytes = Convert.FromBase64String(req.EncKey);
                sessionKey = CryptoHelper.RsaDecrypt(encKeyBytes, rsaPrivateKey);
                if (sessionKey.Length != 32)
                {
                    return CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid session key length (expect 32 bytes)");
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, "RSA decrypt failed: " + ex.Message);
            }

            // 5. AES 解密 payload
            string payloadJson;
            try
            {
                payloadJson = CryptoHelper.AesDecrypt(req.EncData, sessionKey);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, "AES decrypt failed: " + ex.Message);
            }

            // 6. 解析 payload
            ExecPayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<ExecPayload>(payloadJson);
                if (payload == null || string.IsNullOrEmpty(payload.Action) || string.IsNullOrEmpty(payload.ConnStr))
                {
                    return CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid payload (missing action or connStr)");
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, "JSON parse failed: " + ex.Message);
            }

            // 7. 执行
            QueryResult result;
            try
            {
                switch (payload.Action.ToLowerInvariant())
                {
                    case "query":
                        result = _executor.ExecuteQuery(payload.ConnStr, payload.Sql, payload.Timeout, payload.MaxRows);
                        break;

                    case "nonquery":
                        result = _executor.ExecuteNonQuery(payload.ConnStr, payload.Sql, payload.Timeout);
                        break;

                    case "batch":
                        result = _executor.ExecuteBatch(payload.ConnStr, payload.Sql, payload.Timeout, payload.MaxRows);
                        break;

                    case "tables":
                        result = _executor.GetTables(payload.ConnStr, payload.Schema);
                        break;

                    case "schema":
                        if (string.IsNullOrEmpty(payload.TableName))
                        {
                            result = new QueryResult { Success = false, Message = "tableName is required for schema action" };
                        }
                        else
                        {
                            result = _executor.GetTableSchema(payload.ConnStr, payload.TableName);
                        }
                        break;

                    default:
                        result = new QueryResult { Success = false, Message = "Unknown action: " + payload.Action };
                        break;
                }
            }
            catch (Exception ex)
            {
                result = new QueryResult { Success = false, Message = "Server error: " + ex.Message };
            }

            // 8. AES 加密结果返回
            string resultJson = JsonConvert.SerializeObject(result);
            byte[] respIv;
            string encResult = CryptoHelper.AesEncrypt(resultJson, sessionKey, out respIv);

            var response = new ExecResponse
            {
                EncData = encResult,
                Iv = Convert.ToBase64String(respIv)
            };

            var respContent = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = respContent };
        }

        private HttpResponseMessage CreateErrorResponse(HttpStatusCode code, string message)
        {
            var err = new { success = false, message = message };
            var content = new StringContent(JsonConvert.SerializeObject(err), Encoding.UTF8, "application/json");
            return new HttpResponseMessage(code) { Content = content };
        }
    }

    // ==================== 请求/响应模型 ====================

    public class ExecRequest
    {
        public string Timestamp { get; set; } = "";
        public string Token { get; set; } = "";
        public string EncKey { get; set; } = "";
        public string EncData { get; set; } = "";
    }

    public class ExecPayload
    {
        public string Action { get; set; } = "";       // query / nonquery / batch / tables / schema
        public string ConnStr { get; set; } = "";      // SQL Server 连接串
        public string Sql { get; set; } = "";          // SQL 语句
        public int Timeout { get; set; } = 60;          // 超时秒
        public int MaxRows { get; set; } = 100000;      // 最大行数
        public string TableName { get; set; }          // schema action 用
        public string Schema { get; set; }             // tables action 用
    }

    public class ExecResponse
    {
        public string EncData { get; set; } = "";
        public string Iv { get; set; } = "";
    }
}
