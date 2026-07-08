# 2026-07-07 修复SQL编辑器高亮时滚动条跳回顶部的问题

## 问题
陛下反馈：当SQL脚本较多时，移动到中间位置开始编辑，滚动条会自动跳回顶部偏左位置，使用体验不好。

## 原因
SqlEditor.Highlight()方法会先Select(0, TextLength)将所有文本选中再设色，这会导致RichTextBox自动将选中内容滚动到视图可见位置，从而引发滚动条跳转。

## 修复方案
在Highlight()前保存当前垂直和水平滚动位置，高亮完成后恢复滚动位置。

## 实现
1. 添加Win32 API：GetScrollPos、SetScrollPos（用于保存和设置滚动位置）
2. 添加常量：SB_HORZ=0、SB_VERT=1、SB_THUMBPOSITION=4
3. 在Highlight()方法开始处保存vScroll和hScroll
4. 在finally块中恢复滚动位置：
   - SetScrollPos设置滚动条位置
   - SendMessage发送WM_VSCROLL/WM_HSCROLL确保内部滚动状态同步

## 修改文件
D:\work\A3Tools\A3Tools.Plugins.Default\Forms\SqlEditor.cs
