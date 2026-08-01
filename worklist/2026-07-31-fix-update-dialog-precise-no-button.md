# 2026-07-31 A3 客户端更新弹窗 — 自动按「否」(精确定位版)

## 背景

陛下 14:24 / 17:39 / 17:40 三次反馈:

1. **14:24**:选了 launcher「是否继续更新」弹窗的「否」,A3 客户端应该正常启动才对 → 现在「完全没反应」(什么都不启)
2. **17:39**:现在选「是」也卡在更新窗口,回车根本没生效 → 现在的代码发回车没用
3. **17:40**:用上下键控制选否再回车 → 想要我们能切焦点后按「否」

## 三处根本原因(实测定位)

### Bug 1:`TryFindAndClickUpdateDialog` 根本找不到更新弹窗
- 老实现用 `Process.GetProcessesByName + MainWindowTitle` 扫窗体
- 但 A3 客户端的「升级文件检测」是**模态子窗口**(`Form.ShowDialog`),`Process.MainWindowTitle` 对模态子窗通常返回空(它只反映顶级「主窗体」标题)
- 所以 `TryFindAndClickUpdateDialog` **从未触发过**:`TryAutoConfirmUpdateDialog` 的 Debug 日志从来没打出来过
- 调试脚本 `scratch/dump_a3_windows.ps1` 列出 PID=28492 进程所有窗体,确认有 1 个:
  ```
  HWND=2A110A PID=28492 Proc='君则A3' 
  Class='WindowsForms10.Window.8.app.0.1ca0192_r8_ad1' 
  Vis=True Bounds=(2718,591 537x209) 
  Title='升级文件检测'
  ```
  子控件:
  - Button HWND=3D10EA Bounds=(3030,742 56x21) Text='**是**'
  - Button HWND=131244 Bounds=(3108,742 56x21) Text='**否**'
  - Static HWND=231138 Bounds=(2735,666 504x16) Text='系统检测到有需要升级的文件,版本号为0.0.0.V359,是否需要升级?'

### Bug 2:即使找到弹窗,回车触发的是默认按钮「是」
- 老逻辑:`SendMessage(hwnd, WM_KEYDOWN, VK_RETURN)` → 默认按钮(`BS_DEFPUSHBUTTON`)
- A3 弹窗默认按钮是「是」→ 老代码 = 自动升级(即使我们想按否)

### Bug 3:`PrepareUpdateScenarioForLaunch` External 选否 = return false
- 语义错误:launcher 问「是否升级」,选「否」≠ 整个启动流程放弃
- 应该是:launcher 跳过更新,但 A3 客户端/开发工具照常启动
- A3 客户端自己弹的「升级文件检测」由 `TryFindAndClickUpdateDialog` 自动按否

## 修复方案

### Fix 1:用 EnumWindows + EnumChildWindows 精确找「否」按钮
```
EnumWindows:
  过滤条件 PID ∈ A3 进程组 && IsWindowVisible && 
           ClassName.StartsWith("WindowsForms10.Window.")

找到弹窗 HWND 后:
  AllowSetForegroundWindow + AttachThreadInput + 
  SetForegroundWindow + BringWindowToTop  (抢焦点)

EnumChildWindows 找按钮:
  ClassName contains "BUTTON"
  Text 匹配 A3_NO_BUTTON_TEXTS 中任一关键字
  → SendMessage(BM_CLICK) 精确点击
```

### Fix 2:回车退路(找不到「否」按钮时)
```
if (allBtns.Count >= 2):
    keybd_event(VK_TAB); keybd_event(VK_RETURN);
    # 默认焦点「是」→ Tab 一次跳到「否」→ 回车
elif (allBtns.Count == 1):
    keybd_event(VK_RETURN);  # 单按钮,回车带走
```

### Fix 3:PrepareUpdateScenarioForLaunch External 选否
```
if (MessageBox.Show != Yes)
    Debug.WriteLine("External 选「否」→ launcher 跳过,照常启动 A3");
    return true;  // ← 关键:不再 return false,挡下游
```

## 改动文件

| 文件 | 改动 |
|------|------|
| `A3Tools/Forms/MainForm.cs` | 1. P/Invoke 区加 `EnumWindows/EnumChildWindows/GetClassNameW/GetWindowTextW/...`(line 362-415)<br>2. 加 `A3_NO_BUTTON_TEXTS` 静态数组(中文 + 英文 + 带助记符)(line 418)<br>3. `A3_UPDATE_DIALOG_TITLES` 加「升级文件检测」精确关键字(line 426)<br>4. `PrepareUpdateScenarioForLaunch` External 选否 → return true(line 417)<br>5. **重写 `TryFindAndClickUpdateDialog`(line 570-740)**:从「Process+MainWindowTitle」改「EnumWindows+EnumChildWindows+BM_CLICK」+ attach thread input 抢焦点 + 找按钮 + 退路键事件<br>6. `LaunchDevToolsForAccount` 也调 `TryAutoConfirmUpdateDialog`(line 895)<br>7. 删文件底部 line ~3299-3310 重名 P/Invoke,改注释 |

## 关键代码(重写部分)

```csharp
// 收集 A3 进程组 PIDs
var a3Pids = new HashSet<uint>();
foreach (var name in new[] { PROC_CLIENT, PROC_DEVTOOLS })
{
    foreach (var p in Process.GetProcessesByName(name))
        if (p.Id != myPid) a3Pids.Add((uint)p.Id);
}

// EnumWindows 找更新弹窗
EnumWindows((h, l) =>
{
    GetWindowThreadProcessId(h, out pid);
    if (!a3Pids.Contains(pid)) return true;
    if (!IsWindowVisible(h)) return true;
    var cls = GetClassNameW...;
    if (!cls.StartsWith("WindowsForms10.Window")) return true;
    var title = GetWindowTextW...;
    foreach (var t in A3_UPDATE_DIALOG_TITLES)
        if (title.Contains(t, ...)) { foundHwnd = h; ...; return false; }
    return true;
}, IntPtr.Zero);

// 抢焦点
AllowSetForegroundWindow(myPid);
uint targetTid = GetWindowThreadProcessId(foundHwnd, out _);
uint myTid = GetCurrentThreadId();
bool attached = (myTid != targetTid) && AttachThreadInput(myTid, targetTid, true);
try { SetForegroundWindow(foundHwnd); BringWindowToTop(foundHwnd); }
finally { if (attached) AttachThreadInput(myTid, targetTid, false); }

// 找「否」按钮
EnumChildWindows(foundHwnd, (ch, l) =>
{
    var cls = GetClassNameW...;
    if (!cls.Contains("BUTTON")) return true;
    var text = GetWindowTextW...;
    foreach (var key in A3_NO_BUTTON_TEXTS)
    {
        if (text.Contains(key, ...) || text.Replace("(","").Replace(")","").Trim().Contains(key, ...))
        { noBtn = ch; break; }
    }
    return true;
}, IntPtr.Zero);

if (noBtn != IntPtr.Zero)
    SendMessage(noBtn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
```

## 编译

- ✅ 0 CS 错误(原 376 个 CS8632 + 2 个 NU1701 警告不变)
- ⚠️ 文件锁问题:VS (PID 5180) + A3Tools (PID 3964) 锁了 bin/Debug/.pdb/.dll,临时输出到 `scratch/compile_only/` 验证
- ✅ A3Tools.dll 337,920 bytes 重新生成

## 测试步骤

1. 关掉 VS + 关掉跑的 A3Tools(让它们释放文件锁)
2. `dotnet build D:\work\A3Tools\A3Tools.sln -c Debug`(此时输出到正式 bin/Debug)
3. 起 **DebugView**(Sysinternals 微软免费)→ 勾 Capture → 滤 `*AutoConfirm*`
4. 启 0081 账套 → 点启动 → launcher 弹窗选「否」
5. 观察 DebugView 应有:
   ```
   [AutoConfirm] 找到更新弹窗 '升级文件检测' (PID=28492, HWND=0x2A110A, ...)
   [AutoConfirm] 弹窗子按钮 [2 个]: '是', '否'
   [AutoConfirm] 点击「否」按钮 '否' (HWND=0x131244) 成功
   ```
6. A3 客户端启动到登录页 = ✅

## 经验沉淀

**`Process.MainWindowTitle` 不可信**(模态子窗返回空)
- 所有用 `Process.MainWindowTitle` 匹配弹窗标题的代码几乎都是错的
- 改用 `EnumWindows + EnumChildWindows`

**Win32 抢焦点要 `AttachThreadInput`**
- Vista 之后后台进程不能直接 `SetForegroundWindow`
- 必须 `AllowSetForegroundWindow` + attach target thread input + SetForegroundWindow + BringWindowToTop

**`EnumChildWindows` 找按钮比 SendMessage 文本匹配稳**
- `SendMessage(WM_SETTEXT)` 等消息需要发到精确控件 HWND,但找控件本身已能 100% 锁定
- 用 `BM_CLICK` 模拟点击 = 真实用户点击(包括 BN_CLICKED 通知),VB.NET / C# / WPF / WinForms 都生效
- `SendMessage(KEYDOWN/VK_RETURN)` 在焦点不在该按钮时**无效**(本次 bug 根因)

**WinForms 弹窗默认按钮 = 「是」/「确定」**
- Tab 切换焦点顺序固定:确定 → 取消 → 是 → 否 (按 `TabIndex` 与 `AcceptButton` 设置)
- 一般场景「否/取消」是 TabIndex=1,确定是 0;但 A3 这种用法 Tab 顺序则是「是」→「否」,所以**默认焦点回车 = 「是」**

**External 弹窗问「是否升级」选否 ≠ 不启动**
- 语义分层:launcher 自带的更新机制 + A3 客户端启动 = 两个独立关注点
- 正确语义:launcher 跳过更新时,A3 客户端照常启动;A3 客户端自己弹的更新框由 TryFindAndClickUpdateDialog 处理
