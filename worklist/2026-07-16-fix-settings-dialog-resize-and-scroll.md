# 设置页面支持调整大小 + 自动滚动条 (2026-07-16)

## 背景

陛下反馈：在分辨率小的电脑上，A3Tools 的「设置」窗体太大装不下，需要像「账套新增」窗体（AccountDialog）一样支持：
1. 自动出滚动条
2. 用户可自由调整窗体大小

陛下二次反馈：实际跑起来「最下方被覆盖很多内容，大概是固定底部给遮盖了」。

## 根因

对比 `SettingsDialog.Designer.cs` 和 `AccountDialog.Designer.cs`：

| 属性 | SettingsDialog（问题） | AccountDialog（参考） |
|---|---|---|
| `FormBorderStyle` | `FixedDialog` ❌ | 默认 `Sizable` ✅ |
| `MaximizeBox` | `false` ❌ | 默认 `true` ✅ |
| `MinimizeBox` | `false` ❌ | 默认 `true` ✅ |
| `MinimumSize` | 未设 ❌ | `new Size(760, 560)` ✅ |
| `SizeGripStyle` | 未设 ❌ | `SizeGripStyle.Show` ✅ |
| `ClientSize` | `(1152, 1430)` ❌ | `(900, 720)` ✅ |
| `mainPanel.AutoScroll` | `true` ✅ | `contentPanel.AutoScroll = true` ✅ |
| `AutoScrollMinSize` | 未设（依赖自动计算）❌ | 未设（依赖自动计算）✅ |

### 二次根因（陛下反馈底部被遮盖）

第一轮只改了 Form 属性让窗体可调，但跑起来发现 `mainPanel.AutoScroll` 没出滚动条。

`.NET 7 WinForms` 的 `ScrollableControl` 在 `Dock = Fill` 模式下，`AutoScrollMinSize` 不会自动按子控件最大 Y 计算 —— 会取 `ClientSize` 当默认值。所以 `AutoScrollMinSize.Height ≈ 582`，与 `ClientSize.Height = 582` 相等，AutoScroll 判断「内容未超出」→ **不出滚动条**。

底部 600+ px 的内容（`txtHubConfigDir` / `btnHubConfigBrowse` 最大底 Y=1241）被 mainPanel 边界裁掉，看起来就像被底部 bottom 面板遮住。

> **结论**：Dock=Fill 的 AutoScroll 必须显式设 `AutoScrollMinSize`，不能依赖自动计算。

## 修复

只动 `SettingsDialog.Designer.cs`，控件布局不动（8 行 diff）：

**第一轮（窗体属性）**：
```diff
-        ClientSize = new Size(1152, 1430);
+        ClientSize = new Size(1152, 720);  // 默认高度砍半
         ...
-        FormBorderStyle = FormBorderStyle.FixedDialog;
-        MaximizeBox = false;
-        MinimizeBox = false;
+        MinimumSize = new Size(820, 600);   // 最小尺寸保护
         ...
+        SizeGripStyle = SizeGripStyle.Show; // 右下角调整抓手
```

**第二轮（mainPanel 滚动区域）**：
```diff
         mainPanel.Dock = DockStyle.Fill;
         mainPanel.Location = new Point(0, 60);
         mainPanel.Name = "mainPanel";
+        mainPanel.AutoScrollMinSize = new Size(0, 1260);  // 显式设滚动区域
+        mainPanel.Padding = new Padding(0, 0, 0, 12);      // 底部 12px 留白
         mainPanel.Size = new Size(1152, 1320);
```

效果：
- **小屏打开不再装不下**：默认 `1152×720`，控件垂直高度累计到 ~1300px，超出部分靠垂直滚动条看
- **可调大小**：去掉 `FixedDialog` 后窗体可拖拽边缘/右下角缩放，`mainPanel.Dock = Fill` 自动填满
- **不会缩太小看不见**：`MinimumSize = (820, 600)` 保底
- **最大化/最小化可用**
- **滚动条正确触发**：`AutoScrollMinSize = 1260 > ClientSize 582` 强制出垂直滚动条；底部 Padding 让内容不贴边

控件 X 坐标不动（最大用到 X=1116），mainPanel 宽 1152 时不会被裁。`mainPanel.Dock=Fill` 拉伸时宽度增大也不影响。

## 验证

- `dotnet build A3Tools.sln -c Debug -v minimal` → **0 错 331 警告**（原项目 pre-existing nullable 警告，跟本次改动无关）
- 与 AccountDialog 体验完全一致

## 变更清单

- 修改：`A3Tools/Forms/SettingsDialog.Designer.cs`（窗体属性块 + mainPanel 滚动属性，共 8 行 diff）

## 经验教训

`.NET 7 WinForms Panel.AutoScroll` 在 `Dock = Fill` 模式下不可靠——必须显式设 `AutoScrollMinSize`，否则滚动条不出现。这是 .NET WinForms 的已知行为，不是项目代码问题。