# 2026-07-13 HTTP 代理模式剩余工具接入

## 陛下要求
- 源模式（直连）代码逻辑不要动
- 只有选择 HTTP 代理才走新的逻辑
- 没设置应和原来保持一致（代码和逻辑）
- 参考已改完的 4 个工具：复制数据库对象、复制 Win 表单、搜索前台、搜索后台

## 已改完的（陛下验证通过）
- 跨库复制数据库对象（CrossDbCopyTableForm）
- 跨库复制 Win 表单（CrossDbCopyFormForm）
- 搜索前台/后台表单

## 已具备的能力（不用重写）
- `ProxyHelper.CopyTableDataAsync` / `CopyTableDataByParentGuidAsync` / `CopyTableDataByKeysAsync` / `GetTableColumnsAsync`
- `ProxyHelper.IsHttp(account)` / `CreateDataAccess(account)` / `WarnIfHttp(account, formName)`
- `DirectDataAccess` / `HttpDataAccess` 两种 IDataAccess 实现
- `TableCopyService` 同步直连版本（不动）

## 待改工具（按优先级）

### 1. GenericCopyToolForm（通用复制）—— 完全没 Http 分支
**当前**：4 处直连（TestConn × 2 + GetAllColumns + GetMainRowGuid + BtnConfirm 主流程）
**方案**：
- 加 `_srcAccount` / `_tgtAccount` 字段（SelectAccount / LoadPresetAccounts 调用方不动，只在 ApplyAccountToDatabaseFields 内加 2 行存字段）
- 加 `IsHttpMode` 字段（仿 Win 表单 Line 83-85）
- BtnSearch_Click：Http 分支走 `ProxyHelper.ExecuteQueryToDataTableAsync(srcDA, sql)`，GetAllColumns 走 Http 版本
- BtnConfirm_Click：Http 分支走 `BtnConfirmHttpAsync`，独立方法里用 `ProxyHelper.CopyTableDataAsync` + `CopyTableDataByParentGuidAsync`
- 直连代码 100% 不动
- **状态**：✅ 完成（Build: 0 错）

### 2. CrossDbCopyConfigDataForm（跨库复制配置数据）—— 3 处直连
- 已有 2 个 IsHttp 分支点，剩 3 处直连（Line 241/348/380）
- 仿 Win 表单在直连代码前面加 Http 分支
- **状态**：✅ 完成（Build: 0 错）。BtnSearch_Click 和 BtnFindMissing_Click 两个流程加 Http 分支。复用现有 BuildTempAccount 自动检测 Http 模式。独立 BtnSearchHttpAsync + BtnFindMissingHttpAsync 方法。

### 3. CrossDbCopyObjectLinkForm / ReportForm / WebObjectForm / AppChartForm —— 各 1 处直连
- 各加 1 个 Http 分支（按 SearchBackendForm 模板）
- 工程量小
- **状态**：✅ 全部完成（Build: 0 错）。每个工具加 IsHttpMode 字段 + ApplyAccountToDatabaseFields 加 1 行保存 _srcAccount/_tgtAccount（Win 表单已改完模板）+ BtnSearch_Click 改 async void 加 Http 分支 + 独立 BtnSearchHttpAsync 方法。

## 修改原则
- 每个直连代码前面加 `if (ProxyHelper.IsHttp(...)) { Http 分支 return; }`
- 直连代码原样保留在 else 分支或后面
- 新增的 Http 模式方法独立命名（`*HttpAsync` 后缀，仿 Win 表单）

## 最终结果（2026-07-13）
- ✅ 6 个工具全部接入 HTTP 代理模式
- ✅ A3Tools.Plugins.Default Build: 0 错 0 警告
- ✅ A3Tools 主项目 Build: 0 错（19 个历史警告，与本次无关）
- ✅ 陛下原则遵守：直连代码 100% 不动，Http 模式是独立方法（`*HttpAsync` 后缀）

## 后续补丁（2026-07-13）
- ✅ 修复 Http 模式重复列名报错：ProxyHelper.ExecuteQueryToDataTableAsync 对齐 SqlDataAdapter.Fill 行为，遇到重复列名自动加 _2/_3 后缀（修复 SearchBackendForm "业务分组代码" 重复列报错）。Build: 0 错。
- ✅ 移除复制 APP 表单 Http 模式搜索提示弹窗：CrossDbCopyAppFormForm.BtnSearch_Click Line 516-523 删除。Build: 0 错。