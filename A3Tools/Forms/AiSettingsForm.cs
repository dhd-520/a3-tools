using System;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

/// <summary>
/// AI 助理设置面板
/// <para>★ 2026-08-26 陛下要求：</para>
/// <list type="bullet">
///   <item>厂商列表（增删改查 + 7 家预设 + 自定义）</item>
///   <item>全局配置（默认厂商 / 存储策略 / 历史保留天数）</item>
///   <item>ApiKey AES 加密保存</item>
/// </list>
/// </summary>
public partial class AiSettingsForm : Form
{
    private readonly AiConfigService _configService = new();
    private AiChatConfig _config = new();

    // 记录上一次自动填充的 URL / Model，用于「切换类型时只在原值是自动填的情况下覆盖」
    private string LastAutoUrl = string.Empty;
    private string LastAutoModel = string.Empty;

    public AiSettingsForm()
    {
        InitializeComponent();
        LoadConfigToUi();
        InitCmbType();
        cmbType.SelectedIndexChanged += CmbType_SelectedIndexChanged;
        lstProviders.SelectedIndexChanged += (_, _) => LoadProviderToForm();
        btnAdd.Click += BtnAdd_Click;
        btnDelete.Click += BtnDelete_Click;
        btnSetDefault.Click += BtnSetDefault_Click;
        btnApplyProvider.Click += BtnApplyProvider_Click;
        btnAddPreset.Click += BtnAddPreset_Click;
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
    }

    private void InitCmbType()
    {
        cmbType.Items.Clear();
        foreach (AiProviderType t in Enum.GetValues(typeof(AiProviderType)))
        {
            cmbType.Items.Add(TypeToDisplay(t));
        }
        cmbType.SelectedIndex = 0;
    }

    private void CmbType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // 切换类型时自动填默认 URL / 模型（仅当字段为空时）
        var preset = AiProviderPreset.Defaults.FirstOrDefault(p => p.Name == cmbType.Text);
        if (preset != null)
        {
            if (string.IsNullOrWhiteSpace(txtApiUrl.Text) || txtApiUrl.Text == LastAutoUrl)
                txtApiUrl.Text = preset.ApiUrl;
            if (string.IsNullOrWhiteSpace(txtModel.Text) || txtModel.Text == LastAutoModel)
                txtModel.Text = preset.Model;
            LastAutoUrl = preset.ApiUrl;
            LastAutoModel = preset.Model;
        }
    }

    private void LoadConfigToUi()
    {
        _config = _configService.LoadConfig();
        RefreshProviderList();
        cmbStoreStrategy.SelectedIndex = (int)_config.StoreStrategy;
        nudRetentionDays.Value = _config.RetentionDays;
        nudMaxContext.Value = _config.MaxContextTokens;
        nudTemperature.Value = (decimal)_config.Temperature;
        txtSystemPromptAppend.Text = _config.SystemPromptAppend;
        chkAutoResume.Checked = _config.AutoResumeLastSession;
    }

    private void RefreshProviderList()
    {
        lstProviders.Items.Clear();
        foreach (var p in _config.Providers)
        {
            string mark = p.IsDefault ? "★ " : "   ";
            string enabled = p.Enabled ? "" : " [已禁用]";
            lstProviders.Items.Add($"{mark}{p.Name} ({p.Model}){enabled}");
        }
        if (lstProviders.Items.Count > 0 && lstProviders.SelectedIndex < 0)
            lstProviders.SelectedIndex = 0;
    }

    private void LoadProviderToForm()
    {
        var p = GetSelectedProvider();
        if (p == null) return;

        // 反向映射 type → cmb 显示文本
        cmbType.SelectedIndex = cmbType.Items.IndexOf(TypeToDisplay(p.Type));
        txtName.Text = p.Name;
        txtApiUrl.Text = p.ApiUrl;
        txtApiKey.Text = p.ApiKey;
        txtModel.Text = p.Model;
        chkEnabled.Checked = p.Enabled;
        txtRemark.Text = p.Remark;
        LastAutoUrl = p.ApiUrl;
        LastAutoModel = p.Model;
    }

    private static string TypeToDisplay(AiProviderType type) => type switch
    {
        AiProviderType.OpenAI => "OpenAI",
        AiProviderType.DeepSeek => "DeepSeek",
        AiProviderType.Qwen => "通义千问",
        AiProviderType.Zhipu => "智谱 GLM",
        AiProviderType.Moonshot => "月之暗面 Kimi",
        AiProviderType.MiniMax => "MiniMax",
        AiProviderType.Ollama => "Ollama 本地",
        _ => "自定义 OpenAI 兼容"
    };

    private static AiProviderType DisplayToType(string display) => display switch
    {
        "OpenAI" => AiProviderType.OpenAI,
        "DeepSeek" => AiProviderType.DeepSeek,
        "通义千问" => AiProviderType.Qwen,
        "智谱 GLM" => AiProviderType.Zhipu,
        "月之暗面 Kimi" => AiProviderType.Moonshot,
        "MiniMax" => AiProviderType.MiniMax,
        "Ollama 本地" => AiProviderType.Ollama,
        _ => AiProviderType.Custom
    };

    private AiProviderConfig? GetSelectedProvider()
    {
        int idx = lstProviders.SelectedIndex;
        if (idx < 0 || idx >= _config.Providers.Count) return null;
        return _config.Providers[idx];
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var p = new AiProviderConfig
        {
            Name = "新厂商",
            Type = AiProviderType.OpenAI,
            ApiUrl = "https://api.openai.com/v1",
            Model = "gpt-4o-mini",
            Enabled = true
        };
        _config.Providers.Add(p);
        RefreshProviderList();
        lstProviders.SelectedIndex = _config.Providers.Count - 1;
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var p = GetSelectedProvider();
        if (p == null) return;
        if (MessageBox.Show($"确定删除厂商「{p.Name}」？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        bool wasDefault = p.IsDefault;
        _config.Providers.Remove(p);
        if (wasDefault)
        {
            var newDefault = _config.Providers.FirstOrDefault(x => x.Enabled);
            if (newDefault != null)
            {
                newDefault.IsDefault = true;
                _config.DefaultProviderId = newDefault.Id;
            }
        }
        RefreshProviderList();
    }

    private void BtnSetDefault_Click(object? sender, EventArgs e)
    {
        var p = GetSelectedProvider();
        if (p == null) return;
        foreach (var x in _config.Providers) x.IsDefault = false;
        p.IsDefault = true;
        _config.DefaultProviderId = p.Id;
        RefreshProviderList();
        lstProviders.SelectedIndex = _config.Providers.IndexOf(p);
    }

    private void BtnApplyProvider_Click(object? sender, EventArgs e)
    {
        var p = GetSelectedProvider();
        if (p == null)
        {
            MessageBox.Show("请先在左侧列表选中一个厂商", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        p.Type = DisplayToType(cmbType.Text);
        p.Name = txtName.Text.Trim();
        p.ApiUrl = txtApiUrl.Text.Trim();
        p.ApiKey = txtApiKey.Text; // 不 Trim，保留原样
        p.Model = txtModel.Text.Trim();
        p.Enabled = chkEnabled.Checked;
        p.Remark = txtRemark.Text.Trim();

        if (string.IsNullOrEmpty(p.Name))
        {
            MessageBox.Show("厂商名称不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshProviderList();
        lstProviders.SelectedIndex = _config.Providers.IndexOf(p);
        MessageBox.Show($"厂商「{p.Name}」已更新（ApiKey 保存时自动加密）", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnAddPreset_Click(object? sender, EventArgs e)
    {
        using var dlg = new PresetSelectForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var preset = dlg.SelectedPreset;
        if (preset == null) return;

        // 检查同名是否已存在
        if (_config.Providers.Any(x => x.Name == preset.Name))
        {
            MessageBox.Show($"厂商「{preset.Name}」已存在，跳过添加", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newProvider = new AiProviderConfig
        {
            Name = preset.Name,
            Type = preset.Type,
            ApiUrl = preset.ApiUrl,
            Model = preset.Model,
            ApiKey = string.Empty, // 陛下后续填
            Enabled = true
        };

        if (_config.Providers.Count == 0)
            newProvider.IsDefault = true;

        _config.Providers.Add(newProvider);
        RefreshProviderList();
        lstProviders.SelectedIndex = _config.Providers.IndexOf(newProvider);
        MessageBox.Show($"已添加「{preset.Name}」，请填写 API Key 后点「应用到选中厂商」", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // 收集全局设置
        _config.StoreStrategy = (ChatStoreStrategy)cmbStoreStrategy.SelectedIndex;
        _config.RetentionDays = (int)nudRetentionDays.Value;
        _config.MaxContextTokens = (int)nudMaxContext.Value;
        _config.Temperature = (double)nudTemperature.Value;
        _config.SystemPromptAppend = txtSystemPromptAppend.Text.Trim();
        _config.AutoResumeLastSession = chkAutoResume.Checked;

        _configService.SaveConfig(_config);
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>
/// 预设厂商选择小窗（独立类，避免 AiSettingsForm 太长）
/// </summary>
internal class PresetSelectForm : Form
{
    public AiProviderPreset? SelectedPreset { get; private set; }

    private ListBox lstPresets = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    public PresetSelectForm()
    {
        Text = "选择预设厂商";
        Size = new System.Drawing.Size(420, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = System.Drawing.Color.White;

        var lbl = new Label
        {
            Text = "选择要添加的厂商预设（ApiKey 需后续手动填）：",
            Location = new System.Drawing.Point(16, 16),
            AutoSize = true,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F)
        };

        lstPresets = new ListBox
        {
            Location = new System.Drawing.Point(16, 48),
            Size = new System.Drawing.Size(372, 240),
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        foreach (var p in AiProviderPreset.Defaults)
        {
            lstPresets.Items.Add($"{p.Name}  ({p.Model})");
        }
        lstPresets.SelectedIndex = 0;

        btnOk = new Button
        {
            Text = "确定",
            Location = new System.Drawing.Point(220, 300),
            Size = new System.Drawing.Size(80, 32),
            BackColor = System.Drawing.Color.FromArgb(24, 144, 255),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            int idx = lstPresets.SelectedIndex;
            if (idx >= 0 && idx < AiProviderPreset.Defaults.Count)
                SelectedPreset = AiProviderPreset.Defaults[idx];
        };

        btnCancel = new Button
        {
            Text = "取消",
            Location = new System.Drawing.Point(308, 300),
            Size = new System.Drawing.Size(80, 32),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[] { lbl, lstPresets, btnOk, btnCancel });
    }
}
