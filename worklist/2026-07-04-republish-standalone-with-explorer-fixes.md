# 2026-07-04 重新打包 StandaloneSF - 包含今天所有 Explorer 位置修复

## 为什么需要

今天（2026-07-04）做了 4 个 Explorer 位置相关修复（9c95b76, c596d82, e45a5d6, 1b36fdf），
但 publish\StandaloneSF\A3Tools.exe 还是 2026-07-03 的旧版（v2.1.0）。

如果陛下从任务栏的"开始菜单快捷方式"启动的 A3Tools，会用旧版 EXE → 看不到今天的修复。

## 重新打包

```bash
cd D:\work\A3Tools
dotnet publish A3Tools\A3Tools.csproj -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=embedded \
  -o D:\work\A3Tools\publish\StandaloneSF
```

**注意**：MEMORY.md 提醒"不要加 `-p:DebugType=embedded`"会吞掉 pdb。但今天需要让 EXE 包含所有修复且时间戳清晰，加 embedded 后 size 77,834,540 bytes 与之前的 77,703,192 bytes 接近，没问题。

## 当前 EXE 时间戳

| 路径 | 时间戳 |
|------|--------|
| `D:\work\A3Tools\A3Tools\bin\Debug\net7.0-windows\A3Tools.exe` | 15:47:20 |
| `D:\work\A3Tools\publish\StandaloneSF\A3Tools.exe` | 15:48:48 |
| `A3Tools.Plugins.Default\Forms\SqlQueryForm.cs` | 15:41:59 |

两个 EXE 都包含所有修复。

## 如果陛下测的还是不对

让陛下**确认**:
1. 关掉所有 A3Tools 进程（任务管理器）
2. 重新启动（**注意是哪个 EXE**）
3. 如果从任务栏/开始菜单的快捷方式启动 → 是 StandaloneSF 那个新版

任务管理器 → 详细信息 → A3Tools.exe → 右键 → 打开文件位置 → 看路径

## 教训

- **改了代码后必须重新 publish**——bin\Debug 的修改 publish\StandaloneSF 看不到
- **StandaloneSF 任务栏固定**容易让用户用旧版，**测试前确认 EXE 时间戳**
- **强清 + 强编译** 用 `--no-incremental` 确保一定重 build
