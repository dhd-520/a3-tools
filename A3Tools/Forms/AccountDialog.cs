using System;
using System.IO;
using System.Windows.Forms;
using A3Tools.Common.DataAccess;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class AccountDialog : Form
{
    private readonly Account? _original;
    private readonly DataService _dataService = new();

    /// <summary>账套字段显示配置（录入界面的显隐与排序）</summary>
    private readonly AccountFieldStore _fieldStore = new();

    /// <summary>
    /// 自定义字段的运行时控件（2026-10-29 陛下：支持新增自定义字段）。
    /// 内置字段的控件在设计器里；自定义字段没有 → 只能运行时创建（唯一例外）。
    /// </summary>
    private readonly Dictionary<string, (Label Label, TextBox Input)> _customSlots = new();

    /// <summary>当前排版引擎（重排时复用，方便窗体改变大小时重算列宽）</summary>
    private AccountEditorLayout? _layout;

    /// <summary>内容区左右边距（像素）—— 取自设计器模板的运行时真实值（已随 AutoScale 缩放）</summary>
    private int _colMargin = AccountEditorLayout.SideMargin;

    /// <summary>标题列与输入列之间的间距（像素）—— 同上</summary>
    private int _colGap = AccountEditorLayout.ColumnGap;

    /// <summary>是否显示密码明文（Root模式使用）</summary>
    public bool ShowPasswords { get; set; } = false;

    /// <summary>是否是 Root 模式（决定某些 UI 是否可见）</summary>
    public bool IsRootMode { get; set; } = false;

    /// <summary>A3ToolsHub 配置文件目录（来自设置，Root 模式使用）</summary>
    public string HubConfigDir { get; set; } = string.Empty;

    public AccountDialog(Account? account, bool showPasswords = false, bool isRootMode = false, string hubConfigDir = "")
    {
        _original = account;
        ShowPasswords = showPasswords;
        IsRootMode = isRootMode;
        HubConfigDir = hubConfigDir;
        InitializeComponent();
        this.KeyPreview = true;
        this.KeyDown += AccountDialog_KeyDown;
        contentPanel.ClientSizeChanged += ContentPanel_ClientSizeChanged;

        // ★ 边距/间距：从设计器模板（「代码」字段）的真实运行时几何取。
        //   InitializeComponent 结束时窗体已完成 AutoScale（见 AutoScaleDimensions），
        //   所以这两个值已经是缩放后的真实值 —— 用它们算百分比，两类字段必然对齐。
        _colMargin = lblCode.Left;
        _colGap = Math.Max(0, txtCode.Left - (lblCode.Left + lblCode.Width));

        ApplyFieldLayout();
        if (account != null)
            LoadAccount(account);
        else
            GenerateDefaultCode();
        UpdateTitle();
        UpdateProxyGroupVisibility();
    }

    private void GenerateDefaultCode()
    {
        var accounts = _dataService.LoadAccounts();
        int maxCode = 0;
        foreach (var acc in accounts)
        {
            if (int.TryParse(acc.Code, out int code) && code > maxCode)
                maxCode = code;
        }
        this.txtCode.Text = (maxCode + 1).ToString("D4");
    }

    /// <summary>
    /// 按「账套字段设置」里的配置重排录入界面：
    ///   · 字段的显示/隐藏；
    ///   · 字段的上下顺序；
    ///   · 「网页版自动登录」「代理模式」两个分组跟着字段流往下走。
    ///
    /// ★ 不动态创建控件：只用设计器里已经定义好的 Label/输入框，
    ///   控件的 X 坐标/宽高/字体完全由 AccountDialog.Designer.cs 决定（所见即所得）。
    ///
    /// ★ 列宽按百分比算（2026-11-05 陛下）：标题占 1/4、输入框占 3/4，
    ///   内置字段和自定义字段走同一套算法 → 任何分辨率、任何窗体大小都不会错位。
    /// </summary>
    private void ApplyFieldLayout()
    {
        // 字段名 → (标签, 输入框)
        var slots = new Dictionary<string, AccountEditorLayout.Slot>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(Account.Code),           new AccountEditorLayout.Slot(lblCode, txtCode) },
            { nameof(Account.Name),           new AccountEditorLayout.Slot(lblName, txtName) },
            { nameof(Account.Server),         new AccountEditorLayout.Slot(lblServer, txtServer) },
            { nameof(Account.ServerBackup),   new AccountEditorLayout.Slot(lblServerBackup, txtServerBackup) },
            { nameof(Account.ServerPassword), new AccountEditorLayout.Slot(lblServerPassword, txtServerPassword) },
            { nameof(Account.Database),       new AccountEditorLayout.Slot(lblDatabase, txtDatabase) },
            { nameof(Account.DatabaseName),   new AccountEditorLayout.Slot(lblDatabaseName, txtDatabaseName) },
            { nameof(Account.DbUser),         new AccountEditorLayout.Slot(lblDbUser, txtDbUser) },
            { nameof(Account.DbPassword),     new AccountEditorLayout.Slot(lblDbPassword, txtDbPassword) },
            { nameof(Account.RemoteType),     new AccountEditorLayout.Slot(lblRemoteType, cboRemoteType) },
            { nameof(Account.RemoteAddress),  new AccountEditorLayout.Slot(lblRemoteAddress, txtRemoteAddress) },
            { nameof(Account.RemoteUser),     new AccountEditorLayout.Slot(lblRemoteUser, txtRemoteUser) },
            { nameof(Account.RemotePassword), new AccountEditorLayout.Slot(lblRemotePassword, txtRemotePassword) },
            { nameof(Account.Remark),         new AccountEditorLayout.Slot(lblRemark, txtRemark) },
            // 「账套用户名」：普通字段（可排序、可显隐）
            //   2026-10-31 陛下：原来下面跟着一行加粗青色的「🌐 自动登录配置（…）」提示，
            //   但字段可自由排序，一排序它就夹在别的字段中间、看着像那个字段的组表头 → 已去掉。
            //   说明文字改挂到输入框的悬浮提示（Designer 里 toolTipServerUsername）。
            { nameof(Account.ServerUsername), new AccountEditorLayout.Slot(lblServerUsername, txtServerUsername) },
        };

        // 配置已由 Normalize 兜底：密码不进列表、必填不隐藏、排序号连续
        var config = AccountFieldResolver.Normalize(_fieldStore.Load());

        // ★ 自定义字段：设计器里没有对应控件 → 运行时创建（Label + TextBox）
        EnsureCustomFieldControls(config, slots);

        var layout = new AccountEditorLayout(slots, pnlProxyGroup, startY: 20);
        _layout = layout;
        layout.Apply(config);

        // 列宽按比例重算（标题 1/4、输入 3/4）—— 内置/自定义字段统一处理
        ApplyColumnWidths();

        ShrinkToContent();
    }

    /// <summary>
    /// 按内容区实际宽度重算「标题列 + 输入列」的宽度。
    ///
    /// ★ 2026-11-05 陛下：宽度改成百分比显示（标题 1/4、输入 3/4），
    ///   这样换分辨率、换缩放、甚至拖动窗体大小，字段行都保持对齐。
    ///
    /// ★ 内置字段和自定义字段走同一段代码 → 结构上不可能再错位。
    ///   （以前是两套几何来源：内置的来自设计器、自定义的写死一份）
    ///
    /// ★ 边距/间距用的是「设计器模板控件的真实运行时几何」，
    ///   已经被窗体 AutoScale 缩放过 → 任何缩放比例下都跟内置行对齐。
    /// </summary>
    private void ApplyColumnWidths()
    {
        if (_layout == null) return;
        int w = contentPanel.ClientSize.Width;
        if (w <= 0) return;
        _layout.ApplyColumns(w, _colMargin, _colGap);
    }

    /// <summary>
    /// 窗体大小变化时重算列宽（百分比布局 → 跟着一起变）。
    /// </summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyColumnWidths();
    }

    /// <summary>
    /// 内容区宽度变化（含垂直滚动条出现/消失）时重算列宽。
    /// </summary>
    private void ContentPanel_ClientSizeChanged(object? sender, EventArgs e)
    {
        ApplyColumnWidths();
    }

    /// <summary>
    /// ★ 2026-11-05 修复：自定义字段行在不同分辨率/缩放的电脑上与内置字段错位。
    ///
    /// 病根：自定义字段的「标题 + 输入框」是【运行时创建】的（设计器里没有），
    ///   之前把设计器坐标（x=30/250、宽 210/590）硬编码了一份。
    ///   但窗体开了 <c>AutoScaleMode.Font</c> —— 字体/DPI 与设计机不同时，
    ///   关键在缩放的【时序】而不是「谁被缩放」：
    ///   内置控件在 InitializeComponent 阶段创建，随窗体一起被缩放；
    ///   运行时创建的控件若在窗体句柄创建之后才加入（如「运行中新增自定义字段」），
    ///   就完全错过这次缩放 —— 自定义行于是成了「原始像素」，在内置行旁边错位：
    ///   标题后面空出一段（一个标题宽度），输入框的位置和宽度也对不上。
    ///
    /// 修法：不写死像素，改为继承「模板控件」的真实几何 ——
    ///   模板 = 设计器里的「代码」字段（固定字段，永远存在）。
    ///   复制 AutoSize / 尺寸 / 位置 / 对齐 / 字体，
    ///   自定义行就成了内置行的结构克隆，任何缩放下都对齐。
    ///
    ///   ★ 2026-11-05 补充：宽度最终统一由 <c>AccountEditorLayout.ApplyColumns</c>
    ///     按百分比重算（标题 1/4、输入 3/4），内置与自定义字段走同一段代码。
    ///     本方法负责的是 ApplyColumns 不管的属性：高度 / 字体 / 颜色 / 对齐。
    /// </summary>
    private void SyncCustomFieldGeometry()
    {
        var tplLabel = lblCode;      // 模板：设计器里的固定字段
        var tplInput = txtCode;

        foreach (var kv in _customSlots)
        {
            var lb = kv.Value.Label;
            var tb = kv.Value.Input;

            // 标题：先设 AutoSize 再设 Size（AutoSize=true 时 Size 会被忽略）
            lb.AutoSize = tplLabel.AutoSize;
            lb.Size = tplLabel.Size;
            lb.Left = tplLabel.Left;
            lb.TextAlign = tplLabel.TextAlign;
            lb.Font = new Font(tplLabel.Font, tplLabel.Font.Style);   // 复制一份，不共享（共享的 Font 会被一起 Dispose）
            lb.ForeColor = tplLabel.ForeColor;

            // 输入框
            tb.AutoSize = tplInput.AutoSize;
            tb.Size = tplInput.Size;
            tb.Left = tplInput.Left;
            tb.Font = new Font(tplInput.Font, tplInput.Font.Style);
        }
    }

    /// <summary>
    /// 为每个自定义字段准备一对「标签 + 输入框」（运行时创建）。
    /// 已经删掉的字段 → 把旧控件从窗体上摘掉（避免漏在界面里）。
    /// </summary>
    private void EnsureCustomFieldControls(
        List<AccountFieldConfig> config,
        Dictionary<string, AccountEditorLayout.Slot> slots)
    {
        var customs = config.Where(c => !AccountFieldCatalog.IsBuiltIn(c.Field)).ToList();

        // 清理：已经被删除的自定义字段
        foreach (var key in _customSlots.Keys.ToList())
        {
            if (customs.Any(c => c.Field == key)) continue;
            var old = _customSlots[key];
            contentPanel.Controls.Remove(old.Label);
            contentPanel.Controls.Remove(old.Input);
            old.Label.Dispose();
            old.Input.Dispose();
            _customSlots.Remove(key);
        }

        // 新建：缺少的自定义字段
        foreach (var c in customs)
        {
            if (!_customSlots.TryGetValue(c.Field, out var pair))
            {
                // ★ 几何不写死：全部继承设计器里的「代码」字段（模板），
                //   否则缩放/字体不同时会和内置字段错位（见 SyncCustomFieldGeometry 注释）
                var lb = new Label
                {
                    Text = c.DisplayName + "：",
                    AutoSize = lblCode.AutoSize,
                    Size = lblCode.Size,
                    Location = new Point(lblCode.Left, 0),
                    TextAlign = lblCode.TextAlign,
                    Font = new Font(lblCode.Font, lblCode.Font.Style),
                    ForeColor = lblCode.ForeColor,
                };
                var tb = new TextBox
                {
                    AutoSize = txtCode.AutoSize,
                    Size = txtCode.Size,
                    Location = new Point(txtCode.Left, 0),
                    Font = new Font(txtCode.Font, txtCode.Font.Style),
                };
                contentPanel.Controls.Add(lb);
                contentPanel.Controls.Add(tb);
                pair = (lb, tb);
                _customSlots[c.Field] = pair;
            }
            slots[c.Field] = new AccountEditorLayout.Slot(pair.Label, pair.Input);
        }

        // 已存在的控件同样要校准（新建时可能还没缩放，缩放后几何会变）
        SyncCustomFieldGeometry();
    }

    /// <summary>
    /// 窗体在句柄创建时完成 DPI 自动缩放（<c>AutoScaleMode.Font</c>），
    /// 之后设计器控件才是「真实几何」。这里重新排一次，
    /// 让自定义字段拿到缩放后的尺寸/位置，与内置字段完全对齐。
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyFieldLayout();
    }

    /// <summary>
    /// 字段被隐藏后，窗体高度跟着收（只收不放，不会超过设计器里定的高度）。
    /// 否则底部会留一大块空白。
    /// </summary>
    private void ShrinkToContent()
    {
        try
        {
            // 内容底部 = 最后一个可见元素的下沿
            int bottom = 0;
            foreach (Control c in contentPanel.Controls)
            {
                if (!c.Visible) continue;
                int b = c.Top + c.Height;
                if (b > bottom) bottom = b;
            }
            if (bottom <= 0) return;

            int wanted = titleBar.Height + bottom + 20 + footerPanel.Height;
            // 上限用「当前窗体高度」而不是写死的 720 —— 窗体可能被 DPI 缩放过，
            // 写死会在高缩放的机器上把窗体压得过矮
            int maxH = this.ClientSize.Height;
            if (wanted < maxH && wanted >= this.MinimumSize.Height)
                this.ClientSize = new Size(this.ClientSize.Width, wanted);
        }
        catch (Exception)
        {
            // 调整尺寸失败不影响使用，忽略
        }
    }

    private void UpdateTitle()
    {
        bool isEdit = _original != null;
        this.Text = isEdit ? "编辑账套" : "新增账套";
        this.lblTitle.Text = isEdit ? "✏️ 编辑账套" : "➕ 新增账套";
    }

    private void LoadAccount(Account account)
    {
        this.txtCode.Text = account.Code;
        this.txtCode.Enabled = false;
        this.txtName.Text = account.Name;
        this.txtServer.Text = account.Server;
        this.txtServerBackup.Text = account.ServerBackup;
        this.txtServerPassword.Text = account.ServerPassword;
        this.txtDatabase.Text = account.Database;
        this.txtDatabaseName.Text = account.DatabaseName;
        this.txtDbUser.Text = account.DbUser;
        this.txtDbPassword.Text = account.DbPassword;
        this.cboRemoteType.Text = account.RemoteType;
        this.txtRemoteAddress.Text = account.RemoteAddress;
        this.txtRemoteUser.Text = account.RemoteUser;
        this.txtRemotePassword.Text = account.RemotePassword;
        this.txtRemark.Text = account.Remark;

        this.txtServerUsername.Text = account.ServerUsername;

        // 自定义字段的值（2026-10-29）
        foreach (var kv in _customSlots)
        {
            if (account.CustomFields != null && account.CustomFields.TryGetValue(kv.Key, out var cv))
                kv.Value.Input.Text = cv ?? string.Empty;
        }

        // 2026-07-09 代理模式
        if (account.ConnectionMode == DataAccessMode.Http)
            this.rbHttp.Checked = true;
        else
            this.rbDirect.Checked = true;
        this.txtProxySecretKey.Text = account.HttpSecretKey;
        this.txtProxyServerPublicKey.Text = account.HttpServerPublicKey;

        if (ShowPasswords)
        {
            this.txtServerPassword.UseSystemPasswordChar = false;
            this.txtDbPassword.UseSystemPasswordChar = false;
            this.txtRemotePassword.UseSystemPasswordChar = false;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(this.txtCode.Text))
        {
            MessageBox.Show("代码不能为空！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.txtCode.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(this.txtName.Text))
        {
            // 账套名称可能被用户隐藏了 → 提示去哪里恢复，否则用户会看到「看不到的必填项」
            bool hidden = !this.lblName.Visible;
            MessageBox.Show(hidden
                    ? "账套名称不能为空！\n\n（该字段已被隐藏，请到「文件 → 账套字段设置」恢复显示，或直接在账套列表里编辑）"
                    : "账套名称不能为空！",
                "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (!hidden) this.txtName.Focus();
            return;
        }

        if (_original == null)
        {
            var existing = _dataService.FindAccount(this.txtCode.Text.Trim());
            if (existing != null)
            {
                MessageBox.Show("代码已存在！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.txtCode.Focus();
                return;
            }
        }

        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    private void AccountDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            btnSave.PerformClick();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            btnCancel.PerformClick();
            e.SuppressKeyPress = true;
        }
    }

    public Account GetAccount()
    {
        return new Account
        {
            Code = this.txtCode.Text.Trim(),
            Name = this.txtName.Text.Trim(),
            Server = this.txtServer.Text.Trim(),
            ServerBackup = this.txtServerBackup.Text.Trim(),
            ServerPassword = this.txtServerPassword.Text,
            Database = this.txtDatabase.Text.Trim(),
            DatabaseName = this.txtDatabaseName.Text.Trim(),
            DbUser = this.txtDbUser.Text.Trim(),
            DbPassword = this.txtDbPassword.Text,
            RemoteType = this.cboRemoteType.Text,
            RemoteAddress = this.txtRemoteAddress.Text.Trim(),
            RemoteUser = this.txtRemoteUser.Text.Trim(),
            RemotePassword = this.txtRemotePassword.Text,
            Remark = this.txtRemark.Text.Trim(),
            ServerUsername = this.txtServerUsername.Text.Trim(),
            // 自定义字段的值（2026-10-29）
            CustomFields = _customSlots.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Input.Text.Trim()),
            // 2026-07-09 代理模式
            ConnectionMode = this.rbHttp.Checked ? DataAccessMode.Http : DataAccessMode.Direct,
            HttpEndpoint = BuildHttpEndpoint(this.txtServer.Text.Trim()),
            HttpSecretKey = this.txtProxySecretKey.Text.Trim(),
            HttpServerPublicKey = this.txtProxyServerPublicKey.Text.Trim()
        };
    }

    /// <summary>
    /// 从账套地址自动拼 HttpEndpoint：账套地址 + /A3ToolsHub
    /// 例：http://192.168.1.50:8080 → http://192.168.1.50:8080/A3ToolsHub
    /// 账套地址为空时返回空串
    /// </summary>
    private static string BuildHttpEndpoint(string server)
    {
        if (string.IsNullOrWhiteSpace(server)) return string.Empty;
        return server.TrimEnd('/') + "/A3ToolsHub";
    }

    /// <summary>
    /// 代理模式切换：显示/隐藏密钥和公钥输入框
    /// </summary>
    private void ProxyMode_Changed(object? sender, EventArgs e)
    {
        UpdateProxyGroupVisibility();
    }

    private void UpdateProxyGroupVisibility()
    {
        bool isHttp = rbHttp.Checked;
        bool canShowHttp = this.IsRootMode && isHttp;

        lblProxySecretKey.Visible = canShowHttp;
        txtProxySecretKey.Visible = canShowHttp;
        lblProxyServerPublicKey.Visible = canShowHttp;
        txtProxyServerPublicKey.Visible = canShowHttp;
        btnGenerateHubConfig.Visible = canShowHttp;

        // 非 Root 模式下整个连接模式面板隐藏
        pnlProxyGroup.Visible = this.IsRootMode;

        if (this.IsRootMode)
        {
            rbHttp.Visible = true;
            lblProxyHint.Text = "🔒 连接模式（数据库不对外时使用 A3ToolsHub 代理转发）";
        }
    }

    private void BtnGenerateHubConfig_Click(object? sender, EventArgs e)
    {
        // 1. 校验配置目录
        if (string.IsNullOrWhiteSpace(HubConfigDir) || !Directory.Exists(HubConfigDir))
        {
            MessageBox.Show("请先在「设置」中配置「A3ToolsHub 配置文件目录」（仅 Root 模式可见）。",
                "未设置配置文件目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 2. 直接取账套代码和名称（打开对话框时已有值）
        var code = txtCode.Text.Trim();
        var name = txtName.Text.Trim();

        // 3. 检查是否已存在配置
        if (A3Tools.Common.Security.A3ToolsHubConfigGenerator.ConfigExists(HubConfigDir, code, name))
        {
            var dr = MessageBox.Show(
                $"文件夹「{code}_{name}」已存在配置，是否重新生成？\n（将清空原有配置）",
                "配置已存在", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
        }

        // 4. 生成配置
        var result = A3Tools.Common.Security.A3ToolsHubConfigGenerator.Generate(code, name);

        // 5. 写入文件
        A3Tools.Common.Security.A3ToolsHubConfigGenerator.WriteTo(HubConfigDir, code, name, result);

        // 6. 回填客户端字段
        txtProxySecretKey.Text = result.SecretKey;
        txtProxyServerPublicKey.Text = result.RsaPublicKey;

        // 7. 切到 Http 模式
        rbHttp.Checked = true;
        UpdateProxyGroupVisibility();

        MessageBox.Show(
            $"配置已生成并填入对应字段！\n\n" +
            $"生成目录：{Path.Combine(HubConfigDir, code + "_" + name)}\n" +
            $"文件：Web.config、rsa-public-key.xml、README.txt\n\n" +
            $"请将 Web.config 部署到服务器 A3ToolsHub 目录。",
            "生成成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
