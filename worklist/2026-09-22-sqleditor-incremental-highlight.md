# SqlEditor 增量高亮 - 修复大文档卡顿

**日期**: 2026-09-22
**项目**: D:\work\A3Tools
**修复文件**: A3Tools.Plugins.Default\Forms\SqlEditor.cs
**陛下反馈**: "内置查询工具当内容多时，就变得很卡，输入一个内容要等半天才有反应。1000 行左右的存储过程就非常明显", "可不是 500ms，我感觉每按下一次按键我等几秒钟甚至十几秒才能反应过来"

## 问题定位

`SqlEditor.Highlight()` 是 SQL 编辑器的语法高亮入口。每次按键触发 `OnTextChanged` → 200ms 后 `_highlightTimer.Tick` → `Highlight()` 全量重算。

**真正的瓶颈**（实测 1000 行存储过程 ≈ 50K 字符）：
```csharp
Select(0, TextLength);              // 选全文
SelectionColor = Color.Black;       // ★ 逐字符改 RTF 颜色属性
// 然后 5 轮正则 + 几千次 Select + SelectionColor
```

`SelectionColor = Black` 内部是 richEdit **逐字符改 RTF 颜色数据**,即使 `WM_SETREDRAW=0` 也只是不画,**改数据本身没法跳过**。50K 字符 × 改属性 = **几秒到十几秒级**。

## 修复方案:增量高亮

把"按一个键 → 全量重算"改成"按一个键 → 只重算光标附近 ±N 行"。

### 改动点 (一个文件,约 80-100 行)

1. **加 3 个字段**:
   - `_pendingLineFrom` / `_pendingLineTo` — 待高亮的行范围
   - `_lastTextLength` — 用于检测"批量插入"(粘贴/加载文件)

2. **改 `OnTextChanged`**:
   - 总是记录 `_pendingLineFrom/_pendingLineTo`(即使 `_suppressHighlight`)
   - 根据 `delta = TextLength - _lastTextLength` 判断:
     - `|delta| <= 20`(单字符/单词):范围 = 当前行 ±3/±50
     - `|delta| > 20`(粘贴/批量删除):范围扩大覆盖整个插入区域

3. **改 `Highlight()` 签名**:
   - `private void Highlight(int fromLine, int toLine)`
   - 只对 `[fromLine, toLine]` 行范围做"重置+设色",而不是全文
   - 5 个正则的 `m.Index + startIdx` 加偏移量

4. **改 Timer Tick**:
   - 用 `_pendingLineFrom/_pendingLineTo` 调 `Highlight(from, to)`

5. **改 `HighlightNow()`**:
   - 全文高亮入口,调 `Highlight(0, lineCount - 1)`
   - 用于"打开大文件 / 显式刷新"场景

## 边界处理

- **拖选抑制**:`MouseButtons != MouseButtons.None` 早 return(沿用原逻辑)
- **选中恢复**:每个 Highlight 范围用 `selStart/selLen` 保存,最后 `Select(selStart, selLen).SelectionColor = Black` 恢复
- **粘贴/批量**:粘贴时即使 `_suppressHighlight`,也要更新 `_pendingLineFrom/_pendingLineTo`,这样后续 timer tick 用新范围
- **跨行删除残留颜色**:大批量删除时 |delta|>20 触发,够用
- **空文档 / 没行**:`GetLineCount() == 0` 早 return

## 预期收益

| 场景 | 当前 | 改后 |
|------|------|------|
| 1000 行输入 | 几秒 ~ 十几秒 | < 100ms (只重算 50 行) |
| 100 行输入 | ~500ms | < 30ms |
| 打开大文件 | 全量一次性卡 | 仍全量(只在打开时) |

## 验证

- 编译 `dotnet build` 0 错
- 手动测试:打开 1000 行存储过程,连续打字,响应 < 100ms
- 边缘 case:粘贴大段 SQL、高亮立即生效
- 边缘 case:删除跨多行,周围字符颜色无残留

## 状态

- [x] 创建 worklist
- [x] 改 SqlEditor.cs 增量高亮 (3 处)
- [x] 改 LineNumberPanel 宽度 (1 处)
- [x] 改 HighlightNow 跳过初次重置 (2 个文件)
- [x] 改 HighlightNow 可见区域优先 + 后台异步 (1 个文件)
- [x] 编译通过 (0 错, 319 警告其中 1 个可能是新增私有字段引用警告)
- [ ] 陛下手动验证 ← **等陛下 F5 试**
- [x] 更新 MEMORY.md

## 实施结果 (2026-09-22)

**第一次编译** (增量高亮改完):
```
0 个警告, 0 个错误。用时 00:00:01.48
```
踩坑:RichTextLine 没有 `GetLineCount()` 方法,用 `Lines.Length` 代替 — 3 处全改。

**第二次编译** (行号面板宽度改完):
```
0 个警告, 0 个错误。用时 00:00:19.54
```

**第三次编译** (跳过初次重置改完):
```
318 个警告 (历史遗留, 0 新警告), 0 个错误。用时 00:00:17.90
```

---

## 子任务 2: 行号面板宽度 - 修复 "99 后又到 00" 截断

**陛下反馈**: "行号有问题最多支持两位,99 之后又到了 00,是不是 1 没显示出来"

**根因**:`LineNumberPanel.SyncFontSize` 只在 FontSizeChanged 时算 Width,文件加载/粘贴后 Lines.Length 涨了但 Width 没动 → 100/1000 等高位行号的"1"被 `DrawString` 的 `Width - size.Width - 6` 计算裁掉。

**改动** (`SqlEditor.cs` 内 `LineNumberPanel` 类):
- 加字段 `_lastWidthLineCount = -1`(缓存上次算宽度的行数,行数没变就跳过)
- 加方法 `UpdateWidthIfNeeded()` — 用 `TextRenderer.MeasureText(maxLineText, boldFont)` + 边距 14,Bold 因为当前行是粗体
- `SyncFontSize` 清缓存 + 调 `UpdateWidthIfNeeded`
- `OnViewChanged` 之前 `=> Invalidate()`,改成 `UpdateWidthIfNeeded(); Invalidate();`
- 保底宽度 44(原代码小文件用的宽度)

**预期效果**:
- 1-99 行 → Width 44
- 100-999 行 → 自动撑到 3 位数字宽度
- 1000-9999 行 → 自动撑到 4 位数字宽度
- 字号缩放 → Width 跟着字号自适应

---

## 子任务 3: 跳过初次重置 - 修复 "首次加载 6-7 秒" 慢

**陛下反馈**: "文本多首次加载慢问题能解决么？1000 行左右的存储过程,打开要等六七秒左右才能操作"

**根因**:`SqlQueryTabPage.SetEditorText` 流程:
1. `editor.Text = "1000 行 SQL"`(~1.5s,RTF 解析)
2. `editor.HighlightNow()` → `Highlight(0, 999)`(~5s)
   - `Select(0, 50000).SelectionColor = Black` 是元凶 — richEdit 逐字符改 RTF 颜色,50000 次属性写入
   - **白做功**:`editor.Text = "..."` 后所有字符默认就是黑色

**方案**:**跳过初次重置**(陛下拍板 A 方案)

**改动** (2 个文件):
- `SqlEditor.cs:118` `HighlightNow()` 加 `bool resetColors = true` 参数
- `SqlEditor.cs:944` `Highlight(int fromLine, int toLine)` 签名加 `bool resetColors = true`,把 `SelectionColor=Black` 包在 `if (resetColors)` 里
- `SqlQueryTabPage.cs:288` `rtbEditor.HighlightNow()` → `rtbEditor.HighlightNow(resetColors: false)`

**关键**:
- 默认 `resetColors = true` → SearchReplaceDialog 等调用点行为不变
- 5 轮关键字/字符串/数字/注释设色**仍然全跑** → 高亮效果完整保留

**预期收益**:
| 场景 | 当前 | 改后 |
|------|------|------|
| 1000 行首次加载 | 6-7s | < 1s |
| 1000 行增量编辑 | < 100ms | < 100ms |
| 查找替换刷新 | 重置+重设色 | 不变 |