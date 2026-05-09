# A3Tools 工作日志

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
