# 2026-07-07 修复选中问题+添加搜索替换

## 问题
1. SQL编辑器选中不太好用，老是不听使唤、选多
2. 希望支持Ctrl+F搜索、Ctrl+H替换

## 修复内容
1. 修复选中问题：
   - Highlight()里精确保存selStart、selLen、selColor
   - 恢复选中时用保存的selColor，避免全选改色后恢复不对
   - 保留vScroll/hScroll滚动位置恢复逻辑

2. 添加搜索替换功能：
   - 新建SearchReplaceDialog.cs和SearchReplaceDialog.Designer.cs
   - 支持：查找下一个、查找上一个、区分大小写、替换、全部替换
   - SqlEditor添加ShowSearchReplace方法
   - SqlEditor添加快捷键：Ctrl+F=查找，Ctrl+H=替换

## 修改文件
- D:\work\A3Tools\A3Tools.Plugins.Default\Forms\SqlEditor.cs
- D:\work\A3Tools\A3Tools.Plugins.Default\Forms\SearchReplaceDialog.cs（新增）
- D:\work\A3Tools\A3Tools.Plugins.Default\Forms\SearchReplaceDialog.Designer.cs（新增）
