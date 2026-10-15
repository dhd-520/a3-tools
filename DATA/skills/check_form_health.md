---
name: check_form_health
title: A3 表单深度诊断（31 项检查）
category: 表单诊断
description: |
  「检查表单」「表单问题」「诊断表单」「表单健康检查」
  「检查XX表单」「检查XX菜单」「XX页面有问题」「表单有 bug」
  「XX表单报错」「表单控件问题」等场景**必须**触发本 Skill。
  输入：菜单名 / 表单 CODE / 表单 GUID（通常陛下给的是前端菜单名）。
  输出：6 层共 31 项深度检查报告 + 修复建议。
requires_confirmation: false
write_required: false
---

# check_form_health — A3 表单深度诊断

## 触发条件
当用户说"检查表单"、"表单问题"、"诊断表单"、"表单健康检查"、"检查XX表单"、"检查XX菜单"、"XX页面有问题"时触发本 Skill。

**重要**：用户通常给的是前端菜单名称（如"商城产品"、"采购订单"），不是表单 CODE。需要通过 S_MENU 表查找对应的表单。

## 前置条件
- 数据库连接正常（execute_sql 可用）
- 用户指定要检查的菜单名或表单名（名称模糊搜索、菜单名或 GUID 精确匹配）

## 执行流程

### 第 0 步：通过菜单名定位表单

用户给的通常是前端菜单名称，需要先通过 S_MENU → S_OBJECT 关联找到表单。

**S_MENU 表结构**：
- `GUID` — 菜单主键
- `NAME` — 菜单显示名（用户看到的名字）
- `FULLNAME` — 完整路径（如 `供应链/商城管理/商城基础设置/商城产品`）
- `OBJECTGUID` — 对应的表单 GUID（关联 S_OBJECT.GUID）
- `ISVISIBLE` — 是否可见（1=可见，0=隐藏）
- `PARENTGUID` — 父菜单 GUID

**查找逻辑**：

```sql
-- 方式1：按菜单名模糊搜索
SELECT m.GUID MenuGuid, m.NAME MenuName, m.FULLNAME MenuPath, m.ISVISIBLE,
       m.OBJECTGUID, o.CODE FormCode, o.NAME FormName, o.DATATYPE
FROM S_MENU m
LEFT JOIN S_OBJECT o ON m.OBJECTGUID = o.GUID
WHERE m.NAME LIKE '%{用户输入}%'
ORDER BY m.NAME

-- 方式2：如果用户直接给了表单 CODE
SELECT m.GUID MenuGuid, m.NAME MenuName, m.FULLNAME MenuPath,
       m.OBJECTGUID, o.CODE FormCode, o.NAME FormName, o.DATATYPE
FROM S_MENU m
RIGHT JOIN S_OBJECT o ON m.OBJECTGUID = o.GUID
WHERE o.CODE = '{用户输入}' OR o.NAME LIKE '%{用户输入}%'
```

**注意**：
- 一个表单可能被多个菜单引用（如 MRP控制台 被两个菜单引用）
- 有些菜单没有 OBJECTGUID（文件夹菜单，466个），跳过
- OBJECTGUID 可能是表单 CODE（如 `ITEMLIST`）而非标准 GUID
- 如果找到多个匹配，列出让用户选择
- 如果 S_MENU 中找不到，直接在 S_OBJECT 中按 NAME/CODE 搜索

让用户确认目标表单后，继续后续检查。

### 第 1 步：获取表单基础信息

```sql
-- 表单元数据
SELECT GUID, CODE, NAME, SUBSYSTEMGUID, DATATYPE, PROCINFO,
       AUDITINGPROCNAME, DELETEPROCNAME, UNAUDITINGPROCNAME,
       APPROVEFLOWGUID, RIGHTGUID, AUDITINGFIELD
FROM S_OBJECT WHERE GUID = '{formGuid}'

-- 控件总数
SELECT COUNT(*) AS ControlCount FROM S_CONTROL WHERE OBJECTGUID = '{formGuid}'

-- 数据源总数
SELECT COUNT(*) AS DataCount FROM S_DATA WHERE OBJECTGUID = '{formGuid}'
```

### 第 2 步：加载全量控件和数据源

**控件数据**（用于后续所有检查）：
```sql
SELECT GUID, CONTROLNAME, PARENTNAME, CONTROLTYPE, ASSEMBLYNAME,
       CAPTION, VIEWNAME, DATANAME, X, Y, WIDTH, HEIGHT,
       ISMUST, ISREADONLY, ISVISIBLE, EXTENDS
FROM S_CONTROL
WHERE OBJECTGUID = '{formGuid}'
ORDER BY CONTROLTYPE, CONTROLNAME
```

**数据源数据**：
```sql
SELECT GUID, CODE, NAME, DATASOURCE, SAVEFIELDS, VIEWNAME,
       PARENTVIEWNAME, REALTABLENAME, ISMAINDATASOURCE,
       MAINKEYWORD, RELATIONINFO, FILTERSTRING, ISPAGE, PAGECLIENT
FROM S_DATA
WHERE OBJECTGUID = '{formGuid}'
ORDER BY ISMAINDATASOURCE DESC, CODE
```

**用 C# 工具加载结果到内存**（后续检查需要解析 EXTENDS 字段）。

---

## 层 1：结构完整性检查

### #1 孤儿控件
```sql
SELECT c.GUID, c.CONTROLNAME, c.CONTROLTYPE
FROM S_CONTROL c
LEFT JOIN S_OBJECT o ON c.OBJECTGUID = o.GUID
WHERE c.OBJECTGUID = '{formGuid}' AND o.GUID IS NULL
```
> 正常情况下不会命中（因为已经按 OBJECTGUID 过滤了），但如果有外键异常会捕获。

### #2 孤儿数据源
```sql
SELECT d.GUID, d.CODE, d.NAME
FROM S_DATA d
LEFT JOIN S_OBJECT o ON d.OBJECTGUID = o.GUID
WHERE d.OBJECTGUID = '{formGuid}' AND o.GUID IS NULL
```

### #3 父控件断裂
对表单内所有非容器控件（排除 A3TabPage/A3TabControl/A3Panel/A3Splitter/GroupBox/A3ToolBar/A3TableLayoutPanel/A3SecondSplitPanel），检查其 PARENTNAME 是否在同表单的 CONTROLNAME 中存在。

**用 C# 检查**：遍历控件列表，对每个非容器控件的 PARENTNAME 在控件集合中查找。找不到的记录为断裂。

### #4 GUID 重复
```sql
SELECT GUID, COUNT(*) cnt
FROM (
    SELECT GUID FROM S_CONTROL WHERE OBJECTGUID = '{formGuid}'
    UNION ALL
    SELECT GUID FROM S_DATA WHERE OBJECTGUID = '{formGuid}'
) t
GROUP BY GUID HAVING COUNT(*) > 1
```

---

## 层 2：绑定校验

### #5 控件 VIEWNAME 孤儿绑定
对表单内每个有 VIEWNAME 的控件（排除 ToolBarButton/ToolBarSeparator/A3TabPage/A3TabControl/A3Panel/A3Splitter/GroupBox/A3Label/A3StatusPicture/A3Image/A3ImgShow/A3MultiPicture/A3WebBrowser/A3Link/A3Task/A3Progress/A3MultiState/A3MultiDate/A3QueryState/A3Loadometer/A3FilterStringControl），检查其 VIEWNAME 是否在同表单的 S_DATA.VIEWNAME 中存在。

**用 C# 检查**：收集本表单所有 S_DATA.VIEWNAME 为集合，遍历控件检查。

**例外情况**：如果控件的 VIEWNAME 与 OBJECTGUID 同名（如 `S_SCM_SEORDER`），可能是平台隐式数据源，标记为 ⚠️ 而非 ❌。

### #6 GridColumn DataName 列不存在
对每个 CONTROLTYPE = 'GridColumn' 的控件：
1. 从 EXTENDS 解析 `DataName` 值
2. 从 EXTENDS 解析 `Header` 值（用于报告展示）
3. 找到该 Grid 所属的数据源 VIEWNAME（通过 PARENTNAME 找父 Grid → Grid 的 VIEWNAME）
4. 查询 `INFORMATION_SCHEMA.COLUMNS` 检查 DataName 是否存在

```sql
-- 检查某列是否存在于某表/视图
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = '{viewName}' AND COLUMN_NAME = '{dataName}'
```

**注意**：DataName 可能是计算列（如 `Formula` 中定义的），这种情况标记为"计算列，跳过"。

### #7 数据源 VIEWNAME 不存在
```sql
-- 检查 VIEWNAME 是否是真实表或视图
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{viewName}'
UNION
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_NAME = '{viewName}'
```

对本表单每个 S_DATA.VIEWNAME 逐一检查。

### #8 SAVEFIELDS 与真实列交叉
对每个有 SAVEFIELDS 的数据源：
1. 按逗号拆分 SAVEFIELDS
2. 对每个字段名，查询 INFORMATION_SCHEMA.COLUMNS
3. 不存在的记录为断裂

### #9 RELATIONINFO 断裂
解析 RELATIONINFO 格式 `[子列,父数据源VIEWNAME.父列]`：
1. 子列：检查是否存在
2. 父数据源 VIEWNAME：检查是否存在
3. 父列：在父数据源对应的表中检查

### #10 无主数据源
```sql
SELECT COUNT(*) FROM S_DATA
WHERE OBJECTGUID = '{formGuid}' AND ISMAINDATASOURCE = '1'
```
如果为 0 但有数据源，标记 ⚠️。

---

## 层 3：数据源 SQL 深度解析

### #11 SQL FROM/JOIN 引用表不存在
对每个数据源的 DATASOURCE 字段：
1. 用正则提取 `FROM\s+(\w+)` 和 `JOIN\s+(\w+)` 中的表名
2. 对每个表名查询 INFORMATION_SCHEMA.TABLES
3. 不存在的记录

**注意**：排除子查询中的 FROM（需要括号匹配）。如果 SQL 太复杂无法解析，标记为"SQL 复杂度超限，建议人工检查"。

### #12 SQL SELECT 引用列不存在
如果 SQL 是简单的 `SELECT col1, col2 FROM table` 格式：
1. 提取 SELECT 和 FROM 之间的列名
2. 对每个列名在表中检查

**注意**：跳过 `*`、`AS` 别名、函数调用（`ISNULL(...)` 等）、`CASE WHEN`。只检查简单的 `表别名.列名` 或裸列名。

### #13 SELECT ***
检查 DATASOURCE 是否以 `SELECT *` 开头（忽略大小写和空白）。

### #14 无分页数据源
检查 ISPAGE 是否为 '0' 且 DATASOURCE 中无 TOP 关键字。

---

## 层 4：EXTENDS 属性一致性

### EXTENDS 解析函数
所有 EXTENDS 解析统一用以下逻辑：

```
输入: ext (string) — EXTENDS 字段值
输出: Dictionary<string, string>

逻辑:
1. 按 "|!" 拆分为 parts
2. 对每个 part，找 "|@" 的位置
3. "|" 左边为 key，右边为 value
4. 返回 key-value 字典
```

### #15 必填但不可见
遍历所有控件：
- 检查 EXTENDS 中是否有 `Must` 或 `Required` key 且值为 `True`/`1`
- 同时 EXTENDS 中 `Visible` = `0` 或 `False`
- 或者 S_CONTROL.ISVISIBLE = '0'

### #16 必填但只读
- EXTENDS 中 `Must`/`Required` = `True`/`1`
- 同时 `ReadOnly` = `True`/`1`
- 或者 S_CONTROL.ISREADONLY = '1'

### #17 GridColumn Visible=0 但有 DataName
- CONTROLTYPE = 'GridColumn'
- EXTENDS 中 `Visible` = `0`
- EXTENDS 中有非空 `DataName`
- 标记为"隐藏列，可能是遗留"

### #18 GridColumn Locked=1 且 Visible=0
- EXTENDS 中 `Locked` = `1` 且 `Visible` = `0`
- 逻辑矛盾：锁定一个看不到的列

### #19 HandlerType 无效
对 CONTROLTYPE = 'ToolBarButton' 的控件：
- 从 EXTENDS 解析 `HandlerType`
- 有效值列表：`ADD`, `EDIT`, `DELETE`, `VIEW`, `REFRESH`, `PRINT`, `IMPORT`, `EXPORT`, `AUDITING`, `UNAUDITING`, `CHECKOUT`, `CUSTOM`, `SAVE`, `CLOSE`, `EXIT`, `PRINTPREVIEW`, `SENDDOWN`, `SENDBACK`, `SENDCANCEL`, `SENDCONFIRM`, `SENDUNDO`, `SENDVIEW`, `SENDPRINT`, `SENDEMAIL`, `SENDSMS`, `SENDWX`, `SENDAPP`, `SENDOTHER`
- 不在列表中的记录

### #20 BeforeTarget/AfterTarget 断裂
- `BeforeTarget` / `AfterTarget` 格式通常为 `表单GUID` 或 `存储过程名`
- 检查引用的表单 GUID 是否在 S_OBJECT 中存在
- 如果是存储过程名，检查 sys.procedures

### #21 ComboViewName 不存在
对 A3Combo / A3LookUpEdit 控件：
- 从 EXTENDS 解析 `ComboViewName`
- 检查是否在 S_DATA.VIEWNAME 或 INFORMATION_SCHEMA.TABLES 中存在

### #22 DisplayPath 断裂
- `DisplayPath` 通常格式为 `VIEWNAME.COLUMNNAME`
- 检查 VIEWNAME 和 COLUMN 是否存在

### #23 Formula 语法检查
- 从 EXTENDS 解析 `Formula`
- 基本检查：括号匹配 `()`、引号闭合 `''`
- 如果引用了列名（格式 `[列名]`），检查是否存在

---

## 层 5：业务逻辑 & 存储过程

### #24 存储过程不存在（主字段）
```sql
SELECT NAME, AUDITINGPROCNAME, DELETEPROCNAME, UNAUDITINGPROCNAME
FROM S_OBJECT
WHERE GUID = '{formGuid}'
```

对每个非空的存储过程名：
```sql
SELECT name FROM sys.procedures WHERE name = '{procName}'
```

### #25 存储过程不存在（PROCINFO EXTENDS 格式）
PROCINFO 字段使用 EXTENDS 格式：`DELETE|@USERDELETE|!AUDITING|@|!UNAUDITING|@`

解析逻辑：
1. 按 `|!` 拆分
2. 每个 part 按 `|@` 拆分得到 key/value
3. key 是操作类型（DELETE/AUDITING/UNAUDITING），value 是存储过程名
4. 对每个非空的存储过程名检查 sys.procedures

### #26 审批流 GUID 断裂
```sql
SELECT APPROVEFLOWGUID FROM S_OBJECT WHERE GUID = '{formGuid}'
```
如果非空：
```sql
SELECT GUID FROM S_APPROVEFLOW WHERE GUID = '{approveFlowGuid}'
```

### #27 权限 GUID 断裂
```sql
SELECT RIGHTGUID FROM S_OBJECT WHERE GUID = '{formGuid}'
```
如果非空，检查权限表中是否存在。

---

## 层 6：性能 & 最佳实践

### #28 超大 Grid
```sql
SELECT PARENTNAME, COUNT(*) ColCount
FROM S_CONTROL
WHERE OBJECTGUID = '{formGuid}' AND CONTROLTYPE = 'GridColumn'
GROUP BY PARENTNAME
HAVING COUNT(*) > 50
ORDER BY ColCount DESC
```

### #29 超大表单
```sql
-- 控件数 > 200
SELECT COUNT(*) FROM S_CONTROL WHERE OBJECTGUID = '{formGuid}'
-- 数据源数 > 10
SELECT COUNT(*) FROM S_DATA WHERE OBJECTGUID = '{formGuid}'
```

### #30 同名控件
```sql
SELECT CONTROLNAME, COUNT(*) cnt
FROM S_CONTROL
WHERE OBJECTGUID = '{formGuid}'
GROUP BY CONTROLNAME
HAVING COUNT(*) > 1
ORDER BY cnt DESC
```

### #31 相同 CODE 不同 NAME
```sql
SELECT CODE, COUNT(DISTINCT NAME) nameCnt, STRING_AGG(NAME, ', ') Names
FROM S_OBJECT
WHERE CODE = '{formCode}'
GROUP BY CODE
HAVING COUNT(DISTINCT NAME) > 1
```

---

## 输出格式

```
═══════════════════════════════════════════════════════
🔍 A3 表单深度诊断报告
═══════════════════════════════════════════════════════
表单: {NAME}（{CODE}）
GUID: {GUID}
子系统: {SUBSYSTEMGUID}
控件总数: {n} | 数据源: {m} | 检查项: 31
诊断时间: {timestamp}

───────────────────────────────────────────────────────
🔴 层 1：结构完整性
───────────────────────────────────────────────────────
  ✅ #1 孤儿控件：无
  ✅ #2 孤儿数据源：无
  ❌ #3 父控件断裂（2 项）：
     • 控件 [A3Text5] PARENTNAME=A3Panel3 → 同表单中不存在
     • 控件 [A3Combo2] PARENTNAME=A3TableLayoutPanel2 → 同表单中不存在
  ✅ #4 GUID 重复：无

───────────────────────────────────────────────────────
🟡 层 2：绑定校验
───────────────────────────────────────────────────────
  ❌ #5 控件 VIEWNAME 孤儿绑定（3 项）：
     • 控件 [ToolBar1] VIEWNAME=S_DATASELECT → 同表单 S_DATA 中无此 VIEWNAME
     • 控件 [A3DataGrid1] VIEWNAME=S_SCM_PRICECUSTOM → 同表单 S_DATA 中无此 VIEWNAME
     • 控件 [A3Text1] VIEWNAME=TaskData → 同表单 S_DATA 中无此 VIEWNAME
  ❌ #6 GridColumn DataName 列不存在（1 项）：
     • GridColumn [BRANDNAME] DataName=BRANDNAME → VIEWNAME=S_SCM_OLITEM 中无此列（最接近: BRANDGUID）
  ⚠️ #7 数据源 VIEWNAME 不存在：全部存在
  ⚠️ #8 SAVEFIELDS 断裂（1 项）：
     • 数据源 [S_SCM_OLITEM_INPUT01] SAVEFIELDS 中的 OLDCODE 在 S_SCM_OLITEM 中不存在
  ✅ #9 RELATIONINFO 断裂：无
  ✅ #10 无主数据源：有主数据源

───────────────────────────────────────────────────────
🟠 层 3：数据源 SQL 深度解析
───────────────────────────────────────────────────────
  ❌ #11 SQL 引用表不存在（1 项）：
     • 数据源 [S_SCM_OLITEM_INPUT01] SQL 引用 S_SCM_BRAND → 表不存在
  ⚠️ #12 SQL 引用列不存在：无法自动解析（SQL 复杂度超限）
  ✅ #13 SELECT *：无
  ⚠️ #14 无分页数据源（1 项）：
     • 数据源 [S_SCM_OLITEM_INPUT01] ISPAGE=0 且无 TOP，S_SCM_OLITEM 有 50,000+ 行

───────────────────────────────────────────────────────
🔵 层 4：EXTENDS 属性一致性
───────────────────────────────────────────────────────
  ⚠️ #15 必填但不可见（2 项）：
     • 控件 [QTY] Must=True, Visible=0
     • 控件 [PRICE] Must=True, Visible=0
  ✅ #16 必填但只读：无
  ⚠️ #17 GridColumn Visible=0 但有 DataName（15 项）：
     • [BRANDNAME] Visible=0, DataName=BRANDNAME
     • [SPEC] Visible=0, DataName=SPEC
     • ... (13 more)
  ✅ #18 Locked+Visible 矛盾：无
  ⚠️ #19 HandlerType 无效（1 项）：
     • ToolBarButton [CUSTOM1] HandlerType=CUSTOMDATAIMPORT（不在有效列表中）
  ❌ #20 BeforeTarget/AfterTarget 断裂（1 项）：
     • ToolBarButton [SENDDOWN] BeforeTarget=XXX-YYY → S_OBJECT 中不存在
  ✅ #21 ComboViewName 不存在：全部存在
  ✅ #22 DisplayPath 断裂：无
  ✅ #23 Formula 语法：全部正常

───────────────────────────────────────────────────────
🟢 层 5：业务逻辑 & 存储过程
───────────────────────────────────────────────────────
  ❌ #24 存储过程不存在（1 项）：
     • DELETEPROCNAME=S_SCM_OLITEM_DELETE → sys.procedures 中不存在
  ✅ #25 PROCINFO 存储过程：全部存在
  ✅ #26 审批流 GUID：正常
  ✅ #27 权限 GUID：正常

───────────────────────────────────────────────────────
⚪ 层 6：性能 & 最佳实践
───────────────────────────────────────────────────────
  ⚠️ #28 超大 Grid（1 项）：
     • Grid [S_SCM_OLITEM] 有 153 列（建议 < 50）
  ⚠️ #29 超大表单：控件 156 个（建议 < 200），数据源 3 个
  ✅ #30 同名控件：无
  ✅ #31 相同 CODE 不同 NAME：无

═══════════════════════════════════════════════════════
📊 诊断汇总
═══════════════════════════════════════════════════════
❌ 错误: 5 项（必须修复）
⚠️ 警告: 6 项（建议检查）
✅ 通过: 20 项

修复优先级：
  1. #6 GridColumn DataName 列不存在 → 改 DATANAME 或加列
  2. #11 SQL 引用表不存在 → 修改数据源 SQL
  3. #24 存储过程不存在 → 创建存储过程或更新引用
  4. #5 控件 VIEWNAME 孤儿绑定 → 检查是否需要补充数据源
  5. #20 BeforeTarget 断裂 → 更新按钮目标
═══════════════════════════════════════════════════════
```

---

## 注意事项

1. **SQL Server 版本兼容**：0080 账套的 SQL Server 不支持 `STRING_SPLIT`（可能是 2014 或更早），所有 EXTENDS 解析必须在 C# 端做
2. **EXTENDS 格式**：`Key|@Value|!Key|@Value|!...`，`|@` 分隔键值，`|!` 分隔条目
3. **PROCINFO 双格式**：S_OBJECT 的 PROCINFO 字段也用 EXTENDS 格式，而 AUDITINGPROCNAME/DELETEPROCNAME 是纯名称
4. **大量控件时分批检查**：如果控件 > 500，每批查询 100 个，避免 SQL 过长
5. **INFORMATION_SCHEMA 优先**：用 `INFORMATION_SCHEMA.COLUMNS/TABLES` 而非 `sys.columns`，跨数据库兼容性更好
6. **报告截断**：如果某检查项命中 > 20 条，只显示前 20 条 + 总数