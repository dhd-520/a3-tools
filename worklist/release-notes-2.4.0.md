## A3Tools v2.4.0

### 🐛 修复
- **SQL 查询工具**：对象资源管理器双击表加载 CREATE 脚本失败。根因：表（`sys.objects.type='U'`）在 `sys.sql_modules` 里没有记录,`LoadCreateScriptAsync` 必然返回 null。修复：objType=='U' 早期 return 分流到 `LoadTableScriptAsync`，重写表脚本路径支持直连 + Http 双模式,直连和 Http 输出格式 100% 一致。Build: 0 错。
- **Http 模式细节修复**（合并提交）
  - `ProxyHelper.ExecuteQueryToDataTableAsync` 修复重复列名报错：直连 `SqlDataAdapter` 自动加 `_2/_3` 后缀,Http 模式原本抛 `DuplicateNameException`,修复后对齐 SqlDataAdapter 行为（治本,影响所有 Http 调用方）
  - `CrossDbCopyAppFormForm` 移除 Http 模式搜索提示弹窗（陛下反馈无需二次确认,直接进入搜索逻辑）

### ✨ 新增
- **通用复制工具**（`GenericCopyToolForm`）接入 Http 代理模式：4 处直连全部走独立 Http 分支方法,直连代码 100% 不动
- **跨库复制配置数据**（`CrossDbCopyConfigDataForm`）接入 Http 代理模式：`BtnSearch_Click` 和 `BtnFindMissing_Click` 两个流程加 Http 分支
- **4 个 1 处直连工具**接入 Http 代理模式：
  - 跨库复制单据流转（`CrossDbCopyObjectLinkForm`）
  - 跨库复制报表（`CrossDbCopyReportForm`）
  - 跨库复制 WEB 看板（`CrossDbCopyWebObjectForm`）
  - 跨库复制移动看板（`CrossDbCopyAppChartForm`）

### 📚 文档
- Http 代理模式批量迁移 worklist（`2026-07-11-tools-http-migration-bulkcopy.md` + `2026-07-13-http-proxy-tools-remaining.md`）

### 📝 升级提示
- 启动 A3Tools → 帮助 → 检查更新
- 工具会自动从 Gitee（国内快）/ GitHub 兜底拉取最新版
- 本次直连代码 100% 不动,无破坏性变更,可放心升级
