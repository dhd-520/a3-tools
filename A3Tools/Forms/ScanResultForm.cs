using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace A3Tools.Forms;

/// <summary>
/// 文件夹扫描结果选择对话框
/// ★ 2026-09-01 用于知识库管理(扫描文件夹后让陛下选要导入的文件)
/// </summary>
public partial class ScanResultForm : Form
{
    private readonly List<FileInfo> _files;
    public List<FileInfo> SelectedFiles { get; } = new();

    public ScanResultForm(List<FileInfo> files)
    {
        InitializeComponent();
        _files = files;
    }

    private void ScanResultForm_Load(object? sender, EventArgs e)
    {
        foreach (var f in _files)
        {
            var sizeKb = f.Length / 1024.0;
            var display = $"{f.Name}    [{sizeKb:F1} KB]    {f.FullName}";
            clbFiles.Items.Add(display, CheckState.Checked);  // 默认全选
        }
        UpdateSummary();
    }

    private void ClbFiles_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        // 用 BeginInvoke 延迟到 check 状态更新后刷新计数
        BeginInvoke(new Action(() => UpdateSummary()));
    }

    private void UpdateSummary()
    {
        int total = clbFiles.Items.Count;
        int checkedCount = clbFiles.CheckedItems.Count;
        lblSummary.Text = $"已选 {checkedCount} / {total}";
    }

    private void BtnSelectAll_Click(object? sender, EventArgs e)
    {
        for (int i = 0; i < clbFiles.Items.Count; i++)
            clbFiles.SetItemChecked(i, true);
        UpdateSummary();
    }

    private void BtnSelectNone_Click(object? sender, EventArgs e)
    {
        for (int i = 0; i < clbFiles.Items.Count; i++)
            clbFiles.SetItemChecked(i, false);
        UpdateSummary();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SelectedFiles.Clear();
        for (int i = 0; i < clbFiles.Items.Count; i++)
        {
            if (clbFiles.GetItemChecked(i) && i < _files.Count)
                SelectedFiles.Add(_files[i]);
        }
        base.OnClosing(e);
    }
}