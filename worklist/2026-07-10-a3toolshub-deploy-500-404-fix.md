# 2026-07-10 A3ToolsHub 部署 500/404 修复

## 背景
陛下反馈：A3ToolsHub 部署后，服务器本地访问返回 500，客户端访问返回 404。

## 定位
1. `Global.asax` 声明：`Inherits="A3ToolsHub.WebApiApplication"`。
2. 项目中缺少 `Global.asax.cs` / `WebApiApplication` 类型，IIS 加载应用时会找不到启动类，导致 500。
3. `A3ToolsHub.csproj` 之前是 SDK-style Library，`dotnet publish` 只输出 DLL 到发布根目录，没有形成 ASP.NET/IIS 标准结构。
4. 部署说明写成“把 A3ToolsHub/bin/* 拷贝到 A3ToolsHub 子目录”，容易让 DLL 落在根目录；ASP.NET Framework 必须是根目录 `Global.asax/Web.config` + `bin/*.dll`。
5. 客户端 404 的常见原因：IIS 未把 `A3ToolsHub` 建为应用程序、URL 路径不对，或路由未注册/应用未正常启动。

## 修复内容
- 新增 `A3ToolsHub/Global.asax.cs`
  - `WebApiApplication : HttpApplication`
  - `Application_Start()` 调用 `GlobalConfiguration.Configure(WebApiConfig.Register)` 注册 Web API 路由。
- 修改 `A3ToolsHub/A3ToolsHub.csproj`
  - 将 `Global.asax`、`Web.config` 纳入输出/发布。
  - publish 后生成 IIS 标准布局：
    - 根目录：`Global.asax`、`Web.config`
    - `bin/`：`A3ToolsHub.dll`、`Newtonsoft.Json.dll`、`System.Web.Http*.dll` 等依赖。
- 修改 `ExecController`
  - 增加 `GET /` 健康检查：返回 `A3ToolsHub is running`。
  - 增加 `GET /api/exec` 明确返回 405：提示使用 `POST /api/exec`。
- 修改 `tools/A3ToolsHubSetup/Program.cs`
  - README 部署说明改为标准 IIS 目录结构。
  - 修正 SecretKey 说明：SecretKey 是长期共享密钥，4 小时窗口是 timestamp/token 有效期。

## 正确部署结构
```text
A3ToolsHub/
├── Global.asax
├── Web.config
└── bin/
    ├── A3ToolsHub.dll
    ├── Newtonsoft.Json.dll
    ├── System.Net.Http.Formatting.dll
    ├── System.Web.Http.dll
    └── System.Web.Http.WebHost.dll
```

## 验证
```powershell
dotnet build D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj -c Release --nologo
dotnet build D:\work\A3Tools\tools\A3ToolsHubSetup\A3ToolsHubSetup.csproj -c Release --nologo
dotnet publish D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj -c Release -o D:\work\A3Tools\publish\A3ToolsHubTest --nologo
```

结果：
- A3ToolsHub build：0 警告，0 错误
- A3ToolsHubSetup build：0 警告，0 错误
- publish 输出已验证为根目录 `Global.asax/Web.config` + `bin/*.dll`

## 部署后测试
- `http://账套地址/A3ToolsHub/` 应返回：`A3ToolsHub is running`
- `http://账套地址/A3ToolsHub/api/exec` 浏览器 GET 应返回：405，需要 POST
- 客户端实际调用：`POST http://账套地址/A3ToolsHub/api/exec`
