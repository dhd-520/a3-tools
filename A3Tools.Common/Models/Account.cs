using System.Text.Json.Serialization;
using A3Tools.Common.DataAccess;

namespace A3Tools.Models;

/// <summary>
/// 账套账户模型
/// </summary>
public class Account
{
    public string Code { get; set; } = string.Empty;           // 代码
    public string Name { get; set; } = string.Empty;           // 账套名称
    public string Pinyin { get; set; } = string.Empty;          // 账套名称拼音首字母
    public string Server { get; set; } = string.Empty;         // 账套地址
    public string ServerBackup { get; set; } = string.Empty; // 账套备用地址
    public string ServerUsername { get; set; } = "admin"; // 账套用户名（A3系统默认 admin）
    public string ServerPassword { get; set; } = string.Empty;// 账套密码（加密）
    public string Database { get; set; } = string.Empty;      // 数据库地址
    public string DatabaseName { get; set; } = string.Empty;    // 数据库名称
    public string DbUser { get; set; } = string.Empty;        // 数据库登陆用户名
    public string DbPassword { get; set; } = string.Empty;    // 数据库登陆密码（加密）
    public string RemoteType { get; set; } = string.Empty;    // 远程方式
    public string RemoteAddress { get; set; } = string.Empty; // 远程地址
    public string RemoteUser { get; set; } = string.Empty;    // 远程用户名
    public string RemotePassword { get; set; } = string.Empty;// 远程密码（加密）
    public string Remark { get; set; } = string.Empty;           // 备注

    // ======== 2026-07-09 新增：代理连接模式 ========
    
    /// <summary>
    /// 连接模式：Direct（直连数据库）或 Http（走 A3ToolsHub 代理）
    /// 默认 Direct（向后兼容）
    /// </summary>
    public DataAccessMode ConnectionMode { get; set; } = DataAccessMode.Direct;
    
    /// <summary>
    /// A3ToolsHub 服务端地址（仅 Http 模式用）
    /// 格式：http://账套地址/A3ToolsHub
    /// </summary>
    public string HttpEndpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// HMAC 共享密钥（仅 Http 模式用）
    /// 客户端和服务端必须一致
    /// </summary>
    public string HttpSecretKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 服务端 RSA 公钥（XML 格式，仅 Http 模式用）
    /// 用于加密每次请求的 AES session key
    /// </summary>
    public string HttpServerPublicKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string? DbPasswordDecrypted { get; set; }   // 仅内存中使用

    [JsonIgnore]
    public string? RemotePasswordDecrypted { get; set; } // 仅内存中使用

    /// <summary>
    /// 是否配置了网页版自动登录所需的全部信息（密码非空 + 设置中配置了选择器）
    /// </summary>
    [JsonIgnore]
    public bool HasWebAutoLogin => !string.IsNullOrEmpty(ServerPassword);

    /// <summary>
    /// Build complete SQL Server connection string for DirectDataAccess or HttpDataAccess
    /// </summary>
    [JsonIgnore]
    public string ConnectionString => string.Format("Server={0};Database={1};User Id={2};Password={3};TrustServerCertificate=True;",
        Database, DatabaseName, DbUser, DbPasswordDecrypted ?? DbPassword);
}
