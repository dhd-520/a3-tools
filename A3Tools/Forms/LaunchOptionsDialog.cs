using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace A3Tools.Forms;

public class LaunchOptionsDialog : Form
{
    private CheckBox chkDesktop;
    private CheckBox chkDevTools;
    private CheckBox chkWeb;
    private ComboBox cboBrowser;
    private Button btnOK;
    private Button btnCancel;

    public bool LaunchDesktop { get; private set; }
    public bool LaunchDevTools { get; private set; }
    public bool LaunchWeb { get; private set; }
    public string SelectedBrowser { get; private set; } = "chrome";

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
    public LaunchOptionsDialog(bool defaultDesktop, bool defaultDevTools, bool defaultWeb, string defaultBrowser = "chrome")
    {
        LaunchDesktop = defaultDesktop;
        LaunchDevTools = defaultDevTools;
        LaunchWeb = defaultWeb;
        SelectedBrowser = defaultBrowser;

        InitializeComponent();
        LoadDefaults();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Text = "选择启动选项";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ClientSize = new System.Drawing.Size(540, 440);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = System.Drawing.Color.White;

        // 标题栏
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176)
        };

        var lblTitle = new Label
        {
            Text = "🚀 选择启动选项",
            Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.White,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(24, 0, 0, 0)
        };
        titleBar.Controls.Add(lblTitle);

        // 内容区域
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(36, 24, 36, 12) };

        var lblHint = new Label
        {
            Text = "选择要启动的程序（已记住上次选择）：",
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Location = new System.Drawing.Point(0, 0),
            Size = new System.Drawing.Size(460, 30)
        };

        chkDesktop = new CheckBox
        {
            Text = "启动电脑端（君则A3.exe）",
            Location = new System.Drawing.Point(0, 40),
            Size = new System.Drawing.Size(460, 36),
            Font = new System.Drawing.Font("微软雅黑", 10F)
        };

        chkDevTools = new CheckBox
        {
            Text = "启动开发工具（君则A3集成开发工具.exe）",
            Location = new System.Drawing.Point(0, 88),
            Size = new System.Drawing.Size(460, 36),
            Font = new System.Drawing.Font("微软雅黑", 10F)
        };

        chkWeb = new CheckBox
        {
            Text = "启动网页版",
            Location = new System.Drawing.Point(0, 136),
            Size = new System.Drawing.Size(460, 36),
            Font = new System.Drawing.Font("微软雅黑", 10F)
        };

        // 浏览器选择区域
        var lblBrowser = new Label
        {
            Text = "选择浏览器：",
            Location = new System.Drawing.Point(30, 178),
            Size = new System.Drawing.Size(120, 36),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        cboBrowser = new ComboBox
        {
            Location = new System.Drawing.Point(150, 180),
            Size = new System.Drawing.Size(280, 32),
            Font = new System.Drawing.Font("微软雅黑", 10F),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat
        };

        // 填充浏览器选项
        cboBrowser.Items.Clear();
        foreach (var browser in BrowserMap)
        {
            cboBrowser.Items.Add(new BrowserItem { Value = browser.Key, Display = browser.Value });
        }
        cboBrowser.DisplayMember = "Display";
        cboBrowser.ValueMember = "Value";

        content.Controls.Add(lblHint);
        content.Controls.Add(chkDesktop);
        content.Controls.Add(chkDevTools);
        content.Controls.Add(chkWeb);
        content.Controls.Add(lblBrowser);
        content.Controls.Add(cboBrowser);

        // 底部按钮
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = System.Drawing.Color.FromArgb(248, 248, 248)
        };

        btnCancel = new Button
        {
            Text = "取消",
            Size = new System.Drawing.Size(120, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        btnCancel.DialogResult = DialogResult.Cancel;

        btnOK = new Button
        {
            Text = "启动",
            Size = new System.Drawing.Size(120, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(24, 145, 176),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font("微软雅黑", 10F),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.DialogResult = DialogResult.OK;
        btnOK.Click += BtnOK_Click;

        bottom.Controls.Add(btnOK);
        bottom.Controls.Add(btnCancel);

        bottom.Resize += (s, e) =>
        {
            btnCancel.Left = bottom.Width - 36 - btnCancel.Width;
            btnOK.Left = btnCancel.Left - 12 - btnOK.Width;
        };
        btnCancel.Left = bottom.Width - 36 - btnCancel.Width;
        btnOK.Left = btnCancel.Left - 12 - btnOK.Width;

        this.Controls.Add(bottom);
        this.Controls.Add(content);
        this.Controls.Add(titleBar);

        this.AcceptButton = btnOK;
        this.CancelButton = btnCancel;

        this.ResumeLayout(false);
    }

    private void LoadDefaults()
    {
        chkDesktop.Checked = LaunchDesktop;
        chkDevTools.Checked = LaunchDevTools;
        chkWeb.Checked = LaunchWeb;

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
