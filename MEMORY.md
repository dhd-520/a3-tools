
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
