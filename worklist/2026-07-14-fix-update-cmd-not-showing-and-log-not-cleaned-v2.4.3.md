# 2026-07-14 v2.4.3 - 修复自动更新 cmd 不弹出 + 脚本日志不清理

## 背景

v2.4.2 回滚到 v2.3.8 final cmd 窗口显示模式后,陛下反馈两个问题:
1. **点更新按钮 cmd 窗口不弹出**,需要手动去文件夹找 `_update.bat` 双击才能跑
2. **跑完后没清理 `_update.bat` 和 `_update.log`**,一直留在目录里

## 根本原因

v2.4.2 (commit c62cbf9) 用的启动方式:
```csharp
var psi = new ProcessStartInfo
{
    FileName = "cmd.exe",
    Arguments = $"/k \"\"{batPath}\"",  // ★ 罪魁祸首
    UseShellExecute = true,
    CreateNoWindow = false,
    WorkingDirectory = currentDir
};
```

**问题 1 根因**:
- `Arguments = "/k \"\"{batPath}\""` 展开后是 `/k ""D:\path\_update.bat""`(双引号转义的奇怪格式)
- `UseShellExecute=true` 时 Arguments 直接传给 ShellExecuteEx 的 lpParameters
- Windows / cmd.exe 解析这个格式会出问题:`/k ""path""` 被 cmd 看作 `/k "path"`(外层两个 `"` 被剥掉一层)→ 路径有空格时会被切碎
- cmd 找不到 bat → 立即退出 → 窗口闪过就消失
- 陛下看到 "什么反应都没有",实际是 cmd 启动失败

**问题 2 根因**:
- bat 末尾只有 `del "%~f0"` 删 bat 自己
- 完全没碰 `_update.log`,一直留在目录里占空间

## 修复方案

### 启动方式:直接启动 bat 文件

```csharp
var psi = new ProcessStartInfo
{
    FileName = batPath,  // ★ 直接启动 bat
    UseShellExecute = true,
    CreateNoWindow = false,
    WorkingDirectory = currentDir
};
```

- Windows 自动用 cmd.exe 来跑 `.bat` 文件
- 不需要手动拼 `cmd /k "path"`,避免所有引号转义问题
- cmd 窗口一定会显示
- bat 跑完后 cmd 自动关(因为是 `cmd /c` 等价行为)
- bat 末尾 `pause` → cmd 卡在 pause 等陛下按键 → 陛下关窗前 cmd 不消失

### bat 末尾:延迟清理 + pause

```batch
:: === 1-3. 升级步骤(保留原逻辑)===
...

:: === 4. 启动新版本 ===
start "" "currentExe"

:: === 5. 后台延迟清理 bat 和 log(陛下关窗口后清理,独立 cmd 不被影响)===
(
    echo @echo off
    echo chcp 65001 ^>nul
    echo timeout /t 10 /nobreak ^>nul
    echo del "%~f0" 2>nul
    echo del "logPath" 2>nul
) > "_cleanup.bat"
start /min "" cmd /c "_cleanup.bat"

:: === 6. 让陛下看到结果(pause 不自动关窗)===
echo.
echo ============================================================
echo   升级完成！可以关闭此窗口
echo   日志和脚本将在 10 秒后自动清理
echo ============================================================
pause
```

**关键设计**:
- `_cleanup.bat` 是**独立 cmd 进程**(`start /min cmd /c`),陛下关窗口不影响它
- 延迟 10 秒给陛下时间关窗 + 看到窗口内容
- 即使陛下立即关窗,_cleanup.bat 也会在 10 秒后清理 bat 和 log
- `pause` 让 cmd 窗口不自动关,陛下看完手动关
- `/min` 让清理窗口最小化,不闪烁干扰陛下

### 其他改进

- 等待 A3Tools 退出时间: 2s → **3s**,避免偶发的 exe 文件锁
- 旧 exe 删除加 **retry 循环**,如果偶发文件锁会重试而不是直接失败
- bat 用 UTF-8 with BOM 写入(原本就是),但加 CRLF normalize(source 是 LF,bat 要求 CRLF)

## 改动文件

| 文件 | 改动 |
|---|---|
| `A3Tools/Services/UpdateService.cs` | PerformUpdate + PerformZipUpdate: ProcessStartInfo 改为 FileName=batPath; bat 末尾加 _cleanup.bat + pause; 等待时间 + retry 循环 |
| `A3Tools/A3Tools.csproj` | Version 2.4.2 → 2.4.3 |
| `A3Tools.Common/A3Tools.Common.csproj` | Version 2.4.2 → 2.4.3 |
| `A3Tools.Plugins.Default/A3Tools.Plugins.Default.csproj` | Version 2.4.2 → 2.4.3 |

## Build

`dotnet build A3Tools.sln -c Release`: 0 错 (334 历史警告,无新警告)

## Publish

`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`: ✓ 成功
- 输出: `publish/Standalone/A3Tools.exe` (74.22 MB 单文件)
- zip: `publish/A3Tools_v2.4.3.zip` (71.07 MB,内含 RELEASE_NOTES.md)

## Gitee Release

- **URL**: https://gitee.com/wangq80368036/A3ToolsRelease/releases/tag/v2.4.3
- **Release ID**: 745336
- **Asset**: `A3Tools_v2.4.3.zip` (71.07 MB)
- **Download**: https://gitee.com/wangq80368036/A3ToolsRelease/releases/download/v2.4.3/A3Tools_v2.4.3.zip
- **Git tag**: `v2.4.3` → `f05d3dd` (commit hash)

## 经验教训

1. **UseShellExecute=true + Arguments 引号转义是坑** - 永远优先 `FileName=batPath` 直接启动,让 Windows 自动处理
2. **bat 自己的清理要用独立 cmd 进程**(`start cmd /c`) - 父 bat 删自己的子 cmd 互相不影响
3. **`pause` 是 bat 防止 cmd 自动关的最简单办法** - 比 `/k` 简单可靠
4. **csproj 是 GBK 编码** - PowerShell `Set-Content -Encoding UTF8` 会破坏中文注释(`--` 规则触发),下次用字节级修改
5. **PowerShell `[byte]'4'` 返回 4 不是 0x34** - 必须用 `[byte]0x34` 或者直接 ASCII 数字

## 后续 TODO

- [ ] 陛下本地测试 v2.4.3 自动更新流程(点 v2.4.2 → v2.4.3 升级 → 验证 cmd 窗口弹出 + 升级完自动清理)
- [ ] 如果还有问题,看 _cleanup.bat 是否被 exec 启动(在任务管理器里找 `cmd.exe /c _cleanup.bat`)