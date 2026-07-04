# 2026-07-04 对象资源管理器重写（6 类 6 树 + 性能根治）

## 起因

陛下反馈原版（单树 + 6 CheckBox）两个问题：
1. **布局**：6 类对象混一棵大树不可读
2. **性能**：加 Explorer 后程序卡顿，关窗要等半天

## 重构内容

### 布局：6 类 → 6 个独立 Tab

```
┌──────────────────────────────┐
│ 🔄 刷新  (X 个对象)          │ ← pnlTop
├──────────────────────────────┤
│ [表 U] [视图 V] [TVF] [标量] │ ← TabControl：6 页
│ [过程 P] [触发器 TR]         │
│ ┌──────────────────────────┐ │
│ │ 筛选 [_________]         │ │ 每页独立 TextBox
│ ├──────────────────────────┤ │
│ │ 📁 dbo (250)             │ │ 每页独立 TreeView
│ │  └─ SaleOrder            │ │
│ │     └─ BillNO            │ │
│ └──────────────────────────┘ │
└──────────────────────────────┘
```

### 性能根治（4 个武器）

| 问题 | 修复 | 性能提升 |
|------|------|----------|
| 单树 1000+ 节点 | 6 类分 6 树 | **单树节点数 ÷ 6** |
| 4-5 次并发重建（切库/toggle/Refresh/LoadDatabases 同时触发） | SemaphoreSlim(1,1) + WaitAsync(0) | **同时只跑 1 个** |
| RefreshAsync 老任务未取消 | CancellationTokenSource 链 cancel | **同一 (server,db) 连按 10 次只跑最后一次** |
| ImageList 每个 form new 1 份 | 6 树共享 1 份 ImageList（Designer 一次构建） | **6 个 GDI handle → 1 个** |
| 关窗不 Dispose bitmap | Dispose(true) 释放 imageList + components | **关窗秒级（原 30s+）** |
| Filter 触发整树重建 | 只改 BackColor + 自动 Expand 命中父节点 | **过滤 O(n)，n=当类对象数** |

## 改动文件

- `ObjectExplorerForm.cs`：重写为 BindTabEvents/RefreshAsync(节流)/RebuildOneTree(单树)/ApplyFilterToOneTree(单树)
- `ObjectExplorerForm.Designer.cs`：重写为 TabControl + 6 TabPage + 共享 ImageList + 顶栏 Refresh

## 验证

- dotnet build：0 错误
- 6 Tab 切到任意一个，瞬间出现，不用等重建
- 关窗应秒关（不再卡死）
- 双击对象穿透到 SqlQueryForm.OpenScript（已验证过的接口）
- Top 刷新按钮 forceReload（不走 cache）

## 已知约束

- 列子节点默认挂上；用户可手动 CollapseAll（手动折叠 schema 节点）
- ImageList 仍是占位色块（后续接 A3Tools/Icons 替换）
