# 2026-08-29 AI 聊天气泡空白（诊断日志阶段）

## 状态
🟡 Build 成功，0 错；等陛下手动发长 AI 回复后看日志

## 目标
昨天连续猜 4 轮都失败，今天按陛下原话：
> "先看诊断日志再说，不要再靠猜"

## 改动
**唯一改动：在 `CreateBubbleRow` 末尾加完整诊断日志**

### 触发点
`D:\work\A3Tools\A3Tools\Forms\AiChatForm.cs` 行 392~402

### 打印字段（陛下昨天列的 4 个检查点 + 我加的几个辅助值）
| 字段 | 含义 | 检查点 |
|------|------|--------|
| `contentLen` | 文本字符数 | — |
| `labelMaxW` | 气泡 TextBox 允许的最大宽度 | — |
| `contentW` | 测出来的 TextBox 实际宽度 | 4 |
| `contentW - labelMaxW` (Δ) | 宽度是不是按测量值走（应为 ≤0） | 4 |
| `bubble.{W,H}` | 气泡 Panel 尺寸 | — |
| `bubble.Padding L+R / T+B` | 气泡 Padding 总和 | — |
| `row.{W,H}` | row 高度 = max(bubbleH, avatarH) + verticalPadding*2 | 2 |
| `panelClient.{W,H}` | splitChat.Panel1 的实际客户端区 | — |
| `autoScrollMinSize.{W,H}` | 滚动容器的最小滚动尺寸 | 3 |
| `row.Bottom` | row 实际底 Y | — |
| `row.Bottom - autoScrollMinSize.Height` (Δ) | **滚动容器是否触发**（应为 0 或接近 0） | 3 |

`MeasureTextRobust` 调用前也加了一行：
- `fontH` = Font.Height
- `contentH / fontH` = **理论应该有多少行**（如果远大于 1 但 contentH 偏小 → 测量值错）

### 调用源区分（陛下 08:52 提醒：加载历史也要记录）
- `CreateBubbleRow` 新增第 3 个参数 `source`（默认 `"AddNew"`）
- `Render`（加载历史/重画）→ 来源于 `RenderMessages` 内的循环
- `AddNew`（发新消息追加）→ 来源于 `AddMessageBubbleNew`
- 日志前缀：`[Row:Assistant|Render]` vs `[Row:Assistant|AddNew]`，一眼区分

### 加载历史专属日志（RenderMessages 末尾新增）
`[RenderDone] msgCount=... totalHeight=... autoScrollMinSizeH=... panelClientH=... (溢出需要滚动=...)`

关键看：
- `autoScrollMinSizeH` 必须 == `totalHeight`（如果不等 → 渲染时算多了）
- `溢出需要滚动=true` 且内容少（msgCount 小）时 → `totalHeight` 偏大是直接证据

## 关键路径
- 输出位置：Visual Studio「输出窗口」→ 选「调试」
- 触发动作：发一条 AI 回复（让 AI 回**长文本**，越长越好，比如"详细说说 .NET 7 的特性"）

## 陛下需要做的事
### 场景 A：发新消息触发（昨天测过的）
1. VS 打开方案，**运行**（F5），打开 AI 助力窗口
2. 打开 VS「输出窗口」选「调试」
3. 发一条能让 AI 回**长内容**的消息
4. 看到 AI 气泡下方出现空白后，**截图**输出窗口里 `[Row:Assistant|AddNew]` 开头的几行

### 场景 B：加载历史触发（陛下 08:52 提醒）
1. 发 1~2 条消息 → 关闭 AI 窗口
2. 重新打开 AI 窗口 → **点历史会话列表里的旧会话**（加载历史）
3. 看有没有空白 → **截图** `[Row:Assistant|Render]` 开头的几行 + `[RenderDone]` 行

## 下一步（拿到日志后）
按 4 个检查点逐一分析：
- **如果 `bubble.Height` 远大于 `contentH` + Padding** → bubble 本身算多了（Panel Padding/Dock 问题）
- **如果 `row.Height` 远大于 `bubbleH` + verticalPadding*2** → row 算多了
- **如果 `row.Bottom - autoScrollMinSize.Height` 是大负数** → AutoScrollMinSize 没更新到 row.Bottom
- **如果 `contentH / fontH` 远大于实际行数** → TextBox.GetPreferredSize 测多了（昨天刚换的）

## 已知提示（陛下昨天总结的臣的问题）
- 连续 4 轮改法都在改"看似相关"的地方（pnlMessages 中间层、Graphics.MeasureString），没真看数值
- 今天原则：**看日志，不猜**

## Build
- 0 错（2 个 warning 是项目既有的：`rowSpacing` 未使用 + 拼音包版本警告）
