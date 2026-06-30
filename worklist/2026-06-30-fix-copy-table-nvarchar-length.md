# 2026-06-30 修复复制表结构 NVARCHAR 长度翻倍 Bug

## 症状

「跨库复制数据库对象」工具 → 类型=U → 复制源库的 `NVARCHAR(3000)` 到目标库，生成的脚本变成 `NVARCHAR(6000)`。原本 3000 字符的字段被强制放大 2 倍。

## 根因

`CrossDbCopyTableForm.GenerateCreateTableScript` 调用 `sys.columns.max_length` 拿列长度，但：
- **`max_length` 对 Unicode 类型（NVARCHAR / NCHAR）返回的是字节数（2 字节/字符）**
- 对 VARCHAR/CHAR/VARBINARY/BINARY 返回字节数（1 字节/字符 = 字符数）

旧的 `GetSqlDataType` 把 `max_length` 直接当成字符数用：

```csharp
"nvarchar" => maxLen == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(" + (maxLen) + ")",
```

`NVARCHAR(3000)` 在 sys.columns 中 `max_length = 6000` 字节，脚本里直接套上去变成 `NVARCHAR(6000)`。

## 同名方法对比（关键）

`CompareTablesForm.FormatSqlDataType` 在 2026-06-15 已经正确处理了：

```csharp
"nvarchar" => maxLen == -1 ? "NVARCHAR(MAX)" : "NVARCHAR(" + (maxLen / 2) + ")",
"nchar" => "NCHAR(" + (maxLen / 2) + ")",
"varbinary" => maxLen == -1 ? "VARBINARY(MAX)" : "VARBINARY(" + maxLen + ")",
"binary" => "BINARY(" + maxLen + ")",
```

但 `CrossDbCopyTableForm.GetSqlDataType` 当时没有被同步修复。

## 修复

对齐到 `CompareTablesForm.FormatSqlDataType` 的逻辑：

- NVARCHAR/NCHAR：长度 `maxLen / 2`
- VARBINARY：补 MAX 分支
- BINARY：补上
- 加同样的注释说明字节/字符差异

## 改动文件

- `A3Tools.Plugins.Default/Forms/CrossDbCopyTableForm.cs`
  - `GetSqlDataType` 方法：NVARCHAR/NCHAR 长度除以 2；补 VARBINARY（MAX/-1）+ BINARY
  - 加 XML 注释解释 `max_length` 在不同类型下的语义

## 验证

```powershell
dotnet build A3Tools.sln -c Debug --nologo
```

结果：0 错。

## 教训

- **相同的工具方法在多个 Form 里要保持同步**：本次有两处 `FormatSqlDataType` / `GetSqlDataType`，只在对比工具修了一次，复制工具漏了，导致 bug 潜伏到生产被陛下发现
- **未来改进方向**：把 `FormatSqlDataType` 抽到 `A3Tools.Common` 里共用，避免重复实现导致漂移
- **`sys.columns.max_length` 的语义陷阱**：Unicode 类型字节=字符×2，新手常踩坑，最好封装到 helper 里