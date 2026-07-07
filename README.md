# A3工具箱

一个用于管理A3账套的 Windows 桌面工具，支持账套管理、一键启动、数据库连接、远程访问及可扩展的工具箱插件系统。
使用人员可直接现在release.zip使用

## 功能特性

### 1. 账套管理

- **账套列表**：DataGridView 展示所有账套信息（代码、名称、地址、数据库、远程等）
- **新增/编辑/删除**：完整的账套 CRUD 操作
- **搜索功能**：支持按代码、名称、拼音首字母、地址等快速搜索过滤
- **数据导入**：从 XML 文件批量导入账套，自动生成编码，保留原始数据到备注
- **复制账套信息**：右键菜单一键复制账套详细信息到剪贴板

### 2. 一键启动

- **电脑端**：启动君则A3.exe 客户端程序
- **开发工具**：启动君则A3集成开发工具.exe
- **网页版**：打开 H5 网页端，支持选择浏览器（Chrome/Edge/Firefox/360）
- **启动选项**：首次启动弹出选项对话框，可选择启动项和浏览器

### 3. 数据库连接

- 直接启动 SSMS 并连接到指定数据库
- 自动填充服务器地址、数据库名称、用户名
- 数据库密码自动解密并复制到剪贴板

### 4. 远程访问

- **RDP**：远程桌面连接，自动生成 RDP 文件并启动
- **向日葵**：一键打开向日葵远程控制
- **其他**：复制远程地址到剪贴板

### 5. 工具箱（插件系统）

- 基于 `tools.json` 配置化加载工具，无需重新编译主程序
- 内置跨库复制工具：复制表结构、复制 Win 表单、复制 APP 表单
- **搜索后台表单**：在指定账套数据库中搜索 S_OBJECT 表，查询表单信息及业务分组
- 支持开发者通过 DLL + 配置扩展新工具

### 6. 进程管理

- 运行状态 Tab 页展示各账套启动的进程（Web/客户端/开发工具/数据库/远程）
- 记录所有启动的进程 ID，支持一键关闭指定账套的全部进程

### 7. Root 模式

- 连续点击标题 5 次触发，输入密码后可查看和复制明文密码

## 解决方案结构

```
A3Tools.sln
├── A3Tools/                          # 主程序（WinForms）
│   ├── A3Tools.csproj
│   ├── Program.cs                    # 程序入口
│   ├── Forms/
│   │   ├── MainForm.cs               # 主窗体逻辑
│   │   ├── MainForm.Designer.cs      # 主窗体设计器
│   │   ├── AccountDialog.cs          # 账套编辑对话框
│   │   ├── AccountDialog.Designer.cs
│   │   ├── LaunchOptionsDialog.cs    # 启动选项对话框
│   │   ├── SettingsDialog.cs         # 设置对话框
│   │   └── SettingsDialog.Designer.cs
│   ├── Models/
│   │   ├── AppSettings.cs            # 应用设置模型
│   │   └── ToolConfig.cs             # 工具配置模型
│   ├── Services/
│   │   ├── DataService.cs            # 数据服务（JSON 持久化）
│   │   ├── PinyinHelper.cs           # 拼音首字母转换
│   │   ├── ToolsConfigService.cs     # 工具配置服务
│   │   └── ToolExecutorService.cs    # 工具执行服务（反射加载）
│   └── Plugins/
│       ├── IPlugin.cs                # 旧版插件接口（保留兼容）
│       └── tools.json                # 工具配置文件
│
├── A3Tools.Common/                   # 公共类库（共享模型和接口）
│   ├── A3Tools.Common.csproj
│   ├── Models/
│   │   ├── Account.cs                # 账套数据模型
│   │   └── AccountStatus.cs          # 账套运行状态模型
│   ├── Plugins/
│   │   └── IToolContext.cs            # 工具上下文接口
│   └── Services/
│       └── EncryptionService.cs      # AES 加密服务
│
├── A3Tools.Plugins.Default/          # 默认插件（内置工具）
│   ├── A3Tools.Plugins.Default.csproj
│   ├── DefaultTools.cs               # 跨库复制工具定义
│   └── Forms/
│       ├── CrossDbCopyTableForm.cs   # 复制表结构窗体
│       ├── CrossDbCopyFormForm.cs    # 复制 Win 表单窗体
│       └── CrossDbCopyAppFormForm.cs # 复制 APP 表单窗体
│
└── DATA/                             # 数据目录
    ├── accounts.json                 # 账套数据
    └── settings.json                 # 应用设置
```

## 数据存储

### 账套数据 (DATA/accounts.json)

```json
{
  "code": "0001",
  "name": "账套名称",
  "pinyin": "ZTMC",
  "server": "http://xxx.com/",
  "serverPassword": "账套密码(AES加密)",
  "database": "数据库地址",
  "databaseName": "数据库名称",
  "dbUser": "数据库用户名",
  "dbPassword": "数据库密码(AES加密)",
  "remoteType": "RDP/向日葵/其他",
  "remoteAddress": "远程地址",
  "remoteUser": "远程用户名",
  "remotePassword": "远程密码(AES加密)",
  "remark": "备注"
}
```

### 设置数据 (DATA/settings.json)

```json
{
  "appDirectory": "A3应用程序目录",
  "lastSelectedAccount": "",
  "launchDesktop": true,
  "launchDevTools": true,
  "launchWeb": false,
  "selectedBrowser": "chrome",
  "showLaunchOptionsDialog": true,
  "ssmsPath": "",
  "trayShowHotkey": "Ctrl+Shift+Z"
}
```

### 工具配置 (Plugins/tools.json)

```json
{
  "tools": [
    {
      "name": "复制表结构",
      "description": "复制表结构到目标数据库",
      "library": "A3Tools.Plugins.Default.dll",
      "className": "A3Tools.Plugins.Default.CrossDbCopyTableTool",
      "methodName": "Execute",
      "enabled": true,
      "icon": "",
      "category": "跨库"
    }
  ]
}
```

## 安全机制

- **密码加密**：使用 AES 加密存储数据库密码、账套密码、远程密码
- **机器绑定**：加密密钥基于机器名和用户名生成，数据无法跨机器解密
- **自动加解密**：保存时自动加密，加载时自动解密，内存中明文仅在 Root 模式可见

## 开发说明

### 环境要求

- .NET 7.0 SDK
- Windows 10/11

### 编译运行

```bash
dotnet build
dotnet run --project A3Tools\A3Tools.csproj
```

### 开发工具插件

工具通过 `tools.json` 配置化加载，开发步骤：

1. 创建类库项目，引用 `A3Tools.Common`
2. 编写工具类，实现 `Execute` 方法（支持多种签名）：
   ```csharp
   // 签名1：无参数
   public void Execute()
   // 签名2：接收当前账套
   public void Execute(Account account)
   // 签名3：接收工具上下文
   public void Execute(IToolContext context)
   // 签名4：接收账套和上下文
   public void Execute(Account account, IToolContext context)
   ```
3. 编译 DLL 放入 `Plugins/` 目录
4. 在 `Plugins/tools.json` 中添加配置项
5. 重启程序自动加载

`IToolContext` 接口提供以下能力：
| 方法 | 说明 |
|------|------|
| `GetSelectedAccount()` | 获取当前选中的账套 |
| `GetSelectedAccountCode()` | 获取当前选中的账套代码 |
| `GetAllAccounts()` | 获取所有账套列表 |
| `ShowMessage(string)` | 显示消息提示框 |
| `ShowError(string)` | 显示错误提示框 |

## 版本历史

### v2.3.0 (2026-07-07)

**重点：自动更新**

- **启动时后台检查更新** —— 有新版本自动弹窗，不打扰
- **帮助 → 检查更新** —— 手动触发检查
- **一键下载+覆盖+重启** —— 点【立即更新】即全套完成
- **GitHub Releases 作为更新源** —— 公开仓库 + 免认证 API
- **不强制更新** —— 用户可选择【稍后】

**实现**
- `UpdateService`（检查/下载/备份/替换/重启）
- `UpdateForm`（更新提示窗 + 进度条 + 速度显示）
- 发布流程：`git tag v2.x.0 && git push github v2.x.0` → 页面拖拽 exe 到 Release

**配置位置**：`A3Tools/Services/UpdateService.cs` 顶部的 `GitHubOwner` / `GitHubRepo` 常量

### v2.2.0 (2026-07-07)

**重点：内置 SQL 查询工具**（替代 SSMS 日常使用）

- **多 Tab 查询** —— 自绘 × 关闭按钮、中键关闭、右键菜单（关闭当前/关闭其他/重命名）
- **对象资源管理器** —— 树状浏览表/视图/函数/存储过程/触发器；右键【复制对象名/复制完整路径/打开脚本】
- **IntelliSense 智能提示** —— 上下文推断 + 50ms 节流；EXEC 后空格、SELECT * 后未输表名都能弹；Ctrl+滚轮调字号
- **SQL 高亮** —— 关键字/字符串/数字/注释分色，闪烁冻结重绘 + 滚动位置保留
- **GO 批处理分割** —— 仿 SSMS 按 GO 拆批执行，单批失败不中断
- **Ctrl+F 查找 / Ctrl+H 替换** —— 上一个/下一个、区分大小写、全部替换
- **多屏位置记忆** —— 记住上次所在屏幕（DeviceName.HashCode），副屏拔走后回退主屏中心
- **多库切换** —— 顶栏下拉 + 异步加载数据库列表
- **状态栏实时字号** —— 默认 12pt，随时看到当前字号
- **字体默认 11pt → 12pt** —— Consolas 等宽，SQL 友好
- **联想框行高 22 → 24** —— 文字不部被切

**修复**

- 回车只能输入一次（重写 HandleEnterWithIndent + SuppressKeyPress 顺序）
- 高亮后滚动条跳回顶部（保存/恢复 vScroll+hScroll）
- 选中状态紊乱（精确保存 selStart/selLen/selColor）
- tools.json 不支持 // 注释（StripJsonLineComments 字符串状态机预处理）
- 联想框光标位置不准（对准当前光标 X）
- 联想框光标移到别处不关闭（OnSelectionChanged 隐藏）

**新增组件**：`SqlEditor`、`IntelliSensePopup`、`LineNumberPanel`、`ObjectExplorerForm`、`SearchReplaceDialog`、`SqlIntelliSenseProvider`、`SqlObjectSchemaCache`、`SqlQueryForm`、`SqlQueryTabPage`、`SqlScriptLoader`、`SettingsStore`

**调佣方式**：账套右键 → **SQL 查询**

### v2.1.0 (2026-06-27)

- **新增自定义工具 MVP**：工具箱新增「自定义工具」分组，支持可视化配置工具名称、主表、复制关键字、关联表（`;` 分隔多个）、关联字段
- **通用复制窗体 `GenericCopyToolForm`**：完全对齐 `CrossDbCopyReportForm` 布局，源/目标账套、搜索主表、添加关键字、确认复制、进度条一应俱全；主表按 `PrimaryKey` 走 `TableCopyService.CopyTableData`，关联表按 `ForeignKey` 走 `TableCopyService.CopyTableDataByParentGuid`
- **工具箱预选账套**：主页支持快捷键预设源/目标账套，启动工具时自动带入数据库连接信息，免去重复选择
- **自定义工具管理**：右键自定义工具按钮弹出【编辑】【删除】菜单；配置保存到 `DATA\custom-tools.json`
- **界面优化**：编辑账套窗体支持调整大小；账套新增与 Root 模式退出流程优化

### v2.0.0 (2026-06-17)

- **三端自动登录**（核心特性）：
  - **网页版**：Chrome DevTools Protocol（CDP）远程控制浏览器自动填表登录 `h5comerp`，原生 `value` setter 兼容 React / antd-mobile / Vue 双向绑定
  - **客户端**：`Win32AutoLoginHelper` 通过 `FindWindow` + `EnumChildWindows` + `WM_SETTEXT` + `BM_CLICK` 自动登录君则 A3.exe
  - **开发工具**：同样走 Win32 API 自动登录君则 A3 集成开发工具.exe（单独加密存储密码）
  - 账套级开关：可在账套编辑中独立配置是否启用网页 / 客户端 / 开发工具自动登录
- **跨库复制配置数据**（新工具）：支持 `S_DATASELECT`（标准查询）/ `S_SYSTEMSETTING`（系统参数）/ `S_CUSTOMDATA`（自定义数据）三种数据类型跨库复制，基于 `TableCopyService.GetTableColumns` + `SqlBulkCopy` 实现，目标库有的列才复制
- **跨库复制 APP 表单增强**：新增编码规则（`S_BILLCODERULE` + `S_BILLCODERULEDETAIL`）+ 标准查询（`S_DATASELECT`）自动复制
- **AccountDialog 重构**：改为标准所见即所得（VS 设计器可正常打开），新增客户端/开发工具自动登录配置项
- **浏览器启动优化**：新窗口模式默认最大化；Tab 模式走 ShellExecute 跳板开新 Tab（避免独立 Edge 进程污染配置）
- **报表复制增强**：复制报表新增 `S_REPORTCOLUMNSETTING` 表（v2.0.3 增量）
- **A3Tools 自身单实例**：Mutex 守卫防重复启动，重复启动自动切到前台
- **打包**：三个 csproj 版本号 1.0.0 → 2.0.0；StandaloneSF 单文件模式稳定运行

### v1.3.0 (2026-06-10)

- **跨库复制工具布局优化**：统一复制报表、复制WEB看板、复制移动看板的布局结构，与其他工具保持一致
- **搜索面板修复**：修复复制单据流转工具的搜索面板显示问题，调整行高比例为100%并修复DataGridView停靠
- **执行后不自动关闭**：6个跨库复制工具（Win表单、APP表单、单据流转、报表、Web看板、移动看板）执行完成后不再自动关闭，方便继续操作

### v1.2.0 (2026-05-30)


- **全局快捷键**：支持托盘显示、新增账套、删除账套、启动账套、设置、链接数据库、远程连接等快捷键配置
- **Edge Dock 边缘停靠**：窗体贴边自动隐藏到托盘，点击托盘或悬停边缘呼出
- **快捷键修复**：修复 InitHotkey 未调用 EnsureReceiver 导致快捷键注册失败的问题

### v1.1.1 (2026-05-13)

- **浏览器进程管理修复**：
  - 修复 Chrome/Edge/Firefox 启动时记录 PID 不正确的问题，统一使用 `UseShellExecute=false` 确保获取真实浏览器进程 ID
  - 关闭账套 Web 时能正确 Kill 对应的浏览器进程
  - 新增 `_processLaunchModes` 记录启动模式（窗口/Tab），标签页模式支持优雅关闭（保留窗口）
- **新增快捷键**：按 Escape 键最小化窗体

### v1.1.0 (2026-05-09)

- **跨库复制表单增强**：
  - 新增「同时复制关联存储过程」选项：勾选后自动复制 S_OBJECT 中 AUDITINGPROCNAME/DELETEPROCNAME/UNAUDITINGPROCNAME 指向的存储过程
  - 新增编码规则复制：自动识别 S_CONTROL 中 DATANAME=CODE/BILLNO 的扩展字段，复制对应的 S_BILLCODERULE 和 S_BILLCODERULEDETAIL
  - 新增标准查询复制：自动识别 S_CONTROL 中 CONTROLTYPE=A3Text/GridColumn 的扩展字段，复制对应的 S_DATASELECT
- **浏览器启动优化**：修复设置中「启动新窗口」选项不生效的问题；增加注册表浏览器路径查找；修复选择特定浏览器不生效的问题
- **设置窗口优化**：窗体高度从 700px 调整为 780px

### v1.0.2 (2026-04-30)

- **新增搜索后台表单工具**：可搜索账套数据库中的 S_OBJECT 表，查询表单信息（GUID、代码、名称、解决方案、业务分组等）
- 支持按表单名称或代码模糊搜索
- 窗体界面优化（宽度1.5倍、选择账套区域加高）

### v1.0.1 (2026-04-30)

- **新增托盘隐藏功能**：拖到屏幕顶部自动隐藏到托盘
- **托盘快捷键**：可在设置中配置快捷键（如 Ctrl+Shift+Z），从托盘恢复显示窗体
- **全局快捷键**：使用独立的 HotkeyReceiver 接收快捷键消息，窗体隐藏时也能响应
- **设置窗口优化**：增加高度，添加托盘快捷键设置项
- 修复 EdgeDockManager 子窗体检测问题
- 修复工具箱插件 DLL 输出路径问题

### v1.1 (2026-04-25)

- 提取 A3Tools.Common 公共类库，支持插件引用共享模型和接口
- 新增 IToolContext 接口（GetSelectedAccountCode、GetAllAccounts 等）
- 新增跨库复制 Win 表单、APP 表单工具
- 新增数据库名称字段
- 修复搜索过滤在刷新后失效的问题

### v1.0 (2026-04-18)

- 账套管理（增删改查、搜索、拼音首字母检索）
- 一键启动 A3 程序（支持选择浏览器、启动选项对话框）
- 数据库连接（SSMS 自动连接）
- 远程访问（RDP/向日葵）
- 数据导入（XML 批量导入）
- AES 密码加密存储（机器绑定）
- 进程管理与运行状态监控
- 工具箱插件系统（tools.json 配置化 + DLL 反射加载）
- 跨库复制表结构工具
- Root 模式（密码明文查看）

## 许可证

MIT License
