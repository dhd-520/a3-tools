# 2026-07-03 SQL 查询工具

## 需求

陛下想做一个轻量级 SQL 查询工具，替代 SSMS 常用功能（SSMS 启动太慢）。

- **手动 SQL 编辑器**：类似 SSMS 的查询窗口
- **复用账套连接**：工具箱选中的源账套，未选择提示并无法打开
- **多 Tab**：像 SSMS 一样可以打开多个查询 Tab
- **显示当前连接对象**：顶栏显示账套、服务器、当前数据库
- **支持切换查询数据库**：工具内下拉切换 USE 库
- **预留穿透接口**：从「复制数据库对象」双击函数/存储过程 → 类似 SSMS 的「右键 ALTER 脚本」

## 关键选型（陛下确认）

- **SQL 编辑器**：ScintillaNET 5.2.2（接近 SSMS 体验，行号/语法高亮/当前行高亮）
- **多 Tab 策略**：按账套单实例（账套 A 一个窗体、账套 B 一个，任务栏切换）

## 文件结构

```
A3Tools.Plugins.Default/
├── A3Tools.Plugins.Default.csproj  # +ScintillaNET 包
├── DefaultTools.cs                  # +SqlQueryTool
└── Forms/
    ├── SqlQueryForm.cs              # 主窗体（多Tab容器 + 顶栏 + 状态栏）
    ├── SqlQueryForm.Designer.cs     # Designer
    ├── SqlQueryTabPage.cs           # TabPage 用户控件（编辑器+结果+消息）
    ├── SqlQueryTabPage.Designer.cs  # Designer
    └── SqlScriptLoader.cs           # 加载对象 CREATE 脚本（穿透接口）
```

## 主体类

### SqlQueryTool

- 入口：在 DefaultTools.cs 加一个 Tool 类
- 单实例字典：`Dictionary<string accountKey, SqlQueryForm>`
- 按 `account.Code | account.Database | account.DatabaseName` 隔离
- 验证账套完整性（Database + DatabaseName + DbUser 都非空）

### SqlQueryForm（主窗体）

```
┌──────────────────────────────────────────────────────┐
│ 账套: A3-test │ 服务器: 192.168.1.100 │ 当前库: [master ▼] [刷新] [断开] │
├──────────────────────────────────────────────────────┤
│ [+ 新建查询] [× 关闭当前] [× 关闭其他] [⚙ 设置]                       │
├──────────────────────────────────────────────────────┤
│ [Tab1: 查询1*] [Tab2: sp_my_proc] [Tab3 +]                            │
│ ┌─────────────────────────┬──────────────────────┐                   │
│ │ [▶ F5] [▶ 选中 Ctrl+F5]  │ [结果] [消息]                              │
│ │                          │                                          │
│ │   Scintilla 编辑器        │  DataGridView                             │
│ │   - 行号                  │  (列宽自动 / 右键复制)                     │
│ │   - SQL 关键字高亮         │                                          │
│ │   - 当前行高亮            │                                          │
│ │                          │                                          │
│ └─────────────────────────┴──────────────────────┘                   │
├──────────────────────────────────────────────────────┤
│ 状态: 就绪 │ 耗时: 123ms │ 影响: 5 行                                  │
└──────────────────────────────────────────────────────┘
```

### SqlQueryTabPage（用户控件）

- 内部布局：SplitContainer 上下分（编辑器 / 结果+消息）
- 上：Scintilla 编辑器
- 下：TabControl [结果 / 消息]
- Tab 标题：默认 `查询N`，改过显示 `*`，右键菜单【重命名 / 关闭 / 关闭其他】

### SqlScriptLoader（穿透接口占位）

```csharp
public static class SqlScriptLoader
{
    /// <summary>
    /// 加载对象的 CREATE 脚本（存储过程/函数/视图/触发器）。
    /// 后期由穿透调用方传入 connection 和对象名，本期只搭接口。
    /// </summary>
    public static Task<string?> LoadCreateScriptAsync(string connStr, string objType, string objName);
}
```

### 穿透入口（SqlQueryForm 公开方法）

```csharp
public void OpenScript(string database, string objType, string objName)
{
    var script = SqlScriptLoader.LoadCreateScriptAsync(_currentConnStr, objType, objName).Result;
    var tab = NewTab($"{objType}.{objName}", script ?? $"-- 加载 {objType}.{objName} 失败");
    _tabControl.SelectedTab = tab;
}
```

## 连接管理

- **原始连接串**：从 `account.Database + account.DatabaseName + account.DbUser + account.DbPassword` 构造（DbPassword 走解密后的 `DbPasswordDecrypted`）
- **数据库切换**：用 `SqlConnectionStringBuilder { InitialCatalog = newDb }` 重置连接串，**不重连**，等下次执行时按当前串打开
- **执行命令超时**：`CommandTimeout = 0`（不超时，符合 SSMS 习惯）
- **异步执行**：`await conn.OpenAsync()` + `await cmd.ExecuteReaderAsync()`，避免 UI 卡死

## 快捷键

- `F5`：执行当前 Tab 所有 SQL
- `Ctrl+F5`：执行选中
- `Ctrl+N`：新建查询 Tab
- `Ctrl+W`：关闭当前 Tab
- `Ctrl+Tab`：下一个 Tab

## 顶栏「刷新」/「断开」

- **刷新**：重新查询 sys.databases 更新下拉
- **断开**：清空当前 Tab 的结果集 + 消息面板

## 错误处理

- 连接失败 → 消息面板显示 SqlException 详情，状态栏「连接失败」
- SQL 错误 → 消息面板显示 line number + message，状态栏「执行失败」
- 多结果集 → 第一个填 DataGridView，后续追加到消息面板

## 工具箱注册

```json
{
  "name": "SQL查询",
  "description": "SQL 查询编辑器（替代 SSMS 常用功能）",
  "library": "A3Tools.Plugins.Default.dll",
  "className": "A3Tools.Plugins.Default.SqlQueryTool",
  "methodName": "Execute",
  "enabled": true,
  "icon": "",
  "category": "查询"
}
```

## 验证

```powershell
dotnet build D:\work\A3Tools\A3Tools.sln -c Debug --nologo
```

期望：0 错误（新增代码无 warning；可能引入 ScintillaNET 自身 warning 但无关紧要）。

## 后续扩展（本期不做）

- [ ] 「复制数据库对象」搜索结果行双击 → 调 `SqlQueryForm.OpenScript(db, objType, objName)`
- [ ] Tab 内容持久化到 `DATA\sql-history\{accountKey}.json`（按账套保存，下次打开恢复）
- [ ] SQL 片段收藏（Favorites）
- [ ] 查询结果导出 CSV / Excel
- [ ] 关键字 snippet（`ssf` + Tab = `SELECT * FROM `）