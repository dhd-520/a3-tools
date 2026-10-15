---
name: add_form_schema_to_kb
title: 从表单提取表结构加入知识库
category: 知识库
requires_confirmation: true
write_required: true
description: |
  ⚠️ **强制优先**（2026-09-30 陛下要求）：当陛下提到以下场景时，**必须** load_skill("add_form_schema_to_kb")：
  - 「从表单提取表结构加入知识库」/「把表单的表结构加到 KB」/「表单 XXX 的表加到 KB」
  - 「把这个表单的表结构同步到知识库」/「表单用到的表加到 KB」/「表单引用的表加到 KB」
  - 「表单表结构喂给 AI」/「让 AI 知道这个表单的表结构」/「表单对应的真实表加到 KB」
  - 同时含「表单」+「表结构/字段/列」组合的请求
  - 任何含 "S_CONTROL" / "VIEWNAME" / "DATANAME" / "CAPTION" / "A3Combo" / "A3Text" / "ComboString" / "DataSelectCode" 的请求

  **2026-10-15 陛下新增**：处理 S_CONTROL 时支持以下控件元数据提取（与表结构一起入库）：
  - **CONTROLTYPE = A3Combo** → 自动从 `EXTENDS` 解析 `ComboString` 字段，提取下拉选项（value+label 列表）
  - **CONTROLTYPE = A3Text** → 检查 `EXTENDS.DataSelectCode` 字段，非空即为「标准查询文本框」，记录查询代码

  **绝对不要**用知识库工作流的 add_knowledge_entry 直接存 —— 本 Skill 要先查 S_CONTROL、
  去重 VIEWNAME、用 INFORMATION_SCHEMA 验证 DATANAME 真实存在（过滤关联字段）、
  再按表切片入库（一张表 = 一条 entry）。

  陛下提供账套编码 + A3 表单内码（OBJECTGUID），AI 自动从 S_CONTROL 表提取该表单
  引用的所有视图(View)及其列定义，并把"真实存在的列"以"一张表 = 一条知识条目"
  的粒度加入指定知识库。关联字段（VIEWNAME 为空、DATANAME 为空、或者在真实表结构中
  找不到的 DATANAME）会自动过滤掉，不进知识库。

trigger_keywords:
  加到知识库,加入 KB,同步到 KB,提取表结构到知识库,把表加到 KB,表单表结构,表单用到的表,表单引用的表,表单对应的真实表,喂给 AI,S_CONTROL,VIEWNAME,DATANAME,CAPTION,A3Combo,A3Text,ComboString,DataSelectCode,EXTENDS,下拉选项,标准查询

  典型场景：
  - 「把 001 账套里 OBJECTGUID=ABC-123 这个表单用到的表加到知识库」
  - 「把销售订单表单的表结构同步到 KB」
  - 「补全 A3 数据迁移 KB 里缺的字段定义」

  触发关键词：「加到知识库」「加入知识库」「同步到 KB」「提取表结构到知识库」
  「表单 XXX 的表加到 KB」

  ⚠️ 高危流程（requires_confirmation=true）：执行 SQL + 写入知识库两个动作
  都会弹窗让陛下确认。
parameters: account_code=账套编码; form_guid=表单内码(OBJECTGUID)
---

# 从表单提取表结构加入知识库

## 步骤 0：确认输入参数

陛下必须提供以下两个参数：

- `account_code` — 账套编码（例如 `001`、`A3DEMO`）
- `form_guid` — A3 表单内码 OBJECTGUID（在 A3 客户端里打开表单 → 「表单 GUID」属性可看到）

如果陛下没说或者只说了一半，先问清楚再继续。**不要猜测**。

## 步骤 1：校验账套与表单存在

调用以下工具校验：

```
get_account_detail(account_code=<account_code>)
```

如果返回 `account not found` 或账套不可用 → 停止，提示陛下先去主界面确认账套存在。

## 步骤 2：执行 S_CONTROL 查询

调 `execute_sql`，参数：

```json
{
  "account_code": "<account_code>",
  "query": "SELECT CAPTION, VIEWNAME, DATANAME, CONTROLTYPE, EXTENDS FROM S_CONTROL WHERE OBJECTGUID='<form_guid>' AND ISNULL(VIEWNAME,'')<>'' AND ISNULL(DATANAME,'')<>''",
  "limit": 5000
}
```

执行后得到一组 `(CAPTION, VIEWNAME, DATANAME, CONTROLTYPE, EXTENDS)` 五元组。

⚠️ 注意：

- `limit=5000` 是兜底，正常表单控制几百条以内
- 如果返回 `0 行` → 提示陛下「这个表单内码下没有引用任何视图（或者视图名为空），无法提取」
- **`CONTROLTYPE` 和 `EXTENDS` 在步骤 5 用来提取 A3Combo 下拉选项 / A3Text 标准查询代码** —— 不要在 SELECT 里漏掉

## 步骤 3：解析结果，按 VIEWNAME 分组

把上一步的结果按 `VIEWNAME` 分组：

```
TABLE_001 = [ (CAPTION_1, DATANAME_1, CONTROLTYPE_1, EXTENDS_1), (CAPTION_2, DATANAME_2, ...), ... ]
TABLE_002 = [ ... ]
...
```

用 `Distinct()` 去重（同一个 VIEWNAME+CAPTION+DATANAME 三元组保留一份即可；CONTROLTYPE/EXTENDS 取第一非空值）。汇总出：

- `tables` = 所有不重复的 VIEWNAME 列表（按字母排序）
- 每张表对应一组「(列名 DATANAME, 列说明 CAPTION)」

## 步骤 4：用 get_table_schema 过滤"关联字段"

⚠️ **这一步是关键** — S_CONTROL 里 DATANAME 不一定是表里真实存在的列。
A3 系统会把「关联字段」也写在 S_CONTROL 里（比如通过 Lookup / 引用关系拉进来的
虚拟字段），这些字段在 `INFORMATION_SCHEMA.COLUMNS` 或 `get_table_schema`
返回里**找不到**。本 Skill 自动过滤掉。

对 `tables` 里的每张表，调用：

```
get_table_schema(account_code=<account_code>, table_name=<VIEWNAME>)
```

返回的 JSON 里 `columns` 数组 → 提取出所有真实的列名，记为 `real_columns[table]`。

然后对 S_CONTROL 抓到的每条 `(CAPTION, VIEWNAME, DATANAME, ...)`：

```
if DATANAME ∈ real_columns[VIEWNAME]:
    保留 (CAPTION, DATANAME) 作为真实列
else:
    丢弃（这是关联字段，不是表里实际有的列）
```

记录过滤统计：

- 抓到 X 条 S_CONTROL 记录
- Y 条因 DATANAME 不在表结构里被丢弃
- Z 条保留

**关联字段虽然被过滤掉，但它们的 CONTROLTYPE/EXTENDS 也跟着丢了** —— 步骤 5 只处理真实列里的特殊控件。这是有意设计：关联字段不进 KB，控件元数据也不进。

把统计结果在步骤 8 一并报告给陛下。

## 步骤 5：处理特殊控件（A3Combo 下拉 + A3Text 标准查询）

⚠️ **2026-10-15 陛下新增**：除了表/列结构，还要从真实列里提取两类控件元数据。
这些信息是后续 AI 生成查询 / 迁移 / 数据补全脚本的关键参考（例如"价格类型"列存
的是 0/1/2/3，AI 就能直接生成对应的中文枚举，不用反查 view）。

### 5.1 EXTENDS 字符串解析

EXTENDS 格式：
- 多个属性用 `|!` 拼接
- 每个属性 `KEY|@VALUE`
- 示例（`A3Combo`）：

  ```
  TabIndex|@618|!AutoTab|@True|!IsAutoCenter|@False|!ComboString|@[0,全部更新][1,默认采购价][2,销售七级价限价][3,电商零售]|!DisplayPath|@Caption|!ValuePath|@Value|!ComboViewName|@
  ```

- 示例（`A3Text` 含标准查询）：

  ```
  ...|!DataSelectCode|@SCM_ITEMSELECT|!IsSpaceSelect|@True|!IsMultiSelect|@False|!SelectOther|@[GUID,ITEMGUID]...|...
  ```

通用解析函数（任何语言都能照搬）：

```python
def parse_extends(extends_str):
    """把 EXTENDS 字符串解析为 {KEY: VALUE} 字典"""
    if not extends_str:
        return {}
    result = {}
    for attr in extends_str.split('|!'):
        if '|@' in attr:
            key, _, value = attr.partition('|@')
            result[key] = value
    return result
```

### 5.2 A3Combo：提取下拉选项

筛选条件：`CONTROLTYPE = 'A3Combo'`

从 `parse_extends(EXTENDS)` 字典里取 `ComboString` 字段：
- 格式：`[value,label][value,label]...`（每对是中括号包起来 value 和 label，逗号分隔）
- 示例：`[0,全部更新][1,默认采购价][2,销售七级价限价][3,电商零售]`

解析函数：

```python
import re
def parse_combo_string(combo_str):
    """把 ComboString 解析为 [(value, label), ...] 列表"""
    if not combo_str:
        return []
    pattern = re.compile(r'\[([^,\]]+),([^\]]*)\]')
    return [(m.group(1), m.group(2)) for m in pattern.finditer(combo_str)]
```

同时记录 EXTENDS 里以下相关字段（如果有）：
- `DisplayPath`（默认 `Caption`）— 显示字段名
- `ValuePath`（默认 `Value`）— 值字段名
- `ComboViewName`（可空）— 关联视图名（**空表示是硬编码选项**）

存入结构（按 VIEWNAME + DATANAME 索引）：

```
combo_options['S_SCM_OLPOLYCOSTDETAIL']['PRICETYPE'] = {
    'options': [('0', '全部更新'), ('1', '默认采购价'), ('2', '销售七级价限价'), ('3', '电商零售')],
    'display': 'Caption',
    'value':   'Value',
    'view':    ''   # 或 'S_XXX_VIEW' (关联视图)
}
```

### 5.3 A3Text：检测标准查询

筛选条件：`CONTROLTYPE = 'A3Text'`

从 `parse_extends(EXTENDS)` 字典里取 `DataSelectCode` 字段：
- **不为空** → 这是一个「标准查询」文本框，记录 `DataSelectCode` 作为查询代码
- 为空 → 是普通文本框，不记录

存入结构（按 VIEWNAME + DATANAME 索引）：

```
standard_query['S_SCM_OLPOLYCOSTDETAIL']['ITEMGUID'] = 'SCM_ITEMSELECT'
```

> 💡 **同源参考字段**（本次不强制提取，陛下后续可能用到）：
> `SelectOther`（字段映射，例如 `[GUID,ITEMGUID][NAME,TASKITEMNAME]`）、
> `IsMultiSelect`（多选）、`IsSpaceSelect`（空格触发）、`QuoteColumn`（引号列）、
> `IsManualInput`（手工输入）。需要时同样用 `parse_extends` 拿。

### 5.4 解析失败保护（容错优先）

- `parse_extends` 永远不抛异常（`if not extends_str: return {}`）
- `parse_combo_string` 用正则，找不到匹配返空列表
- 任何 EXTENDS 解析失败 / ComboString 格式异常的情况都**静默跳过**对应记录
  —— 主流程（表/列结构提取）必须不受影响

## 步骤 6：选择/创建目标知识库

调 `list_knowledge_bases` 拿到所有 KB 列表，然后调 `recommend_knowledge_target`：

```
recommend_knowledge_target(
  content_preview="表名 TABLE_001 ... 列说明 ...",
  hint="A3 表单 <form_guid> 表结构"
)
```

AI 根据推荐结果决定：

- **推荐分数高且匹配业务** → 用这个 KB
- **都不合适** → 调 `create_knowledge_base`：

  ```json
  {
    "name": "<账套编码>_<表单简称>_表结构",
    "description": "从账套 <account_code> 表单 <form_guid> 自动提取的表结构说明"
  }
  ```

## 步骤 7：写入知识库（每张表 = 一条 entry）

对过滤后的每张表，构造一条 `KnowledgeEntry`：

- `title` = `<VIEWNAME>`（不带前缀，直接表名）
- `content` = 表格 Markdown：

  ```markdown
  # 表名：<VIEWNAME>

  来源：A3 账套 <account_code>，表单 OBJECTGUID = <form_guid>
  提取时间：<YYYY-MM-DD HH:MM>

  ## 列说明

  | DATANAME | CAPTION    |
  | -------- | ---------- |
  | <列名 1> | <列说明 1> |
  | <列名 2> | <列说明 2> |
  | ...      | ...        |
  ```

  **如果该表存在 A3Combo / A3Text 控件，追加 `## 控件说明` 段**（步骤 5 提取）：

  ```markdown
  ## 控件说明

  ### PRICETYPE（A3Combo 下拉）
  - 显示字段: Caption
  - 值字段: Value
  - 关联视图: （空，硬编码）
  - 选项:
    | 值 | 显示文本     |
    | -- | ------------ |
    | 0  | 全部更新     |
    | 1  | 默认采购价   |
    | 2  | 销售七级价限价 |
    | 3  | 电商零售     |

  ### ITEMGUID（A3Text 标准查询）
  - 查询代码: SCM_ITEMSELECT
  ```

- `source_type` = `A3FormExtract`（如果 schema 支持，否则用 `Manual`）
- `tags` = `["A3", "<account_code>", "<form_guid>", "表结构"]`

调 `add_knowledge_entry`：

```json
{
  "base_id": "<chosen_base_id>",
  "title": "<VIEWNAME>",
  "content": "...",
  "source_type": "A3FormExtract",
  "tags": ["A3", "<account_code>", "<form_guid>", "表结构"]
}
```

每张表调用一次 `add_knowledge_entry`，一次一张表，**不要**把所有表拼成一条 entry。

## 步骤 8：报告

执行完后给陛下完整总结：

```
═══════════════════════════════════════
✅ 表结构提取完成
═══════════════════════════════════════
账套：<account_code>
表单 OBJECTGUID：<form_guid>
目标知识库：<base_id> (<base_name>)

📊 统计：
- S_CONTROL 原始记录：<X> 条
- 关联字段已过滤：<Y> 条
- 真实列保留：<Z> 条
- A3Combo 控件：<N> 个（下拉选项已提取）
- A3Text 标准查询：<M> 个（已记录查询代码）

📋 已加入知识库的表：
  ✓ TABLE_001  (<列数> 列, <N1> 个 A3Combo, <M1> 个 A3Text)
  ✓ TABLE_002  (<列数> 列)
  ...

⚠️ 跳过的 VIEWNAME：
  - <TABLE_003>：<原因，比如表在数据库中不存在 / get_table_schema 失败>

═══════════════════════════════════════
```

## 注意事项

- **DATANAME 一定要在 get_table_schema 里验证** — A3 的 S_CONTROL 是"声明式"的，
  实际表里没建这一列的就不要进知识库（关联字段）。
- **每张表 = 一条 entry** — 不要把所有表拼成一条大 entry，否则后续检索和分页都不方便。
- **同名表复用已有 entry** — 如果 KB 里已经有 `<VIEWNAME>` 这条 entry，调
  `recommend_knowledge_target` 应该能看到；如果陛下选择"覆盖"，用 `update_knowledge_entry` 而不是 `add_knowledge_entry`。
- **limit=5000 是兜底** — 正常表单不应该超过 1000 条 S_CONTROL 记录，
  如果超过，先停下让陛下确认表单内码是不是对的。
- **失败不要静默** — get_table_schema 失败的表，要单独列在报告里，不要硬加。
- **EXTENDS 解析要鲁棒** — `parse_extends` / `parse_combo_string` 必须不抛异常，
  解析失败要静默跳过（容错优先），让表结构主体提取不受影响。
- **ComboString vs ComboViewName 区分** — `ComboString` 是硬编码选项，
  `ComboViewName` 关联视图选项。两者可同时存在。存 KB 时只暴露 `ComboViewName`
  **名字**，**不要把视图 SQL 一起塞进 KB**（视图是 A3 内部实现细节，跨版本会变）。
- **关联字段的控件元数据丢弃** — 步骤 4 过滤掉的关联字段，其 CONTROLTYPE/EXTENDS
  也跟着丢弃（不存 KB）。这避免给 AI 喂"幻觉控件"。

## 异常分支

| 情况                          | 处理                                                           |
| ----------------------------- | -------------------------------------------------------------- |
| 账套找不到                    | 提示陛下在主界面确认账套存在                                   |
| S_CONTROL 返回 0 行           | 提示陛下检查 OBJECTGUID 是否正确（也可能表单里没引用任何视图） |
| get_table_schema 失败         | 跳过该表，列在"跳过的 VIEWNAME"里，不硬加                      |
| 关联字段过滤后某张表 0 列     | 跳过该表，提示「这张表全部是关联字段，没真实列」               |
| 推荐不到合适 KB               | 提示陛下指定一个 KB，或者建一个新的（requires_confirmation）   |
| A3Combo 但 ComboString 空     | 正常写入控件说明，选项列表为空（提示"未配置下拉选项"）        |
| A3Text 但 DataSelectCode 空   | 不算标准查询，不写控件说明段                                   |
| EXTENDS 整体解析异常          | 静默跳过该条 S_CONTROL 记录，主流程继续                        |
