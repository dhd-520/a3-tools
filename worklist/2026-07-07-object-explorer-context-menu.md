# 2026-07-07 对象资源管理器右键菜单

## 需求
陛下要求A3Tools继续优化，给对象资源管理器添加右键菜单，方便操作。

## 实现

### 1. 给ObjectExplorerForm.Designer.cs添加ContextMenuStrip和菜单项
- 添加ContextMenuStrip contextMenuTree
- 添加三个ToolStripMenuItem：miCopyName、miCopyFullName、miOpenScript
- 配置菜单项文本和大小
- 给每棵TreeView绑定ContextMenuStrip

### 2. ObjectExplorerForm.cs添加事件处理
- 添加字段TreeNode? _selectedNode记录右键选中节点
- 绑定TreeView.NodeMouseClick事件记录选中节点
- 绑定ContextMenuStrip.Opening事件：没选中对象节点时禁用菜单项
- 实现MiCopyName_Click：提取并复制对象名（不含schema）
- 实现MiCopyFullName_Click：复制完整路径（含schema）
- 实现MiOpenScript_Click：和双击一样，打开脚本

## 测试
dotnet build：0错误，250警告（历史警告，无关本次修改）
