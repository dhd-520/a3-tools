using System;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

public partial class SearchReplaceDialog : Form
{
    private readonly SqlEditor _editor;
    private int _lastFindIndex = -1;
    private string _lastFindText = "";

    public SearchReplaceDialog(SqlEditor editor, bool replaceMode = false)
    {
        _editor = editor;
        InitializeComponent();
        btnReplace.Visible = replaceMode;
        btnReplaceAll.Visible = replaceMode;
        lblReplace.Visible = replaceMode;
        txtReplace.Visible = replaceMode;
        Text = replaceMode ? "替换" : "查找";
    }

    private void SearchReplaceDialog_Load(object? sender, EventArgs e)
    {
        txtFind.Text = _editor.SelectedText;
        txtFind.Focus();
    }

    private void BtnFindNext_Click(object? sender, EventArgs e)
    {
        FindNext();
    }

    private void BtnFindPrevious_Click(object? sender, EventArgs e)
    {
        FindPrevious();
    }

    private void BtnReplace_Click(object? sender, EventArgs e)
    {
        if (_editor.SelectedText.Equals(txtFind.Text, chkCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
        {
            _editor.SelectedText = txtReplace.Text;
        }
        FindNext();
    }

    private void BtnReplaceAll_Click(object? sender, EventArgs e)
    {
        string text = _editor.Text;
        string find = txtFind.Text;
        string replace = txtReplace.Text;
        StringComparison comp = chkCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(find, index, comp)) != -1)
        {
            count++;
            index += find.Length;
        }

        if (count == 0)
        {
            MessageBox.Show("未找到匹配内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show($"找到 {count} 处匹配，确定全部替换吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            int selStart = _editor.SelectionStart;
            int selLen = _editor.SelectionLength;

            _editor.SuspendHighlight(true);
            _editor.SuspendLayout();
            try
            {
                _editor.Text = ReplaceAll(text, find, replace, comp);
                _editor.Select(selStart, selLen);
            }
            finally
            {
                _editor.ResumeLayout();
                _editor.SuspendHighlight(false);
                _editor.HighlightNow();
            }

            MessageBox.Show($"已替换 {count} 处", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private string ReplaceAll(string text, string find, string replace, StringComparison comp)
    {
        var sb = new System.Text.StringBuilder();
        int lastIndex = 0;
        int index;
        while ((index = text.IndexOf(find, lastIndex, comp)) != -1)
        {
            sb.Append(text.Substring(lastIndex, index - lastIndex));
            sb.Append(replace);
            lastIndex = index + find.Length;
        }
        sb.Append(text.Substring(lastIndex));
        return sb.ToString();
    }

    private bool FindNext()
    {
        if (string.IsNullOrEmpty(txtFind.Text)) return false;

        if (_lastFindText != txtFind.Text)
        {
            _lastFindText = txtFind.Text;
            _lastFindIndex = -1;
        }

        int start = _lastFindIndex == -1 ? _editor.SelectionStart : _lastFindIndex + _lastFindText.Length;
        int index = _editor.Text.IndexOf(txtFind.Text, start, chkCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        if (index == -1)
        {
            MessageBox.Show("未找到匹配内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        _lastFindIndex = index;
        _editor.Select(index, txtFind.Text.Length);
        _editor.ScrollToCaret();
        _editor.Focus();
        return true;
    }

    private bool FindPrevious()
    {
        if (string.IsNullOrEmpty(txtFind.Text)) return false;

        if (_lastFindText != txtFind.Text)
        {
            _lastFindText = txtFind.Text;
            _lastFindIndex = -1;
        }

        int start = _lastFindIndex == -1 ? _editor.SelectionStart : _lastFindIndex;
        int index = _editor.Text.LastIndexOf(txtFind.Text, Math.Max(0, start - 1), chkCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        if (index == -1)
        {
            MessageBox.Show("未找到匹配内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        _lastFindIndex = index;
        _editor.Select(index, txtFind.Text.Length);
        _editor.ScrollToCaret();
        _editor.Focus();
        return true;
    }

    private void TxtFind_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            FindNext();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        Close();
    }
}
