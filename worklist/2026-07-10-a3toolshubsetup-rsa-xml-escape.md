# 2026-07-10 A3ToolsHubSetup 工具生成的 Web.config XML 转义修复

## 背景
陛下反馈：A3ToolsHubSetup.exe 生成的 Web.config 替换后 500，VS 发布的 Web.config 正常。

## 根因
A3ToolsHubSetup 工具的 `webConfigTemplate` 字符串直接把 RSA 私钥 XML 拼到 `appSettings` 节点的 value 属性里：

```csharp
<add key=""RsaPrivateKey"" value=""{privateKey}"" />
```

但 `privateKey` 是 `<RSAKeyValue>...</RSAKeyValue>` 这样的完整 XML 字符串，含 `<` `>` `&` 等特殊字符。

结果生成的 Web.config 第 5 行变成：

```xml
<add key="RsaPrivateKey" value="<RSAKeyValue><Modulus>.../Modulus>...</RSAKeyValue>" />
```

`<` 在 XML 属性值里必须转义为 `&lt;`，否则 **XML 解析失败**。`ConfigurationManager` 读到这行会抛 `ConfigurationErrorsException` → IIS 500。

## 验证
臣用 PowerShell 加载生成出的 Web.config 跑 XML 解析，得到：

```
无法将值"..."转换为类型"System.Xml.XmlDocument"。错误:
"<"(十六进制值 0x3C)是无效的特性字符。第 5 行，位置 37。
```

而 VS 发布的 Web.config 里 value 是占位符 `CHANGE_ME_TO_RSA_PRIVATE_KEY_XML`（无特殊字符），所以能正常解析。

## 修复
`tools/A3ToolsHubSetup/Program.cs`：
1. `using System.Security;`
2. 写入模板前用 `SecurityElement.Escape()` 转义 privateKey 和 publicKey：
   ```csharp
   string escapedPrivateKey = SecurityElement.Escape(privateKey);
   string escapedPublicKey = SecurityElement.Escape(publicKey);
   ```
3. 模板里用 `escapedPrivateKey` 代替 `{privateKey}`。
4. rsa-public-key.xml 仍写入**原始** XML（直接给客户端用，不是 XML attribute 值）。

## 验证
- `dotnet build A3ToolsHubSetup`：0 警告，0 错误
- 重新跑 Setup，生成新 Web.config
- PowerShell `[xml]` 解析：**VALID XML now** ✅
- 用 `ConfigurationManager.OpenMappedExeConfiguration` 读取：
  - `AppSettings.Settings["RsaPrivateKey"].Value` 拿到 `<RSAKeyValue>...` 正确反转义
  - `RSA.FromXmlString(value)` 成功
  - `RSA.ToXmlString(false)` 导出的公钥与生成的 `rsa-public-key.xml` 完全一致

## 实际效果
- Web.config 现在是合法 XML
- IIS / ConfigurationManager 可以正常解析
- 服务器端读到的 RsaPrivateKey 是原始的 `<RSAKeyValue>...</RSAKeyValue>`，可以正确还原 RSA 私钥

## 教训
**不要把 XML 字符串直接拼到 Web.config 的 XML attribute value 里**。必须用 `System.Security.SecurityElement.Escape()` 转义 `< > & " '`，否则 .NET 的配置解析器会拒。

如果以后在 .NET 配置里塞可能含 XML 特殊字符的字符串，**永远先 Escape**。
