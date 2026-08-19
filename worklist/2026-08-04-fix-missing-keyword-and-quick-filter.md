# 2026-08-04 自定义工具 缺失数据关键字过滤 + 快速过滤框

## 背景

陛下 09:17 反馈:
1. 「缺失数据」按钮:如果搜索关键字有值需要按关键字过滤,现在是全部都列出来了
2. 最上方需要增加快速过滤框,像其他工具一样可以在搜索结果的基础上二次过滤

## 修改

### 文件 1: `A3Tools.Plugins.Default/Forms/GenericCopyToolForm.Designer.cs`

**新增 2 个控件**(在 pnlSearch 内 DGV 上方单独一行):
- `lblQuickFilter` (Label): 文本「快速过滤:」, 位置 (10, 52), 宽 134 高 28
- `txtQuickFilter` (TextBox): 占位文字「在当前结果中再次过滤(所有列模糊匹配, 实时)」, 位置 (168, 49), 宽 740 高 38

**调整 DGV**:
- `Location: (10, 45) → (10, 86)` (给过滤行让 41px)
- `Size: (1225, 297) → (1225, 256)` (DGV 减 41px 高)

### 文件 2: `A3Tools.Plugins.Default/Forms/GenericCopyToolForm.cs`

#### A) 缺失数据关键字过滤

- `BtnMissingData_Click` 末尾读 `txtSearchKeyword.Text.Trim()` 作 keyword, 传入两个 RunMissing*Async
- `RunMissingDataSearchAsync(string keyword)` + `RunMissingDataSearchHttpAsync(string keyword)` 签名加 keyword
- 源端 SQL 分两路:
  - `keyword` 为空: 保留原 `SELECT TOP 500 * FROM dbo.[{table}] ORDER BY [{pk}]` (全表前 500)
  - `keyword` 非空: 走搜索路径 — 读 dbColumns → 计算 validSearchCols → BuildSearchSql → TOP 5000
- 目标端不变 (TOP 100000 主键 + 应用层差集)
- HTTP 版本用 ProxyHelper.EscapeSql 注入关键字, 模式跟 BtnSearchHttpAsync 一致

#### B) 快速过滤二次过滤

- 新增字段 `private string _quickFilterText = ""`
- `BindSearchResults`: `dgvSearchResults.DataSource = dt` → `dt.DefaultView` (DataView 模式, 后续可设 RowFilter)
- 新增 `ApplyQuickFilter()`: 对 DGV 每个可见列 `CONVERT([col], 'System.String') LIKE '%{escaped}%'` OR 拼接, 设到 `_searchResults.DefaultView.RowFilter`
- 订阅 `txtQuickFilter.TextChanged`: 实时更新 `_quickFilterText` + 调 `ApplyQuickFilter()`
- `BtnSearch_Click` / `BtnMissingData_Click` 开头: 清空 `_quickFilterText` + `txtQuickFilter.Clear()` (新查询重置二次过滤上下文, 对齐 CrossDbCopyConfigDataForm 的"点击查询时清空过滤框"语义)

#### C) 重构: 抽 `ComputeValidSearchCols` helper

之前 `BtnSearch` 直连 + `BtnSearchHttpAsync` 各自重复实现了"根据配置 + dbColumns 计算 validSearchCols"逻辑(均 ~25 行)。提到 helper:
```csharp
private List<string> ComputeValidSearchCols(IList<string> dbColumns)
```
两处搜索调用点改用 helper, 缺失数据两处也复用。一处定义, 四处复用, 总行数净减约 50 行。

## 改动统计

```
A3Tools.Plugins.Default/Forms/GenericCopyToolForm.Designer.cs |  29 +++-
A3Tools.Plugins.Default/Forms/GenericCopyToolForm.cs          | 186 +++++++++++++++------
2 files changed, 160 insertions(+), 55 deletions(-)
```

## 编译

- `dotnet build A3Tools.sln -c Release` ✅ **0 错 316 警告**
- 316 警告都是项目历史警告 (`CS4014`/`CS8632`/`CS1998`/`CS0168`/`CS0169`/`CS0414`), 不是本次引入

## 验证场景

1. **缺失数据 + 关键字**:
   - 输入关键字「test」→ 点「缺失数据」→ 源端只查 LIKE '%test%' 的 TOP 5000, 应用层求差
   - 关键字为空 → 点「缺失数据」→ 原行为, 源端 TOP 500
2. **快速过滤**:
   - 搜索/缺失数据出结果后, 在快速过滤框输入字符 → DGV 实时显示所有列匹配的行
   - 清空快速过滤框 → 显示全部结果
   - 点新查询(搜索/缺失) → 快速过滤框自动清空

## 关键设计

- **DataView 模式**: DGV 绑 `dt.DefaultView`, 过滤用 `RowFilter` (DataTable 自带, 无需手动维护过滤后的 DataTable)
- **可见列**: 过滤范围限定 `col.Visible=true` 的列(列配置生效后, 隐藏列不参与过滤)
- **类型转换**: 用 `CONVERT([col], 'System.String')` 兜底非字符串列(数字/日期), LIKE 才能正确比较
- **特殊字符**: 单引号 `'` 转义为 `''` (SQL Server RowFilter 标准)
- **空查询**: 关键字为空时直接 `_searchResults.DefaultView.RowFilter = ""` (性能最高)

## Git

未 commit (等陛下决定 commit 信息和版本号)

## 后续可选 (本次未做)

- 快速过滤框右侧加「清除」按钮(目前用 X 自带)
- 过滤结果数显示在 `lblSearchProgress` (类似 "已显示 3/10 条")
- 跨列 AND 过滤(目前是 OR, 跟 CrossDbCopyConfigDataForm 一致)
