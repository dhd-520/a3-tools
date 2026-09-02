using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;
using Markdig;

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

    // ★ 2026-09-02 陛下要求:知识库内容支持 Markdown 预览
    //   运行时构建(Designer 一行不改,避免 VS 设计器重写覆盖)
    private TabControl? tabContent;
    private TabPage? tpEdit;
    private TabPage? tpPreview;
    private WebBrowser? webPreview;

    public KnowledgeBaseForm()
    {
        InitializeComponent();
        Load += (_, _) => InitData();
    }

    private void InitData()
    {
        InitSourceFilter();
        InitMarkdownPreviewTab();
        InitButtonTooltips();
        InitEditorChangeEvents();
        // ★ 先设 SplitterDistance(此时 Panel 才有正确尺寸)
        ApplyDefaultSplitterDistance();
        // ★ 然后再定位按钮(依赖 Panel 正确尺寸)
        RepositionActionButtons();
        RefreshBaseList();
        SetEditorEnabled(false);
        UpdateStatus("就绪");
    }

    /// <summary>
    /// ★ 2026-09-02 修复:保存/取消按钮永远为灰色
    ///   原 Designer 只绑了 txtContent.TextChanged → OnEditorChanged
    ///   但改 txtTitle(标题) / txtTags(标签) 时不会触发 _isDirty,按钮永远灰色
    ///   这里手动订阅所有三个字段的 TextChanged
    /// </summary>
    private void InitEditorChangeEvents()
    {
        // 用 -= += 模式防止重复订阅(InitData 可能被调多次)
        txtTitle.TextChanged -= OnEditorChanged;
        txtTitle.TextChanged += OnEditorChanged;

        txtTags.TextChanged -= OnEditorChanged;
        txtTags.TextChanged += OnEditorChanged;

        // txtContent 已在 Designer 里绑了,但 InitMarkdownPreviewTab 移动了 Parent,安全起见重新订阅
        txtContent.TextChanged -= OnEditorChanged;
        txtContent.TextChanged += OnEditorChanged;
    }

    /// <summary>
    /// ★ 2026-09-02 修复:保存/取消按钮重叠
    ///   原 Designer:btnSaveEntry 在 pnlEntryEdit.Width-180,btnCancelEdit 在 pnlEntryEdit.Width-90,间距 10px 太紧
    ///   Anchor=Bottom|Right 在 Form.Load 时按 runtime 尺寸重算,但 Designer 里用 pnlEntryEdit.Width(此时为 0)算 Location,Anchor 起点错乱
    ///   这里运行时显式设置 Location + Anchor,间距加大到 20px
    /// </summary>
    private void RepositionActionButtons()
    {
        int btnW = 90;  // 稍微加宽一点(原 80,中文"保存"+"取消"+图标更舒服)
        int gap = 10;
        int bottomMargin = 10;

        // 重新设 Anchor(从 Designer 的 Anchor=Bottom|Right 继承过来,但 Location 重新算)
        btnSaveEntry.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnCancelEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        // 从右往左排:btnCancelEdit 在最右,btnSaveEntry 在其左边
        int panelW = pnlEntryEdit.Width;
        int panelH = pnlEntryEdit.Height;
        int y = panelH - btnSaveEntry.Height - bottomMargin;

        btnCancelEdit.Location = new Point(panelW - btnW - 50, y);
        btnSaveEntry.Location = new Point(panelW - btnW * 2 - gap - 150, y);

        // 字符计数 Label 也跟着调到底部左边
        lblCharCount.Location = new Point(10, y + 6);  // 垂直居中
    }

    /// <summary>
    /// ★ 2026-09-02 运行时设置 SplitterDistance 默认值。
    /// 陛下要求:左侧知识库宽度 500(原 250 的 2 倍),右上条目列表高度 600+(原 280 的 2 倍多)。
    /// </summary>
    private void ApplyDefaultSplitterDistance()
    {
        // scMain 左侧 = 500(form 宽 1200 时 Panel1=500, Panel2=694)
        try { scMain.SplitterDistance = 500; }
        catch { /* 超过容器尺寸时 WinForms 自动 clamp */ }

        // scRight 上方条目列表高度 = 380(平衡:Panel1 380 看条目 + Panel2 ≈ 254 放编辑器 + 按钮)
        //   ★ 2026-09-02 调整:不能设太大(比如 600),否则 Panel2 被压太矮,保存/取消按钮重叠
        try
        {
            int maxDist = scRight.Height - scRight.SplitterWidth - 25;
            scRight.SplitterDistance = Math.Min(380, Math.Max(100, maxDist));
        }
        catch { /* 同上 */ }
    }

    private void InitSourceFilter()
    {
        cmbSourceFilter = new ComboBox
        {
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 9.5F),
        };
        cmbSourceFilter.Items.Add(new FilterOption("全部来源", null));
        cmbSourceFilter.Items.Add(new FilterOption("✍ 手动添加", KnowledgeSourceType.Manual));
        cmbSourceFilter.Items.Add(new FilterOption("📄 文件导入", KnowledgeSourceType.FileImport));
        cmbSourceFilter.Items.Add(new FilterOption("🤖 AI 提取", KnowledgeSourceType.AiExtract));
        cmbSourceFilter.Items.Add(new FilterOption("💬 对话提取", KnowledgeSourceType.ChatExtract));
        cmbSourceFilter.SelectedIndex = 0;
        cmbSourceFilter.SelectedIndexChanged += (_, _) => RefreshEntryList();
        // 插入到 pnlEntryList 中：位于 txtSearch 上方
        pnlEntryList.Controls.Add(cmbSourceFilter);
        pnlEntryList.Controls.SetChildIndex(cmbSourceFilter, 1);
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

        int added = 0, skipped = 0, updated = 0;
        foreach (var file in selected)
        {
            // ★ 2026-09-01 扫描同样按 H2 拆分多条目（docx 检测 Heading 样式后插 `## `）
            var content = _mgr.ReadFileContent(file.FullName);
            var sections = KnowledgeBaseManager.SplitByH2Static(content);
            // 过滤出有标题的段落（不是首段的纯前缀部分）
            var realSections = sections.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();

            // 同一文件已存在 → 先删旧条目再重新拆分（保持一致）
            var existingForFile = kb.Entries
                .Where(e => e.SourceFile.Equals(file.FullName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var old in existingForFile)
                _mgr.DeleteEntry(kb.Id, old.Id);
            // 重新加载最新 kb
            kb = _mgr.GetBase(_currentBase.Id);

            if (realSections.Count == 0)
            {
                // 没有 H2 → 退化为 1 条
                var entry = new KnowledgeEntry
                {
                    Title = Path.GetFileNameWithoutExtension(file.Name),
                    Content = content,
                    SourceFile = file.FullName,
                    SourceType = Models.KnowledgeSourceType.FileImport,
                    ContentHash = KnowledgeBaseManager.ComputeHashStatic(content),
                    Tags = KnowledgeBaseManager.ExtractTagsFromContent(content),
                };
                _mgr.AddEntry(kb.Id, entry);
                added++;
            }
            else
            {
                // 按 H2 拆为多条
                var fileBaseName = Path.GetFileNameWithoutExtension(file.Name);
                foreach (var section in realSections)
                {
                    var entry = new KnowledgeEntry
                    {
                        Title = $"{fileBaseName} - {section.Title}",
                        Content = section.Content,
                        SourceFile = file.FullName,
                        SourceType = Models.KnowledgeSourceType.FileImport,
                        ContentHash = KnowledgeBaseManager.ComputeHashStatic(file.FullName + section.Title),
                        Tags = KnowledgeBaseManager.ExtractTagsFromContent(section.Content),
                    };
                    _mgr.AddEntry(kb.Id, entry);
                    added++;
                }
            }
            updated++;
        }

        _currentBase = kb;
        RefreshEntryList();
        UpdateStatus($"✅ 导入完成: 处理 {updated} 个文件,新增 {added} 条目,跳过(无变化) {skipped}");
    }

    // ━━━━━━━━━━━━━━━━ 条目列表 ━━━━━━━━━━━━━━━━

    private List<KnowledgeEntry> _filteredEntries = new();
    private ComboBox cmbSourceFilter = null!;

    private void RefreshEntryList()
    {
        lstEntries.Items.Clear();
        if (_currentBase == null) return;

        var entries = _currentBase.Entries.AsEnumerable();
        // 来源类型筛选
        if (cmbSourceFilter.SelectedItem is FilterOption filter && filter.Type.HasValue)
        {
            entries = entries.Where(e => e.SourceType == filter.Type.Value);
        }
        // 关键词搜索
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
            item.SubItems.Add(SourceTypeLabel(entry.SourceType));
            item.SubItems.Add(string.Join(", ", entry.Tags));
            item.SubItems.Add(FormatSourceFile(entry.SourceFile, entry.SourceType));
            item.SubItems.Add(entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            item.Tag = entry;
            lstEntries.Items.Add(item);
        }
        UpdateStatus($"显示 {_filteredEntries.Count} / {_currentBase.Entries.Count} 条目");
        UpdateSelectedCount();
    }

    /// <summary>来源类型 → 显示标签</summary>
    private static string SourceTypeLabel(KnowledgeSourceType t) => t switch
    {
        KnowledgeSourceType.Manual => "✍ 手动",
        KnowledgeSourceType.FileImport => "📄 文件",
        KnowledgeSourceType.AiExtract => "🤖 AI 提取",
        KnowledgeSourceType.ChatExtract => "💬 对话",
        _ => t.ToString(),
    };

    /// <summary>源文件路径 → 显示文本</summary>
    private static string FormatSourceFile(string sourceFile, KnowledgeSourceType type)
    {
        if (string.IsNullOrEmpty(sourceFile)) return type == KnowledgeSourceType.Manual ? "(手动添加)" : "";
        // 路径太长显示文件名
        return sourceFile.Length > 80 ? "…" + sourceFile[^77..] : sourceFile;
    }

    /// <summary>筛选选项包装</summary>
    private record FilterOption(string Display, KnowledgeSourceType? Type)
    {
        public override string ToString() => Display;
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
            RefreshPreviewIfActive();
            return;
        }
        txtTitle.Text = entry.Title;
        txtTags.Text = string.Join(", ", entry.Tags);
        txtSource.Text = entry.SourceFile;
        txtContent.Text = entry.Content;
        lblCharCount.Text = $"字符:{entry.Content?.Length ?? 0}";
        RefreshPreviewIfActive();
    }

    private void SetEditorEnabled(bool enabled)
    {
        txtTitle.Enabled = enabled;
        txtTags.Enabled = enabled;
        txtContent.Enabled = enabled;
        // ★ 2026-09-02 简化:选中条目后按钮直接可用(不再用 _isDirty 门控)
        btnSaveEntry.Enabled = enabled;
        btnCancelEdit.Enabled = enabled;
    }

    /// <summary>
    /// ★ 2026-09-02 陛下反馈:保存/取消按钮始终为灰色,不理解作用。
    ///   这里初始化 ToolTip 解释按钮行为:灰色 = 无未保存修改(或未选条目),修改后变蓝可点击。
    /// </summary>
    private void InitButtonTooltips()
    {
        var tip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 200,
            ReshowDelay = 200,
            IsBalloon = false,
        };
        tip.SetToolTip(btnSaveEntry, "保存当前条目的修改(标题/标签/内容)\n灰色 = 无未保存修改");
        tip.SetToolTip(btnCancelEdit, "撤销当前条目的修改,重新加载原始内容\n灰色 = 无未保存修改");
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (_currentEntry == null) return;
        _isDirty = true;
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

    // ━━━━━━━━━━━━━━━━ 批量选删 ━━━━━━━━━━━━━━━━

    private void TsbSelectAll_Click(object? sender, EventArgs e)
    {
        foreach (ListViewItem item in lstEntries.Items)
            item.Checked = true;
        UpdateSelectedCount();
    }

    private void TsbSelectNone_Click(object? sender, EventArgs e)
    {
        foreach (ListViewItem item in lstEntries.Items)
            item.Checked = false;
        UpdateSelectedCount();
    }

    private void TsbDeleteSelected_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) return;
        var checkedItems = lstEntries.CheckedItems;
        if (checkedItems.Count == 0) return;

        var ok = MessageBox.Show(
            $"确认删除选中的 {checkedItems.Count} 个条目？\n此操作不可恢复！",
            "批量删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ok != DialogResult.Yes) return;

        var ids = checkedItems.Cast<ListViewItem>()
            .Select(item => (item.Tag as KnowledgeEntry)?.Id ?? "")
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        var removed = _mgr.DeleteEntries(_currentBase.Id, ids);
        _currentBase = _mgr.GetBase(_currentBase.Id);
        RefreshEntryList();
        UpdateStatus($"✅ 已删除 {removed} 个条目");
    }

    private void LstEntries_ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        int total = lstEntries.Items.Count;
        int selected = lstEntries.CheckedItems.Count;
        tslSelectedCount.Text = selected > 0 ? $"  [已选 {selected}/{total}]" : $"  [共 {total}]";
        tsbDeleteSelected.Text = selected > 0 ? $"🗑 删除选中({selected})" : "🗑 删除选中(0)";
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

    private async void TsbAiSplit_Click(object? sender, EventArgs e)
    {
        if (_currentBase == null) { MessageBox.Show("请先选择知识库"); return; }

        // CheckedItems 和 SelectedItems 类型不同,统一用 Count 判断
        List<ListViewItem> selectedEntries;
        if (lstEntries.CheckedItems.Count > 0)
            selectedEntries = lstEntries.CheckedItems.Cast<ListViewItem>().ToList();
        else if (lstEntries.SelectedItems.Count > 0)
            selectedEntries = lstEntries.SelectedItems.Cast<ListViewItem>().ToList();
        else
            selectedEntries = new List<ListViewItem>();
        if (selectedEntries.Count == 0)
        {
            MessageBox.Show("请先勾选或选中要 AI 拆分的条目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var entryIds = selectedEntries
            .Select(i => (i.Tag as KnowledgeEntry)?.Id ?? "")
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        var confirm = MessageBox.Show(
            $"将对 {entryIds.Count} 个条目逐个调用 AI 分析内容并按主题拆分为多条。\n每个条目调 AI 一次,可能耗时较长。\n确定开始吗？",
            "AI 拆分确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var configService = new AiConfigService();
        var provider = configService.GetDefaultProvider();
        if (provider == null)
        {
            MessageBox.Show("未配置默认 AI 供应商,请先在「帮助 -> AI 助理设置」中配置",
                "AI 未配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var progress = new ProgressForm("AI 拆分中...");
        progress.Show(this);

        try
        {
            var backend = new OpenAiCompatibleBackend();
            var (success, failed, errors) = await Task.Run(async () => await _mgr.AiSplitEntriesAsync(
                _currentBase.Id, entryIds, provider, backend,
                new Progress<KnowledgeBaseManager.AiExtractProgress>(p =>
                {
                    progress.SetProgress(p.Index, p.Total, p.FileName, p.Status);
                    UpdateStatus($"🤖 [{p.Index}/{p.Total}] {p.FileName} - {p.Status}");
                }),
                progress.CancellationToken));

            progress.Close();
            _currentBase = _mgr.GetBase(_currentBase.Id);
            RefreshEntryList();

            var msg = $"✅ AI 拆分完成\n选中 {entryIds.Count} 个条目：\n成功 {success}\n失败 {failed}";
            if (errors.Count > 0)
                msg += "\n\n错误详情：\n" + string.Join("\n", errors.Take(10));
            MessageBox.Show(msg, "AI 拆分结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateStatus($"✅ AI 拆分：成功 {success}, 失败 {failed}");
        }
        catch (OperationCanceledException)
        {
            progress.Close();
            UpdateStatus("⚠ AI 拆分已取消");
        }
        catch (Exception ex)
        {
            progress.Close();
            MessageBox.Show($"AI 拆分失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatus("❌ AI 拆分失败");
        }
    }

    // ━━━━━━━━━━━━━━━━ 辅助 ━━━━━━━━━━━━━━━━

    private void UpdateStatus(string msg)
    {
        tsslStatus.Text = $"{DateTime.Now:HH:mm:ss}  {msg}";
    }

    private static string ComputeHashUnused(string content)
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

    // ━━━━━━━━━━━━━━━━ Markdown 预览(2026-09-02 陛下要求,运行时构建) ━━━━━━━━━━━━━━━━

    /// <summary>
    /// ★ 2026-09-02 陛下要求:知识库内容支持 Markdown 预览。
    ///   重要:Designer.cs 一行不改(陛下调布局的自由完全保留),所有控件运行时构建。
    ///   步骤:
    ///     1. 记录 txtContent 原位置/Anchor(继承)
    ///     2. 创建 tabContent + tpEdit + tpPreview + webPreview
    ///     3. 把 txtContent 从 pnlEntryEdit 移到 tpEdit(Dock=Fill)
    ///     4. tabContent 放在 txtContent 原位置,继承 Anchor
    /// </summary>
    private void InitMarkdownPreviewTab()
    {
        // 1. 记录 txtContent 原状态(为了 tabContent 完全继承)
        var txtLoc = txtContent.Location;
        var txtSize = txtContent.Size;
        var txtAnchor = txtContent.Anchor;

        // 2. 创建 TabControl + 2 个 TabPage
        tabContent = new TabControl
        {
            Location = txtLoc,
            Size = txtSize,
            Anchor = txtAnchor,  // 继承 txtContent 的 Anchor,运行时随窗体缩放
        };
        tpEdit = new TabPage { Text = "✏ 编辑" };
        tpPreview = new TabPage { Text = "👁 预览(Markdown)" };

        // 3. 把 txtContent 从 pnlEntryEdit 移到 tpEdit
        pnlEntryEdit.Controls.Remove(txtContent);
        txtContent.Dock = DockStyle.Fill;
        tpEdit.Controls.Add(txtContent);

        // 4. webPreview 装到 tpPreview
        webPreview = new WebBrowser
        {
            Dock = DockStyle.Fill,
            ScriptErrorsSuppressed = true,
        };
        tpPreview.Controls.Add(webPreview);

        // 5. tabContent 装 TabPage + 加到 pnlEntryEdit(覆盖原 txtContent 位置)
        tabContent.Controls.Add(tpEdit);
        tabContent.Controls.Add(tpPreview);
        pnlEntryEdit.Controls.Add(tabContent);

        // 6. 切到预览 Tab 触发渲染
        tabContent.SelectedIndexChanged += (_, _) =>
        {
            if (tabContent.SelectedIndex == tpPreview.TabIndex)
                RenderMarkdownPreview();
        };

        // 7. 更新 lblContent 文字提示用户切 Tab 看预览
        lblContent.Text = "内容(Markdown):  💡 切到下方「👁 预览」Tab 看渲染效果";
    }

    /// <summary>
    /// LoadEntryToEditor 后若当前在预览 Tab,自动刷新预览。
    /// ★ 2026-09-02 hook 进 LoadEntryToEditor,保证选条目 → 切到预览 Tab 能立刻看到渲染。
    /// </summary>
    private void RefreshPreviewIfActive()
    {
        if (tabContent != null && tabContent.SelectedIndex == tpPreview!.TabIndex)
        {
            RenderMarkdownPreview();
        }
    }

    /// <summary>把 txtContent 的 markdown 渲染到 webPreview</summary>
    private void RenderMarkdownPreview()
    {
        if (webPreview == null) return;
        try
        {
            string html = RenderMarkdownAsHtml(txtContent.Text ?? "");
            webPreview.DocumentText = html;
        }
        catch (Exception ex)
        {
            webPreview.DocumentText = $"<html><body style=\"font-family:Microsoft YaHei UI;padding:20px;color:#b00;\"><h3>渲染失败</h3><pre>{System.Net.WebUtility.HtmlEncode(ex.Message)}</pre></body></html>";
            UpdateStatus($"❌ Markdown 渲染失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Markdig → GitHub 风格 CSS HTML(★ 2026-09-02 加大:正文 17px,标题 24/21/18,代码 16px,适配右侧窗格大字阅读)。
    /// 支持 GFM(表格/任务列表/代码块)+ CommonMark(标题/列表/代码/粗体/斜体/链接/引用/分割线)。
    /// </summary>
    private static string RenderMarkdownAsHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        string bodyHtml = Markdown.ToHtml(markdown ?? "", pipeline);

        string html = @"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""><style>
body {
    font-family: 'Microsoft YaHei UI', 'Segoe UI', -apple-system, sans-serif;
    font-size: 20px;
    line-height: 1.7;
    color: #24292f;
    background: #ffffff;
    padding: 14px 18px;
    margin: 0;
}
h1, h2, h3, h4, h5, h6 { margin: 20px 0 12px 0; font-weight: 600; line-height: 1.3; }
h1 { font-size: 28px; padding-bottom: 8px; border-bottom: 1px solid #d0d7de; }
h2 { font-size: 24px; padding-bottom: 6px; border-bottom: 1px solid #d0d7de; }
h3 { font-size: 21px; }
h4 { font-size: 18px; }
p { margin: 0 0 14px 0; }
ul, ol { margin: 0 0 14px 0; padding-left: 28px; }
li { margin: 4px 0; }
li > p { margin: 0; }
blockquote {
    margin: 0 0 10px 0;
    padding: 0 12px;
    color: #57606a;
    border-left: 4px solid #d0d7de;
    background: #f6f8fa;
}
code {
    font-family: 'Consolas', 'Cascadia Code', monospace;
    font-size: 18px;
    background: rgba(175, 184, 193, 0.2);
    padding: 2px 6px;
    border-radius: 4px;
    color: #24292f;
}
pre {
    background: #f6f8fa;
    padding: 12px 14px;
    border-radius: 6px;
    overflow-x: auto;
    margin: 0 0 12px 0;
    line-height: 1.55;
}
pre code { background: transparent; padding: 0; font-size: 18px; }
strong { font-weight: 600; color: #24292f; }
em { font-style: italic; }
a { color: #0969da; text-decoration: none; }
a:hover { text-decoration: underline; }
hr { border: none; border-top: 1px solid #d0d7de; margin: 16px 0; }
table { border-collapse: collapse; margin: 0 0 14px 0; font-size: 17px; }
table th, table td { border: 1px solid #d0d7de; padding: 6px 12px; }
table th { background: #f6f8fa; font-weight: 600; }
input[type='checkbox'] { margin-right: 4px; vertical-align: middle; }
.task-list-item { list-style: none; padding-left: 0; }
img { max-width: 100%; }
</style></head><body>" + bodyHtml + @"</body></html>";
        return html;
    }
}