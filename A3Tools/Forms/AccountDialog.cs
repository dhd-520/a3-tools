using System;
using System.Windows.Forms;
using A3Tools.Common.DataAccess;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class AccountDialog : Form
{
    private readonly Account? _original;
    private readonly DataService _dataService = new();

    /// <summary>
    /// 是否显示密码明文（Root模式使用）
    /// </summary>
    public bool ShowPasswords { get; set; } = false;

    public AccountDialog(Account? account, bool showPasswords = false)
    {
        _original = account;
        ShowPasswords = showPasswords;
        InitializeComponent();
        this.KeyPreview = true;
        this.KeyDown += AccountDialog_KeyDown;
        if (account != null)
            LoadAccount(account);
        else
            GenerateDefaultCode();
        UpdateTitle();
        ProxyMode_Changed(null, EventArgs.Empty);
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
        bool isHttp = rbHttp.Checked;
        lblProxySecretKey.Visible = isHttp;
        txtProxySecretKey.Visible = isHttp;
        lblProxyServerPublicKey.Visible = isHttp;
        txtProxyServerPublicKey.Visible = isHttp;
    }
}
