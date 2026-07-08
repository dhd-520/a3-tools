# 2026-07-06 上下文推断终极修复 + 27/27 全过

## 问题
陛下反馈"EXEC 空格后再输入没有存储过程提示"和"SELECT * FROM 后不输表名.也没列名提示"。

## 根因
1. **EXEC 上下文**：原 `IsAfterExecKeyword` 依赖 `GetCurrentWord`，EXEC 空格后 word="" → 关闭 popup
2. **输完表名/别名后**：`SELECT * FROM T1` caret 末尾时算法命中 FROM → 返 AfterObjectKeyword，但实际用户想弹 T1 的列
3. **数字切断 word**：`T1` 中 `1` 是数字，原算法在 firstChar 检查时跳过数字 → word 被切成 `T` 和 `1`

## 修复
**文件**：`A3Tools.Plugins.Default/Forms/SqlIntelliSenseProvider.cs`

### 1. 新增 `lastNonKwAtCaret` 标志
扫到非关键字 word 时，如果 caret 紧接 word 末尾（word 后是空白/逗号/文档末尾）→ 标记 `lastNonKwAtCaret = true`。

### 2. FROM 命中分支
```csharp
if (word.Equals("FROM", ...) || word.Equals("JOIN", ...) || ...)
{
    if (lastNonKwAtCaret && !sawSelectLike)
        return SqlContextKind.AfterColumnKeyword;  // 输完表名/别名后弹列
    // ... 其他分支
}
```

### 3. 数字不跳过
`firstChar == char.IsDigit` 改为不跳过，让 wordStart 倒推时能合并 T1 → "T1"。

## 测试覆盖
**TestContext 项目**：`D:\work\A3Tools\TestContext/Program.cs`
**27 个用例全过**：
- EXEC 上下文：6 个（EXEC 空格、EXEC 完整过程、EXECUTE 等）
- SELECT 上下文：6 个（SELECT、SELECT *、SELECT * FROM T1 a WHERE 等）
- FROM 上下文：3 个（FROM、FROM + space 等）
- 输完表名/别名：4 个（T1 末、a 末、逗号+T2、T1 a, T2 b）
- 边界：5 个（empty、SELECT 1+、WHERE x=1、SELECT 1;、JOIN/UPDATE bare）
- 杂项：3 个（top 10、after a.、schema 名）

## 顺便修的 Bug
- **编译错误 line 425**：`IsSelectLike` 表达式体多了一个 `}`，csc 在中文环境下会报行号错位的 CS1022。删掉多余 `}` 解决。
- **测试用例 caret 越界**：3 个用例 caret > fullSql.Length，被 line 287 早返 Generic。修正 caret 为 length。
