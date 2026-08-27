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
        Resize += (_, _) => SyncMessagesWidth();
        pnlMessagesScroll.Resize += (_, _) => SyncMessagesWidth();
        splitChat.Panel1.Resize += (_, _) => SyncMessagesWidth();

        // ★ 2026-08-27 修陛下反馈「打开聊天框后不自动滚动到最下方」：
        //   窗体完全显示后 + layout 完成后才滚到底（构造函数里 RenderMessages 时控件还没 layout 完，Maximum=0 滚不动）
        Shown += (_, _) => ScrollToBottom();

        // 立即设真实约束（不等 Shown，避免首帧 RenderMessages 拿到错的 Panel1 宽度）
        splitChat.Panel1MinSize = 200;
        splitChat.Panel2MinSize = 160;
        splitChat.SplitterDistance = Math.Max(200, splitChat.Height - 160 - splitChat.SplitterWidth);

        // ★ 立即调一次 SyncMessagesWidth，确保首次渲染前 pnlMessages.Width 已等于 Panel1 宽度
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
        // ★ 单一宽度源 = splitChat.Panel1.ClientSize.Width（这是 SplitContainer 上下分后的消息区真实宽度）
        // pnlMessages.AutoSize=false，必须手动设 Width 和 Height 才能正确布局
        int vScroll = SystemInformation.VerticalScrollBarWidth;
        int w = splitChat.Panel1.ClientSize.Width - vScroll;
        if (w > 0 && pnlMessages.Width != w)
        {
            pnlMessages.Width = w;
        }
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
        pnlMessages.Controls.Clear();
        if (_currentSession == null) return;

        // 先同步 pnlMessages.Width，避免消息渲染时用旧宽度
        SyncMessagesWidth();

        // ★ 2026-08-27 重写：绝对定位累加 Y 坐标，pnlMessages.AutoSize=false 所以手动设 Height
        int y = 12;
        int totalHeight = 24; // 上下边距各 12

        foreach (var msg in _currentSession.Messages)
        {
            var row = CreateBubbleRow(msg, y);
            y += row.Height + 14;  // ★ row 间距加大
            totalHeight += row.Height + 14;
        }

        // 设 pnlMessages.Height + ScrollableControl.AutoScrollMinSize（触发垂直滚动条出现）
        pnlMessages.Height = totalHeight;
        pnlMessagesScroll.AutoScrollMinSize = new System.Drawing.Size(0, totalHeight);

        ScrollToBottom();
    }

    /// <summary>
    /// 创建一条消息气泡 row（绝对定位）
    /// ★ 2026-08-27 重写：单一宽度源 = pnlMessages.ClientSize.Width（已 SyncMessagesWidth 同步），不再用中间变量 availWidth
    ///   row.Width = pnlMessages.ClientSize.Width，row 撑满消息区
    ///   AI气泡靠左Dock=Left， 用户气泡靠右Dock=Right
    ///   气泡 MaximumSize.Width = rowWidth * 0.85 限制最大宽度
    /// </summary>
    private Panel CreateBubbleRow(ChatMessage msg, int y)
    {
        bool isUser = msg.Role == ChatRole.User;
        bool isError = msg.IsError;
        bool isSystem = msg.Role == ChatRole.System;

        // ★ 单一宽度源 = pnlMessages.ClientSize.Width（已同步，真实宽度）
        int rowWidth = pnlMessages.ClientSize.Width;
        if (rowWidth < 200) rowWidth = 600;

        // ★ 2026-08-27 美化：加大间距，圆角气泡
        const int sidePadding = 24;      // row 左右边距（原来 12）
        const int gap = 10;             // 头像与气泡间隔（原来 8）
        const int verticalPadding = 10;  // row 上下边距（原来 4）
        const int cornerRadius = 14;     // 圆角半径
        const int rowSpacing = 14;       // row 间距（在 RenderMessages 里用了 4）

        // 气泡最大宽度 = row 宽度的 85%（陛下要求加宽）
        int bubbleMaxW = (int)((rowWidth - sidePadding * 2) * 0.85);
        int labelMaxW = bubbleMaxW - 24;  // -24 = 气泡 Padding(12+12)

        // 头像
        var avatar = new Label
        {
            Text = isUser ? "B" : (isError ? "!" : "AI"),
            Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
            AutoSize = true,
            ForeColor = isUser ? System.Drawing.Color.FromArgb(24, 144, 255) : System.Drawing.Color.Gray,
            BackColor = System.Drawing.Color.Transparent,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };

        // 气泡内容 Label
        string displayContent = isSystem ? "" : msg.Content;
        Label contentLabel;
        if (isUser)
        {
            contentLabel = new Label
            {
                Text = displayContent,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(labelMaxW, 10000)
            };
        }
        else
        {
            contentLabel = RenderMarkdownLabel(displayContent, isError, labelMaxW);
        }

        // ★ 关键：Label 保持 AutoSize=true + MaximumSize=labelMaxW，让它自动按 labelMaxW 渲染多行
        //   （之前 AutoSize=false + 手动 Size 会导致 Label 不读 MaximumSize，变成单行渲染被截断）
        //   PreferredSize 只是提示高度，这里不手动设 Size

        // 气泡 Panel
        var bubble = new Panel
        {
            BackColor = isError ? System.Drawing.Color.FromArgb(254, 240, 240)
                  : isUser ? System.Drawing.Color.FromArgb(24, 144, 255)
                  : System.Drawing.Color.FromArgb(245, 245, 245),
            Padding = new Padding(12, 10, 12, 10),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new System.Drawing.Size(bubbleMaxW, 10000)
        };
        bubble.Controls.Add(contentLabel);

        // ★ 2026-08-27 美化：圆角气泡 + 随 AutoSize 实时重设 Region（防止圆角被截断）
        bubble.Resize += (_, _) => bubble.SetRoundRect(cornerRadius);
        bubble.SetRoundRect(cornerRadius);  // 首次设一次

        // ★ 不需要 Add/Remove bubble 自身，只需 GetPreferredSize 触发计算
        //   （之前 bubble.Controls.Add(bubble) 是错误，会引发循环控件引用）

        int bubbleWidth = bubble.PreferredSize.Width;
        int bubbleHeight = bubble.PreferredSize.Height;
        int avatarWidth = avatar.PreferredSize.Width;
        int avatarHeight = avatar.PreferredSize.Height;
        int rowHeight = System.Math.Max(bubbleHeight, avatarHeight) + verticalPadding * 2;

        // ★ row 撑满 pnlMessages
        var row = new Panel
        {
            Location = new System.Drawing.Point(0, y),
            Size = new System.Drawing.Size(rowWidth, rowHeight),
            BackColor = System.Drawing.Color.Transparent
        };

        if (isUser)
        {
            // 靠右（与 AI 镜像）：气泡在左、头像在右
            int avatarX = rowWidth - sidePadding - avatarWidth;
            int bubbleX = avatarX - gap - bubbleWidth;
            bubble.Location = new System.Drawing.Point(bubbleX, verticalPadding);
            avatar.Location = new System.Drawing.Point(avatarX, verticalPadding + (bubbleHeight - avatarHeight) / 2);
        }
        else
        {
            // 靠左：头像在左，气泡在头像右边
            int avatarX = sidePadding;
            int bubbleX = avatarX + avatarWidth + gap;
            avatar.Location = new System.Drawing.Point(avatarX, verticalPadding + (bubbleHeight - avatarHeight) / 2);
            bubble.Location = new System.Drawing.Point(bubbleX, verticalPadding);
        }

        row.Controls.Add(bubble);
        row.Controls.Add(avatar);

        pnlMessages.Controls.Add(row);
        return row;
    }

    /// <summary>
    /// ★ 2026-08-27 统一接口：发消息时也调用 CreateBubbleRow 新代码
    ///   解决了旧 AddMessageBubble 写死 640/600 导致 AI 气泡宽度恒为 600 的问题
    /// </summary>
    private void AddMessageBubbleNew(ChatMessage msg)
    {
        if (_currentSession == null) return;
        SyncMessagesWidth();

        // 拼接到末尾：下一个 row 的 Y = pnlMessages.Height - pnlMessages.Padding.Bottom
        int currentBottom = pnlMessages.Height; // 含 Padding
        int y = currentBottom - pnlMessages.Padding.Bottom;

        var row = CreateBubbleRow(msg, y);

        // 重设总高 + ScrollableControl.AutoScrollMinSize（触发滚动条）
        // ★ 底部预留 80px 缓冲，防止最后一条消息贴底被输入框遮
        int newBottom = row.Bottom + 80;
        if (newBottom > pnlMessages.Height)
        {
            pnlMessages.Height = newBottom;
        }
        pnlMessagesScroll.AutoScrollMinSize = new System.Drawing.Size(0, newBottom);
    }

    private Label RenderMarkdownLabel(string markdown, bool isError, int labelMaxW)
    {
        // 简化渲染：把 Markdown 转成 HTML 再剥出来给 Label（Label 不支持 HTML，所以做最简单的替换）
        var html = Markdown.ToHtml(markdown ?? string.Empty, _mdPipeline);
        // 简单提取文本（去掉 HTML 标签，但保留换行）
        var sb = new StringBuilder();
        bool inTag = false;
        foreach (char c in html)
        {
            if (c == '<') inTag = true;
            else if (c == '>') { inTag = false; sb.Append(' '); }
            else if (!inTag) sb.Append(c);
        }
        var text = DecodeHtmlEntities(sb.ToString()).Trim();

        return new Label
        {
            Text = text,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
            ForeColor = isError ? System.Drawing.Color.FromArgb(180, 30, 30) : System.Drawing.Color.FromArgb(50, 50, 50),
            AutoSize = true,
            // ★ 陛下要求 AI 气泡也撑宽，不再写死 600
            MaximumSize = new System.Drawing.Size(labelMaxW, 10000)
        };
    }

    private static string DecodeHtmlEntities(string s)
    {
        return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&")
                .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
    }

    private void ScrollToBottom()
    {
        // ★ 防御：未创建 Handle 直接返回，避免 BeginInvoke 报「在创建窗口句柄之前，不能在控件上调用 Invoke 或 BeginInvoke」
        if (!pnlMessagesScroll.IsHandleCreated) return;

        pnlMessagesScroll.InvokeIfNeeded(() =>
        {
            // PerformLayout 后再读 Maximum
            pnlMessagesScroll.PerformLayout();
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
        Label? label = FindBubbleLabel(aiMsg);
        if (label == null)
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
                label.Text = display;
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
    /// 找出指定消息对应的 Label（用于流式追加）
    /// </summary>
    private Label? FindBubbleLabel(ChatMessage msg)
    {
        // 倒序找：最后一个 row 的 Label
        for (int i = pnlMessages.Controls.Count - 1; i >= 0; i--)
        {
            if (pnlMessages.Controls[i] is Panel row && row.Controls.Count > 0)
            {
                if (row.Controls[0] is FlowLayoutPanel hbox)
                {
                    foreach (Control c in hbox.Controls)
                    {
                        if (c is Panel bubble && bubble.Controls.Count > 0 && bubble.Controls[0] is Label lbl)
                            return lbl;
                    }
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
        await pnlMessages.InvokeAsync(() =>
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
