# 2026-07-04 对象资源管理器（仿 SSMS）

## 目标

仿 SSMS 在 SQL 查询窗体右侧加可隐藏的"对象资源管理器"：
- 5+1 类对象（表/视图/TVF/标量函数/存储过程/触发器）
- 树状展示（Schema → 对象 → 列）
- 类型筛选（CheckBox）+ 名称筛选（实时高亮）
- 双击对象 → 加载 CREATE 脚本进新 Tab
- 与父窗体生命周期同步（关闭父窗体 → Explorer 一起关）

## 改动文件

### 🆕 新增

| 文件 | 作用 |
|------|------|
| `ObjectExplorerForm.cs` | 主体：TreeView (Schema/Object/Column 三层) + ToolStrip (6 CheckBox) + Refresh 按钮 + Filter 输入框 + 状态栏；Designer 用 placeholder 色块图 (7 key) |
| `ObjectExplorerForm.Designer.cs` | Designer：FlowLayoutPanel 包 6 个 CheckBox + TextBox + TreeView + Button + Label + ImageList |

### 🔧 改动

| 文件 | 改动 |
|------|------|
| `SqlObjectSchemaCache.cs` | 加 `StoredProcedure` (`P`) / `Trigger` (`TR`) 两种 ObjectKind；SQL `WHERE o.type IN ('U','V','IF','TF','FN','P','TR')`（之前不含 P/TR）；新增 `GetObjectsByKind(connStr, kinds)` 公共 API |
| `SqlScriptLoader.cs` | `LoadCreateScriptAsync` 拆分 `schema.name` → 按 schema 精确过滤，避免重名对象查错；同时让 ObjectExplorer 可直接传 `dbo.TableName` |
| `SqlQueryForm.cs` | 新增 `_explorer` 单例 + `_explorerVisible` toggle + `_explorerUserClosed` 标志 + `BtnToggleExplorer_Click` + `OnFormClosed` 关闭 explorer；切库 / LoadDatabasesAsync finally 自动 refresh explorer |
| `SqlQueryForm.Designer.cs` | 工具栏新增 `btnToggleExplorer`（"📂 对象资源管理器"按钮） |

## 关键设计

### 1. 树节点分层 + Tag
- Schema 节点: `Tag = ("schema", "dbo")`
- 对象节点: `Tag = ("object", "U|dbo.S_SCM_SEORDER")` —— type 字符 + 全限定名，pipe 分隔
- 列节点: `Tag = ("column", "BILLNO")` —— 不参与双击穿透

### 2. 双击穿透
- TreeNodeMouseDoubleClick 提取 pipe 前的 type 和 pipe 后的 fullName
- 调 `_owner.OpenScript(database: "", objType, objName)` 走已有穿透接口
- 数据库切不切由当前下拉决定（如果对象在非当前库，下拉选择应已对上）

### 3. 单例 + 生命周期
- 主窗体关闭 → explorer 一起 Close+Dispose
- explorer 自己点 × → 标记 _explorerUserClosed = true，按钮再开就重新 new 一个
- explorer 重新打开 → Show + Refresh + 同步 `_explorerVisible` 字段

### 4. 实时筛选
- txtFilter TextChanged → ClearHighlights（清之前黄色）→ 递归高亮匹配列节点
- CheckBox 触发 RebuildTree（直接 RebuildSelectedKinds + GetObjectsByKind 重新填树）

### 5. 图标占位（占色块）
- Designer 里画 16x16 的纯色矩形 + 灰边（schema=灰 / table=蓝 / view=紫 / tvf=橙 / fn=红 / proc=绿 / trigger=紫红 / column=灰）
- 后续找图标资源替换直接改 ImageList.Images.Add(key, ico) 一行

### 6. Schema 不重复查
- 一个 SQL 拉所有类型 + 列（sys.columns 的 STRING_AGG 同时拼）
- 单连 IO → 0.1-0.5s 完成

## 验证

- `dotnet build`：0 错误
- 点工具栏"📂 对象资源管理器" → 右侧弹出 Explorer
- 顶部输入 `S_SCM` → 列名高亮黄色
- 双击 `Sales.S_SCM_SEORDER` → 新 Tab 显示 CREATE TABLE/脚本
- 切库（数据库下拉） → Explorer 树自动刷新
- 关闭 Explorer × → 按钮变回"打开"文字
- 关闭主窗体 → Explorer 也跟着关

## 下一步（可选）

- 替换占位图标为真实图标（A3Tools/Icons 或新加）
- 对象节点右键菜单：复制名 / 查看依赖 / 用作模板
- 列节点双击 → 复制列名到剪贴板
- 拖拽对象名到编辑器（SSMS 风格）
