# 2026-06-30 自定义工具 - 搜索列设置

## 需求

「自定义工具」主表的搜索结果目前固定 `SELECT TOP 5000 *`，无论主表有多少列都会全部展示。需要根据配置只显示指定列、隐藏指定列、用中文标题替代英文列名。

- 搜索列名（多个用英文分号）: `GUID;CODE;SUBSYSTEMGUID;NAME;NOTES`
- 列显示名称（多个用英文分号）: `GUID;代码;分类;名称;备注`
- 隐藏列（多个用英文分号）: `GUID;SUBSYSTEMGUID`

## 设计原则

- **向后兼容**：三个新字段全部留空 → 完全保留旧行为（旧 `SELECT *` + 旧 WHERE）
- **PrimaryKey 永远显示**：即便 `HiddenColumns` 标记了也要显示出来，否则「添加选中」逻辑会断
- **数据库不存在列自动过滤**：配置的列不在 db 中（被删/拼错）自动跳过，搜索照常进行
- **PrimaryKey 自动参与搜索**：即使没写在 `SearchColumns` 里，只要 db 存在，就自动插入到搜索列表

## 改动文件

### 新增字段
- `A3Tools.Common/Models/CustomToolConfig.cs`
  - 新增 3 个 string 属性：`SearchColumns` / `ColumnDisplayNames` / `HiddenColumns`
  - 新增 3 个解析器：`SearchColumnList` / `ColumnDisplayNameList` / `HiddenColumnSet`（[JsonIgnore]）

### 配置窗体
- `A3Tools.Plugins.Default/Forms/CustomToolConfigDialog.Designer.cs`
  - 行数：8 → 12（新增 3 个多行 TextBox + 1 个 hint Label）
  - 窗体高度：645 → 880
  - 新增控件：`lblSearchColumns` / `txtSearchColumns`、`lblColumnDisplayNames` / `txtColumnDisplayNames`、`lblHiddenColumns` / `txtHiddenColumns`、`lblSearchHint2`
- `A3Tools.Plugins.Default/Forms/CustomToolConfigDialog.cs`
  - 编辑模式加载 3 个新字段
  - BtnSave 增加校验：
    - SearchColumns 和 ColumnDisplayNames 数量必须一致
    - SearchColumns 必须包含 PrimaryKey
    - 任一为空则报错

### 通用复制窗体
- `A3Tools.Plugins.Default/Forms/GenericCopyToolForm.cs`
  - **BuildSearchInfo + 列存在性校验**：连接打开后查 `INFORMATION_SCHEMA.COLUMNS` 拿到主表实际列名，过滤掉 `SearchColumns` 中不存在的列，再保证 `PrimaryKey` 列入搜索
  - **BuildSearchSql(List<string> validSearchColumns)**：旧硬编码 `WHERE [pk] LIKE OR [NAME] LIKE` 改为按配置列循环拼接 `OR`
  - **GetAllColumns**：辅助方法一次性查回主表全部列
  - **删除静态 HasColumn**：被 GetAllColumns 取代
  - **ApplySearchColumnLayout**：新方法，负责按 `SearchColumns` 重排可见列 + 按 `ColumnDisplayNames` 设置 HeaderText + 按 `HiddenColumns` 隐藏列 + 强制 PrimaryKey 可见 + 调整 DisplayIndex 让 0 位留给 `chk`
  - **BuildConfigInfoText** / **BuildSearchHintText**：新增并显示搜索列、隐藏列信息
- `A3Tools.Plugins.Default/Forms/GenericCopyToolForm.Designer.cs`
  - 无改动（窗体布局沿用旧逻辑）

## 关键行为

### 旧配置（SearchColumns 等三个字段全为空）
```
SELECT TOP 5000 *
FROM dbo.[<表>]
WHERE CONVERT(NVARCHAR(4000), [<主键>]) LIKE @keyword
   OR CONVERT(NVARCHAR(4000), [NAME]) LIKE @keyword
ORDER BY [<主键>]
```
DataGridView 列：全部显示，主键列标题为 `<主键>`。

### 新配置（按陛下要求）
- `SearchColumns = "GUID;CODE;SUBSYSTEMGUID;NAME;NOTES"`
- `ColumnDisplayNames = "GUID;代码;分类;名称;备注"`
- `HiddenColumns = "GUID;SUBSYSTEMGUID"`

```
SELECT TOP 5000 *
FROM dbo.[<表>]
WHERE CONVERT(NVARCHAR(4000), [CODE]) LIKE @keyword
   OR CONVERT(NVARCHAR(4000), [SUBSYSTEMGUID]) LIKE @keyword
   OR CONVERT(NVARCHAR(4000), [NAME]) LIKE @keyword
   OR CONVERT(NVARCHAR(4000), [NOTES]) LIKE @keyword
   OR CONVERT(NVARCHAR(4000), [<主键>]) LIKE @keyword
ORDER BY [<主键>]
```
DataGridView 列（按 DisplayIndex）：
1. 选择（chk）
2. CODE（标题：代码）
3. SUBSYSTEMGUID（标题：分类）
4. NAME（标题：名称）
5. NOTES（标题：备注）
6. `<主键>`（强制显示，标题 = PrimaryKey）

GUID 因为在 HiddenColumns 中强制隐藏，不显示。

## 边界处理

- `SearchColumns` 包含不存在的列名 → 跳过该列，不报错（其他列仍能搜索）
- `SearchColumns` 包含 PrimaryKey 是必要的，否则保存时校验失败
- `HiddenColumns` 中的 PrimaryKey → 强制显示（保护「添加选中」逻辑）
- `ColumnDisplayNames` 缺失项 → 用数据库原列名兜底
- 主表没有 NAME 列 → 旧行为自动跳过 NAME，不强制要求
- 配置的搜索列全部不存在 → 抛 `InvalidOperationException("未能找到任何可用于搜索的列，请检查搜索列配置。")`

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：0 错误，新增代码无 warning（168 个历史 warning 全部为旧代码）。

## 兼容性

- 旧配置文件无 `searchColumns` 字段 → JSON 序列化按空字符串处理 → 完全保留旧行为
- 旧工具按钮直接点击 → 调用 `GenericCopyToolForm` 实例化，配置空，行为不变
- 新配置只对新建/编辑的配置生效

## 后续可选

- 工具加载时校验配置：如果 `MainTable.SearchColumns` 包含的列在 db 不存在，给出警告
- 「重新加载」按钮 → 让运行时刷新主表的实际列做对比
