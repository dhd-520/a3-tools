# 2026-08-04 SQL 编辑器优化 (联想异步化 + 粘贴去格式 + Tab 缩进)

## 背景

陛下 09:40 反馈 5 个 SQL 编辑器问题:
1. 选中不好用, 老选多/选少, 手动调不了
2. 联想会卡顿导致无法编辑/操作 (联想毕竟是附带功能, 不应影响正常使用)
3. 粘贴带格式 (希望统一查询编辑框默认格式)
4. 需要支持 Tab 缩进 / Shift+Tab 反缩进 (选中文本也要支持)
5. 打开上千行存储过程每编辑一步都卡一会 (可能和 #2 有关)

## 修改总览

3 文件改:
- `A3Tools.Plugins.Default/Forms/SqlEditor.cs` +277 行
- `A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs` -4 行
- `A3Tools.Plugins.Default/Forms/SqlObjectSchemaCache.cs` +46 行

总 +328/-4 行。

## 问题 1: 选区改善 (本次跳过, 留 worklist 立项)

**判断**: 7/15 已设 `AutoWordSelection = false` 修了单词级吸附(主因)。陛下反馈"选多选少/手动调不了" 是 WinForms RichTextBox 本身的"行为"而非 bug:
- 点击落点会 normalize 到最近字符边界
- Shift+方向键扩展受 RichTextBox 自带规则影响
- 双击选词 / 三击选段 的边界由 RichTextBox 决定

**真正治本方案** (留 worklist 立项):
- 换 **ScintillaNET** 或 **AvalonEdit** 独立文本编辑器
- 支持: 虚拟空间/多选/列选/块选/语法高亮原生/代码折叠
- 工作量: 3-5 天 (替换 RichTextBox + 适配行号面板 + 测试所有快捷键)
- 风险: 需保留所有现有功能 (F12 跳转/查找替换/注释/缩进/智能提示)

**本次不做**: 工程量超出本次"快速优化"范围。

## 问题 2+5: 联想异步化 + 大脚本不卡 (合并修复, 同根因)

### 根因

`SqlIntelliSenseProvider.GetSuggestions` 入口同步调用 `SqlObjectSchemaCache.EnsureLoadedSync(connectionString, timeoutMs: 10000)` — 同步等最多 10s!

首次按一个键(EXEC/SELECT*)时:
- 缓存未就绪 → 同步等 10s
- 整个 UI 线程被卡 → 看起来"卡死"
- 第二次按键又来一次 → 又等 10s(因为缓存还是没就绪 — 同步等是按调用次数算, 第一次已经到 10s 但超时)
- 上千行存储过程 + 每次按键触发节流 50ms 后再调 → 每步都卡

### 修复 (3 文件协同)

**`SqlObjectSchemaCache.cs` 改造**:
- 删除同步 10s 等待路径
- 新增 `public static event Action<string>? Loaded` (缓存就绪事件)
- 新增 `public static bool IsLoaded(string connectionString)` (UI 线程快查)
- 新增 `public static void EnsureLoadingAsync(string connectionString)` (fire-and-forget 异步加载, 完成后触发 Loaded 事件)

**`SqlIntelliSenseProvider.GetSuggestions` 改造**:
- 删除 `EnsureLoadedSync` 调用
- 只读缓存, 未就绪返空
- 注释明确: 触发端订阅 Loaded 事件后重弹

**`SqlEditor.TriggerIntelliSense` 改造**:
- 拆出 `ShowIntelliSensePopup` (纯 UI 显示)
- 拆出 `TryStartAsyncReloadAndRepopup` (fire-and-forget 启动 + 订阅 Loaded 一次)
- 拆出 `RepopupIfStillRelevant` (缓存就绪后, 在 UI 线程重弹)
- 逻辑:
  - 第一次按 EXEC/SELECT: 缓存未就绪 → 立即 Hide + 启动后台加载
  - 缓存就绪后 (200-500ms): Loaded 事件触发 → 重弹
  - 用户已走开 (光标不在强上下文位置): 不重弹
  - 整个过程 UI 线程 0 阻塞

### 关键设计

- **重弹条件检查**: `RepopupIfStillRelevant` 重新调 `DetectContext` 检查当前光标是否仍在强上下文 (EXEC/SELECT*/FROM 后), 避免用户已输入其他字符还弹旧结果
- **一次性订阅**: `Loaded += handler` 后, handler 内部先 `Loaded -= handler` 再重弹, 避免重入
- **Invoke 检查**: Loaded 事件从 ThreadPool 触发, `RepopupIfStillRelevant` 用 `BeginInvoke` 切回 UI 线程
- **控件销毁保护**: `if (IsDisposed || !IsHandleCreated) return` 避免快速切 Tab 后访问已销毁控件

## 问题 3: 粘贴去格式 (WM_PASTE 拦截)

### 根因

`RichTextBox.Paste()` 默认会带 RTF 格式 (颜色/字体/段落), 复制外部带格式 SQL 进来, 编辑框字体/颜色乱。

### 修复

`SqlEditor.WndProc` 拦截 `WM_PASTE (0x0302)`:
- 拿 `Clipboard.GetText()` 纯文本
- 转换 `\r\n` / `\r` → `\n` (统一换行)
- 通过 `SelectedText = text` 插入 (保持当前 SelectionFont/SelectionColor, 不带外部格式)
- 吞掉原消息, 不调 `base.WndProc` 让默认 Paste 走

**WinForms 关键点**: WM_PASTE 是 SendMessage 触发的, 在 WndProc 拦截后必须 `return` 不调 base, 否则 base 还会走默认 Paste 流程。

## 问题 4: Tab 缩进 / Shift+Tab 反缩进 (含选区)

### 现状

- 7/15 注释里 "TODO: Tab 缩进 / Shift+Tab 反缩进" — 之前没做
- `Tab` 默认行为: 焦点切到下一控件 (Tab 导航)
- 联想 popup 显示时 Tab 优先选联想项 — **这个保留**

### 修复 (SqlEditor.cs 新增 3 方法)

- `HandleTabIndent(KeyEventArgs e)` (line 444)
- `HandleShiftTabIndent(KeyEventArgs e)` (line 487)
- `IndentMultipleLines(int lineStart, int lineEnd, bool addIndent)` (line 521)
- `DedentLine(int line)` (line 596)
- 共享常量 `private const string IndentText = "    "` (4 空格, 跟 HandleEnterWithIndent 一致)

### 行为表

| 选区状态 | Tab | Shift+Tab |
|---|---|---|
| 无选区 (caret 单点) | 在光标位置插入 4 空格 | 从光标所在行行首去最多 4 空格 |
| 单行选区 | 在光标位置插入 4 空格 (覆盖选区) | 从选区所在行行首去最多 4 空格 |
| 多行选区 | 每行行首加 4 空格 | 每行行首去最多 4 空格 (行首无 4 空格时该行不动) |

### 细节

- **多行选区"边界空行"问题**: 当选区末尾正好是换行符 (caret 选到行末 `\n` 之后), 跨行范围 -= 1, 避免空行被加缩进。这是 VS / SSMS 通用行为
- **去缩进只去空格不动 \t**: SQL 很少用 tab, 改了风险大; `\t` 不在去缩进逻辑里
- **跟联想冲突**: `if (_intelliSense.IsVisible) Tab/Enter 优先选联想项` 的逻辑 (line 287-326) 保持不变; popup 不显示时才走缩进
- **跟注释/查找/格式刷等冲突**: 无 — 注释用 `Ctrl+/`, 查找用 `Ctrl+F`, Tab 不冲突

## 编译

- `dotnet build A3Tools.sln -c Release` ✅ **0 错** (315 警告, 都是历史项目警告, 跟上一轮一样)
- 1 个修复: SqlEditor.cs 缺 `using System.Text;` (因 IndentMultipleLines 用了 StringBuilder)

## 验证场景

### 问题 2/5
- 打开新账套 → 缓存未就绪 → 按 EXEC 空格 → 立即 Hide (不卡 UI) → 300ms 后缓存就绪 → 自动重弹存储过程列表
- 加载 1000+ 行存储过程 → 按字符 → 不再卡, 50ms 后联想异步触发
- 切库 → 缓存 invalidate → 按 EXEC → 同样走异步重弹路径

### 问题 3
- 复制外部带颜色的 SQL → 粘贴到编辑器 → 字体/颜色跟随当前编辑器 (没有红/蓝/绿乱入)
- 选中文本粘贴 → 覆盖选区, 不带外部格式

### 问题 4
- 单行按 Tab → 插入 4 空格 (光标右移)
- 选 3 行按 Tab → 每行行首加 4 空格
- 选 3 行 (有缩进) 按 Shift+Tab → 每行行首去 4 空格
- 行首无 4 空格 (只有 2 空格) 按 Shift+Tab → 去 2 空格, 不报错
- 联想 popup 显示时按 Tab → 优先选联想项, 不缩进

## 关键设计取舍

- **问题 1 不动**: 工程量超出本次范围, 留 worklist 立项
- **问题 2/5 用 Loaded 事件而非轮询**: 事件驱动零开销, 比 `Application.Idle` 或 Timer 轮询干净
- **粘贴拦截在 WndProc 而非 OnPaste**: `RichTextBox` 没有 OnPaste 虚方法
- **Tab 缩进用 SelectedText 插入**: RichTextBox 走 SelectionText 设置, 自然走 "删除选中 + 插入" 模式, 不需要管光标位置恢复

## Git

未 commit (等陛下决定 commit 信息和版本号 v2.4.7 → v2.4.8?)

## 后续可选 (本次未做)

- 缩进宽度可配置 (SettingsDialog 加一个 "Tab 宽度" 选项)
- 缩进用 tab 字符 (`\t`) 而非 4 空格 (VS 风格可切换)
- 问题 1 立项: 换 ScintillaNET 解决选区/语法高亮原生支持
