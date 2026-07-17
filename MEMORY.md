
### WinForms Form 位置/尺寸设置顺序（2026-07-04 教训）

- **Form.Width / Form.Height / Form.Right 在 Show() 之前都是默认值（100x100）**
- **必须先 Show() 拿到 Handle，Width 才是真实值**
- **Show 之后用 Win32 SetWindowPos 强制设精确位置+大小**
- **不能用 Size / SetBounds 在 Show 之前定位**
- **Owner Form 自动限制子窗体位置（X 超出屏幕 → 夹回）**
- **多屏要用 Screen.FromControl(this).WorkingArea，不用 PrimaryScreen**

## 2026-07-17 Http 模式 SELECT BILLNO,* 报 DuplicateNameException

- ✅ **状态**: 成功编译 (0 错 333 warning 全部历史)
- **根因**: `SqlQueryTabPage.ExecuteViaDataAccessAsync` (537-538 行) Http 模式路径用 `dt.Columns.Add(col.Name, ...)` 无去重。**不在服务端** —— 服务端 `A3ToolsHub/Sql/SqlExecutor.cs` 用 `List<ColumnInfo>` 装结果,List 允许重复名,原样序列化发回客户端,客户端撞名 → 抛 `DuplicateNameException`
- **三条路径不一致**:
  - ✅ 直连 SqlDataReader (`SqlQueryTabPage.cs:385-394`) try/catch DuplicateNameException + _2/_3 后缀
  - ✅ IDataAccess→DataTable (`ProxyHelper.ExecuteQueryToDataTableAsync` 73-86 行) HashSet 主动去重
  - ❌ Http 模式 ExecuteBatchAsync→DataTable (`ExecuteViaDataAccessAsync` 537 行) 无去重(陛下踩的就是这个)
- **修复**: ExecuteViaDataAccessAsync 构造 dt 时加 HashSet 去重 + _2/_3 后缀(照抄 ProxyHelper 算法,11 行新增)。**服务端不改**(List 重复名天然不报错,原样发回由客户端去重更合理——服务端不该擅改用户 SQL 的列名)
- **worklist**: D:\work\A3Tools\worklist\2026-07-17-fix-http-mode-duplicate-column-datatable.md
- **顺手发现但未改**: `DirectDataAccess.BulkCopyAsync` (297 行) 也是裸 `dt.Columns.Add` 无去重,BulkCopy 场景一般不会重名,先不动。要修加 try/catch 即可
- **经验**:
  1. 加新路径时记得对齐已有兜底:2026-07-09 加 ExecuteViaDataAccessAsync 时复制了 dt 构造逻辑但漏复制去重 → 应该封装成 `BuildDataTableFromResultTable(table, dedup: true)` 工具方法
  2. 重复列名是 SSMS 允许 / DataTable 不允许的边界 → 任何把 IEnumerable<string> 灌进 dt.Columns 的地方都得主动去重,不能依赖 try/catch(Http 模式 List 不可控,服务端 List 不会替你报错)

## 2026-07-17 SQL IntelliSense FROM 后过滤存储过程

- ✅ **状态**: 成功编译 (0 错 2 warning NU1701 旧)
- **根因**: `SqlObjectSchemaCache.GetObjectSuggestions` 没按 `ObjectKind` 过滤,缓存里 6 类全返(表 U/视图 V/TVF IF+TF/标量函数 FN/**存储过程 P**/触发器 TR),`SqlIntelliSenseProvider.GetSuggestions` 3 处 FROM-like 调用(第 154/187/223 行)都没二次过滤 → 存储过程泄到 FROM 弹窗
- **"又出来了"来龙去脉**: 2026-07-04 commit `e0144d5` 首次实现时缓存只有 U/V/IF/TF/FN 5 类(没 P/TR),弹窗天然干净;同天 commit `6c216b2` 加 P/TR Kind 后 `GetObjectSuggestions` 没同步加 kind 过滤 → 存储过程泄到 FROM 弹窗。2026-07-06 `intellisense-context-fix` 修 EXEC 路径也没碰这里
- **修复**:
  1. `SqlObjectSchemaCache.GetObjectSuggestions` 加可选参 `IEnumerable<ObjectKind>? kinds = null`(默认全返,向后兼容)+ 内部过滤
  2. `SqlIntelliSenseProvider.GetSuggestions` 3 处 FROM-like 调用都传 `{ Table, View, TableValuedFunction }`
- **EXEC 不受影响**: EXEC 上下文走完全独立的 `AfterExec` 分支,不调 `GetObjectSuggestions`
- **worklist**: D:\work\A3Tools\worklist\2026-07-17-fix-from-intellisense-shows-stored-procedures.md
- **经验**: 往缓存里加 Kind 时,**所有"返回对象的 API"都要同步审视是否需要 kind 过滤**(同 6c216b2 commit 还改过 `GetColumnSuggestions`,目前只按 name 查列,kind 不影响列查询,这次不需要动)
