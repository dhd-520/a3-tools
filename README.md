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
