---
name: upgrade_account
title: 升级账户（基于标准账套 8088）
category: 账套升级
requires_confirmation: true
description: |
  以标准账套（默认 8088）的结构为依据，升级目标账套的表、视图、字段，让目标账套达到与标准账套一致的结构。
  适用场景：陛下想让某个新账套（或老账套）跟标准账套结构对齐。
parameters: source_code=标准账套编码（默认 8088）; target_code=要升级的目标账套编码; target_tables=重点补字段的表（多个用英文逗号分隔，默认 S_SCM_USER,S_SCM_CUSTOM,S_SCM_ITEMNEW,S_SCM_MEMBER,S_SCM_STOCK）
---

# 升级账户（基于标准账套 8088）

## 概述
以 `source_code`（默认 8088）的结构为依据，把 `target_code` 的表、视图、字段补齐到与标准账套一致。

## 必需参数
- **source_code**：标准账套编码（默认 `8088`）
- **target_code**：要升级的目标账套编码

## 可选参数
- **target_tables**：Phase 4 要重点补字段的表（多个用英文逗号分隔，默认 `S_SCM_USER,S_SCM_CUSTOM,S_SCM_ITEMNEW,S_SCM_MEMBER,S_SCM_STOCK`）

## 执行步骤

### Phase 1：列缺失表
1. 对 `source_code` 调 `list_tables`，拿到标准表清单 `src_tables`
2. 对 `target_code` 调 `list_tables`，拿到目标表清单 `tgt_tables`
3. 找出 `tgt_tables` 中没有、但 `src_tables` 中有的表 → `missing_tables`
4. 陛下汇报：缺失 N 张表，列出来

### Phase 2：创建缺失表
对 `missing_tables` 中每个表 `T`：
1. 调 `get_table_schema(code=source_code, table_name=T)` 拿到列定义
2. AI 根据列定义拼出 `CREATE TABLE [T] (...)` 语句（包含主键、默认值、NOT NULL）
3. 调 `execute_ddl(code=target_code, sql=..., dry_run=true)` 先给陛下预览
4. 陛下确认后，调 `execute_ddl(code=target_code, sql=...)` 真正执行
5. 报错处理：若表已存在但结构不同，先提示陛下手工 DROP 再 CREATE

### Phase 3：列缺失视图
1. 调 `list_tables` 时注意返回的 `type` 字段（`BASE TABLE` 或 `VIEW`），筛出 VIEW
2. 对 source 和 target 分别筛 VIEW 清单，对比找出缺失视图
3. 对每个缺失视图 `V`：
   - 调 `get_view_definition(code=source_code, view_name=V)` 拿 CREATE VIEW
   - 调 `execute_ddl(code=target_code, sql=...)` 在目标库创建（同样走 dry_run → 真正执行）

### Phase 4：补关键表的缺失字段
1. 把 `target_tables` 拆成单表列表
2. 对每张表 `T`：
   - 调 `get_table_schema(code=source_code, table_name=T)` → `src_cols`
   - 调 `get_table_schema(code=target_code, table_name=T)` → `tgt_cols`
   - 对比找出 target 缺失的列（`src_cols` 有但 `tgt_cols` 没有）
3. 对每个缺失列 `C`：
   - AI 拼 `ALTER TABLE [T] ADD [C] <TYPE> [NULL/NOT NULL] [DEFAULT ...]`
   - 调 `execute_ddl(code=target_code, sql=...)` 执行（同样走 dry_run → 真正执行）

### Phase 5：汇报
1. 汇总：创建了 N 张表、N 个视图、补了 M 个字段
2. 列出每个变更（含 SQL 摘要），让陛下复核
3. 若有失败，列出来让陛下手工处理

## 注意事项

- ⚠️ **升级前必须备份 target 账套**（陛下可在 UI 工具里手动备份）
- ⚠️ 所有 DDL 操作都会弹窗让陛下二次确认（这是 `execute_ddl` 工具的安全机制）
- ⚠️ 建议先在测试账套验证完整流程
- ⚠️ 若 target 表已存在但结构不同，`execute_ddl` 会失败（CREATE TABLE 冲突）；需要先手工 DROP 再 CREATE
- ⚠️ Phase 4 的 `compare_table_schemas` 不适用此场景（它是两不同表对比），要用 `get_table_schema` 两次对比同表
- 💡 若 source/target 字段类型不一致但列都在，应跳过 ALTER（避免误改已存在列）

## 陛下说"升级账套"时的标准回复模板
1. 确认参数：source_code 默认 8088，target_code 是？
2. 调 `list_skills` 找 `upgrade_account`，调 `load_skill` 读全文
3. 按 Phase 1-5 执行
4. 每个 DDL 都先 `dry_run=true` 给陛下看，确认后再真正执行