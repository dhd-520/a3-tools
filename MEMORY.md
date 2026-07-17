
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

## 2026-07-17 PowerShell 5.1 UTF-8 误判修正 + release.ps1 端到端加固

- ✅ **状态**: release.ps1 改造完成 (commit 42fb045),0 语法错
- **⚠️ 重大修正**: **v2.4.0 → v2.4.4 一直以为的"Gitee API 中文 mojibake bug"是误判**
  - **真相**: PowerShell 5.1 `Invoke-WebRequest` 把 UTF-8 字节流当 Latin-1 解码,臣读回来的全是 mojibake。**Gitee 服务端存的始终是正确 UTF-8**(hex 验证:"修复"=`E4 BF AE E5 A4 8D` ✅)
  - **客户端为什么显示正常**: `UpdateService.cs` 用 .NET `HttpClient` 读 Gitee API,正确处理 UTF-8。陛下 v2.4.4 截图就是铁证
  - **后续**: v2.4.5 已经 PATCH body 改回中文(id=750218,hex 已验证)。release.ps1 直接传中文即可,**不需要**英文 body + 塞 zip workaround(虽然塞 zip 仍有离线阅读价值所以保留)
  - **调试 API 编码问题的正确做法**:
    ```powershell
    Invoke-WebRequest -Uri "..." -UseBasicParsing -OutFile raw.json
    $rawJson = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes("raw.json"))
    # 或者直接 hex 看
    ```
- **release.ps1 加固**(v2.4.5 踩坑自动化):
  1. **Step 5.5(新)**: csproj bump 后自动 `git commit -m "chore(release): csproj 版本号 bump -> vX.Y.Z"`
  2. **Step 7.5(新)**: 默认把 `-ReleaseNotes` 内嵌为 zip 里的 `RELEASE_NOTES.md`(`-SkipEmbedNotes` 可跳过)
  3. **Step 8 改造**: tag push 失败时 `throw + exit 1`(不再 warn 继续)。新增 Step 8.5 验证 tag 指向当前 HEAD,不一致就 force push 修
  4. **Step 12(新)**: 最终验证——GET release 检查 body UTF-8 中文存在 + tag 指向 + zip asset 挂载
- **worklist**: D:\work\A3Tools\worklist\2026-07-17-release-ps1-fix-and-powershell-utf8-misdiagnosis.md
- **经验教训**(重要,记一次够):
  1. **绝不能再信 PowerShell 5.1 `Invoke-WebRequest | ConvertFrom-Json` 输出做中文判断**——必须用 `OutFile` + `[Text.Encoding]::UTF8.GetString` 或 hex dump
  2. **脚本失败要 throw + exit**,不要只 Warn——warn 会让流程半残继续走完,出问题难定位
  3. **任何"原子操作"必须 commit**——之前 Step 5 改 csproj 不 commit 是大坑,Step 5.5 强制 commit
  4. **Gitee release API 行为诡异**: tag 已存在时会"自动创建"一个指向 target_commitish 分支 HEAD 的新 tag object,但实际用的是 A3ToolsRelease 仓库(发布仓库)而非 origin(源码仓库)的 master HEAD——如果发布仓库 master 落后源码仓库 master,tag 指向就错了。Step 8.5 强制验证可破
- **下次发版**: 直接 `.\scripts\release.ps1 -Version "2.4.6" -ReleaseNotes "...中文..."` 一键搞定
