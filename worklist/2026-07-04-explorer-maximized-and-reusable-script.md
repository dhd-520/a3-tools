# 2026-07-04 2 个改进：Explorer 最大化定位 + CREATE 脚本可重复执行

## Bug 1：主窗体最大化 → Explorer 屏幕外 / 半外

### 之前的位置算法

```csharp
int x = this.Right + gap;          // 主窗体右侧 4 px
if (x + 360 > wa.Right)
{
    if (this.Left - 360 - 4 >= wa.Left)
        x = this.Left - 360 - 4;     // 主窗体左侧
    else
        x = wa.Right - 360;          // 屏幕右
}
```

问题：主窗体**最大化**时 `this.Right == wa.Right`，x = wa.Right + 4 后立即跳到主窗体左侧（左侧不够） → 最终位置 `wa.Right - 360`，看起来贴 WorkArea 右侧 —— **但视觉上 Explorer 跨在了主窗体原来位置上，结果被认定为"半屏外半屏内"。**

### 修复

主窗体**最大化时**，Explorer 在主窗体**内部右侧**（Owned Form 叠加）：

```csharp
if (this.WindowState == FormWindowState.Maximized)
{
    // 最大化：内部右侧叠加（在主窗体内，必然可见）
    x = this.Right - explorerWidth;
    y = this.Top;
}
else
{
    // 还原：主窗体右侧外（之前的逻辑）
    x = this.Right + gap;
    y = this.Top;
    if (x + explorerWidth > wa.Right)
    {
        if (this.Left - explorerWidth - gap >= wa.Left)
            x = this.Left - explorerWidth - gap;
        else
            x = Math.Max(wa.Left, wa.Right - explorerWidth);
    }
}
```

**最大化时 Explorer 覆盖部分编辑器但永远可见**（用户可折叠/拖动主窗体访问被遮部分）。

## Bug 2：双击存储过程脚本无法直接执行（重复 CREATE 报错）

### 之前生成

```sql
USE [POS_SnackStore]
GO

CREATE PROCEDURE [dbo].[sp_xxx]
    ...
GO
```

**问题**：对象已存在时执行 → SQL Server 抛 `There is already an object named 'sp_xxx' in the database`。用户必须手动改成 `ALTER`（还要去掉 CREATE 关键字）才能跑，繁琐。

**陛下反馈**："加载后 改为 ALTER 无法直接执行" —— 描述的就是这个痛点。

### 修复：SSMS 风格"先删再建"模式

```sql
USE [POS_SnackStore]
GO

IF OBJECT_ID(N'[dbo].[sp_xxx]', N'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_xxx]
GO

CREATE PROCEDURE [dbo].[sp_xxx]
    ...
GO
```

**直接 F5 跑通**，第二次跑也不报错（先 DROP 再 CREATE，幂等操作）。

### 触发器特殊处理

触发器不能 DROP/CREATE 重写（DROP 触发器会丢失）。生成：

```sql
-- 触发器不能 DROP 重创，改用 ALTER 触发器定义以仅重编译
-- 如需重置：DISABLE TRIGGER [tr_xxx]
-- GO
-- DROP TRIGGER [tr_xxx]
```

新方法 `BuildDropStatement(objType, fullName)`：

| 类型 | DROP 语句 |
|------|----------|
| P (存储过程) | IF OBJECT_ID(N'...', N'P') IS NOT NULL DROP PROCEDURE ... |
| FN (标量函数) | IF OBJECT_ID(N'...', N'FN') IS NOT NULL DROP FUNCTION ... |
| TF/IF (表值) | IF OBJECT_ID(N'...', N'TF'/'IF') IS NOT NULL DROP FUNCTION ... |
| V (视图) | IF OBJECT_ID(N'...', N'V') IS NOT NULL DROP VIEW ... |
| TR (触发器) | 仅生成注释（禁用/删除操作由用户执行）|
| U (表) | 仅生成注释（不删表）|

## 改动文件

| 文件 | 改动 |
|------|------|
| `SqlQueryForm.cs` | `ComputeExplorerLocation` 增加最大化分支（叠加内部右侧）|
| `SqlScriptLoader.cs` | 新增 `BuildDropStatement`；`LoadCreateScriptAsync` 头加 IF EXISTS DROP |

## 验证

- `dotnet build`：0 错误
- 主窗体最大化 → 打开 Explorer → Explorer 在主窗体内部右侧（完全可见）
- 主窗体还原 → Explorer 在主窗体右侧外
- 双击存储过程 → 加载脚本 → F5 → 直接成功（先 DROP 后 CREATE，无"已存在"报错）
- 双击触发器 → 加载注释说明 + 原 CREATE TRIGGER（用户手动处理触发器）

## 下一步

- 若陛下更喜欢"Explorer 始终贴主窗体外侧（即使最大化）"，可考虑"最大化时同时把主窗体缩小让位" — 但目前内部叠加更简单且不影响显示
- 若用户脚本需要 SSMS "ALTER 到"模式（不丢对象），可加 `LoadAlterScriptAsync` —— 后续按需
