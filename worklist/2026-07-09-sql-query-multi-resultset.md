# 2026-07-09 SQL 查询工具支持多结果集（仿 SSMS Results to Grid）

## 症状

陛下 2026-07-09 反馈：SQL 查询工具一次跑多个 SELECT 或多个 GO 批，**只显示第一个结果集**，其他结果集被降级到"消息"面板的纯文本里。用户看不到其他结果集，要看必须切到消息 Tab 找纯文本输出。

期望行为：像 SSMS "Results to Grid" 那样，**每个结果集都有自己的网格 Tab 页**。

## 根因

`SqlQueryTabPage.ExecuteAsync` 中处理 `SqlDataReader.NextResult` 的循环：

```csharp
do
{
    var dt = new DataTable();
    dt.Load(reader);
    if (!hasResult)         // ← 第一个结果集
    {
        dgvResult.DataSource = dt;  // 显示在固定 dgvResult
        hasResult = true;
    }
    else                    // ← 后续所有结果集
    {
        AppendMessage($"--- 后续结果集（{dt.Rows.Count} 行 x {dt.Columns.Count} 列）---\n");
        AppendMessage(DataTableToText(dt));   // 降级成纯文本写进 Messages
    }
    affectedRows += dt.Rows.Count;
}
while (await reader.NextResultAsync(_cts.Token));
```

结构性限制：`tabResult` 里只有一个固定 `dgvResult`，只能挂一个 `DataTable.DataSource`。第二个起的结果集根本没地方显示，只能塞进 Messages。

## 修复

### 改造方案

把"tabResult 内一个固定 dgvResult"改成"tabResult 内一个 `tcResults` TabControl，每个结果集一个 sub-Tab，每个 sub-Tab 一个 DataGridView"。

仿 SSMS "Results to Grid" 行为：
- 多个 GO 批：每个批产生 N 个结果集 → N 个 sub-Tab
- 同一批内多个 NextResult：每个结果集也独立一个 sub-Tab
- 第一个 sub-Tab 自动选中 + 自动切到结果 Tab
- Tab 标题：`结果 1  ·  123 行`、`结果 2  ·  5 行`...

### 改动文件

#### 1. `SqlQueryTabPage.Designer.cs`
- **移除** `private DataGridView dgvResult;` 字段
- **新增** `private TabControl tcResults;` 容器字段
- `tabResult.Controls.Add(dgvResult)` → `tabResult.Controls.Add(tcResults)`，`tcResults.Dock = DockStyle.Fill`
- 右键菜单相关代码移除（改在运行时绑定到所有 DataGridView 上）

#### 2. `SqlQueryTabPage.cs`

**字段**：
- 新增 `private ContextMenuStrip ctxResultMenu = null!;` —— 所有结果集 DataGridView 共享一个右键菜单，绑定到 `components` 自动释放

**公共属性**：
- `ResultGrid` 改成返回 `tcResults.TabPages[0]` 里的 DataGridView（兼容旧调用），无结果集返 `null`

**`InitEditor`**：
- 初始化 `ctxResultMenu`（复制单元格 / 整行 / 全部）

**`ClearResults` 重写**：
- 不再 `dgvResult.DataSource = null`
- 遍历 `tcResults.TabPages` 反向释放（`page.Dispose()` + 子控件 `Dispose()`），避免内存泄漏

**`ExecuteAsync` 多结果集循环**：
```csharp
int resultInBatch = 0;
do
{
    var dt = new DataTable();
    dt.Load(reader);
    AddResultTable(dt, i + 1, ++resultInBatch);  // 每个结果集都进 tcResults
    hasResult = true;
    affectedRows += dt.Rows.Count;
}
while (await reader.NextResultAsync(_cts.Token));
```
（移除了"hasResult=true 后丢 Messages"的分支）

**`ExecuteAsync` 状态栏消息**：
- 多结果集成功时：`✓ 执行成功，N 个结果集 / M 行`
- 单结果集成功时：`✓ 执行成功，影响 M 行`（兼容旧样式）
- 部分批失败时：`✗ 部分批次失败（X 批 / Y 个结果集），成功 M 行`
- 成功路径切到 tabResult 而不是 tabMessages（因为结果集都进 tcResults 了，应该看结果）

**新增 `AddResultTable` / `CreateResultDataGridView` / `CurrentResultGrid`**：
- `AddResultTable(dt, batchIdx, resultInBatch)`：新建 `TabPage("结果 N · M 行")` + `DataGridView` + 加到 `tcResults`，第一个自动选中
- `CreateResultDataGridView()`：工厂方法，每次新建一个与原 `dgvResult` 同款样式的 DataGridView（白底、隔行、只读、自动列宽、共享 ctxResultMenu）
- `CurrentResultGrid`：返回 `tcResults.SelectedTab` 中的 DataGridView（复制功能用）

**复制方法（`CopySelectedCell` / `CopySelectedRow` / `CopyAllToClipboard`）**：
- 全部从"固定 `dgvResult`"改成"`CurrentResultGrid`"，多结果集时复制当前可见的那个

**`ClearAll` + `ExecuteAsync` 入口**：
- `dgvResult.DataSource = null` → `ClearResults()`（统一清空逻辑）

## 设计要点 / 教训

- **WinForm 准则里的"禁止运行时 new Button()"指的是固定布局控件**（设计器应该管的）；根据运行时数据动态创建控件（DataGridView 列、TabPage 子页、ListView Item 等）**是允许的**，本改造就是典型场景
- **`ContextMenuStrip` 关联 `components`**：运行时 `new ContextMenuStrip(components)` 能让 Dispose 自动释放，避免泄漏
- **多结果集的状态栏文案要区分单/多**：单结果集还是"影响 N 行"（熟悉），多结果集是"N 个结果集 / M 行"（明确数量）
- **Dispose 子控件**：`tcResults.TabPages.Clear()` 只清引用不释放控件，要手动 `foreach (Control c in page.Controls) c.Dispose(); page.Dispose();` 避免内存泄漏
- **共享右键菜单的 `ToolStripMenuItem.Click`**：所有 DataGridView 用同一个 ctxResultMenu 时，菜单 Click 事件要通过 `CurrentResultGrid` 获取**当前**那个 DGV 的选中行/单元格，不能捕获某个固定的 DGV 引用（否则所有 DGV 都用第一个的）

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：**0 错 261 警告**（警告全是历史的，非本次引入）。

## 后续验证建议

陛下测试用例：
1. 写一个脚本包含 2-3 个 SELECT（用 `;` 分隔在同一批），跑完看是不是出现 2-3 个 sub-Tab
2. 写一个脚本包含 3 个 GO 批，每个批一个 SELECT，跑完看是不是出现 3 个 sub-Tab
3. 切到不同的结果集 sub-Tab，验证右键复制/单元格选择/列宽都正常
4. 跑失败脚本（如 `SELECT 1/0`），看 Messages Tab 错误信息 + Results Tab 显示 `1 个结果集` 是否正确
5. 状态栏消息文案是否符合预期（"N 个结果集 / M 行" vs "影响 M 行"）

## 后续改进方向

- **大量结果集时性能**：每个 DataGridView 都 AutoSizeColumnsMode.AllCells + 隔行底色 + 全部数据加载 → 上万行时可能慢。可加"超过 N 行不加载行样式"优化
- **结果集分页**：大数据集可以加分页器（`TOP 1000` 之类）或者虚拟模式
- **导出多个结果集到 Excel**：把 N 个 DataTable 写到 Excel 不同 sheet
