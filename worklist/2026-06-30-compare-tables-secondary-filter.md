# 2026-06-30 表结构对比窗体 - 二次筛选

## 需求

「表结构对比」窗体（`CompareTablesForm`）打开后只能看全量差异。当源/目标库差异数量大时，定位特定差异项很费劲。需要支持按「差异类型」和「字段名」二次筛选。

## 设计

在摘要行上方新增一条筛选行 `pnlFilterRow`：

| 控件 | 类型 | 说明 |
|------|------|------|
| 差异类型：缺表 | CheckBox | 多选勾选要显示的差异类型，全不勾 = 不过滤 |
| 差异类型：缺字段 | CheckBox | 同上 |
| 差异类型：类型差异 | CheckBox | 同上 |
| 字段名： | TextBox | 模糊匹配 `ColumnName`，空 = 不过滤 |
| 清空筛选 | Button | 一键重置所有筛选条件 |

## 关键设计

- **保留全量数据**：新增私有字段 `_allDifferences`，每次对比完成后存全量
- **拆分渲染**：
  - `RenderDifferences(differences)` 入口：存全量 + 更新摘要/按钮 + 调 `ApplyFilter`
  - `RenderRows(rows)` 实际渲染 DataGridView 行（每次筛选后重画）
  - `ApplyFilter()` 根据筛选条件过滤全量数据 → 调 `RenderRows`
- **摘要始终基于全量**：筛选变化不会让「共 N 项差异」的统计数变，避免误导
- **字段名匹配边界**：
  - 大小写不敏感子串匹配
  - 缺表行 `ColumnName` 为空，输入字段名筛选时会被过滤（因为没有字段名可比）
  - 这是预期行为：要看缺表就用「差异类型」勾选；字段名筛选只针对「缺字段」「类型差异」

## 改动文件

- `A3Tools.Plugins.Default/Forms/CompareTablesForm.Designer.cs`
  - mainLayout 5 行 → 6 行（新增 42px `pnlFilterRow`）
  - ClientSize 757 → 759
  - 新增 7 个控件：`pnlFilterRow` + `lblDiffType` + 3 个 `CheckBox` + `lblColumnName` + `txtColumnFilter` + `btnClearFilter`
- `A3Tools.Plugins.Default/Forms/CompareTablesForm.cs`
  - 私有字段 `_allDifferences` 保留全量
  - `RenderDifferences` 拆分（保留摘要/按钮逻辑，新增 ApplyFilter 调用）
  - 新增 `ApplyFilter()` / `ClearFilter()` / `RenderRows()`
  - 构造函数订阅 5 个事件：3 CheckBox.CheckedChanged + TextChanged + btnClearFilter.Click

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：0 错，新增代码 0 warning。

## 边界处理

- 全量数据为空时（无差异）：ApplyFilter 直接渲染空表，不抛异常
- 字段名为空字符串（缺表行）：Contains 永远 false，缺表行会被字段名筛选过滤掉——这是预期，避免误显示
- 摘要/进度条/按钮在筛选变化时不动，仅 DataGridView 行重画
- 摘要/进度基于全量统计，**不会**因为筛选而变化（避免误导用户）

## 后续可选

- 字段名筛选也支持匹配表名（陛下没要求暂不做）
- 「全选/反选当前筛选结果」按钮
- 摘要文本增加「已筛选 X/Y」提示