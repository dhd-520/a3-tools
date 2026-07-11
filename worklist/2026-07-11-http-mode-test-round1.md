# 2026-07-11 Http 代理模式测试第一轮结果

## 测试范围
A3Tools 工具箱全部 11 个工具，在 Http 代理账套下的可用性手动测试。

## 测试结果

### ✅ Http 代理模式正常工作（4 个）

| 工具名 | 实现类 | 实现 Form | 备注 |
|--------|--------|-----------|------|
| 复制数据库对象 | `CrossDbCopyTableTool` | `CrossDbCopyTableForm.cs` | 最早迁移，commit `90849f7` |
| 复制Win表单 | `CrossDbCopyWinFormTool` | `CrossDbCopyFormForm.cs` | 最早迁移，commit `90849f7` |
| 搜索后台表单 | `SearchBackendFormTool` | `SearchBackendForm.cs` | 已有 `IsHttp` 分支处理 |
| 搜索前台菜单 | `SearchFrontendMenuTool` | `SearchFrontendMenuForm.cs` | 推断（用户说"前台表单"，疑为口误） |

### ❌ Http 代理模式异常（7 个）

| 工具名 | 实现类 | 实现 Form | 状态 |
|--------|--------|-----------|------|
| 复制APP表单 | `CrossDbCopyAppFormTool` | `CrossDbCopyAppFormForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| 复制WEB看板 | `CrossDbCopyWebObjectTool` | `CrossDbCopyWebObjectForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| 复制报表 | `CrossDbCopyReportTool` | `CrossDbCopyReportForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| 复制单据流转 | `CrossDbCopyObjectLinkTool` | `CrossDbCopyObjectLinkForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| 复制配置数据 | `CrossDbCopyConfigDataTool` | `CrossDbCopyConfigDataForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| 复制移动看板 | `CrossDbCopyAppChartTool` | `CrossDbCopyAppChartForm.cs` | 已迁移（commit `050d129`），但测试异常 |
| SQL查询 | `SqlQueryTool` | `SqlQueryForm.cs` | 已迁移（commit `51f7db4`），但测试异常 |

## 关键观察

- **"已迁移≠可用"**：commit `050d129` 涉及的 6 个 Form 代码确实都已改用 `ProxyHelper`，但运行时仍有异常
- **早期迁移的 2 个反而能用**：`90849f7` commit 阶段的 `CrossDbCopyTableForm`（复制数据库对象）和 `CrossDbCopyFormForm`（复制Win表单）完全可用
- **可能根因候选**（下周验证）：
  1. **`BuildTempAccount` 助手逻辑差异**：陛下排查代码时的具体行为？
  2. **子窗体未迁移**：例如复制 APP 表单里的"标准查询"对话框仍用 `SqlConnection`？
  3. **批量插入 500 行的边界**：BulkCopy 特定行数/类型下崩溃？
  4. **Http 模式 SQL 转义遗漏**：某些 `WHERE` 条件未走 `EscapeSql`？
  5. **`BulkCopyAsync` 自身 bug**：Http 实现里有没有类型转换 Bug？

## 下周继续 TODO

- [ ] **复测 7 个异常工具**：拿到具体错误（log/弹窗/截图）
- [ ] **逐 Form 排查 Bug**：每个异常 Form 单独定位
- [ ] **确认子窗体覆盖度**：例如 CompareTablesForm 已迁，但其他子窗体（比如复制报表里的格式对话框）是否仍走 SqlConnection？
- [ ] **Common 工具排查**：`ObjectExplorerForm` / `GenericCopyToolForm` 这些没在 tools.json 里的，也要确认 Http 模式
- [ ] **跨库复制数据库对象里的"搜索"区**：标准查询子窗体在 Http 模式下的行为
- [ ] **SQL 查询工具**：commit `51f7db4` 已迁移，但用户测试仍异常，需要单独排查

## 当前分支状态

- 分支：`ToolsV1`
- 最新 commit：`1487120 feat(compare): 对比表结构子窗体迁移 Http 代理模式`（今天的第二次提交）
- 上一 commit：`050d129 feat(tools): 跨库复制 6 工具完整迁移 Http 代理模式 + BulkCopyAsync 接口`
- master 分支保持 `90849f7` 不变
- origin/ToolsV1 已推 Gitee

## 备注

- 用户反馈时间：2026-07-11 17:36 GMT+8
- 测试账套环境：A3ToolsHub 部署在 账套服务器（A3ToolsHubSetup 生成的密钥对）
- Http 模式下"能正常工作的 4 个工具"是用户实测口头反馈，暂无错误日志/截图，下周需要补充具体的异常类型
