using System;
using System.IO;
using System.Security;
using System.Text;

namespace A3Tools.Common.Security;

/// <summary>
/// A3ToolsHub 配置文件一键生成器。
/// 生成：Web.config（含 SecretKey + RsaPrivateKey）+ rsa-public-key.xml + README.txt。
/// 对应 A3ToolsHubSetup 的功能，区别是不写文件只生成内容字符串，供调用方自行写出。
/// </summary>
public static class A3ToolsHubConfigGenerator
{
    /// <summary>生成结果</summary>
    public class ConfigResult
    {
        public string SecretKey { get; set; } = string.Empty;
        public string RsaPrivateKey { get; set; } = string.Empty;
        public string RsaPublicKey { get; set; } = string.Empty;
        public string WebConfigContent { get; set; } = string.Empty;
        public string ReadmeContent { get; set; } = string.Empty;
    }

    /// <summary>
    /// 生成 A3ToolsHub 配置内容。
    /// </summary>
    /// <param name="accountCode">账套代码（用于文件夹命名）</param>
    /// <param name="accountName">账套名称（用于 README 说明）</param>
    /// <returns>生成的配置内容</returns>
    public static ConfigResult Generate(string accountCode, string accountName)
    {
        // 1. 生成 RSA-2048 密钥对
        CryptoHelper.GenerateRsaKeyPair(out string publicKey, out string privateKey);

        // 2. 生成 SecretKey（64 字节 hex）
        var keyBytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        string secretKey = BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant();

        // 3. XML 转义（RSA XML 含 < > & 字符）
        string escapedPrivateKey = SecurityElement.Escape(privateKey);
        string escapedPublicKey = SecurityElement.Escape(publicKey);

        // 4. 生成 Web.config 内容
        string webConfig = GenerateWebConfig(secretKey, escapedPrivateKey);

        // 5. 生成 README 内容
        string readme = GenerateReadme(accountCode, accountName, secretKey, publicKey);

        return new ConfigResult
        {
            SecretKey = secretKey,
            RsaPrivateKey = privateKey,
            RsaPublicKey = publicKey,
            WebConfigContent = webConfig,
            ReadmeContent = readme
        };
    }

    /// <summary>
    /// 将配置内容写入目标目录（{dir}/{账套代码+账套名称}/）。
    /// 目录不存在则创建，已存在则覆盖。
    /// </summary>
    /// <param name="baseDir">配置生成根目录</param>
    /// <param name="accountCode">账套代码</param>
    /// <param name="accountName">账套名称</param>
    /// <param name="result">Generate() 的结果</param>
    public static void WriteTo(string baseDir, string accountCode, string accountName, ConfigResult result)
    {
        // 文件夹名：账套代码 + 账套名称，去掉非法字符
        string folderName = SanitizeFolderName(accountCode + "_" + accountName);
        string targetDir = Path.Combine(baseDir, folderName);

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        // 写 Web.config
        File.WriteAllText(
            Path.Combine(targetDir, "Web.config"),
            result.WebConfigContent,
            new UTF8Encoding(false));

        // 写 rsa-public-key.xml（原始 XML，不过滤）
        File.WriteAllText(
            Path.Combine(targetDir, "rsa-public-key.xml"),
            result.RsaPublicKey,
            new UTF8Encoding(false));

        // 写 README.txt
        File.WriteAllText(
            Path.Combine(targetDir, "README.txt"),
            result.ReadmeContent,
            new UTF8Encoding(false));
    }

    /// <summary>检查目标文件夹是否已存在配置</summary>
    public static bool ConfigExists(string baseDir, string accountCode, string accountName)
    {
        string folderName = SanitizeFolderName(accountCode + "_" + accountName);
        string targetDir = Path.Combine(baseDir, folderName);
        return File.Exists(Path.Combine(targetDir, "Web.config"));
    }

    private static string GenerateWebConfig(string secretKey, string escapedPrivateKey)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""SecretKey"" value=""{secretKey}"" />
    <add key=""RsaPrivateKey"" value=""{escapedPrivateKey}"" />
  </appSettings>

  <!--
    程序集绑定重定向：System.Web.Http v5.2.9 强引用 Newtonsoft.Json v6.0.0.0，
    但 A3ToolsHub 发布产出是 Newtonsoft.Json v13.x。
  -->
  <runtime>
    <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
      <dependentAssembly>
        <assemblyIdentity name=""Newtonsoft.Json"" publicKeyToken=""30ad4fe6b2a6aeed"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-13.0.0.0"" newVersion=""13.0.0.0"" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name=""System.Web.Http"" publicKeyToken=""31bf3856ad364e35"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-5.2.9.0"" newVersion=""5.2.9.0"" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name=""System.Net.Http.Formatting"" publicKeyToken=""31bf3856ad364e35"" culture=""neutral"" />
        <bindingRedirect oldVersion=""0.0.0.0-5.2.9.0"" newVersion=""5.2.9.0"" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>

  <system.web>
    <compilation debug=""false"" targetFramework=""4.5.2"" />
    <httpRuntime targetFramework=""4.5.2"" maxRequestLength=""1048576"" executionTimeout=""300"" />
    <customErrors mode=""Off"" />
    <trust level=""Full"" />
  </system.web>

  <system.codedom>
    <compilers>
      <compiler language=""c#;cs;csharp"" extension="".cs"" warningLevel=""4""
                type=""Microsoft.CSharp.CSharpCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"">
        <providerOption name=""CompilerVersion"" value=""v4.0""/>
      </compiler>
      <compiler language=""vb;vbs;visualbasic;vbscript"" extension="".vb"" warningLevel=""4""
                type=""Microsoft.VisualBasic.VBCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"">
        <providerOption name=""CompilerVersion"" value=""v4.5""/>
      </compiler>
    </compilers>
  </system.codedom>
  <system.webServer>
    <handlers>
      <remove name=""ExtensionlessUrlHandler-Integrated-4.0"" />
      <remove name=""OPTIONSVerbHandler"" />
      <remove name=""TRACEVerbHandler"" />
      <add name=""ExtensionlessUrlHandler-Integrated-4.0"" path=""*."" verb=""*"" type=""System.Web.Handlers.TransferRequestHandler"" preCondition=""integratedMode,runtimeVersionv4.0"" />
    </handlers>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength=""104857600"" />
      </requestFiltering>
    </security>
  </system.webServer>
  <system.serviceModel>
    <serviceHostingEnvironment aspNetCompatibilityEnabled=""true"" multipleSiteBindingsEnabled=""true"" />
  </system.serviceModel>
</configuration>
";
    }

    private static string GenerateReadme(string accountCode, string accountName, string secretKey, string publicKey)
    {
        return $@"A3ToolsHub 部署说明（账套：{accountCode} {accountName}）
================================================================================

【生成的内容】
  Web.config          - 包含 SecretKey 和 RsaPrivateKey（仅服务端用）
  rsa-public-key.xml  - RSA 公钥（分发给客户端，配置账套时填入）

【服务端部署步骤】
  1. IIS 物理目录结构必须是：
       A3ToolsHub\Global.asax
       A3ToolsHub\Web.config
       A3ToolsHub\bin\A3ToolsHub.dll
       A3ToolsHub\bin\Newtonsoft.Json.dll
       A3ToolsHub\bin\System.Web.Http.dll 等依赖 DLL
  2. 把 A3ToolsHub\Global.asax 拷贝到账套 IIS 站点下的 A3ToolsHub 根目录
  3. 把 A3ToolsHub\bin\Release\net452\*.dll 拷贝到 A3ToolsHub\bin 目录
  4. 把本目录的 Web.config 覆盖到 A3ToolsHub 根目录（**重要**）
  5. IIS 管理器 → 站点 → 添加应用程序 → 别名 A3ToolsHub → 物理路径指向 A3ToolsHub 根目录
  6. 应用池选 .NET CLR 4.0，管道模式 Integrated
  7. 测试：
       浏览器访问 http://账套地址/A3ToolsHub/ 应返回 A3ToolsHub is running
       浏览器访问 http://账套地址/A3ToolsHub/api/exec 应返回 405（需要 POST）

【客户端配置账套（使用一键生成后自动回填）】
  一键生成功能已自动将 SecretKey 和 RsaPublicKey 填入账套对应字段。
  ConnectionMode 已设为 Http，HttpEndpoint 已自动生成。

【SecretKey（客户端账套填入 HttpSecretKey）】
  {secretKey}

【RsaPublicKey（客户端账套填入 HttpServerPublicKey）】
{publicKey}
";
    }

    /// <summary>去掉文件夹非法字符</summary>
    private static string SanitizeFolderName(string name)
    {
        var sb = new StringBuilder(name);
        foreach (char c in Path.GetInvalidFileNameChars())
            sb.Replace(c, '_');
        foreach (char c in new[] { '<', '>', ':', '"', '|', '?', '*' })
            sb.Replace(c, '_');
        return sb.ToString().Trim();
    }
}
