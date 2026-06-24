# 2026-06-24 复制报表工具新增 S_REPORTCOLUMNSETTING 表 + 升版本 v2.0.3

## 背景
复制报表工具 (`CrossDbCopyReportForm`) 已支持 S_REPORT 表 + 多张子表，但报表列设置 (`S_REPORTCOLUMNSETTING`) 还没纳入同步范围，导致目标库报表打开时列宽/列配置丢失。本次补齐。

## 修改
- `A3Tools.Plugins.Default/Forms/CrossDbCopyReportForm.cs:350`
  - 在复制 S_REPORTSCHEME 之后追加一行：
    ```csharp
    TableCopyService.CopyTableDataByParentGuid(srcConn, tgtConn, "S_REPORTCOLUMNSETTING", "REPORTGUID", reportGuid, deleteFirst, "[报表]");
    ```
- `A3Tools/Forms/MainForm.Designer.cs:110`
  - `lblVersion.Text` 由 `v2.0.0` 改为 `v2.0.3`。

## 验证
- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：0 错误，149 个历史警告（均为 nullable 相关历史 warning）。
- `dotnet publish A3Tools/A3Tools.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o D:\work\A3Tools/publish/StandaloneSF`：成功。
- StandaloneSF 产物（总 ≈76 MB）：
  - 根目录：`A3Tools.exe` (74.08 MB) + 3 个 pdb + `A3Tool.ico` + 用户手册
  - `Plugins/`：`A3Tools.Plugins.Default.dll` + `A3Tools.Common.dll` + `tools.json`
  - 无 `.log` / `~$*` 残留

## 提交
- `00ebe39 feat(report-copy): 报表复制新增 S_REPORTCOLUMNSETTING 表 + 升版本 v2.0.3`
