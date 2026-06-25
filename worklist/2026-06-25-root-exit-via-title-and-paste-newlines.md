# 2026-06-25 Root 模式改用标题退出 + 一键添加粘贴保留换行

## 需求

1. 关闭 Root 模式不需要额外加小锁，A3工具箱（Root）字样前面不就有个锁吗，点这里就行，范围大点没关系
2. 一键添加账套，复制进输入框能不能保留换行（现在都在一行，虽然也能添加但看着怪）

## 实现

### 需求 1：标题即退出 Root 入口

**`A3Tools/Forms/MainForm.Designer.cs`**
- 删除 lblLock 控件所有相关：
  - 字段声明 `private Label lblLock = null!;`
  - 实例化 `lblLock = new Label();`
  - `titleBar.Controls.Add(lblLock);`
  - lblLock 控件初始化块（Anchor/Location/Font/Text/Visible/Click 等）
- lblTitle 加 `Cursor = Cursors.Hand`（提示用户可点击）

**`A3Tools/Forms/MainForm.cs`**
- 删除 `private ToolTip? toolTipRootExit = null!;` 字段
- 删除构造函数里 `toolTipRootExit = new ToolTip { ... }` 初始化
- `LblTitle_Click` 改造：
  - Root 模式 → 单击即调 `ConfirmExitRootMode()` 返回（不需要 5 次）
  - 非 Root 模式 → 保留原"5 次连点"逻辑（移除 `&& !_isRootMode` 条件）
- 新增 `ConfirmExitRootMode()` 方法（原 LblLock_Click 逻辑抽出来）
- `UpdateRootModeUI` 删除所有 lblLock / toolTipRootExit 相关代码
- 删除 `LblLock_Click` 方法

### 需求 2：粘贴保留换行

**`A3Tools/Forms/QuickAddAccountDialog.cs`**
- 重写 `WndProc` 拦截 `WM_PASTE (0x0302)`：
  - 只在 `ActiveControl == txtPaste` 时拦截
  - 从 `Clipboard.GetText()` 取文本（保留 \r\n）
  - 手动 `Remove(start, len).Insert(start, text)` 替换选区
  - 重置 `SelectionStart` + `ScrollToCaret()`
  - 阻止默认粘贴（直接 return，不调 base.WndProc）
- 不在 txtPaste 上挂事件（Form 级 WndProc 已能精准拦截 TextBox 焦点场景）

## 设计要点

- **标题退出范围**：陛下说"范围大点没关系"，所以单击 lblTitle 任何位置都触发
- **保持 5 次连点进入**：非 Root 模式时仍是 5 次连点，避免误触进入
- **lblTitle.Cursor = Hand**：视觉提示，让用户知道标题可点击
- **不需要 ToolTip**：陛下说"字样前不就有个锁吗"——所以靠图标本身就够提示
- **WndProc 拦截 WM_PASTE**：比 KeyDown 拦截 Ctrl+V 更稳，能处理右键菜单粘贴、SendKeys 等各种粘贴途径
- **ActiveControl 过滤**：只在 txtPaste 获得焦点时拦截，其他控件用 WinForms 默认行为

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，19 警告（历史警告无关）

## 待测试（陛下测试验收）

1. **Root 退出**：
   - 启动 → 默认小锁不在 titleBar 上（已删除 lblLock）
   - 进入 Root 模式（连点标题 5 次 + 密码 xiaopacai）→ 标题变黄 + "🔓 A3工具箱 (Root)" + 鼠标变手型
   - 单击标题任意位置 → 弹"确认要退出 Root 模式吗？" → 取消保持 / 确定退出
2. **粘贴换行**：
   - 一键添加窗体复制账套信息（含多行）→ 粘贴到 txtPaste → 看到多行文本（不再是单行）
   - Ctrl+V 和右键粘贴都保留换行
3. **保留 5 次连点**：
   - 非 Root 模式下连点标题 5 次依然触发密码弹窗
   - 第 5 次点完进入密码弹窗，弹窗关闭后 5 次计数已重置，下次连点 5 次才能再触发