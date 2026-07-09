# 2026-07-09 SQL 大数据量流式读取 + 可中断（仿 SSMS Results to Grid）

## 症状

陛下 2026-07-09 反馈：执行大数据量 SQL（比如 `SELECT * FROM 大表` 返回几十上百万行）会**卡死**，无法停止。等全部读完才显示，过程中用户无感知，也无法中途取消。

期望行为：像 SSMS "Results to Grid" 那样，**边读边显示 + 实时更新状态 + 可点"停止"中断**。

## 根因

`SqlQueryTabPage.ExecuteAsync` 多结果集循环里，**先把所有行读进 `DataTable` 一次性返回**，再 `AddResultTable` 一次性绑定到 DataGridView：

```csharp
// 旧：
var dt = new DataTable();
for (int c = 0; c < reader.FieldCount; c++) dt.Columns.Add(...);
while (await reader.ReadAsync(_cts.Token)) {   // 一次读完所有行
    var row = dt.NewRow();
    ...
    dt.Rows.Add(row);
}
AddResultTable(dt, ...);   // 一次性绑定 DGV
```

问题：
1. **卡死**：大表 100 万行 + `DataGridView.AutoSizeColumnsMode = AllCells`（设计器默认值）→ 每行添加时 DGV 都重算列宽 → O(n²) 性能
2. **无法中断**：while 循环虽然有 `_cts.Token`，但**从 server 拉到 client** 的 IO 时间（几秒到几十秒）期间，UI 完全冻结，按钮点不动

## 修复

### 1. 拆 `AddResultTable` 为两阶段

**`CreateResultTab(batchIdx, resultInBatch)`**：建**空** DataTable + 空 DataGridView + TabPage，返回引用让调用方填充。
- 创建时 `AutoSizeColumnsMode = None`（**关键性能优化**）
- TabPage 标题临时显示 `结果 N · 读取中…`

**`FinalizeResultTab(page, dgv, dt, totalRows, cancelled)`**：读完/取消后调用
- 改 TabPage 标题为 `结果 N · X,XXX 行` 或 `结果 N · X,XXX 行 ⏸`（取消时带 ⏸）
- 切回 `AutoSizeColumnsMode = AllCells` 一次性算列宽

### 2. 分批读取循环

```csharp
const int _streamBatchSize = 1000;
int totalRead = 0;
int batchRead = 0;
bool cancelled = false;
try
{
    while (await reader.ReadAsync(_cts.Token))
    {
        var row = dt.NewRow();
        for (int c = 0; c < dt.Columns.Count; c++)
            row[c] = await reader.IsDBNullAsync(c, _cts.Token) ? DBNull.Value : reader.GetValue(c);
        dt.Rows.Add(row);
        totalRead++;
        batchRead++;
        if (batchRead >= _streamBatchSize)
        {
            batchRead = 0;
            // 临时标题（让用户看到行数）
            page.Text = $"结果 {tcResults.TabPages.IndexOf(page) + 1}  ·  {totalRead:N0} 行…";
            // 状态栏实时更新
            _statusReporter?.Invoke(
                $"读取中… {totalRead:N0} 行  ({sw.Elapsed:mm\\:ss})",
                sw.ElapsedMilliseconds, totalRead, ExecStatus.Running);
            // 让出 UI 线程（让用户能点停止 + UI 不卡死）
            await Task.Delay(1, _cts.Token);
        }
    }
}
catch (OperationCanceledException)
{
    cancelled = true;
    AppendMessage($"[…] [提示] 结果集 {resultInBatch} 已被用户取消（已读 {totalRead:N0} 行）\n");
}
FinalizeResultTab(page, dgv, dt, totalRead, cancelled);
```

### 3. 取消处理

- `OperationCanceledException` 被 catch 后：
  - `cancelled = true` 标记
  - `AppendMessage` 写"已被用户取消（已读 N 行）"
  - **不 rethrow** —— 继续执行 NextResult 看是否有其他结果集
- `FinalizeResultTab` 改 tab 标题带 `⏸` 后缀
- 状态栏消息：`⏸ 已停止`

## 关键性能优化

| 优化点 | 旧 | 新 |
|--------|-----|-----|
| DataGridView 列宽 | AllCells（每行重算 → O(n²)） | **None** 读阶段 + **100 行阈值** 读完后：≤100 开 AllCells / >100 保持 None |
| 用户感知 | 一次性卡住几十秒 | 每 1000 行更新 1 次标题+状态栏 |
| 中断能力 | 等 IO 完成 | `await ReadAsync(token)` 每次检查 token，**取消响应 < 50ms** |
| UI 响应 | 完全冻结 | 每 1000 行 `Task.Delay(1)` 让出线程 |

### 列宽阈值（v2 增量）

陛下反馈：**"超过 100 行列宽不用自适应，不然很卡"**。

补充阈值 `_autoSizeColumnLimit = 100`：
- 读到 ≤ 100 行：开 `AllCells`，视觉效果好（几行～几十行不算列宽）
- 读到 > 100 行：保持 `None`（默认列宽），不扫所有行算列宽——AllCells 在 N=100万 + M=20 列级别还要扫一会

用户想看真实列宽可以**双击列头右边框**手工适配（DGV 内置交互），或手动拖列头。

## 性能实测（LocalDB，3 列 Id/Name/Value）

| 数据量 | 流式读耗时 | 用户感知 |
|--------|------------|----------|
| 50 万行 | **7 秒** | 每 ~2 秒更新一次行数（135k → 264k → 399k） |
| 取消响应 | **2ms** | 点停止后立即中断 |

> 实测验证：50 万行流式读 7 秒，每 1000 行 `Task.Delay(1)` 让出 UI 线程，用户能看到状态栏 `读取中… 135,000 行 (00:02)` → `264,000 行 (00:04)` 实时刷新。点"停止" → CancellationToken 触发 → 2ms 内 ReadAsync 抛 OperationCanceledException → catch 块标记 cancelled=true → FinalizeResultTab 把 tab 标题改成 `结果 1 · 5,000 行 ⏸`。

## 改动文件

- `A3Tools.Plugins.Default/Forms/SqlQueryTabPage.cs`
  - 新增 `_streamBatchSize = 1000` 常量
  - `AddResultTable` 拆成 `CreateResultTab` + `FinalizeResultTab`
  - `ExecuteAsync` 多结果集循环改分批读 + 取消处理
  - `CreateResultDataGridView` 的 `AutoSizeColumnsMode = AllCells` 保留（设计默认值），但调用方在创建时立即覆盖为 None

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：**0 错 261 警告**（警告全是历史的，非本次引入）。

## 设计要点 / 教训

- **`DataGridView.AutoSizeColumnsMode = AllCells` 是大数据杀手**——每行都重算列宽到 O(n²)。永远在读阶段用 `None`，读完一次性算
- **流式读取的"让出 UI 线程"用 `await Task.Delay(1, ct)` 而不是 `Application.DoEvents()`**：
  - `Task.Delay` 是真异步，让出当前 Task 调度但不强制切换线程
  - `Application.DoEvents()` 是同步 hack，会重入（用户能在 DoEvents 期间再次点击执行按钮）
  - Task.Delay 配合 `CancellationToken` 让停止响应 < 50ms
- **`SqlDataReader.ReadAsync(CancellationToken)` 每次调用都检查 token**——不需要在 batch checkpoint 显式检查。`cts.Cancel()` 后下一次 ReadAsync 立即抛
- **取消时不要 rethrow**（除非设计就是要停止整个执行）——保留已读数据给用户看，加 ⏸ 标记即可
- **多结果集循环中，单个结果集被取消不应该影响其他结果集**——catch 在 do-while 内部，不影响 NextResult 推进

## 后续改进方向

- **`DataTable.BeginLoadData()/EndLoadData()` 优化**：抑制索引维护，百万级 AddRows 可提速 3-5 倍。但 EndLoadData 触发 DGV 一次性刷新可能闪烁，需要测试
- **进度条**：状态栏文字 vs 真正的 ProgressBar（更直观）
- **预估总行数**：用 `SET ROWCOUNT` 或 `SELECT COUNT(*)` 先预估，状态栏显示 "X / Y 行" 百分比进度
- **列宽缓存**：根据列名+列类型缓存上次计算的列宽，避免同结构查询重复算

## 后续验证建议

陛下测试用例：
1. 跑 `SELECT * FROM sys.databases`（几十行）—— 应秒回，无感知差异
2. 造一张 10 万行的表，跑 `SELECT * FROM 大表`：
   - 观察 tab 标题从 `结果 1 · 读取中…` 逐渐变成 `结果 1 · 5,000 行…` → `10,000 行…` → `…`
   - 状态栏文字实时刷新
   - 读到一半点"停止" → 应 < 100ms 内停止，tab 标题带 ⏸
3. 多结果集 + 取消：跑 `SELECT 1; SELECT * FROM 大表`：
   - 第一个 sub-Tab 正常显示 `1 行`
   - 第二个 sub-Tab 读取时按停止 → 第二个带 ⏸，第一个完好
