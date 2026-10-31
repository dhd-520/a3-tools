
### ⚠️ 【永久】永远不要 `git restore <file>` (2026-09-01 差点丢光未提交改动)
- `git restore <file>` 是**不可逆**的，会覆盖未提交的本地修改
- `git fsck --dangling` 也找不回未 stage 的 working tree 修改（git 只在对象库里保存 stage/commit 过的内容）
- **正鯇备份习惯**：改字节/改结构前 `cp 原文件 worklist/YYYY-MM-DD-baseline-xxx.cs`
- **清理 BOM 用 `read` + `write` 工具重写整个文件**（read 出来的内容自带干净 BOM，不需要手动处理字节）
- PowerShell 的 `[..]` 范围运算符对 byte[] 有 bug，不要用 `$bytes[9..]` 这种写法操作字节

### ⚠️ 【永久】.NET 7 项目不持 C# 12 集合表达式 (2026-09-01 踩坑)
- `<TargetFramework>net7.0-windows</TargetFramework>` → 默认 LangVersion = C# 11
- C# 12 的 `[char]10` / `['│', '┼']` 这种集合元素语法会编译不过 (CS1026/CS1525/CS1002)
- **正确写法**：`content.Split('\n')` / `trimmed.Split(new[] { '│', '┼' }, StringSplitOptions.RemoveEmptyEntries)`
- 如果以后陛上升到 .NET 8 SDK 可以用集合表达式

### ⚠️ 【永久】Markdown 多步处理的顺序问题 (2026-09-01)
- 检测型转换（如表格）和全局型转换（如标题/粗体）一起跑时，**先跑全局**、再跑检测型
- 原顺序「表格优先」会跳过 Pretty，表格外的 `##` `**` `-` 全部不替换
- 调试技巧：用 `write` + `exec` 独立测试项目验证逻辑，不依赖 A3Tools 启动

### AI 气泡 Markdown 处理现状 (2026-09-01 commit e8fde63)
- `A3Tools/Forms/AiChatForm.cs` L282 if 块三级处理：
  1. `RemoveThinkTags` 删除 `<think>...</think>`（DeepSeek-R1/Qwen-QwQ）
  2. `MarkdownToPrettyText` 全局：`##` → `━━...━━`、`**bold**` → `【bold】`、`code` → `「code」、`-` → `•`
  3. `MarkdownTableToText` 表格：标准 `|` 和预格式化 `│` 两种输入都识别，输出统一 `│─┼─` 制表符对齐
- Label 不持局部格式，纯 Unicode 符号模拟是现阶段最优解

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

### 2026-10-22 新增「A3密文解密」工具 + WinForms Anchor 括号坑

- **新增工具**: `A3DesDecryptTool`（分类「辅助」），注册在 `A3Tools/Plugins/tools.json`
- **涉及文件**: `A3Tools.Common/Services/A3DesCrypto.cs`、`A3Tools.Plugins.Default/Forms/A3DesDecryptForm.cs(.Designer.cs)`、`DefaultTools.cs`、`tools.json`
- **A3 客户端密文算法**（实测推导，非 3DES！）：
  - 实际是 **DES / CBC / PKCS7**（不是 TripleDES），类为 `A3.Common.Util.A3DESEncrypt`，内部用 `DESCryptoServiceProvider`
  - Key = `Convert.FromBase64String(A3DESEncrypt.CKey)` → 8 字节，CKey = `jkHuIy9D/9i=`
  - IV  = `Base64Decode(A3DESEncrypt.CIV)` 后**取前 8 字节**，CIV = `Mi9l/+7Zujhy12se6Yjy111A`（解码后 18 字节）
  - `GeneralKeyIV()` 方法返回的是**每次随机生成**的新 key/iv，不能用于解密既有密文，别被它误导
  - 与工具箱自身的 `EncryptionService`（机器名+用户名 SHA256 派生的 AES-256）**完全无关**，两套并存
- **⚠️ WinForms Anchor 括号坑（重要）**：设计器 4 向 Anchor 的标准写法是 **`((AnchorStyles)(((A|B) | C | D)))` —— 三个左括号**
  - 手写漏掉第三个 `(` → 括号不配对 → 编译器报 `CS1002 应输入 ;` / `CS1513 应输入 }`
  - **报错行号会飘到下一行**，且报错列号超出那行实际长度，极易误判成「编码乱码」而白折腾
  - 教训：先数括号再怀疑编码。排查顺序 = ①行号列号对不上文件内容 → ②先怀疑语法不是编码 → ③再查 BOM
- **PowerShell 冒烟测试 net7 程序集**：WinPS 5.1（.NET Framework）**加载不了 net7.0 DLL**，`Assembly.LoadFrom` 不报错但 `GetType` 返回 null，别误判成「类型没编进去」。要测就建 net7.0-windows 控制台工程用 `dotnet run`
- **PowerShell 脚本铁律**（本轮又踩）：脚本文件内**禁止任何非 ASCII 字符**（含注释和路径）——PS 5.1 按 GBK 读，乱码后 `-EA 0` 静默扫不到文件；路径用 `Join-Path` + 动态枚举，输出路径用 `$env:TEMP`
- **元数据字符串扫描**：类型/命名空间名在元数据里可被 UTF-8 扫到，但**字符串字面量在 `#US` 堆是 UTF-16**，扫不到不等于没编进去
- **冒烟测试工程**: `D:\Temp\a3smoke`（含正例 2 条 + 反例 4 条，可复用回归）

### 2026-10-24 AI 启动账套「任意组合启动」改造（4 个叠加 bug）

- **需求**：陛下反馈「AI 助理启动 A3 账套时没法根据参数单独启动客户端或开发工具，现在启动就是客户端和H5web端一起启动，开发工具还启动不了」
- **新增**: `A3Tools.Common/Models/LaunchTargetSpec.cs`（namespace `A3Tools.Models`）
  - 4 个独立开关：`LaunchDesktop` / `LaunchDevTools` / `LaunchErp` / `LaunchWechatWork`
  - `TryParse(raw, out spec, out error)`：token **精确匹配**（不是 Contains），支持 `+ , ; / |` 和空白分隔，忽略大小写，支持中文别名（客户端/开发工具/网页/企业微信）
  - `TryParseTokens(IEnumerable<string>)`：AI 传数组时用
  - `ToCanonicalString()`：规范化往返（AI 侧校验后交给 MainForm，避免两处解析规则漂移）
  - `Default` = client+erp（与旧行为一致，不让存量用法突变）
- **改**: `MainForm._LaunchAccountInternal` 的 `forceTarget`(string) → `forceSpec`(LaunchTargetSpec?)，**四个开关全量覆盖** settings
- **改**: `MainForm.LaunchAccountByCode(code, target)`；`StartAccountAction` 加 `target` 参数（string/array 都支持，`ReadTargetArgument`）
- **改**: `AiChatForm` 提示词教模型「只启客户端」「加上开发工具」等场景必须传 target
- **⚠️ 这次的 4 个真 bug（都是字符串 Contains 判断惹的祸）**：
  1. **forceTarget 只写 `settings.LaunchWeb`（死字段！）** —— 启动逻辑真正读的是 `LaunchErp` / `LaunchWechatWork` → **AI 无论指定什么，H5web 都跟着启**（陛下反馈的根因）
  2. `forceTarget.Contains("all")` 被当成客户端判断 → **"all" 不含 "client" → 「全启」反而把客户端关掉**
  3. `Contains` 是子串匹配，"devtools" 里也含 "dev"，歧义多
  4. devtools exe 不存在时**静默跳过**（`if (File.Exists(exe2))` 无 else）→ 陛下只看到「没反应」。现已加明确 toast
- **⚠️ 设置项「部分覆盖」是万恶之源**：只改其中几个开关、其余留旧值 → 行为不可预测。**要么全量覆盖，要么别动**
- **⚠️ `AppSettings.LaunchWeb` 是历史死字段**（等价 LaunchErp），启动逻辑不读它。以后新增启动开关**必须接到真正被读的字段上**
- **⚠️ AI 工具参数要有 enum 提示**：schema 里 `@enum = new[] { "client", "devtools", ... }` 让模型知道合法值；非法值**把错误回传给 AI**（`AiActionResult.Fail`）让它自我纠正，而不是静默启动错的东西
- **⚠️ 编程时不要用 `Contains` 解析命令式字符串**：token 精确匹配 + 显式错误回传，可测性天差地别
- **单元测试工程**: `D:\Temp\a3launchtest`（38 项全 PASS：单组件/组合/分隔符/中文别名/非法输入/none/Default/规范化往返/数组形式/旧 bug 回归）
- **部署提示**: 实际运行的实例在 `D:\Work\工具\A3工具箱更新\`（单文件发布），源码 `bin\Debug` 那份的 `DATA\settings.json` 是另一套（AppDirectory 指向 `D:\A3\未升级A3`）。**改完要发布过去才生效**
- **⚠️ `D:\A3\未升级A3\` 目录下没有 `君则a3集成开发工具.exe`**（只有 `D:\A3\君则A3\` 有），所以设置指向它时开发工具永远起不来
- **⚠️ A3 进程名大小写不一致**：exe 实际叫 `君则a3集成开发工具.exe`（小写 a），而代码里常量写 `君则A3集成开发工具`（大写 A）—— Windows 不敏感所以能跑，但别照着常量名去猜磁盘文件名

### 2026-10-24 AI 对话框「工具调用显示具体内容」+ execute_sql 安全补洞

- **需求**：陛下要求 AI 助理对话框里调用工具要显示具体内容、可折叠（如 execute_sql 要看到实际执行的 SQL），并检查该工具是不是不能执行 INSERT
- **★ 关键发现：`ChatToolCallRecord.ArgumentsJson` 字段早就一直在记录工具入参**，只是渲染时只画了 `ToolName` + `Result.Summary`，参数被整个丢掉 → **不用改后端，改渲染即可**
- **改 `A3Tools/Forms/AiChatForm.cs`（实时流 + 历史回看两条路径都要改，否则视觉不一致）**：
  - CSS 新增 `.tool-args-block` / `.tool-args-content` / `.tool-args-label` / `.tool-sql` / `.tool-args-json`（仿 thinking-block，默认收起）
  - JS 新增 `buildToolArgsHtml(argsJson)`：`sql` 字段单独提出来放「实际执行的 SQL：」块，其余参数格式化后放「其他参数：」块
  - JS `renderToolCard(name, summary, argsJson)` / `openAppendToolBlock(name, summary, argsJson)` / `rerenderToolStream` 全部加 argsJson 透传
  - C# 新增 `BuildToolArgsHtml(string?)` 静态方法（历史回看路径，与 JS 端输出同款）
  - C# `onToolExecuted` 把 `toolCallLog.Last().ArgumentsJson` 一并推给前端
  - **★ 顺手修的显示盲区**：`onToolExecuted` 原来第一行是 `if (!result.Success) return true;` → **失败的工具调用根本没有卡片**，陛下只看到 AI 说「查询失败」，看不到它到底试了什么 SQL。已改成失败也出卡片（`AiActionResult.Fail` 会把错误填进 `Summary`，数据是有的）
- **★ SQL 安全补洞（反射实测发现的 3 个真绕过）**：
  - `SELECT * INTO newTbl FROM t` → 建表 = 写操作，但「INTO」不在黑名单、语句以 SELECT 开头 → **直接通过检查**。已把 `INTO` 加入黑名单
  - `SELECT * FROM OPENROWSET(...)` / `OPENDATASOURCE(...)` → 跨库跨服务器外联（横向移动 + 数据外泄）→ 已加入黑名单（另加 `OPENQUERY`）
  - `SELECT * FROM sp_executesql` → **这是在执行存储过程而不是查表**（T-SQL 特性：`FROM` 后面跟过程名即调用）→ 已加正则 `\bFROM\s+(?:\[?[\w\-#]+\]?\s*\.\s*)?\[?(?:SP|XP)_[\w\-#]*\]?` 拦截
  - **结论：execute_sql 本来就【不能】执行 INSERT** —— 双重拦截：黑名单 `\bINSERT\b`（词边界，不误伤 UPDATED_AT）+ 白名单必须以 SELECT/WITH/SHOW/DESCRIBE/DESC/EXPLAIN 开头
  - `execute_ddl` 也拦 INSERT/UPDATE/DELETE/EXEC/MERGE（只放 CREATE/ALTER/DROP/TRUNCATE），所以**当前没有任何工具能写业务数据**
- **★ 测试断言写错的自坑**（自己踩的）：断言输出里含 `"code"` 裸引号，实际输出是 `&quot;code&quot;`（HTML 编码后才安全）→ 断言要按编码后的形态写
- **★ 提取 AI 气泡 HTML/JS 做 node --check 验证**（好办法，值得复用）：
  - 脚本 `C:\Users\林宝宝\.openclaw\workspace\a3_decrypt\extract_ui_js.ps1`
  - **⚠️ 结束判定不能用「行尾是 `";`」** —— CSS 里 `content: ""▶"";` 这行也以 `";` 结尾，会误命中导致只截到 4KB。正确做法：**锚定 `</html>`**
  - C# verbatim 字符串 `@"..."` 里 `""` = 字面量 `"`；取出来要 `.Replace('""','"')` 还原；非插值 verbatim 里 `{` `}` 无需转义
  - 提取后 `node --check` 验证 JS 语法（exit 0 才算过）—— 比肉眼 review 靠谱
- **⚠️ 写 JS 嵌在 C# verbatim 字符串里**：JS 里的双引号必须写成 `""`，单引号 `'...'` 可直接用（写 JS 优先用单引号，能少一半转义坑）
- **测试工程**: `D:\Temp\a3sqltest`（49 项：SQL 安全 41 + 参数渲染 8）

### 2026-10-25 execute_sql 开放 INSERT 权限（UPDATE/DELETE 只回传语句）

- **需求**：陛下要求「AI 调用的 execute_sql 工具开放 Insert 权限，只限制不让更新，更新时给出更新语句就行」
- **★ 为什么要【重写】而不是打补丁**（旧实现是「整条 SQL 扫黑名单 + 必须以 SELECT 开头」，开放 INSERT 必然踩坑）：
  1. 旧黑名单含 `INTO` → `INSERT INTO` 本身含 INTO，**光把 INSERT 挪出黑名单会被自己误杀**
  2. 旧实现扫**整条 SQL** → `SELECT * FROM t WHERE note = 'please DELETE this'` 会被误判成写操作
  3. 旧多语句检查扫 `;` → `INSERT INTO t(a) VALUES('a;b')` 会被误判成多语句
- **新方案**：`A3Tools/Services/AiActions/SqlStatementAnalyzer.cs`（新增文件）
  - **词法遮罩**（3 步，顺序不能错）：去注释 → 字符串字面量内容清空（保留引号）→ 方括号标识符内容清空
    - 遮罩后 `text`（保留方括号）供 sp_ 检测用；`scan`（方括号也清空）供关键字扫描用
  - **主语句动词解析**：只认「语句第一个词」；`WITH cte AS (...)` 会用括号配对穿透到 CTE 之后的动词
  - **按动词分类**：Select / Insert / Update / Delete / Other
  - **多语句判定**在遮罩后的文本上做 → 字面量/子查询里的分号天然不触发
- **各类型策略**：
  | 类型 | 行为 |
  |---|---|
  | SELECT/WITH/SHOW/DESCRIBE/EXPLAIN | 直接执行（不弹框，保住 2026-08-27 陛下决定） |
  | INSERT | **真正执行**（走 `ExecuteNonQueryAsync` 写入通道），弹确认框，单次最多 5000 行 |
  | UPDATE / DELETE | **不执行**，把语句原样放进 result（`executed=false` + `sql` + `message`），AI 拿给陛下手工跑 |
  | DDL / EXEC / MERGE / GRANT | 照旧拒绝 |
- **★ INSERT 走 `ExecuteNonQueryAsync` 而不是 `ExecuteQueryAsync`**：查询通道用 `ExecuteReaderAsync`，INSERT 用它会拿不到影响行数；写入必须走 `ExecuteNonQuery`（Http 代理模式下对应 Hub 的 `nonquery` action，已支持）
- **★ 确认框改为「按参数判定」**：
  - `IAiAction` 新增默认接口方法 `bool RequiresConfirmationFor(Dictionary<string,object?> args) => RequiresConfirmation;`（默认实现 → 其它 Action 零改动）
  - `OpenAiCompatibleBackend.cs:361` 改成 `action.RequiresConfirmationFor(argsDict)`
  - `ExecuteSqlAction.RequiresConfirmation` 仍是 `false`（保住 8-27「只读查询不弹框」），但覆写 `RequiresConfirmationFor`：只读 → false，INSERT → true
  - 静态属性 `RequiresConfirmation` 无法区分「同一条工具的只读与写入」→ 必须按参数判定
- **★ 同步改了 AI 提示词**（`AiChatForm.cs` 3588/3643 行附近）：不改的话提示词还写着「禁止 INSERT」，AI 会不敢用新权限
  - 同时加了一条硬规则：「需要修改/删除时不要自己换写法绕过，工具会拒绝并把语句原样返回给陛下」
- **★ 自己踩的回归坑**：重写时把 `SHOW`/`DESCRIBE`/`EXPLAIN` 漏在动词表外 → 这 3 种只读语句全部被拒（比旧实现更严 = 回归）
  - 教训：重写安全检查时，**旧实现放行的每一种形式都要在新实现里逐条复测**（`DESC` 也一样）
  - 注意 `DESC` 加进动词表是安全的：只认「语句第一个词」，`ORDER BY x DESC` 的 DESC 不会被误当主动词
- **★ 测试断言要按语义写**：别断言「首尾空白必须保留」这种实现细节，断言「语义保真」即可
- **测试工程**: `D:\Temp\a3sqltest` 重写为 89 项全绿（直接引用类型，不再靠反射）

### 2026-10-25 工具参数显示中文变 \uXXXX 转义（已修）

- **陛下反馈**：「AI助理工具显示中文无法查看 能不能显示成正常中文信息」，贴出来的实际内容：
  `"base_name": "A3 \u5546\u57CE\u8868\u7ED3\u6784"` —— 本该是「A3 商城表结构」
- **★ 病根：.NET `JsonSerializer` 默认编码器 `JavaScriptEncoder.Default` 会把非 ASCII 全转义成 \uXXXX**
  - 厂商返回的 tool_call `function.arguments` 本身就是转义串（`OpenAiCompatibleBackend.cs:343` 直接存）
  - **二次转义点在 C# 历史卡片**：`AiChatForm.BuildToolArgsHtml` 用默认 `JsonSerializerOptions` 重新序列化 → 又转义一遍 → 界面显示成乱码
  - **JS 实时卡片本来就是好的**（`JSON.parse` 会解码，`JSON.stringify` 不转义非 ASCII）→ 所以陛下看到的是**历史/重开**那条路径
  - 已用最小复现程序实证：默认编码器输出 `\u5546\u57CE`，换 `UnsafeRelaxedJsonEscaping` 立刻变「商城」
- **修法**：
  - 新增 `A3Tools/Services/JsonText.cs`（静态类）：
    - `DisplayOptions` = `{WriteIndented=true, Encoder=UnsafeRelaxedJsonEscaping}` → 给界面展示
    - `CompactOptions` = `{WriteIndented=false, Encoder=UnsafeRelaxedJsonEscaping}` → 给存盘/日志
    - `Relax(json, options)` = 解析成功则用展示选项重序列化；失败则走 `DecodeUnicodeEscapes` 兜底
    - `DecodeUnicodeEscapes(text)` = 手工扫 `\uXXXX` 还原（支持代理对 `\uD83D\uDE00`）
  - `AiChatForm.BuildToolArgsHtml` → 用 `JsonText.DisplayOptions`；catch 分支改 `JsonText.DecodeUnicodeEscapes(raw)`
  - JS 端 `buildToolArgsHtml` 加 `decodeUnicodeEscapes(s)`，catch 分支用它（实时卡片本来就是对的，只是补齐对称性）
  - `OpenAiCompatibleBackend.cs:390` → 存盘/日志用 `JsonText.Relax(argsJson, JsonText.CompactOptions)`
    - **只改存储副本，`tc.Function.Arguments` 原串不动** → 发给模型的消息格式不变
- **XSS 风险不变**：`UnsafeRelaxedJsonEscaping` 不转义 `<>&`，但展示前还有 `WebUtility.HtmlEncode` 兜底 → 已有测试守着（`<script>` / `<img onerror>` 均被编码）
- **★ 顺带发现陛下数据里的一个错字**：知识库标题 `满赠关聛活动` 里第 4 字是 **U+805B「聛」**，正确应为 **U+8054「联」** → 「满赠关联活动」
  - `\uXXXX` 是确定性映射 → 说明**知识库里存的原文就是「聛」**，是数据侧的错字（大概率从别处复制时带入），不是编码问题
  - 已在测试里改成**码点精确比对**（不再手打中文，避免自己敲错）
- **★ 测试输出别走控制台**：Windows 控制台默认 GBK，中文断言详情全是乱码、还会在某些字上截断 → 二哈把 `Console.SetOut` 到 UTF-8 文件再读
  - 产物：`D:\Temp\a3sqltest\test-output.txt`
- **★ 教训：断言别手打中文**。这次 FAIL 是二哈自己把 U+8054「联」当成 U+805B「聛」敲进断言，查码点才定位 → 断言一律用码点或从数据推导
- **★ 文件锁**：编译时 VS(25180) + 运行中的 A3Tools(22340) 锁着 `bin\Debug` → 按 `dotnet-build-vs-file-lock` 技能：`taskkill /F /IM A3Tools.exe /T` 关掉 App（VS 不能动），再用 `MSBuild.exe` 编译 → exit 0

### 2026-10-25 展开/收起工具区域 → 视图跳到最底部（已修）

- **陛下反馈**：「我展开或收起工具区域 或者某个具体工具执行参数 都会跳到最后，而不是停留在当前位置」
- **★ 病根链**（`AiChatForm.cs`）：
  1. 点 `<details>` 的 `summary` → DOM 高度变 → `ResizeObserver` → `_notifyNow()` → postMessage 给 C#
  2. C# `WebMessageReceived`：`wv2.Height = newH` → `LayoutBubbleRow` + `RelayoutMessages()` → **下方气泡全部位移**
  3. C#：`if (_userWasAtBottom)` → **强制把滚动值设成新底部** → 💥 视口跳到末尾
- **★ 修法：区分「用户自己折叠」和「内容自己变高」**
  - **JS 侧**（`AiChatForm.cs` 内嵌 JS blob）：
    - capture 阶段 `document.addEventListener('click', ...)` 找 `summary`（向上冒泡找）→ 记 `window.__detailsToggleAt = Date.now()` 和 `window.__detailsToggleY`（summary 的文档 Y）
    - **★ 用 `click` 不用 `toggle` 事件**：`toggle` 是异步派发的任务，可能晚于 ResizeObserver 回调跑 → 标记来不及置上
    - `_notifyNow()` 的 payload 带上 `toggle`（`Date.now() - __detailsToggleAt < 1200` 的时间窗）+ `ty`
      - 时间窗 1.2s 的原因：一次折叠会连着来一串 notify（ResizeObserver + 字体加载 + settle 定时器），
        全部带 `toggle=true` → C# 统一走锚定；**否则第一个 notify 锚定、后面的又跟滚 → 照样跳**
  - **C# 侧**：`toggle=true` → **不走跟滚**，改走【滚动锚定】
    - 折叠点在视口**上方** → 视口内内容整体平移 `delta` → 滚动值 `+= delta` 补偿（看到同一段内容）
    - 折叠点在视口**内/下方** → 视口内容不动 → 滚动值**原样保持**（陛下要的「停留在当前位置」）
    - 判定用**屏幕坐标**（`wv2.RectangleToScreen` vs `pnlMessagesScroll.RectangleToScreen`）→ 免坐标系换算
- **★ 关键：`_oldBubbleH` 必须在改 `wv2.Height` 之前抓**，且 `_oldMax/_oldCur` 也要提前抓（原来的代码在第一次 `wv2.Height = newH` 之后才抓 → 拿到的是旧 max，注释里也承认了）
- **★ `_lastHeightApply` 字段声明了但根本没被使用** → 高度应用路径**没有节流**，每个 notify 都会立即应用 → 锚定补偿能生效
- **`_userWasAtBottom` 跟滚逻辑保留不动** → 流式输出时依然自动跟到底，不影响原有体验

### 2026-10-26 自定义工具「关联字段」支持多个，与关联表一一对应（v2.6.8）

**需求**（陛下）：关联表每个可能用不同外键。关联字段改成可填多个（英文分号隔开），与上面的关联表按顺序一一对应；只填 1 个时所有关联表共用。

**★★ 规则（写在 `CustomToolForeignKeyResolver`）**
- 关联字段留空 → `None`：只复制主表，不复制关联表
- 只填 **1** 个 → `Shared`：所有关联表共用这一个（**完全兼容旧配置，旧数据零迁移**）
- 填 **多个** → `PerTable`：必须与关联表**数量一致**，按顺序一一对应
- 数量不一致 → **非法**（报错，不静默猜）

**★★ 架构：纯逻辑抽出来，可单测**
- 新增 `A3Tools.Common/Services/CustomToolForeignKeyResolver.cs`（纯算术 + 无 WinForms）
  - `SplitList(raw)` / `Resolve(relatedTables, foreignKey)` → `ForeignKeyResolution`
  - `ForeignKeyResolution`：Mode(Shared/PerTable/None) + RelatedTables + ForeignKeys(等长同序) + Error + `GetForeignKey(i)`
- `CustomToolConfig` 加 `ForeignKeyList` 和 `ForeignKeys`（都是 `[JsonIgnore]`，不落盘）
- `CustomToolConfigDialog`：保存前校验（数量不匹配 → 弹窗报错，焦点回 `txtForeignKey`）
- `GenericCopyToolForm`：构造时解析一次存 `_fkResolution`；复制时 `for (int t...)` 按表取各自外键
  - 同步 `TableCopyService.CopyTableDataByParentGuid` 和 Http `ProxyHelper.CopyTableDataByParentGuidAsync` **两条路都改了**
  - 状态栏文案：PerTable 模式显示「表=字段」对照（如 `S_A=F1 / S_B=F2`）

**★★ 坑：测试必须走生产序列化器（又踩一次同一个坑的变种）**
- 二哈第一版 G 段用 `JsonSerializer.Deserialize(list)` **默认选项** → 属性名大小写不匹配（`relatedTables` vs `RelatedTables`）→ 3 个 FAIL
- 生产 `CustomToolStorage` 用的是 `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` + `WriteIndented`
- **修法：测试直接用生产的 `CustomToolStorage` 类**（它写 `AppContext.BaseDirectory\DATA\`，即测试输出目录，**不碰真实部署数据**）
- **★ 教训升级：上次是「手抄生产逻辑」，这次是「绕过生产序列化器」——本质一样：测试用的路径 ≠ 生产路径，就测不准**

**★ 真实数据回归**：直接拿 `D:\Work\工具\A3工具箱更新\DATA\custom-tools.json` 跑一遍 → 5 条配置、1 条有关联表、全部 Shared（旧的单字段）→ 旧配置零影响

**★ 测试工程**：`D:\Temp\a3fktest`（46 项全绿）

**★ 部署**：`D:\Work\工具\A3工具箱更新\` → File=2.6.8.0，`DATA\` 未动

### 2026-10-26 账套管理列宽：只改临近列 + 总宽恒定占满 100%（v2.6.7）

**需求**（陛下）：首页账套管理拖列宽时，只改临近列，总宽不变、始终占满 100%。

**★★ 病根：`AutoSizeMode = Fill` 是「一拖全动」的元凶（探针实测锤死）**
- 5 列 Fill，改 `col[2].Width = 200` → `col[3]` 103→79、`col[4]` 92→70 **全被连坐**
  （WinForms 在任何一列宽度变化后，会把**所有** Fill 列按 FillWeight 重新分配）
- 全部改 `None` 后改 `col[2].Width` → **只有 col[2] 变，其余纹丝不动** ✅
- ⇒ 结论：**列改 None，宽度改由自己分配**（别指望 WinForms）

**★★ WinForms 实测出来的两个尺寸事实（别猜，去测）**
1. **可用宽度 = `dgv.DisplayRectangle.Width - 1`**
   - `DisplayRectangle` 已扣除垂直滚动条（实测：无滚动条 600，出滚动条 **583**）
   - 再减 1px：实测总宽正好 = 600 时**水平滚动条会冒出来**，599 才不出
2. **垂直滚动条出现/消失 → 可用宽度变了（600 ↔ 583）**
   → 必须监听 `dgv.Controls` 里的 `VScrollBar.VisibleChanged` 重新铺满，否则右边空 17px 或出横向滚动条

**★★ 架构：逻辑与控件定义分离（SOUL.md 规矩）**
- `A3Tools/Services/DgvColumnWidthAllocator.cs` — 纯算术（无 WinForms 依赖）：`ScaleToFit` / `DistributeByWeight` / `ApplyUserResize`
- `A3Tools/Services/DgvColumnLayoutManager.cs` — 生产类，持有 DataGridView，负责播种/铺满/拖动补偿/滚动条联动
- `MainForm.cs` 只做：`new DgvColumnLayoutManager(dgvAccounts, "ColCode")` + 列定义改 None
- **★ 教训：测试里不要「手抄一份生产逻辑」** —— 二哈第一版测试就是复制品，结果和生产代码漂移（复制品漏了铺满步骤），
  测了个寂寞还误以为生产代码有 bug。正确做法：**把逻辑抽成生产类，测试直接用生产类**。

**★★ 两个真 bug（都是测试抓出来的）**
1. **`ApplyUserResize` 正负号写反**：列变宽时邻列该**变窄**，我最初写成变宽
   - 正确语义：`pending = delta`（>0 = 要从别的列**扣**宽度，<0 = 别的列要**长**）
2. **空间不足时水位填充提前 `break`** → 未处理的列停在 **0**，突破 MinimumWidth
   - 修法：`FitCore` 末尾补一道底线：任何列不得 < 自己的 MinimumWidth
   - 此时总宽会超出可用宽 → 出水平滚动条，这是空间不够时的必然结果（总比把列压成 0 看不见内容强）

**★★ WinForms 写回列宽的两个坑**
- 在 `ColumnWidthChanged` 里写 `col.Width` → **递归** → 必须有 `_applying` 标志位防重入
- 列重建（`Columns.Clear()` + `AddRange`）期间也会触发 `ColumnWidthChanged` → 用 `Suspend()/Resume()` 屏蔽

**★ 拖动语义**（陛下要的「只改临近列」）
- 拖动被拖列 → 该列取新宽，差值由**右侧相邻列**吸收（右邻优先，再往左回退）
- 邻列到最小宽还不够吸收 → 顺延给再下一列；全部到底 → 把被拖列自己缩回去（**总宽永远恒定**）
- 窗口缩放时：用户手动调过的列保持**相对比例**跟着缩放；「代码」列（ColCode）默认锁宽，用户手动拖过它后就解锁

**★ 测试工程**：`D:\Temp\a3coltest`（59 项全绿，含直接跑生产类的集成用例 E 段 18 项）

**★ 部署**：`D:\Work\工具\A3工具箱更新\` → File=2.6.7.0，`DATA\` 未动
- **★ 坑：exe 被锁** —— 部署时 A3Tools 进程又自己跑起来了（PID 11076），`Copy-Item` 报「正由另一进程使用」
  → **覆盖前必须先 `taskkill /F /IM A3Tools.exe /T`**（VS 不能动，A3Tools 可以）

### 2026-10-25 v2.6.6 发布（已完成）

**结果**
- Gitee release: https://gitee.com/wangq80368036/A3ToolsRelease/releases/tag/v2.6.6 （id=1239449）
- tag `v2.6.6` -> release marker commit `e295f83`（只在 A3ToolsRelease 仓库，源码不泄露）
- zip `A3Tools_v2.6.6.zip`（81.42 MB）已上传；`RELEASE_NOTES.md` 已内嵌 zip
- 部署目录 `D:\Work\工具\A3工具箱更新\` 已更新到 **File=2.6.6.0**，`DATA\` 未动
- commits：`915b9b5` 源码 / `0214ca9` + `ee885e2` + `82ba8ae` 发布脚本修复

**★★ 发布脚本连踩 3 个 PS 5.1 坑（都是「stderr 变终止性异常」或「检测逻辑自己成了阻断点」）**

1. **`Test-Path` 拿多行正文当路径**（`release.ps1:127`）
   - 包装脚本传 `-ReleaseNotes <文件内容>`（多行字符串）→ `Test-Path` 抛「路径中具有非法字符」
   - `$ErrorActionPreference = "Stop"` → 脚本在**第 4 步**（版本号 bump 之前）整个退出
   - 修法：`release.ps1` 改成只在字符串「像路径」（`-notmatch "[\r\n]"`）时才 `Test-Path` + `try/catch`；
     包装脚本改成**传文件路径**，由 `release.ps1` 内部 `ReadAllText(path, UTF8)` 读
   - ★ 教训：**检测逻辑本身绝不能成为发布阻断点**

2. **`git tag -d <不存在的tag>` 的 stderr 变终止性异常**（Step 8）
   - `git tag -d v2.6.6` 输出 `error: tag 'v2.6.6' not found.` 到 stderr
   - PS 5.1 + `ErrorActionPreference=Stop` → 直接抛 → 外层 catch 报「tag push failed」→ release 中断
   - 修法：`2>$null` 改成 **`2>&1 | Out-Null`** + `try/catch` 兜底
     - 同样要修 `git fetch A3ToolsRelease master`（它把 `From https://...` 写 stderr）
   - ★ **PS 5.1 通则**：`native_cmd 2>$null` 在 `ErrorActionPreference=Stop` 下会把 stderr 变终止异常，
     一律用 `2>&1 | Out-Null` + try/catch

3. **Step 12 最终验证查错仓库 + 基准错**
   - 查 `git ls-remote origin refs/tags/<tag>` → 但 tag 只推给 A3ToolsRelease → 返回空 → `.Trim()` 作用在 `$null` → 抛「不能对 Null 值表达式调用方法」
   - 修法：改查 A3ToolsRelease + `Select-Object -First 1` + try/catch

**★★ tag 校验基准必须用 marker 而不是 HEAD**
- 2026-09-30 起 tag 指向的是 **release marker commit**（建在 A3ToolsRelease/master 的 tree 上，文件树只有 README.md，**不泄露源码**）
- Step 8.5 原来拿 `git rev-parse HEAD`（源码 HEAD）比对 → 永远不相等 → 误报「tag 指向错 commit」并 force push
- 修法：基准改成 `$markerSha`

**★ 发布前必做**
- **先 commit 源码改动**（`release.ps1` 的 tag 指向 commit，不 commit 就等于发了个空版本）
- 提交消息用 `git commit -F <UTF-8 文件>`，**不要**用 `-m "中文"`（命令行会被 GBK 污染）
- 中文 here-string 里有 `$var:` 会被 PS 当变量解析 → 用**单引号** here-string `@'...'@`
- `publish\RELEASE_NOTES_*.md` 被 gitignore → 说明文件不进 commit（符合设计）
- 部署前**先关掉运行中的 A3Tools**（exe 被锁写不进去）；VS 不能动

**★ 部署到 `D:\Work\工具\A3工具箱更新\` 的正确姿势**
- 只覆盖：`A3Tools.exe` / `*.pdb` / `Plugins\*` / `Resources\*` / `RELEASE_NOTES.md`
- **绝不覆盖 `DATA\`**（账套 accounts.json、会话 ai_chat、知识库 knowledge、skills、logs 都是用户数据）
- 部署完校验：`(Get-Item A3Tools.exe).VersionInfo.FileVersion` 应等于 `<Version>`

**★ Gitee body 中文验证**
- `Invoke-RestMethod` 读出来会 mojibake（PS 5.1 字符串解码问题，**不是 Gitee 的锅**）
- 必须用 `HttpWebRequest` + `GetResponseStream()` 读**原始字节**再 `[Encoding]::UTF8.GetString()`

**★ 已知问题**
- `github.com` 从国内连接不稳（本次 push 443 超时，重试 3 次 + 后台重试仍失败）→ gitee 正常
  - 影响：github 源仓库 master 落后（本地/来源 HEAD=`50be8d9`，github 停在 `bcaeb8f`），**不影响 Gitee 发布和用户更新**
  - 缓解：`release.ps1` 默认 `SkipGitHub=$true`；国内用户走 gitee
  - ★ 补推方式（网络恢复后）：`git push github master`；**不要**重跑整个 `release.ps1`（会再走一遍打包+发版）

---

## v2.6.9 账套字段设置（2026-10-29 陛下需求）

### 需求原文
> 账套管理增加设置页面，可以单独设置每个账套字段是否显示列表、列表宽度、排序、是否显示在录入界面、录入界面排序

**后续陛下改口径**：不用页签形式 → 改成**独立设置窗体**，入口挂在**文件**菜单下（`menuAccountFieldSettings`，菜单位置在「导入账套」和「退出」之间）。

### 新增文件
| 文件 | 作用 |
|---|---|
| `A3Tools.Common/Models/AccountFieldConfig.cs` | 单个字段的配置模型（Field 主键 + 显隐/宽度/排序） |
| `A3Tools.Common/Services/AccountFieldCatalog.cs` | **字段单一数据源**（15 个字段 + 默认值 = 改造前行为） |
| `A3Tools.Common/Services/AccountFieldResolver.cs` | 纯逻辑：合并配置、强制约束、排序归一化、派生视图 |
| `A3Tools.Common/Services/AccountFieldStore.cs` | 持久化 `DATA\account-fields.json`（camelCase） |
| `A3Tools/Forms/AccountFieldSettingsForm.cs` + `.Designer.cs` | 设置窗体（表格 15 行 × 6 列） |
| `A3Tools/Services/AccountEditorLayout.cs` | 录入界面**纯排版引擎**（不创建控件，只摆位置） |

### 架构约定（下次别走偏）
- **设置页 / 账套列表 / 录入窗体 三处共用 `AccountFieldCatalog`** —— 各自写字段清单必然漂移
- **不动态创建控件**：`AccountEditorLayout` 只改设计器里既有 Label/输入框的 `Top` 和 `Visible`，X 坐标/宽高/字体仍由 `.Designer.cs` 说了算（所见即所得）
- **录入排版引擎与设计器逐像素对齐**：起始 Y=20、行距 45、远程方式前 +10、备注行占 80、web 分组 695、proxy 分组 805（= web 底部 +10）
- **列名 = "Col" + 字段名**（`ColCode` / `ColName` …），和改造前的列名完全一致，别另起一套
- 强制约束（`Normalize` 兜底，界面置灰只是提示）：密码类字段禁止进列表；`Code`/`Name` 必填不可隐藏；宽度钳制 `0..2000`；排序号归一化为 `0..n-1`；列表至少一列

### ★ 踩坑：测试宿主非 DPI 感知 → 窗体坐标全错
- 单测里直接 `new AccountDialog(null)` 断言 `lblRemoteType.Top == 435` **失败**，实测 `pnlWebGroup` 变成 473×58（设计值 840×100）
- 根因：**WinForms 窗体按 `AutoScaleDimensions` + 字体自动缩放**，而控制台测试宿主默认**非 DPI 感知** → 缩放系数 ≈0.57，所有设计坐标都被缩掉
- 修法：测试 `Main` 开头加 `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`，**与生产程序（A3Tools.exe 带 app.manifest，DPI 感知）对齐** → 57 项全绿，坐标与设计器逐像素一致
- ★ 教训：**断言 WinForms 绝对坐标的测试，必须先对齐 DPI 感知模式**，否则测的是「宿主缩放」而不是「生产布局」

### 验证（本轮）
- 新增字段配置单测 `D:\Temp\a3fieldtest` → **57 pass / 0 fail**（A 默认值兼容 / B 强制约束 / C 排序归一化 / D 派生视图 / E 持久化往返 / F 录入排版 / G 窗体运行时冒烟）
- 列宽单测 `D:\Temp\a3coltest` → **59 pass / 0 fail**
- 关联字段单测 `D:\Temp\a3fktest` → **46 pass / 0 fail**
- SQL 回归 `D:\Temp\a3sqltest` → **89 passed, 0 failed**（输出在 `test-output.txt`，控制台是空的，别以为没跑）

### 部署（已完成）
- 版本 **2.6.9**（`A3Tools.csproj` 的 `<Version>`），程序集版本 2.6.9.0
- 已发布到 `D:\Work\工具\A3工具箱更新`（`DATA\` 未动，仍是 10/16 16:11:16）
- ★ 部署前要 `Stop-Process` 掉 A3Tools：它 `CloseMainWindow` 只会**缩到托盘**不退出，exe 锁着写不进去；而且**它会被重新拉起**（本次杀完又冒出 PID 20724），杀完立刻复制
- 发布说明：`release-notes-v2.6.9.md` → 同时拷成部署目录的 `RELEASE_NOTES.md`（原来那份还停在 v2.6.6）

### 未发布到 Gitee
- `v2.6.7` / `v2.6.8` / `v2.6.9` **都还没发 Gitee Release**，tag 只到 `v2.6.6`
- 等陛下确认后再跑 `release.ps1 -Version 2.6.9`（**发布 tag 校验要以 release marker 为基准，不是源码 HEAD**）

### ★ 2026-10-29 陛下追问「账套用户名录入界面排序为什么不能修改」
**根因**：「账套用户名」的输入框在 `pnlWebGroup`「网页版自动登录」面板**内部**（面板内坐标 15,55），
排版引擎只能重排**字段流**（contentPanel 的直接子控件）→ 当时取巧标成 `EditorOrderFixed`，排序号直接置灰。

**改法（已上线，仍是 2.6.9）**：
- 模型加 `EditorGroup`（string?，null = 普通字段）：分组字段的**排序号 = 整个分组面板在字段流里的位置**，
  分组带着提示文字和输入框一起移动
- `AccountEditorLayout.Slot` 加 `GroupHost`（分组面板）：有 GroupHost 的字段 → 摆 `GroupHost.Top`，面板占自己高度
- `GetEditorFields` 不再过滤固定字段，分组字段**参与**排序流
- 隐藏分组字段 → 整个分组面板一起隐藏
- 默认配置下坐标与设计器**逐像素不变**（web 695 / proxy 805）

**★ 顺手修的 UX 缺陷：排序号 ≠ 位置**
- 原来的排序号只是「排序键」：用户把某字段改成 3（想放第 3 位）时，若原来就有个字段是 3，
  **稳定排序**会让这两个按原有先后排 → 用户意图被无视（实测填 3 落到第 5 位）
- 现在改成**位置语义**：填几就是第几位
  - `AccountFieldResolver.MoveTo(configs, field, newPos, isEditorOrder)` —— 纯函数，可单测
  - 设置页 `CellEndEdit` → MoveTo → 回填所有行序号（表格用 **1 基**显示，1 = 最前）
  - 保存时 1 基 → 0 基，落盘后仍是连续 0..n-1

**教训**：给用户「填个序号」的输入框时，**序号必须是位置语义**，不能只是排序键 ——
排序键在遇到重复值时会让用户的意图静默失效。

### 本轮新增验证
- 字段配置单测 **72 pass / 0 fail**（新增 H 段：MoveTo 位置语义 8 项）
- 列宽 59/0、关联字段 46/0、SQL 89/0 全部保持

### ★★ v2.6.10 陛下再改口径：「账套用户名改成和普通的、账套名称一样，只固定代码就行」
**最终形态**（这版才是他要的）：
- **账套用户名 = 普通字段**：独立成行排在字段流里（默认在备注之后），可排序、可显隐
- **「网页版自动登录」分组面板拆掉**：`pnlWebGroup` 整个删除，
  `lblWebHint` / `lblServerUsername` / `txtServerUsername` 三个控件搬进 `contentPanel`
  - 账套用户名标签/输入框用标准字段坐标（30 / 250，宽 590）
  - 自动登录说明改成**随行说明**（`AccountEditorLayout.Slot.Note`），排在字段行下面一行，跟着显隐
- **代码 = 唯一固定字段**（`Required = true`）：永远排第一、不可隐藏、排序号不可改
  - `AccountFieldResolver.GetEditorFields` 把 `Required` 的字段提到最前
  - `账套名称` 解除必填锁（`Required = false`）→ 可排序、可隐藏
- **新默认坐标**（AccountDialog.Designer.cs，已同步）：账套用户名 695、说明 744、代理分组 788
- 隐藏账套名称后新增账套会提示「该字段已被隐藏，请到「文件 → 账套字段设置」恢复显示」
- 模型里的 `EditorGroup` / `Slot.GroupHost` 已删除（不再有分组概念）

**教训**：
- 「整组一起移动」虽然技术上成立，但**用户要的是「这个字段跟别人一样」** —— 优先做最小惊讶的方案
- **分组面板（Panel）里塞控件 = 该控件永远无法参与字段流重排** —— 以后别把字段控件藏在面板里
- 改 Designer 时：**emoji 行的字节容易对不上**，整块替换会失败 → 拆成小块、避开含 emoji 的行

### ★★ v2.6.11 陛下再提：「现有字段为内置字段，支持新增字段，自定义字段可删，内置的不能删」
**实现（内置 = AccountFieldCatalog.All 里的 15 个，其余都是用户新增的）**：
- 模型：`AccountFieldConfig.CustomName`（**要持久化** → 不能用 JsonIgnore）；`DisplayName` 仍是 JsonIgnore，由解析器填充
- `AccountFieldCatalog.IsBuiltIn(field)` → 判定内置/自定义（内置不可删）
- 解析器 `Normalize` 现在**保留自定义字段**（原来不在目录里的字段全被丢掉）；
  ★ 安全护栏：**只有带 CustomName 的才算自定义字段** → 配置文件里手改出来的陌生字段直接丢弃，
  免得升级后目录里删掉的字段变成一根莫名其妙的列
- 值落地：`Account.CustomFields`（`Dictionary<string,string>`）→ 随 accounts.json 一起存（camelCase）
- 列表显示自定义字段列：DataPropertyName 设 **null**（不是 Account 的属性，绑定会报错），
  值由 `CellFormatting` 从 `CustomFields` 取（键 = 列名去掉 "Col" 前缀）
- 录入界面：**设计器里没有自定义字段的控件 → 运行时创建**（Label+TextBox 加进 contentPanel）
  → 这是「所见即所得」原则的**唯一例外**（用户自定义字段天生没有设计器控件）
  · 字段被删 → 旧控件从窗体摘掉并 Dispose
  · `Slot.Note` 机制复用给「随行说明」
- 设置窗体：新增「类型」列（内置/自定义）+「➕ 新增字段」+「🗑 删除字段」按钮
  · 新增走 `PromptInputForm`（新写的通用输入框窗体，带 Designer 文件）
  · 自定义字段键 = `Cust_` + DateTime.Now.Ticks；重名检查（跟内置/自定义都不许重）
  · 删除内置字段 → 明确提示「内置字段，不能删除」
- **删掉的字段其历史值会留在 accounts.json 里**（不再显示，但数据没丢）

**教训**：
- 「用户新增字段」不是加一行表格那么简单 —— 要连**值存储 + 列表显示 + 录入控件**一起打通，
  否则就是个没用的空壳
- 绑定 DataGridView 时，列的 DataPropertyName 指向不存在的属性会报错 → 自定义字段列用
  **DataPropertyName=null + CellFormatting 手动填值**

### ★★ v2.6.12 陛下反馈：「账套字段设置界面有点小，底部很多东西重叠了」
**表象是「窗口小」，底下压着三个真 bug（靠运行时实测坐标查出来的，不是看设计器）**：

| # | 现象 | 根因 | 修法 |
|---|---|---|---|
| 1 | 窗体整体缩小（实测客户区 732×477，设计写 860×620） | `AutoScaleDimensions=(16,35)` 是 **11pt** 微软雅黑基准，窗体却用 **10pt** → WinForms 按字体差值整体缩放 **0.86 倍** | 改成 10pt 对应的 `(14,30)` → 缩放系数实测 **1.000** |
| 2 | 状态文字压住底部按钮 | `lblStatus` `AutoSize=true` 且与按钮**同一行**（按钮 X=155 起，状态文字实测 243px） | 底栏改**上下两行**：上行状态文字（左右拉伸），下行按钮（靠右） |
| 3 | 顶部提示文字被截断 | 4 行提示挤在 58px 标签里，文字实测需要 **224px** | 压成 3 行 + 标签给到 100px |

**★ 三条通用教训（WinForms，必记）**：
- **`AutoScaleDimensions` 必须与窗体 `Font` 严格匹配**：
  微软雅黑 **10pt → (14F, 30F)**、**11pt → (16F, 35F)**、9pt → (7F, 15F)。
  写错 → 整个窗体被按字体差值缩放（不报错、不崩，**只是看着变小又挤**，极难靠读代码发现）
- **设计器里 `Size = new Size(w,h)` 会覆盖 `ClientSize`**（`Size` 是含边框的外框尺寸）→ 客户区凭空少 16×39px。
  **只写 `ClientSize`，别写 `Size`**（本项目 AccountDialog 等都是这么写的）
- **AutoSize 标签不要和按钮放同一行**：消息一变长就压住按钮。要么固定宽度，要么换行

**★ 排查手法（本次靠它定位）**：写个一次性诊断程序，**实例化窗体 → Show → PerformLayout → 反射读运行时真实坐标**，
再断言 ①缩放系数 ②两两相交 ③越界 ④文字实际所需高度（`TextRenderer.MeasureText`）⑤列宽/行高 vs 可视区。
比盯设计器代码可靠得多 → 这套已固化成单测 **J 段 14 项**（`D:\Temp\a3fieldtest`）
- 诊断工程：`D:\Temp\a3layoutdiag`（可复用，改 `CheckForm` 就能测别的窗体）

### ★★ v2.6.13 陛下需求：自定义工具增加「二级关联表 + 二级关联字段」
**复制链路：主表 → 一级关联表 → 二级关联表**（三级）

**配置格式（分号 = 组边界，逗号 = 组内多表）**：
```
一级关联表   S_PRINTSCHEME;S_PRINTSCHEME2;S_PRINTSCHEME3
一级关联字段 GUID;GUID2;GUID3
二级关联表   S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3,S_PRINTSCHEMEDATA4;S_PRINTSCHEMEDATA5
二级关联字段 GUID,GUID2;GUID3;GUID5
```

**★★ 最重要的一条规则（二级组内与一级字段完全对称）**：
- 组内只填 **1** 个关联字段 → **该组所有表共用**这一个（Shared）
  （例：组2 的 `S_PRINTSCHEMEDATA3,S_PRINTSCHEMEDATA4` 对应 `S_PRINTSCHEME2`，字段相同都是 `GUID3`）
- 组内填 **多个** → 必须与该组表数一致，一一对应（PerTable）
- 其他数量 → 明确报错
> ⚠️ 二哈一开始把组内规则写成「表数必须 == 字段数」，把陛下的原始配置误判成错误。
> **教训：新增的分组规则要跟已有的同层规则对称，不要自己发明更严的约束。**

**实现**：
- 模型：`CustomToolConfig` 新增 `SecondaryRelatedTables` / `SecondaryForeignKey` / `SecondaryParentKey`
- 解析器：`A3Tools.Common/Services/CustomToolSecondaryLinkResolver.cs`（纯逻辑可单测）
  · 新增枚举 `SecondaryFieldMatchMode { Shared, PerTable }`
  · `SecondaryLinkGroup.ForeignKeys` 存**展开后**的字段（与 Tables 始终等长同序）→ 复制引擎不用改
  · `SplitGroups` 按 `;` 分组并**保留中间空组**（空组 = 该一级表无二级表）
- 复制引擎（`GenericCopyToolForm.cs`，直连 + Http 两分支都加）：
  · 二级表的父值 = **对应一级关联表那一行的主键值**
  · 主键列探测：配置指定 → 数据库 `PRIMARY KEY` → `GUID` → `OBJECTGUID` → `CODE`
  · 复用现有 `TableCopyService.CopyTableDataByParentGuid`，**不新增复制路径**
  · 表名/字段名来自配置 → 拼 SQL 前一律 `ValidateTableName/ValidateFieldName`
  · 二级配置不合法 → **复制前**就报错，不带着坏配置去连库

### ★★ WinForms 布局的另外两个坑（v2.6.13 实测又踩到）
1. **窗体没设 `Font` 时会继承系统字体**（Microsoft YaHei UI 9pt）→
   `AutoScaleDimensions` 就得是 `(13F, 28F)`；硬写成 10pt 的 `(14,30)` → 窗体被缩放 ~7%
   （实测设计 1040×830 渲染成 **936×775**）。
   → **要么给窗体显式设 `Font = 微软雅黑 10pt`（推荐，和各控件一致），要么按继承字体填缩放基准**
2. **`AutoScroll` 挂窗体上时，`Dock=Bottom` 的按钮会贴到「内容」底部而不是视口底部** → 被滚走看不见。
   → 正确做法：**内层面板负责滚动**（`Panel.AutoScroll=true` + 布局 `Dock=Top` + `AutoSize`），
     按钮区单独挂在窗体上 `Dock=Bottom`，永久钉在可视区底部。
   · 还要注意 `Controls.Add` 顺序：**先加 Fill 区再加 Bottom 区** → 底部区先占位，内容区自动让开
3. **缩放基准的实测判据不是 `CurrentAutoScaleDimensions / AutoScaleDimensions`**（框架运行时会覆写设计值，比值恒为 1），
   而是 **运行时 `ClientSize` vs 你写的 `ClientSize`** —— 这才是真信号。

### ★★ 配置文件「防丢」加固（v2.6.14，必记）
**踩过的坑**：JSON 配置文件被损坏时，`LoadAll()` 吞异常返回【空列表】→ 界面只是「工具不见了」无任何提示
→ 用户保存任意一个工具 → `Upsert()` 再读（还是空）→ `SaveAll()` 只写回这 1 个 → **其余配置被永久覆盖丢失**。

**修法（三层）**：
1. **原子写**：先写 `xxx.json.tmp` → `File.Move(tmp, target, overwrite:true)` 原子替换；
   替换前把上一版复制成 `xxx.json.bak`。不残留半截文件（半截文件正是触发损坏的原因）。
2. **读取失败不静默**：`CustomToolStorage` / `AccountFieldStore` 新增 `LastError`；
   读取失败先备份原文件为 `custom-tools.corrupt-yyyyMMdd-HHmmss.json`，绝不丢原始数据。
   MainForm / AccountFieldSettingsForm 读取失败时**弹窗告知**（含备份路径）。
3. **损坏状态禁止写入**：`Upsert()` / `Delete()` 读失败直接抛错 → 堵死覆盖路径。

**⚠️ 验证老配置兼容性时必须走生产路径**：
- `CustomToolStorage._jsonOptions` 用 `JsonNamingPolicy.CamelCase`（**没有** `PropertyNameCaseInsensitive`）
  → 自己写 `JsonSerializer.Deserialize` 不带这个选项会**一个字段都读不进去**，还会误判成「全 PASS」的假绿
- 正确做法：`new CustomToolStorage().LoadAll()`（它按 `AppContext.BaseDirectory\DATA` 定位文件）
- 验证工程：`D:\Temp\a3compat`（69 项：真实文件逐条验 + 不设置组合矩阵 + 老配置兼容 + 防丢保护）
- ⚠️ 生产 JSON 里中文是 **`\uXXXX` 转义**存的，不是明文 → 别用 `Contains("中文")` 判断

### ★★ 账套整机迁移（v2.6.15）：导出全部 → 换机导入
**硬约束（必记）**：`EncryptionService` 密钥 = `SHA256(机器名 + 用户名 + "A3Tools-2026")`
→ **跟机器绑定**。换台电脑后同一份 `accounts.json` 密文**解不开**（`Decrypt` 静默返回空串），
表现为「**账套还在、密码全没了**」且**零报错**。所以跨机迁移**只能**用独立格式 + 明文密码。

- 导出菜单：`文件(_F) → 导出全部账套(_E)`，**仅 Root 模式可见**（普通模式菜单里没这一项）
- 导入菜单：`文件(_F) → 导入账套(_I)` **按文件内容自动分流**：
  `$type=a3accounts-v1` → 新导入（字段全）；含 `<TbLYBillSet>` → 老 XML 导入（**原有行为一行没改**）
- 合并策略（按**代码**匹配）：本地没有 → 新增；本地已有 → **覆盖**（导入的赢）；代码为空 → 自动分配
- 导入的密码落盘时由 `DataService` 用**本机**密钥重新加密
- 自定义字段的**字段配置**一并迁移；内置字段显示配置**保留本机的**
- 格式与知识库导出同一套路（`$type` 判别）
- 测试工程：`D:\Temp\a3transfer`（47 项：往返一致性 / 跨机约束 / 格式判别 / 合并计划 / 字段配置合并 / 真实生产文件）

**★ 真实 UI E2E 已跑通（2026-10-30，不只是单测）**：
- 导出：Root 模式连点标题 5 次 → 进 Root → 菜单从 4 项变 7 项（出现 `导出全部账套(_E)`）→
  导出成功，**118 个账套 / 132KB**，代码集合与生产完全一致，名称+账套密码 **0 条不一致**，
  DB 密码确为**明文**（`XuFeng#KR@202601131819` 等）✅
- 导入：隔离环境（`D:\Temp\a3importtest`，**不碰生产 DATA**）导入同一文件 →
  118 个账套全部进账，代码/名称与导出、生产三方一致，**dbPassword 已用本机密钥重新加密** ✅
- 临时产物（含明文密码）**已全部删除**。

**⚠ 踩过的坑（UI 自动化）**：
1. `Program.cs` 里有单实例 Mutex `A3Tools_SingleInstance_Mutex_v1` →
   隔离实例启动前**必须先停生产实例**，否则隔离实例直接退出
2. PowerShell 5.1 读**无 BOM** 的 .ps1 按 ANSI 解码 → **脚本里出现中文就语法错乱**。
   写 .ps1 一律用**纯 ASCII**，中文用 `[char]0xXXXX` 拼
3. `SendInput` 的绝对坐标是按**虚拟桌面**归一化的，多显示器下要用主屏宽高 → 点歪。
   改用 `SetCursorPos`（真实屏幕像素）+ `mouse_event` 才准
4. Win32 **MessageBox 的按钮 UIA 枚举返回 0**（`btns=0`），要用 Win32 子窗口枚举 + `BM_CLICK`；
   文件对话框（`#32770`）则用**前台 + SendKeys** 驱动最稳（文件名框默认有焦点）
5. `Text` 控件的 UIA 名字在 Root 模式下是 `🔓 A3工具箱 (Root)`，**锁头 emoji 在前** →
   `StartsWith('A3')` 会匹配不上，要用 `-like '*(Root)*'`

### 历史结论：「导出账套信息」菜单从来没存在过
全 git 历史（`git log --all -p` 扫 `MainForm.Designer.cs` 所有 `.Text =`）证明：
主窗体历史上所有菜单文字里只有 `menuCopyAccount.Text = "复制账套信息"`（复制**选中的一行**），
**从来没有**叫「导出账套信息」的菜单。现有「导入账套」是老工具 A3 XML 的单向导入。
→ v2.6.15 才补上配套导出。

### ★ 录入界面的「随行说明」已取消（v2.6.16）
**踩过的坑**：字段下面挂一句静态说明（如「账套用户名」下面的自动登录配置提示），
但**字段可以自由排序** → 说明跟着字段跑，夹在别的字段中间时看着就像那个字段的**组表头**。
当时部署端顺序是 `备用地址 → 账套用户名 → 账套密码`，而那行字用的是**加粗青色**
（`10F Bold` + `ForeColor(24,145,176)`，跟真组表头 `lblProxyHint` 同款）→
陛下看到的就是「账套密码上面有个『登陆配置』表头」。

**规则**：录入界面里**不要再加字段级的独立提示行**。要说明就挂到**输入框的 ToolTip**
（Designer 里 `ToolTip` + `components` 容器，`SetToolTip` 绑到输入框）→ 不占界面、不被排序带偏。
`AccountEditorLayout` 的 `Note` 机制**已整块移除**（它当初只为这一行提示而存在）。
代理分组 Y：788 → **750**（少一行说明的高度）。

### 未发布到 Gitee
- `v2.6.7` ~ `v2.6.16` **都还没发 Gitee Release**，tag 只到 `v2.6.6`
- 等陛下确认后再跑 `release.ps1 -Version 2.6.16`（**发布 tag 校验要以 release marker 为基准，不是源码 HEAD**）
