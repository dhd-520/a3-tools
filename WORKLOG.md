# A3Tools 工作日志

## 2026-06-16 (续)

### CDP 自动登录切到 browser-level session 架构（新增页签模式修复）

**问题：** 昨天实现的"新增页签模式"自动登录不生效。从 cdp.log 看到 3 个根因：
1. `FindExistingBrowserDebugPort` 在 `Process.Start` 之后才调，会把刚启动的新进程误认成"现有浏览器"，走错分支
2. `/json/new` HTTP 端点返回 **405 Method Not Allowed**（Edge 111+ 已禁用）
3. 连的是 page-level WebSocket，page 导航后 WebSocket 直接 Aborted，CDP session 失效

**方案：** 走 browser-level session + Target domain（CDP 标准做法）

**实现：**

1. **`CdpSession.cs`**
   - `OnEvent` 签名从 `Action<string, JsonElement>` 改成 `Action<string, JsonElement, string?>`，第三个参数是 sessionId（null 表示 browser-level 事件）
   - `HandleMessage` 解析消息时提取 `sessionId` 字段，透传给 OnEvent 订阅者
   - `SendCommandAsync` 加可选 `string? sessionId` 参数：非 null 时消息体里带 `sessionId` 路由到具体 page
   - `EvaluateAsync` 同样加 `string? sessionId` 参数

2. **`CdpHelper.cs`**
   - 新增 `ConnectBrowserLevelAsync(int port)`：连 `/json/version` 的 wsUrl，拿 browser-level session
   - 新增 `CreateNewTabAsync(int port, string url)`：用 `Target.setAutoAttach({flatten:true})` + `Target.createTarget({url})` + `Target.attachToTarget` 拿 pageSessionId，避开被 Edge 禁用的 `/json/new` HTTP 端点
   - 新增 `AttachToPageAsync(int port, string? urlFilter)`：连 browser-level session 后用 `Target.getTargets` 找现有 page（优先匹配 urlFilter），attach 拿到 pageSessionId
   - `AutoLoginAsync` 加 `string? sessionId` 参数，所有 Page/Runtime/Input 命令带 sessionId 路由
   - `OnLoadEvent` 事件过滤：仅接受 `evtSessionId == sessionId` 的事件（避免其他 page/iframe 事件干扰）
   - 旧 `CreateNewTabInExistingBrowserAsync`（用 `/json/new`）保留为旧路径注释

3. **`MainForm.cs`**
   - 调换流程顺序：Tab 模式 + useCdp 时**先** `FindExistingBrowserDebugPort`，查到设置 `useExistingBrowser=true` 并 `cdpPort=existingPort`，**不启动新进程**直接调 `RunCdpAutoLoginAsync(..., useExistingBrowser: true)`
   - 旧的内嵌 WMI 查现有端口代码（嵌套在 useCdp 块内）已合并到新的 `CdpHelper.FindExistingBrowserDebugPort` 之前调用
   - `RunCdpAutoLoginAsync` 加 `bool useExistingBrowser = false` 参数：
     - `useExistingBrowser=true` → 调 `CreateNewTabAsync` 拿 `(browserSession, pageSessionId)`
     - `useExistingBrowser=false`（新进程场景）→ 循环等 6s 后调 `AttachToPageAsync(port, url)` 拿 `(browserSession, pageSessionId)`
   - 合并 `DoAutoLoginAsync` 进 `RunCdpAutoLoginAsync`（避免多层调用），统一调 `AutoLoginAsync(session, url, ..., sessionId: pageSessionId)`

**影响：** 两个模式（启动新窗口 / 新增页签）都改成 browser-level session，page 导航后 WebSocket 不再断。Tab 模式优先复用现有浏览器，避免无谓启动新进程。

**编译：** `dotnet build A3Tools` 通过，0 错误。

---

## 2026-06-16

### 跨库复制数据库对象新增【缺失对象】按钮

**需求：** 在 CrossDbCopyTableForm 下方查询区域，【查询】按钮旁边新增【缺失对象】按钮，点击后把源库有但目标库没有的对象展示在下方（与【查询】共用类型和关键字）。

**实现：**

1. **Designer.cs（`Forms/CrossDbCopyTableForm.Designer.cs`）**
   - 新增 `btnFindMissing` 按钮字段
   - 位置：x=664, y=5, 宽 120x41（接在 btnSearch 右边）
   - 颜色：橙 #e45e1d（与 btnSearch/btnAddSelected/btnClearSelected/btnCompareTables 区分）
   - 调整 btnAddSelected（x=790）、btnClearSelected（x=916）、btnCompareTables（x=1044）位置右移

2. **Form.cs（`Forms/CrossDbCopyTableForm.cs`）**
   - 新增 `BtnFindMissing_Click` 处理器
   - 校验源/目标库连接信息 + 对象类型
   - 步骤：
     1. 查源库：取当前类型全部对象（含元数据）→ `srcData`（支持关键字过滤）
     2. 查目标库：取当前类型全部对象名 → `tgtNames` HashSet
     3. 差集：`srcData.AsEnumerable().Where(r => !tgtNames.Contains(...))` → `missingRows`
     4. 回填到 `dgvSearchResults`（与查询共用同一 DataGridView），默认全选复选框
     5. 状态行：`源库共 N 个XXX（已按关键字过滤），缺失 M 个`，缺失 > 0 橙色，否则绿色
   - 抽取公共方法：
     - `BuildConnString(server, dbName, user, password)`：统一连接串构造（Windows 集成 vs 用户名密码，密码走 EncryptionService.Decrypt）
     - `GetTypeDisplay(objectType)`：U→表、V→视图、TF→表值函数、FN→标量值函数、P→存储过程

**影响：** 只影响 CrossDbCopyTableForm（跨库复制数据库对象工具），其他 5 个跨库复制工具未改动。

**编译：** `dotnet build A3Tools.Plugins.Default` 通过，0 错误。

---

## 2026-06-10

### 版本更新 v1.3.0

**更新内容：**
- 发布 A3工具箱 v1.3.0（Framework 和 Standalone 两个版本）
- 统一跨库复制工具布局
- 修复搜索面板显示问题
- 6个跨库复制工具执行后不自动关闭

---


### 统一跨库复制工具布局

**问题：** 复制报表、复制WEB看板、复制移动看板三个工具的布局混乱，与复制App表单不一致。

**修复：** 参考 CrossDbCopyFormForm 的布局结构，统一调整三个工具的 Designer.cs：

1. 将两列 TableLayoutPanel 布局改为单列布局（与表单一致）
2. 数据库面板（pnlDatabases）包含 sourceLayout 和 targetLayout 并排
3. CODE 输入框跨越整行
4. 新增 pnlCheckboxes 和 pnlButtons 面板
5. 搜索面板（pnlSearch）在最下方，自适应高度
6. 统一窗体尺寸为 1256x951

**涉及文件：**
- `CrossDbCopyReportForm.Designer.cs` - 复制报表
- `CrossDbCopyWebObjectForm.Designer.cs` - 复制WEB看板
- `CrossDbCopyAppChartForm.Designer.cs` - 复制移动看板

**补充修改（2026-06-10 14:23）：**
- 复制报表：提示和「先删除目标数据」复选框合并到同一行（rowHintAndCheckbox TableLayoutPanel）
- mainLayout 从 9 行减少为 8 行

---

## 2026-06-09

### 复制WIN表单集成搜索功能

**功能：** 将搜索后台表单功能集成到复制WIN表单窗体下方，窗体变大，支持多选并自动拼接OBJECTGUID。

**改进：**
1. 窗体从 873x730 扩大到 1200x800，下方新增搜索区域
2. 搜索区域包含：搜索关键字输入框、查询按钮、添加选中按钮、结果DataGridView
3. DataGridView 支持多选（MultiSelect = true，SelectionMode = FullRowSelect）
4. 点击「添加选中」按钮，自动将选中的 OBJECTGUID 追加到上方的 OBJECTGUID 输入框
5. 自动过滤已添加的 GUID，避免重复

**涉及文件：**
- `CrossDbCopyFormForm.cs` - 新增 `BtnSearch_Click`、`BtnAddSelected_Click` 方法
- `CrossDbCopyFormForm.Designer.cs` - 新增搜索相关控件（Label、TextBox、Button、DataGridView）

---

## 2026-06-04

### 工具箱窗口改为非模态

**问题：** 工具箱中的工具窗体（跨库复制表、跨库复制表单、搜索后台表单等）在执行完后自动关闭（模态对话框），每次想再次执行同一账套的工具时都需要重新打开窗体、重新选择账套。

**修复：** 将 `DefaultTools.cs` 中所有工具窗体的 `ShowDialog()` 改为 `Show()`，改为非模态窗口，手动关闭才退出。

**涉及文件：**
- `DefaultTools.cs` - 7个工具方法 `ShowDialog()` → `Show()`

### Root模式Ctrl+C快捷键复制

**功能：** Root模式下按 `Ctrl+C` 快速复制当前选中账套的完整信息（含明文密码），不弹窗，显示2秒自动消失的Toast提示。

**实现：** `MainForm_KeyDown` 中增加Root模式检测，`Ctrl+C` 时调用 `CopySelectedAccountSilently()`，复制后显示 `ShowToast()` 提示（深色背景居中文字，2秒后自动关闭）。


**涉及文件：**
- `MainForm.cs` - 新增 `CopySelectedAccountSilently()` 方法 + `ShowToast()` 方法

### 复制表结构时同步复制触发器

**功能：** 在跨库复制表结构时，将该表的所有触发器一并复制到目标数据库。

**实现：**
1. 新增 `GetTriggersForTable()` 方法，查询指定表的所有触发器定义（`sys.triggers` + `sys.sql_modules`）
2. 表结构复制完成后，调用 `GetTriggersForTable()` 获取触发器脚本列表，逐个执行到目标库

**涉及文件：**
- `CrossDbCopyTableForm.cs` - 新增 `GetTriggersForTable()` 方法，复制表后自动同步触发器

---

## 2026-06-03

### 启动选项对话框快捷键

**功能：** 在启动选项对话框（LaunchOptionsDialog）中，按 `1`/`2`/`3` 键切换三个复选框的勾选状态，无需鼠标点击。

| 按键 | 功能 |
|------|------|
| `1` / 小键盘1 | 勾选/取消「启动电脑端」 |
| `2` / 小键盘2 | 勾选/取消「启动开发工具」 |
| `3` / 小键盘3 | 勾选/取消「启动网页版」 |

**实现：** 在 `InitializeComponent()` 末尾注册 `KeyDown` 事件，`LaunchOptionsDialog_KeyDown` 处理按键切换复选框状态并阻止按键传播。

**涉及文件：**
- `LaunchOptionsDialog.cs` - 新增 `KeyDown` 事件处理 + `LaunchOptionsDialog_KeyDown` 方法

---

### 主窗体搜索框快捷键

**功能：** 在主窗体任意位置按 `` ` ``（反引号/Tab键上方）键，自动聚焦到搜索框。

**实现：** 构造函数中设置 `KeyPreview = true`，`WireUpEvents` 中注册 `MainForm_KeyDown` 事件处理 `` ` `` 键。

**涉及文件：**
- `MainForm.cs` - 新增 `KeyPreview = true` + `MainForm_KeyDown` 方法

---

### 跨库复制工具升级

**功能：** 改造复制表结构工具，支持多种数据库对象类型，并可选择已存在对象的处理策略。

**新增：**
1. **勾选项「已存在对象先删除再创建」**：勾选则先删后建，未勾选则跳过已存在对象
2. **对象类型下拉选项**：源库和目标库各有一个下拉，支持以下类型：
   - 表结构（U）
   - 视图（V）
   - 表值函数（TF）
   - 标量值函数（FN）
   - 存储过程（P）
3. **多对象类型复制支持**：视图、函数、存储过程通过 `sys.sql_modules` 获取定义并复制

**涉及文件：**
- `CrossDbCopyTableForm.cs` - 核心逻辑重构
- `CrossDbCopyTableForm.Designer.cs` - 新增下拉框 + 复选框 + 布局调整

---

## 2026-05-09

### 跨库复制表单增强（v1.1.0）

**问题：** 原复制表单工具只复制 S_OBJECT/S_CONTROL/S_DATA 三张表，缺少关联的存储过程、编码规则和标准查询，导致复制的表单在目标库无法正常使用。

**新增功能：**

#### 1. 复制关联存储过程
- 新增选项「同时复制关联存储过程」
- 复制 S_OBJECT 后，对 AUDITINGPROCNAME/DELETEPROCNAME/UNAUDITINGPROCNAME 三个字段检查
- 目标库不存在对应存储过程则从源库复制（用 OBJECT_DEFINITION 提取定义，用 ALTER PROCEDURE 创建）
- 目标库已存在则跳过

#### 2. 复制编码规则
- 复制 S_CONTROL 后，查找 DATANAME='CODE' 或 'BILLNO' 的记录
- 解析 EXTENDS 字段（格式：`KEY|@VALUE|!KEY|@VALUE|!...`）
- 提取 `CodeRuleGuid` 对应值
- 目标库不存在对应编码规则则复制 S_BILLCODERULE + S_BILLCODERULEDETAIL
- 公共方法 `ParseExtendsField` 处理扩展字段解析

#### 3. 复制标准查询
- 复制 S_CONTROL 后，查找 CONTROLTYPE='A3Text' 或 'GridColumn' 的记录
- 解析 EXTENDS 字段提取 `DataSelectCode` 值
- 目标库不存在对应标准查询则复制 S_DATASELECT

### 浏览器启动优化
- **问题：** 设置中「启动新窗口」和「选择浏览器」选项不生效
- **根因：**
  1. `GetBrowserPath` 只在固定路径查找，找不到就直接用默认浏览器，完全忽略用户设置
  2. 浏览器找不到时，直接用 ShellExecute 打开 URL，不传 `--new-window` 参数
- **修复：**
  1. 增加注册表查找（HKLM/HKCU/App Paths）
  2. 所有 fallback 路径都尊重 `BrowserNewWindow` 和 `SelectedBrowser` 设置
  3. 新增 `FindBrowserFromRegistry`、`BuildBrowserArgs` 辅助方法

### 设置窗口高度调整
- 从 700px → 740px → 780px（分两次调整），避免内容被遮挡

**涉及文件：**
- `CrossDbCopyFormForm.cs` - 核心逻辑
- `CrossDbCopyFormForm.Designer.cs` - 新增复选框
- `MainForm.cs` - 浏览器启动逻辑、版本号
- `README.md` - 更新文档

---
