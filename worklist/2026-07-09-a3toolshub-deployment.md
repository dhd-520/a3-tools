# A3ToolsHub 部署文档

> 2026-07-09 首发版本。复用药套现有 IIS .NET CLR 4.0 应用池，零额外依赖。

## 什么是 A3ToolsHub

A3ToolsHub 是 A3Tools 的**服务端代理**，部署在内网账套 IIS 站点下。当账套数据库不开放外网访问时，客户端把 SQL 请求加密后发给 A3ToolsHub，A3ToolsHub 在内网用账套的连接串执行 SQL，结果加密返回。

**安全机制**：
- HMAC-SHA256 Token + 4 小时时间戳窗口（防重放）
- AES-256-CBC 加密请求/响应 body（防抓包）
- RSA-2048 加密 AES session key（每次请求新 key）
- 服务端**无状态**（不存配置/连接串/Token）

---

## 一、生成密钥对（每个账套独立生成）

在能跑 .NET 的机器上（陛下开发机即可）：

```powershell
cd D:\work\A3Tools\tools\A3ToolsHubSetup
.\bin\Debug\net452\A3ToolsHubSetup.exe .\out
```

会生成 3 个文件到 `out/`：

| 文件 | 内容 | 用法 |
|------|------|------|
| `Web.config` | SecretKey + RsaPrivateKey（XML） | **服务端部署用** |
| `rsa-public-key.xml` | RsaPublicKey（XML） | **客户端配账套用** |
| `README.txt` | 部署说明 + 密钥完整内容 | 备份留存 |

**重要**：每个账套生成一套独立密钥对，不要复用。

---

## 二、服务端部署（账套 IIS 服务器）

### 前置条件

- ✅ 账套 IIS 站点正常运行（.NET CLR 4.0 应用池）
- ✅ 账套数据库能被 IIS 所在机器访问（同机 / 内网）
- ❌ 不需要装 .NET 8（用 .NET Framework 4.5.2）

### 步骤 1：拷贝文件到 IIS 站点

把以下文件复制到账套 IIS 站点根目录下的 `A3ToolsHub` 子目录：

```
C:\inetpub\账套站点\A3ToolsHub\
├── bin\
│   ├── A3ToolsHub.dll
│   ├── System.Net.Http.Formatting.dll
│   ├── System.Web.Http.dll
│   ├── System.Web.Http.WebHost.dll
│   ├── Newtonsoft.Json.dll
│   └── ...（其他依赖）
└── Web.config     ← 从 A3ToolsHubSetup\out\Web.config 复制
```

**文件来源**：
- `A3ToolsHub.dll` + `bin\` 依赖：从 `D:\work\A3Tools\A3ToolsHub\bin\Debug\net452\` 复制
- `Web.config`：从 `D:\work\A3Tools\tools\A3ToolsHubSetup\out\Web.config` 复制

### 步骤 2：在 IIS 添加应用程序

打开 **IIS 管理器**：

1. 左侧树形菜单 → 展开账套站点
2. **右键**账套站点 → 添加应用程序
3. 填写：
   - **别名**：`A3ToolsHub`
   - **物理路径**：`C:\inetpub\账套站点\A3ToolsHub`
   - **应用程序池**：选 `.NET CLR v4.0` 的现有池（**不要新建**，复用药套的）
4. 点击确定

### 步骤 3：测试

浏览器访问：

```
http://账套地址/A3ToolsHub/api/exec
```

**期望结果**：
- HTTP **405** Method Not Allowed（端点存在，但只接受 POST）
- **不是** 404（说明部署成功）

如果是 404 → 检查 IIS 应用程序别名、物理路径、bin 目录是否齐全。

---

## 三、客户端账套配置

在 A3Tools 客户端打开账套编辑对话框：

1. **连接模式**：选 **走 A3ToolsHub 代理（账套地址 + /A3ToolsHub）**
2. 此时下方显示 **共享密钥** + **服务端公钥** 两个输入框
3. 填写：
   - **共享密钥**：从 `A3ToolsHubSetup\out\README.txt` 复制 SecretKey
   - **服务端公钥**：从 `rsa-public-key.xml` 复制整个 XML 内容
4. **连接字符串** 仍然填账套数据库的原始连接串（HttpDataAccess 会加密后传给服务端）
5. 保存账套

---

## 四、验证

打开 A3Tools → 工具箱 → SQL 查询工具 → 选中 Http 账套 → 执行 `SELECT 1`：

**期望结果**：
- 状态栏：`✓ Http 执行成功，影响 1 行`
- 结果集 Tab：显示 1 行 1 列（值=1）
- 消息面板：`已连接（Http 代理：账套名）`

**故障排查**：
- **401 Unauthorized**：时间戳超过 4 小时（重发请求）；或者 SecretKey 不一致
- **HTTP 405**：A3ToolsHub 部署成功但请求方法不对
- **HTTP 404**：A3ToolsHub 没部署（IIS 别名错、bin 缺文件、app pool 选错）
- **RSA decrypt failed**：服务端公钥不匹配
- **超时**：账套数据库不在 IIS 所在机器能访问的内网

---

## 五、安全提示

1. **SecretKey 必须保密**——泄露等于账号裸奔
2. **每个账套一套独立密钥**——不要复用
3. **RsaPrivateKey 只在服务端 Web.config**——永远不要复制到客户端
4. **建议 HTTPS**——IIS 站点配 SSL 证书，防止传输层抓包
5. **防火墙白名单**——A3ToolsHub 只接受特定 IP 段访问
6. **日志审计**——服务端可加 W3C 日志记录请求来源 IP

---

## 六、卸载

1. IIS 管理器 → 右键 A3ToolsHub 应用程序 → 删除
2. 删除 `C:\inetpub\账套站点\A3ToolsHub` 整个目录
3. 客户端账套切换回"直连数据库"模式