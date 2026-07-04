# 2026-07-04 SQL IntelliSense 数据库对象联想

## 目标

把 IntelliSense 从纯关键字扩展为**对象 + 列 + 别名**三栖提示，仿 SSMS 体验：

1. 关键字 + 系统函数（已有）
2. **表 / 视图 / 表值函数 / 标量函数** 名（按 schema 限定）
3. **列名**（输入 `表名.` / `别名.` / `schema.表名.` 时）
4. **别名**：`FROM Tbl A`, 在 `SELECT A.` 时弹 Tbl 的列

## 文件改动

### 🆕 新增

| 文件 | 作用 |
|------|------|
| `SqlObjectSchemaCache.cs` | 全局静态缓存服务：`(server, db)` 维度缓存"表/视图/函数+列名"。`WarmupAsync` 后台预热 + `GetObjectSuggestions` schema-aware + `GetColumnSuggestions` 列名查询（SQL: `sys.objects JOIN sys.schemas WHERE type IN ('U','V','IF','TF','FN')`，用 `STRING_AGG` 拼列） |
| `SqlAliasResolver.cs` | 解析 SQL 找 `FROM/JOIN` 后面的 `表/函数 AS 别名` 映射（支持 `[schema].[table] [AS] alias` / `表 别名(col1, col2)` TVF 风格 / `INNER/LEFT/RIGHT/FULL/CROSS JOIN` 五种连接前缀），同时提供 `StripBrackets` / `SplitObj` 公共辅助方法 |

### 🔧 改动

| 文件 | 改动 |
|------|------|
| `SqlIntelliSenseProvider.cs` | `Filter(prefix)` 改名 `GetSuggestions(prefix, connStr, fullSql, caretOffset, max)`，新增 `TryGetColumnSuggestion` 私有方法做"列名联想"分支（先查 alias map，再 fallback 到裸对象 / 全限定名） |
| `SqlEditor.cs` | 新增 `ConnectionString` 属性；`TriggerIntelliSense` 把当前 `Text` + caret 一起传给 Provider；popup 大小略增（320x280） |
| `SqlQueryForm.cs` | 5 处改动：构造函数预热缓存 + `LoadDatabasesAsync` finally 同步 ConnectionString & 预热 + `CmbDatabase_SelectedIndexChanged` 预热 + `NewTab` 同步编辑器 ConnectionString + `SyncEditorConnectionStrings` 私有方法 |

## 关键设计

### 1. 缓存一致性
- 缓存按 `(server, db)` 隔离 → 切账套不串
- 2 分钟 stale → 隔很久打开自动刷新
- 并发去重 → 切库时若还在加载中重复触发，回来只等结果不重跑
- 后台预热 → 不阻塞 UI（用户在编辑器打字时大概率已完成）

### 2. Schema 限定识别
- `SELE` → 关键字 + 所有 schema 下 `Se*` 对象
- `dbo.Se` → 关键字 + 仅 dbo 下 `Se*` 对象  
- `dbo.` → 关键字 + 仅 dbo 下所有对象
- `Sales.X` → `Sales` 当 schema，弹 Sales 下 `X*` 对象

### 3. 列名联想识别
- `A.` → A 是别名 → 弹 A 对应对象的列
- `A.N` → A 是别名 → 弹 A 对应对象的 `N*` 列
- `Customer.` → Customer 是表/视图 → 弹列
- `Customer.N` → 同上
- `dbo.Customer.N` → 全限定名查对象 → 弹列
- miss 时 fallback 把 leftPart 当裸对象名再查

### 4. 优雅降级
- 缓存拉取失败 → 返回空列表 → UI 只显示关键字
- AliasResolver 解析失败 → 返回空 map → UI 退化为对象联想
- 表无权限 → 列缓存为空 → 自动跳到下一段逻辑

### 5. 性能考虑（潜在风险）
- 每按一键触发 `TriggerIntelliSense` → `SqlAliasResolver.Parse(Text)` 跑全文本正则
- 10K 字符 SQL 也只是几次 `regex.Matches`（O(n)），但**未做 hash 缓存** → 若陛下测出卡顿再加

## 验证

- `dotnet build`：0 错误
- 输入 `SELECT * FROM S_SCM_SEORDER A,` 后在 `A.` 处触发 → 弹 A 对应对象列名
- 输入 `SELECT A.` 在最普通 SELECT 查询里 → 弹 A 别名对应对象的列
- 输入 `Customer.` → 弹 Customer 表的列
- 输入 `[dbo].Customer.N` → 弹 dbo.Customer 表的 N* 列

## 下一步（可选）

- 列名联想 cache 失效优化（加 `(text hash → alias map)` 缓存，避免每按键全文本正则）
- 候选项加 icon 区分：关键字 / 表 / 视图 / TVF / 标量函数（用 ListView + imageList 替 ListBox；或 ListBox DrawItem 绘 emoji）
- `Ctrl+J` 强制展开
