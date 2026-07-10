# 2026-07-10 A3ToolsHub 部署 500 修复（第二轮）

## 背景
陛下反馈：第一轮修完后，重新发布到 IIS 仍然 500。

## 根因（按可能性排序）
1. **Newtonsoft.Json 绑定重定向缺失**（最可能）
   - `System.Web.Http.dll` 5.2.9 强引用 `Newtonsoft.Json v6.0.0.0`。
   - Publish 输出的是 `Newtonsoft.Json v13.0.0.0`（来自 `Newtonsoft.Json 13.0.3` 包）。
   - 没有 `<assemblyBinding>` redirect → 启动期 `FileLoadException` → IIS 500。
2. **Microsoft.Web.Infrastructure.dll 缺失**
   - WebHost 集成管道启动时依赖。
   - SDK-style Library 项目默认只输出编译期引用 DLL，NuGet 包里 Microsoft.Web.Infrastructure 只在 publish 时被拷贝。
3. **缺启动期错误诊断**
   - Application_Start 抛错时 IIS 默认只回 500 页面，没记日志、不返回详细错误。

## 修复
### 1. Web.config 增加 binding redirects（关键）
```xml
<runtime>
  <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
    <dependentAssembly>
      <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
      <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.0.0" />
    </dependentAssembly>
    <dependentAssembly>
      <assemblyIdentity name="System.Web.Http" publicKeyToken="31bf3856ad364e35" culture="neutral" />
      <bindingRedirect oldVersion="0.0.0.0-5.2.9.0" newVersion="5.2.9.0" />
    </dependentAssembly>
    <dependentAssembly>
      <assemblyIdentity name="System.Net.Http.Formatting" publicKeyToken="31bf3856ad364e35" culture="neutral" />
      <bindingRedirect oldVersion="0.0.0.0-5.2.9.0" newVersion="5.2.9.0" />
    </dependentAssembly>
  </assemblyBinding>
</runtime>
<system.web>
  ...
  <trust level="Full" />
</system.web>
```

### 2. csproj 增加 Microsoft.Web.Infrastructure 包
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Web.Infrastructure" Version="2.0.1" />
</ItemGroup>
```

### 3. Global.asax.cs 增加错误日志（路径调整）
- `Application_Start` 包 try/catch
- 错误写入 **部署目录/logs/startup.log**（采用 `AppDomain.CurrentDomain.BaseDirectory`）
- 部署目录不可写时退回到 `%TEMP%\A3ToolsHub-logs\startup.log`
- 同时写 Windows EventLog (Application)
- `Application_Error` 落盘所有运行时异常

### 4. A3ToolsHubSetup/Program.cs 同步
- 重新生成的 Web.config 模板也带同样的 binding redirects。

## 验证
```powershell
# 构建
dotnet build D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj -c Release
dotnet build D:\work\A3Tools\tools\A3ToolsHubSetup\A3ToolsHubSetup.csproj -c Release

# 发布并核对结构
dotnet publish D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj -c Release -o D:\work\A3Tools\publish\A3ToolsHubFull
```

publish 输出：
- 根：`Global.asax`、`Web.config`（含 `<runtime>` 重定向）
- `bin/`：`A3ToolsHub.dll`、`Microsoft.Web.Infrastructure.dll`、`Newtonsoft.Json.dll`、`System.Web.Http.dll`、`System.Web.Http.WebHost.dll`、`System.Net.Http.Formatting.dll`

PowerShell 加载校验：
```powershell
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes("...\A3ToolsHub.dll"))  # OK
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes("...\Newtonsoft.Json.dll"))  # OK
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes("...\System.Web.Http.dll"))  # OK
[Reflection.Assembly]::Load([IO.File]::ReadAllBytes("...\Microsoft.Web.Infrastructure.dll"))  # OK
```

## 部署后如果仍 500
1. 读 `C:\A3ToolsHub-logs\startup.log`，里面有完整异常堆栈。
2. 看 Windows EventLog → Application，看 `[A3ToolsHub]` 来源的条目。
3. 浏览器访问 `http://账套地址/A3ToolsHub/`：现在 `GET /` 应该返回 `A3ToolsHub is running`。
4. `http://账套地址/A3ToolsHub/api/exec` 浏览器 GET 返回 405，是预期。
5. 把 startup.log 内容贴给二哈，可以再针对性修。

## 关键文件
- `D:\work\A3Tools\A3ToolsHub\Web.config`（带 binding redirects）
- `D:\work\A3Tools\A3ToolsHub\Global.asax.cs`（带错误处理）
- `D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj`（含 Microsoft.Web.Infrastructure 包）
- `D:\work\A3Tools\tools\A3ToolsHubSetup\Program.cs`（同步 web.config 模板）