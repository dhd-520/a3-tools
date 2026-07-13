## 2026-07-13 修复升级静默无反馈（toast 通知）

### 问题
陛下反馈: "这次更新 cmd 窗口都没显示, 之前有就是关不掉"
- 上次 v2.3.14: 升级时弹 cmd 黑窗但关不掉（已修: 加 async drain stream）
- 这次 v2.4.0: CreateNoWindow=true 让 cmd 完全静默，陛下看不到任何进度

陛下选择方案 2: **保留静默（CreateNoWindow=true），加 toast 通知**

### 修复
1. **PerformUpdate / PerformZipUpdate 启动 bat 前**
   - Task.Run 异步弹 AlreadyRunningToastForm "A3Tools 升级中…升级完成后会自动重启"
   - 2.5-3 秒后自动消失（不抢焦点，不阻塞 PerformUpdate）

2. **bat 末尾不再删 _update.log**
   - 成功追加 `STATUS=SUCCESS` 行
   - 失败路径（unzip FAILED / tempExtract missing）保留原 FATAL 行

3. **Program.cs Main 启动时检测 _update.log**
   - `CheckPreviousUpdateResult()` 在 Application.Run 前调用
   - log 含 `STATUS=SUCCESS` → toast "A3Tools 升级成功" (3 秒)
   - log 含 `FATAL` → toast "A3Tools 升级失败:<最后一行 FATAL>" (6 秒)
   - log 无明确标记 → 不提示（避免隔夜重启弹旧升级提示）
   - 检测后删除 log（避免下次启动重复弹）

### 复用已有 toast
直接复用 `AlreadyRunningToastForm`（已在 Program.cs 里实现: WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW + 淡入淡出）
无需新增 toast 类

### 文件改动
- `A3Tools/Services/UpdateService.cs`
  - using System.Windows.Forms + A3Tools.Forms
  - PerformUpdate / PerformZipUpdate 开头加 toast Task.Run
  - bat 末尾保留 log + 加 STATUS=SUCCESS 行（取消 del logPath）
- `A3Tools/Program.cs`
  - using System / System.IO / System.Linq / System.Threading.Tasks / System.Diagnostics
  - Application.Run 前调用 CheckPreviousUpdateResult
  - 新增 CheckPreviousUpdateResult 方法

### Build
0 错 20 警告（均为已有 nullable 警告，无新增）

### 测试
- 用模拟 _update.log 内容验证 CheckPreviousUpdateResult 逻辑
- toast 复用 AlreadyRunningToastForm，已在 v2.3.14 单实例检测里实战过
- 下次发布 v2.4.1 时陛下能完整看到: 点升级 → toast "升级中" → 静默解压 → 新版本启动 → toast "升级成功"

### 已知问题
- 如果 A3Tools 升级过程崩溃，新 A3Tools 启动时检测不到 log，会安静退出（不弹失败 toast）。可能需要单独的崩溃上报机制（TODO）