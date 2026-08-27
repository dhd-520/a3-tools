# A3Tools 工具知识库

> 这是 AI 助力的核心知识库，每次对话都会作为 System Prompt 注入。
> 更新日期：2026-08-26
> 维护原则：A3Tools 加新功能 / 改功能时，同步更新本文件。

---

## 1. 工具定位

**A3Tools** 是 A3 程序（A3 ERP / 财务软件）的辅助启动器，本质是一个**账套管家 + 工具集合**。

**核心目标**：
- 一键启动 A3 客户端 / 开发工具 / 网页版 / 企业微信
- 集中管理多套账（A3 通常一个公司一套账，开发者手上有十几套很常见）
- 解决 A3 程序本身的"启动慢 / 配置散 / 更新烦"等痛点

**不是什么**：
- 不是 A3 程序本体（启动器）
- 不是破解 / 破解工具
- 不是 A3 业务功能实现（业务功能在 A3 程序里）

---

## 2. 主界面 - Tab1「账套启动」

### 2.1 账套列表

账套数据存在 `DATA/accounts.json`，密码字段用 AES 加密。

**显示列**（DataGridView）：
- 编码（Code）：A3 账套的数字编码，001-999
- 名称（Name）：账套的中文名（例：临沂总账、青岛分店）
- 服务器（Server）：数据库服务器地址
- 数据库（Database）：数据库名（敏感字段，默认显示 ***）
- 远程地址（RemoteAddress）：远程连接地址（敏感字段）
- 远程用户（RemoteUser）：远程登录用户名（敏感字段）
- 备注（Remark）：陛下自由备注

**快捷搜索**：
- 顶部搜索框，按名称 / 编码 / 服务器模糊匹配
- 支持拼音首字母搜索（NPinyin，比如输入 `lyzz` 命中「临沂总账」）

**敏感字段脱敏**：
- 默认显示 `***`
- 标题栏连点 5 次（5 秒内）进入 Root 模式，明文显示
- Root 密码：`xiaopacai`

### 2.2 启动选项

勾选组合控制启动哪些进程：
- � **启动桌面端**（A3Client.exe）
- ☑ **启动开发工具**（A3DevTools.exe）
- ☑ **启动 ERP 网页版**（/h5comerp/#/login，默认勾选）
- ☑ **启动企业微信**（/h5apperp/#/index/home，账号复用 ERP）

**重要约束**：
- 「ERP」和「企业微信」是 2026-08-14 拆开的两个独立选项
- 旧版本 `LaunchWeb` 仍保留向后兼容（等价于 LaunchErp）

### 2.3 启动流程

点「启动账套」按钮后：

1. 检查进程是否已启动（用 PID 列表判断，不是简单的进程名）
2. 区分进程类型：客户端 / 开发工具 / 网页 / 数据库 / 远程
3. 未启动则启动新进程
4. 启动后用 CDP（Chrome DevTools Protocol）自动登录
5. 登录完成后置顶窗口到前台

**进程识别关键点**：
- A3 客户端 / 开发工具 / 网页版的 PID 全部混在一个 list 里
- 用 bool 标记（`IsClientRunning` / `IsDevToolsRunning`）区分类型
- 进程清理时必须按类型分别清，否则会误判

### 2.4 账套操作

- **新增**：工具栏「+ 新增账套」按钮 → 弹出 AccountDialog
- **编辑**：双击行 或 选中后点「编辑」
- **删除**：选中后点「删除」→ 删除并重排后续编码保持连贯
- **快速新增**：「快速新增」按钮（QuickAddAccountDialog）— 一键从模板复制

---

## 3. 主界面 - Tab2「工具箱」

### 3.1 内置工具（4 个）

| 工具名 | 干啥 |
|--------|------|
| **链接数据库** | 启动 SSMS（默认）或 A3Tools 内置 SQL 查询工具，连接到选中账套的数据库 |
| **跨库复制** | 跨 A3 账套复制表数据（TableCopyService），统一调度 |
| **SQL 查询** | 内置 SQL 编辑器，支持语法高亮、执行计划查看 |
| **数据导出** | 把查询结果导出到 Excel / CSV |

### 3.2 插件体系

```
Plugins/
├── IPlugin.cs              # 扩展插件接口
├── tools.json              # 插件配置（哪些 dll 加载）
└── *.dll                   # 第三方插件放这里自动加载
```

**插件开发者接口**：
- `IPlugin` 接口：实现 `Name` / `Description` / `Icon` / `Execute(IToolContext)`
- `IToolContext` 由 MainForm 实现，提供 `GetSelectedAccount()` 等上下文

**插件存放规则**：
- 第三方 dll 放 `Plugins/` 目录
- 工具箱启动时扫描，自动加入工具列表
- `tools.json` 控制加载顺序和启用状态

---

## 4. 主界面 - Tab3「设置」

### 4.1 通用设置
- **A3 程序目录**：默认 `C:\Program Files (x86)\A3`，可改成自定义路径
- **默认启动选项**：决定每次启动账套时勾选哪些进程
- **开机自启**：写注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

### 4.2 更新设置
- **检查更新**：启动时自动检查 Gitee Release
- **双源首发**：主源 Gitee，备用源 Gitee（互相备份）
- **自动下载**：检测到新版本自动下载 zip

### 4.3 高级设置
- **Root 模式**：默认关闭，开启后密码字段明文显示
- **调试日志**：写日志到 `DATA/logs/`，出问题排查用
- **数据备份**：账套改动时自动备份 `accounts.json.bak`

---

## 5. 主界面 - Tab4「更新」

A3Tools 自己（launcher）的更新流程，**跟 A3 程序本身的更新是两码事**：

| 维度 | A3Tools launcher | A3 程序本体 |
|------|-----------------|-------------|
| 检查时机 | launcher 启动时 | A3 程序内部自己检查 |
| 更新源 | Gitee Release API | A3 程序自己服务器 |
| 弹窗 | launcher 弹 UpdateForm | A3 程序自己弹升级框 |
| 自动点确认 | launcher 不管 A3 自己的弹窗 | 由 launcher 的 PrepareUpdateScenarioForLaunch 配合 |

**更新流程（launcher）**：
1. `CheckUpdateOnStartupAsync()` 在启动时跑
2. `UpdateService.CheckForUpdateAsync()` 调 Gitee API
3. 拿到新版本 → 弹 `UpdateForm` 让陛下选「立即更新 / 稍后 / 跳过」
4. 陛下确认后 → 下载 zip → 解压覆盖 → 重启 launcher
5. 旧 exe 改名 `_update.bat` 延迟删除（避免文件占用）

---

## 6. 托盘功能

- 关闭按钮 → 最小化到托盘（不退出）
- 双击托盘图标 → 显示主窗口
- 右键托盘 → 菜单：显示主窗口 / 退出 / 检查更新

**关键**：默认行为是「最小化到托盘」，真正退出要走托盘菜单「退出」。

---

## 7. 已知问题 / 常见故障

### Q: 启动账套后只弹一个窗口，客户端/开发工具没起来？
**A**: 检查「启动选项」是否勾选；看进程列表里是否残留了旧 PID。

### Q: 启动后窗口不置顶？
**A**: `TryBringAccountProcessesToFront()` 失败，看 logs 里有没有 CDP 连接错误。

### Q: 手动关掉开发工具后再点启动，提示"已运行"但实际起不来？
**A**: 已知 bug。原因是 `AccountStatus.ProcessIds` 混合存了客户端 + 开发工具的 PID，只用 bool 标记区分类型，进程清理时只杀 PID 不同不重置 bool。修复方案见 2026-07-14 worklist。

### Q: 账套数据丢失？
**A**: 检查 `DATA/accounts.json` 是否存在；看 `accounts.json.bak` 备份；尝试 Root 模式看敏感字段。

### Q: 更新失败？
**A**: 看 `DATA/logs/update_*.log`；Gitee 双源都挂的可能性极低，可能是网络问题；手动下 zip 覆盖。

### Q: 设置页打开后窗口大小调不了？
**A**: 2026-07-16 修复过：`FormBorderStyle=FixedDialog` → `Sizable` + 加 `MinimumSize`。如果还复现，clear `SettingsDialog` 配置。

### Q: 插件不显示？
**A**: 检查 `Plugins/` 目录里 dll 是否齐全；`A3Tools.Common.dll` 必须和插件 dll 同目录，否则 `Assembly.LoadFrom` 加载失败。

---

## 8. 数据存储

所有数据在 `DATA/` 目录：

```
DATA/
├── accounts.json            # 账套列表（含加密密码）
├── accounts.json.bak        # 账套备份
├── settings.json            # 应用程序设置（含加密 DevTools 密码）
├── ai_chat_config.json      # AI 助力配置（厂商、ApiKey 等）
├── ai_chat/
│   └── sessions.json        # AI 对话历史（AES 加密）
├── logs/                    # 运行日志（按日期分文件）
│   └── 2026-08-26.log
└── update/                  # 更新相关临时文件
    └── _update.bat
```

---

## 9. 版本信息

- **当前版本**：v2.5.0
- **技术栈**：C# WinForms + .NET 7
- **依赖**：
  - Markdig（Markdown 渲染）
  - Microsoft.SqlServer.SqlManagementObjects（SSMS 集成）
  - NPinyin（拼音首字母搜索）

---

## 10. 跟陛下的协作约定

- **陛下叫陛下**，不要叫"用户"或"开发者"
- **技术栈**：React + C# / .NET Framework 4.5（SnackStorePOS），C# / .NET 7（A3Tools）
- **沟通风格**：简洁直接，非明确要求不要把代码贴聊天框
- **当前活跃项目**：A3Tools（A3 程序启动器）、SnackStorePOS（便利店收银）
- **WinForm 原则**：所见即所得，能用 Designer 就别动态 new

---

_本文件由 A3Tools 团队维护，新增/修改功能时同步更新_
