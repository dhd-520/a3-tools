# 2026-07-04 修复 SQL IntelliSense 不弹 + 弹出位置错 + 行高截字

## 问题

陛下反馈 SQL 编辑器 IntelliSense 完全不弹，按 `Ctrl+Space` 也没反应，三个故障 + 最后一个修复细节：

| 故障 | 症状 | 根因 |
|------|------|------|
| ① IntelliSense 不弹 | 输入 `SELE` 等半天不出现候选 | IntelliSensePopup 用 `ToolStripDropDown` 实现，**默认抢焦点到内部 ListBox**，导致后续字母键进 ListBox 不进编辑器 → OnTextChanged 不再触发 → timer 不再启动 → 永远不弹 |
| ② VS `ITypeIdentityResolutionService` 异常 | VS 输出窗口提示"在 ITypeIdentityResolutionService 开始处理排队程序集之前，无法解析类型" | `IntelliSensePopup` 构造函数中 `Opacity = 0.98` 立即触发分层窗口创建；VS 设计器加载自定义控件字段时会自动 new IntelliSensePopup()，这个调用在 Designer 上下文里炸 ITypeIdentityResolutionService |
| ③ 弹出位置错（顶部卡在文字中间） | 弹出框最上方在输入文字中间，看着像挡住 | `SqlEditor.TriggerIntelliSense` 用 `(int)Font.Size + 4` 算纵坐标偏移 → `Font.Size` 是字号（10pt）不是行高（10pt 实际行高 ≈ 14-16px），popup 顶部落在第一/二行之间 |
| ④ 每行下方文字被切 | 输入 `g/p/q/y` 这些带 descender 的字母，下方笔画被截掉 | `DrawMode.OwnerDrawVariable` + `ItemHeight=20` + `Graphics.DrawString + Y+2`，10pt Consolas 文字 descent 区域超 Item 范围，底部被切 |

## 修复

### IntelliSensePopup.cs

| 改动 | 作用 |
|------|------|
| `ToolStripDropDown` → **普通 Form**（继承 `Form`） | 根因：ToolStripDropDown 抢焦点导致 IntelliSense 自毁 |
| `Show()` 后 `owner.BeginInvoke(... Focus())` 立即还焦点 | 下一轮字母键回到编辑器 → OnTextChanged → timer 重启 |
| `TopMost = true`（Hide 时回 `false`） | 防止 popup 被 SplitContainer / TabControl 遮挡 |
| `Opacity = 0.98` 延后到第一次 `ShowNearCaret` 才设 + `try/catch` 兜底 | 避免 ctor 阶段触发分层窗口 |
| 构造函数加 `LicenseManager.UsageMode == Designtime` 早返回 | VS 设计器加载字段时直接 return，不创建 Form Window Handle → 不再触发 ② 异常 |
| 所有方法 `_listBox` null-safe | 万一设计时/异常路径下被调用不抛 NRE |
| 屏幕边界检查（底部超出 → 翻到光标上方；右边超出 → 靠右贴齐） | 不同分辨率/窗口位置都看不到"被屏幕切"的 popup |
| `DrawMode.OwnerDrawVariable` → `OwnerDrawFixed` + `ItemHeight=22` | 强制按统一行高切分，杜绝变量行高引起的渲染异常 |
| `Graphics.DrawString + Y+2` → `TextRenderer.DrawText + VerticalCenter \| NoPadding` | 文字严格居中绘制，不被切 |
| 新增 `ClosePopup()`（Close 而非 Hide）+ Escape 时 `Owner.Focus()` | 完整关闭路径 + Esc 后焦点立刻回编辑器 |

### SqlEditor.cs

| 改动 | 作用 |
|------|------|
| IntelliSense 节流 150ms → **50ms** | "等半天不弹"观感改善 |
| 高亮节流与 IntelliSense 节流**独立判断**（`!_suppressHighlight` / `!_suppressIntelliSense` 分别判断） | 之前共用 `_suppressHighlight`，`ReplaceCurrentWord` 时整个 OnTextChanged 直接 return → IntelliSense 也不再触发 |
| popup 锚点改用**下一行首字符位置**，没有下一行时用 `Font.Height + 2` | `Font.Height` 是真正的行高（≈ 1.2 × `Font.Size`），解决 ③ 位置错 |

## 验证

- `dotnet build A3Tools\A3Tools.csproj -c Debug --nologo` → **0 错误**
- 输入 `SELE` 等关键字应正常弹出候选列表
- `↑↓` 可选中候选项（选中行蓝色高亮整行）
- 选中后按 `Enter` / `Tab` 触发补全（自动关闭 popup）
- 按 `Esc` 关闭 popup 并把焦点还给编辑器
- 按 `Ctrl+Space` 任何时刻触发补全

## 文件

- `A3Tools.Plugins.Default/Forms/IntelliSensePopup.cs` — 从 `class IntelliSensePopup`（组合 ToolStripDropDown + ListBox）改为 `class IntelliSensePopup : Form`（继承 Form + ListBox 直接 Controls.Add）
- `A3Tools.Plugins.Default/Forms/SqlEditor.cs` — 节流拆双轨 + 位置算法改

## 提交

即将 `fix(sql-query): 修复 IntelliSense 不弹 + 弹出位置错 + 行高截字` 提交

## 下一步

陛下拍板数据库对象联想（A 方案/无/F12 触发全表全列拉取）后接入。
