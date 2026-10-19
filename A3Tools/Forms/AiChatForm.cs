using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;
using A3Tools.Services.AiActions.AiAssistantLogging;
using A3Tools.Services.ChatStore;
using A3Tools.Common.Forms; // 2026-10-10 共享账套选择器 A3Tools.Common 项目
using Markdig;

namespace A3Tools.Forms;

/// <summary>
/// AI 助理聊天主窗口
/// <para>★ 2026-08-26 陛下要求：</para>
/// <list type="bullet">
///   <item>左侧会话列表（新建/删除/切换）</item>
///   <item>右侧消息流（气泡样式）</item>
///   <item>顶部厂商切换 + 设置按钮</item>
///   <item>底部输入框（Enter 发送，Shift+Enter 换行）</item>
/// </list>
/// </summary>
public partial class AiChatForm : Form
{
    private readonly AiConfigService _configService = new();
    private readonly OpenAiCompatibleBackend _backend = new();
    private readonly MarkdownPipeline _mdPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>
    /// ★★★ 2026-10-07 根治 WebView2 DPI 空白问题（陛下多次反馈「气泡下方大片空白/内容不全」）：
    ///   反射探针证实根因：CoreWebView2Controller.BoundsMode 默认 UseRawPixels，
    ///   WinForms wrapper 把 wv2.Height(DIP) 当物理像素传进 Bounds，而 RasterizationScale=1.75，
    ///   → CSS viewport = wv2.Height ÷ 1.75（vpH 永远只有控件高度的 57%），
    ///   → HWND 物理高 = wv2.Height×1.75 但浏览器只画顶部 57%，下面 43% 空白。
    ///   修法：把 BoundsMode 改成 UseRasterizationScale，Bounds 按 CSS 像素解释：
    ///     CSS viewport = Bounds = wv2.Height，物理 = wv2.Height×1.75（正好填满 HWND）。
    ///   于是 wv2.Height = contentH 即可完美填满，零空白零裁切。
    /// </summary>
    private static Microsoft.Web.WebView2.Core.CoreWebView2Controller? GetController(Microsoft.Web.WebView2.WinForms.WebView2 wv2)
    {
        // ★★★ 2026-10-08 鲁棒性提升：WebView2 1.0.1829.34016 反射字段名可能改名 / 嵌套，
        //   按字段名列表 + CoreWebView2 内嵌 controller 字段 依次找
        try
        {
            var t = typeof(Microsoft.Web.WebView2.WinForms.WebView2);
            string[] names = { "_coreWebView2Controller", "_controller", "controller", "_coreWebView2" };
            foreach (var n in names)
            {
                var fi = t.GetField(n,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (fi != null)
                {
                    var v = fi.GetValue(wv2);
                    if (v is Microsoft.Web.WebView2.Core.CoreWebView2Controller c1) return c1;
                    if (v is Microsoft.Web.WebView2.Core.CoreWebView2 c2)
                    {
                        // 从 CoreWebView2 内部找 controller 字段
                        var ct = c2.GetType();
                        foreach (var cfi in ct.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            if (cfi.FieldType == typeof(Microsoft.Web.WebView2.Core.CoreWebView2Controller))
                                return (Microsoft.Web.WebView2.Core.CoreWebView2Controller)cfi.GetValue(c2);
                        }
                    }
                }
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// ★★★ 2026-10-08 终极修法（陛下反馈「单气泡 70% 但文字只填 49%」）：
    ///   不再依赖 BoundsMode 切换，留在默认 UseRawPixels，但手动让 Bounds 按物理像素 × DPI。
    ///   反射复核：wrapper 每次 resize 会把 Bounds 重置回 ClientSize(DIP) → vpW = ClientSize/1.75 = 49%框宽（气泡仅 49% 充满）。
    ///   本方法在每次 Resize 后强设 Bounds = ClientSize × DPI/96（按物理像素），
    ///     vpW = Bounds / RasterizationScale = (ClientSize × 1.75) / 1.75 = ClientSize ✓ 完美填满
    ///     vpH 同理 → 文字 wrap 到气泡可视宽度
    ///   wv2.Resize 钩子同步重设，保证 wrapper 覆盖后立刻压回去。
    /// </summary>
    private static void ForceWebView2Dpi(Microsoft.Web.WebView2.WinForms.WebView2 wv2)
    {
        var ctrl = GetController(wv2);
        if (ctrl == null) return;
        try { ctrl.ShouldDetectMonitorScaleChanges = false; } catch { }
        // ★★★ 2026-10-08 终极根治：RasterizationScale=1.0
        //   WinForms ClientSize 是物理像素（168 DPI → 1.75x 缩放）
        //   RasterizationScale=1.75 时 viewport=物理/1.75 = 气泡 57%（裁切 + 不换行）
        //   改 1.0 → viewport=物理像素 = 气泡完整尺寸 → WinForms 布局(物理像素) 完美匹配 WebView2 viewport(CSS像素)
        //   文本从 1.75x 锐利变 1x，但保证完整显示
        try { ctrl.RasterizationScale = 1.0; } catch { }
        try { ctrl.BoundsMode = Microsoft.Web.WebView2.Core.CoreWebView2BoundsMode.UseRawPixels; } catch { }
        try
        {
            ctrl.Bounds = new System.Drawing.Rectangle(0, 0, wv2.ClientSize.Width, wv2.ClientSize.Height);
        }
        catch { }
    }

    private static void ConfigureWebView2Dpi(Microsoft.Web.WebView2.WinForms.WebView2 wv2)
    {
        ForceWebView2Dpi(wv2);
        // ★★★ 同步 Resize 钩子：wrapper 每次 Resize 重置后立刻压回 RasterizationScale=1.0
        try
        {
            wv2.Resize += (s, e) => ForceWebView2Dpi(wv2);
        }
        catch { }
    }

    private AiChatConfig _config = new();
    private IChatStore _store = null!;
    private ChatSession _currentSession = null!;
    private AiProviderConfig? _currentProvider;
    private CancellationTokenSource? _cts;
    private bool _isSending = false;

    // ★ 2026-10-09 陛下需求：选模型左侧增加账套选择
    //   _mainForm 从构造传入，调 GetAllAccounts() 拉账套列表 + GetSelectedAccountCode() 拉当前选中
    //   _currentAccountCode / _currentAccountName 记录当前选中账套，BuildSystemPrompt 注入
    //   "" 表示未选 → AI 需询问陛下
    private MainForm? _mainForm;
    private string _currentAccountCode = string.Empty;
    private string _currentAccountName = string.Empty;

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOMOVE = 0x0002;

    // ★ 2026-09-30 陛下反馈：打开历史会话大量消息卡
    //   改为虚拟列表: 初始只渲染最后 10 条, 往上滚到底触发加载更多 (每次 +10)
    //   窗口上限 25 (用户/AI 发新消息时维持, 不无限增长)
    private int _visibleStartIndex;   // flpMessages.Controls 里第一个气泡对应的 _currentSession.Messages 索引
    private bool _isLoadingOlder;     // 防重入 (Scroll 事件可能短时间内多次触发)
    private const int InitialWindowSize = 10;
    private const int LoadMoreBatchSize = 10;
    private const int MaxWindowSize = 25;
    private List<ChatToolCallRecord>? _pendingToolCallLog;

    public AiChatForm(MainForm? mainForm = null)
    {
        InitializeComponent();
        // ★ 2026-10-17 v2.9: 独立顶级窗口, 不被 MainForm 隐藏影响, 任务栏可找
        this.ShowInTaskbar = true;
        // ★ 2026-10-17 v2.10: 支持 ESC 关闭快捷键
        this.KeyPreview = true;
        this.KeyDown += AiChatForm_KeyDown;
        // ★ 2026-10-09 账套选择：仿工具箱样式（按钮 + 显示标签 + 清空按钮）
        _mainForm = mainForm;
        btnSelectAccount.Click += BtnSelectAccount_Click;
        btnClearAccount.Click += BtnClearAccount_Click;
        // 加载上次选择 + 刷新显示
        LoadSelectedAccountFromSettings();
        RefreshAccountDisplay();
        // 绑定事件
        cmbProvider.SelectedIndexChanged += (_, _) => SwitchProvider();
        btnSettings.Click += BtnSettings_Click;
        btnNewSession.Click += (_, _) => NewSession();
        lstSessions.SelectedIndexChanged += (_, _) => LoadSelectedSession();
        btnDeleteSession.Click += BtnDeleteSession_Click;
        btnClearAll.Click += BtnClearAll_Click;
        txtInput.KeyDown += TxtInput_KeyDown;
        btnSend.Click += BtnSend_Click;
        btnCancel.Click += (_, _) => _cts?.Cancel();

        // pnlMessages Dock=None，需手动同步宽度以填满 splitChat.Panel1
        //   ★ 2026-08-29 方案 A：flpMessages Dock=Top，会自动跟 pnlMessagesScroll 宽度走，不需要手同步
        Resize += (_, _) => OnContainerResized();
        pnlMessagesScroll.Resize += (_, _) => OnContainerResized();
        pnlInput.Resize += (_, _) => LayoutInputPanel();
        splitChat.Panel2.Resize += (_, _) => LayoutInputPanel();
        pnlSidebar.Resize += (_, _) => LayoutSidebar();

        // ★ v15: WebView2 异步渲染时 FlowLayoutPanel 高度变化 → 强制 Value = Maximum
        //   陛下 17:30 反馈："正常应该是交叉渲染"（正序交叉 Add 顺序）✅
        //   加上 Resize 事件: WebView2 加载完 → Resize 触发 → Value 跟随 Maximum 同步
        //   视觉：屏幕焦点始终在 FlowLayoutPanel 底部 = 最新消息 (AI:对话10)
        //   滚动条 Value 不变（在 Maximum）→ "滚动条不动" ✅
        //   debounce 200ms 在 flpMessages_Resize 里处理
        flpMessages.Resize += flpMessages_Resize;

        // ★ 2026-09-30 虚拟列表: Scroll 事件监听"滚到顶", 触发 LoadOlderMessages
        //   (但 Scroll 事件在某些场景不触发, 见下方 EnsureScrollPollTimer 注释)
        pnlMessagesScroll.Scroll += PnlMessagesScroll_Scroll;

        // ★ 2026-09-30 v4 根因修复: 不依赖 Scroll 事件, 构造时直接启动轮询 Timer
        //   原因: 陛下报告 "多次滚动都不加载, 日志文件也没" — 二哈看代码发现
        //   EnsureScrollPollTimer() 只在 Scroll handler 里调, 如果 Scroll 事件不触发
        //   Timer 永远不启动, LoadOlder 永远不进, 日志没一行
        //   修法: 构造时直接启动 Timer, Scroll handler 只是个 fallback
        EnsureScrollPollTimer();
        DebugLog("===== AI scroll diag started (v4) =====");
        DebugLog($"current dir = {AppDomain.CurrentDomain.BaseDirectory}");
        DebugLog($"form created, _visibleStartIndex={_visibleStartIndex}");

        // ★ 2026-08-27 修陛下反馈「打开聊天框后不自动滚动到最下方」：
        //   窗体完全显示后 + layout 完成后才滚到底（构造函数里 RenderMessages 时控件还没 layout 完，Maximum=0 滚不动）
        Shown += (_, _) =>
        {
            ScrollToBottom();
            LayoutInputPanel(); // ★★★ 2026-09-02 初始按钮位置同步贴右边
            LayoutSidebar();    // ★★★ 2026-09-02 sidebar 控件初始位置
        };

        // 立即设真实约束（不等 Shown，避免首帧 RenderMessages 拿到错的 Panel1 宽度）
        splitChat.Panel1MinSize = 200;
        splitChat.Panel2MinSize = 160;
        splitChat.SplitterDistance = Math.Max(200, splitChat.Height - 160 - splitChat.SplitterWidth);

        // ★ 2026-08-29 方案 A：flpMessages 自己管 Padding=(22,14,22,4)，splitChat.Panel1 还原为默认
        splitChat.Panel1.Padding = new Padding(0);
        // ★ 2026-08-29 修复「聊天记录空白」：之前误删了 splitChat.Panel1.Controls.Add(pnlMessagesScroll)
        //   现在 pnlMessagesScroll 装 flpMessages，flpMessages 装气泡
        if (splitChat.Panel1.Controls.Contains(pnlMessagesScroll) == false)
            splitChat.Panel1.Controls.Add(pnlMessagesScroll);

        // 兼容保留 SyncMessagesWidth（现在是空操作）
        SyncMessagesWidth();

        LoadConfig();
        InitStore();
        RefreshSessions();
        if (_config.AutoResumeLastSession)
            LoadLastSession();
        else
            NewSession();
    }

    private void SyncMessagesWidth()
    {
        // ★ 2026-08-28 终极修法：row 直接挂在 pnlMessagesScroll（ScrollableControl）上
        //   不再有中间层 FlowLayoutPanel → 什么都不能手动设宽了，SyncMessagesWidth 变成了"空操作"
        //   ScrollableControl 会随客户端宽度自动 layout，row 自己的 width 在 CreateBubbleRow 里取 pnlMessagesScroll.ClientSize.Width
    }

    // ========== 数据初始化 ==========

    private void LoadConfig()
    {
        _config = _configService.LoadConfig();
        // ★ 2026-10-07 修陛下反馈「日志开关取消勾选仍记录」：
        //   AiAssistantLogger.Enabled 是进程级静态字段（默认 true）
        //   之前只在 AiSettingsForm 打开时 / 保存时同步，AiChatForm 启动不会同步
        //   导致：配置 EnableAiLog=false 但 Enabled 仍为 true，日志照写
        AiAssistantLogger.Enabled = _config.EnableAiLog;
        // ★ 2026-10-09 失败重试配置同步到 backend
        _backend.MaxRetries = _config.MaxRetryCount;
        _backend.RetryIntervalSeconds = _config.RetryIntervalSeconds;
        cmbProvider.Items.Clear();
        foreach (var p in _config.Providers.Where(x => x.Enabled))
        {
            cmbProvider.Items.Add($"{p.Name} ({p.Model})");
        }
        if (cmbProvider.Items.Count == 0)
        {
            cmbProvider.Items.Add("（未配置厂商，请点设置）");
            cmbProvider.SelectedIndex = 0;
            cmbProvider.Enabled = false;
            _currentProvider = null;
        }
        else
        {
            var def = _configService.GetDefaultProvider();
            if (def != null)
            {
                int idx = _config.Providers.FindIndex(p => p.Id == def.Id);
                if (idx >= 0) cmbProvider.SelectedIndex = idx;
                else cmbProvider.SelectedIndex = 0;
                _currentProvider = def;
            }
            else
            {
                cmbProvider.SelectedIndex = 0;
                _currentProvider = _config.Providers.FirstOrDefault(p => p.Enabled);
            }
        }
        UpdateProviderLabel();
    }

    // ★ 2026-10-09 陛下需求：选模型左侧增加账套选择
    // ★ 2026-10-09 仿工具箱上方样式：账套选择 = 「选择账套…」按钮 + 显示标签 + 「清空」按钮
    //   选账套弹 AccountSelectForm（DataGridView 多列显示 编码/名称/服务器/备注 + 搜索框）
    //   选中后存 _currentAccountCode / _currentAccountName + 持久化 AppSettings.LastSelectedAccount
    //   BuildSystemPrompt 注入「当前账套 = XXX」AI 默认用
    //   陛下消息里明确「在账套 999 上…」可覆盖

    /// <summary>
    /// 从 AppSettings.LastSelectedAccount 恢复上次选择（找不到对应账套就清空）
    /// </summary>
    private void LoadSelectedAccountFromSettings()
    {
        try
        {
            var ds = new DataService();
            var settings = ds.LoadSettings();
            string code = settings.LastSelectedAccount ?? "";
            if (string.IsNullOrEmpty(code)) return;

            List<Account> accounts = _mainForm != null
                ? _mainForm.GetAllAccounts()
                : ds.LoadAccounts();
            var acc = accounts.FirstOrDefault(a => a.Code == code);
            if (acc != null)
            {
                _currentAccountCode = acc.Code;
                _currentAccountName = acc.Name ?? "";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI Chat] 加载账套选择失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 「选择账套…」按钮 → 跟工具箱上方账套选择逐字同款
    /// 陛下 2026-10-10 反馈：
    ///   1. 「A3Tools 是让你直接调用工具箱那个账套选择」—— 不再造并行选择器
    ///   2. 「本来用的就是统一抽出来的」—— A3Tools.Common.Form.Forms.AccountSelectForm 是共享类
    ///   3. 「把工具箱的账套选择都改了」—— MainForm.SelectToolAccount 保持原状，不动
    ///   本方法仅调 _mainForm.GetAllAccounts() 拿账套 + new 共享选择器，逻辑跟 MainForm.SelectToolAccount 一样
    /// </summary>
    private void BtnSelectAccount_Click(object? sender, EventArgs e)
    {
        try
        {
            // 拉账套列表：主路径走 _mainForm（跟工具箱 SelectToolAccount 同款）
            // 独立调用场景（_mainForm 为 null）走 DataService fallback
            List<Account> accounts = _mainForm != null
                ? _mainForm.GetAllAccounts()
                : new DataService().LoadAccounts();
            if (accounts.Count == 0)
            {
                MessageBox.Show(this, "尚未配置任何账套，请到 A3Tools 主界面新增账套。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // ★ 2026-10-10 关键：直接调共享选择器 A3Tools.Common.Forms.AccountSelectForm
            //   这是工具箱 SelectToolAccount 用的同一个类，不再造并行
            using var dlg = new AccountSelectForm(accounts);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedAccount != null)
            {
                ApplySelectedAccount(dlg.SelectedAccount);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"选择账套出错：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 应用陛下选中的账套：更新字段 + 持久化 + 刷新显示 + 写日志
    /// </summary>
    private void ApplySelectedAccount(Account acc)
    {
        _currentAccountCode = acc.Code;
        _currentAccountName = acc.Name ?? "";

        // 持久化到 AppSettings.LastSelectedAccount
        try
        {
            var ds = new DataService();
            var settings = ds.LoadSettings();
            settings.LastSelectedAccount = acc.Code;
            ds.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI Chat] 保存账套选择失败: {ex.Message}");
        }

        RefreshAccountDisplay();
        DebugLog($"账套切换: code='{acc.Code}', name='{acc.Name}'");
    }

    /// <summary>
    /// 「清空」按钮 → 把 _currentAccountCode / _currentAccountName 置空
    /// </summary>
    private void BtnClearAccount_Click(object? sender, EventArgs e)
    {
        _currentAccountCode = "";
        _currentAccountName = "";

        // 持久化清空
        try
        {
            var ds = new DataService();
            var settings = ds.LoadSettings();
            settings.LastSelectedAccount = "";
            ds.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI Chat] 清空账套选择失败: {ex.Message}");
        }

        RefreshAccountDisplay();
        DebugLog("账套已清空");
    }

    /// <summary>
    /// 刷新 lblSelectedAccountName 显示
    ///   未选: 「（未选择）」 浅色
    ///   选中: 「{Code} - {Name} ({Server})」 白
    /// </summary>
    private void RefreshAccountDisplay()
    {
        if (string.IsNullOrEmpty(_currentAccountCode))
        {
            lblSelectedAccountName.Text = "（未选择）";
            lblSelectedAccountName.ForeColor = Color.FromArgb(220, 230, 245);
            btnClearAccount.Enabled = false;
        }
        else
        {
            string display = $"{_currentAccountCode} - {_currentAccountName}";
            try
            {
                List<Account> accounts = _mainForm != null
                    ? _mainForm.GetAllAccounts()
                    : new DataService().LoadAccounts();
                var acc = accounts.FirstOrDefault(a => a.Code == _currentAccountCode);
                if (acc != null && !string.IsNullOrEmpty(acc.Server))
                    display = $"{_currentAccountCode} - {acc.Name} ({acc.Server})";
            }
            catch { /* 显示简化版 */ }
            lblSelectedAccountName.Text = display;
            lblSelectedAccountName.ForeColor = Color.White;
            btnClearAccount.Enabled = true;
        }
    }

    private void InitStore()
    {
        _store = _config.StoreStrategy switch
        {
            ChatStoreStrategy.Memory => new MemoryChatStore(),
            ChatStoreStrategy.Disabled => new NullChatStore(),
            _ => new LocalChatStore()
        };
    }

    private void RefreshSessions()
    {
        lstSessions.Items.Clear();
        var sessions = _store.GetSessions();
        foreach (var s in sessions)
        {
            string title = s.Title;
            if (title.Length > 22) title = title.Substring(0, 22) + "...";
            string time = s.LastActiveAtUtc.ToLocalTime().ToString("MM-dd HH:mm");
            lstSessions.Items.Add($"[{time}] {title}");
        }
    }

    private void LoadLastSession()
    {
        var sessions = _store.GetSessions();
        if (sessions.Count > 0)
        {
            lstSessions.SelectedIndex = 0;
            LoadSelectedSession();
        }
        else
        {
            NewSession();
        }
    }

    private void NewSession()
    {
        _currentSession = new ChatSession
        {
            Title = "新会话",
            CreatedAtUtc = DateTime.UtcNow,
            LastActiveAtUtc = DateTime.UtcNow
        };
        if (_currentProvider != null)
        {
            _currentSession.ProviderId = _currentProvider.Id;
            _currentSession.Model = _currentProvider.Model;
        }
        _store.SaveSession(_currentSession);
        RefreshSessions();
        RenderMessages();
        txtInput.Focus();
    }

    private void LoadSelectedSession()
    {
        var sessions = _store.GetSessions();
        int idx = lstSessions.SelectedIndex;
        if (idx < 0 || idx >= sessions.Count) return;
        _currentSession = sessions[idx];
        // ★ 2026-10-13 B+C 优化：只拉最后 InitialWindowSize 条消息，不加载全量
        //   之前是 _currentSession = sessions[idx]（GetSession() 会全量解密），1000+ 条消息卡顿
        //   现在 GetSessions() 只返元数据 + GetSessionTail 只解密最后 10 条 ≈ 5ms
        _currentSession.Messages = _store.GetSessionTail(_currentSession.Id, InitialWindowSize);
        RenderMessages();
        // ★ 2026-10-17 v2.11 历史会话加载 settle: 150ms 后一次性应用所有 wv2 缓存高度
        //   加载期 ResizeObserver 只 buffer 高度, 不改 wv2.Height, 避免 50 个 wv2 各自触发 reflow 让上方气泡一直抖
        StartHistorySettle();
    }

    private void SwitchProvider()
    {
        int idx = cmbProvider.SelectedIndex;
        var enabledList = _config.Providers.Where(p => p.Enabled).ToList();
        if (idx >= 0 && idx < enabledList.Count)
        {
            _currentProvider = enabledList[idx];
            if (_currentSession != null)
            {
                _currentSession.ProviderId = _currentProvider.Id;
                _currentSession.Model = _currentProvider.Model;
                _store.SaveSession(_currentSession);
            }
            UpdateProviderLabel();
        }
    }

    private void UpdateProviderLabel()
    {
        if (_currentProvider == null)
        {
            lblCurrentProvider.Text = "未配置厂商，请点设置添加厂商";
            lblCurrentProvider.ForeColor = System.Drawing.Color.FromArgb(251, 67, 42);
        }
        else
        {
            lblCurrentProvider.Text = $"当前：{_currentProvider.Name} · {_currentProvider.Model}";
            lblCurrentProvider.ForeColor = System.Drawing.Color.FromArgb(60, 60, 60);
        }
    }

    // ========== 渲染消息 ==========

    // ★ v16: 删掉了 v15 的 _lastRenderScrollTime / RenderScrollDebounceMs
    //   每次 Resize 都设 Value = Maximum 即可, WinForms 同值自动去重, 不需要防抖

    // ★ v17: 跳底追逐 Timer (覆盖 WebView2 异步加载)
    //   Maximum 稳定 500ms 后自动停止, 避免一直跟用户滚动抢位置
    private System.Windows.Forms.Timer? _scrollChaseTimer = null;

    /// <summary>
    /// v17 核心: 设 Value = Maximum (O(1), WinForms 同值自动去重)
    ///   抽出来给 RenderMessages 末尾 + BeginInvoke + flpMessages_Resize + 追逐 Timer 共用
    /// </summary>
    private void JumpToBottom()
    {
        try
        {
            int max = pnlMessagesScroll.VerticalScroll.Maximum;
            int cur = pnlMessagesScroll.VerticalScroll.Value;
            if (max > 0 && cur != max)
            {
                pnlMessagesScroll.VerticalScroll.Value = max;
            }
        }
        catch { /* 静默失败 */ }
    }

    /// <summary>
    /// v17 追逐 Timer: WebView2 异步加载时 Maximum 持续增大, 启动一个 Timer
    ///   每 100ms 设一次 Value = Maximum, Maximum 连续 5 tick (500ms) 稳定后停止
    ///   避免一直跟用户上滚抢位置 (Maximum 稳定意味着 WebView2 全部加载完)
    /// </summary>
    private void StartScrollChase()
    {
        // 停掉旧的 (避免多次 RenderMessages 调用积累 Timer 泄露)
        StopScrollChase();
        try
        {
            _scrollChaseTimer = new System.Windows.Forms.Timer { Interval = 100, Enabled = false };
            int lastMax = -1;
            int stableTicks = 0;
            _scrollChaseTimer.Tick += (_, _) =>
            {
                try
                {
                    int max = pnlMessagesScroll.VerticalScroll.Maximum;
                    // Maximum 稳定 5 tick (500ms) 意味着 WebView2 全部加载完, 停止追逐
                    if (max == lastMax)
                    {
                        stableTicks++;
                        if (stableTicks >= 5)
                        {
                            StopScrollChase();
                            return;
                        }
                    }
                    else
                    {
                        stableTicks = 0;
                        lastMax = max;
                    }
                    JumpToBottom();
                }
                catch
                {
                    StopScrollChase();
                }
            };
            _scrollChaseTimer.Start();
        }
        catch { /* Timer 启动失败不影响主流程 */ }
    }

    private void StopScrollChase()
    {
        if (_scrollChaseTimer != null)
        {
            try { _scrollChaseTimer.Stop(); } catch { }
            try { _scrollChaseTimer.Dispose(); } catch { }
            _scrollChaseTimer = null;
        }
    }

    /// <summary>
    /// ★ 2026-10-17 v2.11 历史会话加载 settle:
    ///   150ms 内 wv2 ResizeObserver 全部 fire, handler 只 buffer 高度不应用
    ///   150ms 后一次性把所有 buffer 高度写到 wv2.Height + 1 次 reflow → 上方气泡不抖
    ///   对比修复前: 50 个 wv2 各自触发 1 次 reflow = 50 次 FlowLayoutPanel 重排 = 上方一直抖
    /// </summary>
    private void StartHistorySettle()
    {
        StopHistorySettle();
        _isLoadingHistory = true;
        _pendingHistoryHeights.Clear();
        try
        {
            _historySettleTimer = new System.Windows.Forms.Timer { Interval = 150, Enabled = false };
            _historySettleTimer.Tick += (_, _) =>
            {
                try
                {
                    // 一次性应用所有缓存高度
                    foreach (var kv in _pendingHistoryHeights)
                    {
                        try { kv.Key.Height = kv.Value; } catch { /* wv2 已 dispose, 静默 */ }
                    }
                    _pendingHistoryHeights.Clear();
                    _isLoadingHistory = false;

                    // 一次性 reflow
                    flpMessages.ResumeLayout(true);
                    pnlMessagesScroll.PerformLayout();
                    JumpToBottom();
                }
                catch { /* 静默失败 */ }
                finally
                {
                    StopHistorySettle();
                }
            };
            _historySettleTimer.Start();
        }
        catch { /* Timer 启动失败不影响主流程 */ }
    }

    private void StopHistorySettle()
    {
        if (_historySettleTimer != null)
        {
            try { _historySettleTimer.Stop(); } catch { }
            try { _historySettleTimer.Dispose(); } catch { }
            _historySettleTimer = null;
        }
    }

    private void RenderMessages()
    {
        // ★ v15: 2026-10-13 陛下 17:26 反馈
        //   "感觉没有什么变化，看起来你像是先渲染用户对话内容，又渲染AI对话内容"
        //   陛下又澄清了 "先渲染" 的真正含义：
        //     "先渲染 AI: 对话10内容" → 用户视觉上第一个看到的是 AI:对话10（最新）
        //     "再渲染 我: 问题10"    → 然后才看到 我:问题10
        //     "一次向上"             → 视觉上从底部向上扩展
        //
        // 修法（v15）：
        //   1) 正序 Add (最老先 Add, 最新最后 Add)
        //      v14 倒序 Add + SetChildIndex(0) 跟正序 Add 最终 Controls 集合索引完全一样
        //      （倒序 Add 后 SetChildIndex(0) = 新加的移到顶部, 但 后续 Add 又把之前的挤下去）
        //      所以 v14 跟 v13 之前的正序 Add 最终结果完全一样 = "没有变化" 正是陛下的反馈
        //
        //   2) ResumeLayout(true) 后**立即** Value = Maximum (不绕扊eginInvoke, 不用 500ms Timer)
        //      关键洞察：ResumeLayout(true) 同步 layout, Maximum 已更新
        //      立即 Value = Maximum 触发 Invalidate
        //      下一次 paint 直接画 FlowLayoutPanel 底部 (AI:对话10)
        //      用户**第一个看到**的就是 AI:对话10 (最新) ✅
        //      没有 "先看到 我:问题6 然后跳到 AI:对话10" 的闪烁
        //
        //   3) FlowLayoutPanel.Resize 事件 + 200ms debounce
        //      WebView2 异步渲染时 FlowLayoutPanel 高度变化 → Resize 触发
        //      自动 Value = Maximum → 屏幕焦点始终在 FlowLayoutPanel 底部
        //      视觉：屏幕内容向上滚（老内容向上滚出屏幕，新内容从屏幕底部扩展）
        //      滚动条 Value 不变 (在 Maximum) → "滚动条不动" ✅
        //      老内容在屏幕上方 = 视觉外 = "静默渲染" ✅
        //      debounce 200ms 避免 WebView2 频繁 resize 时反复设 Value (闪烁)
        flpMessages.SuspendLayout();
        flpMessages.Controls.Clear();
        if (_currentSession == null) { flpMessages.ResumeLayout(true); return; }

        SyncMessagesWidth();

        int total = _currentSession.Messages.Count;
        if (total == 0) { flpMessages.ResumeLayout(true); return; }

        // 初始窗口: 只显示最后 InitialWindowSize 条
        _visibleStartIndex = Math.Max(0, total - InitialWindowSize);

        // ★ v15: 正序 Add (最老先 Add, 最新最后 Add)
        //   最终 Controls 集合索引: [sentinel, 我:问题6, AI:对话6, ..., 我:问题10, AI:对话10]
        //   视觉布局：最老在顶部, 最新在底部 (微信样式)
        //   跟 v14 倒序 Add + SetChildIndex(0) 最终结果完全一样, 但代码更简洁
        if (_visibleStartIndex > 0)
        {
            flpMessages.Controls.Add(CreateLoadMoreSentinel(_visibleStartIndex));
        }

        for (int i = _visibleStartIndex; i < total; i++)
        {
            var msg = _currentSession.Messages[i];
            var bubble = CreateBubbleRow(msg, source: "Render");
            flpMessages.Controls.Add(bubble);
        }

        flpMessages.ResumeLayout(true);

        // ★ v17: 2026-10-14 陛下 09:03 反馈
        //   "还是不行很奇怪奇数此是可以的，比如第一次打开可以定位到最低处，
        //    但切换到其他会话再切换回来，就不行，但再切换其他会话，再切换回来确是可以的"
        //
        // 真根因：flpMessages.ResumeLayout(true) 只 layout flpMessages 自身,
        //   不会自动 layout 父控件 pnlMessagesScroll.
        //   立刻读 pnlMessagesScroll.VerticalScroll.Maximum 拿到的是**旧值或 0**
        //   → Value = 0 设进去 → 跳不到底
        //
        // 奇偶差异解释：
        //   - 1st (LoadLastSession) 成功: Form 第一次显示, Application 主动跑完整 layout pass,
        //                              pnlMessagesScroll.Maximum 已是正确值
        //   - 2nd 失败: pnlMessagesScroll 自上次切换后没人触发 layout, Maximum 残留旧值
        //   - 3rd 成功: 2nd 失败时 pnlMessagesScroll.VerticalScroll 内部状态异常
        //              导致下个 Application.Idle 主动 layout → Maximum 正确
        //   - 4th 失败: 跟 2nd 一样
        //
        // 修法 4 层防御:
        //   1) pnlMessagesScroll.PerformLayout() 强制父控件 layout (修根因)
        //   2) JumpToBottom() 立即设 Value = Maximum
        //   3) BeginInvoke: 消息循环空闲时再跳一次 (覆盖 PerformLayout 还没生效的情况)
        //   4) ScrollControlIntoView(lastBubble): WinForms 内置 API, 最可靠
        //   5) StartScrollChase(): 2 秒追逐 Timer 覆盖 WebView2 异步加载
        try { pnlMessagesScroll.PerformLayout(); } catch { /* 静默失败 */ }
        JumpToBottom();

        try
        {
            this.BeginInvoke(new Action(() =>
            {
                JumpToBottom();
                // ScrollControlIntoView: WinForms 内置 API, 强制把最后一个气泡滚到可见
                if (flpMessages.Controls.Count > 0)
                {
                    var lastCtrl = flpMessages.Controls[flpMessages.Controls.Count - 1];
                    try { pnlMessagesScroll.ScrollControlIntoView(lastCtrl); } catch { }
                    JumpToBottom();
                }
            }));
        }
        catch { /* BeginInvoke 失败不影响主流程 */ }

        StartScrollChase();
    }

    /// <summary>
    /// ★ v16: 2026-10-14 陛下 08:40 反馈
    ///   "第一次打开会话可以滚动到最底部，但切换后再次切换回来无法定位到最底部"
    ///
    /// 真根因：v15 用了 200ms debounce, 切回时 RenderMessages 立即设 Value = Maximum
    ///   之后 WebView2 异步加载, FlowLayoutPanel.Resize 频繁触发
    ///   200ms 内 Resize 全部被 debounce 吃掉 → Maximum 持续增大但 Value 不跟 → 屏幕停在中间
    ///   200ms 后才更新一次, 跳到一半又可能错过 WebView2 最终状态
    ///
    /// 修法：去掉 debounce! 每次 Resize 都设 Value = Maximum
    ///   设 Value = Maximum 是 O(1), WinForms 同值不触发 Invalidate
    ///   即使 Resize 触发 100 次, 也只是设 100 次同一个值, 无性能/闪烁问题
    /// </summary>
    private void flpMessages_Resize(object sender, EventArgs e)
    {
        if (flpMessages.Controls.Count == 0) return;
        if (!pnlMessagesScroll.Visible) return;
        JumpToBottom();
    }

    /// <summary>
    /// 往上滚到顶时, 把更早的消息 prepend 到 flpMessages.Controls (每次 +10)
    /// ★ 关键: prepend 后必须保持滚动位置 (用户视觉上看不出跳变)
    ///   FlowLayoutPanel.Add() 总是 append 到末尾, 所以要按"从老到新"顺序 Add,
    ///   才能让 Add 出的控件顺序 = 视觉上从上到下的顺序
    /// </summary>
    private void LoadOlderMessages(int count)
    {
        if (_isLoadingOlder || _currentSession == null) return;
        int total = _currentSession.Messages.Count;
        if (_visibleStartIndex <= 0) return;

        int newStart = Math.Max(0, _visibleStartIndex - count);
        int actuallyLoad = _visibleStartIndex - newStart;
        if (actuallyLoad <= 0) return;

        DebugLog($"LoadOlderMessages ENTER: count={count} vStart={_visibleStartIndex} newStart={newStart} actuallyLoad={actuallyLoad} total={total} controlsBefore={flpMessages.Controls.Count}");

        _isLoadingOlder = true;
        try
        {
            // 1) 记录当前滚动 Y (滚动条值)
            int oldScrollY = pnlMessagesScroll.VerticalScroll.Value;

            // 2) 移除顶部 sentinel (它是索引 0)
            if (flpMessages.Controls.Count > 0 && flpMessages.Controls[0].Tag is SentinelLoadMore)
            {
                flpMessages.Controls.RemoveAt(0);
            }

            // 3) ★★ 2026-10-13 B+C 优化: 从 store 拉增量切片, 不动整个 in-memory messages 数组
            //   之前是从 _currentSession.Messages[newStart.._visibleStartIndex] 切片
            //   现在改成 _store.GetSessionMessagesRange(id, newStart, count) 只读 + 解密 older
            //   然后 _currentSession.Messages.InsertRange(0, older) 同步 in-memory 累积视图
            var olderMessages = _store.GetSessionMessagesRange(_currentSession.Id, newStart, actuallyLoad);
            if (olderMessages.Count == 0)
            {
                // store 没拉到（文件被删/损坏）→ 直接退出，不渲染也不动 _visibleStartIndex
                return;
            }
            // 同步 in-memory 累积视图
            _currentSession.Messages.InsertRange(0, olderMessages);

            flpMessages.SuspendLayout();
            var newBubbles = new System.Collections.Generic.List<Control>();
            for (int i = 0; i < olderMessages.Count; i++)  // forward: 拉到的消息
            {
                var msg = olderMessages[i];
                var bubble = CreateBubbleRow(msg, source: "LoadOlder");
                flpMessages.Controls.Add(bubble);  // append 到末尾
                newBubbles.Add(bubble);
            }
            // 把新气泡从末尾移到顶部, 按顺序排 (older[0] 在 index 0, older[last] 在 index N-1)
            for (int i = 0; i < newBubbles.Count; i++)
            {
                flpMessages.Controls.SetChildIndex(newBubbles[i], i);
            }
            _visibleStartIndex = newStart;

            // 4) 加新 sentinel 在 index 0 (最顶部)
            if (_visibleStartIndex > 0)
            {
                var newSentinel = CreateLoadMoreSentinel(_visibleStartIndex);
                flpMessages.Controls.Add(newSentinel);  // append
                flpMessages.Controls.SetChildIndex(newSentinel, 0);  // 移到最顶部
            }
            flpMessages.ResumeLayout(true);
            pnlMessagesScroll.PerformLayout();

            int capturedNewBubblesCount = newBubbles.Count;
            int capturedOldY = oldScrollY;
            int actualPrependedHeight = 0;
            if (newBubbles.Count > 0)
            {
                Control lastNew = newBubbles[newBubbles.Count - 1];
                actualPrependedHeight = lastNew.Bottom + lastNew.Margin.Bottom;
            }
            // 5) ★ v13: 简化滚动保持 — 取消 v12 5fps 10 次轮询
            //   陛下 15:51 反馈：“滚动条直至不停向下跳，加载完可视区域闪烁”
            //   根因：v12 Timer 200ms × 10 = 2 秒轮询，每次都设 Value + PerformLayout → 连续重布局
            //         ResizeObserver 看到 wv2.Height 变多次 → 多次 SmartScrollToBottom → 跳到底部
            //   修法：一次性等 WebView2 init（单次 Timer 500ms），读 originalFirst.Top 设 Value 后立刻 Stop+Dispose
            //         不再轮询、不再 PerformLayout 中间态 → 布局稳定 + 滚动条不跳
            //   失败容忍：500ms 还没 init 也无所谓，不强求还原（用户可以手动重新滚到原位置）
            int capturedIdx = 1 + capturedNewBubblesCount;
            DebugLog($"scroll preserve v13 scheduled: oldY={capturedOldY} prepended={actualPrependedHeight} (newBubbles={capturedNewBubblesCount}) capturedIdx={capturedIdx}");
            try
            {
                var singleShot = new System.Windows.Forms.Timer { Interval = 500, Enabled = false };
                singleShot.Tick += (_, _) =>
                {
                    singleShot.Stop();
                    singleShot.Dispose();
                    try
                    {
                        if (capturedIdx >= flpMessages.Controls.Count) return;
                        Control originalFirst = flpMessages.Controls[capturedIdx];
                        int targetY = originalFirst.Top;
                        int maxY = pnlMessagesScroll.VerticalScroll.Maximum;
                        if (targetY > maxY) targetY = maxY;
                        if (targetY < 0) targetY = 0;
                        pnlMessagesScroll.VerticalScroll.Value = targetY;
                        DebugLog($"scroll preserve v13 SET: targetY={targetY} maxY={maxY}");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"scroll preserve v13 EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    }
                };
                singleShot.Start();
            }
            catch (Exception ex)
            {
                DebugLog($"scroll preserve v13 Timer start FAILED: {ex.GetType().Name}: {ex.Message}");
            }
        }
        finally
        {
            _isLoadingOlder = false;
        }
        DebugLog($"LoadOlderMessages EXIT: vStart={_visibleStartIndex} controlsAfter={flpMessages.Controls.Count}");
    }

    /// <summary>
    /// "加载更早消息" 顶部占位 panel, 视觉提示用户还有更早内容
    /// ★ 不用按钮: Scroll 事件检测到 Value=0 自动 LoadOlder, 避免点击触发
    ///   Tag = SentinelLoadMore 标记, 方便 LoadOlderMessages 识别/移除
    /// </summary>
    private Panel CreateLoadMoreSentinel(int remainingOlder)
    {
        var lbl = new Label
        {
            Text = "↑ 往上滚加载更早 " + remainingOlder + " 条消息...",
            AutoSize = false,
            Height = 28,
            Dock = DockStyle.Top,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
            ForeColor = System.Drawing.Color.FromArgb(150, 150, 150),
            BackColor = System.Drawing.Color.Transparent,
            Cursor = System.Windows.Forms.Cursors.Default,
            Margin = new Padding(0, 4, 0, 4),
        };
        var p = new Panel
        {
            Width = flpMessages.ClientSize.Width,
            Height = 36,
            BackColor = System.Drawing.Color.Transparent,
            Margin = new Padding(0),
        };
        p.Controls.Add(lbl);
        p.Tag = new SentinelLoadMore();
        return p;
    }

    /// <summary> Sentinel 标签 (区分"加载更多"panel 和普通气泡 row) </summary>
    private sealed class SentinelLoadMore { }

    /// <summary>
    /// 创建一条消息气泡 row（方案 A v4：Label 直接当气泡）
    /// ★ 2026-08-29 陛下灵魂拷问「你一直算不明白，到底哪种方式能改好」：
    ///   - **Label 直接当气泡（AutoSize=true 100% 可靠）**
    ///   - Label.MaximumSize.Width 限制最大宽 → 自动换行
    ///   - Label.Padding 直接生效（背景包含 padding）
    ///   - Label.AutoSize 包含 Padding 的尺寸计算
    ///   - **row = 简单 Panel，Width 锁定 flp.ClientSize.Width，高度 = bubble.PreferredSize.Height + Margin**
    ///   - AI/用户靠左/靠右：row.Width - bubble.Width - padding
    ///   - 这是唯一需要计算的量（二哈诚承认这一点）
    /// </summary>
    private Panel CreateBubbleRow(ChatMessage msg, string source = "AddNew")
    {
        bool isUser = msg.Role == ChatRole.User;
        bool isError = msg.IsError;
        bool isSystem = msg.Role == ChatRole.System;

        // flp 撑满父容器后，气泡可用最大宽度 = flp.ClientSize.Width - flp Padding
        int flpClientW = Math.Max(400, flpMessages.ClientSize.Width - flpMessages.Padding.Horizontal);
        const int sidePadding = 24;
        const int cornerRadius = 10;

        // 气泡最大宽度 = 可用宽度的 85%（陛下要求加宽）
        int bubbleMaxW = (int)((flpClientW - sidePadding * 2) * 0.85);

        // 处理文本内容
        string displayContent = isSystem ? "" : msg.Content;
        if (!isUser && !isSystem)
        {
            // ★★★ 2026-09-02 陛下反馈：AI 气泡根本不需要 MarkdownToPrettyText/MarkdownTableToText 预处理
            //   AI/Error 气泡走 WebView2(下面 NavigateToString)，WebView2 能直接渲染完整 Markdown：
            //     ## 标题 / ### 标题 / **粗体** / `代码` / 表格 / 列表 / 引用 / 链接
            //   之前走 Unicode 符号改造（标题变 ━━，表格变 │─┼─）是给 Label 准备的，
            //   错误地作用到了 WebView2 路径 → 导致标题/表格内容被破坏
            // ★ 2026-10-09 陛下要求：AI 思考内容（<think>...</think>）默认折叠、点击展开
            //   之前走 RemoveThinkTags 剥掉 → 历史会话永远看不到思考块
            //   修法：让 RenderBodyWithThinkingBlocks（在 RenderMarkdownAsHtml 里）处理
            //   跟流式定型 HTML 走同一条路径，history 和 streaming 看到一致折叠 UI
            //   （三处用同一段 RenderMarkdownAsHtml → 同样折叠块）
            displayContent = (displayContent ?? string.Empty).Trim();
        }
        else
        {
            displayContent = (displayContent ?? string.Empty).Trim();
        }

        // ★★★ Label 直接当气泡（AutoSize=true 100% 可靠）
        // 颜色：仅用户气泡用（AI/Error 改用 WebView2）
        Color bubbleTop, bubbleBottom, borderColor, textColor;
        if (isUser)
        {
            // 用户气泡：蓝色渐变
            bubbleTop = System.Drawing.Color.FromArgb(64, 169, 255);   // 亮蓝
            bubbleBottom = System.Drawing.Color.FromArgb(24, 144, 255);  // 深蓝
            borderColor = System.Drawing.Color.FromArgb(15, 110, 220);
            textColor = System.Drawing.Color.White;
        }
        else
        {
            // 占位（AI/Error 用 WebView2，不走 Label 路径）
            bubbleTop = bubbleBottom = borderColor = textColor = System.Drawing.Color.Black;
        }

        Control bubble;
        if (isUser)
        {
            // ★★★ 用户气泡：Label + Paint 自绘（已验证 100% 可靠）
            var labelBubble = new Label
            {
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(bubbleMaxW, int.MaxValue),
                BackColor = System.Drawing.Color.White,
            ForeColor = textColor,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
            Padding = new Padding(12, 10, 12, 10),
            Text = displayContent,
            Cursor = Cursors.IBeam,
            Margin = new Padding(0),
            // ★ 关联 ChatMessage，方便后续流式更新查找
            Tag = msg,
        };

        // ★ 关键：注册 OnPaintBackground 事件代替 Paint
        //   Label 默认 OnPaintBackground 填充矩形背景，然后 OnPaint 画文字
        //   我们的渐变必须画在 OnPaintBackground 阶段（文字之前）
        //   注：Label.OnPaintBackground 事件无公开订阅，需要继承或用 WndProc
        //   最简方案：直接重设 BackColor 为透明，然后 在 Paint 里画背景 + 文字
        //   Label 默认 OnPaint 后才调用 Paint 事件，所以我们订阅 Paint + 重画文字
        labelBubble.Paint += (sender, e) =>
        {
            if (sender is not Label lbl || lbl.Width <= 0 || lbl.Height <= 0) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // ★ Label 默认已画过背景和文字（在我们订阅之前），现在清除重画
            g.Clear(lbl.BackColor);  // 清除为透明

            // 1. 圆角矩形路径
            const int radius = 12;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(lbl.Width - d, 0, d, d, 270, 90);
            path.AddArc(lbl.Width - d, lbl.Height - d, d, d, 0, 90);
            path.AddArc(0, lbl.Height - d, d, d, 90, 90);
            path.CloseFigure();

            // 2. 线性渐变填充（顶亮 → 底暗）
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new System.Drawing.Point(0, 0),
                new System.Drawing.Point(0, lbl.Height),
                bubbleTop, bubbleBottom);
            g.FillPath(brush, path);

            // 3. 描边
            using var pen = new System.Drawing.Pen(borderColor, 1f);
            g.DrawPath(pen, path);

            // ★ 4. 重画文字（g.Clear 把默认文字也擦掉了）
            var textRect = new System.Drawing.Rectangle(
                lbl.Padding.Left, lbl.Padding.Top,
                lbl.Width - lbl.Padding.Horizontal,
                lbl.Height - lbl.Padding.Vertical);
            using var textBrush = new System.Drawing.SolidBrush(lbl.ForeColor);
            g.DrawString(lbl.Text, lbl.Font, textBrush, textRect,
                new System.Drawing.StringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Near,
                    LineAlignment = System.Drawing.StringAlignment.Near
                });

            // ★ 5. 裁剪到圆角路径（防止 g.Clear 范围超出圆角部分）
            g.SetClip(path);
            g.DrawRectangle(new System.Drawing.Pen(System.Drawing.Color.White, 0.5f),
                0, 0, lbl.Width - 1, lbl.Height - 1);
        };
        // 选中复制：Ctrl + 点击 复制到剪贴板（陛下 13:55 拍板方案 B）
        labelBubble.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (!ModifierKeys.HasFlag(Keys.Control)) return;
            if (string.IsNullOrEmpty(labelBubble.Text)) return;
            Clipboard.SetText(labelBubble.Text);
            // ★ 给个反馈：气泡闪一下（临时改变描边颜色 150ms）
            //   由于 BackColor 已设为透明 + Paint 里手画，修改 BackColor 无效
            //   这里直接订阅一次性 Paint 事件画亮色边框
            System.Action<System.Drawing.Color, System.Drawing.Color> flashOnce = null;
            flashOnce = (origTop, origBottom) =>
            {
                labelBubble.Paint += (s, pe) =>
                {
                    if (s is not Label lbl2 || lbl2.Width <= 0) return;
                    var g2 = pe.Graphics;
                    g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var p = new System.Drawing.Drawing2D.GraphicsPath();
                    const int r = 12;
                    int dd = r * 2;
                    p.AddArc(0, 0, dd, dd, 180, 90);
                    p.AddArc(lbl2.Width - dd, 0, dd, dd, 270, 90);
                    p.AddArc(lbl2.Width - dd, lbl2.Height - dd, dd, dd, 0, 90);
                    p.AddArc(0, lbl2.Height - dd, dd, dd, 90, 90);
                    p.CloseFigure();
                    // 闪色边框：亮黄
                    using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 200, 0), 3f);
                    g2.DrawPath(pen, p);
                };
                labelBubble.Invalidate();
                var t = new System.Windows.Forms.Timer { Interval = 150 };
                t.Tick += (_, _) =>
                {
                    labelBubble.Invalidate();  // 触发原 Paint 重画（背景画返回原色）
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            };
            flashOnce(bubbleTop, bubbleBottom);
        };
            bubble = labelBubble;
        }
        else
        {
            // ★★★ AI/Error 气泡：WebView2 + Markdown HTML
            //   高度由 HTML 里 ResizeObserver 推送真实高度
            var wv2 = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Width = bubbleMaxW,
                Height = 80,
                MinimumSize = new System.Drawing.Size(0, 0),  // ★ 不许有最小尺寸限制
                MaximumSize = new System.Drawing.Size(int.MaxValue, int.MaxValue),  // ★ 取消任何最大尺寸约束
                DefaultBackgroundColor = isError
                    ? System.Drawing.Color.FromArgb(255, 245, 245)
                    : System.Drawing.Color.FromArgb(244, 248, 251),
                Tag = msg,
                Margin = new Padding(0),
                ZoomFactor = 1.0,
            };
            // ★★★ 2026-09-02 陛下反馈：鼠标在气泡内滚轮无法滚动聊天列表
            //   原因：WebView2 默认把 wheel 事件吞进浏览器内部，WinForms 控件收不到
            //   正确修法：HTML 里 JS 监听 wheel → postMessage({type:'wheel',delta}) → C# 滚动父容器
            //   ★ 不要订阅 wv2.MouseWheel（WebView2 是 native 子窗口，WinForms 收不到）
            // ★ 订阅 wv2.SizeChanged：ResizeObserver 推高度 → wv2.Height 变 → row 重算
            wv2.SizeChanged += (_, _) =>
            {
                var row = wv2.Parent as Panel;
                if (row == null) return;
                Label? avatarInRow = null;
                foreach (Control c in row.Controls)
                {
                    if (c is Label al && !ReferenceEquals(c, wv2))
                    {
                        avatarInRow = al;
                        break;
                    }
                }
                LayoutBubbleRow(row, wv2, avatarInRow, isUser);
                // RelayoutMessages();  // ★ 2026-08-31 流式时不重排避免闪烁
            };
            // ★ 2026-10-07 用系统真实 DPI 换算（不猜倍数、不校准）
            //   JS 报 scrollHeight（纯 CSS 像素），C# 用 DeviceDpi/96 换算成 device pixel
            //   之前校准失败是因为 innerHeight 在 viewport 变化前就测了，导致比例偏大
            //   DeviceDpi 是系统报告的真实 DPI，不是猜的
            wv2.CoreWebView2InitializationCompleted += (s, e) =>
            {
                if (!e.IsSuccess || wv2.CoreWebView2 == null) return;
                // ★★★ 2026-10-07 根治 DPI 空白：修正 BoundsMode 语义
                ConfigureWebView2Dpi(wv2);
                wv2.CoreWebView2.WebMessageReceived += (s2, e2) =>
                {
                    try
                    {
                        var json = e2.TryGetWebMessageAsString();
                        if (string.IsNullOrEmpty(json)) return;
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        // ★★★ 2026-09-02 滚轮事件分流：{type:'wheel', delta:...}
                        //   转发为父容器 pnlMessagesScroll 滚动，不再走 ResizeObserver 高度计算
                        if (doc.RootElement.TryGetProperty("type", out var typeProp) &&
                            typeProp.GetString() == "wheel")
                        {
                            // ★★★ 2026-09-02 陛下反馈：方向反向 + 粒度过小
                            //   浏览器 wheel 语义: deltaY > 0 = 内容向下(看下面) = Value 变大
                            //   WinForms MouseEventArgs 语义: Delta > 0 = Value 变小 (翻转过)
                            //   之前用 Value - delta 是 WinForms 默认语义，浏览器 deltaY 是另一个语义 → 反向
                            //   正确公式: newValue = oldValue + delta  (delta 不除，每次 120px 滚 120 像素，跟空白处 WinForms 默认一致)
                            int delta = doc.RootElement.TryGetProperty("delta", out var dp) ? dp.GetInt32() : 0;
                            int newY = pnlMessagesScroll.VerticalScroll.Value + delta;
                            if (newY < pnlMessagesScroll.VerticalScroll.Minimum) newY = pnlMessagesScroll.VerticalScroll.Minimum;
                            if (newY > pnlMessagesScroll.VerticalScroll.Maximum) newY = pnlMessagesScroll.VerticalScroll.Maximum;
                            if (newY != pnlMessagesScroll.VerticalScroll.Value)
                            {
                                pnlMessagesScroll.VerticalScroll.Value = newY;
                                pnlMessagesScroll.PerformLayout();
                            }
                            return;
                        }
                        // ★ 2026-10-09 修陛下反馈「流式输出气泡左右上下晃动」
                        //   流式期锁 wv2.Height → C# 不动 wv2 高度 → FlowLayoutPanel 不重排 → 气泡不晃
                        //   wheel 事件不走这条路径（上面已 return），不影响
                        //   只跳过 height 同步；其他 wv2 的 height 同步仍正常
                        if (ReferenceEquals(wv2, _streamingWebView2)) return;
                        // ★ 2026-10-17 v2.11 历史会话加载期: 只 buffer 高度, 不改 wv2.Height 避免 reflow
                        if (_isLoadingHistory)
                        {
                            if (doc.RootElement.TryGetProperty("h", out var hPropHist))
                            {
                                int realHh = hPropHist.GetInt32();
                                if (realHh > 0)
                                {
                                    int hBodyH = doc.RootElement.TryGetProperty("h_body", out var hbH) ? hbH.GetInt32() : 0;
                                    int hVisH = doc.RootElement.TryGetProperty("h_visible", out var hvH) ? hvH.GetInt32() : 0;
                                    int contentHh = hVisH > 0 ? hVisH : (hBodyH > 0 ? hBodyH : realHh);
                                    _pendingHistoryHeights[wv2] = contentHh + 10;
                                }
                            }
                            return;
                        }
                        if (!doc.RootElement.TryGetProperty("h", out var hProp)) return;
                        int realH = hProp.GetInt32();
                        if (realH <= 0) return;
                        // ★★★ 2026-10-07 陛下反馈「内容总高度有问题」log 诊断确认：
                        //   h_visible=12375, h_body=12367, h_scroll=12374
                        //   → h_visible 比 h_body 多 8px 是最后一个<p>的 margin-bottom 撑出 overflow:hidden
                        //   → h_body 才是用户肉眼真正看到的高度
                        //   → h_scroll=12374 是 scrollHeight（含内容但不含 margin collapse 溢出）
                        //   现在用 h_body 作为 newH 依据（避开 margin collapse 污染）
                        int hScroll = doc.RootElement.TryGetProperty("h_scroll", out var hsProp) ? hsProp.GetInt32() : 0;
                        int hVisible = doc.RootElement.TryGetProperty("h_visible", out var hvProp) ? hvProp.GetInt32() : 0;
                        int hBody = doc.RootElement.TryGetProperty("h_body", out var hbProp) ? hbProp.GetInt32() : 0;
                        int hHtml = doc.RootElement.TryGetProperty("h_html", out var hhProp) ? hhProp.GetInt32() : 0;
                        // ★ 2026-10-13 陛下要求：调试期 WebView2Debug.log 全部清理
                        //   - 之前为「内容总高度」「Mode B 不生效」加的诊断写入
                        //   - hScroll/hVisible/hBody/hHtml 仍保留局部变量（被下方 contentH 兜底用）
                        // ★★★ 2026-10-08 终极修法（同步 Resize 钩子强制 Mode B 后 vpH = wv2.Height）：
                        //   Mode B 下 vpH = Bounds.H = wv2.Height（CSS 像素解释），
                        //   所以 wv2.Height = contentH 即可让内容完整填满气泡，不需 ×DPI/96。
                        //   + 10px buffer 吃掉 wrapper 边缘 / 行高舍入
                        int contentH = hVisible > 0 ? hVisible : (hBody > 0 ? hBody : realH);
                        int newH = contentH + 10;
                        wv2.Height = newH;
                        // ★ BackColor 跟气泡背景色同色，掩盖 WinForms WebView2 HWND > viewport 的底部空白
                        try { wv2.BackColor = wv2.DefaultBackgroundColor; } catch { }
                        // ★★★ 2026-10-07 修陛下反馈「历史会话底部差很多内容」：
                        //   之前的 SmartScrollToBottom 用 (newMax - curVal) <= 100 判断
                        //   错：wv2 异步长大后 newMax 暴涨，curVal 还在老位置 → 差值巨大 → 误判"用户不在底部" → 不滚
                        //   修法：增长 wv2 前先记录用户是否在老底部，涨完再决定是否跟滚
                        int _oldMax = pnlMessagesScroll.VerticalScroll.Maximum;
                        int _oldCur = pnlMessagesScroll.VerticalScroll.Value;
                        int _visibleH = pnlMessagesScroll.ClientSize.Height;
                        bool _userWasAtBottom = (_oldMax > 0) && (_oldCur + _visibleH >= _oldMax - 50);
                        wv2.Height = newH;
                        var row = wv2.Parent as Panel;
                        if (row != null)
                        {
                            Label? avatarInRow = null;
                            foreach (Control c in row.Controls)
                            {
                                if (c is Label al && !ReferenceEquals(c, wv2))
                                {
                                    avatarInRow = al;
                                    break;
                                }
                            }
                            LayoutBubbleRow(row, wv2, avatarInRow, isUser);
                        }
                        RelayoutMessages();
                        // ★★★ 2026-10-07 手动跟滚新底部：wv2 增长后跟滚新底部
                        //   （SmartScrollToBottom 的判断在 wv2 长大后会失准）
                        if (_userWasAtBottom)
                        {
                            int _newMax = pnlMessagesScroll.VerticalScroll.Maximum;
                            int _newCur = _newMax - _visibleH;
                            if (_newCur < 0) _newCur = 0;
                            if (_newCur > pnlMessagesScroll.VerticalScroll.Maximum) _newCur = pnlMessagesScroll.VerticalScroll.Maximum;
                            if (_newCur != _oldCur)
                            {
                                pnlMessagesScroll.VerticalScroll.Value = _newCur;
                            }
                        }
                    }
                    catch
                    {
                        // ★ 2026-09-02 删除 ResizeObserver 调试日志
                    }
                };
                wv2.CoreWebView2.NavigateToString(RenderMarkdownAsHtml(displayContent, BuildToolCardsHtml(msg.ToolCalls)));
            };
_ = wv2.EnsureCoreWebView2Async();
            bubble = wv2;
        }

        // ★ 头像 Label（AI/Error/User 全部显示，用户头像 = "B" 蓝色）
        Label? avatar = null;
        if (!isSystem)
        {
            avatar = new Label
            {
                AutoSize = true,
                Text = isUser ? "B" : (isError ? "!" : "AI"),
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                ForeColor = isUser ? System.Drawing.Color.FromArgb(24, 144, 255) : (isError ? System.Drawing.Color.FromArgb(180, 30, 30) : System.Drawing.Color.Gray),
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Margin = new Padding(0),
            };
        }

        // ★ row = Panel，锁定宽度，按内容高度撑开，AI/用户靠左/靠右
        const int gap = 10;
        var row = new Panel
        {
            Width = flpMessages.ClientSize.Width,  // ★ 已含 flp.Padding.Right=17 预留滚动条位置
            BackColor = System.Drawing.Color.Transparent,
            Margin = new Padding(0, 0, 0, 14),  // 行间距
        };
        // ★ 布局：先把 bubble + avatar 加进 row，row.Layout() 后 PreferredSize 才准确
        if (avatar != null) row.Controls.Add(avatar);
        row.Controls.Add(bubble);
        row.PerformLayout();  // 强制 layout，让 PreferredSize 准

        // ★ 手算布局：bubble 位置 + row 高度（抽出为 LayoutBubbleRow 方法）
        //   AI 消息：[avatar (auto)][gap][bubble (auto, 左)]
        //   用户消息：                    [bubble (auto, 右)]
        // ★ 2026-08-29 修复「流式输出只显示一行」：
        //   row.SizeChanged 只在 Size 实际变化时触发，bubble.Text 变化不会触发
        //   所以 StreamDisplayAsync 里直接调用 LayoutBubbleRow() 重算布局
        row.SizeChanged += (_, _) => LayoutBubbleRow(row, bubble, avatar, isUser);
        // 立即触发一次 SizeChanged
        row.Size = new System.Drawing.Size(row.Width, 1);

        // ★ 诊断日志（2026-09-02 删除）

        return row;
    }

    /// <summary>
    /// ★ 2026-08-29 抽出的布局方法：重新计算 row 高度、头像位置、气泡位置
    ///   流式输出时 bubble.Text 变化不会触发 row.SizeChanged，需要手动调用
    /// </summary>
    private void RelayoutMessages()
    {
        if (!flpMessages.IsHandleCreated) return;
        // 同步 flpMessages 宽度跟滚动容器
        flpMessages.Width = pnlMessagesScroll.ClientSize.Width;
        // 手算 row Y 位置 + 总高度
        int y = flpMessages.Padding.Top;
        foreach (Control c in flpMessages.Controls)
        {
            if (c is not Panel row) continue;
            row.Width = flpMessages.ClientSize.Width;
            row.Location = new System.Drawing.Point(0, y);
            y += row.Height + row.Margin.Bottom + row.Margin.Top;
        }
        int newH = y + flpMessages.Padding.Bottom + flpMessages.Padding.Top;
        if (flpMessages.Height != newH) flpMessages.Height = newH;
        pnlMessagesScroll.AutoScrollMinSize = new System.Drawing.Size(flpMessages.Width, newH);
    }

    /// <summary>
    /// ★★★ 2026-09-02 陛下反馈：窗体变化布局不跟着变
    ///   flpMessages.Dock=None 是手动控制，Designer 里没设 Anchor
    ///   Resize 时需手动同步：flp 宽度、每行宽度、row Y 位置、象头位置、气泡宽
    ///   ★ WebView2.Width / Label.MaximumSize 创建时设了固定值，resize 后需重设才能跟着变大变小
    /// </summary>
    private void OnContainerResized()
    {
        if (!IsHandleCreated || !pnlMessagesScroll.IsHandleCreated) return;
        // ★★★ 重算每个气泡的宽度 = 当前 flp 宽度的 85%（与 CreateBubbleRow 保持一致）
        int flpClientW = Math.Max(400, flpMessages.ClientSize.Width - flpMessages.Padding.Horizontal);
        const int sidePadding = 24;
        int bubbleMaxW = (int)((flpClientW - sidePadding * 2) * 0.85);
        foreach (Control rowCtl in flpMessages.Controls)
        {
            if (rowCtl is not Panel row) continue;
            foreach (Control c in row.Controls)
            {
                if (c is Microsoft.Web.WebView2.WinForms.WebView2 wv2)
                {
                    // ★ WebView2 resize 后重新设宽度
                    if (wv2.Width != bubbleMaxW) wv2.Width = bubbleMaxW;
                }
                else if (c is Label lbl && lbl.Tag is ChatMessage m && m.Role == ChatRole.User)
                {
                    // ★ Label 用户气泡：重设 MaximumSize.Width 让 Text 重新换行
                    var maxSize = lbl.MaximumSize;
                    if (maxSize.Width != bubbleMaxW)
                        lbl.MaximumSize = new System.Drawing.Size(bubbleMaxW, int.MaxValue);
                }
            }
        }
        RelayoutMessages();
        // ★ 2026-09-30 修陛下反馈「流试输出不跟滚」：流式上下文直接 ScrollToBottom (内部 retry 等 WebView2 撑开)
        //   SmartScrollToBottom 保留给非流式场景 (LoadOlder / WebView2 ResizeObserver 等), 避免拽用户
        ScrollToBottom();
    }

    /// <summary>
    /// ★★★ 2026-09-02 陛下反馈：窗体变化布局不跟着变 / 发送按钮右边空了很大部分
    ///   pnlInput 里 btnSend/btnCancel 都是 Location 固定，没设 Anchor
    ///   txtInput 设了 Anchor=Top|Bottom|Left|Right，会撑满整个右边、覆盖到按钮
    ///   Resize 时手动重新定位
    ///   ★★★ 关键：btnCancel 默认 Visible=false（没在发送时隐藏）
    ///     如果 btnSend 贴在 btnCancel 左边，而 cancel 隐藏 → btnSend 右边会空出一大块
    ///     所以要按 btnCancel.Visible 分支处理：
    ///       - 可见时：btnCancel 贴右、btnSend 紧贴 btnCancel 左
    ///       - 隐藏时：btnSend 直接贴右
    /// ★ lblStatus 不在 pnlInput 里（在 pnlSidebar 里），不要这里管
    /// </summary>
    private void LayoutInputPanel()
    {
        if (!pnlInput.IsHandleCreated) return;
        int clientW = pnlInput.ClientSize.Width;
        int clientH = pnlInput.ClientSize.Height;
        if (clientW <= 0 || clientH <= 0) return;
        const int gap = 10;
        if (btnCancel.Visible)
        {
            // ★ btnCancel 贴右
            btnCancel.Location = new System.Drawing.Point(
                clientW - btnCancel.Width - pnlInput.Padding.Right,
                pnlInput.Padding.Top);
            // ★ btnSend 紧贴 btnCancel 左
            btnSend.Location = new System.Drawing.Point(
                btnCancel.Left - btnSend.Width - gap,
                pnlInput.Padding.Top);
        }
        else
        {
            // ★ btnCancel 隐藏：btnSend 直接贴右
            btnSend.Location = new System.Drawing.Point(
                clientW - btnSend.Width - pnlInput.Padding.Right,
                pnlInput.Padding.Top);
        }
        // ★★★ 关键修复：txtInput 的 Right anchor 会撑满整个右边，覆盖到按钮
        //   手动把 txtInput 右收到 btnSend.Left - gap
        int txtNewRight = btnSend.Left - gap;
        int txtNewW = txtNewRight - txtInput.Left;
        if (txtNewW > 50 && txtInput.Width != txtNewW)
        {
            txtInput.Width = txtNewW;
        }
    }

    /// <summary>
    /// ★★★ 2026-09-02 陛下反馈：btnDeleteSession/btnClearAll/lblStatus/lstSessions 不跟着 resize
    ///   全是 Location 固定，没设 Dock/Anchor
    ///   Resize 时手动重新定位：
    ///     lblSidebarTitle: 顶部 (Dock=Top 会被 Designer 重置，不动)
    ///     lstSessions: 中部 Fill
    ///     btnDeleteSession: 底部左
    ///     btnClearAll: 底部右
    ///     lblStatus: 在按钮下方贴底左
    /// </summary>
    private void LayoutSidebar()
    {
        if (!pnlSidebar.IsHandleCreated) return;
        int clientW = pnlSidebar.ClientSize.Width;
        int clientH = pnlSidebar.ClientSize.Height;
        if (clientW <= 0 || clientH <= 0) return;

        int pad = pnlSidebar.Padding.Left;       // 15
        int padTop = pnlSidebar.Padding.Top;     // 15
        int padR = pnlSidebar.Padding.Right;     // 15
        int padB = pnlSidebar.Padding.Bottom;    // 15
        int titleH = 50;                         // 会话列表标题 + 一点间距
        int btnH = btnDeleteSession.Height;      // 52
        int statusH = lblStatus.Height;          // 状态栏高度

        // ★ lstSessions 撑满中部：title 下 + 按钮组上方
        int btnGroupH = btnH + statusH + 10;     // 按钮 + 状态 + 间距
        lstSessions.Location = new System.Drawing.Point(pad, padTop + titleH);
        lstSessions.Size = new System.Drawing.Size(
            clientW - pad - padR,
            clientH - padTop - titleH - btnGroupH - padB);

        // ★ 按钮贴底
        int btnY = clientH - btnH - statusH - 10 - padB;
        btnDeleteSession.Location = new System.Drawing.Point(pad, btnY);
        btnClearAll.Location = new System.Drawing.Point(
            clientW - padR - btnClearAll.Width,
            btnY);

        // ★ lblStatus 在按钮下方
        lblStatus.Location = new System.Drawing.Point(
            pad,
            clientH - statusH - padB);
    }    private void LayoutBubbleRow(Panel row, Control bubble, Label? avatar, bool isUser)
    {
        const int sidePadding = 24;
        const int gap = 10;
        // 强制 bubble 立即重新布局（让 PreferredSize 更新）
        bubble.PerformLayout();
        // ★ 用 bubble.Width/Height（实际渲染尺寸）而不是 PreferredSize
        int avatarW = avatar?.Width ?? 0;
        int avatarH = avatar?.Height ?? 0;
        int bubbleW = bubble.Width;
        int bubbleH = bubble.Height;

        // row 高度 = max(avatarH, bubbleH)
        int rowH = Math.Max(avatarH, bubbleH);
        row.Height = rowH;

        // 头像位置
        int avatarX;
        int avatarY = rowH / 2 - avatarH / 2;
        if (isUser)
        {
            // 用户头像靠右：预留 17px 滚动条位置 + sidePadding + 10
            avatarX = flpMessages.ClientSize.Width - avatarW - sidePadding - 17 - 10;
        }
        else
        {
            // AI/Error 头像靠左
            avatarX = sidePadding;
        }
        if (avatar != null) avatar.Location = new System.Drawing.Point(avatarX, avatarY);

        // 气泡位置
        int bubbleX;
        if (isUser)
        {
            // 用户气泡靠右：在头像左边（头像 - gap - 气泡宽），加 8px 缓冲
            bubbleX = Math.Max(sidePadding, avatarX - gap - bubbleW - 8);
        }
        else
        {
            // AI 气泡靠左：头像 + gap
            bubbleX = avatarX + avatarW + gap;
        }
        int bubbleY = rowH / 2 - bubbleH / 2;
        bubble.Location = new System.Drawing.Point(bubbleX, bubbleY);
    }

    /// <summary>
    /// ★ 2026-08-29 方案 A 简化版：在 flpMessages 末尾追加一个 row
    /// ★ 2026-09-30 虚拟列表: 维持窗口上限 MaxWindowSize
    ///   - 用户发新消息 / AI 开始流式 → _currentSession.Messages.Add + 这里 append
    ///   - 若总气泡数(含 sentinel) > MaxWindowSize 且 _visibleStartIndex > 0
    ///     → 移除最顶部 sentinel (索引 0) + 移除最早气泡 (原索引 1, 现 0),
    ///       _visibleStartIndex += 1, 让窗口从末尾掉一条出去
    ///   - 若 _visibleStartIndex == 0 (即历史已全部渲染) → 不再移除, 突破上限
    /// </summary>
    private void AddMessageBubbleNew(ChatMessage msg)
    {
        if (_currentSession == null) return;
        var row = CreateBubbleRow(msg, source: "AddNew");
        flpMessages.SuspendLayout();
        flpMessages.Controls.Add(row);

        // 窗口维护: 移除顶部 sentinel + 最早气泡, 维持总条目数 <= MaxWindowSize
        while (flpMessages.Controls.Count > MaxWindowSize && _visibleStartIndex > 0)
        {
            // 索引 0 一定是 sentinel (LoadOlder 阶段已确保); 索引 1 是最早气泡
            if (flpMessages.Controls.Count >= 1 && flpMessages.Controls[0].Tag is SentinelLoadMore)
            {
                flpMessages.Controls.RemoveAt(0);
            }
            if (flpMessages.Controls.Count >= 1 && !(flpMessages.Controls[0].Tag is SentinelLoadMore))
            {
                flpMessages.Controls.RemoveAt(0);
                _visibleStartIndex++;
            }
            else
            {
                break;  // safety
            }
        }
        flpMessages.ResumeLayout(true);
    }

    private TextBox RenderMarkdownTextBox_Old(string markdown, bool isError, int labelMaxW)
    {
        return null;  // 已被新方案替代，保留空实现
    }
    private static TextBox CreateSelectableTextBox(string text, System.Drawing.Font font, System.Drawing.Color foreColor, System.Drawing.Color backColor, int maxW)
    {
        // ★ 2026-08-28 终极修法 关键点：把 text 中的换行符都替换为空格
        //   原因：Graphics.MeasureString 按 maxW WordBreak 测算高度；TextBox 遇到 \n 强制换行 + 再 WordBreak
        //   → 测量高度 < 实际占用高度 → TextBox MinimumSize 撑爆 bubble → 出现空白
        //   解决方案：去掉 \n，让 TextBox 完全依赖 Width + WordWrap 走，跟 Graphics 测量一致
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            text ?? string.Empty, @"[\r\n]+", " ");

        // ★ 2026-08-28 用 Graphics.MeasureString 真实测量，避开 TextRenderer 中文测高 bug
        int w, h;
        using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
        {
            var measured = ControlExtensions.MeasureTextRobust(g, normalized, font, maxW);
            // TextBox 内部边框约 2~3px，左右上下各加 4 保守值（避免被裁字）
            w = Math.Min(maxW, measured.Width + 4);
            h = measured.Height + 4;
        }

        var tb = new TextBox
        {
            Text = normalized,
            Font = font,
            ForeColor = foreColor,
            BackColor = backColor,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            ScrollBars = ScrollBars.None,
            Cursor = Cursors.IBeam,
            TabStop = false,
            // ★ 不设 MinimumSize / MaximumSize：让 TextBox 尺寸完全由 Graphics 测量决定
            //   前版 MinimumSize = (maxW, measured.Height+4) 当 \n 存在时会让 measured.Height 偏小
            //   但 TextBox 实际占用偏大 → MinimumSize 反而成了"撑高"工具，导致底部空白
            Width = w,
            Height = h,
        };
        // ★ 2026-08-29 诊断：TextBox 设 Size 后 PreferredSize 是什么？
        //   陛下反馈修复 #3 完全没生效 → 推测 TextBox 自动撑高到实际渲染高度，忽略我设的 Height
        // ★ 诊断日志（2026-09-02 删除）
        _ = tb.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));        return tb;
    }

    private static string DecodeHtmlEntities(string s)
    {
        return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&")
                .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
    }

    /// <summary>
    /// ★ 2026-09-30 虚拟列表 Scroll 事件
    ///   用户滚到顶 (VerticalScroll.Value == 0) 且还有更早消息 → LoadOlderMessages
    ///   防抖: _isLoadingOlder 防止短时间内多次触发
    /// </summary>
    private System.Windows.Forms.Timer? _scrollPollTimer;
    private int _lastSeenScrollValue = -1;

    /// <summary>
    /// ★ Bug fix 2026-09-30 v3: Scroll 事件 + e.NewValue 不可靠 (用户报告多次滚动不触发)
    ///   改用 Application.Idle 轮询 VerticalScroll.Value: 简单、可靠、不依赖 ScrollEventArgs 语义
    ///   每次 Idle 检查: 若 Value 跌到 <= 5 且之前 > 5 → 触发 LoadOlder
    ///   用 _lastSeenScrollValue 记录上次, 只在"刚滚到顶"时触发一次 (不会重复上/滚)
    /// </summary>
    private void PnlMessagesScroll_Scroll(object? sender, ScrollEventArgs e)
    {
        // ★ v4: 加日志, 验证 Scroll 事件到底触不触发 (之前怀疑根本不触发, 现在看证据)
        DebugLog($"Scroll event fired: Type={e.Type} Orient={e.ScrollOrientation} NewValue={e.NewValue}");
        EnsureScrollPollTimer();  // 兜底 (正常情况 Timer 早就启动了)
    }

    private void EnsureScrollPollTimer()
    {
        if (_scrollPollTimer != null) return;
        _scrollPollTimer = new System.Windows.Forms.Timer { Interval = 80 };  // 12fps 足够
        _scrollPollTimer.Tick += ScrollPollTimer_Tick;
        _scrollPollTimer.Start();
        DebugLog("ensureScrollPollTimer started");
    }

    private void ScrollPollTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (_isLoadingOlder) return;
            if (_visibleStartIndex <= 0) return;
            if (_currentSession == null || _currentSession.Messages.Count == 0) return;
            if (!pnlMessagesScroll.IsHandleCreated) return;

            int curVal = pnlMessagesScroll.VerticalScroll.Value;
            bool visible = pnlMessagesScroll.VerticalScroll.Visible;
            int maxV = pnlMessagesScroll.VerticalScroll.Maximum;

            // 只在"刚跌到顶"时触发 (curVal <= 5 且上次 > 5)
            if (curVal <= 5 && _lastSeenScrollValue > 5)
            {
                DebugLog($"poll tick: curVal={curVal} lastSeen={_lastSeenScrollValue} max={maxV} visible={visible} -> TRIGGER LoadOlder (vStart={_visibleStartIndex} total={_currentSession.Messages.Count})");
                LoadOlderMessages(LoadMoreBatchSize);
            }
            _lastSeenScrollValue = curVal;
        }
        catch (Exception ex)
        {
            DebugLog($"ScrollPollTimer_Tick EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        }
    }



    /// <summary>
    /// ★ 2026-09-30 v11 陛下提示根因: "刷新气泡就定位到最底部"
    ///   之前 WebView2 ResizeObserver / OnContainerResized 等多处无条件 ScrollToBottom
    ///   不管用户在不在底部都跳, 导致用户在历史中被拽回底部
    ///   修法: 只在用户已经在底部 (100px 内) 才滚, 不在就不动
    ///   - 流式输出: 用户在底部 → 跟着滚 ✓
    ///   - 首次加载: RenderMessages 后用户在底部 → 跟着滚 ✓
    ///   - 加载历史: 用户在顶部 (看老消息) → 不动 ✓
    /// ★ 2026-10-07 增强: 加 _streamFreezeUntil 冻结机制
    ///   AI 流式完后 WebView2 还会有 N 多次 ResizeObserver 延迟触发
    /// (代码块字体加载、表格交宽调整、ScrollToBottom 内部又 Timer 调 8 次重试)
    /// 之前总问牌频发拽回底部 → 陛下手不能向上滚 -> 冻结 5 秒期间所有调送到 队底都不滚
    /// </summary>
    private DateTime _streamFreezeUntil = DateTime.MinValue;
    // ★ 2026-10-07 wv2.Height 赋值节流：JS 端每帧都报高度，C# 端最快 150ms 应用一次
    //  防止流式输出时 wv2 反复撑缩让 WebView2 反复进入低 DPI 重渲染（陛下反馈「屏幕闪烁一会模糊一会清晰」的根因）
    private DateTime _lastHeightApply = DateTime.MinValue;
    // ★ 2026-10-09 修陛下反馈「流式输出气泡左右上下晃动」
    //   根因：每 500ms JS notify → C# ResizeObserver 设 wv2.Height → FlowLayoutPanel 重排 → 后续气泡位置变 → 上下晃
    //   修法：流式期锁定 wv2.Height，HTML 内容超出 wv2 时 wv2 内部 overflow:auto 滚动
    //   流式结束 setBubbleBodyOnly 后才做 final height sync
    //   仿 ChatGPT/Claude 网页 chat 行为：气泡不晃，只是不停吐字
    private Microsoft.Web.WebView2.WinForms.WebView2? _streamingWebView2 = null;

    // ★ 2026-10-17 v2.11 历史会话加载期: 缓存 wv2 目标高度不直接应用, 避免 FlowLayoutPanel 反复 reflow 导致上方气泡抖动
    //   症状: 加载历史时最后几条渲染好了, 之前部分一直抖
    //   修法: 加载期 ResizeObserver 只 buffer 高度, settle Timer 一次性应用 + 1 次 reflow
    private bool _isLoadingHistory = false;
    private readonly Dictionary<Microsoft.Web.WebView2.WinForms.WebView2, int> _pendingHistoryHeights = new();
    private System.Windows.Forms.Timer? _historySettleTimer = null;
    private void SmartScrollToBottom()
    {
        if (!pnlMessagesScroll.IsHandleCreated) return;
        // ★ 2026-10-07 冻结机制: AI 流式后总调送到 的 ResizeObserver 闪烁事件被冻住
        if (DateTime.UtcNow < _streamFreezeUntil) return;
        int curVal = pnlMessagesScroll.VerticalScroll.Value;
        int maxVal = pnlMessagesScroll.VerticalScroll.Maximum;
        // 用户在底部 100px 内 → 跟着滚 (流式体验)
        bool userAtBottom = (maxVal - curVal) <= 100;
        if (userAtBottom)
        {
            ScrollToBottom();
        }
        // else: 用户在历史中, 不动 (历史浏览体验)
    }

    /// <summary>
    /// ★ 2026-09-30: 诊断阶段使用, 修复完成后改为空实现 (no-op)
    ///   - 调用点全部保留 (修复过程中加的诊断点), 编译通过
    ///   - 方法体为空, 不再写日志文件, 性能无开销
    ///   - 后续真要加调试日志, 把方法体填回去就行
    /// </summary>
    private static void DebugLog(string msg) { }

    private void ScrollToBottom()
    {
        // ★★★ 2026-09-02 陛下反馈：打开会话/重新渲染后滚不到最底部
        //   原因：AI 气泡是 WebView2，HTML 渲染 + ResizeObserver 推送高度是异步的
        //   ScrollToBottom 同步调用时 VerticalScroll.Maximum 还是旧值（WebView2 未撑开）
        //   修法：
        //   1) 先同步试一次，立即滚
        //   2) 接着分多次 0/50/150/300/600ms 重试，覆盖 WebView2 init + Render + ResizeObserver 回调
        //   3) 最后挂 Application.Idle，捕获布局收尾的一次
        //   4) 每次重试前都 PerformLayout + Refresh
        if (!pnlMessagesScroll.IsHandleCreated) return;

        void DoScroll()
        {
            // ★ 2026-10-09 修陛下反馈「对话刚完成还是无法向上滑动」
            //   根因：ScrollToBottom 内部挂 8 次 50ms 重试 Timer 覆盖 WebView2 撑开延迟
            //        流式期 500ms 节流下，每 chunk 启 1 个 Timer，N 个 Timer 互相叠加
            //        流式结束 L1527 设的 _streamFreezeUntil=+5s 只保护 SmartScrollToBottom()
            //        旧 Timer 仍在 5s 内反复 Value=max → 拽回底部
            //   修法：DoScroll() 也走冻结，5s 冻结期内所有重试 no-op
            if (DateTime.UtcNow < _streamFreezeUntil) return;
            try
            {
                pnlMessagesScroll.SuspendLayout();
                pnlMessagesScroll.PerformLayout();
                flpMessages.PerformLayout();
                int max = pnlMessagesScroll.VerticalScroll.Maximum;
                if (max > 0)
                {
                    // ★ 每次赋值前重新读 max：Refresh() 后 Maximum 可能会变（WebView2 扩张后）
                    //   之前用缓存的 max，第二次 Value=max 可能越界报 ArgumentOutOfRangeException
                    var vs = pnlMessagesScroll.VerticalScroll;
                    vs.Value = Math.Min(max, vs.Maximum);
                    pnlMessagesScroll.Refresh();
                    max = pnlMessagesScroll.VerticalScroll.Maximum;
                    if (max > 0 && pnlMessagesScroll.VerticalScroll.Value != max)
                        pnlMessagesScroll.VerticalScroll.Value = Math.Min(max, pnlMessagesScroll.VerticalScroll.Maximum);
                }
            }
            finally
            {
                pnlMessagesScroll.ResumeLayout(true);
            }
        }

        pnlMessagesScroll.InvokeIfNeeded(() =>
        {
            DoScroll();
            // ★ 异步重试：覆盖 WebView2 init（~50ms） + Render + ResizeObserver（~50-200ms）
            var t = new System.Windows.Forms.Timer { Interval = 50 };
            int tries = 0;
            t.Tick += (_, _) =>
            {
                DoScroll();
                tries++;
                if (tries >= 8) // 400ms
                {
                    t.Stop();
                    t.Dispose();
                }
            };
            t.Start();
        });
    }

    // ========== 发送消息 ==========

    // ★ 2026-10-17 v2.10: ESC 关闭窗体 (KeyPreview=true 才能捕获, 控件先收到 KeyDown 之前 Form 收到)
    private void AiChatForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            this.Close();
        }
    }

    private void TxtInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            BtnSend_Click(sender, e);
        }
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        if (_isSending) return;
        var text = txtInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (_currentProvider == null)
        {
            MessageBox.Show("未配置 AI 厂商，请点设置添加厂商和 API Key", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrEmpty(_currentProvider.ApiKey))
        {
            MessageBox.Show($"厂商「{_currentProvider.Name}」未配置 API Key，请点设置", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 添加用户消息
        var userMsg = new ChatMessage
        {
            SessionId = _currentSession.Id,
            Role = ChatRole.User,
            Content = text
        };
        _currentSession.Messages.Add(userMsg);
        if (_currentSession.Title == "新会话" || string.IsNullOrEmpty(_currentSession.Title))
        {
            _currentSession.Title = text.Length > 30 ? text.Substring(0, 30) + "..." : text;
        }
        _currentSession.LastActiveAtUtc = DateTime.UtcNow;
        _currentSession.ProviderId = _currentProvider.Id;
        _currentSession.Model = _currentProvider.Model;
        _store.SaveSession(_currentSession);

        AddMessageBubbleNew(userMsg);
        txtInput.Clear();
        ScrollToBottom();
        RefreshSessions();

        // 切到「思考中」状态
        _isSending = true;
        btnSend.Enabled = false;
        btnCancel.Visible = true;
        lblStatus.Text = "AI 思考中...";
        lblStatus.ForeColor = System.Drawing.Color.FromArgb(24, 144, 255);
        // ★ 2026-10-07 清除上一轮冻结：新对话开始 → 允许自动滚跟随流式输出
        _streamFreezeUntil = DateTime.MinValue;

        _cts = new CancellationTokenSource();

        var placeholderAi = new ChatMessage
        {
            SessionId = _currentSession.Id,
            Role = ChatRole.Assistant,
            Content = "思考中..."
        };
        _currentSession.Messages.Add(placeholderAi);
        AddMessageBubbleNew(placeholderAi);
        ScrollToBottom();

        var toolCallLog = new List<ChatToolCallRecord>();
        _pendingToolCallLog = toolCallLog;

        try
        {
            // ===== 第一阶段：带 tools schema，看 AI 是否需要调工具 =====
            var messagesForApi = BuildApiMessages(_currentSession.Messages.Take(_currentSession.Messages.Count - 1).ToList());

            // 回调：AI 调用了工具后显示中间状态
            Func<AiActionResult, Task<bool>> onToolExecuted = async (result) =>
            {
                if (!result.Success) return true;
                string summary = result.Summary;
                await pnlMessagesScroll.InvokeAsync(() =>
                {
                    // ★ 2026-09-20 陛下反馈「调用工具时整个对话框闪烁刷新」
                    //   根因：之前 RenderMessages() 会 Controls.Clear() + 重建所有气泡，
                    //         AI 调一次工具就重建 N 个 WebView2 + NavigateToString 重新初始化 → 整屏闪
                    //   原修法：只 FindBubbleForStream 占位气泡 + ExecuteScriptAsync 重设 innerHTML
                    //         但重设 innerHTML 会重新初始化页面，滚动位置 / ResizeObserver 都丢失
                    // ★ 2026-10-07 陛下要求：工具调用要累计显示（看总共调了哪些）
                    //   新修法：调 HTML 里的 openAppendToolBlock(name, summary)
                    //         JS 在 DOM 中 appendChild(jump) 追加一个 tool-call-card div，不重设 innerHTML
                    //         滚动位置 / ResizeObserver 都保留住
                    string toolName = toolCallLog.Last().ToolName;
                    // ★ 同时往 placeholderAi.Content 里累加，方便历史回看 / SaveSession 后再加载能恢复
                    if (string.IsNullOrEmpty(placeholderAi.Content))
                        placeholderAi.Content = $"🔧 调用工具：{toolName}\n{summary}";
                    else
                        placeholderAi.Content += $"\n\n🔧 调用工具：{toolName}\n{summary}";
                    var (bubble, isWv2) = FindBubbleForStream(placeholderAi);
                    if (bubble != null && isWv2 && bubble is Microsoft.Web.WebView2.WinForms.WebView2 wv2 && wv2.CoreWebView2 != null)
                    {
                        // ★ 用 appendTool 字符串调 JS 端 openAppendToolName，不要重设 innerHTML
                        //   参数转 JSON 防注入；toolName / summary 转 base64 避免引号 / 换行引起 JS 语法问题
                        string nameJson = System.Text.Json.JsonSerializer.Serialize(toolName);
                        string summaryJson = System.Text.Json.JsonSerializer.Serialize(summary ?? string.Empty);
                        string script = "window.openAppendToolBlock(" + nameJson + "," + summaryJson + ");";
                        _ = wv2.CoreWebView2.ExecuteScriptAsync(script);
                    }
                    // ★ 2026-10-19 v2.12 修陛下反馈「关掉重开历史会话工具调用看不到了」
                    //   之前 onToolExecuted 只写 placeholderAi.Content 字符串 + JS 端追加卡片
                    //   placeholderAi.ToolCalls 字段从未被赋值 → 关掉重开从 disk load 时 msg.ToolCalls=null
                    //   BuildToolCardsHtml(msg.ToolCalls) 返回 empty string → 历史会话渲染空 tools-stream
                    //   修法: 每次工具完成都同步 toolCallLog 到 placeholderAi.ToolCalls (复制避免引用被后续清空)
                    //   这样 LocalChatStore.WriteMessagesFile 序列化时会把 ToolCalls 写进 messages/{id}.json
                    //   历史会话 LoadSelectedSession 时 BuildToolCardsHtml 走折叠详情输出 (v2.12 同时改)
                    placeholderAi.ToolCalls = new List<ChatToolCallRecord>(toolCallLog);
                    ScrollToBottom();
                });
                return true;
            };

            // ★ 2026-10-13 增：Skill 流程可视化 1+2 层回调
            //   - load_skill 进入 Skill 模式 → UI 渲染横幅 + 步骤清单
            //   - 后续每次 tool call → UI 推进一步（⏸️→⏳→✅）
            // ★ 2026-10-17 v2.9 修陛下反馈「第二次触发 skill 步骤不显示」:
            //   旧逻辑: CoreWebView2 == null → return (静默吞掉)
            //           第一次 OK 是因为用户输入到 AI load_skill 间隔长, WebView2 init 已完成
            //           第二次失败是因为连发消息, init 还没完 → 静默 return
            //   新逻辑: CoreWebView2 == null 时订阅 CoreWebView2InitializationCompleted
            //           等初始化完再调 JS (不静默 return, 不丢)
            Action<SkillProgressEvent> onSkillProgress = (ev) =>
            {
                try
                {
                    var (bubble, isWv2) = FindBubbleForStream(placeholderAi);
                    if (bubble == null || !isWv2) return;
                    if (bubble is not Microsoft.Web.WebView2.WinForms.WebView2 wv2) return;

                    void DoUpdate()
                    {
                        try
                        {
                            // 重新拿一次, 避免闭包到老的 wv2
                            var (curBubble, _) = FindBubbleForStream(placeholderAi);
                            if (curBubble is not Microsoft.Web.WebView2.WinForms.WebView2 curWv2 || curWv2.CoreWebView2 == null) return;

                            // 1) 步骤标题序列化成 JSON 数组喂 JS
                            var stepTitlesJson = System.Text.Json.JsonSerializer.Serialize(ev.StepTitles ?? new List<string>());
                            string skillNameJson = System.Text.Json.JsonSerializer.Serialize(ev.SkillName ?? "");
                            string skillTitleJson = System.Text.Json.JsonSerializer.Serialize(ev.SkillTitle ?? "");
                            string phaseJson = System.Text.Json.JsonSerializer.Serialize(ev.Phase ?? "");
                            int curStep = ev.CurrentStepIndex;
                            // 2) 调 JS：loaded 阶段全量渲染；progress 阶段只更新勾选；aborted/completed 阶段全勾完成
                            string script =
                                "window.updateSkillBanner(" +
                                    skillNameJson + "," +
                                    skillTitleJson + "," +
                                    stepTitlesJson + "," +
                                    curStep + "," +
                                    phaseJson +
                                ");";
                            _ = curWv2.CoreWebView2.ExecuteScriptAsync(script);
                            // 3) 同步更新 placeholderAi.Content（刷新历史 / 切会话能看）
                            if (string.IsNullOrEmpty(placeholderAi.Content))
                            {
                                placeholderAi.Content = $"📋 Skill 流程：{ev.SkillName} ({ev.StepTitles?.Count ?? 0} 步)";
                            }
                        }
                        catch { /* 可视化失败不影响主流程 */ }
                    }

                    if (wv2.CoreWebView2 == null)
                    {
                        // 等 WebView2 初始化完再调 JS (一次性订阅, 避免重入)
                        void OnInit(object? s, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
                        {
                            wv2.CoreWebView2InitializationCompleted -= OnInit;
                            if (!e.IsSuccess) return;
                            pnlMessagesScroll.InvokeAsync(DoUpdate);
                        }
                        wv2.CoreWebView2InitializationCompleted += OnInit;
                    }
                    else
                    {
                        pnlMessagesScroll.InvokeAsync(DoUpdate);
                    }
                }
                catch { /* 可视化失败不影响主流程 */ }
            };

            var turnResult = await _backend.SendWithToolsAsync(_currentProvider, messagesForApi, toolCallLog, onToolExecuted, OnToolNeedsConfirmAsync, onSkillProgress, _cts.Token);

            // AI 返回最终文本 → 替换占位 → 流式显示
            await StreamDisplayAsync(placeholderAi, turnResult.FinalContent);
            // ★ 2026-10-07 陛下反馈「AI 完成后手不能向上滚」
            //   流式完成后 WebView2 ResizeObserver 还会有 N 次延迟触发（字体/表格/代码块 ScrollToBottom 内部 8 次定时器重试）
            //   冻结 5 秒 SmartScrollToBottom，避免反复拽回底部
            // ★ 2026-10-10 修陛下反馈「5s 后还跳几下」（方案 2 阶段 3）
            //   旧：AddSeconds(5) → 5s 后冻结到期，markdown 异步渲染（表格/代码块/列表）补发 ResizeObserver
            //     → 集中多次改 wv2.Height → FlowLayoutPanel 多次重排 → 整个列表跳几下
            //   新：AddSeconds(10) → 留足异步渲染时间；后续可调为“连续 3 次不变化才解锁”彻底根治
            _streamFreezeUntil = DateTime.UtcNow.AddSeconds(10);
            _currentSession.LastActiveAtUtc = DateTime.UtcNow;
            _store.SaveSession(_currentSession);

            lblStatus.Text = toolCallLog.Count > 0
                ? $"完成 · 调了 {toolCallLog.Count} 个工具"
                : "完成";
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(57, 181, 74);
        }
        catch (OperationCanceledException)
        {
            _currentSession.Messages.Remove(placeholderAi);
            placeholderAi.Content = "（已取消）";
            placeholderAi.IsError = true;
            placeholderAi.ErrorMessage = "用户取消";
            _currentSession.Messages.Add(placeholderAi);
            _store.SaveSession(_currentSession);
            // ★ 2026-10-09 修：原 RenderMessages() 会 Controls.Clear() + 重建所有 WebView2 → 整屏闪
            //   改为就地更新占位气泡的内容和背景色，不动其他气泡（跟工具调用的修法同款）
            UpdatePlaceholderBubbleToError(placeholderAi, "（已取消）", System.Drawing.Color.FromArgb(255, 245, 245));
            lblStatus.Text = "已取消";
            lblStatus.ForeColor = System.Drawing.Color.Gray;
        }
        catch (Exception ex)
        {
            _currentSession.Messages.Remove(placeholderAi);
            placeholderAi.Content = $"调用失败：{ex.Message}";
            placeholderAi.IsError = true;
            placeholderAi.ErrorMessage = ex.Message;
            _currentSession.Messages.Add(placeholderAi);
            _store.SaveSession(_currentSession);
            // ★ 2026-10-09 修：原 RenderMessages() 会 Controls.Clear() + 重建所有 WebView2 → 整屏闪
            //   改为就地更新占位气泡的内容和背景色，不动其他气泡（跟工具调用的修法同款）
            UpdatePlaceholderBubbleToError(placeholderAi, $"调用失败：{ex.Message}", System.Drawing.Color.FromArgb(255, 245, 245));
            lblStatus.Text = "出错";
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(251, 67, 42);
        }
        finally
        {
            _isSending = false;
            btnSend.Enabled = true;
            btnCancel.Visible = false;
            _cts?.Dispose();
            _cts = null;
            _pendingToolCallLog = null;
            RefreshSessions();
        }
    }

    /// <summary>
    /// 流式显示文本到 AI 占位气泡（打字机效果）
    /// ★ 2026-08-29 方案 A：现在是 Label 流式更新
    ///   Label.AutoSize=true + MaximumSize 限制最大宽 → 自动换行、自动撑高
    ///   不用手算高度、不用 TextBox PreferredSize
    /// </summary>
    /// <summary>
    /// ★ 2026-10-09 修：模型调用失败/用户取消时，原 RenderMessages() 会 Controls.Clear() + 重建所有 WebView2 → 整屏闪
    ///   改为就地更新占位气泡的内容和背景色，不动其他气泡（跟工具调用的修法同款）
    ///   不重建 WebView2、不重设 innerHTML，只调 setBubbleBodyOnly 渲染错误文本 + 改背景色
    /// </summary>
    private void UpdatePlaceholderBubbleToError(ChatMessage msg, string errorText, System.Drawing.Color errorBg)
    {
        var (bubble, isWv2) = FindBubbleForStream(msg);
        if (bubble == null) return;
        if (isWv2 && bubble is Microsoft.Web.WebView2.WinForms.WebView2 wv2)
        {
            // 1. 改背景色（红了）—— 不用重新 NavigateToString，不重建 WebView2
            try { wv2.DefaultBackgroundColor = errorBg; } catch { }
            try { wv2.BackColor = errorBg; } catch { }
            // 2. 把错误信息渲染到 #bubble-body（复用流式结束的 setBubbleBodyOnly，不动 tools-stream）
            if (wv2.CoreWebView2 != null)
            {
                string html = RenderMarkdownAsHtml(errorText);
                string escaped = System.Text.Json.JsonSerializer.Serialize(html);
                string js = "window.setBubbleBodyOnly(" + escaped + ");";
                _ = wv2.CoreWebView2.ExecuteScriptAsync(js);
            }
        }
        else if (bubble is Label lbl)
        {
            lbl.Text = errorText;
            var row = lbl.Parent as Panel;
            if (row != null)
            {
                Label? avatar = null;
                foreach (Control c2 in row.Controls)
                {
                    if (c2 is Label al && c2 != lbl) { avatar = al; break; }
                }
                LayoutBubbleRow(row, lbl, avatar, avatar?.Text == "B");
            }
        }
        ScrollToBottom();
    }

    /// <summary>
    /// ★ 2026-10-09 陛下反馈「流式输出疯狂闪烁」+「关掉重开内容不一致」修复
    ///   _streamedTextLength: 当前 bubble-body 已经渲染过的字符数，下次发增量只发 currentText.Substring(_streamedTextLength)
    ///   之前用 updateBubbleBody 每 200ms 整段 innerHTML 替换 + rerenderToolStream 重建 + notify → 三连击闪烁
    ///   现在流式期 appendStreamingText(纯文本) 只追加不替换；结束后 setBubbleBodyOnly 一次性渲染最终 markdown
    /// </summary>
    private int _streamedTextLength = 0;

    private async Task StreamDisplayAsync(ChatMessage aiMsg, string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            fullText = "（AI 没有返回内容）";
        }

        _currentSession.Messages.Remove(aiMsg);
        // ★ 2026-10-09 不再清空 Content —— 之前这里把 onToolExecuted 累积的工具摘要全擦了
        //   现在 onToolExecuted 改写 placeholderAi.ToolCalls，Content 留作流式期间 aiMsg.Content = currentText 自然覆盖
        _currentSession.Messages.Add(aiMsg);

// ★ 2026-08-31锛氭祦寮忚緭鍑哄悓鏃舵敮鎸?Label (鐢ㄦ埛) 鍜?WebView2 (AI)
        //   鎵惧埌姘旀场瀵瑰簲鐨勬帶浠讹紙Label 鎴?WebView2锛夛紝娴佸紡杩藉姞鍐呭
        var (bubble, isWv2) = FindBubbleForStream(aiMsg);
        if (bubble == null)
        {
            aiMsg.Content = fullText;
            RenderMessages();
            return;
        }

        // ★ 2026-10-09 修闪烁：先清空 bubble-body 一次，后续只 append 增量
        if (isWv2 && bubble is Microsoft.Web.WebView2.WinForms.WebView2 wv2Clear && wv2Clear.CoreWebView2 != null)
        {
            _streamedTextLength = 0;
            // 保留 tools-stream 里的卡片，只清 bubble-body
            await wv2Clear.CoreWebView2.ExecuteScriptAsync(
                "(function(){var b=document.getElementById('bubble-body');if(b)b.innerHTML='';})();");
            // ★ 2026-10-09 修陛下反馈「流式输出气泡左右上下晃动」
            //   设流式期标志 → C# ResizeObserver 收到 notify 不再动 wv2.Height → FlowLayoutPanel 不重排 → 气泡不晃
            //   加 streaming-mode class → body overflow-y:auto → 内容超出 wv2 时 wv2 内部出滚动条
            //   给个最小 200px 避免 wv2 太矮看不到字（只发生一次 reflow，远好于每 500ms reflow）
            _streamingWebView2 = wv2Clear;
            if (wv2Clear.Height < 200) wv2Clear.Height = 200;
            await wv2Clear.CoreWebView2.ExecuteScriptAsync(
                "(function(){try{document.body.classList.add('streaming-mode');}catch(e){}})();");
        }

        // 閫愬瓧绗︽祦寮忔樉绀?
        var buffer = new StringBuilder();
        DateTime lastRenderTime = DateTime.MinValue;
        // ★ 2026-09-30 陛下反馈 AI 助理流式刷新太快一直闪
        //   60ms 约 16fps, 加上 WebView2 跨进程 ExecuteScriptAsync + Label 整行 LayoutBubbleRow
        //   频繁重排, 肉眼明显闪烁. 改为 200ms (5fps) + 最少 3 字符间隔
        //   仍能看出逐字/逐行渲染, 但不会闪.
        //   ★ 2026-10-09 陛下反馈 200ms 仍高：AI 流式过程中气泡跟手太快，眼累。改为 500ms
        const int renderThrottleMs = 500;
        const int minCharsBetweenRenders = 3;
        int charsSinceLastRender = 0;
        foreach (char c in fullText)
        {
            if (_cts?.IsCancellationRequested == true) break;
            buffer.Append(c);
            charsSinceLastRender++;
            string currentText = buffer.ToString();

            bool isParagraphEnd = (c == '\n' || c == '.' || c == '?' || c == '!');
            bool shouldRender = isParagraphEnd
                || (charsSinceLastRender >= minCharsBetweenRenders
                    && (DateTime.UtcNow - lastRenderTime).TotalMilliseconds >= renderThrottleMs);
            if (!shouldRender)
            {
                aiMsg.Content = currentText;
                continue;
            }
            lastRenderTime = DateTime.UtcNow;
            charsSinceLastRender = 0;

            Microsoft.Web.WebView2.WinForms.WebView2? wv2ForLambda = (isWv2 ? bubble as Microsoft.Web.WebView2.WinForms.WebView2 : null);
            Label? lblForLambda = (!isWv2 ? bubble as Label : null);
            await pnlMessagesScroll.InvokeAsync(() =>
            {
                aiMsg.Content = currentText;
                if (wv2ForLambda != null && wv2ForLambda.CoreWebView2 != null)
                {
                    // ★ 2026-10-09 修闪烁：只 append 增量纯文本到 bubble-body（不替换 innerHTML、不动 tools-stream）
                    string deltaText = currentText.Substring(_streamedTextLength);
                    _streamedTextLength = currentText.Length;
                    if (deltaText.Length == 0) return;
                    string escaped = System.Text.Json.JsonSerializer.Serialize(deltaText);
                    string js = "window.appendStreamingText(" + escaped + ");";
                    _ = wv2ForLambda.CoreWebView2.ExecuteScriptAsync(js);
                }
                else if (lblForLambda != null)
                {
                    lblForLambda.Text = StripMarkdown(currentText);
                    var row = lblForLambda.Parent as Panel;
                    if (row != null)
                    {
                        Label? avatar = null;
                        foreach (Control c2 in row.Controls)
                        {
                            if (c2 is Label al && c2 != lblForLambda) { avatar = al; break; }
                        }
                        LayoutBubbleRow(row, lblForLambda, avatar, avatar?.Text == "B");
                    }
                }
                // ★ 2026-10-09 修陛下反馈「流式输出上下晃动」
                //   原 ScrollToBottom() 每 500ms 锁底一次 → 陛下中途滚上去读历史被拽回 → 上下晃
                //   修法：SmartScrollToBottom() —— 用户距底 < 100px 才锁底，滚上去就让陛下读
                //   跟聊天 app (Slack/微信/Discord) 的 smart 跟滚一致
                //   副作用：流式期滚上去后，AI 输出继续但视图不动 → 陛下读完再滚回底看新内容
                SmartScrollToBottom();
            });
        }

        // ★ 流式结束后：WebView2 触发一次最终更新（用 setBubbleBodyOnly 替换为完整 markdown HTML，不动 tools-stream）
        if (isWv2 && bubble is Microsoft.Web.WebView2.WinForms.WebView2 wv2End)
        {
            if (wv2End.CoreWebView2 != null)
            {
                await System.Threading.Tasks.Task.Delay(150);
                // ★ 2026-10-09 修陛下反馈「AI 完成后不能向上滚动，时间随内容变长」
                //   根因：RenderMarkdownAsHtml(fullText) Markdig 解析是 O(n) 同步操作
                //         + JsonSerializer.Serialize 又是 O(n) → 全堵在 UI 线程
                //         → pnlMessagesScroll 滚动条拖不动，外层 Panel 也不能滚
                //   修法：Markdig 解析放后台线程（Task.Run），UI 线程只做轻量 IPC
                string htmlEnd = await System.Threading.Tasks.Task.Run(
                    () => RenderMarkdownAsHtml(fullText));
                await pnlMessagesScroll.InvokeAsync(() =>
                {
                    // ★ 2026-10-09 修陛下反馈「流式输出气泡晃动」
                    //   先清流式期标志 → 下面 setBubbleBodyOnly 触发的 notify
                    //   会让 C# ResizeObserver 走正常 height sync（_streamingWebView2 已是 null）
                    //   wv2 一次性跳到真实高度 → FlowLayoutPanel 重排（这一次，不晃）
                    //   JS 端 setBubbleBodyOnly 内部同时移除 streaming-mode class
                    //   body 恢复 overflow:hidden → 不再出内部滚动条
                    _streamingWebView2 = null;
                    // ★ 2026-10-09 用 setBubbleBodyOnly 替代 updateBubbleBody —— 不调 rerenderToolStream
                    //   避免重建 tools-stream 引发的闪烁；保持工具卡片原样
                    string escapedEnd = System.Text.Json.JsonSerializer.Serialize(htmlEnd);
                    string jsEnd = "window.setBubbleBodyOnly(" + escapedEnd + ");";
                    _ = wv2End.CoreWebView2.ExecuteScriptAsync(jsEnd);
                    // ★ 2026-10-19 v2.13 陛下要求：输出过程中一条一条显示，对话完成时工具部分默认折叠，需要看再手动展开
                    //   setBubbleBodyOnly 替换 bubble-body 为完整 markdown
                    //   紧跟 foldToolStream 把 tools-stream 现有卡片包到 details 默认 collapsed
                    //   流式期 openAppendToolBlock 仍一条一条追加 (不折叠)，陛下能实时看到
                    //   对话完成瞬间一次性折叠 → 最终聊天框默认收起，需查看手动展开
                    //   历史会话路径由 BuildToolCardsHtml v2.12 已输出 details，三态视觉一致
                    _ = wv2End.CoreWebView2.ExecuteScriptAsync("window.foldToolStream();");

                });
            }
        }

        // 最终内容存进 message（带 markdown）
        aiMsg.Content = fullText;
        _streamedTextLength = 0;  // 重置，下次流式从头算
        _store.SaveSession(_currentSession);
    }

    /// <summary>
    /// 找出指定消息对应的 Label 气泡（用于流式追加）
    /// ★ 2026-08-29 方案 A：现在气泡是 Label（不是 TextBox），Tag=msg 关联
    ///   检索路径：flpMessages.Controls (row) → row.Controls (avatar + bubble)
    /// </summary>
    private Label? FindBubbleLabel(ChatMessage msg)
    {
        for (int i = flpMessages.Controls.Count - 1; i >= 0; i--)
        {
            if (flpMessages.Controls[i] is Panel row)
            {
                foreach (Control c in row.Controls)
                {
                    if (c is Label lbl && lbl.Tag is ChatMessage cm && cm == msg)
                        return lbl;
                }
            }
        }
        return null;
    }

    /// <summary>

    /// ★ 2026-08-31：流式输出用，返回 (控件, 是否WebView2)
    ///   用户气泡 = Label（AutoSize 自动撑高）
    ///   AI/Error 气泡 = WebView2（用 ExecuteScriptAsync 更新 innerHTML）
    /// </summary>
    private (Control? bubble, bool isWv2) FindBubbleForStream(ChatMessage msg)
    {
        for (int i = flpMessages.Controls.Count - 1; i >= 0; i--)
        {
            if (flpMessages.Controls[i] is Panel row)
            {
                foreach (Control c in row.Controls)
                {
                    if (c.Tag is ChatMessage cm && cm == msg)
                    {
                        return (c, c is Microsoft.Web.WebView2.WinForms.WebView2);
                    }
                }
            }
        }
        return (null, false);
    }
    /// 把 markdown 简化成纯文本（用于流式 Label 显示）
    /// </summary>
    private string StripMarkdown(string md)
    {
        var html = Markdown.ToHtml(md ?? string.Empty, _mdPipeline);
        var sb = new StringBuilder();
        bool inTag = false;
        foreach (char c in html)
        {
            if (c == '<') inTag = true;
            else if (c == '>') { inTag = false; sb.Append(' '); }
            else if (!inTag) sb.Append(c);
        }
        return DecodeHtmlEntities(sb.ToString()).Trim();
    }

    /// <summary>
    /// ★ 2026-09-01 陛下要求：DeepSeek/Qwen 等推理模型输出的 &lt;think&gt;...&lt;/think&gt; 标签
    ///   是 AI 思考过程(Chain of Thought),对陛下无价值 → 直接删除整个块
    ///   多行、非贪婪匹配,处理嵌套和连续多个 think 块
    /// </summary>
    private string RemoveThinkTags(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        // 匹配 <think>...</think>(含跨行内容),后面可选吃掉一个换行避免留空行
        return System.Text.RegularExpressions.Regex.Replace(
            content,
            @"<think>[\s\S]*?</think>\s*\n?",
            string.Empty
        ).TrimStart();
    }

    /// <summary>
    /// ★ 2026-09-01 陛下要求：AI 气泡支持 Markdown 表格（Label 渲染不出 HTML 表格）
    ///   支持两种表格格式：
    ///     1. 标准 Markdown：| col1 | col2 | + |----|----| + | a | b |
    ///     2. AI 预格式化：col1 │ col2 + ───┼──── + CPU │ 90
    ///   至少 3 行含表格分隔符才算
    /// </summary>
    private bool ContainsMarkdownTable(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        var lines = content.Split('\n');
        int tableLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // 标准 Markdown 表格：| col1 | col2 | 开头
            if (trimmed.StartsWith("|") && trimmed.Count(c => c == '|') >= 2)
                tableLineCount++;
            // AI 预格式化表格：含 │ 或 ┼ 分隔符
            else if (trimmed.Contains('│') || trimmed.Contains('┼'))
                tableLineCount++;
        }
        return tableLineCount >= 3;
    }

    /// <summary>
    /// ★ 2026-09-01 把 Markdown 表格转成对齐纯文本（Label 渲染用）
    ///   支持两种输入格式：
    ///     1. 标准 Markdown：| 名称 | 值 |  →  名称 │ 值
    ///     2. AI 预格式化： 名称 │ 值        →  名称 │ 值（重新对齐）
    ///   输出统一格式: │ 列分隔,─┼─ 表头/数据分隔,─ 列填充
    /// </summary>
    private string MarkdownTableToText(string markdown)
    {
        var lines = markdown.Split('\n');
        var result = new StringBuilder();
        bool inTable = false;
        List<string[]>? tableRows = null;
        List<int>? colWidths = null;

        void FlushTable()
        {
            if (tableRows == null || tableRows.Count == 0) return;
            // 算列宽
            if (colWidths == null) colWidths = new List<int>();
            for (int c = 0; c < tableRows[0].Length; c++)
            {
                int maxW = 0;
                foreach (var row in tableRows)
                    if (c < row.Length && row[c].Length > maxW) maxW = row[c].Length;
                colWidths.Add(maxW);
            }
            // 输出表头
            for (int c = 0; c < tableRows[0].Length; c++)
            {
                if (c > 0) result.Append(" │ ");
                result.Append(tableRows[0][c].PadRight(colWidths[c]));
            }
            result.AppendLine();
            // 分隔线
            for (int c = 0; c < tableRows[0].Length; c++)
            {
                if (c > 0) result.Append("─┼─");
                result.Append(new string('─', colWidths[c]));
            }
            result.AppendLine();
            // 数据行（跳过表头和分隔行）
            for (int r = 2; r < tableRows.Count; r++)
            {
                for (int c = 0; c < tableRows[r].Length; c++)
                {
                    if (c > 0) result.Append(" │ ");
                    string cell = c < tableRows[r].Length ? tableRows[r][c] : "";
                    result.Append(cell.PadRight(colWidths[c]));
                }
                result.AppendLine();
            }
            tableRows = null;
            colWidths = null;
            inTable = false;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            // 检测表格行：两种格式
            //   1. 标准 Markdown: | col1 | col2 |（以 | 开头 + ≥2 个 |）
            //   2. AI 预格式化:  col1 │ col2（含 │/┼ 分隔符 + ≥2 个）
            bool isPipeTable = trimmed.StartsWith("|") && trimmed.Count(c => c == '|') >= 2;
            bool isPreAligned = !isPipeTable
                && (trimmed.Contains('│') || trimmed.Contains('┼'));
            bool isTableLine = isPipeTable || isPreAligned;

            // 分隔行检测
            bool isSeparator = false;
            if (isPipeTable)
            {
                // |---|---| 格式
                isSeparator = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\|[-:\s]+\|");
            }
            else if (isPreAligned)
            {
                // ───┼──── 或 ──────── 格式
                isSeparator = (trimmed.Contains('─') || trimmed.Contains('┼'))
                    && !trimmed.Any(c => char.IsLetterOrDigit(c));
            }

            if (isTableLine)
            {
                if (!inTable) { inTable = true; tableRows = new List<string[]>(); }
                // 解析行
                string[] cells;
                if (isPipeTable)
                {
                    // | a | b | → [a, b]
                    cells = trimmed.Trim('|').Split('|');
                }
                else
                {
                    // a │ b ┼ c → [a, b, c]
                    cells = trimmed.Split(new[] { '│', '┼' }, StringSplitOptions.RemoveEmptyEntries);
                }
                for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                if (!isSeparator) tableRows!.Add(cells);
            }
            else
            {
                if (inTable) FlushTable();
                result.AppendLine(line);
            }
        }
        if (inTable) FlushTable();
        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// ★ 2026-09-01 陛下要求：AI 气泡支持 Markdown 标题 + 粗体（Label 渲染不出局部格式）
    ///   思路是用 Unicode 符号让标题/粗体在 Label 里看着有层次：
    ///     # H1 标题    →  ━━━━ 标题 ━━━━
    ///     ## H2 标题   →  ━━ 标题 ━━
    ///     ### H3 标题  →  ▌ 标题
    ///     ####+ H4+    →  ▸ 标题
    ///     **粗体**     →  【粗体】
    ///     `代码`        →  「代码」
    ///     - 列表项      →  • 列表项
    ///   不处理:链接(交给 Markdig)、代码块(交给 Markdig)、图片(交给 Markdig)
    /// </summary>
    private string MarkdownToPrettyText(string markdown)
    {
        var lines = markdown.Split('\n');
        var result = new StringBuilder();

        // 先逐行处理（标题、列表）
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();

            // 标题 # / ## / ### / #### / ##### / ######
            // 注意顺序：先匹配 ###（多个 # 的），再匹配 ##（两个 #），最后 #（一个 #）
            if (trimmed.StartsWith("###### "))
            {
                result.Append("▸ ").AppendLine(trimmed[6..]);
            }
            else if (trimmed.StartsWith("##### "))
            {
                result.Append("▸ ").AppendLine(trimmed[5..]);
            }
            else if (trimmed.StartsWith("#### "))
            {
                result.Append("▌ ").AppendLine(trimmed[4..]);
            }
            else if (trimmed.StartsWith("### "))
            {
                result.Append("▌ ").AppendLine(trimmed[3..]);
            }
            else if (trimmed.StartsWith("## "))
            {
                result.Append("━━ ").Append(trimmed[3..]).AppendLine(" ━━");
            }
            else if (trimmed.StartsWith("# "))
            {
                result.Append("━━━━ ").Append(trimmed[2..]).AppendLine(" ━━━━");
            }
            // 列表 - 或 *
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                result.Append("• ").AppendLine(trimmed[2..]);
            }
            // 普通行
            else
            {
                result.AppendLine(line);
            }
        }

        // 然后处理行内标记（粗体、行内代码）
        var text = result.ToString();
        // **xxx** → 【xxx】（粗体）
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "【$1】");
        // `xxx` → 「xxx」（代码）
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`\n]+?)`", "「$1」");

        return text.TrimEnd();
    }

    /// <summary>
    /// ★ 2026-08-29 14:59 陛下要求 AI 气泡支持 Markdown 完整渲染（含表格/代码块）
    ///   复用 UpdateForm.RenderMarkdownAsHtml 的 GitHub CSS 风格
    ///   返回完整 HTML，可直接赋给 WebBrowser.DocumentText
    /// </summary>
    private string RenderMarkdownAsHtml(string markdown, string toolCardsHtml = "")
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        // ★ 2026-10-09 陛下要求：AI 思考内容（&lt;think&gt;...&lt;/think&gt;）默认折叠、点击展开
        //   拆 markdown 为 [text, thinking, text, ...] 段，text 段走 Markdig，thinking 段渲染为 <details>
        string bodyHtml = RenderBodyWithThinkingBlocks(markdown ?? string.Empty, pipeline);
        // ★★★ 2026-09-02 陛下反馈表格差劲：手工包装 table → div.bubble-table-wrap 才能 overflow-x:auto 滚
        bodyHtml = WrapTablesForScroll(bodyHtml);

        // GitHub 风格 CSS（与 UpdateForm 一致，适配 AI 气泡场景字号略小）
        string html = @"<!DOCTYPE html><html><head><meta charset=""utf-8""><style>
::-webkit-scrollbar { display: none !important; width: 0 !important; height: 0 !important; }
html { margin: 0; padding: 0; }  /* ★ 消除 html 默认 8px margin，避免 wv2.Height = scrollHeight 还不够 */
body { font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif; font-size: 24px; line-height: 38px; color: #24292f; background: transparent; padding: 10px 14px 10px 7px; margin: 0; overflow: hidden; overflow-wrap: break-word; word-break: break-word; }  /* ★★★ 2026-10-08 RasterizationScale=1.0 后所有 CSS 尺寸 ×1.75 补回 168 DPI 下的视觉大小 */

/* ★ 2026-10-09 修陛下反馈「流式输出气泡左右上下晃动」
   流式期 wv2 高度锁死（_streamingWebView2 机制）→ 内容超出 wv2 时裁掉
   body.streaming-mode 临时改 overflow-y:auto → wv2 内部出滚动条
   appendStreamingText 末尾 body.scrollTop = scrollHeight → 始终看最新字
   流式结束 setBubbleBodyOnly 移除 class → body 恢复 overflow:hidden */
body.streaming-mode { overflow-y: auto; }

h1, h2, h3, h4, h5, h6 { margin: 24px 0 14px 0; font-weight: 600; line-height: 1.25; }
h1 { font-size: 32px; padding-bottom: 7px; border-bottom: 1px solid #d0d7de; }
h2 { font-size: 28px; padding-bottom: 7px; border-bottom: 1px solid #d0d7de; }
h3 { font-size: 24px; }
p { margin: 0 0 14px 0; }
ul, ol { margin: 0 0 14px 0; padding-left: 38px; }
li { margin: 4px 0; }
blockquote { margin: 0 0 14px 0; padding: 0 18px; color: #57606a; border-left: 7px solid #d0d7de; background: #f6f8fa; }
code { font-family: 'Consolas', monospace; font-size: 16pt; background: rgba(175, 184, 193, 0.2); padding: 2px 7px; border-radius: 7px; word-break: break-all; }
pre { background: #f6f8fa; padding: 14px 18px; border-radius: 10px; overflow-x: auto; margin: 0 0 14px 0; line-height: 1.45; white-space: pre-wrap; word-break: break-all; }
pre code { background: transparent; padding: 0; font-size: 16pt; }
strong { font-weight: 600; }
em { font-style: italic; }
a { color: #0969da; text-decoration: none; }
hr { border: none; border-top: 1px solid #d0d7de; margin: 24px 0; }
/* ★★★ 2026-09-02 陛下反馈表格显示差劲，升级为 GitHub 正式版表格样式 ★★★ */
.bubble-table-wrap {
  display: block;
  max-width: 100%;
  overflow-x: auto;
  margin: 0 0 18px 0;
  border: 1px solid #d0d7de;
  border-radius: 10px;
  background: #ffffff;
}
table {
  border-collapse: collapse;
  width: max-content;
  min-width: 100%;
  margin: 0;
  font-size: 23px;
  line-height: 35px;
}
table th, table td {
  border: 1px solid #d0d7de;
  padding: 10px 24px;
  text-align: left;
  vertical-align: top;
  white-space: nowrap;
  min-width: 105px;
  max-width: 735px;
  overflow: hidden;
  text-overflow: ellipsis;
}
/* ★ 首列背景区分，提示这是关键列 */
table th:first-child, table td:first-child {
  background: #f6f8fa;
  font-weight: 600;
  color: #24292f;
}
/* ★ 表头采用深色，主题更明确 */
table thead th {
  background: #eaeef2;
  font-weight: 700;
  color: #24292f;
  position: sticky;
  top: 0;
  z-index: 1;
}
/* ★ 斑马纹 */
table tbody tr:nth-child(even) td { background: #f9fafb; }
table tbody tr:nth-child(even) td:first-child { background: #f1f3f5; }
table tbody tr:hover td { background: #eef3f8; }
table tbody tr:hover td:first-child { background: #e0e7ee; }
/* ★ 单元格内代码/链接舒展 */
table code { background: rgba(175,184,193,0.2); padding: 2px 9px; border-radius: 5px; font-size: 21px; }
table a { color: #0969da; text-decoration: none; border-bottom: 1px solid transparent; }
table a:hover { border-bottom-color: #0969da; }
img { max-width: 100%; }
/* ★ 2026-10-09 思考过程折叠块：默认 collapsed，点击 summary 展开 */
.thinking-block {
  margin: 0 0 14px 0;
  border: 1px solid #d0d7de;
  border-radius: 8px;
  background: #f6f8fa;
  font-size: 20px;
  line-height: 30px;
  overflow: hidden;
}
.thinking-block > summary {
  padding: 8px 14px;
  cursor: pointer;
  color: #57606a;
  font-weight: 500;
  user-select: none;
  list-style: none;
  border-bottom: 1px solid transparent;
}
.thinking-block > summary::-webkit-details-marker { display: none; }
.thinking-block > summary::before {
  content: ""▶"";
  display: inline-block;
  margin-right: 8px;
  font-size: 14px;
  transition: transform 0.15s;
}
.thinking-block[open] > summary::before { transform: rotate(90deg); }
.thinking-block > summary:hover { background: #eaeef2; }
.thinking-block[open] > summary { border-bottom-color: #d0d7de; }
.thinking-content {
  padding: 10px 14px;
  color: #57606a;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 400px;
  overflow-y: auto;
}
/* ★ 2026-10-19 v2.12 工具调用折叠块: 仿 thinking-block 样式, 默认 collapsed, 点击 summary 展开
   蓝色主题跟 tool-call-card 背景色 (#f0f7ff) 配套 */
.tools-block {
  margin: 0 0 14px 0;
  border: 1px solid #b6dcfb;
  border-radius: 8px;
  background: #f0f7ff;
  font-size: 20px;
  line-height: 30px;
  overflow: hidden;
}
.tools-block > summary {
  padding: 8px 14px;
  cursor: pointer;
  color: #0969da;
  font-weight: 500;
  user-select: none;
  list-style: none;
  border-bottom: 1px solid transparent;
}
.tools-block > summary::-webkit-details-marker { display: none; }
.tools-block > summary::before {
  content: ""▶"";
  display: inline-block;
  margin-right: 8px;
  font-size: 14px;
  transition: transform 0.15s;
}
.tools-block[open] > summary::before { transform: rotate(90deg); }
.tools-block > summary:hover { background: #e1ecf7; }
.tools-block[open] > summary { border-bottom-color: #b6dcfb; }
.tools-content { padding: 10px 14px; }
/* ★ 2026-10-13 增：Skill 流程可视化横幅（1+2 层）
   需求：AI 调 load_skill 后，在气泡顶部显示 Skill 模式 + 步骤清单
   - skill-banner：横幅本体，橙色背景，醒目提示在走 Skill 流程
   - skill-step-active：当前正在执行的步骤，橙色加粗
   - skill-step-done：已完成步骤，绿色 + 删除线
   - skill-step-pending：未开始步骤，灰色
   - skill-banner-completed/aborted：完成/中断时改色提示 */
.skill-banner {
  background: linear-gradient(135deg, #fff8e1 0%, #ffecb3 100%);
  border-left: 4px solid #ff9800;
  padding: 10px 14px;
  margin: 0 0 14px 0;
  border-radius: 8px;
  font-size: 13px;
  line-height: 20px;
  box-shadow: 0 2px 4px rgba(255, 152, 0, 0.15);
}
.skill-banner-header {
  font-weight: 600;
  color: #e65100;
  margin-bottom: 8px;
  font-size: 14px;
}
.skill-banner-empty {
  color: #999;
  font-style: italic;
  font-size: 12px;
}
.skill-step-list {
  list-style: none;
  padding-left: 0;
  margin: 0;
}
.skill-step {
  padding: 4px 0;
  font-size: 13px;
  line-height: 18px;
  transition: all 0.2s;
}
.skill-step-pending { color: #999; }
.skill-step-active {
  color: #ff9800;
  font-weight: 600;
  background: rgba(255, 152, 0, 0.08);
  padding-left: 8px;
  border-radius: 4px;
}
.skill-step-done {
  color: #4caf50;
  text-decoration: line-through;
  opacity: 0.75;
}
.skill-banner-completed {
  background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
  border-left-color: #4caf50;
}
.skill-banner-aborted {
  background: linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%);
  border-left-color: #f44336;
}
</style></head><body><div id=""bubble-body"">" + bodyHtml + @"</div><div id=""tools-stream"">" + toolCardsHtml + @"</div><script>
(function() {
  // ★ 2026-08-31 方案 X：用 getBoundingClientRect().bottom - top 推真实高度
  //   WebView2 控件 Height  == 浏览器渲染区高度
  //   但浏览器渲染区 < scrollHeight (scrollbar预留 + 内部padding)
  //   getBoundingClientRect 给出 body 元素渲染边界，比 scrollHeight 更准
  function notify() {
    try {
      var rect = document.body.getBoundingClientRect();
      var h = Math.ceil(rect.bottom - rect.top);
      var w = Math.max(document.body.scrollWidth, document.documentElement.scrollWidth);
      var childs = [];
      for (var i = 0; i < document.body.children.length; i++) {
        var c = document.body.children[i];
        childs.push(c.tagName + '=' + Math.round(c.offsetHeight));
      }
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
          h: h, w: w,
          fs: getComputedStyle(document.body).fontSize,
          lh: getComputedStyle(document.body).lineHeight,
          ch: childs.join(';')
        }));
      }
    } catch (e) {}
  }
  // ★ 2026-10-07 陛下反馈「气泡截断 + 屏幕闪烁」：
  //   原 JS 端防抖 150ms → 流式时 ResizeObserver 持续 fire，防抖定时器一直被重置→整个流式期间都不通知 C# → wv2.Height 不变 → 内容被裁
  //   同时各次 resize 间歇性拉高造成屏幕闪烁
  //   修法：去掉 JS 端防抖，每帧立即报高度（scrollHeight，准确反映 Markdown/表格/代码块需要的高度）
  //   闪烁控制改在 C# 端：节流 wv2.Height 赋值，最少间隔 150ms，避免反复撑缩导致 WebView2 低 DPI 重新渲染
  let _lastNotify = 0;
  function _notifyNow() {
    try {
      // ★★★ 2026-10-07 彻底换测量方式：
      //   之前用 getBoundingClientRect 测 #bubble-body / #tools-stream 的 bottom
      //   → 测量值可能包含子元素 margin，导致偏大 → wv2 过大 → 空白
      //   改用 document.body.scrollHeight：浏览器报告的 body 真实内容高度（不含 body padding）
      //   这是最权威的内容高度指标，不依赖任何子元素测量
      var h_scroll = Math.max(
        document.body.scrollHeight || 0,
        document.documentElement.scrollHeight || 0
      );
      var h_visible = 0;
      var ids = ['bubble-body', 'tools-stream'];
      for (var i = 0; i < ids.length; i++) {
        var el = document.getElementById(ids[i]);
        if (el) {
          var r = el.getBoundingClientRect();
          if (r.bottom > h_visible) h_visible = r.bottom;
        }
      }
      h_visible = Math.ceil(h_visible);
      var h_body = Math.ceil(document.body.getBoundingClientRect().height);
      var h_html = Math.ceil(document.documentElement.getBoundingClientRect().height);
      // ★★★ 2026-10-07 陛下反馈「内容总高度有问题」：
      //   优先用 h_visible（实际可见底部），如果 h_visible 异常再用 h_scroll 兜底
      var h = h_visible > 0 ? h_visible : h_scroll;
      var w = Math.max(
        document.body.scrollWidth || 0,
        document.documentElement.scrollWidth || 0
      );
      // 报 viewport 实际 CSS 高度，C# 端用它自校准换算比例
      var vp = Math.ceil(window.innerHeight || document.documentElement.clientHeight || 0);
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
          h: h, w: w, vp: vp,
          h_scroll: h_scroll, h_visible: h_visible, h_body: h_body, h_html: h_html
        }));
      }
      _lastNotify = Date.now();
    } catch (e) {}
  }
  // 首次立即报（拿到初始尺寸）
  setTimeout(_notifyNow, 0);
  setTimeout(_notifyNow, 50);
  // ResizeObserver / 内置定时器 → 立即通知（C# 端节流）
  if (typeof ResizeObserver !== 'undefined') {
    new ResizeObserver(_notifyNow).observe(document.body);
  } else {
    setInterval(_notifyNow, 250);
  }
  // 字体加载完：立即报一次（最终 settle 状态）
  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(_notifyNow);
  }
  // ★★★ 2026-10-07 修陛下反馈「气泡底部差很多内容」：C# 端 150ms 节流会把 streaming 末尾最后一次 notify 挡掉
  //   （流式接近结束时 notify 还在密集 fire，最后一个 100% 高度的 notify 距离前一个 < 150ms → 被节流丢弃
  //   → wv2 永远停在 mid-stream 高度 → 底部差内容）
  //   修法：加 4 个 setTimeout 强制在不同延迟后再报，间隔足够让节流窗口过去
  setTimeout(_notifyNow, 200);
  setTimeout(_notifyNow, 500);
  setTimeout(_notifyNow, 1500);
  setTimeout(_notifyNow, 3000);

  // ★ 2026-10-07 陛下要求：AI 调过的工具要能看到总计列表
  //   提供 openAppendToolBlock(name, summary)：在 #tools-stream 末尾插一个块（不是覆盖 innerHTML）
  //   这样中途调多次工具不会被重置，滚动位置 / ResizeObserver 都保持原状
  //   内部存 window.__appendedToolBlocks 数组 → 流式输出重写 #bubble-body 后还能重渲染
  window.__appendedToolBlocks = [];
  function renderToolCard(name, summary) {
    var card = document.createElement('div');
    card.className = 'tool-call-card';
    card.style.cssText = 'margin:8px 0; padding:8px 12px; background:#f0f7ff; border:1px solid #b6dcfb; border-radius:6px; font-size:13px; line-height:20px; color:#0b3a66;';
    var head = document.createElement('div');
    head.style.cssText = 'font-weight:600; color:#0969da; margin-bottom:4px;';
    head.textContent = '🔧 ' + name;
    card.appendChild(head);
    var body = document.createElement('div');
    body.style.whiteSpace = 'pre-wrap';
    body.textContent = summary || '';
    card.appendChild(body);
    return card;
  }
  function rerenderToolStream() {
    try {
      var stream = document.getElementById('tools-stream');
      if (!stream) return;
      // ★ 2026-10-19 v2.19: 流式期也折叠 (陁下退路: 对话中和对话完成都折叠)
      //   与 BuildToolCardsHtml (历史会话路径) 输出格式完全一致
      //   保留展开状态: 如果陁下手动展开了, 新工具追加时保持展开
      var wasOpen = false;
      var existing = stream.querySelector('details.tools-block');
      if (existing && existing.open) wasOpen = true;
      stream.innerHTML = '';
      var n = window.__appendedToolBlocks.length;
      if (n === 0) return;
      var details = document.createElement('details');
      details.className = 'tools-block';
      if (wasOpen) details.open = true;
      var summary = document.createElement('summary');
      summary.textContent = '🔧 工具调用 (' + n + ')';
      details.appendChild(summary);
      var content = document.createElement('div');
      content.className = 'tools-content';
      for (var i = 0; i < n; i++) {
        var blk = window.__appendedToolBlocks[i];
        content.appendChild(renderToolCard(blk.name, blk.summary));
      }
      details.appendChild(content);
      stream.appendChild(details);
    } catch (e) {}
  }
  window.openAppendToolBlock = function(name, summary) {
    try {
      window.__appendedToolBlocks.push({name: name, summary: summary || ''});
      rerenderToolStream();
      notify();  // 高度变化推回 C#
    } catch (e) {}
  };  // ★ 2026-10-19 v2.13 陛下要求：输出过程中一条一条显示，对话完成时工具部分默认折叠，需要看再手动展开
  //   流式期 openAppendToolBlock 一条一条追加 tool-card → 保持展开 (不折叠)
  //   对话完成时 (C# 调 setBubbleBodyOnly 之后) 调 foldToolStream 一次性包到 details 折叠块
  //   与 BuildToolCardsHtml (历史会话路径) 输出格式完全一致，三态视觉统一
  window.foldToolStream = function() {
    try {
      var stream = document.getElementById('tools-stream');
      if (!stream) return;
      if (stream.querySelector('.tools-block')) return;
      var cards = stream.querySelectorAll('.tool-call-card');
      if (cards.length === 0) return;
      var details = document.createElement('details');
      details.className = 'tools-block';
      var summaryEl = document.createElement('summary');
      summaryEl.textContent = '🔧 工具调用 (' + cards.length + ')';
      details.appendChild(summaryEl);
      var content = document.createElement('div');
      content.className = 'tools-content';
      while (stream.firstChild) {
        content.appendChild(stream.firstChild);
      }
      details.appendChild(content);
      stream.appendChild(details);
      notify();
    } catch (e) {}
  };  // ★ 2026-10-19 v2.17 修陛下反馈「v2.16 误删 foldToolStream 的 }; 导致历史会话加载不出来 + v2.16 setInterval 跟 v2.14 一样有 bug 撤掉」
  //   紧急诊断: v2.16 删除范围 145892-147949 把 foldToolStream 函数的 }; 一起删了
  //             -> wv2 加载 HTML 模板时 JS 解析失败 -> setBubbleBodyOnly 等函数不存在
  //             -> C# 调 setBubbleBodyOnly 失败 -> 历史会话气泡内容渲染不出来
  //   v2.17 修法:
  //     1. 补 foldToolStream 的 }; 闭合 (上面那行)
  //     2. 删除 v2.16 整个 setInterval 块 (流式期前 1.5秒误折叠 bug 跟 v2.14 一样)
  //     3. 保留 v2.13 C# 触发点 (L2280) 修 if 块限制 - 由 C# 端扩 if 块条件

  // ★ 2026-10-13 增：Skill 流程可视化（1+2 层）
  //   C# 端在 load_skill 时调一次（phase='loaded' 渲染横幅 + 步骤清单）
  //   后续每次 tool call 调一次（phase='progress' 推进 ⏸️→⏳→✅）
  //   Skill 结束时调一次（phase='completed' / 'aborted' 全勾完成/改色）
  //   stepTitles 允许为空数组（Skill 无 ## 步骤 段时降级显示提示）
  window.updateSkillBanner = function(skillName, skillTitle, stepTitles, currentStepIndex, phase) {
    try {
      var body = document.getElementById('bubble-body');
      if (!body) return;
      var banner = document.getElementById('skill-banner');
      // 1) loaded 阶段：全量重建横幅
      if (phase === 'loaded') {
        if (banner) banner.remove();
        banner = document.createElement('div');
        banner.id = 'skill-banner';
        banner.className = 'skill-banner';
        var header = document.createElement('div');
        header.className = 'skill-banner-header';
        header.textContent = '📋 Skill 模式：' + skillName + (skillTitle ? '（' + skillTitle + '）' : '') + ' · 共 ' + (stepTitles ? stepTitles.length : 0) + ' 步';
        banner.appendChild(header);
        if (stepTitles && stepTitles.length > 0) {
          var list = document.createElement('ul');
          list.className = 'skill-step-list';
          for (var i = 0; i < stepTitles.length; i++) {
            var li = document.createElement('li');
            li.className = 'skill-step skill-step-pending';
            li.setAttribute('data-step', i);
            li.setAttribute('data-title', stepTitles[i]);
            li.textContent = '⏸️ Step ' + (i + 1) + '：' + stepTitles[i];
            list.appendChild(li);
          }
          banner.appendChild(list);
        } else {
          var noStep = document.createElement('div');
          noStep.className = 'skill-banner-empty';
          noStep.textContent = '（此 Skill 未定义 ## 步骤 段，将按工具调用顺序推进）';
          banner.appendChild(noStep);
        }
        body.insertBefore(banner, body.firstChild);
      }
      if (!banner) return;
      // 2) 更新步骤状态（progress / completed / aborted 都走这段）
      var items = banner.querySelectorAll('.skill-step');
      var total = stepTitles ? stepTitles.length : 0;
      if (phase === 'completed' || phase === 'aborted') {
        banner.classList.add('skill-banner-' + phase);
        for (var j = 0; j < items.length; j++) {
          items[j].className = 'skill-step skill-step-done';
          items[j].textContent = '✅ Step ' + (j + 1) + '：' + items[j].getAttribute('data-title');
        }
      } else {
        for (var k = 0; k < items.length; k++) {
          if (k < currentStepIndex) {
            items[k].className = 'skill-step skill-step-done';
            items[k].textContent = '✅ Step ' + (k + 1) + '：' + items[k].getAttribute('data-title');
          } else if (k === currentStepIndex) {
            items[k].className = 'skill-step skill-step-active';
            items[k].textContent = '⏳ Step ' + (k + 1) + '：' + items[k].getAttribute('data-title');
          } else {
            items[k].className = 'skill-step skill-step-pending';
            items[k].textContent = '⏸️ Step ' + (k + 1) + '：' + items[k].getAttribute('data-title');
          }
        }
      }
      // 3) 更新头部进度文字
      var headerEl = banner.querySelector('.skill-banner-header');
      if (headerEl && phase !== 'loaded') {
        var doneCount = (phase === 'completed' || phase === 'aborted') ? total : Math.max(0, currentStepIndex + 1);
        var phaseLabel = phase === 'completed' ? '✅ 完成' : (phase === 'aborted' ? '⚠️ 中断' : ('⏳ 进行中 ' + doneCount + '/' + total));
        headerEl.textContent = '📋 Skill 模式：' + skillName + (skillTitle ? '（' + skillTitle + '）' : '') + ' · ' + phaseLabel;
      }
      // 4) 高度变化推回 C#
      if (typeof _notifyNow === 'function') _notifyNow();
    } catch (e) {}
  };
  // ★ 2026-10-07 陛下反馈：AI 流式完成后又重设 body.innerHTML，把 tools-stream 里的 tool cards 全擦了
  //   改用 updateBubbleBody(html)：只重设 #bubble-body（内容区域），不动 #tools-stream，然后重渲染 tool 列表
  window.updateBubbleBody = function(html) {
    try {
      var body = document.getElementById('bubble-body');
      if (body) {
        body.innerHTML = html;
      } else {
        // 老 HTML 结构（没 #bubble-body 包鞄）→ 退回原行为重设 body
        document.body.innerHTML = html;
      }
      rerenderToolStream();
      notify();
    } catch (e) {}
  };

  // ★ 2026-10-09 修陛下反馈「流式输出疯狂闪烁」+「关掉重开内容不一致」：
  //   闪烁根因：updateBubbleBody 每次 innerHTML 替换 + rerenderToolStream 重建 + notify 触发 C# 高度重算 → 三连击
  //   修法：
  //     appendStreamingText(text)  流式时调，只把纯文本 appendChild 到 #bubble-body（不重建 DOM、不动 tools-stream）
  //     setBubbleBodyOnly(html)    流式结束调，一次性把 #bubble-body 替换为完整 markdown HTML（不动 tools-stream）
  //   工具卡片从 #bubble-body 里抽离 → #tools-stream 专门负责，渲染时只管一边
  window.appendStreamingText = function(text) {
    try {
      var body = document.getElementById('bubble-body');
      if (body) {
        // 用 textNode 追加，避免 HTML 解析问题（纯文本不需转义）
        body.appendChild(document.createTextNode(text));
        // ★ 2026-10-10 修陛下反馈「流式期闪」
        //   旧：document.body.scrollTop = document.body.scrollHeight  + notify()
        //     1) 直接赋值 scrollTop → 浏览器立刻跳滚动 → 重绘 → 气泡内文字跳
        //     2) notify()  → C# ResizeObserver 走一轮 → wv2 进程重绘 → 文字边抗锯齿
        //   新：requestAnimationFrame 平滑滚 + 不调 notify（流式期锁高用不到）
        //     1) RAF 下一帧才赋值 scrollTop → 浏览器同帧合并多次 → 文字不跳
        //     2) 不 notify → C# ResizeObserver 不 fire → 文字边不重新抗锯齿
        requestAnimationFrame(function() {
          document.body.scrollTop = document.body.scrollHeight;
        });
      }
      // ★ 2026-10-10 流式期不调 notify（锁高度用不到；避免 wv2 进程不必要重绘）
    } catch (e) {}
  };
  window.setBubbleBodyOnly = function(html) {
    try {
      var body = document.getElementById('bubble-body');
      if (body) {
        body.innerHTML = html;
      }
      // ★ 2026-10-09 移除 streaming-mode：body 恢复 overflow:hidden
      // ★ 2026-10-10 修陛下反馈「收尾闪」
      //   旧：removeClass  + notify() 同步走
      //     removeClass 立刻把 body overflow 从 auto 变 hidden → 布局突变
      //     notify 同步立刻调 → C# ResizeObserver 当帧拿老高度 + 下一帧拿新高度 → 中间态
      //   新：double requestAnimationFrame 延后 notify
      //     第 1 帧：removeClass 提交（body 布局变化提交）
      //     第 2 帧：body 重新布局完成（滚动条/高度都稳定）
      //     notify() 在第 2 帧末尾调 → C# ResizeObserver 拿稳定后高度 → 一次到位
      //     避免 wv2 多次重排 / FlowLayoutPanel 跳变
      try { document.body.classList.remove('streaming-mode'); } catch (e) {}

        // ★ 2026-10-19 v2.18: 流式结束自动折叠工具调用 (300ms 延輯补链完成)
        //   不依赖 C# 触发点 (v2.13 if 块不总进) + 不依赖 setInterval/MutationObserver
        //   setBubbleBodyOnly 是流式结束的唯一信号, 在这里 hook 折叠 = 所有路径都触发
        setTimeout(function() { window.foldToolStream(); }, 300);      requestAnimationFrame(function() {
        requestAnimationFrame(function() {
          notify();
        });
      });
    } catch (e) {}
  };

  // ★★★ 2026-09-02 转发滚轮事件到 C#：WebView2 默认吞 wheel 进浏览器内部，
  //   WinForms 控件本身收不到事件。所以用 JS 监听 wheel，postMessage 给 C# 滚父容器
  //   ★ passive:false 才能 preventDefault 阻止浏览器内部滚动
  window.addEventListener('wheel', function(e) {
    try {
      e.preventDefault();
      e.stopPropagation();
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
          type: 'wheel',
          delta: e.deltaY,
          ctrl: e.ctrlKey,
          shift: e.shiftKey
        }));
      }
    } catch (err) {}
  }, { passive: false });
  // ★ 同时捕获内部滚动容器的滚轮（表格 .bubble-table-wrap 内的横滚也转给父容器）
  document.body.addEventListener('wheel', function(e) {
    // ★ bubble-table-wrap 内只有横滚，没有纵滚意义，纵滚转交聊天列表
    //   横滚（Shift+wheel 或 deltaX）允许浏览器自己处理
    if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
      e.preventDefault();
      e.stopPropagation();
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
          type: 'wheel',
          delta: e.deltaY,
          ctrl: e.ctrlKey,
          shift: e.shiftKey
        }));
      }
    }
  }, { passive: false });
})();
</script></body></html>";
        return html;
    }

    /// <summary>
    /// ★ 2026-10-09 陛下要求：AI 思考内容（&lt;think&gt;...&lt;/think&gt;）默认折叠、点击展开
    /// <para>把 markdown 拆成 [text, thinking, text, ...] 段：text 段走 Markdig 解析，
    /// thinking 段渲染为 &lt;details class="thinking-block"&gt; 折叠块（默认 collapsed）</para>
    /// <para>替换后效果：对话中 / 完成后 / 关掉重开 三种状态看到的思考内容长度一致（都折叠），
    /// 只有用户主动点 summary 才展开看到思考过程</para>
    /// </summary>
    private string RenderBodyWithThinkingBlocks(string markdown, MarkdownPipeline pipeline)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        // ★ 用全限定名 System.Text.RegularExpressions.Regex（using 段没引，保持 diff 小）
        var thinkingRegex = new System.Text.RegularExpressions.Regex(
            @"<think>([\s\S]*?)</think>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var matches = thinkingRegex.Matches(markdown);

        // 没有思考块 → 走原 Markdig 路径
        if (matches.Count == 0)
            return Markdown.ToHtml(markdown, pipeline);

        var sb = new StringBuilder();
        int lastEnd = 0;
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            // 思考块之前的正常 markdown → Markdig 解析
            string textPart = markdown.Substring(lastEnd, m.Index - lastEnd);
            if (!string.IsNullOrWhiteSpace(textPart))
                sb.Append(Markdown.ToHtml(textPart, pipeline));

            // 思考块本身 → 渲染为折叠 details（默认 collapsed）
            string thinkContent = m.Groups[1].Value.Trim();
            sb.Append(RenderThinkingBlock(thinkContent));

            lastEnd = m.Index + m.Length;
        }
        // 最后一个思考块之后的尾段
        string trailingText = markdown.Substring(lastEnd);
        if (!string.IsNullOrWhiteSpace(trailingText))
            sb.Append(Markdown.ToHtml(trailingText, pipeline));

        return sb.ToString();
    }

    /// <summary>
    /// 把单个思考内容渲染为 &lt;details class="thinking-block"&gt; 折叠 HTML
    /// <para>内容走 HTML encode + 保留换行（不解析 markdown，保持纯文本可读，避免递归嵌思考）</para>
    /// </summary>
    private string RenderThinkingBlock(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        // ★ WebUtility.HtmlEncode 转义（避免思考里带 <>& 被当 HTML 解析）
        string encoded = System.Net.WebUtility.HtmlEncode(content)
            .Replace("\r\n", "\n").Replace("\n", "<br>");
        return $"<details class=\"thinking-block\"><summary>💭 思考过程（点击展开）</summary><div class=\"thinking-content\">{encoded}</div></details>";
    }

    /// <summary>
    /// ★ 2026-10-09 把 List<ChatToolCallRecord> 渲染成 HTML 卡片字符串（跟 JS renderToolCard 同款样式）
    ///   加载历史消息时由 CreateBubbleRow 调，把 msg.ToolCalls 注入到 #tools-stream 容器里
    ///   跟 openAppendToolBlock 实时追加的卡片样式完全一致 → 关掉重开看起来一样
    ///   name/summary 走 System.Net.WebUtility.HtmlEncode 防 XSS
    /// </summary>
    private string BuildToolCardsHtml(List<ChatToolCallRecord>? toolCalls)
    {
        if (toolCalls == null || toolCalls.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        // ★ 2026-10-19 v2.12 仿 thinking-block 折叠: 工具调用整体包到 <details class="tools-block"> 默认 collapsed
        //   陛下要求: AI 助力工具部分能像思考模式一样折叠, 默认收起, 可手动展开, 历史会话也要体现
        //   summary 显示数量 (N) → 陛下一眼看到调了几个工具, 不用展开也知道
        sb.Append("<details class=\"tools-block\"><summary>🔧 工具调用 (").Append(toolCalls.Count).Append(")</summary><div class=\"tools-content\">");
        foreach (var tc in toolCalls)
        {
            string name = System.Net.WebUtility.HtmlEncode(tc.ToolName ?? string.Empty);
            string summary = System.Net.WebUtility.HtmlEncode(tc.Result?.Summary ?? string.Empty);
            // 跟 JS renderToolCard 完全同款 inline style → 实时 / 历史视觉一致
            sb.Append("<div class=\"tool-call-card\" style=\"margin:8px 0; padding:8px 12px; background:#f0f7ff; border:1px solid #b6dcfb; border-radius:6px; font-size:13px; line-height:20px; color:#0b3a66;\">");
            sb.Append("<div style=\"font-weight:600; color:#0969da; margin-bottom:4px;\">🔧 ").Append(name).Append("</div>");
            sb.Append("<div style=\"white-space:pre-wrap;\">").Append(summary).Append("</div>");
            sb.Append("</div>");
        }
        sb.Append("</div></details>");
        return sb.ToString();
    }

    /// <summary>
    /// ★★★ 2026-09-02 表格包裹为可滚动容器：AI 输出的表格列数可能超出气泡宽度，需 overflow-x:auto
    ///   原 table 没有包裹层，CSS 的 overflow-x:auto 不起作用
    ///   这里手动将顶层 <table>...</table> 包成 <div class="bubble-table-wrap">...</div>
    ///   只包最外层 table（不包 thead/nested table）
    /// </summary>
    private static string WrapTablesForScroll(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var result = new StringBuilder(html.Length + 64);
        int i = 0;
        while (i < html.Length)
        {
            int idx = html.IndexOf("<table", i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                result.Append(html, i, html.Length - i);
                break;
            }
            // ★ 需确认 <table 后是空格或 >，避免误匹配 <tablefoo>
            char next = idx + 6 < html.Length ? html[idx + 6] : ' ';
            if (next != ' ' && next != '>')
            {
                result.Append(html, i, idx - i + 6);
                i = idx + 6;
                continue;
            }
            // ★ 找匹配的 </table>（同层级）
            int endIdx = html.IndexOf("</table>", idx, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0)
            {
                result.Append(html, i, html.Length - i);
                break;
            }
            int tableEnd = endIdx + "</table>".Length;
            // ★ 输出 <table> 之前的内容
            result.Append(html, i, idx - i);
            // ★ 包一层可滚动容器
            result.Append("<div class=\"bubble-table-wrap\">");
            result.Append(html, idx, tableEnd - idx);
            result.Append("</div>");
            i = tableEnd;
        }
        return result.ToString();
    }

    private List<ChatMessage> BuildApiMessages(List<ChatMessage> history)
    {
        var result = new List<ChatMessage>();

        // System Prompt：拼装知识库 + 陛下自定义追加
        // R4: 传入陛下最近一条用户消息，用于检索相关知识库条目
        var lastUserMsg = history.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? "";
        var sysContent = BuildSystemPrompt(lastUserMsg);
        result.Add(new ChatMessage { Role = ChatRole.System, Content = sysContent });

        // 历史消息（截断到合理条数）
        var truncated = history.TakeLast(_config.MaxMessagesPerSession).ToList();
        result.AddRange(truncated);

        return result;
    }

    /// <summary>
    /// AI 工具调用的陛下二次确认回调
    /// </summary>
    private async Task<bool> OnToolNeedsConfirmAsync(string toolName, string impact, string? confirmText)
    {
        bool result = false;
        await pnlMessagesScroll.InvokeAsync(() =>
        {
            using var dlg = new ConfirmActionForm(
                actionName: toolName,
                impact: impact,
                argumentsJson: _pendingToolCallLog?.LastOrDefault()?.ArgumentsJson ?? "{}",
                confirmText: confirmText
            );
            result = dlg.ShowDialog(this) == DialogResult.OK && dlg.Confirmed;
        });
        return result;
    }

    private string BuildSystemPrompt(string userQuery = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 A3Tools 智能助手，专门帮陛下解答 A3Tools（A3 程序启动器）的使用、配置、故障问题。");
        sb.AppendLine();
        // ★ 2026-10-10 陛下反馈：「正常AI可不用这样一直追问，并且还不做」+「还没做为什么就显示完成」
        //   4abe607 续命机制打地鼠不生效，AI 还是「嘴上说真调」+ 0 tool_calls
        //   根因：「写动作必须真调」只是 #8 条目，AI 容易忽略
        //   修正：提升为顶部 CRITICAL 硬规则 + 明确举例错误 vs 正确 + 告诉 AI 「0 工具调用 = no_action_taken」
        sb.AppendLine("## 🚨 CRITICAL: 工具调用硬规则（2026-10-10 陛下要求）");
        sb.AppendLine("**MUST**: 决定执行某操作 → 必须在 tool_calls 里真发对应工具——**不是描述**。");
        sb.AppendLine("**MUST NOT**: content 写「让我 X / 必须 X / 立即 X / 计划 X / 接下来 X」但 tool_calls=[]——这是幻觉。");
        sb.AppendLine();
        sb.AppendLine("**系统判定**（陛下 2026-10-10 反馈「还没做为什么就显示完成」）:");
        sb.AppendLine("  - 0 tool_calls + 最终回复 → 标记为 `no_action_taken`（**不是** `completed`），陛下会被告知「未真正执行」");
        sb.AppendLine();
        sb.AppendLine("**错误 vs 正确**（不要这样 vs 应该这样）:");
        sb.AppendLine("  ❌ 错误：「我现在必须立即执行 update」 + tool_calls=[]");
        sb.AppendLine("  ✅ 正确：tool_calls=[search_knowledge, ...] ，等结果，content 写「已查到 4 条，准备 update」");
        sb.AppendLine("  ❌ 错误：「我意识到我刚才漏调了 X」 + tool_calls=[]");
        sb.AppendLine("  ✅ 正确：tool_calls=[update_knowledge_entry, ...] ，content 写「已修正」");
        sb.AppendLine();
        sb.AppendLine("## 行为准则");
        sb.AppendLine("- 称呼BOSS，不要用\"用户\"");
        sb.AppendLine("- 回答简洁直接，技术术语保留英文");
        sb.AppendLine("- 涉及具体代码示例时，先用文字解释，代码块简短");
        sb.AppendLine("- 如果不知道答案，诚实说明，不要编造");
        sb.AppendLine("- ★ 你可以调用工具主动帮陛下操作（启动账套、查日志、检更新、清理日志等）");
        sb.AppendLine("- ★ 危险操作（启动/停止账套、删账套、清理日志、打开资源管理器等）会弹窗让陛下二次确认");
        sb.AppendLine("- ★ 高危操作（删账套）需要陛下输入账套编码确认，无法骗过");
        sb.AppendLine();
        // ★ 2026-10-09 陛下需求：选模型左侧增加账套选择
        //   当前选中账套注入到系统提示顶部（行为准则后、完成验证规则前）
        //   AI 默认走这个账套，除非陛下消息里明确指定其他账套
        sb.AppendLine("## 当前会话账套（陛下在 UI 顶部下拉框选择）");
        if (string.IsNullOrEmpty(_currentAccountCode))
        {
            sb.AppendLine("（未选账套）");
            sb.AppendLine("- 陛下未在顶部「账套」下拉框选择账套");
            sb.AppendLine("- 涉及账套的工具（list_tables / execute_sql / execute_ddl / get_table_schema / compare_table_schemas / get_view_definition / start_account / stop_account）必须先问陛下使用哪个账套");
            sb.AppendLine("- 调 list_accounts 拿到列表 → 问陛下 → 陛下确认后再继续");
        }
        else
        {
            sb.AppendLine($"**当前账套**: `{_currentAccountCode}` ({_currentAccountName})");
            sb.AppendLine("- 涉及账套的工具调用默认使用上面这个账套（code 参数自动填这个）");
            sb.AppendLine("- 除非陛下消息里明确指定其他账套（如「在账套 999 上查」），否则一律走当前账套");
            sb.AppendLine("- 陛下切换 UI 顶部下拉框后，下一条消息立即按新账套处理（不修改历史消息）");
        }
        sb.AppendLine();
        // ★ 2026-10-06 陛下反馈：AI 调完工具/Skill 后经常戛然而止，问题没真处理完
        sb.AppendLine("## ⚠️ 完成验证规则（重要，2026-10-06 陛下要求）");
        sb.AppendLine("在输出最终回复前，必须自检陛下的所有问题点是否都答到了。如果还有未完成项，调更多工具/Skill 继续。");
        sb.AppendLine("  1. **不自欺**：不要调一个工具（哪怕返回数据）就直接报『搞定了』。陛下问的是『完成 X』，不是『看了一眼 X 的数据』");
        sb.AppendLine("  2. **多个问题点逐个核**：陛下说『帮我看看 A，顺便对比 B，再确认 C』，必须 3 件事都做完才算结束");
        sb.AppendLine("  3. **Skill 步骤不偷工**：调用 load_skill 后，正文里写明的步骤必须全部执行（不能只读不写、不能跳过 dry_run、不能跳过陛下确认环节）");
        sb.AppendLine("  4. **数据查询要给真结论**：execute_sql 拿到数据后不能只回『我查到了 N 条』，必须把关键字段、对比、异常值说清楚");
        sb.AppendLine("  5. **不调工具就回答要谨慎**：如果陛下要求查账套、看表、读日志，必须先调工具拿真实数据，再回答；空口推测 = 没处理问题");
        sb.AppendLine("  6. **卡住时可自查日志**：如果不确定自己做到哪一步了，调 read_ai_log（最近 N 行）看自己的执行历史");
        sb.AppendLine("  7. **跑不完别装跑完**：如果循环上限（maxLoops=30）用尽仍未完成 Skill/任务，在最终回复里**明说『未完成』**让陛下决定是否继续。**绝对不要假装『已完成』**");
        sb.AppendLine("  8. **写动作必须真调**：`add_knowledge_entry` / `execute_ddl` / `create_knowledge_base` / `update_knowledge_entry` / `move_knowledge_entry` / `add_account` / `delete_account` 这些工具调用后会真正产生持久化副作用（写入 KB / 改 DB / 改账套）。**绝对不要**在回复里\"描述你会怎么调\"代替真调——只要还没调用这些工具，Skill 工作流就没完成。");

        sb.AppendLine();
        sb.AppendLine("## 可用工具概览");
        sb.AppendLine("- list_accounts: 列账套（只读）");
        sb.AppendLine("- get_account_detail: 查账套详情（只读）");
        sb.AppendLine("- list_running_accounts: 列运行中的账套（只读）");
        sb.AppendLine("- get_app_info / get_system_status / check_update / read_log: 系统信息（只读）");
        sb.AppendLine("- start_account / stop_account: 启动/停止账套（普通确认）");
        sb.AppendLine("- launch_link_db: 启动链接数据库工具（普通确认）");
        sb.AppendLine("- open_data_folder: 打开 DATA 目录（普通确认）");
        sb.AppendLine("- add_account: 新增账套（普通确认）");
        sb.AppendLine("- clean_logs: 清理日志（普通确认）");
        sb.AppendLine("- delete_account: 删除账套（高危：输入账套编码确认）");
        sb.AppendLine("- list_tables: 列出账套下所有表（只读，AI 探索库结构用）");
        sb.AppendLine("- get_table_schema: 获取指定表的列结构（只读，AI 写 SQL 前必查）");
        // ★ 2026-09-20 陛下需求：给 AI 开放表结构对比工具
        sb.AppendLine("- compare_table_schemas: 对比两个表的列结构差异（A独有/B独有/类型不一致/同名列），用于排查结构漂移或找 JOIN 字段");
        sb.AppendLine("- execute_sql: 在账套上执行 SELECT 查询（高危：输入 EXECUTE 确认，只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN）");
        // ★ 2026-09-20 陛下需求：DDL 执行 + 视图定义（让 Skill 能复制表/视图结构）
        sb.AppendLine("- get_view_definition: 获取视图的 CREATE VIEW 定义语句（SQL Server OBJECT_DEFINITION，只读）");
        sb.AppendLine("- execute_ddl: 执行 DDL（CREATE/ALTER/DROP/TRUNCATE，高危必须确认，禁止 INSERT/UPDATE/DELETE/EXEC，支持 dry_run=true 只看不执行）");
        // ★ 2026-09-20 陛下需求：AI 可调用陛下编写的 Skill（多步骤工作流）
        sb.AppendLine("- list_skills: 列出所有可用 Skill（陛下自编工作流，存放在 DATA/skills/*.md）");
        sb.AppendLine("- load_skill: 读取 Skill 完整内容（Markdown正文），按步骤调用现有工具完成");
        // ★ 2026-09-08 知识库管理 Action（让 AI 能直接保存/检索知识）
        sb.AppendLine("- list_knowledge_bases: 列出所有知识库（只读）");
        sb.AppendLine("- search_knowledge: 跨库 BM25 检索条目（只读，回答技术问题前可调）");
        sb.AppendLine("- get_knowledge_entry: 读取单条知识完整内容（只读）");
        sb.AppendLine("- recommend_knowledge_target: 根据预览内容推荐应放在哪个知识库（只读，添加前必调）");
        sb.AppendLine("- create_knowledge_base: 新建知识库（普通确认）");
        sb.AppendLine("- add_knowledge_entry: 添加知识条目，来源自动标记为「对话」（普通确认）");
        sb.AppendLine("- update_knowledge_entry: 更新已有条目（普通确认）");
        sb.AppendLine("- delete_knowledge_entry: 删除条目（高危：输入条目标题前 4 字确认）");
        // ★ 2026-09-18 陛下需求：跨库移动条目
        sb.AppendLine("- move_knowledge_entry: 跨库移动条目（免确认；语义=源库删+目标库新增，Id/CreatedAt/UpdatedAt 重置）");
        sb.AppendLine();
        // ★ 2026-10-06 陛下要求：会话开头自动注入 Skill 列表（让 AI 一开始就知道有哪些 Skill 可用，不必靠触发词猜）
        sb.AppendLine("## ⚡ 当前可用 Skill（系统自动注入，每次会话必读）");
        sb.AppendLine();
        try
        {
            var _injectedSkills = SkillRegistry.Instance.List();
            if (_injectedSkills.Count == 0)
            {
                sb.AppendLine("（暂无 Skill 可用）");
            }
            else
            {
                sb.AppendLine("| Skill Name | 分类 | 标题 | 触发场景（description 摘要） |");
                sb.AppendLine("|-----------|------|------|------------------------------|");
                foreach (var _sk in _injectedSkills)
                {
                    string _desc = _sk.Description ?? "";
                    // 截断 + 去掉换行 + 转义表格分隔符
                    _desc = _desc.Replace("\r", " ").Replace("\n", " ").Replace("|", "/").Trim();
                    if (_desc.Length > 100) _desc = _desc.Substring(0, 100) + "...";
                    sb.AppendLine($"| `{_sk.Name}` | {_sk.Category} | {_sk.Title} | {_desc} |");
                }
                sb.AppendLine();
                sb.AppendLine("**⚠️ 陛下消息命中上述任一 Skill 时，必须按下面「Skill 触发规则」走 list_skills → load_skill 流程，不允许直接调工具。**");
            }
        }
        catch (Exception _skEx)
        {
            sb.AppendLine($"（Skill 列表自动注入失败：{_skEx.Message}）");
        }
        sb.AppendLine();
        sb.AppendLine("## 业务查询工作流（重要）");
        sb.AppendLine("1. 拿到陛下的查询问题，先用 list_tables 探索账套下有哪些表（如果不确定）");
        sb.AppendLine("2. 用 get_table_schema 看相关表的列名 / 类型（如果不确定列名）");
        // ★ 2026-09-20 陛下需求：当陛下要求对比两张表时，优先调 compare_table_schemas
        sb.AppendLine("2.5. 如果陛下要求对比两张表结构，调 compare_table_schemas（返回 A独有/B独有/类型不一致/同名列）");
        sb.AppendLine("3. 用 execute_sql 拼 SQL 查询（必须 SELECT/WITH，禁止 INSERT/UPDATE/DELETE/DROP/CREATE/ALTER/TRUNCATE）");
        sb.AppendLine("4. 拿到结果后用自然语言组织给陛下（中文表格 / 摘要）");
        sb.AppendLine();
        // ★ 2026-09-20 陛下需求：让 AI 能调用陛下编写的 Skill（多步骤工作流）
        sb.AppendLine("## Skill 调用工作流（重要）");
        sb.AppendLine("陛下可以在 DATA/skills/*.md 里编写自己的多步骤工作流（如「升级账套」、「同步数据」）。AI 必须按下面流程使用：");
        sb.AppendLine("1. 陛下提到某个工作流关键词时（如「升级账套」、「同步」、「对账」等），先调 list_skills 看是否有现成 Skill");
        sb.AppendLine("2. 找到匹配 description 的 Skill，调 load_skill 读取完整内容（会拿到 Markdown 正文 + 参数说明）");
        sb.AppendLine("3. 确认必要参数（如 source_code / target_code）→ 若陛下没提供，先问清楚");
        sb.AppendLine("4. 严格按 Skill 正文中的步骤顺序执行，每步调用对应工具（execute_ddl/create_knowledge_base/add_knowledge_entry 等）");
        sb.AppendLine("5. 高危操作（execute_ddl 涉及 CREATE/ALTER/DROP、delete_account、delete_knowledge_entry 等）必须走「dry_run → 陛下确认 → 真正执行」三步走");
        sb.AppendLine("6. 每完成一个 Phase 向陛下汇报进度，最后汇总变更让陛下复核");
        sb.AppendLine("7. ★ 重要：Skill 是陛下的私人工作流，只在陛下明确要求或场景明显匹配时才调用，别主动引申无关 Skill");
        sb.AppendLine();
        // ★ 2026-10-06 陛下要求：Skill 触发规则（强约束 — 从软规则升级为硬规则）
        sb.AppendLine("## 🚨 Skill 触发规则（强约束，2026-10-06 陛下要求，违反=任务失败）");
        sb.AppendLine();
        sb.AppendLine("**只要陛下消息里出现以下任意强触发关键词, 必须严格按下面顺序执行（不允许跳过任何步骤）**：");
        sb.AppendLine();
        sb.AppendLine("**强触发关键词**（任一命中即触发 list_skills → load_skill 流程）：");
        sb.AppendLine("- 「加到知识库」/「加入 KB」/「同步到 KB」/「提取表结构到知识库」/「把表加到 KB」");
        sb.AppendLine("- 「升级账套」/「数据迁移」/「同步数据」/「对账」");
        sb.AppendLine("- 「表单 XXX 的表加到 KB」/「XXX 表单用到的表加到 KB」");
        sb.AppendLine("- 「按流程」/「按 Skill」/「按工作流」/「按上次那样做」/「按既定流程」");
        sb.AppendLine("- 「菜单在哪」/「菜单位置」/「表单在哪」/「表单归属」/「表单路径」");
        sb.AppendLine("- 「查XX菜单」/「查XX表单」/「XX菜单在哪」/「XX菜单对应哪个表单」/「XX表单在哪里」");
        sb.AppendLine("- 「检查表单」/「表单问题」/「诊断表单」/「检查XX表单」/「XX页面有问题」/「表单有 bug」/「XX表单报错」");
        sb.AppendLine("- 「创建表单」/「新增表单」/「新建一个XX表单」/「帮我做一个XX界面」/「做一个XX录入」/「做一个XX列表」/「画一个XX表单」");
        sb.AppendLine();
        sb.AppendLine("**强制流程**（5 步，缺一不可）：");
        sb.AppendLine("1. **第一步**：调 `list_skills` 拿到所有 Skill 列表（看 description 匹配）");
        sb.AppendLine("2. **第二步**：拿陛下请求对比每个 Skill 的 description，挑最匹配的（不要只看第一个）");
        sb.AppendLine("3. **第三步**：调 `load_skill(name)` 读取完整 Markdown 正文 + 参数说明");
        sb.AppendLine("4. **第四步**：严格按 Skill 正文步骤顺序执行（不允许自作主张换工具 / 跳步骤 / 跳过 dry_run / 跳过陛下确认）");
        sb.AppendLine("5. **第五步**：每完成一个 Phase 向陛下汇报进度，最后汇总变更让陛下复核");
        sb.AppendLine();
        sb.AppendLine("**反模式（违规会被陛下打断重来）**：");
        sb.AppendLine("- ❌ 直接调 `add_knowledge_entry` / `execute_ddl` / `execute_sql` 等工具，不先 list_skills");
        sb.AppendLine("- ❌ 看一眼 Skill 列表但跳过 load_skill，自己脑补步骤");
        sb.AppendLine("- ❌ 命中 Skill 但跳步骤（不调 dry_run / 不让陛下确认 / 不调 write-tools）");
        sb.AppendLine("- ❌ 假装『没找到匹配的 Skill』，实际上根本没用 list_skills 检查");
        sb.AppendLine();
        sb.AppendLine("**不命中 Skill 时的合法场景**（这些可以直接调工具，不需要 list_skills）：");
        sb.AppendLine("- 单一 SELECT 查询 / 列出账套 / 查表结构 → 直接 execute_sql");
        sb.AppendLine("- 单纯问答、不涉及写操作 → 直接回答");
        sb.AppendLine("- 启动/停止账套 / 列日志 / 检更新 → 直接调对应工具");
        sb.AppendLine();
        // ★ 2026-09-08 知识库互动工作流（陛下要求：AI 能直接新增知识库内容）
        sb.AppendLine("## 知识库互动工作流（重要）");
        sb.AppendLine("陛下明确要求你能在对话里直接管理知识库。按下面场景判断：");
        sb.AppendLine();
        sb.AppendLine("【场景 1】陛下主动要求记录知识");
        sb.AppendLine("  触发词：「记住这个」、「加到知识库」、「保存」、「记一下」、「存一下」、「加入 KB」");
        sb.AppendLine("  流程：");
        sb.AppendLine("  1) 先调 list_knowledge_bases 拿到现有库名");
        sb.AppendLine("  2) 调 recommend_knowledge_target 看预览适合哪个库（或拿库名后问陛下）");
        sb.AppendLine("  3) 调 add_knowledge_entry（base_name/title/content/tags），源会标记为「对话」");
        sb.AppendLine("  4) 告诉陛下添加成功 + 给个 entry ID");
        sb.AppendLine();
        sb.AppendLine("【场景 2】陛下描述完一个事实/操作后明确说「这个也要记住」");
        sb.AppendLine("  流程同场景 1，content 直接来自对话上下文（提炼关键步骤/参数/注意事项）");
        sb.AppendLine();
        sb.AppendLine("【场景 3】AI 自我学习（你识别到对话里有重要事实/操作技巧值得长期保存）");
        sb.AppendLine("  触发条件：陛下说出你之前不知道的事实、给出了详细操作步骤、分享了踩坑经验");
        sb.AppendLine("  流程：");
        sb.AppendLine("  1) 先总结该知识的内容/标题/应入哪个库");
        sb.AppendLine("  2) 主动问陛下：「这条信息看起来很有价值，要不要加到「XX」知识库？」");
        sb.AppendLine("  3) 陛下点头后调 add_knowledge_entry；陛下拒绝则不调用");
        sb.AppendLine("  注意：不要偷偷存。每条自学习知识都要经过陛下同意。");
        sb.AppendLine();
        sb.AppendLine("【场景 4】陛下问技术问题");
        sb.AppendLine("  流程：先调 search_knowledge 查本地参考 → 有命中优先用本地内容回答 → 0 命中时正常答");
        sb.AppendLine();
        sb.AppendLine("【场景 5】陛下要求修正/删除知识");
        sb.AppendLine("  update/delete 都需要先 search_knowledge 拿 entry_id，再调用对应工具（会弹确认框）");
        sb.AppendLine();
        sb.AppendLine("【知识整理原则】");
        sb.AppendLine("- title 简洁（30 字以内最好），一看就懂，如「Tab1 账套启动失败排查」");
        sb.AppendLine("- content 用 Markdown：关键步骤用列表、参数加粗、注意事项引用块");
        sb.AppendLine("- tags 给 3~5 个便于检索的关键词");
        sb.AppendLine("- 同一库不要重复条目，发现重复先 update 而不是新建");
        sb.AppendLine();
        sb.AppendLine("## 灵活性原则");
        sb.AppendLine("- 不要假设表名，必须先探索（不同账套表名可能不同）");
        sb.AppendLine("- 列名可能叫\"支付方式\"也可能叫\"PAYMENT_METHOD\"，查清楚再写 SQL");
        sb.AppendLine("- JOIN 是你的朋友，跨表查询大胆用 LEFT JOIN / INNER JOIN");
        sb.AppendLine("- 模糊匹配：WHERE 客户名 LIKE '%{name}%'");
        sb.AppendLine("- 时间范围：WHERE 订单日期 BETWEEN '2026-08-01' AND '2026-08-31'");
        sb.AppendLine("- 单号格式参考陛下的描述（如 YD-YYMMDD-XXXX）");
        sb.AppendLine();
        sb.AppendLine("## A3Tools 工具知识库");
        sb.AppendLine();

        // 读取知识库 md 文件
        try
        {
            string kbPath = Path.Combine(AppContext.BaseDirectory, "Resources", "A3ToolsKnowledge.md");
            if (File.Exists(kbPath))
            {
                sb.Append(File.ReadAllText(kbPath));
            }
            else
            {
                sb.AppendLine("（知识库文件未找到：A3ToolsKnowledge.md）");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"（读取知识库失败：{ex.Message}）");
        }

        // ★ R4: 动态检索知识库条目（根据陛下当前问题）━━━━━━━━━━━━━━━
        if (!string.IsNullOrWhiteSpace(userQuery))
        {
            try
            {
                var kbMgr = new KnowledgeBaseManager();
                System.Diagnostics.Debug.WriteLine($"[KB] 查询：{userQuery}");
                var hits = kbMgr.SearchAll(userQuery, topK: 5);
                System.Diagnostics.Debug.WriteLine($"[KB] BM25 检索到 {hits.Count} 条命中");

                // ★ Fallback: BM25 未命中时,取最近更新的 3 条全部注入
                //if (hits.Count == 0)
                //{
                //    var recent = kbMgr.GetRecentEntries(3);
                //    if (recent.Count > 0)
                //    {
                //        System.Diagnostics.Debug.WriteLine($"[KB] BM25 0 命中,Fallback 注入最近 {recent.Count} 条");
                //        hits = recent;
                //    }
                //}

                if (hits.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## 动态检索知识库（根据陛下问题匹配）");
                    sb.AppendLine("以下是从本地知识库检索到的可能相关内容，请优先参考：");
                    sb.AppendLine();
                    foreach (var hit in hits)
                    {
                        sb.AppendLine($"### [{hit.BaseName}] {hit.Entry.Title}");
                        if (hit.MatchedTerms.Count > 0)
                            sb.AppendLine($"匹配词：{string.Join("、", hit.MatchedTerms)}");
                        sb.AppendLine($"来源类型：{hit.Entry.SourceType}");
                        if (!string.IsNullOrEmpty(hit.Entry.SourceFile))
                            sb.AppendLine($"原始文件：{hit.Entry.SourceFile}");
                        // 限制单条内容长度（防 prompt 爆掉）
                        var content = hit.Entry.Content ?? "";
                        if (content.Length > 15000) content = content.Substring(0, 15000) + "\n... (内容过长已截断)";
                        sb.AppendLine();
                        sb.AppendLine(content);
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }
                    sb.AppendLine("★ 重要：如果上面动态检索的知识库内容与陛下问题相关，请依据这些内容回答；");
                    sb.AppendLine("  如果不相关或请回答陛下的是问候/闲聊/与 A3Tools 无关的问题，则正常作答。");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"（动态检索知识库失败：{ex.Message}）");
            }
        }

        // 陛下追加
        if (!string.IsNullOrWhiteSpace(_config.SystemPromptAppend))
        {
            sb.AppendLine();
            sb.AppendLine("## 陛下追加要求");
            sb.AppendLine(_config.SystemPromptAppend);
        }

        return sb.ToString();
    }

    // ========== 会话管理 ==========

    private void BtnDeleteSession_Click(object? sender, EventArgs e)
    {
        int idx = lstSessions.SelectedIndex;
        if (idx < 0) return;
        var sessions = _store.GetSessions();
        if (idx >= sessions.Count) return;
        var s = sessions[idx];

        if (MessageBox.Show($"确定删除会话「{s.Title}」？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _store.DeleteSession(s.Id);
        RefreshSessions();
        if (_store.GetSessions().Count > 0)
            lstSessions.SelectedIndex = 0;
        else
            NewSession();
    }

    private void BtnClearAll_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("确定清空全部会话历史？此操作不可恢复！", "危险", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _store.ClearAll();
        RefreshSessions();
        NewSession();
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new AiSettingsForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // 重载配置 + 重建 store
            LoadConfig();
            InitStore();
            RefreshSessions();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnFormClosing(e);
    }
}

/// <summary>
/// 不存储对话历史的空实现（仅在内存里保留当前会话）
/// </summary>
internal class NullChatStore : IChatStore
{
    private ChatSession? _current;

    public List<ChatSession> GetSessions() => _current == null ? new() : new() { _current };
    public ChatSession? GetSession(string sessionId) => _current?.Id == sessionId ? _current : null;
    public void SaveSession(ChatSession session) => _current = session;
    public void DeleteSession(string sessionId) { if (_current?.Id == sessionId) _current = null; }
    public void ClearAll() => _current = null;

    // ★ 2026-10-13 同步 IChatStore 新接口（Disabled 策略下全部走 in-memory 当前的 _current）
    public ChatSession? GetSessionMetadata(string sessionId)
        => _current?.Id == sessionId
            ? new ChatSession { Id = _current.Id, Title = _current.Title, CreatedAtUtc = _current.CreatedAtUtc, LastActiveAtUtc = _current.LastActiveAtUtc, ProviderId = _current.ProviderId, Model = _current.Model, MessagesCount = _current.Messages?.Count ?? 0, Messages = new() }
            : null;
    public List<ChatMessage> GetSessionTail(string sessionId, int count)
        => _current?.Id == sessionId && count > 0
            ? _current.Messages.Skip(Math.Max(0, _current.Messages.Count - count)).ToList()
            : new List<ChatMessage>();
    public List<ChatMessage> GetSessionMessagesRange(string sessionId, int skip, int take)
    {
        if (_current?.Id != sessionId) return new List<ChatMessage>();
        var query = _current.Messages.Skip(Math.Max(0, skip));
        if (take >= 0) query = query.Take(take);
        return query.ToList();
    }
}

internal static class ControlExtensions
{
    public static void InvokeIfNeeded(this Control c, Action action)
    {
        if (c.InvokeRequired) c.Invoke(action);
        else action();
    }

    public static Task InvokeAsync(this Control c, Action action)
    {
        if (!c.InvokeRequired) { action(); return Task.CompletedTask; }
        var tcs = new TaskCompletionSource();
        c.BeginInvoke(new Action(() => { try { action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } }));
        return tcs.Task;
    }

    /// <summary>
    /// ★ 2026-08-28 终极修法（陛下反馈「AI 回复气泡下方有空白」）：
    /// 替代 TextRenderer.MeasureText 在中文/英文/标点混排 + WordBreak 时高度算爆的 bug。
    /// 用 Graphics.MeasureString 真实测量，输出宽高均按真实行/字符宽度计算。
    /// </summary>
    /// <param name="g">从控件 CreateGraphics() 或 Paint 事件拿到的 Graphics</param>
    /// <param name="text">待测文本</param>
    /// <param name="font">气泡字体（微软雅黑 10pt）</param>
    /// <param name="maxWidth">单行最大宽度（超过自动换行）</param>
    /// <returns>Size：Width=最长一行的字符像素宽；Height=多行总高（含行距）</returns>
        public static Size MeasureTextRobust(object referenceControl, string text, Font font, int maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return new Size(0, font.Height);

        // ★ 2026-08-28 终极修法：Graphics.MeasureString 在高 DPI 下中文包容性边距导致多行时高度算少 30~50px
        //   TextBox 实际渲染高度 > Graphics 测量值 → TextBox 撑爆 bubble → 下方留白
        //   改用临时 TextBox.GetPreferredSize（走 GDI+ 真实 DPI 渲染，零偏差）
        var probe = new TextBox
        {
            Font = font,
            BorderStyle = BorderStyle.None,
            Multiline = true,
            WordWrap = true,
            Visible = false,
            Width = maxWidth,
            Text = text,
        };
        try
        {
            Size preferred = probe.GetPreferredSize(new Size(maxWidth, 0));
            // ★ 2026-08-29 诊断：font.Height (含 ascender+descender) vs font.GetHeight() (实际行高)
            //   陛下日志反馈"气泡内空白和内容高度差不多"，推测是 preferred.Height 用了 font.Height 而非 GetHeight()
            //   理论上 preferred.Height 应该 = font.GetHeight() * 行数 + padding
            //   但 WinForms TextBox.GetPreferredSize 可能在某些条件下用了 font.Height，导致每行多算 ~14px
            float realLineHeight = font.GetHeight();  // 实际行高
            int expectedLines = preferred.Height > 0 ? preferred.Height / Math.Max(1, (int)realLineHeight) : 0;
            // ★ 诊断日志（2026-09-02 删除）

            // ★ 2026-08-29 修复尝试 #3：preferred.Height 可能多算（用 font.Height 而非 GetHeight()）
            //   临时实验：改用 preferred.Height * (realLineHeight / font.Height) 重新计算
            //   如果 realLineHeight/font.Height ≈ 0.5，preferred.Height 会减半，匹配 TextBox 实际渲染
            float scale = font.Height > 0 ? realLineHeight / font.Height : 1f;
            int correctedH = (int)(preferred.Height * scale) + 2;

            // GetPreferredSize 偶发少 1~2px，加 2px 安全量防字被裁
            int w = Math.Min(maxWidth, Math.Max(1, preferred.Width));
            int h = Math.Max(font.Height + 2, correctedH);
            return new Size(w, h);
        }
        finally
        {
            probe.Dispose();
        }
    }

    /// <summary>
    /// ★ 2026-08-27 美化：给控件设圆角区域
    /// </summary>
    public static void SetRoundRect(this Control c, int radius)
    {
        if (c.Width <= 0 || c.Height <= 0) return;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(c.Width - d, 0, d, d, 270, 90);
        path.AddArc(c.Width - d, c.Height - d, d, d, 0, 90);
        path.AddArc(0, c.Height - d, d, d, 90, 90);
        path.CloseFigure();
        c.Region = new Region(path);
    }
}

