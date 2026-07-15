# 2026-07-15 SQL 查询工具：合并执行按钮 + 修复选中跳选

## 问题
1. 陛下反馈 SQL 查询工具有"执行"和"执行选中"两个按钮，希望合并成 SSMS 风格的一个按钮。
2. 选中操作"经常多选跳选、像不听使唤"。

## 根因
1. **双按钮**：`SqlQueryTabPage.Designer.cs` 里 `btnExecute` + `btnExecuteSelected` 两个按钮，`PerformExecuteSelected` 已经有"无选中执行全部"逻辑但带 `[提示]` 噪音。两个入口用户不知道该用哪个，UI 也冗余。
2. **选中跳选**：`SqlEditor` 继承 `RichTextBox`，而 `RichTextBox.AutoWordSelection` 默认 `true`。拖选鼠标会"吸附"到单词边界，导致选区莫名其妙扩大到整个单词、或者跨单词跳字。VS / SSMS 用的都是 `false`（字符级精确选择）。

## 修改

### SqlQueryTabPage.Designer.cs
- 删除 `btnExecuteSelected` 按钮（声明 + 初始化 + 事件 + Controls.Add）
- 加宽 `btnExecute`：105 → 120（凸显主按钮）
- 调整后面按钮位置紧凑：Stop 132, Save 239, lblHint 362
- 改 lblHint 文案：`F5=执行(有选中则执行选中,否则执行全部)`

### SqlQueryTabPage.cs
- 新增 `PerformExecuteSmart()`：有 `SelectedText` 走选中，否则走 `Text`，无 `[提示]` 噪音
- 保留 `PerformExecuteAll()` / `PerformExecuteSelected()` 作为薄包装路由到 `PerformExecuteSmart()`（防外部调用破）
- `BtnExecute_Click` → `PerformExecuteSmart()`
- 删除 `BtnExecuteSelected_Click`
- 删除 ExecuteAsync / ExecuteViaDataAccessAsync 两处 finally 块里的 `btnExecuteSelected.Enabled = true;`

### SqlEditor.cs
- 构造函数加 `AutoWordSelection = false;`
- 双击选词 / 三击选段不受影响（这俩走的是 `RichTextBox` 的 WM_LBUTTONDBLCLK / 三击逻辑，不归 `AutoWordSelection` 管）

### SqlQueryForm.cs
- 顶栏快捷键：`F5` 和 `Ctrl+F5` 都路由到 `PerformExecuteSmart`（保留 Ctrl+F5 别名防肌肉记忆断档）

## 验证
- `dotnet build A3Tools.sln` → 0 错 19 警告（警告全为既有）
- 插件 DLL 单独编译 → 0 错 313 警告

## 备注
- `AutoWordSelection = false` 是 RichTextBox 类全局的"字符精确选择"开关，对 IntelliSense 弹窗、双击选词、Shift+方向键扩展选区都无影响
- 没改 `HideSelection`（当前 `false` = 失焦保留选区高亮，符合 SSMS 习惯）
