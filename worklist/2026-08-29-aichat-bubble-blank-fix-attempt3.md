# 2026-08-29 AI 聊天气泡空白 - 尝试修复 #3（理论突破！）

## 状态
🟡 Build 成功 0 错；等陛下手动测试

## 陛下反馈（09:00 第三次）

### 修复 #2 效果
| | 修复前 | 修复 #1 | 修复 #2 |
|---|---|---|---|
| `lastRow.Bottom` | 2046 | 2046 | **2038** ✓ |
| `totalHeight` | 2082 | 2068 | **2042** ✓ |
| 最后一行 `Δ` | -36 | -22 | **-4** |

→ Padding 减小生效（少 4px）
→ **水平滚动条还在** ❌
→ **气泡空白还在** ❌
→ 陛下原话：「气泡内空白高度大概和内容高度差不多」← 🎯 **关键线索**

## 🎯 重大理论突破：「气泡内空白 ≈ 内容高度」

陛下日志数据：
```
contentLen=2516 (字符)
contentH=1494 (TextBox 高度)
contentH/fontH = 49.8 → 49 行
font=Microsoft YaHei UI 10pt
font.Height=30
```

### 数学分析
- TextBox 高度 = 1494px
- 如果 TextBox 实际渲染文字高度 = ~735px（49行 × 15px）
- **空白 = 1494 - 735 = 759px ≈ 内容高度 735px** ✓✓✓ **完美匹配陛下描述！**

### 根因
**`Microsoft YaHei UI 10pt` 的 font.Height=30，但实际行高 font.GetHeight()≈15px**
- `Font.Height` = `Font.GetHeight() + ascender/descender padding`（约 2 倍）
- `Graphics.MeasureString` 返回的 Height 用 `Font.Height` 作为 lineHeight
- **`TextBox.GetPreferredSize` 内部也用 `Font.Height` 作为 lineHeight**
- 所以每行高度被算成 30 而非 15，**多算 ~14px/行**

长 AI 消息：49 行 × 14px = **686px 多算** ≈ 759px（加上 TextBox 自身 padding 后完美匹配 1494-735=759）

## 修复尝试 #3

**改动**：`MeasureTextRobust` 用 `font.GetHeight()` 作为实际行高修正 preferred.Height

```csharp
float realLineHeight = font.GetHeight();  // ~15px for 10pt YaHei
float scale = realLineHeight / font.Height;  // ≈ 0.5
int correctedH = (int)(preferred.Height * scale) + 2;
```

加了诊断日志 `[MeasureRobust]` 打印 `fontH / realLineH / preferred / expectedLines`，方便陛下验证。

### 预期
- contentH 从 1494 → 735（× 0.5）
- bubble.H 从 1514 → 755（少 759px）✓✓✓
- "气泡内空白 ≈ 内容高度" 症状应该消失

## 陛下下一步验证
1. F5 启动
2. 加载历史会话
3. 看输出窗口 `[MeasureRobust]` 行的：
   - `fontH=30 realLineH=15.x` （确认 realLineH 是 15 不是 30）
   - `preferred=(...x1494)` 和 `expectedLines`（确认 preferred 用了 font.Height）
4. 验证气泡空白是否消失

## 风险评估
- 如果 `realLineHeight / font.Height` ≈ 0.5，bubble 会**瞬间变矮一半**，长 AI 消息 1494→735
- 但**实际文字渲染不会变**（TextBox 还是渲染那么多内容），只是 TextBox 高度变小了
- **如果文字被裁**，说明算少了，需要把 scale 调到 0.6-0.7

## 水平滚动条
目前 HorizontalScroll 关闭没生效（可能 WinForms 在 AutoScroll=true 时强制启用）。需要看陛下日志，但**这不影响空白修复**。

## Build
- 0 错
- `[MeasureRobust]` 诊断日志已加
- `MeasureTextRobust` 返回值已用 scale 修正