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
            MessageBox.Show("账套名称不能为空！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.txtName.Focus();
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
