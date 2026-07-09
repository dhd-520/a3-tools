## A3Tools v2.3.14

### 🐛 修复
- **自动更新**：升级成功后 cmd 窗体自动关闭（不再弹黑窗）
- **自动更新**：升级成功后 `_update.log` 自动清理（升级失败保留证据）

### ✨ 新增
- **SQL 查询工具**：多结果集支持（仿 SSMS Results to Grid，每个结果集一个 sub-Tab）
- **SQL 查询工具**：大数据量流式读取 + 可中断
  - 状态栏实时更新行数（每 ~2 秒）
  - tab 标题实时显示当前已读行数
  - "停止"按钮立即中断（< 50ms 响应）
  - 列宽自适应阈值 ≤ 100 行（避免大数据集算列宽卡几秒）

### 🔧 性能优化
- **SQL 查询工具**：执行速度优化
  - 同步 `reader.Read()` 替代 `ReadAsync`（消除 N×1ms 调度开销）
  - 读完后一次性 `dgv.DataSource = dt` 绑定（不走 5万单元格逐行通知）
  - 实测 10000 行 Read+Bind 总耗时 37ms

### 📝 重要修复
- 修复了 SQL 多结果集"隔一个丢一个"的关键 bug：
  - 根因：.NET 的 `DataTable.Load(IDataReader)` 内部用 `DataAdapter.Fill`，会自动调 `NextResult()` 推进
  - 修复：手动 `reader.GetName/GetFieldType` 拿列 + `while(reader.Read)` 读行 + 外层 `NextResult` 推进
  - 验证：`SELECT 1; SELECT 2; SELECT 3` 正确返回 3 个结果集

### 📦 下载
- 自动更新已支持（应用内"帮助 → 检查更新"）
- 工具会自动从 Gitee（国内快）/ GitHub 兜底拉取最新版
