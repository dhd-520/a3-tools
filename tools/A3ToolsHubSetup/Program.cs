using System;
using System.IO;
using System.Security;
using A3ToolsHub.Security;

namespace A3ToolsHubSetup
{
    /// <summary>
    /// A3ToolsHub 一键部署工具
    /// 用法：
    ///   1. 编译 A3ToolsHub 后，部署 Global.asax + Web.config 到 IIS A3ToolsHub 根目录
    ///   2. 把 A3ToolsHub/bin/Release/net452/*.dll 拷贝到 IIS A3ToolsHub/bin 目录
    ///   3. 跑本工具：dotnet run -- 生成密钥对 + 写出配置
    ///   4. 把生成的 Web.config 覆盖到 A3ToolsHub 根目录
    ///   5. RsaPublicKey 告诉客户端（每个账套配账套信息时填）
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("=== A3ToolsHub 一键配置生成器 ===\n");

            // 1. 生成 RSA-2048 密钥对
            Console.WriteLine("[1] 生成 RSA-2048 密钥对...");
            CryptoHelper.GenerateRsaKeyPair(out string publicKey, out string privateKey);
            Console.WriteLine("    完成\n");

            // 2. 生成 SecretKey（64 字节 hex）
            Console.WriteLine("[2] 生成 SecretKey（64 字节 hex）...");
            var keyBytes = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }
            string secretKey = BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant();
            Console.WriteLine("    完成\n");

            // 2.5 转义 XML 特殊字符：RSA XML Key 里含 <, >, & 会被认为是 XML 标签
            // 不转义会被 ConfigurationManager 拒、IIS 500。
            string escapedPrivateKey = SecurityElement.Escape(privateKey);
            string escapedPublicKey = SecurityElement.Escape(publicKey);
            Console.WriteLine("[2.5] XML 转义 RSA 密钥（<, >, &）...\n");

            // 3. 写出 web.config
            Console.WriteLine("[3] 写出 web.config 模板...");
            string webConfigTemplate = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <add key=""SecretKey"" value=""{secretKey}"" />
    <add key=""RsaPrivateKey"" value=""{escapedPrivateKey}"" />
  </appSettings>

  <!--
    程序集绑定重定向：System.Web.Http v5.2.9 强引用 Newtonsoft.Json v6.0.0.0，但 A3ToolsHub 发布产出是 Newtonsoft.Json v13.x。
    缺这个重定向会导致 Application_Start 加载时报 FileLoadException，最终 IIS 500。
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

  <!-- 锁定使用 .NET Framework 内建 C# / VB CodeDom 提供程序，防止父级 Web.config 要求 Roslyn -->
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

            string outDir = args.Length > 0 ? args[0] : ".";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            string webConfigPath = Path.Combine(outDir, "Web.config");
            File.WriteAllText(webConfigPath, webConfigTemplate, new System.Text.UTF8Encoding(false));
            Console.WriteLine($"    写出：{Path.GetFullPath(webConfigPath)}\n");

            // 4. 写出 public key 文件（给客户端用）
            string publicKeyPath = Path.Combine(outDir, "rsa-public-key.xml");
            File.WriteAllText(publicKeyPath, publicKey, new System.Text.UTF8Encoding(false));
            Console.WriteLine($"    写出：{Path.GetFullPath(publicKeyPath)}\n");
            // 5. 写出部署说明
            string readmePath = Path.Combine(outDir, "README.txt");
            string readme = $@"A3ToolsHub 部署说明
================================================

【生成的内容】
  Web.config       - 包含 SecretKey 和 RsaPrivateKey（仅服务端用）
  rsa-public-key.xml - RSA 公钥（分发给客户端，配置账套时填入）

【服务端部署步骤】
  1. IIS 物理目录结构必须是：
       A3ToolsHub\Global.asax
       A3ToolsHub\Web.config
       A3ToolsHub\bin\A3ToolsHub.dll
       A3ToolsHub\bin\Newtonsoft.Json.dll
       A3ToolsHub\bin\System.Web.Http.dll 等依赖 DLL
  2. 把 A3ToolsHub\Global.asax 拷贝到账套 IIS 站点下的 A3ToolsHub 根目录
  3. 把 A3ToolsHub\bin\Release\net452\*.dll 拷贝到 A3ToolsHub\bin 目录（注意：不是根目录）
  4. 把本工具生成的 Web.config 覆盖到 A3ToolsHub 根目录（**重要**：含密钥配置）
  5. IIS 管理器 → 站点 → 添加应用程序 → 别名 A3ToolsHub → 物理路径指向 A3ToolsHub 根目录
  6. 应用池选 .NET CLR 4.0，管道模式 Integrated
  7. 测试：
       浏览器访问 http://账套地址/A3ToolsHub/ 应返回 A3ToolsHub is running
       浏览器访问 http://账套地址/A3ToolsHub/api/exec 应返回 405（需要 POST）

【客户端配置账套】
  1. 把 rsa-public-key.xml 内容复制到客户端账套的 HttpServerPublicKey 字段
  2. 账套 HttpEndpoint 填 http://账套地址/A3ToolsHub
  3. 账套 HttpSecretKey 填上面的 SecretKey 值
  4. 账套 ConnectionMode 选 Http
  5. 账套 ConnectionString 仍然填账套的连接串（HttpDataAccess 会加密后传给服务端）

【安全提示】
  - SecretKey 和 RsaPrivateKey **绝对不能泄露**
  - 每个账套生成一套独立的密钥对
  - SecretKey 是长期共享密钥；4 小时窗口指的是请求 timestamp/token 的有效期，不是 SecretKey 有效期

【SecretKey】
  {secretKey}

【RsaPublicKey (客户端用)】
{publicKey}
";
            File.WriteAllText(readmePath, readme, new System.Text.UTF8Encoding(false));
            Console.WriteLine($"    写出：{Path.GetFullPath(readmePath)}\n");

            Console.WriteLine("=== 完成 ===");
            Console.WriteLine("请把 Web.config 部署到 A3ToolsHub 目录，rsa-public-key.xml 分发给客户端。");

            return 0;
        }
    }
}
