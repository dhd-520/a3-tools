using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// ★ 2026-08-14 10:28 陛下反馈:VS 设计模式打不开。
///   修复:控件初始化全部拆到 LaunchOptionsDialog.Designer.cs(标准 WinForms 模式),
///     本文件只留业务代码(属性/构造函数/事件方法)。
///   设计器可正常打开,所见即所得。
/// </summary>
public partial class LaunchOptionsDialog : Form
{
    public bool LaunchDesktop { get; private set; }
    public bool LaunchDevTools { get; private set; }
    public bool LaunchWeb { get; private set; }
    public string SelectedBrowser { get; private set; } = "chrome";
    public string AccountName { get; }
    public string AccountCode { get; }

    private static readonly Dictionary<string, string> BrowserMap = new()
    {
        { "chrome", "Google Chrome" },
        { "msedge", "Microsoft Edge" },
        { "firefox", "Firefox" },
        { "360se", "360安全浏览器" },
        { "default", "系统默认浏览器" }
    };

    /// <summary>
    /// 使用上次的设置作为默认值
    /// </summary>
    public LaunchOptionsDialog(bool defaultDesktop, bool defaultDevTools, bool defaultWeb, string defaultBrowser = "chrome", string accountName = "", string accountCode = "")
    {
        LaunchDesktop = defaultDesktop;
        LaunchDevTools = defaultDevTools;
        LaunchWeb = defaultWeb;
        SelectedBrowser = defaultBrowser;
        AccountName = accountName ?? string.Empty;
        AccountCode = accountCode ?? string.Empty;

        InitializeComponent();
        LoadDefaults();
    }

    private void LaunchOptionsDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.D1:
            case Keys.NumPad1:
                chkDesktop.Checked = !chkDesktop.Checked;
                e.SuppressKeyPress = true;
                break;
            case Keys.D2:
            case Keys.NumPad2:
                chkDevTools.Checked = !chkDevTools.Checked;
                e.SuppressKeyPress = true;
                break;
            case Keys.D3:
            case Keys.NumPad3:
                chkWeb.Checked = !chkWeb.Checked;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void LoadDefaults()
    {
        chkDesktop.Checked = LaunchDesktop;
        chkDevTools.Checked = LaunchDevTools;
        chkWeb.Checked = LaunchWeb;

        // 账套信息文本（例：账套：测试账套 (0001)）
        if (!string.IsNullOrEmpty(AccountName) || !string.IsNullOrEmpty(AccountCode))
        {
            lblAccountInfo.Text = string.IsNullOrEmpty(AccountCode)
                ? $"当前账套：{AccountName}"
                : $"当前账套：{AccountName} ({AccountCode})";
        }
        else
        {
            lblAccountInfo.Text = "当前账套：（未选择）";
        }

        // 填充浏览器选项
        cboBrowser.Items.Clear();
        foreach (var browser in BrowserMap)
        {
            cboBrowser.Items.Add(new BrowserItem { Value = browser.Key, Display = browser.Value });
        }
        cboBrowser.DisplayMember = "Display";
        cboBrowser.ValueMember = "Value";

        // 选中保存的浏览器
        for (int i = 0; i < cboBrowser.Items.Count; i++)
        {
            if (cboBrowser.Items[i] is BrowserItem item && item.Value == SelectedBrowser)
            {
                cboBrowser.SelectedIndex = i;
                break;
            }
        }
        if (cboBrowser.SelectedIndex < 0 && cboBrowser.Items.Count > 0)
            cboBrowser.SelectedIndex = 0;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        LaunchDesktop = chkDesktop.Checked;
        LaunchDevTools = chkDevTools.Checked;
        LaunchWeb = chkWeb.Checked;

        if (cboBrowser.SelectedItem is BrowserItem browser)
        {
            SelectedBrowser = browser.Value;
        }

        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private class BrowserItem
    {
        public string Value { get; set; } = "";
        public string Display { get; set; } = "";
    }
}