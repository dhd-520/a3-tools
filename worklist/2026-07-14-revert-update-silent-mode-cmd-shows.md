# 2026-07-14 回滚更新静默模式 - cmd 窗口重新显示

## 背景

2026-07-14 09:37 升级到 v2.4.1 时，bat 卡在 or /f + powershell 内嵌命令上无任何输出。
陛下反馈"什么反应都没有"。

陛下进一步指出：v2.3.x 之前更新是好的——cmd 窗口会显示 + 跑完停在那（不自动退出）。
v2.3.14 (commit 8c9e7aa) 把窗口改成不显示 (UseShellExecute=true→false + CreateNoWindow=true + Redirect stdout/stderr)，
导致后续 v2.3.14 cmd 卡住问题暴露出来，并促使 v2.4.0 (19294bf) 加了"静默模式 toast"补救，
再之后 815e463 又加了 or /f + powershell 防重入检测——这正是这次卡住的根源。

## 根本原因

\\\atch
for /f "tokens=*" %%p in ('powershell -NoProfile -Command "Get-Process powershell -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt (Get-Date).AddMinutes(-10) } | Select-Object -ExpandProperty Id"') do (
    taskkill /F /PID %%p
)
\\\

cmd 的 for /f 内嵌命令是同步阻塞的：
1. powershell 启动慢（首次 5-10 秒加载 .NET Framework）
2. 内嵌命令的 \( \ $ 容易被 cmd 解析掉导致语法破坏
3. 没有超时机制——powershell 永远 hang → bat 永远卡住

且 bat 没有进度反馈：6 行 echo 后静默，陛下完全不知道卡在哪。

## DATA 文件误删（我的锅）

手动跑更新时用了 \obocopy /MIR\（镜像模式），把 DATA 目录里 src 没有的文件当垃圾删了。
zip 里没有 DATA → accounts.json / custom-tools.json / settings.json 被删。

**已恢复**：从 bin\\Debug\\DATA 恢复（最近 7/13 10:57）。
**备份位置**：\D:\work\A3Tools\worklist\2026-07-14-data-recovery\

## 修复方案：回滚到 v2.3.8 final (39516a3)

精准回滚 UpdateService.cs 到 39516a3 版本：
- UseShellExecute=true + Arguments=/c start "" /b "batPath"
- 这样 cmd.exe 启动后立即 start 新的独立 cmd 跑 bat（独立进程，父 cmd 退出不影响）
- Environment.Exit(0) 杀 A3Tools 不会级联到独立 cmd
- 窗口显示（UseShellExecute=true 时 CreateNoWindow=true 被忽略）

去掉以下无用的补救代码：
- 815e463 (fix(update): bat 解压进度反馈 + 防重入杀残留 powershell) - \or /f + powershell\ 内嵌卡死
- 19294bf (feat(update): 加升级进度 toast 反馈 (静默模式)) - 静默模式不需要
- Program.cs 的 CheckPreviousUpdateResult 方法 - cmd 窗口显示本身就是反馈

## 改动的文件

| 文件 | 改动 |
|---|---|
| A3Tools/Services/UpdateService.cs | git checkout 39516a3（cmd 窗口显示 + 独立 cmd + cd /d %~dp0 + UTF-8 BOM + detailed log） |
| A3Tools/Program.cs | 删除 CheckPreviousUpdateResult() 调用 + 整个方法（cmd 窗口显示不需要 toast 补救） |
| A3Tools/A3Tools.csproj | Version 2.4.1 → 2.4.2 |
| A3Tools.Common/A3Tools.Common.csproj | Version 2.4.1 → 2.4.2 |
| A3Tools.Plugins.Default/A3Tools.Plugins.Default.csproj | Version 2.4.1 → 2.4.2 |

## Build

\dotnet build\ 0 错 0 新警告（历史警告还在）。

## 经验教训

1. **UseShellExecute=true 时 CreateNoWindow=true 被忽略**，不要重复设置
2. **cmd 的 for /f 内嵌 powershell 永远不可靠**——永远走独立 .ps1 文件 + powershell -File
3. **不要用 robocopy /MIR**——镜像模式会删除 src 没有的文件，破坏用户数据
4. **回滚是有效的 debug 工具**——7/13 引入的"补救"代码（防重入 + 静默 toast）反而引入新问题
5. **bat 应该 echo 进度**——每步立即 echo 让陛下看到执行进展