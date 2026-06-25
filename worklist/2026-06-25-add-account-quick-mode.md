# 2026-06-25 新增账套两种模式（手动添加 / 一键添加）

## 需求

新增账套改为两种模式：
1. **手动添加**：保持弹出现有 AccountDialog 窗体
2. **一键添加**：只一个多行文本框，把 Root 模式下复制的账套信息粘贴到文本框，确定后自动解析并添加
- code 保持自动编码（即使粘贴里有 code 也忽略）
- 其他信息自动匹配添加

## 实现

### 新增窗体 `A3Tools/Forms/QuickAddAccountDialog.cs` + `.Designer.cs`
- 标题栏：「🔖 一键添加账套」蓝色（#1891B0）
- 顶部 lblHint 提示：粘贴账套信息文本（在 Root 模式下复制账套信息后粘贴可识别全部字段，含密码）。代码自动分配，其他字段按「字段名：值」自动匹配
- 中间多行 txtPaste（Consolas 10.5pt，700x250）
- 底部 lblStatus（预留解析结果展示位置）
- 三个按钮（右对齐）：
  - 「切换为手动添加」：蓝色下划线链接样式，左下角，关闭当前窗体并通知 MainForm 弹 AccountDialog
  - 「取消」：白色边框，Esc 触发
  - 「确定添加」：蓝色填充，Ctrl+Enter 触发
- 快捷键：Ctrl+Enter 提交、Esc 取消（KeyPreview=true）

### `QuickAddAccountDialog.cs` 解析逻辑
- 按行 split（CRLF / LF / CR 都兼容）
- 每行查找第一个中英文冒号（`:` 或 `：`），提取 label + value
- `TrySetField` switch 匹配字段名（精确匹配）：
  - 「代码」→ 忽略粘贴的 code（自动分配）
  - 「名称」→ Name
  - 「账套地址」/「备用地址」/「账套用户名」/「账套密码」→ Server/ServerBackup/ServerUsername/ServerPassword
  - 「数据库地址」/「数据库名称」/「DB用户」/「DB密码」→ Database/DatabaseName/DbUser/DbPassword
  - 「远程方式」/「远程地址」/「远程用户」/「远程密码」→ RemoteType/RemoteAddress/RemoteUser/RemotePassword
- 未识别的行跳过（容错，不报错）
- 必校验：必须识别至少 1 个字段，且必须包含 Name（否则提示"缺少账套名称"）
- Code 自动分配：复用 AccountDialog.GenerateDefaultCode 逻辑（找现有最大 4 位数字 +1）
- Pinyin 自动计算：`PinyinHelper.GetPinyinInitial(Name)`
- 保存：`_dataService.AddAccount(account)`（DataService 内部自动加密密码字段）

### `A3Tools/Forms/MainForm.Designer.cs`
- BtnAdd 文字：`"➕ 新增"` → `"➕ 新增 ▾"`
- BtnAdd 关联：`btnAdd.ContextMenuStrip = this.addMenu`
- 新增字段 + 实例化 + 配置块：
  - `addMenu: ContextMenuStrip`
  - `miManualAdd: ToolStripMenuItem` 文字"手动添加" → Click `MiManualAdd_Click`
  - `miQuickAdd: ToolStripMenuItem` 文字"一键添加" → Click `MiQuickAdd_Click`

### `A3Tools/Forms/MainForm.cs`
- `BtnAdd_Click`：改为弹下拉菜单（`addMenu.Show(ctrl, 0, ctrl.Height)`）
- `MiManualAdd_Click` → `ShowAccountDialog(null)`（弹原有 AccountDialog）
- `MiQuickAdd_Click`：
  - 弹 QuickAddAccountDialog
  - 如果用户选"切换为手动添加" → 调 `ShowAccountDialog(null)`
  - 如果 OK → `LoadAccounts()` + `ShowToast("账套「xxx」已添加（代码 xxxx）")`

### `A3Tools.csproj`
- 新增 `<Compile Update="Forms\QuickAddAccountDialog.Designer.cs">` 嵌套关系（SDK 项目默认包含 .cs，但 Designer.cs 需要 DependentUpon 元数据让 VS 识别）

## 设计要点

- **下拉式触发**：BtnAdd 文字加 ▾ 符号提示可下拉，点击弹出 ContextMenuStrip，菜单项"手动添加"和"一键添加"
- **复制兼容**：解析逻辑兼容粘贴 Root 模式复制的完整账套信息（含密码），也兼容粘贴部分字段
- **Code 自动**：粘贴里的 code 字段被忽略（陛下明确要求"code 还是保持自动编码"），自动用 GenerateDefaultCode 生成下一个可用 4 位数字
- **字段容错**：未识别的行静默跳过（不报错），只对"完全没识别到任何字段"或"缺少 Name"时报错
- **切换手动**：一键添加窗体内提供"切换为手动添加"链接，应对用户粘贴之外想手动填的场景
- **快捷键**：Ctrl+Enter 提交、Esc 取消，KeyPreview=true 拦截键盘事件
- **依赖**：QuickAddAccountDialog 依赖 A3Tools.Models（Account）+ A3Tools.Services（PinyinHelper、DataService）

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，19 警告（NPinyin/CdpHelper/Win32AutoLoginHelper 等历史警告，与本次无关）

## 待测试（陛下测试验收）

1. 按钮文字变成「➕ 新增 ▾」（带下拉箭头），点击弹出菜单「手动添加 / 一键添加」
2. 选「手动添加」：弹原 AccountDialog，正常填表保存
3. 选「一键添加」：弹多行文本框窗体
4. 在 Root 模式下选个账套复制账套信息，回到一键添加窗体粘贴 → 点确定
5. 自动分配 Code（4 位数字递增），其他字段按「字段名：值」自动填充
6. 验证账套列表新增一行，Toast 提示「账套「xxx」已添加（代码 xxxx）」
7. 一键添加窗体点「切换为手动添加」→ 关闭 + 弹原 AccountDialog
8. 一键添加粘贴空文本/无法识别任何字段时提示错误
9. 粘贴只有名称没有其他字段也能成功（最少只要有名称）