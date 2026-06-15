using System.Text.Json.Serialization;

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

    // ===== 网页版自动登录（Chrome DevTools Protocol）=====
    public string WebUsername { get; set; } = string.Empty;       // 网页版登录用户名
    public string WebPassword { get; set; } = string.Empty;       // 网页版登录密码（加密）
    public string WebUsernameSelector { get; set; } = string.Empty;  // 用户名输入框 CSS 选择器
    public string WebPasswordSelector { get; set; } = string.Empty;  // 密码输入框 CSS 选择器
    public string WebSubmitSelector { get; set; } = string.Empty;    // 登录按钮 CSS 选择器

    [JsonIgnore]
    public string? DbPasswordDecrypted { get; set; }   // 仅内存中使用

    [JsonIgnore]
    public string? RemotePasswordDecrypted { get; set; } // 仅内存中使用

    /// <summary>
    /// 是否配置了网页版自动登录所需的全部信息
    /// </summary>
    [JsonIgnore]
    public bool HasWebAutoLogin =>
        !string.IsNullOrEmpty(WebUsername) &&
        !string.IsNullOrEmpty(WebPassword) &&
        !string.IsNullOrEmpty(WebUsernameSelector) &&
        !string.IsNullOrEmpty(WebPasswordSelector) &&
        !string.IsNullOrEmpty(WebSubmitSelector);
}
