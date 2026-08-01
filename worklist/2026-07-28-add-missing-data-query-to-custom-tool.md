# 2026-07-28 自定义工具 — 增加「缺失数据」按钮（查源有目标无）

## 背景

陛下 08:48 反馈：在「自定义工具」搜索区加一个**缺失数据查询**功能，可以查出源库存在但目标库不存在的数据，多选后可以直接复制。

陛下 08:53 强调：**复制那一块不用改**，缺失数据只是和查询类似，只是条件不同，最终还是要"添加选中 → 复制"。

陛下 09:02 进一步明确：**不是 CheckBox 切换模式**，而是「查询」按钮旁加一个独立的「缺失数据」按钮，**点击即查**（不需要输入关键字），**TOP 500** 上限。

## 最终设计

**搜索区按钮排布**（陛下已确认）：

```
[ 搜索关键字：____ ] [ 查询 ] [ 缺失数据 ] [ 添加选中 ] [ 清空选项 ]
   原有位置           原有      新增橙色    原位置+104    原位置+92
```

- 「缺失数据」按钮：**橙色 `#e45826`** 加粗,与「查询」蓝色形成视觉对比
- 点击后**不需要关键字**,直接跨源/目标库对比主键
- 结果展示在 **同一个 DataGridView**,**下游流程（多选/添加选中/确认复制）一字不动**

## 改动文件

| 文件 | 改动 |
|------|------|
| `Forms/GenericCopyToolForm.Designer.cs` | 1. `chkMissingMode` 字段改为 `btnMissingData` 按钮字段<br>2. `rowHintAndCheckbox` 还原 2 列布局(不再预留第 3 列)<br>3. 橙色按钮配置块(Location=665,5 / Size=110,41 / Text="缺失数据")<br>4. `btnAddSelected` 位置 679 → 783<br>5. `btnClearSelected` 位置 843 → 935 |
| `Forms/GenericCopyToolForm.cs` | 1. 新增 `BtnMissingData_Click`(参数校验 + 走 IsHttpMode 分流)<br>2. `RunMissingDataSearchAsync()` / `RunMissingDataSearchHttpAsync()` 参数去 `keyword`+ TOP 改为 500<br>3. 移除初次方案里的 `ChkMissingMode_CheckedChanged` / `UpdateMissingModeUI`<br>4. `BtnSearch_Click` 还原为纯关键字模式 |

**未改动**：`CustomToolConfig` / `CustomToolConfigDialog` / `TableCopyService` / `MainForm` / `DefaultTools` / `tools.json`(业务入口零侵入)。

## 关键设计

### 应用层求差集(不依赖 LinkedServer)

源/目标通常是不同 SQL Server 实例,没法一条 SQL `WHERE NOT EXISTS` 跨连接。改用:

```
1. SELECT TOP 100000 [PK] FROM dbo.[目标表]    → HashSet<string> tgtPks
2. SELECT TOP 500 * FROM dbo.[源表] ORDER BY [PK]   → srcDt
3. 遍历 srcDt 行:if ([PK] ∉ tgtPks) → missingDt.ImportRow(row)
4. BindSearchResults(missingDt)
```

**TOP 500 源端**:陛下指定,避免大表全扫撑爆 UI。
**TOP 100000 目标端**:防止百万行目标表把内存吃爆(配置类/字典表场景下足够)。
**应用层求差**:跨 SQL Server 实例唯一不依赖基础设施的方案;直连和 Http 双栈代码 100% 对称。

### 直连 + Http 双栈

#### 直连 (`RunMissingDataSearchAsync`)
```csharp
using var srcConn = new SqlConnection(srcConnStr);
using var tgtConn = new SqlConnection(tgtConnStr);
srcConn.Open(); tgtConn.Open();
// SqlDataAdapter 读 srcDt,SqlDataReader 读 tgtPks
// 然后应用层求差
```

#### Http (`RunMissingDataSearchHttpAsync`)
```csharp
var srcDA = ProxyHelper.CreateDataAccess(_srcAccount);
var tgtDA = ProxyHelper.CreateDataAccess(_tgtAccount);
// ProxyHelper.ExecuteQueryToDataTableAsync 读两边
// 然后应用层求差(同直连)
```

### 状态管理

`finally` 块同时复位 `btnMissingData.Enabled` 和 `btnSearch.Enabled = true`,**避免快速点击穿插**导致按钮灰着不可用。

### 下游流程 0 改动

差异结果展示在同一个 `dgvSearchResults`,通过现有 `BindSearchResults` 渲染,应用现有 `ApplySearchColumnLayout` 处理列显隐。

「添加选中 / 清空选项 / 确认复制」按钮一字未动 —— 因为它们只关心 `dgvSearchResults` 当前展示的 PK 列,不在乎行是从"关键字搜索"还是"缺失查询"来的。

## 编译结果

```
Release build: 0 错 315 警告(全部项目历史 CS8632/CS4014/CS0168/CS0169/CS0414/CS1998/CS8602/CS8604 等,与本次改动无关)
Debug build:   0 错 315 警告(同上)
DLL: A3Tools.Plugins.Default.dll 532480 bytes 2026/7/28 9:03:58
```

## 测试用例(待陛下验证)

1. 工具箱上选源库 + 目标库 → 打开任一自定义工具(比如「复制报表」)
2. 直接点「缺失数据」(不输关键字)→ DataGridView 显示源库中、目标库没有的所有 PK 行(最多 500)
3. 多选若干行 → 点「添加选中」→ PK 进 txtKeyValues(走现有逻辑)
4. 点「确认复制」→ 走现有 TableCopyService 复制逻辑(**陛下强调不动**)
5. 在「查询」按钮输入关键字 → 点「查询」→ 走原关键字模糊搜索(回归测试,确认没破坏)

## 经验沉淀

**当用户说"和 XX 类似,只是条件不同"**:
- 把"条件生成部分"独立为**独立按钮**或**独立 SQL 生成器**,而非 Toggle/Radio
- 下游数据流(绑定 DataGridView、多选、添加、复制)完全不动
- 这次只加 1 个 Button + 1 个 Click 事件 + N 行新 SQL,**0 个新数据通路**

**应用层求差集 vs SQL `WHERE NOT EXISTS`**:
- 跨 SQL Server 实例 → 应用层(HashSet)是最普适方案
- 同实例可 LinkedServer / 三段名 → 单 SQL 更高效
- 配置类工具数据量不大,应用层足够了
- 内存安全:源 TOP 500(陛下指定),目标 TOP 100000 防爆

**视觉差异化**:
- 「缺失数据」用警告色(橙色 `#e45826`)而非主色(蓝),**暗示"特殊操作"**,避免与「查询」混淆
- 让陛下一眼分辨两个按钮的语义差异:蓝=常规查询,橙=差异化操作

---

**未提交、未发版。** 等陛下决定是否并入 v2.4.6 / 直接发版。

---

## 调试记录（重要踩坑!）

1. **09:08 第一轮**: CheckBox 联动模式 — 陛下立刻否决："不是要差异勾选框"
2. **09:10 第二轮**: 改成独立「缺失数据」按钮 + 不需关键字 + TOP 500 — 但**编译通过运行时按钮不可见**!
3. **09:12 根因定位**: Designer.cs 里给 `btnMissingData` 写了字段声明、实例化、属性配置、Click 事件订阅,但**漏了一行 `pnlSearch.Controls.Add(btnMissingData)`** —— 控件不在控件树里,所以不渲染!
4. **修复**: pnlSearch.Controls.Add 列表中插入 btnMissingData (line 480,btnSearch 之后 btnAddSelected 之前) → 重编译 → 陛下重测可见。

### 反思 & 防错

- **手写 Designer.cs 容易漏 Controls.Add**。VS 设计器自动加这一行,但文本编辑时不会。
- **编译不会报错** — `Controls.Add` 是方法调用,IDE/编译器不知道"这控件是给哪个父容器用的"。
- **调试方法**: 用 `form.Controls.Find("btnMissingData", true)` 看返回非空 + 控件可见,但要复现这个问题成本高;更直接的是**搜索 Designer.cs 里 `MissingData` 字符串出现位置**,确认 Controls.Add 里有它。
- **教训**: 写完 WinForms Designer 时,最好 diff 一遍"字段声明 + 实例化 + Controls.Add"三件套是否都齐了。

---

## 经验沉淀（追加）

