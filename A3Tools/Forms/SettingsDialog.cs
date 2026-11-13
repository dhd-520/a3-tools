using System;
using System.IO;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class SettingsDialog : Form
{
    private TextBox txtAppDir = null!;
    private Button btnBrowse = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;
    private Label lblTitle = null!;
    private Panel titleBar = null!;
    private CheckBox chkShowLaunchDialog = null!;
    private TextBox txtSsmsPath = null!;
    private Button btnSsmsBrowse = null!;
    private Button btnSsmsClear = null!;
    private CheckBox chkBrowserNewWindow = null!;
    private Panel sepAutoLogin = null!;
    private CheckBox chkClientAutoLogin = null!;
    private CheckBox chkDevToolsAutoLogin = null!;
    private Label lblDevToolsPassword = null!;
    private TextBox txtDevToolsPassword = null!;
    private Label hintDevTools = null!;

    // ★ 2026-08-14 11:34 陛下反馈:SettingsDialog 需要加企业微信选择器配置项
    //   ERP 和企业微信布局不同,placeholder 文案不同,需要独立配置
    private Label lblWechatWorkSelectorTitle = null!;
    private Label lblWechatWorkUsernameSel = null!;
    private TextBox txtWechatWorkUsernameSel = null!;
    private Label lblWechatWorkPasswordSel = null!;
    private TextBox txtWechatWorkPasswordSel = null!;
    private Label lblWechatWorkSubmitSel = null!;
    private TextBox txtWechatWorkSubmitSel = null!;

    public string AppDirectory { get; private set; } = string.Empty;
    public string TrayShowHotkey { get; private set; } = "Ctrl+Shift+Z";

    /// <summary>是否是 Root 模式（决定某些设置项是否可见）</summary>
    public bool IsRootMode { get; set; } = false;

    /// <summary>★ 2026-11-12 字号档位：显示文本 → 缩放系数</summary>
    private static readonly (string Text, float Scale)[] FontSizeOptions =
    {
        ("紧凑 (90%)", 0.9f),
        ("标准 (100%)", 1.0f),
        ("宽松 (115%)", 1.15f),
        ("大号 (130%)", 1.3f)
    };

    /// <summary>不修改字体的哨兵值（族名为空）</summary>
    private const string KeepCurrentFont = "（保持各界面原有字体）";

    public SettingsDialog(bool isRootMode = false)
    {
        IsRootMode = isRootMode;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        txtAppDir.Text = settings.AppDirectory;
        AppDirectory = settings.AppDirectory;
        chkShowLaunchDialog.Checked = settings.ShowLaunchOptionsDialog;
        txtSsmsPath.Text = settings.SsmsPath;
        chkBrowserNewWindow.Checked = settings.BrowserNewWindow;
        txtUsernameSel.Text = settings.WebUsernameSelector;
        txtPasswordSel.Text = settings.WebPasswordSelector;
        txtSubmitSel.Text = settings.WebSubmitSelector;
        // ★ 2026-08-14 11:34 企业微信选择器
        txtWechatWorkUsernameSel.Text = settings.WechatWorkUsernameSelector;
        txtWechatWorkPasswordSel.Text = settings.WechatWorkPasswordSelector;
        txtWechatWorkSubmitSel.Text = settings.WechatWorkSubmitSelector;
        chkClientAutoLogin.Checked = settings.ClientAutoLogin;
        chkDevToolsAutoLogin.Checked = settings.DevToolsAutoLogin;
        txtDevToolsPassword.Text = settings.DevToolsPassword;  // LoadSettings 已自动解密
        chkEnableAi.Checked = settings.EnableAi;

        // Root 专属设置
        txtHubConfigDir.Text = settings.A3ToolsHubConfigDir;

        // ★ 2026-11-12 全局界面字体
        LoadFontControls(settings);

        // 根据是否为 Root 模式显示/隐藏专属控件
        bool isRoot = this.IsRootMode;
        sepHubConfig.Visible = isRoot;
        lblHubConfigTitle.Visible = isRoot;
        hintHubConfig.Visible = isRoot;
        lblHubConfigDir.Visible = isRoot;
        txtHubConfigDir.Visible = isRoot;
        btnHubConfigBrowse.Visible = isRoot;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择A3应用程序目录", ShowNewFolderButton = false };
        if (!string.IsNullOrEmpty(txtAppDir.Text) && Directory.Exists(txtAppDir.Text))
            dialog.SelectedPath = txtAppDir.Text;
        if (dialog.ShowDialog() == DialogResult.OK)
            txtAppDir.Text = dialog.SelectedPath;
    }

    private void BtnSsmsBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "选择SSMS可执行文件", Filter = "可执行文件|*.exe", FileName = "Ssms.exe" };
        if (!string.IsNullOrEmpty(txtSsmsPath.Text) && File.Exists(txtSsmsPath.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(txtSsmsPath.Text);
        if (dialog.ShowDialog() == DialogResult.OK)
            txtSsmsPath.Text = dialog.FileName;
    }

    private void BtnHubConfigBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择 A3ToolsHub 配置文件生成目录", ShowNewFolderButton = true };
        if (!string.IsNullOrEmpty(txtHubConfigDir.Text) && Directory.Exists(txtHubConfigDir.Text))
            dialog.SelectedPath = txtHubConfigDir.Text;
        if (dialog.ShowDialog() == DialogResult.OK)
            txtHubConfigDir.Text = dialog.SelectedPath;
    }

    private void BtnSsmsClear_Click(object? sender, EventArgs e)
    {
        txtSsmsPath.Text = "";
    }

    private void BtnFontFileBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择字体文件（TTF / OTF / TTC）",
            Filter = "字体文件|*.ttf;*.otf;*.ttc|所有文件|*.*"
        };
        if (!string.IsNullOrEmpty(txtFontFile.Text) && File.Exists(txtFontFile.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(txtFontFile.Text);

        if (dialog.ShowDialog() != DialogResult.OK) return;
        txtFontFile.Text = dialog.FileName;

        // 立即加载，让字体列表能选到这个文件里的字体
        if (!UiFontManager.LoadPrivateFontFile(dialog.FileName))
        {
            MessageBox.Show(this,
                $"字体文件加载失败：\n{UiFontManager.LastError}\n\n请确认是有效的 TTF / OTF / TTC 文件。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshFontFamilyList();
        var families = UiFontManager.GetPrivateFontFamilies();
        if (families.Count > 0 && cmbFontFamily.Items.Count > 1)
        {
            cmbFontFamily.SelectedItem = families[0];
        }

        hintFont.Text = $"已加载字体文件，包含：{string.Join("、", families)}\r\n" +
                        "该字体仅本程序使用，不安装到系统。字号按比例缩放（保留标题与正文的层级差）。";
    }

    private void BtnFontFileClear_Click(object? sender, EventArgs e)
    {
        txtFontFile.Text = string.Empty;
        UiFontManager.LoadPrivateFontFile(string.Empty);
        RefreshFontFamilyList();
        hintFont.Text = "字体留空 = 保持各界面原有字体。字号按比例缩放（保留标题与正文的层级差）。\r\n" +
                        "字体文件可选择未安装的 TTF / OTF / TTC，选用后仅本程序使用，不安装到系统。";

        MessageBox.Show(this,
            "已清除字体文件。\r\n\r\n" +
            "★ Windows 的字体注册是进程级的，已加载的字体无法在运行中卸载，\r\n" +
            "所以「不使用字体文件」这个状态需重启程序后才完全生效。",
            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>★ 2026-11-12：把设置里的字体配置填到界面控件上</summary>
    private void LoadFontControls(AppSettings settings)
    {
        // 先加载字体文件，私有字体才能出现在列表里
        UiFontManager.LoadPrivateFontFile(settings.UiFontFilePath);

        RefreshFontFamilyList();

        // 选中已配置的字体
        if (!string.IsNullOrWhiteSpace(settings.UiFontFamily))
        {
            int idx = cmbFontFamily.Items.IndexOf(settings.UiFontFamily);
            cmbFontFamily.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            cmbFontFamily.SelectedIndex = 0;
        }

        // ★ 2026-11-13 字号下拉必须先填项，再设 SelectedIndex。
        //   空 ComboBox 设 SelectedIndex 必然越界：
        //   ArgumentOutOfRangeException("Selected index was invalid")
        //   —— 这个坑真的踩过：打开设置对话框直接崩，字体分区根本显示不出来。
        FillFontSizeItems();

        // 选中已配置的字号档位（取最接近的一档）
        float scale = settings.UiFontSizeScale <= 0 ? 1.0f : settings.UiFontSizeScale;
        int sizeIdx = FindClosestSizeIndex(scale);
        if (sizeIdx >= 0 && sizeIdx < cmbFontSize.Items.Count)
        {
            cmbFontSize.SelectedIndex = sizeIdx;
        }

        txtFontFile.Text = settings.UiFontFilePath ?? string.Empty;
    }

    /// <summary>
    /// ★ 2026-11-13：确保字号下拉的选项与 FontSizeOptions 一致。
    ///
    /// Designer 里已经写了 Items.AddRange（所见即所得，VS 设计器能直接看到这 4 项），
    /// 但万一以后有人改了其中一边，这里做一次自愈校验并重建 —— 选项与下标对不上时
    /// 保存出来的缩放系数就是错的。
    /// </summary>
    private void FillFontSizeItems()
    {
        bool needRebuild = cmbFontSize.Items.Count != FontSizeOptions.Length;
        if (!needRebuild)
        {
            for (int i = 0; i < FontSizeOptions.Length; i++)
            {
                if (!string.Equals(cmbFontSize.Items[i]?.ToString(), FontSizeOptions[i].Text, StringComparison.Ordinal))
                {
                    needRebuild = true;
                    break;
                }
            }
        }
        if (!needRebuild) return;

        cmbFontSize.BeginUpdate();
        cmbFontSize.Items.Clear();
        foreach (var opt in FontSizeOptions) cmbFontSize.Items.Add(opt.Text);
        cmbFontSize.EndUpdate();
    }

    /// <summary>
    /// ★ 2026-11-13：找最接近给定缩放系数的档位下标。
    /// 旧写法用 sizeIdx == 0 当哨兵，有 bug：scale=0.85 时会选中「标准 100%」
    /// 而不是「紧凑 90%」（因为下标 0 既是默认值又是合法候选，哨兵失效）。
    /// </summary>
    private static int FindClosestSizeIndex(float scale)
    {
        int best = 0;
        double bestDiff = double.MaxValue;
        for (int i = 0; i < FontSizeOptions.Length; i++)
        {
            double diff = Math.Abs(FontSizeOptions[i].Scale - scale);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = i;
            }
        }
        return best;
    }

    /// <summary>★ 2026-11-12：重建可选字体列表（私有字体置顶）</summary>
    private void RefreshFontFamilyList()
    {
        string previous = cmbFontFamily.SelectedItem as string ?? string.Empty;

        cmbFontFamily.BeginUpdate();
        cmbFontFamily.Items.Clear();
        cmbFontFamily.Items.Add(KeepCurrentFont);
        foreach (var name in UiFontManager.GetAvailableFamilies())
        {
            cmbFontFamily.Items.Add(name);
        }
        cmbFontFamily.EndUpdate();

        int idx = cmbFontFamily.Items.IndexOf(previous);
        cmbFontFamily.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        AppDirectory = txtAppDir.Text;
        var dataService = new DataService();
        var settings = dataService.LoadSettings();
        settings.AppDirectory = AppDirectory;
        settings.ShowLaunchOptionsDialog = chkShowLaunchDialog.Checked;
        settings.SsmsPath = txtSsmsPath.Text;
        settings.BrowserNewWindow = chkBrowserNewWindow.Checked;
        settings.WebUsernameSelector = txtUsernameSel.Text.Trim();
        settings.WebPasswordSelector = txtPasswordSel.Text.Trim();
        settings.WebSubmitSelector = txtSubmitSel.Text.Trim();
        // ★ 2026-08-14 11:34 企业微信选择器
        settings.WechatWorkUsernameSelector = txtWechatWorkUsernameSel.Text.Trim();
        settings.WechatWorkPasswordSelector = txtWechatWorkPasswordSel.Text.Trim();
        settings.WechatWorkSubmitSelector = txtWechatWorkSubmitSel.Text.Trim();
        settings.ClientAutoLogin = chkClientAutoLogin.Checked;
        settings.DevToolsAutoLogin = chkDevToolsAutoLogin.Checked;
        settings.DevToolsPassword = txtDevToolsPassword.Text;  // 明文，SaveSettings 自动加密
        settings.EnableAi = chkEnableAi.Checked;
        settings.A3ToolsHubConfigDir = txtHubConfigDir.Text.Trim();

        // ★ 2026-11-12 全局界面字体
        string selectedFamily = cmbFontFamily.SelectedItem as string ?? string.Empty;
        settings.UiFontFamily = selectedFamily == KeepCurrentFont ? string.Empty : selectedFamily;
        settings.UiFontSizeScale = cmbFontSize.SelectedIndex >= 0
                                  && cmbFontSize.SelectedIndex < FontSizeOptions.Length
            ? FontSizeOptions[cmbFontSize.SelectedIndex].Scale
            : 1.0f;
        settings.UiFontFilePath = txtFontFile.Text.Trim();

        dataService.SaveSettings(settings);

        // 立即生效（不必重启）
        try
        {
            UiFontManager.ApplyFromSettings(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"应用字体失败：{ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}