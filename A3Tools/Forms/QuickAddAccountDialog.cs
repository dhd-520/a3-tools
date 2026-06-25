using System;
using System.Collections.Generic;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

public partial class QuickAddAccountDialog : Form
{
    private readonly DataService _dataService = new();

    /// <summary>
    /// 解析后的账套（确定后有效）
    /// </summary>
    public Account? CreatedAccount { get; private set; }

    /// <summary>
    /// 是否需要切换为手动添加窗体
    /// </summary>
    public bool SwitchToManual { get; private set; } = false;

    public QuickAddAccountDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 粘贴时强制从剪贴板读 Text（保留换行）手动插入。
    /// TextBox 自己接收 WM_PASTE，所以这里用子类覆盖 WndProc，比 Form 级拦截更稳。
    /// </summary>
    private class PastePreservingTextBox : TextBox
    {
        private const int WM_PASTE = 0x0302;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE && Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                var start = SelectionStart;
                var len = SelectionLength;
                var oldText = Text;
                Text = oldText.Remove(start, len).Insert(start, text);
                SelectionStart = start + text.Length;
                ScrollToCaret();
                return;
            }

            base.WndProc(ref m);
        }
    }

    private void QuickAddAccountDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            BtnConfirm_Click(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            BtnCancel_Click(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    private void BtnSwitchManual_Click(object? sender, EventArgs e)
    {
        SwitchToManual = true;
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    private void BtnConfirm_Click(object? sender, EventArgs e)
    {
        var text = this.txtPaste.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("请先粘贴账套信息文本！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.txtPaste.Focus();
            return;
        }

        var (account, parsedFields, errors) = ParseAccountText(text);
        if (account == null)
        {
            MessageBox.Show("解析失败：\n" + string.Join("\n", errors), "解析错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 必要校验：至少要有名称
        if (string.IsNullOrWhiteSpace(account.Name))
        {
            MessageBox.Show("解析成功但缺少账套名称（名称：xxx），无法添加。\n\n已识别字段：\n" + string.Join("\n", parsedFields), "缺少名称", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 自动分配 Code
        account.Code = GenerateDefaultCode();

        // 自动计算拼音
        account.Pinyin = PinyinHelper.GetPinyinInitial(account.Name);

        // 加密保存
        _dataService.AddAccount(account);

        CreatedAccount = account;
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    /// <summary>
    /// 生成下一个可用 Code（4 位数字，递增）
    /// </summary>
    private string GenerateDefaultCode()
    {
        var accounts = _dataService.LoadAccounts();
        int maxCode = 0;
        foreach (var acc in accounts)
        {
            if (int.TryParse(acc.Code, out int code) && code > maxCode)
                maxCode = code;
        }
        return (maxCode + 1).ToString("D4");
    }

    /// <summary>
    /// 解析粘贴的账套文本，按"字段名：值"格式匹配
    /// </summary>
    private (Account? account, List<string> parsedFields, List<string> errors) ParseAccountText(string text)
    {
        var account = new Account();
        var parsed = new List<string>();
        var errors = new List<string>();

        // 行分隔（兼容 CRLF / LF / CR）
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 找第一个中英文冒号
            int colonIdx = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ':' || line[i] == '：')
                {
                    colonIdx = i;
                    break;
                }
            }
            if (colonIdx < 0)
            {
                // 整行无冒号，跳过（不报错，避免误伤）
                continue;
            }

            var label = line.Substring(0, colonIdx).Trim();
            var value = line.Substring(colonIdx + 1).Trim();

            if (string.IsNullOrEmpty(value)) continue; // 空值跳过

            // 字段名映射（精确匹配 Label）
            bool matched = TrySetField(account, label, value);
            if (matched)
            {
                parsed.Add($"{label}：{value}");
            }
            // 未识别的字段不加入 parsed，但也不报错（容错：用户可能粘贴了别的格式）
        }

        if (parsed.Count == 0)
        {
            errors.Add("未能识别任何字段。请确认文本格式为「字段名：值」每行一条。");
            return (null, parsed, errors);
        }

        return (account, parsed, errors);
    }

    /// <summary>
    /// 根据 Label 文本匹配并设置 Account 属性
    /// </summary>
    private static bool TrySetField(Account acc, string label, string value)
    {
        switch (label)
        {
            case "代码": // 自动编码，忽略粘贴的代码
                return true;
            case "名称":
                acc.Name = value; return true;
            case "账套地址":
                acc.Server = value; return true;
            case "备用地址":
                acc.ServerBackup = value; return true;
            case "账套用户名":
                acc.ServerUsername = value; return true;
            case "账套密码":
                acc.ServerPassword = value; return true;
            case "数据库地址":
                acc.Database = value; return true;
            case "数据库名称":
                acc.DatabaseName = value; return true;
            case "DB用户":
                acc.DbUser = value; return true;
            case "DB密码":
                acc.DbPassword = value; return true;
            case "远程方式":
                acc.RemoteType = value; return true;
            case "远程地址":
                acc.RemoteAddress = value; return true;
            case "远程用户":
                acc.RemoteUser = value; return true;
            case "远程密码":
                acc.RemotePassword = value; return true;
            default:
                return false;
        }
    }
}