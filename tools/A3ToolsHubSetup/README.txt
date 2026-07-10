A3ToolsHub 部署说明
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
  77168585f6e13bcdc04d420892fdfee18eae98f431f322e0a40fa58f96a5530e8c9e174c5ceebc6250ce36e2b2d3521cff0832abe64ca42c776e0397a44e1263

【RsaPublicKey (客户端用)】
<RSAKeyValue><Modulus>uK0cxzqGaTuKN96VJdpKyu5VkoEJ390JYkuHAge/8INw8heXlEJ9kq5iN9dWuy1/fP2vMJnJxxOA+Wyxw8eLBcQasIm0Fz5M2Osy8Gvj0yWrKBVlg6UvNzRP9+IRTSSD6lw2XomXJTN2475Q4rv1lWI5aQ6OpjATSsTF4KZ7PFkl3efGIo0tEDcHARgPS31BpscmFaIY4Sn49vazKE4hoBFApXL8T+7LjbB2HAdpLfACenYBjVtbLbtG6lpCMihXY8cAMRXLZlaRSofndd7AH5ue7vIcmTqkh3nA0PGjYKPrGK0WJIZ9TzYSQxwpkEgDMJJ5VgTMtdzMT8FcOFG51Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>
