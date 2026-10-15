---
name: find_form_location
title: 查找表单/菜单位置
category: 表单辅助
description: |
  「菜单在哪」「菜单位置」「表单在哪」「表单归属」「表单路径」
  「XX菜单在哪」「XX菜单对应哪个表单」「XX表单在哪里」
  「查XX菜单」「菜单路径」等场景**必须**触发本 Skill。
  通过 S_MENU.FULLNAME 查前端菜单路径，通过 S_OBJECT/S_SUBSYSTEM/
  S_BUSINESSTYPE/S_OBJECTGROUP 查后端表单归属。
  S_MENU 找不到时降级到 S_OBJECT.NAME/CODE 直接搜索。
requires_confirmation: false
write_required: false
---

# find_form_location — 查找表单/菜单位置

## 触发条件
当用户说"找表单位置"、"查表单在哪"、"表单路径"、"菜单位置"、"查找XX表单"、"XX表单在哪里"、"查XX菜单"、"XX菜单对应哪个表单"、"XX菜单在哪"、"菜单路径"、"表单归属"时触发本 Skill。


## 前置条件
- 数据库连接正常（execute_sql 可用）
- 用户提供表单/菜单名称或 GUID

## 业务场景
用户想知道：
1. **前端菜单路径** — 在哪个菜单下（如 `供应链/商城管理/商城基础设置/商城产品`）
2. **后端表单归属** — 属于哪个解决方案、业务分组、表单元数据
3. **前后端对应关系** — 哪个菜单指向哪个表单

## 三层位置结构

```
解决方案层（S_BUSINESSTYPE）
  └── 业务分组层（S_SUBSYSTEM）
        └── 表单元数据层（S_OBJECTGROUP，三级树）
              └── 表单层（S_OBJECT）
                    └── 控件层（S_CONTROL）

前端菜单层（S_MENU）
  └── 引用 S_OBJECT.GUID（OBJECTGUID 字段）
```

## 执行流程

### 第 1 步：在 S_MENU 查找前端菜单

先按用户输入模糊搜索菜单：

```sql
SELECT m.GUID MenuGuid,
       m.NAME MenuName,
       m.FULLNAME MenuPath,
       m.ISVISIBLE,
       m.OBJECTGUID FormGuid
FROM S_MENU m
WHERE m.NAME LIKE '%{用户输入}%'
ORDER BY 
  CASE WHEN m.NAME = '{用户输入}' THEN 0
       WHEN m.NAME LIKE '{用户输入}%' THEN 1
       ELSE 2 END,
  m.NAME
```

**判断结果**：

#### 情况 A：找到了菜单（1个或多个）

如果只有一个匹配，直接用；如果多个匹配，列出让用户选择。

通过 `OBJECTGUID` 字段拿到表单 GUID，执行：

```sql
SELECT A.GUID OBJECTGUID,
       A.CODE 代码,
       A.NAME 名称,
       F.CODE 解决方案代码,
       F.NAME 解决方案,
       A.SUBSYSTEMGUID 业务分组代码,
       B.NAME 业务分组,
       A.OBJECTGROUPGUID 业务分组代码,
       C.NAME 业务分组一,
       D.NAME 业务分组二,
       E.NAME 业务分组三
FROM S_OBJECT A
LEFT JOIN S_SUBSYSTEM B ON A.SUBSYSTEMGUID = B.GUID
LEFT JOIN S_BUSINESSTYPE F ON B.BUSINESSTYPEGUID = F.GUID
LEFT JOIN S_OBJECTGROUP C ON A.OBJECTGROUPGUID = C.GUID
LEFT JOIN S_OBJECTGROUP D ON C.PARENTGUID = D.GUID
LEFT JOIN S_OBJECTGROUP E ON D.PARENTGUID = E.GUID
WHERE A.GUID='{OBJECTGUID}'
```

#### 情况 B：没找到菜单，降级到 S_OBJECT

```sql
SELECT A.GUID OBJECTGUID,
       A.CODE 代码,
       A.NAME 名称,
       F.CODE 解决方案代码,
       F.NAME 解决方案,
       A.SUBSYSTEMGUID 业务分组代码,
       B.NAME 业务分组,
       A.OBJECTGROUPGUID 业务分组代码,
       C.NAME 业务分组一,
       D.NAME 业务分组二,
       E.NAME 业务分组三
FROM S_OBJECT A
LEFT JOIN S_SUBSYSTEM B ON A.SUBSYSTEMGUID = B.GUID
LEFT JOIN S_BUSINESSTYPE F ON B.BUSINESSTYPEGUID = F.GUID
LEFT JOIN S_OBJECTGROUP C ON A.OBJECTGROUPGUID = C.GUID
LEFT JOIN S_OBJECTGROUP D ON C.PARENTGUID = D.GUID
LEFT JOIN S_OBJECTGROUP E ON D.PARENTGUID = E.GUID
WHERE A.NAME LIKE '%{表单关键字}%' OR A.CODE LIKE '%{表单关键字}%'
ORDER BY A.NAME
```

### 第 2 步：反向关联菜单（如果表单找到了）

如果用户给的是表单名，找到表单后，可以再反查哪些菜单引用了它：

```sql
SELECT m.GUID MenuGuid, m.NAME MenuName, m.FULLNAME MenuPath, m.ISVISIBLE
FROM S_MENU m
WHERE m.OBJECTGUID = '{表单GUID}' AND m.ISVISIBLE = '1'
ORDER BY m.NAME
```

## 输出格式

找到结果后，按以下格式输出：

```
📍 找到 {n} 条结果：

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
前端菜单路径：供应链 / 商城管理 / 商城基础设置 / 商城产品
菜单可见性：✅ 显示
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
后端表单：
  GUID：      S_SCM_OLITEM_LIST
  代码：      S_SCM_OLITEM_LIST
  名称：      商城产品列表
  
归属结构：
  解决方案：  SCM（供应链）
  业务分组：  供应链
  业务分组一：基础资料
  业务分组二：（无）
  业务分组三：（无）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

如果没找到，明确告诉用户：
```
❌ 在 S_MENU 和 S_OBJECT 中都未找到 "{用户输入}" 相关记录。
可能原因：
1. 表单尚未创建
2. 名称拼写不同（试试 "%关键词%"）
3. 表单被禁用或删除
```

## 关键字段说明

| 字段 | 来源表 | 含义 |
|------|--------|------|
| FULLNAME | S_MENU | 菜单完整路径（如 `供应链/商城管理/商城基础设置/商城产品`） |
| OBJECTGUID | S_MENU | 菜单引用的表单 GUID（关联 S_OBJECT.GUID） |
| SUBSYSTEMGUID | S_OBJECT | 业务分组代码 |
| OBJECTGROUPGUID | S_OBJECT | 业务分组代码（表单直接分组） |
| S_BUSINESSTYPE | 顶级解决方案（如 SCM、HR、FI） |
| S_SUBSYSTEM | 业务分组（如 供应链、人力资源、财务） |
| S_OBJECTGROUP | 三级业务分组（业务分组一/二/三） |

## 与其他 Skill 的配合

- **check_form_health** — 找到位置后可以做 31 项深度诊断
- **create_form** — 找到位置参考后可以基于现有结构创建新表单
- **add_form_schema_to_kb** — 找到位置后可以把表单结构加入知识库

## 常见陷阱

1. **OBJECTGUID 可能是表单 CODE 不是 GUID**（如 `ITEMLIST`），需要兼容两种情况
2. **同名菜单**：搜索 "产品资料" 可能返回多个结果，按 FULLNAME 排序后让用户选
3. **多菜单共用表单**：MRP控制台 被两个菜单引用，列全部
4. **隐藏菜单**（ISVISIBLE=0）：可能存在但前端不可见，搜索时默认包含（不强制过滤）
5. **S_OBJECTGROUP 三级可能不全**：有的表单只有一级、二级分组，后几级为 NULL
6. **解决方案/业务分组链路**：`S_OBJECT → S_SUBSYSTEM → S_BUSINESSTYPE`，一定要 LEFT JOIN（可能没归属）