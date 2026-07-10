using System.Configuration;
using System.Web.Http;

namespace A3ToolsHub
{
    public static class WebApiConfig
    {
        /// <summary>
        /// 共享密钥（HMAC-SHA256 Token 校验用）
        /// 从 web.config appSettings 读取
        /// </summary>
        public static string SecretKey { get; private set; }

        /// <summary>
        /// RSA 私钥（XML 格式，用于解密客户端传来的 AES session key）
        /// 从 web.config appSettings 读取
        /// </summary>
        public static string RsaPrivateKey { get; private set; }

        public static void Register(HttpConfiguration config)
        {
            // 从配置读取密钥
            SecretKey = ConfigurationManager.AppSettings["SecretKey"];
            RsaPrivateKey = ConfigurationManager.AppSettings["RsaPrivateKey"];

            // 属性路由（[Route] 特性）
            config.MapHttpAttributeRoutes();

            // 默认路由（备用）
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // JSON 序列化配置
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.None;
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            // 显式设 CamelCase：ASP.NET Web API 2 默认是 DefaultContractResolver（PascalCase），
            // 但客户端用 System.Text.Json 期望 camelCase（encData 而非 EncData）。
            // 不设这个会导致客户端 GetProperty("encData") 抛 KeyNotFoundException。
            json.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        }
    }
}
