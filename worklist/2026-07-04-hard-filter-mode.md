# 2026-07-04 严格筛选模式（未命中节点隐藏）

## 起因

陛下要求：filter 不仅是高亮，未命中节点要直接隐藏。两种模式双开。

## 设计

- 顶栏新增 ✂/🔍 模式切换按钮（默认严格）
- **严格模式**：`TreeView.Nodes` 内未命中的子树直接 Remove，只保留命中路径
- **高亮模式**：保留所有节点，命中涂黄 + 展开（沿用原行为）
- 输入框节流 200ms（连续键入不卡）：每 Tab 一个独立 System.Windows.Forms.Timer
- 空 filter 自动 Rebuild 全树（cache 命中，瞬间完成）
- CheckBox / 模式按钮：6 棵树立即按当前模式 rebuild

## 改动文件

- `ObjectExplorerForm.Designer.cs`：pnlTop 加 btnMode
- `ObjectExplorerForm.cs`：删 MarkAndExpand / ClearBackColorsOnly；RebuildOneTree 加 filter 参数；新 BindAllTabs / BindOne；新模式切换逻辑；节流 Timer

## 关键逻辑

### 命中判定（按节点类型）
| 节点类型 | 严格模式行为 | 高亮模式行为 |
|---------|------------|------------|
| 列（ColName）命中 | 只显示该列 | 显示并涂黄 |
| 对象（ObjName）命中 | 显示对象 + 全部列，**对象涂黄** | 同左，但其他未命中也显示 |
| 列 + 对象 都未命中 | **整对象 Remove** | 显示但不涂黄 |
| schema 内无任何命中对象 | **整 schema Remove** | 仍显示（保持全树） |

### 节流
- `textbox.TextChanged → Timer.Stop() + Start()`
- `Timer.Tick → Timer.Stop() + 触发 RebuildOneAsync`
- 每 Tab 一份 Timer，互不影响
- 200ms 间隔 —— 键入一次 1 字符，**只在停 200ms 后重建一次**

### 重建并发保护
- 单 Tab Rebuild 走 `BeginInvoke(UI)` 回到主线程
- 切库时 RefreshAsync 用 SemaphoreSlim + CTS，老任务被取消
- 单 Tab filter 不经过 SemaphoreSlim（顺序跑）：防止多个 Tab 同时重建时撞锁

## 测试用例（陛下要求）

| 输入 | 严格模式期望 | 高亮模式期望 |
|------|------------|------------|
| `S_SCM_SEORDER` 在「表」tab | 只显示含 SEORDER 的对象节点 + 其 schema | 所有对象都在，含 SEORDER 的涂黄 + 展开 |
| `BillNo` 在「表」tab | 每个有 BillNo 列的对象只剩 BillNo 一列，其余 Remove | 每个对象都存在，BillNo 列涂黄 |
| `dbo` 在「视图」tab | 整 dbo schema 节点涂黄 + 展开，其他 schema 全 Remove | dbo 涂黄，其他保留 |
| 清空 | 立即全树恢复 | 全树恢复，黄色清除 |

## 验证

- `dotnet build`：0 错误
- 6 Tab 切换流畅
- 200ms 节流生效：连续键入 5 个字符，期望只触发 1-2 次 rebuild
- 模式切换：6 树都按当前模式 rebuild

## 后续

- 陛下若不喜欢节流，可把 200ms 改 50ms 或 0（瞬间重建）
- Hard 模式下可考虑右键「清空筛选」快捷菜单
