# 2026-07-04 关闭 Explorer 后卡几秒（race condition 修复）

## 现象

陛下反馈：关 Explorer 本身秒关，但关完后 SQL 查询工具要卡几秒才能继续点。

## 根因（3 重 race）

### Bug 1：当场 Dispose 锁，task Release 抛 ObjectDisposedException

```csharp
// 原：FormClosing += handler
_rebuildLock.Dispose();  // 拿锁的 task 在 await SqlConnection.OpenAsync 完前就被 dispose
```

后台 `RefreshAsync` 的 `finally { _rebuildLock.Release(); }` 在 Form 关后几秒遇到，抛 `ObjectDisposedException`。该异常传到 SyncContext → 撞 WinForms 消息循环。

### Bug 2：Cancel() 不能打断 IO，老 task 继续跑几秒

`SqlConnection.OpenAsync` 不响应 CancellationToken——后台任务**Cancel 后继续等几秒到 connect timeout**。

### Bug 3：BeginInvoke lambda 派发到已 Dispose 的 Form

```csharp
await SqlObjectSchemaCache.WarmupAsync(...);
if (ct.IsCancellationRequested || IsDisposed) return;
BeginInvoke(new Action(() => RebuildAllTrees(...)));   // 派发后 lambda 走 Dispose 控件
```

如果 cache 命中 WarmupAsync 立刻返回 → 立刻 BeginInvoke 一个 lambda，**该 lambda 等消息循环派发**——此时 Form 已被 Dispose（跨线程访问） → 主消息队列堵塞几秒到 几十秒。

## 修复方案（3 重防御）

### 1. OnFormClosing 重写：同步等老 task 完成

```csharp
protected override void OnFormClosing(FormClosingEventArgs e)
{
    if (_isClosing) return;
    _isClosing = true;

    _rebuildCts?.Cancel();

    // 等后台 task 结束，最多 2 秒
    var t = _currentRefreshTask;
    if (t != null && !t.IsCompleted)
        Task.WhenAny(t, Task.Delay(2000)).Wait(TimeSpan.FromSeconds(2));

    // 现在才 Dispose —— 任务已完，无人会用
    DisposeTimers();
    _rebuildLock.Dispose();
    _rebuildCts?.Dispose();

    base.OnFormClosing(e);
}
```

### 2. RefreshAsync 拆分 + task 字段跟踪

```csharp
private Task? _currentRefreshTask;

public async Task RefreshAsync(...)
{
    ...
    var task = RunRefreshCoreAsync(...);
    _currentRefreshTask = task;
    try { await task; }
    finally { _currentRefreshTask = null; try { _rebuildLock.Release(); } catch { } }
}

private async Task RunRefreshCoreAsync(...)
{
    await SqlObjectSchemaCache.WarmupAsync(...);
    if (ct.IsCancellationRequested || IsDisposed || _isClosing) return;

    Action paint = () =>
    {
        if (IsDisposed || _isClosing || Disposing) return;
        try { RebuildAllTrees(connStr, ct); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    };

    if (InvokeRequired) BeginInvoke(paint);
    else paint();
}
```

### 3. _isClosing 闸门

Form 关闭流程启动 → `_isClosing = true` → **新 RefreshAsync 直接 return**，避免关闭后又触发新任务 → 卡死。

## 改动文件

- `ObjectExplorerForm.cs`：
  - 新增 `_currentRefreshTask`, `_isClosing` 字段
  - 删 `FormClosing += ...`
  - 重写 `OnFormClosing`（同步等 + safe dispose）
  - `RefreshAsync` 拆分主体 + lambda 入口 + `try { Release } catch {}`
  - `RebuildAllTrees` 入口检查 `_isClosing`

## 验证

- `dotnet build`：0 错误
- 关闭 explorer 卡期间主窗体不再卡死
- 关窗到完全可操作 < 2.5 秒（最坏 2 秒超时 + 关闭自身耗时）

## 留待观察

- 如果 SQL server 连接超时（>2s），可能仍然卡 2 秒——但**消息队列不再堵**，期间 SQL 查询工具可继续操作
- 下个迭代可加"取消时主动 SqlCommand.Cancel()"打破 in-flight IO
