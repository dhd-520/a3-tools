# 2026-07-10 A3ToolsHub 客户端 "The given key was not present in the dictionary" 修复

## 问题
客户端连接 A3ToolsHub 代理时报：`The given key was not present in the dictionary.`
日志显示 `已连接（Http 代理：佳琪物资）` 后立即报错。

## 根因
`ExecController` 用 `JsonConvert.SerializeObject(response)` 手动序列化响应，**绕过了 Web API 的 formatter 管线**。
`JsonConvert.SerializeObject` 默认用 `DefaultContractResolver`（PascalCase），输出 `{"EncData":"...","Iv":"..."}`。
客户端 `HttpDataAccess` 用 `root.GetProperty("encData")` 取 camelCase 字段名 → `KeyNotFoundException`。

## 修复（双保险）

### 服务端
1. `ExecController.cs`：加 `_jsonSettings`（含 `CamelCasePropertyNamesContractResolver`），所有 `JsonConvert.SerializeObject` 调用传入 `_jsonSettings`
2. `WebApiConfig.cs`：显式设 `CamelCasePropertyNamesContractResolver`（formatter 管线备用）

### 客户端
3. `HttpDataAccess.cs`：加 `GetPropertyIgnoreCase` 大小写不敏感查找，兼容两种部署

## 验证
- IIS Express 本地测试：响应从 `{"EncData":...,"Iv":...}` 变为 `{"encData":...,"iv":...}` ✅
- Build A3ToolsHub + A3Tools.Common + A3Tools：全部 0 错 ✅

## 修改文件
- `D:\work\A3Tools\A3ToolsHub\Controllers\ExecController.cs`
- `D:\work\A3Tools\A3ToolsHub\App_Start\WebApiConfig.cs`
- `D:\work\A3Tools\A3Tools.Common\DataAccess\HttpDataAccess.cs`
