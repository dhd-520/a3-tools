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
using A3Tools.Services.ChatStore;
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

    private AiChatConfig _config = new();
    private IChatStore _store = null!;
    private ChatSession _currentSession = null!;
    private AiProviderConfig? _currentProvider;
    private CancellationTokenSource? _cts;
    private bool _isSending = false;
    private List<ChatToolCallRecord>? _pendingToolCallLog;

    public AiChatForm()
    {
        InitializeComponent();
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
        Resize += (_, _) => ScrollToBottom();
        pnlMessagesScroll.Resize += (_, _) => ScrollToBottom();

        // ★ 2026-08-27 修陛下反馈「打开聊天框后不自动滚动到最下方」：
        //   窗体完全显示后 + layout 完成后才滚到底（构造函数里 RenderMessages 时控件还没 layout 完，Maximum=0 滚不动）
        Shown += (_, _) => ScrollToBottom();

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
        RenderMessages();
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

    private void RenderMessages()
    {
        flpMessages.Controls.Clear();
        if (_currentSession == null) return;

        // 先同步宽度
        SyncMessagesWidth();

        // ★ 2026-08-29 方案 A：陛下原话「不能根据内容自动撑开么」
        //   flpMessages.AutoSize=true + Dock=Top → 按内容自动撑高
        //   pnlMessagesScroll.AutoScroll=true → 负责滚动条
        //   完全不需要算高度 / y / AutoScrollMinSize
        foreach (var msg in _currentSession.Messages)
        {
            var bubble = CreateBubbleRow(msg, source: "Render");
            flpMessages.Controls.Add(bubble);
        }

        // ★ 2026-08-29 诊断日志：加载历史/重画时记录 flp 高度 vs 滚动容器高度
        System.Diagnostics.Debug.WriteLine(
            $"[RenderDone] msgCount={_currentSession.Messages.Count} flpH={flpMessages.Height} pnlScrollClientH={pnlMessagesScroll.ClientSize.Height} " +
            $"(溢出需要滚动={flpMessages.Height > pnlMessagesScroll.ClientSize.Height})");

        ScrollToBottom();
    }

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
            var html = Markdown.ToHtml(displayContent ?? string.Empty, _mdPipeline);
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in html)
            {
                if (c == '<') inTag = true;
                else if (c == '>') { inTag = false; sb.Append(' '); }
                else if (!inTag) sb.Append(c);
            }
            displayContent = DecodeHtmlEntities(sb.ToString()).Trim();
        }
        else
        {
            displayContent = (displayContent ?? string.Empty).Trim();
        }

        // ★★★ Label 直接当气泡（AutoSize=true 100% 可靠）
        // 颜色：根据角色选 2 种颜色用于线性渐变（顶亮 → 底暗）
        Color bubbleTop, bubbleBottom, borderColor, textColor;
        if (isError)
        {
            bubbleTop = System.Drawing.Color.FromArgb(254, 242, 242);
            bubbleBottom = System.Drawing.Color.FromArgb(248, 220, 220);
            borderColor = System.Drawing.Color.FromArgb(220, 80, 80);
            textColor = System.Drawing.Color.FromArgb(180, 30, 30);
        }
        else if (isUser)
        {
            // 用户气泡：蓝色渐变
            bubbleTop = System.Drawing.Color.FromArgb(64, 169, 255);   // 亮蓝
            bubbleBottom = System.Drawing.Color.FromArgb(24, 144, 255);  // 深蓝
            borderColor = System.Drawing.Color.FromArgb(15, 110, 220);
            textColor = System.Drawing.Color.White;
        }
        else
        {
            // AI 气泡：白色渐变
            bubbleTop = System.Drawing.Color.FromArgb(252, 252, 252);  // 接近白
            bubbleBottom = System.Drawing.Color.FromArgb(238, 238, 238); // 浅灰
            borderColor = System.Drawing.Color.FromArgb(220, 220, 220);
            textColor = System.Drawing.Color.FromArgb(50, 50, 50);
        }

        var bubble = new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(bubbleMaxW, int.MaxValue),
            // ★ 2026-08-29 修复「四角黑色残留」：BackColor 设为父背景色 White，
            //   g.Clear 才能真的清除干净；圆角外的部分跟外背景同色看不到
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
        bubble.Paint += (sender, e) =>
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
        bubble.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (!ModifierKeys.HasFlag(Keys.Control)) return;
            if (string.IsNullOrEmpty(bubble.Text)) return;
            Clipboard.SetText(bubble.Text);
            // ★ 给个反馈：气泡闪一下（临时改变描边颜色 150ms）
            //   由于 BackColor 已设为透明 + Paint 里手画，修改 BackColor 无效
            //   这里直接订阅一次性 Paint 事件画亮色边框
            System.Action<System.Drawing.Color, System.Drawing.Color> flashOnce = null;
            flashOnce = (origTop, origBottom) =>
            {
                bubble.Paint += (s, pe) =>
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
                bubble.Invalidate();
                var t = new System.Windows.Forms.Timer { Interval = 150 };
                t.Tick += (_, _) =>
                {
                    bubble.Invalidate();  // 触发原 Paint 重画（背景画返回原色）
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            };
            flashOnce(bubbleTop, bubbleBottom);
        };

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

        // ★ 诊断日志（验证用，测试后可删）
        System.Diagnostics.Debug.WriteLine(
            $"[Bubble:{msg.Role}|{source}] contentLen={displayContent?.Length ?? 0} " +
            $"flpClientW={flpClientW} bubbleMaxW={bubbleMaxW} bubble=({bubble.PreferredSize.Width}x{bubble.PreferredSize.Height}) " +
            $"avatar={(avatar?.PreferredSize.Width ?? 0)}x{(avatar?.PreferredSize.Height ?? 0)}");

        return row;
    }

    /// <summary>
    /// ★ 2026-08-29 抽出的布局方法：重新计算 row 高度、头像位置、气泡位置
    ///   流式输出时 bubble.Text 变化不会触发 row.SizeChanged，需要手动调用
    /// </summary>
    private void LayoutBubbleRow(Panel row, Label bubble, Label? avatar, bool isUser)
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
    /// </summary>
    private void AddMessageBubbleNew(ChatMessage msg)
    {
        if (_currentSession == null) return;
        var row = CreateBubbleRow(msg, source: "AddNew");
        flpMessages.Controls.Add(row);
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
        Size actualPreferred = tb.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));
        System.Diagnostics.Debug.WriteLine(
            $"[TextBoxSize] setH={h} preferredH={actualPreferred.Height} preferredW={actualPreferred.Width} (Δ={actualPreferred.Height - h})");
        return tb;
    }

    private static string DecodeHtmlEntities(string s)
    {
        return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&")
                .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
    }

    private void ScrollToBottom()
    {
        // ★ 2026-08-29 方案 A：现在滚动容器是 pnlMessagesScroll（不是 splitChat.Panel1）
        //   pnlMessagesScroll 装 flpMessages，flpMessages 装气泡 row
        //   滚到底 = pnlMessagesScroll.VerticalScroll.Value = Maximum
        if (!pnlMessagesScroll.IsHandleCreated) return;

        pnlMessagesScroll.InvokeIfNeeded(() =>
        {
            // ★ 强制 layout，让 flpMessages 高度最新
            pnlMessagesScroll.PerformLayout();
            flpMessages.PerformLayout();
            int max = pnlMessagesScroll.VerticalScroll.Maximum;
            if (max > 0)
            {
                pnlMessagesScroll.VerticalScroll.Value = max;
            }
        });
    }

    // ========== 发送消息 ==========

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
                    placeholderAi.Content = $"调用工具：{toolCallLog.Last().ToolName}\n结果：{summary}";
                    RenderMessages();
                });
                return true;
            };

            var turnResult = await _backend.SendWithToolsAsync(_currentProvider, messagesForApi, toolCallLog, onToolExecuted, OnToolNeedsConfirmAsync, _cts.Token);

            // AI 返回最终文本 → 替换占位 → 流式显示
            await StreamDisplayAsync(placeholderAi, turnResult.FinalContent);
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
            RenderMessages();
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
            RenderMessages();
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
    private async Task StreamDisplayAsync(ChatMessage aiMsg, string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            fullText = "（AI 没有返回内容）";
        }

        _currentSession.Messages.Remove(aiMsg);
        aiMsg.Content = string.Empty;
        _currentSession.Messages.Add(aiMsg);

        // 找到这个气泡对应的 Label
        Label? bubble = FindBubbleLabel(aiMsg);
        if (bubble == null)
        {
            // 兜底：直接全量显示
            aiMsg.Content = fullText;
            RenderMessages();
            return;
        }

        // 逐字符流式显示
        var buffer = new StringBuilder();
        foreach (char c in fullText)
        {
            if (_cts?.IsCancellationRequested == true) break;
            buffer.Append(c);
            string currentText = buffer.ToString();
            string display = StripMarkdown(currentText);

            await pnlMessagesScroll.InvokeAsync(() =>
            {
                aiMsg.Content = currentText;
                bubble.Text = display;

                // ★ 2026-08-29 修复「流式输出只显示一行」：
                //   bubble.Text 更新后 bubble.PreferredSize 自动更新，但 row.SizeChanged 不会自动触发
                //   直接调用 LayoutBubbleRow() 重算布局，不依赖 SizeChanged 事件
                var row = bubble.Parent as Panel;
                if (row != null)
                {
                    // ★ 从 row.Tag 或 sender 拿 avatar + isUser（创建 row 时设）
                    Label? avatar = null;
                    bool isUser = false;
                    foreach (Control c in row.Controls)
                    {
                        if (c is Label lbl && lbl != bubble)
                        {
                            avatar = lbl;
                            isUser = lbl.Text == "B";  // 用户头像 = "B"，AI 头像 = "AI"
                            break;
                        }
                    }
                    LayoutBubbleRow(row, bubble, avatar, isUser);
                }

                ScrollToBottom();
            });

            // 速度控制：中文字符间隔 12ms，ASCII 间隔 6ms
            int delay = c > 127 ? 12 : 6;
            await Task.Delay(delay);
        }

        // 最终内容存进 message（带 markdown）
        aiMsg.Content = fullText;
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
    /// ★ 2026-08-29 14:59 陛下要求 AI 气泡支持 Markdown 完整渲染（含表格/代码块）
    ///   复用 UpdateForm.RenderMarkdownAsHtml 的 GitHub CSS 风格
    ///   返回完整 HTML，可直接赋给 WebBrowser.DocumentText
    /// </summary>
    private string RenderMarkdownAsHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        string bodyHtml = Markdown.ToHtml(markdown ?? string.Empty, pipeline);

        // GitHub 风格 CSS（与 UpdateForm 一致，适配 AI 气泡场景字号略小）
        string html = @"<!DOCTYPE html><html><head><meta charset=""utf-8""><style>
body { font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif; font-size: 10pt; line-height: 1.5; color: #24292f; background: transparent; padding: 0; margin: 0; word-wrap: break-word; }
h1, h2, h3, h4, h5, h6 { margin: 14px 0 8px 0; font-weight: 600; line-height: 1.25; }
h1 { font-size: 18px; padding-bottom: 4px; border-bottom: 1px solid #d0d7de; }
h2 { font-size: 16px; padding-bottom: 4px; border-bottom: 1px solid #d0d7de; }
h3 { font-size: 14px; }
p { margin: 0 0 8px 0; }
ul, ol { margin: 0 0 8px 0; padding-left: 22px; }
li { margin: 2px 0; }
blockquote { margin: 0 0 8px 0; padding: 0 10px; color: #57606a; border-left: 4px solid #d0d7de; background: #f6f8fa; }
code { font-family: 'Consolas', monospace; font-size: 9pt; background: rgba(175, 184, 193, 0.2); padding: 1px 4px; border-radius: 4px; }
pre { background: #f6f8fa; padding: 8px 10px; border-radius: 6px; overflow-x: auto; margin: 0 0 8px 0; line-height: 1.45; }
pre code { background: transparent; padding: 0; font-size: 9pt; }
strong { font-weight: 600; }
em { font-style: italic; }
a { color: #0969da; text-decoration: none; }
hr { border: none; border-top: 1px solid #d0d7de; margin: 14px 0; }
table { border-collapse: collapse; margin: 0 0 8px 0; }
table th, table td { border: 1px solid #d0d7de; padding: 4px 10px; }
table th { background: #f6f8fa; font-weight: 600; }
img { max-width: 100%; }
</style></head><body>" + bodyHtml + @"</body></html>";
        return html;
    }

    private List<ChatMessage> BuildApiMessages(List<ChatMessage> history)
    {
        var result = new List<ChatMessage>();

        // System Prompt：拼装知识库 + 陛下自定义追加
        var sysContent = BuildSystemPrompt();
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

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 A3Tools 智能助手，专门帮陛下解答 A3Tools（A3 程序启动器）的使用、配置、故障问题。");
        sb.AppendLine();
        sb.AppendLine("## 行为准则");
        sb.AppendLine("- 称呼陛下，不要用\"用户\"");
        sb.AppendLine("- 回答简洁直接，技术术语保留英文");
        sb.AppendLine("- 涉及具体代码示例时，先用文字解释，代码块简短");
        sb.AppendLine("- 如果不知道答案，诚实说明，不要编造");
        sb.AppendLine("- ★ 你可以调用工具主动帮陛下操作（启动账套、查日志、检更新、清理日志等）");
        sb.AppendLine("- ★ 危险操作（启动/停止账套、删账套、清理日志、打开资源管理器等）会弹窗让陛下二次确认");
        sb.AppendLine("- ★ 高危操作（删账套）需要陛下输入账套编码确认，无法骗过");
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
        sb.AppendLine("- execute_sql: 在账套上执行 SELECT 查询（高危：输入 EXECUTE 确认，只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN）");
        sb.AppendLine();
        sb.AppendLine("## 业务查询工作流（重要）");
        sb.AppendLine("1. 拿到陛下的查询问题，先用 list_tables 探索账套下有哪些表（如果不确定）");
        sb.AppendLine("2. 用 get_table_schema 看相关表的列名 / 类型（如果不确定列名）");
        sb.AppendLine("3. 用 execute_sql 拼 SQL 查询（必须 SELECT/WITH，禁止 INSERT/UPDATE/DELETE/DROP/CREATE/ALTER/TRUNCATE）");
        sb.AppendLine("4. 拿到结果后用自然语言组织给陛下（中文表格 / 摘要）");
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
            System.Diagnostics.Debug.WriteLine(
                $"[MeasureRobust] textLen={text.Length} fontH={font.Height} realLineH={realLineHeight:F1} " +
                $"preferred=({preferred.Width}x{preferred.Height}) expectedLines={expectedLines}");

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
