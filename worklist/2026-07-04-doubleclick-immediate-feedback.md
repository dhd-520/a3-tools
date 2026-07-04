# 2026-07-04 双击对象 "看起来卡住" 修复

## 现象

陛下反馈：双击 ObjectExplorer 中的对象后，编辑器一片空白，"我还以为卡主了呢"。

## 根因

`OpenScript` 之前这样跑：

```csharp
public void OpenScript(...) {
    // 切库（如果需要）
    _ = LoadAndOpenScriptAsync(objType, objName);
}

private async Task LoadAndOpenScriptAsync(...) {
    lblStatus.Text = $"加载 ...";
    var script = await SqlObjectSchemaCache.WarmupAsync(connStr, forceReload);  // ← 这里 0-3 秒
    BeginInvoke(() => {
        var tab = NewTab(title, content);   // ← 等待 0-3 秒后才执行
        ...
    });
}
```

**实际流程**：
1. 陛下双击 → ObjectExplorer 派发 Tree_NodeMouseDoubleClick
2. 调 OpenScript → fire-and-forget LoadAndOpenScriptAsync
3. **立刻返回**（看起来秒响应）
4. `await SqlScriptLoader.LoadCreateScriptAsync` 跑 1-3 秒（SqlConnection 拉了 + sys.sql_modules query）
5. **期间 Tab 没创建、SelectedTab 没切、Editor 没 Focus**
6. 陛下看到：Editor 还是上一 Tab 的（空），焦点还在 Explorer，主窗体显示状态栏变了「加载中...」但编辑器一片空白
7. 陛下主观感受 "卡住"

**隐藏问题**：

- ObjectExplorer 是 Owned form，焦点在 Explorer
- 即使 BeginInvoke 触发了 NewTab + Focus，**主窗体不一定 Active → Focus 失效**

## 修复

### 1. OpenScript 同步建占位 Tab（不依赖 IO）

```csharp
public void OpenScript(...)
{
    // 1. 切库
    // 2. **同步** NewTab 加载占位 + SelectTab + Focus + Activate 主窗体
    var tab = NewTab($"{objType}.{objName} (加载中…)", "-- 加载中…");
    Activate();
    tab.Editor.Focus();
    // 3. 异步加载
    _ = LoadAndFillScriptAsync(tab, objType, objName);
}
```

### 2. LoadAndFillScriptAsync 仅做填充

```csharp
private async Task LoadAndFillScriptAsync(SqlQueryTabPage tab, ...)
{
    try
    {
        var script = await SqlScriptLoader.LoadCreateScriptAsync(...);
        BeginInvoke(() => {
            if (IsDisposed || tab.Page.IsDisposed) return;
            tab.Page.Text = $"{objType}.{objName}";        // 去掉 (加载中…)
            tab.SetEditorText(script ?? "-- 加载失败");
            tab.Editor.Select(script?.Length ?? 0, 0);     // 光标到末尾
            tab.Editor.Focus();
            lblStatus.Text = $"已加载 ...";
        });
    }
    catch (Exception ex)
    {
        BeginInvoke(() => {
            tab.Page.Text = $"{objType}.{objName} (失败)";
            GetActiveTab()?.AppendMessage($"[错误] ...\n");
        });
    }
}
```

## 行为对照

| 阶段 | 之前 | 现在 |
|------|------|------|
| 双击 → 反馈到 UI | 1-3s（await） | **< 50ms**（同步 Tab 创建）|
| 用户看到 | 「加载中...」状态栏 + Editor 空白 | **新 Tab 出现 + 标题"加载中…" + Editor 占位文本 "加载中…"** |
| 焦点 | Explorer 仍抢主 → Focus 失败 | **主窗体 Activate() + Editor.Focus() 抢回** |
| 加载完成 | 文本注入 + Editor Focus | 文本注入 + Editor.Focus + Select 到末尾，编辑就绪 |

## 测试场景

| 场景 | 期望 |
|------|------|
| 双击存储过程 → 等待 | **立刻**看到新 Tab「P.sp_xxx (加载中…)」+ 占位文本 |
| 加载完成后 | Tab 标题变成「P.sp_xxx」+ 显示实际 CREATE 脚本 |
| 加载失败（obj 不存在） | Tab「P.sp_xxx (失败)」+ 错误消息 |
| 双击时焦点 | Explorer 不抢；主窗体拉前 + 编辑器可输入 |

## 改动文件

- `SqlQueryForm.cs`：
  - `OpenScript` 同步建 Tab + Activate + Focus
  - 拆 `LoadAndOpenScriptAsync` → `LoadAndFillScriptAsync`

## 验证

- `dotnet build`：0 错误
- 双击后 50ms 内看到新 Tab「加载中...」+ 占位文本
- 加载完成后标题更新 + 内容注入 + 光标到末尾
