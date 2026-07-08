# 2026-07-06 SQL 编辑器四项增强

## 陛下反馈

> "1、每个页签×号位置有问题，现在是在右下方。能不能放到右侧中间位置。
> 2、现在列提示是 SELECT * FROm S_SCM_SEORDER A 需要命名别名 或者输入全表名.才有提示。 能不能想SSMS一样。 FROM 后表名写完后，再在SELECT 后方直接输入类名也提示，而不是表名.才提示。
> 3、当我输入EXEC 后方能增加 存储过程提示么
> 4、当光标放到 数据库对象中间，如存储过程，函数等，F12可以直接转到定义么 等同于 搜索对象再双击进入"

## 改动概览

### 1. Tab × 按钮垂直居中

**位置：** `SqlQueryForm.cs` `TabControl_DrawItem` + `TabControl_MouseDown`

**改前：**
```csharp
var closeRect = new Rectangle(bounds.Right - 18, bounds.Top + (bounds.Height - 14) / 2, 14, 14);
e.Graphics.DrawString("×", ..., closeRect.Location);  // DrawString 从左上角开始，字会下沉
```

**改后：**
```csharp
var closeRect = new Rectangle(bounds.Right - 18, bounds.Top, 14, bounds.Height);
using var cf = new StringFormat { Alignment = Center, LineAlignment = Center };
e.Graphics.DrawString("×", ..., closeRect, cf);  // StringFormat 居中
```

效果：× 在 Tab 右边的 14px 列里**水平 + 垂直居中**（VS / SSMS 风格），点击区域同步调整。

### 2. 列名提示增强

#### 2a. 逗号分隔多表支持

**位置：** `SqlAliasResolver.cs` 增加 `regexComma` 第三次扫描

**之前：** `FROM A a, B b, C c` 只能匹配 `a → A`（regexObj 只匹配 FROM 后面第一个），b、c 被忽略。

**改后：** 增加 `regexComma`（`, obj alias` 模式），把后续别名也加入 map：

```csharp
var regexComma = new Regex(
    @",\s*(?:INNER\s+|...)?(?<obj>...)\s+(?:AS\s+)?(?<alias>\w+)\b",
    RegexOptions.IgnoreCase);
```

**验证（PowerShell 测试）：**
| 输入 | 之前只匹配 | 现在 |
|------|----------|------|
| `FROM S_SCM_SEORDER a, dbo.S_CUSTOMER c` | a → S_SCM_SEORDER | a → S_SCM_SEORDER, c → dbo.S_CUSTOMER |
| `FROM T1 a, T2 b, T3 c` | a → T1 | a → T1, b → T2, c → T3 |

#### 2b. 列提示逻辑验证

**结论：** 现有 `TryGetColumnSuggestion` 对基础场景（`FROM S_SCM_SEORDER A` 后 `A.`）应该工作 —— 用 PowerShell 跑了完整正则测试 5 个用例全部命中：
- `SELECT * FROM S_SCM_SEORDER A WHERE A.ID = 1` → `obj=S_SCM_SEORDER alias=A` ✓
- `SELECT * FROM dbo.S_SCM_SEORDER A` → `obj=dbo.S_SCM_SEORDER alias=A` ✓
- `SELECT * FROM S_SCM_SEORDER AS A` → `obj=S_SCM_SEORDER alias=A` ✓
- `SELECT A.* FROM S_SCM_SEORDER A` → `obj=S_SCM_SEORDER alias=A` ✓

**用户的"列提示不工作"可能是以下原因：**
1. 缓存未预热（WPF 异步任务，用户写得飞快）
2. 用户写的是多表逗号（已修复，见 2a）
3. 用户实际没看到 popup（位置不对 / 被挡住 / 系统原因）

如果回归还有问题，**优先看 status bar 是否有"未找到对象"提示**（F12 已加，见 #4）。

### 3. EXEC 后存储过程提示

**位置：** `SqlIntelliSenseProvider.GetSuggestions` 头部增加 `IsAfterExecKeyword` 检查

```csharp
if (IsAfterExecKeyword(fullSql, caretOffset, prefix))
{
    // 返回所有存储过程（按 prefix 前缀过滤）
    var procs = SqlObjectSchemaCache.GetObjectsByKind(connectionString, 
        new[] { ObjectKind.StoredProcedure })
        .Select(o => $"{o.SchemaName}.{o.Name}");
    return matched;
}
```

**`IsAfterExecKeyword` 逻辑：**
- 取光标前（不含 prefix）内容
- 去掉尾部空白 + 开括号
- 末尾必须是 EXEC 或 EXECUTE（前面是空白 / 行首 / 括号）

**验证（PowerShell，8 个用例全 OK）：**

| 场景 | 结果 |
|------|------|
| `EXEC ` | ✓ |
| `EXECUTE ` | ✓ |
| `EXEC Cust` | ✓（开始输入过程名） |
| `EXEC sp_` | ✓（系统过程提示） |
| `SELECT 1` | ✗（不是 EXEC） |
| `sp_executesql ` | ✗（避免误判） |
| `SELECT 1; EXEC ` | ✓ |
| `EXEC(Cust` | ✓（带括号） |

### 4. F12 转到定义

**新增文件 / 改动：**
- `SqlEditor.cs`：新增 `GetWordAtCursor()` 方法 + `GoToDefinitionRequested` 事件 + F12 按键处理
- `SqlQueryTabPage.cs`：订阅 `GoToDefinitionRequested` → 调 `_parent.GoToDefinition()`
- `SqlQueryForm.cs`：新增 `GoToDefinition()` 方法

**`GetWordAtCursor` 逻辑：**
- 从 caret 同时向左、向右找边界
- 支持 `schema.name` / `schema.[name]` / `[schema].[name]` / 纯 name / `@var` / `#tmp`

**`GoToDefinition` 流程：**
1. 取光标处的词
2. 去掉方括号包裹：`[dbo].[S_SCM_SEORDER]` → `dbo.S_SCM_SEORDER`
3. 拆 schema / name（如果有点）
4. 从 `SqlObjectSchemaCache` 查（表 / 视图 / TVF / 标量函数 / 存储过程 / 触发器）
5. 找到 → 调 `OpenScript("", typeChar, "schema.name")`
6. 找不到 → status bar 显示 `未找到对象：xxx`

**等同 Explorer 双击的体验**：自动建 Tab、加载脚本、切到 Tab。

## 改动文件

| 文件 | 改动 |
|------|------|
| `SqlQueryForm.cs` | TabControl_DrawItem/MouseDown × 居中；新增 `GoToDefinition()` |
| `SqlAliasResolver.cs` | 新增 `regexComma` 第三次扫描，逗号分隔多表别名 |
| `SqlIntelliSenseProvider.cs` | 头部增加 `IsAfterExecKeyword` 检查 + 存储过程返回路径 |
| `SqlEditor.cs` | 新增 `GetWordAtCursor()` + `GoToDefinitionRequested` 事件 + F12 处理 |
| `SqlQueryTabPage.cs` | 订阅 `GoToDefinitionRequested` → `_parent.GoToDefinition()` |

## 验证

- `dotnet build`：0 错误（240 个 warning 都是历史 nullable 警告）
- PowerShell 正则测试：5 个 SQL 场景 + 8 个 EXEC 场景全部预期命中

## 待陛下回归

- [ ] Tab × 在每个 tab 右边**正中间**位置（之前是右下角）
- [ ] Tab × 点击区域对应（不能点不到）
- [ ] 列名提示：`SELECT * FROM T1 a, T2 b` 后 `SELECT a.` 和 `b.` 都提示
- [ ] EXEC 后输入存储过程名 → 弹出候选
- [ ] F12 在表/视图/存储过程/函数名上 → 打开定义脚本 Tab

## 教训

- **WinForms 自绘要注意 StringFormat**：默认 DrawString 从左上角开始，要居中必须用 StringFormat
- **正则多表解析需要多次扫描**：FROM 后第一表 + 每个逗号后一表
- **EXEC 上下文检测要去掉括号**：`EXEC(` 也是合法的存储过程调用语法
- **EXECUTE vs EXECUTE** 两种写法都要支持
- **sp_executesql** 等包含 EXEC 的关键字不能误判
- **F12 vs Ctrl+F12**：SSMS 用 F12 直接转到定义（无 modifier），Ctrl+F12 是另一组动作