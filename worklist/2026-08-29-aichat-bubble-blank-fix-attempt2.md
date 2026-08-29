# 2026-08-29 AI 聊天气泡空白 - 尝试修复 #2

## 状态
🟡 Build 成功 0 错；等陛下手动测试

## 陛下反馈（08:57 第二次）

### 修复 #1 结果
- `[RenderDone] totalHeight` 从 2082 → **2068** ✓（少了 14px，rowSpacing 多算修掉）
- 最后一行 `Δ` 从 -36 → **-22**
- **但空白还在 + 出现了水平滚动条**

## 根因分析

### 剩余 22px 空白
- `lastRow.Bottom = 2046`
- `totalHeight = 2068 = lastRow.Bottom + Padding.Bottom(22)`
- **根因**：`splitChat.Panel1.Padding = (22,22,22,22)`，底部 22px 过大
- AI 聊天不需要底部这么多 padding（行间有 rowSpacing=14，气泡本身有 Padding=20）

### 水平滚动条
- `panelClientW = 1597`，row.Width = 1597，bubble.Width = 1316
- **理论上不应该出现水平滚动条**
- 但 WinForms Panel 在 `AutoScroll=true` 时，**layout 过程中会按子控件 DisplayRectangle 自动计算 AutoScrollMinSize**
- 当 Panel1 进入 layout（滚动条出现 → ClientSize.Width 变小 → 触发 re-layout）→ 可能短暂出现水平滚动条
- 强制关闭 HorizontalScroll 解决

## 改动

### 改动 1：构造函数减小 Panel1 Padding
**位置**：`AiChatForm.cs` 行 73

```csharp
// 之前：new Padding(22)（四方向都是 22）
// 之后：new Padding(22, 14, 22, 4)
//   左/右 = 22（保持）
//   上 = 14（从 22 减小）
//   下 = 4（关键！从 22 大幅减小）
```

### 改动 2：强制关闭水平滚动
**位置**：`AiChatForm.cs` 行 76~77

```csharp
splitChat.Panel1.HorizontalScroll.Maximum = 0;
splitChat.Panel1.HorizontalScroll.Visible = false;
```

### 同时保留之前的改动
- RenderMessages 的 `lastRow.Bottom + Padding.Bottom` 修法（去掉 rowSpacing double count）

## 预期效果
- AI 长气泡滚到底部，**下方空白从 22px → 4px**（基本贴底）
- 水平滚动条不再出现
- 短 AI 气泡下方也不再有"明显空白"

## 陛下下一步验证
1. F5 启动
2. 加载历史会话（含长 AI 消息的那条）
3. 滚到底部
4. 看：
   - AI 长气泡下方空白是否消失？
   - 水平滚动条是否消失？
   - 输出窗口 `[RenderDone]` 数据

## 如果还有空白
→ 用 `splitChat.Panel1.AutoScrollMinSize = new Size(0, lastRow.Bottom)` 完全去掉 padding
→ 但要注意这样滚到底可能"贴得太死"（最后一个 row 顶部 Padding=0 不美观）

## Build
- 0 错（padding 命名空间用 `Padding` 不是 `System.Drawing.Padding`，因为 .NET 7 WinForms Padding 在 System.Windows.Forms 下）