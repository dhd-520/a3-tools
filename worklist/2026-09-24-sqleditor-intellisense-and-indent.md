# A3Tools SqlEditor 联想 + 缩进 4 项修复

**日期**: 2026-09-24 15:35
**陛下反馈**: "A3内置查询工具还是有点问题" - 4 个问题
**修复 commit**: (待提交)

---

## 4 个问题

### 1. 联想默认选中第一个 → 按回车习惯性误触
- **现状**: `IntelliSensePopup` 默认 `SelectedIndex=0`,输入完按 Enter 直接用了第一项
- **方案**: `_intelliSense.SelectedIndex = -1` + Enter/Tab 拦截逻辑保持原"无选中即不替换"

### 2. 联想闪烁 → 防抖 800ms
- **现状**: 每次按键(`OnTextChanged`)立刻调 `TriggerIntelliSense`,正则跑+缓存查→闪烁
- **方案**: `_intelliSenseTimer.Interval = 800` + Hide/Esc/焦点丢失 Stop()

### 3. 联想触发条件太宽 — 输完 `S_SCM_SEORDER ` 空格就弹列,应优先表名
- **现状**: `DetectContext` 有"输完表名/别名后默认弹列名"补丁(2026-07-28),把任意非关键字 word 后空格都当列
- **根因**: `SELECT * FROM T1 |` caret 紧接 T1 末尾 → lastNonKwAtCaret → 弹列。`SELECT * FROM S_SCM_SEORDER |` 同样
- **方案 B (陛下拍板)**: 改 `GetSuggestions`:`AfterColumnKeyword` 时,先查 `SqlAliasResolver.Parse(sql, caret)` 看 word 是否真在 aliasMap;**不在 → 降级到 AfterObjectKeyword 弹对象**,不要弹列

### 4. 选中缩进后关键字高亮丢失 + 选中丢失
- **现状**: `IndentMultipleLines` 用 `SelectedText = sb.ToString()` 替换 → `_suppressHighlight` 阻止高亮但**缩进后没手动触发重设色**;选中丢失因为 SelectionStart 改到 firstCi 后续没还原
- **方案**: 缩进前存 `savedSelStart/savedSelLen`,缩进后用 `Highlight(影响行范围)` 重设色 + `SelectionStart=savedSelStart, SelectionLength=savedSelLen` 还原选中

---

## 文件改动

### `A3Tools.Plugins.Default\Forms\SqlEditor.cs`

#### 改动 A: 防抖 800ms
```csharp
// L125-129: _intelliSenseTimer.Interval: 50 → 800
_intelliSenseTimer = new System.Windows.Forms.Timer { Interval = 800 };
_intelliSenseTimer.Tick += (_, _) => { ... TriggerIntelliSense(); };
```

#### 改动 B: IntelliSensePopup 默认无选中
- 找 `_intelliSense.ShowNearCaret(...)` 调用 → 调用前 `_intelliSense.ClearSelection()`(需要 IntelliSensePopup 加这个公开方法)
- 或在 IntelliSensePopup.cs 直接改默认 SelectedIndex=-1

#### 改动 C: HandleTabIndent/HandleShiftTabIndent - 缩进后重设色 + 恢复选中
```csharp
// L1064 IndentMultipleLines 调用处
IndentMultipleLines(lineStart, lineEnd, addIndent: true);
int savedStart = selStart, savedLen = selLen;  // 缩进后新的 selectionStart = lineStart + IndentText.Length * 行数
// 选区计算: 行数 × 4 空格位移 + 文本长度增量
int linesAdded = (lineEnd - lineStart + 1) * IndentText.Length;
SelectionStart = lineStart;
SelectionLength = 0;
Highlight(lineStart, lineEnd);  // 增量高亮
```

#### 改动 D: 缩进后恢复 Selection
```csharp
// 缩进后 SelectionStart 已经在 firstCi + 缩进增量位置
// 算 savedStart 相对位移: 缩进了 4*N 字符 → savedStart += 4*N
int shift = addIndent ? IndentText.Length * (lineEnd - lineStart + 1) : -IndentText.Length * (lineEnd - lineStart + 1);
SelectionStart = savedStart + shift;
SelectionLength = savedLen;
```

### `A3Tools.Plugins.Default\Forms\SqlIntelliSenseProvider.cs`

#### 改动 E: GetSuggestions - 列联想前 aliasMap 验证
```csharp
// L141-148: AfterColumnKeyword 分支
if (ctx == SqlContextKind.AfterColumnKeyword)
{
    // ★ 2026-09-24: 别名/表名验证 (陛下拍板方案 B)
    // 当前光标前最近的 word 必须真在 aliasMap 中才弹列;
    // 否则降级到 AfterObjectKeyword 弹表/视图,避免 SELECT * FROM S_SCM_SEORDER| 后误弹列
    string lastWord = GetLastWordBeforeCaret(fullSql, caretOffset);
    if (!string.IsNullOrEmpty(lastWord) && fullSql != null)
    {
        var aliases = SqlAliasResolver.Parse(fullSql, caretOffset);
        if (!aliases.ContainsKey(lastWord) && !IsKnownObject(lastWord, connectionString))
        {
            // 走对象路径
            ctx = SqlContextKind.AfterObjectKeyword;
        }
    }
    if (ctx == SqlContextKind.AfterColumnKeyword)
    {
        var cols = GetAllColumnsFromAliases(connectionString, fullSql, caretOffset, prefix);
        if (cols != null && cols.Count > 0) return cols;
    }
}
```

### `A3Tools.Plugins.Default\Forms\IntelliSensePopup.cs` (可能不存在,要找)

---

## 测试矩阵

| 场景 | 输入 | 期望 |
|------|------|------|
| 1 | `SELECT * FRO` 按回车 | 弹关键字,回车不自动补 `FROM` |
| 2 | `SELECT * FROM ` 输入 `SE` 停 800ms | 弹表名,不闪 |
| 3 | `SELECT * FROM S_SCM_SEORDER ` | **弹表名联想**(`S_SCM_SEORDER` + 相似表),不弹列 |
| 3b | `SELECT * FROM S_SCM_SEORDER WHERE ` | 弹列(这是真的列上下文) |
| 3c | `SELECT * FROM S_SCM_SEORDER a WHERE a.` | 弹 `a` 别名列 |
| 4 | 选中 5 行按 Tab | 5 行缩进,关键字高亮保持,选中保持 |

---

## 编译验证

- 0 错 0 警告
- 改完跑 MSBuild 看 release 输出
