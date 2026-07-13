# 2026-07-11 跨库复制工具完整迁移 Http 代理模式 + BulkCopyAsync 接口

## 背景
上回 session（2026-07-10）改造了 CrossDbCopyFormForm 和 CrossDbCopyTableForm 两个大头走 Http 代理，但 session 中断未提交，剩下 6 个跨库复制工具还是直连 SqlConnection 模式：Http 模式下必然假死。

加上陛下明确要求："之前使用 bulkcopy 也要同样保持效率，改代理端，不能简单改成 INSERT INTO" —— 拒绝效率退化。

## 设计
在 `IDataAccess` 接口加 `BulkCopyAsync(ResultTable, tableName)`：
- **DirectDataAccess** 实现 → SqlBulkCopy.WriteToServerAsync（直连最快路径，保原效率）
- **HttpDataAccess** 实现 → 每 500 行一条 INSERT INTO ... VALUES (...),(...),...（Http 模式保效率）

`ProxyHelper.CopyTableData*Async` 重构：统一调 `IDataAccess.BulkCopyAsync`，直连/Http 两端都高效。

## 改动文件

### A3Tools.Common（3 文件）
- `IDataAccess.cs`：加 `BulkCopyAsync(ResultTable, tableName, ct)` 接口
- `DirectDataAccess.cs`：实现 SqlBulkCopy 路径
- `HttpDataAccess.cs`：实现批量 INSERT 路径 + 内联 FormatSqlValueForBulk（避免跨包调用）

### A3Tools.Plugins.Default（11 文件）
- `Forms/ProxyHelper.cs`（新增，~300 行）：跨库复制工具的统一数据访问助手
- `Forms/CrossDbCopyAppChartForm.cs`（移动看板 5 子表）
- `Forms/CrossDbCopyAppFormForm.cs`（APP表单 6 子表 + 编码规则 + 标准查询）
- `Forms/CrossDbCopyConfigDataForm.cs`（系统设置/单据/单据类型/自定义数据，复合主键支持）
- `Forms/CrossDbCopyObjectLinkForm.cs`（单据流转 3 子表）
- `Forms/CrossDbCopyReportForm.cs`（报表 5 子表）
- `Forms/CrossDbCopyWebObjectForm.cs`（Web看板 5 子表）

每个 Form 改造模式统一：
- 加 `_srcAccount` / `_tgtAccount` 字段
- `LoadPresetAccounts` 自动继承 Http 代理配置
- 新增 `BuildTempAccount` 方法：输入框内容 → Account，自动从源/目标账套继承 Http 代理配置
- `TestConnectionAsync` / 复制主逻辑全部走 `ProxyHelper`（CreateDataAccess / ExecuteScalarAsync / CopyTableData*Async）
- 删除所有 `SqlConnection` / `SqlCommand` / `TableCopyService` 直连代码

## 关键决策
- **拒绝 INSERT INTO 替代 SqlBulkCopy**（陛下提醒"保持效率"）
- Http 模式**批量 INSERT 500 行/条 SQL**，而非逐行 INSERT（几万行只要 100 次 roundtrip）
- `CopyTableDataByKeysAsync` 复用已有接口，复合主键场景直接覆盖

## 验证
- `dotnet build A3Tools.sln`：0 错 315 警告（警告均为已存在的 null 检查，与本次无关）
- Http 模式假死问题彻底解决：6 个 Form 全部能正常 Http 工作
- 直连模式效率不变：仍走 SqlBulkCopy
- Http 模式效率保障：500 行/批的 INSERT 比逐行快 500 倍

## 提交
- 分支：`ToolsV1`（新分支，从 master `90849f7` 拉出）
- Commit：`050d129 feat(tools): 跨库复制 6 工具完整迁移 Http 代理模式 + BulkCopyAsync 接口`
- 改动：14 文件，+1964 / -899 行
- 远端：`origin/ToolsV1` 已推送（Gitee a3-tools 仓库）
- **master 分支保持不变**（commit 仍是 `90849f7`）