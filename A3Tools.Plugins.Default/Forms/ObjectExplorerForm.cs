using System.ComponentModel;
using A3Tools.Models;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL Server 对象资源管理器（仿 SSMS，按对象类型做 6 个独立 Tab + 硬过滤/高亮 双模式）。
///
/// 布局：
///   ┌──────────────────────────────────────┐
///   │ 🔄 刷新  ✂严格筛选    状态文字...    │ ← pnlTop
///   ├──────────────────────────────────────┤
///   │ [表 U] [视图 V] [TVF] [标量] [过程] [触发器]  ← TabControl：6 页
///   │  ┌──────────────────────────┐ │
///   │  │ 筛选 [____]              ││   每页独立 TextBox（带 200ms 节流）
///   │  ├──────────────────────────┤│
///   │  │ 📁 dbo                   ││   每页独立 TreeView（共享 ImageList）
///   │  │  └─ SaleOrder            ││   双击 → OpenScript
///   │  └──────────────────────────┘ │
///   └──────────────────────────────────────┘
///
/// 模式：
///   - 严格筛选（默认）：未命中节点直接 Remove，剩下的就是命中子树
///   - 高亮模式：保留所有节点，命中节点涂黄 + 展开
///
/// 性能：
///   - RefreshAsync 节流 SemaphoreSlim(1,1)，同时只跑 1 个重建
///   - 过滤器 Timer 节流 200ms，连续键入不卡
///   - ImageList 共享一份；Dispose(true) 释放 GDI handle
/// </summary>
public partial class ObjectExplorerForm : Form
{
    private readonly SqlQueryForm _owner;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private CancellationTokenSource? _rebuildCts;

    // 6 棵树的 (kind, filter, tree) — 一处管理，便于循环
    private readonly List<(SqlObjectSchemaCache.ObjectKind Kind, TextBox Filter, TreeView Tree, System.Windows.Forms.Timer Throttle)>
        _tabs = new();

    private bool _hardFilter = true;   // 默认严格筛选

    /// <summary>VS 设计器无参构造（运行时不会走这里）</summary>
    public ObjectExplorerForm() : this(null!) { if (DesignMode) return; }

    public ObjectExplorerForm(SqlQueryForm owner)
    {
        _owner = owner;
        InitializeComponent();

        if (DesignMode) return;

        BindAllTabs();

        // 顶栏：刷新按钮 + 模式切换按钮
        btnRefreshRoot.Click += (_, _) => _ = RefreshAsync(forceReload: true);
        btnMode.Click += (_, _) => ToggleMode();
        UpdateModeButtonText();

        // 关窗时静默释放
        FormClosing += (_, _) =>
        {
            _rebuildCts?.Cancel();
            _rebuildCts?.Dispose();
            _rebuildLock.Dispose();
            foreach (var (_, _, _, t) in _tabs) t.Dispose();
        };
    }

    /// <summary>绑定 6 个 Tab 与各自 Filter/Tree</summary>
    private void BindAllTabs()
    {
        BindOne(SqlObjectSchemaCache.ObjectKind.Table,           tabPageTable,    txtFilterTable,    treeTable);
        BindOne(SqlObjectSchemaCache.ObjectKind.View,             tabPageView,     txtFilterView,     treeView);
        BindOne(SqlObjectSchemaCache.ObjectKind.TableValuedFunction, tabPageTvf,   txtFilterTvf,      treeTvf);
        BindOne(SqlObjectSchemaCache.ObjectKind.ScalarFunction,   tabPageScalarFn, txtFilterScalarFn, treeScalarFn);
        BindOne(SqlObjectSchemaCache.ObjectKind.StoredProcedure,  tabPageProc,     txtFilterProc,     treeProc);
        BindOne(SqlObjectSchemaCache.ObjectKind.Trigger,          tabPageTrigger,  txtFilterTrigger,  treeTrigger);
    }

    private void BindOne(SqlObjectSchemaCache.ObjectKind kind, TabPage page, TextBox filter, TreeView tree)
    {
        page.Tag = kind;
        tree.Tag = page;
        filter.Tag = tree;

        // 双击事件：共用一个 handler
        tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;

        // 节流 Timer：每 Tab 一个，200ms 内多次 TextChanged 只触发最后一次
        var t = new System.Windows.Forms.Timer { Interval = 200 };
        System.Windows.Forms.Timer? capturedTimer = t;
        t.Tick += (_, _) =>
        {
            capturedTimer.Stop();
            // 切换 tag 同步：TabControl 已选中的 Tab = 当前活动的
            // 实际上每 Tab 的 filter 互相独立 → 这里仅 rebuild 这一棵
            _ = RebuildOneAsync(kind, tree, filter.Text);
        };

        _tabs.Add((kind, filter, tree, t));
        filter.TextChanged += (_, _) => { t.Stop(); t.Start(); };
    }

    /// <summary>模式 toggle</summary>
    private void ToggleMode()
    {
        _hardFilter = !_hardFilter;
        UpdateModeButtonText();
        // 6 棵树用各自当前 filter 重建一次
        foreach (var (kind, filter, tree, _) in _tabs)
        {
            // 立即触发（不等节流）
            _ = RebuildOneAsync(kind, tree, filter.Text);
        }
    }

    private void UpdateModeButtonText()
    {
        btnMode.Text = _hardFilter ? "✂ 严格筛选" : "🔍 仅高亮";
        btnMode.BackColor = _hardFilter ? Color.LightYellow : SystemColors.Control;
    }

    /// <summary>
    /// 唯一入口（外部：主窗体切库 / 关窗后重建 / Refresh 按钮都进这里）
    /// - 节流 SemaphoreSlim：上一轮没完不接新任务
    /// - CancellationTokenSource：取消老的、用新的
    /// </summary>
    public async Task RefreshAsync(bool forceReload = false)
    {
        if (_owner == null || DesignMode || IsDisposed) return;
        var connStr = _owner.CurrentConnectionString;
        if (string.IsNullOrEmpty(connStr))
        {
            ClearAll("(未连接数据库)", Color.Gray);
            return;
        }

        _rebuildCts?.Cancel();
        _rebuildCts?.Dispose();
        _rebuildCts = new CancellationTokenSource();
        var ct = _rebuildCts.Token;

        if (!await _rebuildLock.WaitAsync(0, ct))
            return;

        try
        {
            await SqlObjectSchemaCache.WarmupAsync(connStr, forceReload);

            if (ct.IsCancellationRequested || IsDisposed) return;

            if (InvokeRequired)
                BeginInvoke(new Action(() => RebuildAllTrees(connStr, ct)));
            else
                RebuildAllTrees(connStr, ct);
        }
        catch (OperationCanceledException) { /* ok */ }
        catch (Exception ex)
        {
            ClearAll($"刷新失败: {ex.Message}", Color.Red);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>单棵树的 rebuild（被 RefreshAsync 批跑 + 被 filter 节流触发）</summary>
    private async Task RebuildOneAsync(SqlObjectSchemaCache.ObjectKind kind, TreeView tree, string filter)
    {
        if (_owner == null || IsDisposed || tree.IsDisposed) return;
        var connStr = _owner.CurrentConnectionString;
        if (string.IsNullOrEmpty(connStr)) return;

        // 走同样的 cache 取数 + 直接 UI rebuild（重 IO 量小，不串行化也 OK）
        if (InvokeRequired)
            BeginInvoke(new Action(() =>
            {
                RebuildOneTree(connStr, kind, tree, filter);
                UpdateStatusText();
            }));
        else
        {
            RebuildOneTree(connStr, kind, tree, filter);
            UpdateStatusText();
        }
        await Task.CompletedTask;
    }

    /// <summary>6 棵树分别重建（每树 BeginUpdate 独立）</summary>
    private void RebuildAllTrees(string connStr, CancellationToken ct)
    {
        if (IsDisposed) return;
        foreach (var (kind, filter, tree, _) in _tabs)
        {
            if (ct.IsCancellationRequested || IsDisposed) return;
            RebuildOneTree(connStr, kind, tree, filter.Text);
        }
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (IsDisposed) return;
        int total = 0;
        foreach (var (_, _, t, _) in _tabs)
            if (!t.IsDisposed) total += t.GetNodeCount(includeSubTrees: true);
        lblStatus.Text = $"{total} 个对象";
    }

    /// <summary>单棵树重建：
    /// - filter 为空：完整树
    /// - filter 非空 + 严格模式：未命中直接 Remove；命中路径全显示
    /// - filter 非空 + 高亮模式：所有节点保留，命中涂黄 + 展开
    /// </summary>
    private void RebuildOneTree(string connStr, SqlObjectSchemaCache.ObjectKind kind, TreeView tree, string filter)
    {
        if (IsDisposed || tree.IsDisposed) return;

        var f = (filter ?? "").Trim();
        bool isFiltering = !string.IsNullOrEmpty(f);

        tree.BeginUpdate();
        tree.SuspendLayout();
        try
        {
            tree.Nodes.Clear();

            var objs = SqlObjectSchemaCache.GetObjectsByKind(connStr, new[] { kind });
            if (objs.Count == 0)
            {
                tree.Nodes.Add(new TreeNode($"(无 {GetKindDisplayName(kind)})") { ForeColor = Color.Gray });
                return;
            }

            var bySchema = objs
                .GroupBy(o => o.SchemaName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            // 表/视图只展示对象名（不需要列子节点）
            // TVF / 标量函数 / 存储过程 / 触发器 仍保留列子节点（参数 / 列）
            bool showColumns = kind switch
            {
                SqlObjectSchemaCache.ObjectKind.Table => false,
                SqlObjectSchemaCache.ObjectKind.View  => false,
                _ => true
            };

            foreach (var schemaGroup in bySchema)
            {
                TreeNode? schemaNode = null;
                int schemaVisible = 0;

                foreach (var obj in schemaGroup.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // 拆分命中判定
                    bool objMatch = isFiltering && obj.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;

                    List<string>? colsToShow = null;
                    List<string>? allCols = null;

                    // 表/视图不参与列匹配（没有列子节点）
                    if (showColumns && !string.IsNullOrEmpty(obj.Columns))
                        allCols = obj.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (showColumns && isFiltering && !objMatch && allCols != null)
                    {
                        var matched = allCols.Where(c => c.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                        if (matched.Count > 0) colsToShow = matched;
                    }

                    // 在硬过滤模式：完全无命中 → 跳过整对象
                    if (isFiltering && _hardFilter && !objMatch && colsToShow == null)
                        continue;

                    // 在高亮模式：所有节点都进树（命中与否不影响添加）
                    if (isFiltering && !_hardFilter && objMatch == false && colsToShow == null)
                    {
                        // 高亮模式无命中但过滤模式仍激活 → 仍然加入（涂黄不会，但不删除）
                    }

                    schemaNode ??= new TreeNode($"📁 {schemaGroup.Key}")
                    {
                        Tag = ("schema", schemaGroup.Key),
                        NodeFont = new Font(Font, FontStyle.Bold)
                    };

                    var typeChar = SqlObjectSchemaCache.KindToTypeChar(obj.Kind).Split(',')[0].Trim();
                    var objNode = new TreeNode(obj.Name)
                    {
                        Tag = ("object", $"{typeChar}|{obj.SchemaName}.{obj.Name}"),
                        ImageKey = obj.Kind.ImageKey(),
                        SelectedImageKey = obj.Kind.ImageKey(),
                        BackColor = objMatch && _hardFilter ? Color.LightYellow : Color.Empty
                    };
                    schemaNode.Nodes.Add(objNode);

                    // 列：
                    // - 表/视图：不挂列子节点（陛下要求）—— showColumns=false 时跳过
                    // - 硬过滤 + 命中对象名 → 显示全部列（用户想看命中对象细节）
                    // - 硬过滤 + 只命中列 → 只显示命中的列
                    // - 高亮模式 + 命中对象名 → 显示全部列（涂黄的只在对象层）
                    // - 高亮模式 + 命中列 → 只显示命中的列（涂黄在列层）
                    if (showColumns && allCols != null)
                    {
                        IEnumerable<string> cols = (colsToShow != null)
                            ? colsToShow
                            : (isFiltering && _hardFilter && !objMatch ? Array.Empty<string>() : allCols);

                        foreach (var col in cols)
                        {
                            var colNode = new TreeNode(col)
                            {
                                Tag = ("column", col),
                                ImageKey = "column",
                                SelectedImageKey = "column",
                                BackColor = (colsToShow != null && !objMatch) ? Color.LightYellow : Color.Empty
                            };
                            objNode.Nodes.Add(colNode);
                        }

                        // 命中时全部展开
                        if (objMatch || colsToShow != null)
                            objNode.Expand();
                    }

                    schemaVisible++;
                }

                if (schemaNode != null)
                {
                    if (isFiltering && _hardFilter)
                        schemaNode.Text = $"📁 {schemaGroup.Key} ({schemaVisible})";
                    tree.Nodes.Add(schemaNode);
                }
            }

            if (tree.Nodes.Count == 0)
            {
                tree.Nodes.Add(new TreeNode($"(无匹配 {GetKindDisplayName(kind)})") { ForeColor = Color.Gray });
            }
        }
        finally
        {
            tree.ResumeLayout();
            tree.EndUpdate();
        }
    }

    private void ClearAll(string text, Color color)
    {
        if (IsDisposed) return;
        lblStatus.Text = text;
        lblStatus.ForeColor = color;
        foreach (var (_, _, tree, _) in _tabs)
        {
            if (!tree.IsDisposed)
            {
                tree.BeginUpdate();
                tree.Nodes.Clear();
                tree.Nodes.Add(new TreeNode(text) { ForeColor = color });
                tree.EndUpdate();
            }
        }
    }

    private static string GetKindDisplayName(SqlObjectSchemaCache.ObjectKind kind) => kind switch
    {
        SqlObjectSchemaCache.ObjectKind.Table              => "表",
        SqlObjectSchemaCache.ObjectKind.View               => "视图",
        SqlObjectSchemaCache.ObjectKind.TableValuedFunction => "表值函数",
        SqlObjectSchemaCache.ObjectKind.ScalarFunction     => "标量函数",
        SqlObjectSchemaCache.ObjectKind.StoredProcedure    => "存储过程",
        SqlObjectSchemaCache.ObjectKind.Trigger            => "触发器",
        _ => "对象"
    };

    // ============================================
    // 双击穿透
    // ============================================

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (_owner == null || DesignMode) return;
        if (e.Node?.Tag is not ValueTuple<string, string> tag) return;
        if (tag.Item1 != "object") return;

        var pipeIdx = tag.Item2.IndexOf('|');
        if (pipeIdx < 0) return;
        var objType = tag.Item2.Substring(0, pipeIdx);
        var fullName = tag.Item2.Substring(pipeIdx + 1);

        _owner.OpenScript(database: "", objType: objType, objName: fullName);
    }
}

/// <summary>ObjectKind → ImageKey 映射</summary>
file static class ObjectKindImageMapping
{
    public static string ImageKey(this SqlObjectSchemaCache.ObjectKind kind) => kind switch
    {
        SqlObjectSchemaCache.ObjectKind.Table => "table",
        SqlObjectSchemaCache.ObjectKind.View => "view",
        SqlObjectSchemaCache.ObjectKind.TableValuedFunction => "tvf",
        SqlObjectSchemaCache.ObjectKind.ScalarFunction => "fn",
        SqlObjectSchemaCache.ObjectKind.StoredProcedure => "proc",
        SqlObjectSchemaCache.ObjectKind.Trigger => "trigger",
        _ => "table"
    };
}
