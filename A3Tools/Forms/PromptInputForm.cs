using System;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// 通用输入框对话框（2026-10-29：新增自定义字段时用）。
/// 一个标签 + 一个输入框 + 确定/取消，回车确定、ESC 取消。
/// </summary>
public partial class PromptInputForm : Form
{
    /// <summary>用户输入的内容</summary>
    public string InputText => this.txtInput.Text;

    public PromptInputForm(string title, string prompt)
    {
        InitializeComponent();
        this.Text = title;
        this.lblPrompt.Text = prompt;
        this.txtInput.Select();
    }
}
