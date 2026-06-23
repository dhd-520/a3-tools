# 2026-06-22 账套列表敏感字段脱敏显示

## 需求
主界面账套信息列表中显示的 **数据库地址、数据库名称、DB用户、远程地址、远程用户名**
正常状态下显示 `***`，只有 Root 模式下才显示明文。

## 实现

### `A3Tools/Forms/MainForm.cs`

1. **敏感列名单**（类字段，`HashSet<string> _sensitiveColumnNames`）：
   - `Database` / `DatabaseName` / `DbUser` / `RemoteAddress` / `RemoteUser`
   - 共用 `MaskedPlaceholder = "***"` 常量

2. **新增 `DgvAccounts_CellFormatting` 事件处理器**：
   - 通过 `col.DataPropertyName` 判断是否敏感列（避免维护两份列名映射）
   - `_isRootMode == true` → `return`（让默认绑定生效）
   - `_isRootMode == false` → 空值保持空白，非空统一 `***`，设 `e.FormattingApplied = true`
   - 通过 `Reflection` 读 `Account` 属性（不用硬编码 switch，少一个映射字典）
   - 安全性：行/列 index 都做了越界保护

3. **注册事件**：`InitializeComponent` 中追加
   `this.dgvAccounts.CellFormatting += DgvAccounts_CellFormatting;`

4. **`UpdateRootModeUI` 增加 DataGridView 重绘**：
   - 切换 Root 模式后调 `this.dgvAccounts.Invalidate()`，触发单元格重绘
   - 重绘会再次走 `CellFormatting`，按当前 `_isRootMode` 决定脱敏/明文
   - 空值/Disposed 守卫

### 设计要点

- **不动数据层**：Account 模型、`DataService`、搜索过滤、剪贴板复制全部不受影响
  - 搜索关键字过滤仍按明文进行（用户输关键字找得到，只是看不到值）
  - 双击编辑弹 `AccountDialog`，里面照样显示明文（用户在编辑当然要看）
- **不动列结构**：`SetupDataGridViewColumns` 不改，DataPropertyName 仍是真实属性，
  CellFormatting 是 WinForms 标准的「最后一公里」定制点
- **不在 `LoadAccounts`/`SearchAccounts` 里改**：那两个方法不知道 `_isRootMode`，
  而且会因为模式切换后需要重新刷新而引入状态管理复杂度

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，149 警告全是历史 nullable 警告，与本次改动无关
- 重新打包 `StandaloneSF`：
  - `A3Tools.exe` 77,676,273 bytes（74.06 MB）
  - 总大小约 76.05 MB
  - `Plugins/` 含 `A3Tools.Common.dll` + `A3Tools.Plugins.Default.dll` + `tools.json`
  - 3 个 pdb 全保留，输出目录无 `*.log` 和 `~$*` 残留

## 待测试（陛下测试验收）

- 正常启动：账套列表 5 个敏感列都显示 `***`（账号本身 Code/Name/Server 不受影响）
- 双击编辑：弹窗里仍可见明文（因为 AccountDialog 接收 isRoot 参数）
- 进入 Root 模式（标题连点 5 次 + 输密码 xiaopacai）：5 个敏感列立刻恢复明文
- 退出 Root 模式：立刻重新脱敏，无需重启
- 搜索过滤：搜关键字仍能命中（虽然显示 `***` 看不到值，但能定位到行）
