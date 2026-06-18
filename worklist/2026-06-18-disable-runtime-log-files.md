# 2026-06-18 禁用运行期日志文件生成

## 问题
A3Tools 运行后仍会在 exe 同目录生成两个日志文件：

- `win32-login.log`
- `cdp.log`

此前只是清理/归档了已有日志文件，但源码中的日志写入逻辑仍在，运行时会重新创建文件。

## 根因
- `A3Tools/Services/CdpHelper.cs` 的 `CdpLog` 方法仍调用 `File.AppendAllText(..., "cdp.log")`。
- `A3Tools/Services/Win32AutoLoginHelper.cs` 的 `WinLog` 方法仍调用 `File.AppendAllText(..., "win32-login.log")`。
- `MainForm.cs` 中还有一处提示文本引用 `cdp.log`。

## 修改
- `CdpHelper.CdpLog`：移除文件写入，只保留 `Debug.WriteLine`。
- `Win32AutoLoginHelper.WinLog`：移除文件写入，只保留 `Debug.WriteLine`。
- `MainForm.cs`：将“看上面 cdp.log”的提示改为“请在 VS Debug 输出中查看 CDP 日志”。

## 验证
- 搜索源码确认 `A3Tools` 主项目中不再存在 `AppendAllText` 写入 `cdp.log` / `win32-login.log` 的逻辑。
- 执行：`dotnet build D:\work\A3Tools\A3Tools.sln -c Release`
- 结果：0 错误，149 个历史警告。
