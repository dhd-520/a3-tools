using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using A3Tools.Services;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// A3 密文解密工具窗体。
/// 支持两种用法：
///   1. 手动粘贴密文（每行一条，批量解密）
///   2. 直接读取 A3 的 Account.info，自动列出所有账套的明文连接串
/// </summary>
public partial class A3DesDecryptForm : Form
{
    /// <summary>A3 客户端默认的账套配置文件路径，用作打开对话框的默认位置</summary>
    private const string DefaultAccountInfo = @"D:\A3\Account.info";

    public A3DesDecryptForm()
    {
        InitializeComponent();
    }

    private void BtnDecrypt_Click(object? sender, EventArgs e)
    {
        var input = txtCipher.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("请先粘贴要解密的密文！", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtCipher.Focus();
            return;
        }

        var sb = new StringBuilder();
        var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var total = 0;
        var ok = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            total++;
            if (A3DesCrypto.TryDecrypt(line, out var plain))
            {
                ok++;
                sb.AppendLine(plain);
            }
            else
            {
                sb.AppendLine($"[解密失败 #{total}] {Shorten(line)}");
                sb.AppendLine("  原因：不是有效的 A3 密文（Base64 格式不符、长度非 DES 块整数倍，或密钥不匹配）");
            }
        }

        if (total == 0)
        {
            MessageBox.Show("没有可解密的密文行！", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        txtOutput.Text = sb.ToString();
        lblStatus.Text = $"共 {total} 条，成功 {ok} 条，失败 {total - ok} 条";
    }

    private void BtnFromFile_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 A3 账套配置文件",
            Filter = "A3 账套配置 (*.info)|*.info|所有文件 (*.*)|*.*",
            InitialDirectory = File.Exists(DefaultAccountInfo)
                ? Path.GetDirectoryName(DefaultAccountInfo)
                : null,
            FileName = File.Exists(DefaultAccountInfo) ? Path.GetFileName(DefaultAccountInfo) : string.Empty
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            LoadAccountInfo(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取失败：\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 解析 Account.info，把每个账套的加密连接串解密后输出。
    /// </summary>
    private void LoadAccountInfo(string path)
    {
        var doc = XDocument.Load(path);

        var items = doc.Descendants("AccountItem").ToList();
        if (items.Count == 0)
        {
            txtOutput.Text = "文件中未找到 AccountItem 节点，可能不是 A3 的 Account.info。";
            lblStatus.Text = "解析完成：0 个账套";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"文件：{path}");
        sb.AppendLine($"账套数：{items.Count}");
        sb.AppendLine();

        var failed = 0;
        foreach (var item in items)
        {
            var code = Value(item, "Code");
            var name = Value(item, "Name");
            var dbName = Value(item, "DBName");
            var userName = Value(item, "UserName");
            var cipher = Value(item, "ConnString");

            sb.AppendLine($"=== {code} {name} ===");
            sb.AppendLine($"  数据库  ：{dbName}");
            sb.AppendLine($"  DB 用户 ：{userName}");

            if (string.IsNullOrEmpty(cipher))
            {
                sb.AppendLine("  连接串  ：（未配置）");
            }
            else if (A3DesCrypto.TryDecrypt(cipher, out var plain))
            {
                sb.AppendLine($"  连接串  ：{plain}");
            }
            else
            {
                failed++;
                sb.AppendLine("  连接串  ：解密失败（密文格式不符或密钥不匹配）");
            }

            sb.AppendLine();
        }

        txtOutput.Text = sb.ToString();
        lblStatus.Text = $"解析完成：{items.Count} 个账套，{items.Count - failed} 条连接串解密成功";
    }

    private static string Value(XElement parent, string name)
    {
        var el = parent.Element(name);
        return el == null ? string.Empty : el.Value.Trim();
    }

    private static string Shorten(string text)
    {
        return text.Length <= 60 ? text : text.Substring(0, 60) + "...";
    }

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(txtOutput.Text))
        {
            MessageBox.Show("结果为空，无可复制内容！", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Clipboard.SetText(txtOutput.Text);
            lblStatus.Text = "解密结果已复制到剪贴板";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        txtCipher.Clear();
        txtOutput.Clear();
        lblStatus.Text = "算法：DES / CBC / PKCS7（密钥取自 A3.Common.Util 的 A3DESEncrypt）";
        txtCipher.Focus();
    }
}