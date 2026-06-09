# A3Tools 工作日志

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