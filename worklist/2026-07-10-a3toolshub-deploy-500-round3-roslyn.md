# 2026-07-10 A3ToolsHub 部署 500 修复（第三轮 - Roslyn CodeDom）

## 背景
陛下反馈：第二轮修完后，访问返回：

```
未能找到 CodeDom 提供程序类型“Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider,
Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Version=2.0.1.0,
Culture=neutral, PublicKeyToken=31bf3856ad364e35”。
```

## 定位
- 我们 publish 出去的 `bin/` 只有 7 个 DLL，没有 `Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll`。
- 我们 publish 出去的 `Web.config` 也不带 `<system.codedom>` 节。
- 结论：错误是从 **IIS 父级 Web.config 继承**进来的（账套所在 IIS 站点根的 Web.config 全局启用了 Roslyn CodeDom provider）。
- A3ToolsHub 是 **预编译**项目（所有 controller 在 `A3ToolsHub.dll` 里编译好了），不需要运行时 C# 编译。所以把 system.codedom 强制覆盖到 .NET Framework 内建提供程序即可，不需要塞 Roslyn DLL 到 bin。

## 修复
- `A3ToolsHub/Web.config` 增加 `<system.codedom>` 节，强制使用 `Microsoft.CSharp.CSharpCodeProvider, System`（.NET Framework 自带，不需要额外 DLL）。
- `tools/A3ToolsHubSetup/Program.cs` 同步模板。

## 关键配置
```xml
<system.codedom>
  <compilers>
    <compiler language="c#;cs;csharp" extension=".cs" warningLevel="4"
              type="Microsoft.CSharp.CSharpCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
      <providerOption name="CompilerVersion" value="v4.0"/>
    </compiler>
    <compiler language="vb;vbs;visualbasic;vbscript" extension=".vb" warningLevel="4"
              type="Microsoft.VisualBasic.VBCodeProvider, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
      <providerOption name="CompilerVersion" value="v4.5"/>
    </compiler>
  </compilers>
</system.codedom>
```

## 验证
- `dotnet build A3ToolsHub.csproj -c Release`：0 警告，0 错误
- `dotnet build A3ToolsHubSetup.csproj -c Release`：0 警告，0 错误
- `dotnet publish A3ToolsHub.csproj -c Release -o D:\work\A3Tools\publish\A3ToolsHubFull`：
  - Root: `Global.asax`、`Web.config`（含 `<system.codedom>`）
  - bin/: `A3ToolsHub.dll` + 6 个第三方 DLL（**无 Roslyn DLL**）
- 发布的 Web.config 已确认包含 `<system.codedom>` 并使用内建 `Microsoft.CSharp.CSharpCodeProvider`

## 重新发布

```powershell
dotnet publish D:\work\A3Tools\A3ToolsHub\A3ToolsHub.csproj -c Release -o D:\work\A3Tools\publish\A3ToolsHub
```

然后把整个目录覆盖到 IIS 的 A3ToolsHub 应用物理目录，回收应用池。

## 验证
```text
http://账套地址/A3ToolsHub/        → 返回 "A3ToolsHub is running"
http://账套地址/A3ToolsHub/api/exec  → 浏览器 GET 返回 405
```

## 万一还是 500
看部署目录里的 `logs/startup.log`，那里有完整堆栈。
