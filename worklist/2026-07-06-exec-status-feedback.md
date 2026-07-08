# 2026-07-06 SQL 执行结果状态反馈（Tab 图标 + 状态栏颜色 + 自动切消息）

## 陛下反馈

> "直接打开对象，修改后执行。结果里无任何提示。也不知道执行成功没成功，看看在那里加个提示"

## 根因

`SqlQueryTabPage.ExecuteAsync` 完成后只在底部 `statusStrip` 上写一行小字：

```
✓ 执行成功，影响 X 行    耗时: 230 ms    影响: 0 行
```

**问题：**
- 状态栏字号小、颜色黑（跟其他文字没区别），用户盯结果区根本注意不到
- **DDL 语句（ALTER PROC / CREATE TABLE）没有返回结果集** → 结果 Tab 是空白
- 状态栏不刷新颜色 → 成功失败看起来都一样
- 用户**不知道有没有执行成功**

## 解法 — 三处视觉反馈

### 1. Tab 标题加状态图标（最显眼）

| 状态 | 图标 | 场景 |
|------|------|------|
| `Running` | `⏳` | 执行中 |
| `Success` | `✓` | 全部批次成功 |
| `Failure` | `✗` | 任一批次失败或顶层异常 |
| `Cancelled` | `⏸` | 用户点 ⏹ 停止 |
| `Idle` | （无） | 未执行或已被编辑覆盖 |

示例 Tab 标题：`Table.SalesOrder ✓` / `View.vCustomer ✗`

实现：`Page.Text` 加后缀，TabControl 自绘（`DrawMode.OwnerDrawFixed`）会重画。

图标在编辑器内容修改时被清掉（`TextChanged → SetTabStatusIcon(Idle)`），
因为上次结果已经过期。

### 2. 状态栏文字变彩色（即时）

| 状态 | 颜色 | 色值 |
|------|------|------|
| `Success` | 绿 | `#39b54a` |
| `Failure` | 红 | `#fb432a` |
| `Cancelled` | 灰 | `#8a8f98` |
| `Running` | 蓝 | `#1ba1e2` |
| 其他 | 默认 | `ControlText` |

色值与项目其他位置一致（成功/选中/失败）。

### 3. 失败自动切到「消息」Tab

**改前：** 只有 `!hasResult`（DDL 无结果集）才切到消息 Tab → 有 SELECT 结果但后续批次失败时**不切**，用户看不到错误详情

**改后：** 任何失败路径（部分批次失败 / 顶层异常）都切到消息 Tab → 错误详情一眼可见

## 改动文件

### `SqlQueryTabPage.cs`

1. **新增 `ExecStatus` 枚举**（5 个值）
2. **`_statusReporter` 类型扩展**：从 `Action<string, long, int>` 改为 `Action<string, long, int, ExecStatus>`
3. **新增 `SetTabStatusIcon(ExecStatus)` 方法**：更新 `Page.Text` 加后缀图标；相同文本不重写避免 Tab 重绘闪烁
4. **`InitEditor` 订阅 `rtbEditor.TextChanged`**：用户编辑时清掉旧图标
5. **`SetEditorText` 用 `_suppressStatusClear` 标志**：程序性加载脚本（OpenScript 流程）不触发清状态
6. **`ExecuteAsync` 各退出路径**：
   - 入口：`SetTabStatusIcon(Running)` + `_statusReporter("执行中...", 0, 0, Running)`
   - 成功：`Success` + `"✓ 执行成功，影响 X 行"`
   - 部分失败：`Failure` + `"✗ 部分批次失败（N 批），成功 X 行"` + 切消息 Tab
   - 取消：`Cancelled` + `"⏸ 已停止"`
   - 异常：`Failure` + `"✗ 执行失败：<msg>"` + 切消息 Tab

### `SqlQueryForm.cs`

1. **`UpdateStatus` 签名扩展**：`string, long, int, ExecStatus`
2. **状态栏颜色**：按 `ExecStatus` 设 `lblStatus.ForeColor`

## 验证

- `dotnet build`：0 错误（237 个 warning 全是历史 nullable 警告）
- 场景：
  1. Explorer 双击 `dbo.SalesOrder` → 打开 ALTER PROC 脚本
  2. 改一处代码 → 按 F5
  3. Tab 标题变成 `dbo.SalesOrder ⏳` → `dbo.SalesOrder ✓`
  4. 状态栏出现 `✓ 执行成功，影响 0 行`（绿色 #39b54a）
  5. DDL 没结果集 → 自动切到消息 Tab
  6. 故意写错语法 → 按 F5 → Tab 标题 `✗` + 状态栏红色 `✗ 执行失败：...` + 自动切消息 Tab
  7. 在编辑器里随便改一下 → Tab 标题的 `✓` / `✗` 立刻消失（结果已过期）

## 待陛下回归

- [ ] 场景 A：双击对象 → 改 → F5 → Tab 图标 + 状态栏颜色都正确
- [ ] 场景 B：故意写错 → F5 → 红色提示 + 自动切到消息 Tab
- [ ] 场景 C：F5 执行中点 ⏹ → `⏸` 图标 + 灰色状态栏
- [ ] 场景 D：跑完后再编辑 → 图标消失（说明结果已过期）

## 后续可考虑

- **音效**：成功 `SystemSounds.Asterisk.Play()`、失败 `Hand.Play()` —— 等陛下发话再加
- **图标带颜色**：现在 ✓/✗ 都是 Tab 标题文字同色（黑/灰），可以画彩色 ✓/✗ 更醒目 —— 需要扩展 `TabControl_DrawItem` 绘制逻辑，工作量大
- **Tab 切换时刷新状态栏**：现在切到未执行过的 Tab 会保留上一个 Tab 的状态 —— 可以加 `SelectedIndexChanged` 监听 + 每 Tab 记 LastStatus