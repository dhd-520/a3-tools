using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A3Tools.Services
{
    /// <summary>
    /// ★ 2026-08-13 A3 程序更新配合器（完全独立于 launcher 自己的 UpdateService）。
    ///
    /// 设计目标：launcher 启动 A3 客户端或 A3 开发工具时，如果 A3 自己弹出更新框
    /// （"升级文件检测"），按陛下拍板的 3 种场景处理：
    ///
    /// 场景 1：单启 A3 程序（只客户端或只开发工具）+ 没有其他 A3 进程在跑
    ///         → 默认自动点是（A3 弹框默认按钮=「是」=回车）
    ///         → 等升级完成（A3 弹「升级完成!」「系统提示」）
    ///         → 自动点是（回车）→ A3 重启 → 弹登录框 → 走自动登录
    ///
    /// 场景 2：同时启客户端 + 开发工具 + 没有其他 A3 进程在跑
    ///         → 先杀掉 launcher 自己启的 devtools（释放文件锁）
    ///         → 启动客户端 → 自动点是 → 等升级完成 → 自动点是
    ///         → 客户端起来后，启动开发工具
    ///
    /// 场景 3：已有其他 A3 客户端/开发工具在跑
    ///         → 弹 launcher 自己的确认框（"升级需要关闭所有正在运行的A3进程,是否升级?"）
    ///           ├─ 陛下点是 → 关所有外部 A3 进程 → 走场景 1/2
    ///           └─ 陛下点否 → 抓 A3 升级框"否"按钮 BM_CLICK → A3 弹登录框 → 走自动登录
    ///
    /// 关键约束（2026-08-13 陛下第 N 次强调）：
    ///   - 本类只处理 **A3 程序自身** 的更新（客户端/开发工具本体）
    ///   - **绝对不动** launcher 自己的更新（UpdateService / CheckUpdateOnStartupAsync / UpdateForm）
    ///   - 两套更新机制完全独立,严格用词区分:"A3 程序更新" vs "A3Tools launcher 更新"
    /// </summary>
    public static class A3ProgramUpdateChecker
    {
        #region Win32 API

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint BM_CLICK = 0x00F5;

        #endregion

        #region 窗口关键字（从 MainForm.TryAutoConfirmUpdateDialog 抽出来共用）

        /// <summary>
        /// A3 升级过程中的「是否升级?」窗口标题(Yes/No 对话框)。
        /// ★ 2026-08-13 10:52 扩大关键字:之前只有"升级文件检测"一个,但 A3 不同版本/不同场景可能用别的标题。
        ///   复用 launcher 现有 TryAutoConfirmUpdateDialog 的关键字列表(13 个),避免漏检。
        /// </summary>
        private static readonly string[] UPDATE_DIALOG_TITLES = new[]
        {
            "升级文件检测",      // A3 弹的是/否升级框,陛下 10:11 抓取确认
            "升级", "更新", "发现新版本", "版本更新", "新版本可用",
            "升级提示", "更新提示", "升级确认", "更新确认",
            "系统提示", "提示", "Information", "系统消息"
        };

        /// <summary>A3 升级完成后的「升级完成!」「系统提示」窗口标题。复用 launcher 现有列表。</summary>
        private static readonly string[] UPGRADE_COMPLETE_TITLES = new[]
        {
            "升级完成", "更新完成", "升级成功", "更新成功", "升级完成确认",
            "系统提示", "提示", "Information", "系统消息"
        };

        #endregion

        #region 公开方法

        /// <summary>
        /// 在 launcher 启动 A3 程序之前,**一次性**探一下桌面有没有 A3 程序自己弹的更新框。
        /// 陛下 10:19 明确:不需要轮询等待,有更新框→走场景处理;没找到→直接认定无更新,
        /// 让 A3 程序继续弹登录框(走原自动登录)。
        /// 仅检测,不点任何东西(纯探针)。
        /// 返回 true = 检测到更新框(有更新),false = 没检测到(无更新)。
        /// </summary>
        public static bool DetectA3ProgramUpdate()
        {
            return FindDialogByTitle(UPDATE_DIALOG_TITLES, out _);
        }

        /// <summary>
        /// ★ 2026-08-13 10:20 重写:launcher 启动 A3 客户端或开发工具**之后**调用。
        /// A3 更新弹窗依赖 A3 进程启动,必须等 A3 进程起来才会弹"升级文件检测"框。
        /// 轮询桌面:
        ///   · 找到"升级文件检测"框 → 调 onUpdateDetected(场景 1/2/3 的统一入口)→ 继续轮询等登录框
        ///   · 找到登录框(loginKeyword 命中) → 返 true,A3 程序已可登录,调用方应走自动登录
        ///   · 超时(timeoutMs) → 返 false(走兜底:既不更新也不登录,让调用方自己处理)
        /// </summary>
        /// <param name="loginKeyword">A3 登录框标题关键字,默认"君则A3"</param>
        /// <param name="timeoutMs">轮询总时长,默认 30 秒(给 A3 启动+检测+弹框留时间)</param>
        /// <param name="onUpdateDetected">发现更新框时的回调(参数:找到的更新框 HWND)。回调返 true 表示"已处理,继续等登录框";返 false 表示"放弃等登录框"</param>
        /// <summary>
        /// ★ 2026-08-13 10:41 短轮询 + 立刻返 false(无更新不阻塞)。
        ///
        /// 调用时机:launcher 启 A3 进程**之后**调用。短时间轮询桌面等 A3 弹"升级文件检测"框。
        ///   · 找到升级框 → 调 onUpdateDetected(场景 1/2/3 入口)→ 返回调返回值
        ///   · 找不到(无更新)→ 立刻返 false(不阻塞 launcher,让 launcher 走原自动登录)
        ///
        /// 关键设计:
        ///   · 不轮询登录框(陛下 10:33 反馈:登录框早于升级框弹出,会被误判)
        ///   · timeout 最多 5 秒(A3 启动 + 检测服务端 + 弹升级框几秒足够)
        ///   · 没找到立刻返 false,绝不阻塞 launcher(陛下 10:41 反馈:30s 不能接受)
        /// </summary>
        /// <param name="timeoutMs">最大轮询时长,推荐 5000ms</param>
        /// <param name="onUpdateDetected">发现升级框时的回调。返 true 表示"已处理,继续";返 false 表示"停止轮询"</param>
        public static bool WaitForA3UpdateDialogThenHandle(
            int timeoutMs,
            Func<IntPtr, bool> onUpdateDetected)
        {
            System.Threading.Thread.CurrentThread.Name = "A3UpdatePoller";
            System.Diagnostics.Debug.WriteLine("[A3ProgramUpdate] === Task 线程启动,ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId + ",timeout={timeoutMs}ms");
            System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 开始短轮询(等 A3 升级框),timeout={timeoutMs/1000}s");

            // 第 1 次立即检测(A3 可能已经弹好框了)
            if (FindDialogByTitle(UPDATE_DIALOG_TITLES, out var updateHwnd))
            {
                System.Diagnostics.Debug.WriteLine("[A3ProgramUpdate] ★ 立即检测到 A3 升级文件检测框,触发回调");
                return onUpdateDetected(updateHwnd);
            }

            // 短轮询等 A3 启动 + 检测服务端 + 弹升级框
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            int pollCount = 0;
            while (DateTime.Now < deadline)
            {
                pollCount++;
                if (FindDialogByTitle(UPDATE_DIALOG_TITLES, out updateHwnd))
                {
                    System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] ★ 轮询第 {pollCount} 次检测到 A3 升级框,触发回调");
                    return onUpdateDetected(updateHwnd);
                }
                Thread.Sleep(500);
            }

            System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 无更新,timeout={timeoutMs/1000}s 内轮询 {pollCount} 次未检测到升级框,立刻返 false(不阻塞)");
            return false;
        }

        /// <summary>枚举顶层窗口,找标题包含指定关键字的窗口(首个命中)。</summary>
                /// <summary>枚举顶层窗口,找标题包含指定关键字的窗口(首个命中)。</summary>
        private static bool FindWindowByTitleContains(string keyword, out IntPtr hwnd)
        {
            IntPtr[] foundBox = new IntPtr[1];
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                int len = GetWindowTextLengthW(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowTextW(h, sb, sb.Capacity);
                if (sb.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    foundBox[0] = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            hwnd = foundBox[0];
            return hwnd != IntPtr.Zero;
        }

        /// <summary>
        /// 自动点是按钮（场景 1/2 走这里）：抓 A3 升级框的「是」按钮,SendMessage BM_CLICK。
        /// 陛下 10:11 抓取确认:升级文件检测窗口内有「是」「否」两个 Button 控件。
        /// 限时,找不到返 false(可能是 A3 已经升级完了,框关了)。
        /// </summary>
        public static bool ClickYesButton(int timeoutMs = 3000)
        {
            // ★ 2026-08-13 17:16 陛下反馈"两个升级窗体只处理一个":
            //   同时启 client + devtools 时,两个 A3 升级框同时存在(client 一个 devtools 一个)。
            //   旧逻辑只点一个框,另一个升级框继续卡在桌面上没人管。
            //   新逻辑:循环处理所有现存 A3 升级框,直到 timeout 内连续 N 次找不到框为止。
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            int clickedCount = 0;
            int noDialogStreak = 0;  // 连续"找不到对话框"次数,>=3 认为都点完了
            while (DateTime.Now < deadline)
            {
                if (FindDialogByTitle(UPDATE_DIALOG_TITLES, out var hwnd))
                {
                    noDialogStreak = 0;
                    if (ClickButtonByText(hwnd, "是"))
                    {
                        clickedCount++;
                        System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 已点「是」按钮(累计 {clickedCount} 个)");
                        continue;  // 立刻找下一个
                    }
                }
                else
                {
                    noDialogStreak++;
                    if (noDialogStreak >= 3 && clickedCount > 0) {
                        System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 已连续 {noDialogStreak} 次未找到 A3 升级框,共点了 {clickedCount} 个「是」按钮,退出");
                        return true;
                    }
                }
                Thread.Sleep(200);
            }
            System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] ClickYesButton 超时,共点了 {clickedCount} 个「是」按钮");
            return clickedCount > 0;
        }

        /// <summary>
        /// 自动点否按钮（场景 3 陛下选否时走这里）：抓 A3 升级框的「否」按钮,SendMessage BM_CLICK。
        /// </summary>
        public static bool ClickNoButton(int timeoutMs = 3000)
        {
            // ★ 2026-08-13 17:16 陛下反馈"两个升级窗体只处理一个":
            //   循环处理所有现存 A3 升级框,直到 timeout 内连续 N 次找不到框为止。
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            int clickedCount = 0;
            int noDialogStreak = 0;
            while (DateTime.Now < deadline)
            {
                if (FindDialogByTitle(UPDATE_DIALOG_TITLES, out var hwnd))
                {
                    noDialogStreak = 0;
                    if (ClickButtonByText(hwnd, "否"))
                    {
                        clickedCount++;
                        System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 已点「否」按钮(累计 {clickedCount} 个)");
                        continue;
                    }
                }
                else
                {
                    noDialogStreak++;
                    if (noDialogStreak >= 3 && clickedCount > 0) {
                        System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 已连续 {noDialogStreak} 次未找到 A3 升级框,共点了 {clickedCount} 个「否」按钮,退出");
                        return true;
                    }
                }
                Thread.Sleep(200);
            }
            System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] ClickNoButton 超时,共点了 {clickedCount} 个「否」按钮");
            return clickedCount > 0;
        }

        /// <summary>
        /// 等 A3 升级完成（A3 弹「升级完成!」「系统提示」之类）,自动点掉。
        /// 超时返 false(陛下后续自动登录可能还要等)。
        /// </summary>
        public static bool WaitAndConfirmUpgradeComplete(int timeoutMs = 5 * 60 * 1000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (FindDialogByTitle(UPGRADE_COMPLETE_TITLES, out var hwnd))
                {
                    // 升级完成框默认按钮是「确定」(=回车),直接 BM_CLICK 抓「确定」按钮
                    // 兜底:抓不到「确定」就抓第一个 Button
                    if (ClickButtonByText(hwnd, "确定") || ClickFirstButton(hwnd))
                    {
                        System.Diagnostics.Debug.WriteLine("[A3ProgramUpdate] 已点升级完成确认");
                        return true;
                    }
                }
                Thread.Sleep(500);
            }
            System.Diagnostics.Debug.WriteLine($"[A3ProgramUpdate] 等升级完成超时 {timeoutMs/1000}s");
            return false;
        }

        #endregion

        #region 私有工具方法

        /// <summary>枚举所有顶层窗口,找标题匹配 titleKeywords 中任何一个的窗口(首个命中)。</summary>
        private static bool FindDialogByTitle(string[] titleKeywords, out IntPtr hwnd)
        {
            IntPtr[] foundBox = new IntPtr[1];
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                int len = GetWindowTextLengthW(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowTextW(h, sb, sb.Capacity);
                var title = sb.ToString();
                foreach (var kw in titleKeywords)
                {
                    if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        foundBox[0] = h;
                        return false; // 停止枚举
                    }
                }
                return true;
            }, IntPtr.Zero);
            hwnd = foundBox[0];
            return hwnd != IntPtr.Zero;
        }

        /// <summary>在窗口内枚举子控件,找标题 == buttonText 的 Button,SendMessage BM_CLICK。</summary>
        private static bool ClickButtonByText(IntPtr parentHwnd, string buttonText)
        {
            IntPtr btnHwnd = IntPtr.Zero;
            EnumChildWindows(parentHwnd, (h, l) =>
            {
                int len = GetWindowTextLengthW(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowTextW(h, sb, sb.Capacity);
                if (sb.ToString() == buttonText)
                {
                    btnHwnd = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (btnHwnd == IntPtr.Zero) return false;

            // 把按钮所在窗体拉到前台,确保 SendMessage 落到对的窗口
            SetForegroundWindow(parentHwnd);
            BringWindowToTop(parentHwnd);
            ShowWindow(parentHwnd, 9 /* SW_RESTORE */);

            SendMessageW(btnHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        /// <summary>抓窗口内第一个 Button(兜底用)。</summary>
        private static bool ClickFirstButton(IntPtr parentHwnd)
        {
            IntPtr[] foundBox = new IntPtr[1];
            EnumChildWindows(parentHwnd, (h, l) =>
            {
                var cls = new StringBuilder(256);
                GetClassNameW(h, cls, cls.Capacity);
                if (cls.ToString().Contains("BUTTON", StringComparison.OrdinalIgnoreCase))
                {
                    foundBox[0] = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            var btnHwnd = foundBox[0];
            if (btnHwnd == IntPtr.Zero) return false;

            SetForegroundWindow(parentHwnd);
            BringWindowToTop(parentHwnd);
            ShowWindow(parentHwnd, 9);
            SendMessageW(btnHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        #endregion
    }
}