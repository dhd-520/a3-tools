# 2026-07-11 对比表结构子窗体迁移 Http 代理模式

## 背景
上回（2026-07-11 ToolsV1 `050d129`）迁移了 6 个跨库复制主工具到 Http 代理模式，但 `CompareTablesForm`（跨库复制 → 对比表结构 → 弹出的子窗体）漏掉了。
Http 模式下打开子窗体会直接卡死：`SqlConnection.Open()` 在不存在的服务器上无超时机制，必须等到 OS TCP 超时（默认 21 秒）。

## 设计
沿用 `ProxyHelper` 统一抽象，所有 DB 调用通过 `IDataAccess`：
- `ProxyHelper.CreateDataAccess(account)` 创建直连或 Http 实例
- `ProxyHelper.ExecuteQueryToDataTableAsync(da, sql)` 查询
- `ProxyHelper.ExecuteNonQueryAsync(da, sql)` 执行 DDL

CompareTablesForm 构造函数从 `(srcServer, srcDbName, srcUser, srcPwd, tgtServer, tgtDbName, tgtUser, tgtPwd, tables)` 改为 `(Account? srcAccount, Account? tgtAccount, List<string> tables)`。
父窗体 CrossDbCopyTableForm 负责把输入框 + `_srcAccount`/`_tgtAccount` 合并成临时 Account，Http 配置按 server+dbName 精确继承。

## 改动文件（2 个）

### A3Tools.Plugins.Default/Forms/CompareTablesForm.cs
- 删除字段：`_srcServer/_srcDbName/_srcUser/_srcPassword/_tgtServer/_tgtDbName/_tgtUser/_tgtPassword`
- 新增字段：`_srcAccount/_tgtAccount`（Account?）
- 删除方法：`BuildConnString`、`GetTableColumns(SqlConnection)`、`ExecuteScripts`（同步）、`StartCompare`（同步 Task.Run）
- 新增方法：`GetTableColumnsAsync(IDataAccess, string)`、`ExecuteScriptsAsync(List<string>)`、`StartCompare` 改为 async void
- `StartCompare` 用 `ProxyHelper.CreateDataAccess(_srcAccount/_tgtAccount)` 创建 IDataAccess
- SQL 用 `ProxyHelper.EscapeSql()` 转义表名（Http 模式不支持参数化）
- 确认执行弹窗带模式提示："目标库：xxx/yyy （Http 代理）" 或 "（直连）"

### A3Tools.Plugins.Default/Forms/CrossDbCopyTableForm.cs
- 新增方法：`BuildTempAccount(server, dbName, user, password, isSource)` —— 输入框 → 临时 Account，自动从源/目标账套继承 Http 配置（与 CrossDbCopyAppChartForm 等迁移工具一致）
- `BtnCompareTables_Click` 调用 `BuildTempAccount` 构造 src/tgt 临时 Account 后传给 CompareTablesForm

## 关键决策
- **构造函数签名换掉**：CompareTablesForm 只被 CrossDbCopyTableForm 一处调用，没必要保留旧 API
- **Http 模式 SQL 不用参数化**：A3ToolsHub 走 SQL 字符串拼接，所以表名走 `ProxyHelper.EscapeSql` 转义
- **执行脚本按 IDataAccess 走**：直连保持原 ExecuteNonQuery 行为；Http 模式由服务端 SqlCommand 执行，效果一致

## 验证
- `dotnet build A3Tools.sln`：0 错，319 警告（全部是项目原有 CS8632 nullable 警告，与本次无关）
- 仅修改 2 个文件，~80 行净增（多为 Http 模式错误处理）
- Http 模式对比卡死问题解决：走 IDataAccess，错误秒级返回
- 直连模式行为不变：ProxyHelper.CreateDataAccess 在 ConnectionMode=Direct 时仍返回 DirectDataAccess

## 提交
- 分支：`ToolsV1`
- Commit：`feat(compare): 对比表结构子窗体迁移 Http 代理模式`（待推送）
- 改动：2 文件