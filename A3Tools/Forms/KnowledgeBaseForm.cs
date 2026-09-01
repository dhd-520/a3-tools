using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Forms;

/// <summary>
/// 知识库管理窗口
/// ★ 2026-09-01 陛下要求:知识库系统(CRUD + 文件夹扫描 + 后续 AI 提取)
///   左侧:知识库列表;中上:条目列表;右下:条目编辑
/// </summary>
public partial class KnowledgeBaseForm : Form
{
    private readonly KnowledgeBaseManager _mgr = new();
    private KnowledgeBase? _currentBase;          // 当前选中的知识库
    private KnowledgeEntry? _currentEntry;        // 当前编辑中的条目
    private bool _isDirty;                         // 条目内容是否有未保存修改

    public KnowledgeBaseForm()
    {
        InitializeComponent();
        Load += (_, _) => InitData();
    }

    private void InitData()
    {
        RefreshBaseList();
        SetEditorEnabled(false);
        UpdateStatus("就绪");
    }

    // ━━━━━━━━━━━━━━━━ 知识库列表 ━━━━━━━━━━━━━━━━

    private void RefreshBaseList()
    {
        var selectedId = lstBases.SelectedItem is KnowledgeBase kb ? kb.Id : null;

        lstBases.DataSource = null;
        lstBases.DisplayMember = "Name";
        lstBases.ValueMember = "Id";
        lstBases.DataSource = _mgr.ListBases();

        if (selectedId != null)
        {
            for (int i = 0; i < lstBases.Items.Count; i++)
            {
                if (lstBases.Items[i] is KnowledgeBase b && b.Id == selectedId)
                {
                    lstBases.SelectedIndex = i;
                    break;
                }
            }
        }
        UpdateStatus($"共 {lstBases.Items.Count} 个知识库");
    }

    private void LstBases_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _currentBase = lstBases.SelectedItem as KnowledgeBase;
        if (_currentBase != null)
        {
            // 加载完整数据(含 entries)
            _currentBase = _mgr.GetBase(_currentBase.Id) ?? _currentBase;
            RefreshEntryList();
            SetEditorEnabled(false);
            UpdateStatus($"已选: {_currentBase.Name}({_currentBase.Entries.Count} 条目)");
        }
        else
        {
            _currentBase = null;
            lstEntries.Items.Clear();
            SetEditorEnabled(false);
            UpdateStatus("未选中知识库");
        }
    }

    private void TsbNewBase_Click(object? sender, EventArgs e)
    {
        var name = Prompt.ShowDialog("请输入知识库名称:", "新建知识库", this);
        if (string.IsNullOrWhiteSpace(name)) return;

        var kb = _mgr.CreateBase(name.Trim());
        RefreshBaseList();
        // 选中新创建的
        for (int i = 0; i < lstBases.Items.Count; i++)
        {
            if (lstBases.Items[i] is KnowledgeBase k && k.Id == kb.Id)
            {
                lstBases.SelectedIndex = i;
                break;
            }
        }
        UpdateStatus($"✅ 已创建: {kb.Name}");
    }

    private void TsbDeleteBase_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) return;
        var ok = MessageBox.Show(
            $"确认删除知识库「{_currentBase.Name}」?\n将删除其下所有条目,此操作不可恢复!",
            "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ok != DialogResult.Yes) return;

        _mgr.DeleteBase(_currentBase.Id);
        _currentBase = null;
        RefreshBaseList();
        LstBases_SelectedIndexChanged(null, EventArgs.Empty);
        UpdateStatus("✅ 已删除");
    }

    private void TsbRenameBase_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) return;
        var newName = Prompt.ShowDialog("修改知识库名称:", "重命名", this, _currentBase.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        var kb = _mgr.GetBase(_currentBase.Id);
        if (kb == null) return;
        kb.Name = newName.Trim();
        _mgr.SaveBase(kb);
        RefreshBaseList();
        UpdateStatus($"✅ 已重命名: {kb.Name}");
    }

    private void TsbSetWatchFolder_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) { MessageBox.Show("请先选择知识库"); return; }

        using var dlg = new FolderBrowserDialog();
        dlg.Description = "选择要扫描的文件夹(放入手册/文档)";
        if (!string.IsNullOrWhiteSpace(_currentBase.WatchFolder) && Directory.Exists(_currentBase.WatchFolder))
            dlg.SelectedPath = _currentBase.WatchFolder;

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            var kb = _mgr.GetBase(_currentBase.Id);
            if (kb == null) return;
            kb.WatchFolder = dlg.SelectedPath;
            _mgr.SaveBase(kb);
            _currentBase = kb;
            UpdateStatus($"✅ 已设置扫描文件夹: {dlg.SelectedPath}");
        }
    }

    private void TsbScanFolder_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) { MessageBox.Show("请先选择知识库"); return; }
        if (string.IsNullOrWhiteSpace(_currentBase.WatchFolder) || !Directory.Exists(_currentBase.WatchFolder))
        {
            MessageBox.Show("请先设置扫描文件夹");
            return;
        }

        var files = _mgr.ScanKnowledgeBaseFolder(_currentBase);
        if (files.Count == 0)
        {
            MessageBox.Show($"文件夹下未找到文件\n路径: {_currentBase.WatchFolder}\n模式: {string.Join(", ", _currentBase.FilePatterns)}");
            return;
        }

        // 弹窗让陛下选哪些文件要导入
        using var dlg = new ScanResultForm(files);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var selected = dlg.SelectedFiles;
        if (selected.Count == 0) { UpdateStatus("未选任何文件"); return; }

        // 导入选中的文件
        var kb = _mgr.GetBase(_currentBase.Id);
        if (kb == null) return;

        int added = 0, skipped = 0;
        foreach (var file in selected)
        {
            // 用文件路径+内容 hash 去重
            var content = _mgr.ReadFileContent(file.FullName);
            var hash = ComputeHash(content);

            var existing = kb.Entries.FirstOrDefault(e =>
                e.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase));
            if (existing != null && existing.ContentHash == hash)
            {
                skipped++;
                continue;
            }

            var entry = new KnowledgeEntry
            {
                Title = Path.GetFileNameWithoutExtension(file.Name),
                Content = content,
                SourceFile = file.FullName,
                ContentHash = hash,
                Tags = KnowledgeBaseManager.ExtractTagsFromContent(content),
            };

            if (existing != null)
            {
                // 覆盖更新
                entry.Id = existing.Id;
                entry.CreatedAt = existing.CreatedAt;
                _mgr.UpdateEntry(kb.Id, entry);
            }
            else
            {
                _mgr.AddEntry(kb.Id, entry);
            }
            added++;
        }

        _currentBase = kb;
        RefreshEntryList();
        UpdateStatus($"✅ 导入完成: 新增 {added},跳过(无变化) {skipped}");
    }

    // ━━━━━━━━━━━━━━━━ 条目列表 ━━━━━━━━━━━━━━━━

    private List<KnowledgeEntry> _filteredEntries = new();

    private void RefreshEntryList()
    {
        lstEntries.Items.Clear();
        if (_currentBase == null) return;

        var entries = _currentBase.Entries.AsEnumerable();
        var query = txtSearch.Text?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            entries = entries.Where(e =>
                (e.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (e.Content?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        _filteredEntries = entries.OrderByDescending(e => e.UpdatedAt).ToList();

        foreach (var entry in _filteredEntries)
        {
            var item = new ListViewItem(entry.Title);
            item.SubItems.Add(string.Join(", ", entry.Tags));
            item.SubItems.Add(Path.GetFileName(entry.SourceFile));
            item.SubItems.Add(entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            item.Tag = entry;
            lstEntries.Items.Add(item);
        }
        UpdateStatus($"显示 {_filteredEntries.Count} / {_currentBase.Entries.Count} 条目");
    }

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        RefreshEntryList();
    }

    private void LstEntries_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isDirty)
        {
            var ok = MessageBox.Show("当前条目有未保存修改,放弃?", "提示",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ok != DialogResult.Yes)
            {
                // 恢复选择
                return;
            }
        }

        _currentEntry = lstEntries.SelectedItems.Count > 0
            ? lstEntries.SelectedItems[0].Tag as KnowledgeEntry
            : null;
        LoadEntryToEditor(_currentEntry);
        SetEditorEnabled(_currentEntry != null);
        _isDirty = false;
    }

    // ━━━━━━━━━━━━━━━━ 条目编辑 ━━━━━━━━━━━━━━━━

    private void LoadEntryToEditor(KnowledgeEntry? entry)
    {
        if (entry == null)
        {
            txtTitle.Text = "";
            txtTags.Text = "";
            txtSource.Text = "";
            txtContent.Text = "";
            lblCharCount.Text = "字符:0";
            return;
        }
        txtTitle.Text = entry.Title;
        txtTags.Text = string.Join(", ", entry.Tags);
        txtSource.Text = entry.SourceFile;
        txtContent.Text = entry.Content;
        lblCharCount.Text = $"字符:{entry.Content?.Length ?? 0}";
    }

    private void SetEditorEnabled(bool enabled)
    {
        txtTitle.Enabled = enabled;
        txtTags.Enabled = enabled;
        txtContent.Enabled = enabled;
        btnSaveEntry.Enabled = enabled && _isDirty;
        btnCancelEdit.Enabled = enabled && _isDirty;
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (_currentEntry == null) return;
        _isDirty = true;
        btnSaveEntry.Enabled = true;
        btnCancelEdit.Enabled = true;
        lblCharCount.Text = $"字符:{txtContent.Text.Length}";
    }

    private void BtnSaveEntry_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null || _currentEntry == null) return;
        if (string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            MessageBox.Show("标题不能为空"); return;
        }

        _currentEntry.Title = txtTitle.Text.Trim();
        _currentEntry.Tags = txtTags.Text
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _currentEntry.Content = txtContent.Text;

        _mgr.UpdateEntry(_currentBase.Id, _currentEntry);
        _currentBase = _mgr.GetBase(_currentBase.Id);
        _isDirty = false;
        SetEditorEnabled(true);
        RefreshEntryList();
        UpdateStatus($"✅ 已保存: {_currentEntry.Title}");
    }

    private void BtnCancelEdit_Click(object? sender, EventArgs e)
    {
        LoadEntryToEditor(_currentEntry);
        _isDirty = false;
        SetEditorEnabled(_currentEntry != null);
        UpdateStatus("已撤销修改");
    }

    // ━━━━━━━━━━━━━━━━ 工具栏:条目操作 ━━━━━━━━━━━━━━━━

    private void TsbAddEntry_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) { MessageBox.Show("请先选择知识库"); return; }

        var entry = new KnowledgeEntry
        {
            Title = "新条目",
            Content = "",
        };
        _mgr.AddEntry(_currentBase.Id, entry);
        _currentBase = _mgr.GetBase(_currentBase.Id);
        RefreshEntryList();

        // 选中新条目
        foreach (ListViewItem item in lstEntries.Items)
        {
            if (item.Tag is KnowledgeEntry k && k.Id == entry.Id)
            {
                item.Selected = true;
                item.Focused = true;
                txtTitle.Focus();
                break;
            }
        }
        UpdateStatus($"✅ 已新建条目,请编辑");
    }

    private void TsbDeleteEntry_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null || _currentEntry == null) return;
        var ok = MessageBox.Show($"确认删除条目「{_currentEntry.Title}」?", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ok != DialogResult.Yes) return;

        _mgr.DeleteEntry(_currentBase.Id, _currentEntry.Id);
        _currentBase = _mgr.GetBase(_currentBase.Id);
        RefreshEntryList();
        UpdateStatus("✅ 已删除条目");
    }

    private void TsbSearch_Click(object? sender, EventArgs e)
    {
        txtSearch.Focus();
        txtSearch.SelectAll();
    }

    private async void TsbAiExtract_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) { MessageBox.Show("请先选择知识库"); return; }

        // 选文件夹（默认使用 KB 的 WatchFolder）
        string folderPath = _currentBase.WatchFolder;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "选择要 AI 提取的文件夹";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            folderPath = dlg.SelectedPath;
        }

        // 检查 AI 配置
        var configService = new AiConfigService();
        var provider = configService.GetDefaultProvider();
        if (provider == null)
        {
            MessageBox.Show("未配置默认 AI 供应商，请先在「帮助 -> AI 助理设置」中配置",
                "AI 未配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 确认
        var confirm = MessageBox.Show(
            $"将对文件夹里的文件逐个调用 AI 提炼为 md 知识库条目：\n{folderPath}\n\n根据文件数量与 AI 速度，这可能耗时较长。\n确定开始吗？",
            "确认 AI 提取", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        // 进度窗
        using var progress = new ProgressForm("AI 提炼中...");
        progress.Show(this);

        try
        {
            var backend = new OpenAiCompatibleBackend();
            var summary = await Task.Run(async () => await _mgr.AiExtractFromFolderAsync(
                _currentBase.Id, folderPath, provider, backend,
                new Progress<KnowledgeBaseManager.AiExtractProgress>(p =>
                {
                    progress.SetProgress(p.Index, p.Total, p.FileName, p.Status);
                    UpdateStatus($"🤖 [{p.Index}/{p.Total}] {p.FileName} - {p.Status}");
                }),
                progress.CancellationToken));

            progress.Close();
            _currentBase = _mgr.GetBase(_currentBase.Id);
            RefreshEntryList();

            var msg = $"✅ AI 提取完成\n总计 {summary.Total} 个文件：\n成功 {summary.Success}\n失败 {summary.Failed}\n跳过 {summary.Skipped}";
            if (summary.Errors.Count > 0)
            {
                msg += "\n\n错误详情：\n" + string.Join("\n", summary.Errors.Take(10));
            }
            MessageBox.Show(msg, "AI 提取结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateStatus($"✅ AI 提取：成功 {summary.Success}, 失败 {summary.Failed}, 跳过 {summary.Skipped}");
        }
        catch (OperationCanceledException)
        {
            progress.Close();
            UpdateStatus("⚠ AI 提取已取消");
        }
        catch (Exception ex)
        {
            progress.Close();
            MessageBox.Show($"AI 提取失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatus("❌ AI 提取失败");
        }
    }

    // ━━━━━━━━━━━━━━━━ 辅助 ━━━━━━━━━━━━━━━━

    private void UpdateStatus(string msg)
    {
        tsslStatus.Text = $"{DateTime.Now:HH:mm:ss}  {msg}";
    }

    private static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private void KnowledgeBaseForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isDirty)
        {
            var ok = MessageBox.Show("当前条目有未保存修改,关闭前保存吗?", "提示",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (ok == DialogResult.Yes) BtnSaveEntry_Click(null, EventArgs.Empty);
            else if (ok == DialogResult.Cancel) e.Cancel = true;
        }
    }
}