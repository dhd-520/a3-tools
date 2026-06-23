# 2026-06-22 启动选项对话框加账套信息 + 托盘启动自动激活

## 需求

1. **启动选项对话框**（`LaunchOptionsDialog`）增加当前账套名称显示
2. **托盘模式启动账套**时自动激活到最前端（之前需要手动点任务栏）

## 实现

### 1. `A3Tools/Forms/LaunchOptionsDialog.cs` 加账套信息横幅

- 构造函数加两个可选参数：`string accountName = ""`, `string accountCode = ""`
- 加公开属性 `AccountName` / `AccountCode`（getter only）
- 加 `lblAccountInfo` Label，放在标题栏下方的 `accountBanner` 面板里
  - 横幅背景：淡蓝 `#e8f4f8`，文字主色 `#1891cb`，加粗 10pt
  - Dock=Top，高度 44px
- `LoadDefaults()` 填充文本：
  - 有账套名+代码：`当前账套：xxx (0001)`
  - 有账套名无代码：`当前账套：xxx`
  - 都没有：`当前账套：（未选择）`
- `Controls.Add` 顺序：`bottom → content → accountBanner → titleBar`（保证 z-order，标题在最上）

### 2. `A3Tools/Forms/MainForm.cs` 传账套名 + 强制激活

**传递账套信息**（`LaunchSelectedAccount`）：
```csharp
using var dialog = new LaunchOptionsDialog(
    settings.LaunchDesktop,
    settings.LaunchDevTools,
    settings.LaunchWeb,
    settings.SelectedBrowser,
    account.Name,    // ← 新增
    account.Code);   // ← 新增
```

**托盘启动先恢复主窗**（`LaunchSelectedAccount` 开头）：
```csharp
if (_isHiddenToTray) ShowFromTray();   // 同步恢复 + 拉前台
```

**Win32 强制激活**（`ForceForegroundWindow` 方法 + DllImport）：
```csharp
private void ForceForegroundWindow(IntPtr hWnd)
{
    try
    {
        // 1. 拿到当前前台窗口的进程 ID
        IntPtr fgHwnd = GetForegroundWindow();
        if (fgHwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(fgHwnd, out uint fgPid);
            // 2. 让那个进程「允许」我们抢焦点（Vista 之后必须这步）
            AllowSetForegroundWindow((int)fgPid);
        }
        // 3. 拉到前台
        SetForegroundWindow(hWnd);
        this.BringToFront();
        this.Activate();
    }
    catch
    {
        // 退化：仅 WinForms 自带 Activate
        this.Activate();
    }
}

[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int dwProcessId);
[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

**ShowFromTray 改用 ForceForegroundWindow**：
```csharp
// this.Activate(); ← 旧：WinForms 自带，Vista+ 受限经常失效
ForceForegroundWindow(this.Handle);   // ← 新：Win32 真·抢焦点
```

## 设计要点

- **AllowSetForegroundWindow 是关键**：Vista 开始微软禁止后台进程强行抢焦点，
  必须先调 `AllowSetForegroundWindow(前台进程ID)` 才能 `SetForegroundWindow` 生效
- **横幅背景用淡蓝**：和 A3 工具箱主色 `#1891cb` 呼应，但不刺眼
- **z-order 顺序**：标题栏最后 Add（在最上面），横幅次之，内容在最下
  - 但 Dock=Top 会自动按 Add 顺序排列，Dock=Fill 的 content 会被压缩到剩余高度
- **不做 Dialog 自己的 ForceActivate**：MainForm 已经前台 → `ShowDialog(this)`
  会让 Dialog 自然 Modal 在前面；不需要在 Dialog 里再调一次 SetForegroundWindow
- **`LaunchSelectedAccount` 开头先 ShowFromTray**：解决快捷键 case 4 不主动恢复的问题
  - 旧逻辑：case 4 只 check `tabControl.SelectedTab == tabLaunch` 就调 LaunchSelectedAccount
  - 如果 MainForm 在托盘里，LaunchSelectedAccount 走到 `dialog.ShowDialog(this)`
    时 this 不可见，对话框虽然能弹但可能显示在别的窗口后面
  - 新逻辑：开头先 ShowFromTray（恢复 + 拉前台），然后正常走对话框流程

## 验证

- `dotnet build D:\work\A3Tools\A3Tools.sln -c Release`：**0 错**，149 警告全是历史 nullable
- 重新打包 `StandaloneSF`：
  - `A3Tools.exe` 77,677,362 bytes（74.07 MB，比上次 +468 字节）
  - 总大小约 76.05 MB
  - Plugins / 3 个 pdb / 无残留

## 待测试（陛下测试验收）

1. 正常主界面点启动 → 启动选项对话框出现，**标题栏下蓝色横幅显示「当前账套：测试账套 (0001)」**
2. 程序隐藏到托盘 → 双击托盘图标 → 主窗**自动拉到最前端**（不需点任务栏）
3. 程序在托盘 → 按启动快捷键（如果设置了） → 主窗自动恢复 + 拉到前台 + 启动选项对话框 Modal 在前
4. 如果账套名/代码为空（比如新增未保存的账套），横幅显示「（未选择）」
5. Win11 多桌面切回后，托盘恢复依然能拉到前台（AllowSetForegroundWindow 兼容跨 desktop）
