## A3Tools v2.6.13 (2026-10-29)

自定义工具新增 **二级关联表 / 二级关联字段**（主表 → 一级关联表 → 二级关联表）。

### 🆕 二级关联

复制链路从两级扩展到**三级**：

```
主表 S_PRINT
  └─ 一级关联表 S_PRINTSCHEME / S_PRINTSCHEME2 / S_PRINTSCHEME3   (关联字段 GUID;GUID2;GUID3)
       └─ 二级关联表（按组挂在对应的一级表下）
```

**配置写法**（分号 = 组边界，逗号 = 组内多个表）：

| 配置项 | 示例 | 说明 |
|---|---|---|
| 二级关联表 | `S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3` | 第 1 组 → 第 1 个一级关联表，第 2 组 → 第 2 个 |
| 二级关联字段 | `GUID,GUID2;GUID3` | 分组规则同上，值为**二级表里指向一级表主键的外键列** |
| 二级关联父键 | `GUID;GUID2;GUID3` | **可选**：一级表的主键列名，留空 = 自动探测 |

**规则**
- 二级留空 = 不复制二级表（旧配置完全兼容，**无需迁移**）
- 分组总数必须 = 一级关联表数量（按顺序一一对应）
- **组内关联字段规则与一级关联字段完全对称**：
  - 只填 **1** 个 → 该组**所有表共用**这一个字段（如 `S_PRINTSCHEMEDATA3,S_PRINTSCHEMEDATA4` 都用 `GUID3`）
  - 填**多个** → 必须与该组表数一致，一一对应
  - 其他数量 → 明确报错
- 二级表的父值 = 对应一级关联表那一行的**主键值**
  - 自动探测顺序：数据库定义的 `PRIMARY KEY` → `GUID` → `OBJECTGUID` → `CODE`
  - 探测不到时可用「二级关联父键」手动指定

### 📋 陛下给的实际配置（已按此实现并验证）

| 配置项 | 值 |
|---|---|
| 主表 | `S_PRINT` |
| 一级关联表 | `S_PRINTSCHEME;S_PRINTSCHEME2;S_PRINTSCHEME3` |
| 一级关联字段 | `GUID;GUID2;GUID3` |
| 二级关联表 | `S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3,S_PRINTSCHEMEDATA4;S_PRINTSCHEMEDATA5` |
| 二级关联字段 | `GUID,GUID2;GUID3;GUID5` |

解析结果：
- 第 1 组（→ `S_PRINTSCHEME`）：`S_PRINTSCHEMEDATA`=`GUID`、`S_PRINTSCHEMEDATA2`=`GUID2`（一一对应）
- 第 2 组（→ `S_PRINTSCHEME2`）：`S_PRINTSCHEMEDATA3`=`GUID3`、`S_PRINTSCHEMEDATA4`=`GUID3`（**共用一个字段**）
- 第 3 组（→ `S_PRINTSCHEME3`）：`S_PRINTSCHEMEDATA5`=`GUID5`

### 🐛 顺手修的配置窗体布局问题

改这个窗体时实测发现两个隐藏坑：
- 窗体**没设字体**（继承 Microsoft YaHei UI 9pt），但 `AutoScaleDimensions` 写成了 10pt 的 `(14,30)` → 窗体被缩放 ~7%（设计 1040×830 实际渲染成 936×775）→ 已显式设字体并对齐基准
- `AutoScroll` 挂在窗体上时，`Dock=Bottom` 的按钮会贴到「内容」底部，**被滚走看不见** → 改成内层面板负责滚动、按钮单独钉在窗体底部

### 🧪 验证
- 二级关联解析器单测 **76 项全绿**（新增 H 段 30 项，含**陛下原始配置实测**）
- 字段配置单测 **119 项全绿**（新增 K 段 14 项配置窗体布局回归）
- 账套列宽 **59/0** · SQL 回归 **89/0**

### ⬆️ 升级提示
- 直接替换程序文件即可，**不用动 `DATA` 目录**（旧配置不填二级 = 行为不变）
