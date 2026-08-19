# 2026-08-13 A3 程序更新配合器（独立于 launcher 自己的更新机制）

## 📋 背景

陛下 09:16-09:18 多次强调："A3 客户端/开发工具 vs A3Tools launcher 是两套完全独立的更新机制"。
陛下 09:43 强调："不要把工具任何更新逻辑加到这里来"——明确要求把 A3 程序更新和 launcher 自己的更新**严格分开**。

陛下 10:03 拍板 A3 程序更新分 3 种场景处理：
1. **场景 1**：单启 A3 程序 + 无其他 A3 在跑 → 自动点是
2. **场景 2**：同时启客户端+开发工具 + 无其他 A3 在跑 → 杀 launcher 自己启的 devtools + 自动点是
3. **场景 3**：有其他 A3 在跑 → 弹 launcher 自己的确认框，陛下点否时抓 A3 升级框"否"按钮 BM_CLICK

## ✅ 实现

### 新文件 `Services/A3ProgramUpdateChecker.cs`（独立类,跟 launcher 更新无关）

公开方法：
- `DetectA3ProgramUpdate(timeoutMs)` - 短轮询桌面找"升级文件检测"窗口（只检测,不点）
- `ClickYesButton(timeoutMs)` - 抓 A3 升级框"是"按钮,SendMessage BM_CLICK
- `ClickNoButton(timeoutMs)` - 抓 A3 升级框"否"按钮,SendMessage BM_CLICK
- `WaitAndConfirmUpgradeComplete(timeoutMs)` - 等"升级完成!"弹框,自动点确定

私有工具方法：
- `FindDialogByTitle` - EnumWindows 找标题匹配的窗口
- `ClickButtonByText` - EnumChildWindows 找 Button + SendMessage BM_CLICK
- `ClickFirstButton` - 兜底:抓窗口内第一个 Button

### 修改 `Forms/MainForm.cs`

新增方法 `HandleA3ProgramUpdateBeforeLaunch(account, settings)`（在 `CloseExternalA3Processes` 之前）：
- 3 秒轮询 DetectA3ProgramUpdate
- 没检测到 → 直接 return,走原有自动登录
- 检测到 → 按场景 1/2/3 调度
  - 场景3 用 `GetExternalProcessDisplayNames()` 判定是否已有其他 A3 进程
  - 场景3-陛下点是 → 复用 `CloseExternalA3Processes()` 关掉所有外部 A3 进程
  - 场景3-陛下点否 → ClickNoButton 抓"否"按钮 BM_CLICK
  - 场景2 → 复用 `GetOurTrackedDevtools()` 拿 launcher 自己启的 devtools,杀掉释放文件锁
  - 场景1/2 → ClickYesButton 抓"是"按钮 BM_CLICK

在 `LaunchSelectedAccount` 调用（**在 `PrepareUpdateScenarioForLaunch` 之后**）:
```csharp
if (!PrepareUpdateScenarioForLaunch(account, settings)) return;

// ★ 2026-08-13 A3 程序更新配合(独立于 launcher 自己的 UpdateService):
//   launcher 启动 A3 客户端/开发工具前,检查 A3 是否弹了更新框,按场景1/2/3 处理
HandleA3ProgramUpdateBeforeLaunch(account, settings);
```

### 修改 `A3Tools.csproj`

加新文件编译项 `Services\A3ProgramUpdateChecker.cs`（SDK 默认自动包含,但显式声明便于追踪）。

## 🛡️ 严正承诺（已验证）

launcher 自己的更新机制**一字未动**:
- ❌ `Services/UpdateService.cs` - 未改
- ❌ `CheckUpdateOnStartupAsync()` 方法 - 未改
- ❌ `Forms/UpdateForm.cs/.Designer.cs` - 未改
- ❌ `_userUpdateChoice` 字段 - 未改
- ❌ `HasUpdate`、`UpdateScenario` 枚举 - 未改
- ❌ `PrepareUpdateScenarioForLaunch` 现有逻辑 - 未改(只在它**之后**加了新方法调用)

**严格用词区分**:
- **A3 程序更新** = A3 客户端/开发工具本体的更新（本任务）
- **A3Tools launcher 更新** = launcher 自己的 Gitee Release 更新（陛下用的这个启动器）

## 🔍 现场抓取的 A3 升级框结构

陛下 10:11 弹出更新窗口后,臣用 Win32 API 抓到的结构:
```
升级文件检测 (HWND=658060, WindowsForms10.Window.8.app.0.1ca0192_r8_ad1)
├── 系统检测到有需要升级的文件,版本号为0.0.0.V360,是否需要升级? (STATIC)
├── 是 (BUTTON, WindowsForms10.BUTTON.app.0.1ca0192_r8_ad1)
└── 否 (BUTTON, WindowsForms10.BUTTON.app.0.1ca0192_r8_ad1)
```

**关键发现**:
- 窗口标题 = "升级文件检测"(场景判断关键字)
- 是/否两个按钮,直接 SendMessage BM_CLICK 抓按钮最稳(不用回车/不用坐标)
- 升级提示的版本号 `0.0.0.V360` 是 A3 服务端版本号(逻辑里不用,陛下说"不用管版本号")

## 📦 文件改动清单

| 文件 | 状态 | 行数 |
|------|------|------|
| `Services/A3ProgramUpdateChecker.cs` | 新增 | 275 行 |
| `Forms/MainForm.cs` | +412/-2 | 新增 HandleA3ProgramUpdateBeforeLaunch 方法 + 调用点 |
| `A3Tools.csproj` | +6 | 新文件编译项 |

## 🧪 编译验证

```
dotnet build A3Tools.sln -c Debug
-> 已成功生成
-> 2 个警告(NU1701 NPinyin 兼容性,与本次改动无关)
-> 0 个错误
```

## 🧪 陛下手动测试用例

### 用例 1：A3 有更新 + 单启客户端 + 无其他 A3 在跑（场景 1）
1. 启动 A3 客户端触发"升级文件检测"窗口
2. 在 launcher 里点启动某个账套（只勾客户端）
3. **预期**：launcher 自动点"是"→ A3 升级→ 自动点升级完成→ A3 重启→ 自动登录

### 用例 2：A3 有更新 + 同时启客户端+开发工具 + 无其他 A3 在跑（场景 2）
1. launcher 已启过某个账套的 devtools（用模拟账号跑一次留下 devtools）
2. 启动 A3 客户端触发"升级文件检测"窗口
3. 在 launcher 里点启动同一个账套（勾客户端+开发工具）
4. **预期**：launcher 杀掉自己启的 devtools→ 自动点"是"→ A3 升级→ 启开发工具

### 用例 3：A3 有更新 + 已有其他 A3 在跑（场景 3-是）
1. 手动启一个 A3 客户端（不要 launcher 启）
2. 在 launcher 里点启动某个账套（勾客户端）
3. **预期**：弹确认框"升级需要关闭所有 A3 进程,是否升级?"→ 陛下点是→ 关外部 A3→ 自动点"是"→ A3 升级→ 自动登录

### 用例 4：A3 有更新 + 已有其他 A3 在跑 + 陛下点否（场景 3-否）
1. 同用例 3,但确认框陛下点"否"
2. **预期**：launcher 抓 A3 升级框"否"按钮 BM_CLICK→ A3 走"不升级"流程→ 弹登录框→ launcher 启动 A3→ 自动登录

### 用例 5：A3 无更新（陛下原来已点过"否"过了）
1. A3 程序升级框已关闭（或点了"否"）
2. launcher 启动账套
3. **预期**：HandleA3ProgramUpdateBeforeLaunch 短轮询 3 秒没找到窗口,直接 return,走原有自动登录