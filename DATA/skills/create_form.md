---
name: create_form
title: AI 根据需求创建 A3 表单
category: 表单创建
description: |
  「创建表单」「新增表单」「新建一个XX表单」「帮我做一个XX界面」
  「画一个XX表单」「做一个XX录入」「做一个XX列表」「做一个XX查询」
  「XX表单怎么做」等场景**必须**触发本 Skill。
  输入：业务需求描述（如"设备维修单录入"）。
  输出：完整 S_OBJECT + S_DATA + S_CONTROL 插入 SQL + 创建步骤。
requires_confirmation: true
write_required: true
---

# create_form — AI 根据需求创建 A3 表单

## 触发条件
当用户说"创建表单"、"新增表单"、"新建一个XX表单"、"帮我做一个XX界面"、"画一个XX表单"时触发本 Skill。

## 前置条件
- 数据库连接正常（execute_sql 可用）
- run_code 可用（用于生成 GUID、字符串处理）

## 核心概念

### 三张表的关系
```
S_OBJECT（表单元数据，1条记录）
  ├── S_DATA（数据源，1~N条，定义"从哪取数据"）
  │     └── VIEWNAME = 数据库真实表/视图名
  │         DATASOURCE = SELECT SQL（带 {主关键值} 等占位符）
  │         SAVEFIELDS = 保存时写入的字段列表
  │         RELATIONINFO = 子数据源关联关系
  └── S_CONTROL（控件，1~N条，定义"界面长什么样"）
        ├── 控件类型决定显示什么（文本框/下拉/表格/按钮...）
        ├── VIEWNAME + DATANAME = 绑定到数据源的哪个字段
        ├── PARENTNAME = 放在哪个容器里
        └── EXTENDS = 所有扩展属性（格式 Key|@Value|!Key|@Value|!...）
```

### DATATYPE（表单类型）
| 值 | 类型 | 说明 | 典型场景 |
|----|------|------|---------|
| 0  | 通用 | 列表+录入配对 | S_SCM_OLITEM_LIST(0) + S_SCM_OLITEM_INPUT(0) |
| 1  | 对象 | 带OBJECTTYPE区分录入/查看 | OBJECTTYPE=1为录入 |
| 11 | 主数据 | 物料、客户、仓库等基础档案 | 编辑+列表+子表 |
| 12 | 单据 | 采购单、销售单等业务单据 | 主表+明细表+审批流 |
| 15 | 查询 | 报表、看板、查询界面 | 只读列表+筛选条件 |

### 控件类型速查表（按场景选型）

**输入控件（绑数据字段）：**
| 控件类型 | 用途 | 关键 EXTENDS |
|---------|------|-------------|
| A3Text | 单行文本输入 | `ReadOnly`, `DefaultValue`, `IsMust`, `IsMultiSelect` |
| A3Date | 日期选择 | 同 A3Text，`DefaultValue={系统日期}` |
| A3Decimal | 数字输入 | `DecimalLength`, `Format`, `Formula` |
| A3Integer | 整数输入 | 同 A3Decimal |
| A3Combo | 下拉选择 | `ComboString`(静态), `ComboViewName`(动态), `DisplayPath`, `ValuePath` |
| A3Boolean | 复选框 | `DefaultValue=0` 或 `1` |
| A3MemoText | 多行文本 | 同 A3Text |
| A3RichText | 富文本 | 同 A3Text |
| A3LookUpEdit | 弹窗选择 | `LookUpViewName`, `LookUpCaptionPath`, `LookUpValuePath` |

**容器控件（布局用）：**
| 控件类型 | 用途 | 关键属性 |
|---------|------|---------|
| A3SplitPanel | 左右/上下分割 | 子项是 `.Panel1` 和 `.Panel2`（自动生成） |
| A3SecondSplitPanel | 分割面板子项 | 由 A3SplitPanel 自动创建 |
| A3TableLayoutPanel | 网格布局 | `RowDefine`, `ColumnDefine`（默认2列） |
| A3TabControl | 选项卡容器 | 子项是 A3TabPage |
| A3TabPage | 选项卡页 | `IsVisible` |
| A3Panel | 面板 | `BackColor` |
| A3ToolBar | 工具栏 | 子项是 ToolBarButton |

**列表控件：**
| 控件类型 | 用途 | 关键 EXTENDS |
|---------|------|-------------|
| A3DataGrid | 数据表格 | `ReadOnly=True`, `IsMust=False` |
| GridColumn | 表格列 | `Header`, `DataName`, `Visible`, `Width`, `ReadOnly` |
| A3TreeView | 树形导航 | `HandlerType=Relation` |

**操作控件：**
| 控件类型 | 用途 | 关键 EXTENDS |
|---------|------|-------------|
| ToolBarButton | 工具栏按钮 | `HandlerType`, `BeforeTarget`, `AfterTarget`, `State` |
| A3FilterStringControl | 筛选条件 | `HandlerType=Relation` |
| A3MultiPicture | 图片上传 | `HandlerType`, `State` |

### HandlerType（按钮操作类型）
| 值 | 说明 | 示例 |
|----|------|------|
| ADD | 新增 | `HandlerType=ADD` |
| EDIT | 编辑（打开录入表单） | `HandlerType=Relation`（列表表单用） |
| DELETE | 删除 | `HandlerType=DELETE` |
| VIEW | 查看 | `HandlerType=Relation` |
| SAVE | 保存 | `HandlerType=SAVE` |
| REFRESH | 刷新 | `HandlerType=REFRESH` |
| AUDITING | 审核 | `HandlerType=AUDITING` |
| UNAUDITING | 反审 | `HandlerType=UNAUDITING` |
| EXIT | 退出 | `HandlerType=EXIT` |
| Export | 导出 | `HandlerType=Export` |
| DataTempExport | 数据导出 | `HandlerType=DataTempExport` |
| DataTempImport | 数据导入 | `HandlerType=DataTempImport` |
| BatchEdit | 批量修改 | `HandlerType=BatchEdit` |
| PageSetting | 页面设置 | `HandlerType=PageSetting` |
| MultiSelect | 多选 | `HandlerType=MultiSelect` |
| Relation | 打开关联表单 | `HandlerType=Relation`，需配合 `BeforeTarget` |
| Proc(xxx) | 调用存储过程 | `HandlerType=Proc(S_SCM_OLITEM_MARKET)` |
| PROC(xxx){Parameter\|...} | 带参存储过程 | `HandlerType=PROC(S_SCM_OLITEMSPEC_RECOMMAND){Parameter\|DETAILGUID\|S_SCM_OLITEMSPEC.GUID\|1}` |
| AttachEdit | 附件编辑 | `HandlerType=AttachEdit` |
| AttachView | 附件查看 | `HandlerType=AttachView` |

### State（按钮状态）
| 值 | 含义 |
|----|------|
| 0 | 默认显示 |
| 1 | 默认显示（同0） |
| 2 | 隐藏，可通过页面设置显示 |

---

## 参考模板 1：列表表单 S_SCM_OLITEM_LIST

### 布局结构
```
S_SCM_OLITEM_LIST（商城产品列表，DataType=0）
  ├── A3SplitPanel（根容器，左右分割）
  │     ├── .Panel1（左侧导航区）
  │     │     └── A3Panel → A3TreeView（树形分类导航）
  │     └── .Panel2（右侧数据区）
  │           ├── A3FilterStringControl（筛选条件栏）
  │           └── A3DataGrid（主数据表格，46列）
  └── A3ToolBar（底部工具栏，23个按钮）
```

### S_OBJECT 关键字段
```
GUID: S_SCM_OLITEM_LIST
CODE: S_SCM_OLITEM_LIST
NAME: 商城产品列表
DATATYPE: 0
SUBSYSTEMGUID: 003007
OBJECTGROUPGUID: 00300701
WIDTH: 1010, HEIGHT: 574
AUDITINGFIELD: ISAUDITING          ← 审核状态字段名
AUDITINGPROCNAME: S_SCM_OLITEM_AUDITING
DELETEPROCNAME: S_SCM_OLITEM_DELETE
UNAUDITINGPROCNAME: S_SCM_OLITEM_UNAUDITING
RIGHTGUID: B8CEFD23-...            ← 权限GUID
```

### S_DATA 结构
**主数据源**（S_SCM_OLITEM_LIST01）：
```
VIEWNAME: S_SCM_OLITEM
ISMAINDATASOURCE: 1
MAINKEYWORD: GUID
ISPAGE: 0
DATASOURCE: 多表 JOIN 查询（S_SCM_OLITEM + S_USER + S_ORGANIZATION + S_SCM_BRAND + ...）
            WHERE 1=1 {选择条件} Order By OLITEMTYPE.TYPERANK
SAVEFIELDS: 无（列表表单通常不直接保存）
```

**辅助数据源**（S_SCM_OLITEM_LIST02）：
```
VIEWNAME: S_SCM_OLITEMTYPE
ISMAINDATASOURCE: 0
用途: 树形分类导航的数据源（UNION ALL 构造树结构）
```

### 按钮配置（ToolBarButton）
| 控件名 | Caption | HandlerType | 说明 |
|--------|---------|-------------|------|
| ADD | 新增 | Relation | 打开录入表单 |
| EDIT | 修改 | Relation | 打开录入表单（编辑模式） |
| VIEW | 查看 | Relation | 打开录入表单（查看模式） |
| DELETE | 删除 | DELETE | 删除记录 |
| AUDITING | 审核 | AUDITING | 审核 |
| UNAUDITING | 反审 | UNAUDITING | 反审 |
| REFRESH | 刷新 | REFRESH | 刷新列表 |
| Export | 导出 | Export | 导出数据 |
| DataTempImport1 | 导入更新 | DataTempImport | 数据导入 |
| DataTempExport1 | 数据导出 | DataTempExport | 数据导出 |
| Forbidden | 禁用 | Proc(S_SCM_OLITEM_FORBIDDEN) | 调用存储过程 |
| UnForbidden | 启用 | Proc(S_SCM_OLITEM_UNFORBIDDEN) | 调用存储过程 |
| MARKET | 上架 | PROC(S_SCM_OLITEM_MARKET) | 调用存储过程 |
| UNMARKET | 下架 | PROC(S_SCM_OLITEM_UNMARKET) | 调用存储过程 |
| BatchEdit | 批量修改 | BatchEdit | 批量修改 |
| MultiSelect | 多选 | MultiSelect | 多选模式 |
| PageSetting | 页面设置 | PageSetting | 页面设置 |
| EXIT | 退出 | EXIT | 退出 |

### Grid 列配置示例
```
GridColumn: CONTROLNAME=S_SCM_OLITEM_CODE, Caption=代码, View=S_SCM_OLITEM, Data=CODE
  EXT={Header=代码, DataName=CODE, Visible=1, Width=100, ReadOnly=1, IsMust=0}

GridColumn: CONTROLNAME=S_SCM_OLITEM_NAME, Caption=名称, View=S_SCM_OLITEM, Data=NAME
  EXT={Header=名称, DataName=NAME, Visible=1, Width=369, ReadOnly=1, IsMust=0}

GridColumn: CONTROLNAME=S_SCM_OLITEM_GUID, Caption=GUID, View=S_SCM_OLITEM, Data=GUID [HIDDEN]
  EXT={Header=GUID, DataName=GUID, Visible=0, Width=100, ReadOnly=1, IsMust=0}
```

---

## 参考模板 2：录入表单 S_SCM_OLITEM_INPUT

### 布局结构
```
S_SCM_OLITEM_INPUT（商城产品录入，DataType=0, ObjectType=1）
  ├── A3SplitPanel（根容器，上下分割）
  │     ├── .Panel1（上半部分：状态栏 + 表单字段）
  │     │     ├── A3Panel（标题栏：A3Label "商城产品"）
  │     │     └── A3SplitPanel（上下再分割）
  │     │           ├── .Panel1（状态字段区：审核/禁用/上架等）
  │     │           │     └── A3TableLayoutPanel1（2列网格）
  │     │           └── .Panel2（核心字段区）
  │     │                 ├── A3TableLayoutPanel2（基本属性）
  │     │                 └── A3TableLayoutPanel3（分类/品牌等）
  │     └── .Panel2（下半部分：Tab页）
  │           └── A3TabControl（ContentHolder）
  │                 ├── TabPage[基本信息] → A3SplitPanel
  │                 │     ├── .Panel1 → A3TableLayoutPanel3（分类字段）
  │                 │     └── .Panel2 → A3DataGrid（规格明细）+ A3ToolBar
  │                 ├── TabPage[标题图片] → A3MultiPicture
  │                 ├── TabPage[滚动图册] → A3MultiPicture
  │                 ├── TabPage[商品详情] → A3MultiPicture
  │                 ├── TabPage[商品详情描述] → A3RichText
  │                 └── TabPage[隐藏] [HIDDEN] → GUID/VIDEO 等隐藏字段
  └── A3ToolBar（底部工具栏）
```

### S_OBJECT 关键字段
```
GUID: S_SCM_OLITEM_INPUT
CODE: S_SCM_OLITEM_INPUT
NAME: 商城产品录入
DATATYPE: 0
OBJECTTYPE: 1                              ← 1=录入表单
SUBSYSTEMGUID: 003007
WIDTH: 962, HEIGHT: 752
AUDITINGFIELD: ISAUDITING
AFTERADDSAVESQL: EXEC S_SCM_OLITEM_AS '{主关键值}','{保存类型}'
AFTEREDITSAVESQL: EXEC S_SCM_OLITEM_AS '{主关键值}','{保存类型}'
```

### S_DATA 结构
**主数据源**（S_SCM_OLITEM_INPUT01）：
```
VIEWNAME: S_SCM_OLITEM
ISMAINDATASOURCE: 1
MAINKEYWORD: GUID
DATASOURCE: SELECT 字段1,字段2,... FROM S_SCM_OLITEM Where GUID='{主关键值}'
SAVEFIELDS: GUID,BRANDGUID,CODE,NAME,SPEC,...（保存时写入的字段列表）
```

**子数据源**（S_SCM_OLITEM_INPUT02）：
```
VIEWNAME: S_SCM_OLITEMSPEC
ISMAINDATASOURCE: 0
RELATIONINFO: [OLITEMGUID,S_SCM_OLITEM_INPUT01.GUID]    ← 子表.外键 = 主表.CODE.主键
DATASOURCE: SELECT ... FROM S_SCM_OLITEMSPEC A
            LEFT JOIN S_SCM_ITEM ITEM ON A.ITEMGUID=ITEM.GUID
            ...
            Where A.OLITEMGUID='{主关键值}' ORDER BY A.DETAILNO
SAVEFIELDS: GUID, OLITEMGUID, DETAILNO, ITEMGUID, DESCRIPTION,...
```

### 控件配置示例

**A3Text 输入控件：**
```
CONTROLNAME: CODE
CONTROLTYPE: A3Text
CAPTION: 代码*
VIEWNAME: S_SCM_OLITEM
DATANAME: CODE
PARENTNAME: A3TableLayoutPanel3
ISMUST: 1                                   ← 必填
ISREADONLY: 1                               ← 只读（代码自动生成）
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@True|!IsMust|@True|!DefaultValue|@|!IsMultiSelect|@False|!
```

**A3Boolean 复选框：**
```
CONTROLNAME: A3Boolean5
CONTROLTYPE: A3Boolean
CAPTION: 是否多规格
VIEWNAME: S_SCM_OLITEM
DATANAME: ISMULTISPEC
PARENTNAME: A3TableLayoutPanel2
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@False|!IsMust|@False|!DefaultValue|@0|!
```

**A3Combo 下拉：**
```
CONTROLNAME: A3Combo1
CONTROLTYPE: A3Combo
CAPTION: 产品分区
VIEWNAME: S_SCM_OLITEM
DATANAME: ITEMSUBAREA
PARENTNAME: A3TableLayoutPanel3
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@False|!IsMust|@False|!DefaultValue|@1|!DisplayPath|@Caption|!ComboViewName|@|!
```

**A3Date 日期：**
```
CONTROLNAME: A3Date2
CONTROLTYPE: A3Date
CAPTION: 制单日期
VIEWNAME: S_SCM_OLITEM
DATANAME: CREATEDATE
PARENTNAME: A3TableLayoutPanel1
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@True|!IsMust|@False|!DefaultValue|@{系统日期}|!
```

**A3Decimal 数字：**
```
CONTROLNAME: A3Decimal1
CONTROLTYPE: A3Decimal
CAPTION: 最小包装量
VIEWNAME: S_SCM_OLITEM
DATANAME: UNITQTY
PARENTNAME: A3TableLayoutPanel2
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@False|!IsMust|@False|!DefaultValue|@|!Formula|@|!
```

**A3MultiPicture 图片：**
```
CONTROLNAME: A3MultiPicture1
CONTROLTYPE: A3MultiPicture
CAPTION: 标题图片
VIEWNAME: S_SCM_OLITEM
DATANAME: CAPTIONIMG
PARENTNAME: XtraTabPage_1
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@False|!IsMust|@False|!DefaultValue|@|!HandlerType|@|!State|@|!
```

**A3RichText 富文本：**
```
CONTROLNAME: RICHTEXTDECS
CONTROLTYPE: A3RichText
CAPTION: 提示
VIEWNAME: S_SCM_OLITEM
DATANAME: RICHTEXTDECS
PARENTNAME: XtraTabPage_5
EXTENDS: AutoTab|@True|!IsAutoSendTab|@True|!ReadOnly|@False|!IsMust|@False|!DefaultValue|@|!
```

**隐藏字段（放在隐藏TabPage中）：**
```
CONTROLNAME: GUID
CONTROLTYPE: A3Text
CAPTION: GUID
VIEWNAME: S_SCM_OLITEM
DATANAME: GUID
PARENTNAME: XtraTabPage_4                    ← 隐藏的 TabPage
ISVISIBLE: 0
EXTENDS: ...DefaultValue|@NewID()|!          ← 新增时自动生成GUID
```

**GridColumn 子表列（可编辑）：**
```
CONTROLNAME: S_SCM_OLITEMSPEC_ITEMCODE
CONTROLTYPE: GridColumn
CAPTION: 产品代码
VIEWNAME: S_SCM_OLITEMSPEC
DATANAME: ITEMCODE
PARENTNAME: S_SCM_OLITEMSPEC                 ← 父级是 A3DataGrid
ISMUST: 1                                    ← 子表必填列
EXTENDS: Header|@产品代码|!DataName|@ITEMCODE|!Visible|@1|!Width|@161|!ReadOnly|@0|!IsMust|@1|!IsMultiSelect|@1|!
```

**工具栏按钮（录入表单）：**
```
CONTROLNAME: ADD
CONTROLTYPE: ToolBarButton
CAPTION: 新增
PARENTNAME: A3ToolBarDesigner1
EXTENDS: HandlerType|@ADD|!BeforeTarget|@|!AfterTarget|@|!State|@1|!
```

---

## 执行流程

### 第 0 步：理解需求
和用户确认：
1. **表单名称**：叫什么（如"设备维修单"）
2. **表单类型**：列表? 录入? 列表+录入配对?
3. **业务字段**：需要哪些字段
4. **数据来源**：新建表 or 已有表
5. **特殊需求**：审批流? 打印? 图片? 子表?

### 第 1 步：生成 S_OBJECT
```sql
-- 先检查 CODE 是否已存在
SELECT COUNT(*) FROM S_OBJECT WHERE CODE = '{formCode}'
```

用 run_code 生成新 GUID：`Guid.NewGuid().ToString("D").ToUpper()`

### 第 2 步：设计 S_DATA
**主数据源模板：**
```sql
SELECT 字段1, 字段2, ... FROM 真实表名 WHERE GUID='{主关键值}'
-- SAVEFIELDS: 字段1, 字段2, ...
```

**子数据源模板：**
```sql
SELECT ... FROM 明细表名 WHERE 主表外键='{主关键值}' ORDER BY DETAILNO
-- RELATIONINFO: [子表外键字段,主数据源CODE.主键字段]
```

### 第 3 步：设计 S_CONTROL
1. 先搭容器骨架（SplitPanel → TableLayoutPanel → TabControl）
2. 再放输入控件（A3Text/A3Date/A3Boolean/...）
3. 再放列表控件（A3DataGrid + GridColumn）
4. 最后放按钮（ToolBarButton）

### 第 4 步：验证 & 创建
创建前检查清单：
1. ✅ S_OBJECT 的 CODE 唯一
2. ✅ VIEWNAME 在数据库中存在
3. ✅ DATASOURCE SQL 可执行
4. ✅ SAVEFIELDS 字段在表中存在
5. ✅ PARENTNAME 引用的控件已创建
6. ✅ RELATIONINFO 格式正确
7. ✅ GUID 无重复

创建顺序：S_OBJECT → S_DATA → S_CONTROL（容器先建，控件后建）

### 第 5 步：汇报
```
✅ 表单创建成功！
表单: {NAME}（{CODE}）
数据源: {n} 个 | 控件: {m} 个
⚠️ 需要在 A3 平台中分配权限（RIGHTGUID）
```

---

## 常见陷阱

1. **VIEWNAME 必须是数据库真实存在的表/视图** — 先用 `INFORMATION_SCHEMA` 验证
2. **DATASOURCE 占位符**：`{主关键值}`, `{用户内码}`, `{组织机构内码}`, `{选择条件}` 是平台运行时替换的
3. **EXTENDS 格式**：`Key|@Value|!` 不是 JSON，是管道格式
4. **容器先建，控件后建** — PARENTNAME 必须指向已存在的控件
5. **A3SplitPanel 会自动创建 .Panel1 和 .Panel2** — 不需要手动创建这两个子控件
6. **RELATIONINFO 格式**：`[子列,父数据源CODE.父列]`，注意是 S_DATA 的 CODE 不是表名
7. **隐藏字段放在隐藏 TabPage 中** — `ISVISIBLE=0` 或 Parent 指向隐藏 TabPage
8. **DefaultValue 特殊值**：`{用户内码}`, `{组织机构内码}`, `{系统日期}`, `NewID()`
9. **列表表单和录入表单是配对的** — 列表的 ADD/EDIT 按钮通过 Relation 打开录入表单