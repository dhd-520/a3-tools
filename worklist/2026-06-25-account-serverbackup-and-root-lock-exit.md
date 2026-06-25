# 2026-06-25 账套备用地址 + Root 模式小锁退出

## 需求

1. 账套下方增加备用地址；root 模式下复制账套信息时也要带上；账套用户名复制也要带上
2. Root 模式支持关闭，点击小锁提示关闭，确认后关闭 Root 模式

## 实现

### `A3Tools.Common/Models/Account.cs`
- 新增字段：`public string ServerBackup { get; set; } = string.Empty;` // 备用地址

### `A3Tools/Forms/AccountDialog.Designer.cs`
- 字段声明区加 `lblServerBackup = null!;` 和 `txtServerBackup = null!;`
- `InitializeComponent()` 中实例化、添加到 `contentPanel.Controls`
- 新增控件初始化块（Y=155，紧跟 Server 之下、ServerPassword 之上）
- 后续所有控件 Y 坐标 +45（ServerPassword 200 / Database 245 / DatabaseName 290 / DbUser 335 / DbPassword 380 / RemoteType 435 / RemoteAddress 480 / RemoteUser 525 / RemotePassword 570 / Remark 615）
- `pnlWebGroup` Y: 650 → 695
- `footerPanel` Y: 844 → 889
- `contentPanel.Size`: (942, 794) → (942, 839)
- `ClientSize`: (942, 924) → (942, 969)
- 后续 TabIndex 全部 +2（避免与新增控件 TabIndex 冲突）

### `A3Tools/Forms/AccountDialog.cs`
- `LoadAccount` 增加 `this.txtServerBackup.Text = account.ServerBackup;`
- `GetAccount` 增加 `ServerBackup = this.txtServerBackup.Text.Trim()`

### `A3Tools/Forms/MainForm.Designer.cs`
- 字段声明区加 `private Label lblLock = null!;`
- `InitializeComponent()` 中实例化、加到 `titleBar.Controls`
- 新增控件初始化块：
  - `Anchor = Top | Right`，位置 (975, 13) — 落在 `lblVersion` 左边
  - `Cursor = Hand`
  - `Font = "Segoe UI Emoji", 16pt, Bold`
  - `ForeColor = Yellow`
  - `Text = "🔓"`
  - `Visible = false`（默认隐藏，Root 模式开启才显示）
  - `Click += LblLock_Click`

### `A3Tools/Forms/MainForm.cs`
- 新增字段 `private ToolTip? toolTipRootExit = null!;`
- 构造函数中初始化：`toolTipRootExit = new ToolTip { AutoPopDelay = 3000, InitialDelay = 200 };`
- `CopyAccountInfo` 复制账套信息时增加 `备用地址` + `账套用户名`
- `CopySelectedAccountSilently` 同样增加 `备用地址` + `账套用户名`
- `UpdateRootModeUI`：
  - 进入 Root 模式：设置 `lblLock.Text = "🔓"`、黄色、Visible=true，绑定 ToolTip"点击退出 Root 模式"
  - 退出 Root 模式：隐藏 `lblLock`、清空 ToolTip
- 新增 `LblLock_Click` 事件：
  - 非 Root 模式直接 return（防御性，理论上 Visible=false 时不会触发）
  - 弹确认框："确认要退出 Root 模式吗？\n退出后将无法查看和复制明文密码。"
  - 确认后 `_isRootMode = false; _titleClickCount = 0; UpdateRootModeUI();`

## 设计要点

- **小锁位置**：titleBar 内 `lblVersion` 左侧，Anchor=Top|Right 跟随拉伸；用 🔓 emoji 而非图标资源，避免引入 PNG
- **小锁可见性**：仅 Root 模式可见，非 Root 模式控件 `Visible=false`，避免误触
- **ToolTip**：鼠标悬停小锁 200ms 后提示"点击退出 Root 模式"，3s 自动消失
- **退出确认**：MessageBox OKCancel 弹框，避免误触退出
- **复制补字段**：放在 `Server` 后 `ServerPassword` 前，符合账套-备用-密码的视觉流

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，2 个 NU1701 警告（NPinyin 历史警告，与本次无关）
- 编译产物：`A3Tools.dll` 正常输出到 `bin\Release\net7.0-windows\`

## 待测试（陛下测试验收）

1. 打开账套编辑对话框，能看到「备用地址」输入框（位于账套地址下方）
2. 编辑/新增账套时填写备用地址，保存后重新打开能看到内容
3. Root 模式下（标题点 5 次 + 密码 xiaopacai）标题栏右侧出现黄色 🔓，鼠标悬停有 ToolTip
4. 普通复制和 Ctrl+C 快捷复制都能带上「账套用户名」和「备用地址」
5. 点击小锁弹"确认要退出 Root 模式吗？"，取消则保持 Root，确定则退出（标题变白、小锁隐藏）