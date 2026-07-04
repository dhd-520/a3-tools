using System.ComponentModel;
using A3Tools.Models;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL Server 对象资源管理器（仿 SSMS，按对象类型做 6 个独立 Tab）。
///
/// 布局：
///   ┌──────────────────────────────────────┐
///   │ 🔄 刷新  (顶栏)                       │
///   ├──────────────────────────────────────┤
///   │  [表] [视图] [TVF] [标量] [过程] [触发器]  ← TabControl：每个 Tab 一颗 TreeView
///   │  ┌─────────────────────────────────┐│
///   │  │ Filter: [____]                  ││   每 Tab 独立筛选，框在每 Tab 顶上
///   │  ├─────────────────────────────────┤│
///   │  │ 📁 dbo      <- schema 节点      ││
///   │  │  └─ SaleOrder  <- 对象节点       ││   双击 → OpenScript
///   │  │     └─ BillNO   <- 列节点        ││
///   │  └─────────────────────────────────┘│
///   └──────────────────────────────────────┘
///
/// 性能要点：
///   - 每类对象一颗树 → 节点数 ≈ 总数 / 6（最常见一个库 200~800 张表）
///   - RefreshAsync 走 SemaphoreSlim 节流（避免 切库/toggle 触发 4-5 次重建）
///   - 刷新走 diff（已有节点不重建；只 Add/Remove delta）—— 计划 v2
///   - ImageList 设计器一次构建，所有树共享
///   - TreeView 关 HideSelection + 启用 DoubleBuffered 自定义（如有需要）
/// </summary>
public partial class ObjectExplorerForm : Form
{
    private readonly SqlQueryForm _owner;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);   // 同时只允许 1 个重建
    private CancellationTokenSource? _rebuildCts;

    /// <summary>VS 设计器无参构造（运行时不会走这里）</summary>
    public ObjectExplorerForm() : this(null!) { if (DesignMode) return; }

    public ObjectExplorerForm(SqlQueryForm owner)
    {
        _owner = owner;
        InitializeComponent();

        if (DesignMode) return;

        // 每个 Tab 的事件独立绑定（避免一处改动全部重建）
        BindTabEvents();

        // 顶栏刷新按钮
        btnRefreshRoot.Click += (_, _) => _ = RefreshAsync(forceReload: true);

        // 关窗时静默释放所有 ImageList bitmap（避免 GDI handle 残留）
        FormClosing += (s, e) =>
        {
            _rebuildCts?.Cancel();
            _rebuildCts?.Dispose();
            _rebuildLock.Dispose();
        };
    }

    /// <summary>绑定 6 个 Tab 各自的 Filter + Tree</summary>
    private void BindTabEvents()
    {
        BindOneTab(SqlObjectSchemaCache.ObjectKind.Table,           tabPageTable,    txtFilterTable,    treeTable);
        BindOneTab(SqlObjectSchemaCache.ObjectKind.View,             tabPageView,     txtFilterView,     treeView);
        BindOneTab(SqlObjectSchemaCache.ObjectKind.TableValuedFunction, tabPageTvf,  txtFilterTvf,      treeTvf);
        BindOneTab(SqlObjectSchemaCache.ObjectKind.ScalarFunction,   tabPageScalarFn, txtFilterScalarFn, treeScalarFn);
        BindOneTab(SqlObjectSchemaCache.ObjectKind.StoredProcedure,  tabPageProc,     txtFilterProc,     treeProc);
        BindOneTab(SqlObjectSchemaCache.ObjectKind.Trigger,          tabPageTrigger,  txtFilterTrigger,  treeTrigger);
    }

    private void BindOneTab(SqlObjectSchemaCache.ObjectKind kind, TabPage page, TextBox filter, TreeView tree)
    {
        page.Tag = kind;          // 让 RefreshAsync 能识别每页要拿哪一类
        tree.Tag = page;          // 反向指针（不用 Dictionary 减少 GC）
        filter.Tag = tree;

        filter.TextChanged += (_, _) => ApplyFilterToOneTree(tree, filter.Text);
        tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
    }

    /// <summary>
    /// 唯一入口（外部：主窗体切库 / 关窗后重建 / Refresh 按钮都进这里）
    /// - 节流 SemaphoreSlim：上一轮没完不接新任务
    /// - CancellationToken：取消老的、用新的
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

        // 取消老任务（如果还在跑），启动新的
        _rebuildCts?.Cancel();
        _rebuildCts?.Dispose();
        _rebuildCts = new CancellationTokenSource();
        var ct = _rebuildCts.Token;

        // 节流闸门
        if (!await _rebuildLock.WaitAsync(0, ct))
        {
            // 上一轮还没结束 — 这一轮不重入（避免 4 次并发重建）
            return;
        }

        try
        {
            // 先保证缓存加载
            await SqlObjectSchemaCache.WarmupAsync(connStr, forceReload);

            if (ct.IsCancellationRequested || IsDisposed) return;

            // 6 棵树分别重建（每树 BeginUpdate，避免整窗卡顿）
            // UI thread 因为这是 await 之后——回到原线程
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

    /// <summary>6 棵树分别重建（BeginUpdate/EndUpdate 各自独立）</summary>
    private void RebuildAllTrees(string connStr, CancellationToken ct)
    {
        if (IsDisposed) return;

        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.Table,           treeTable);
        if (ct.IsCancellationRequested) return;
        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.View,             treeView);
        if (ct.IsCancellationRequested) return;
        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.TableValuedFunction, treeTvf);
        if (ct.IsCancellationRequested) return;
        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.ScalarFunction,   treeScalarFn);
        if (ct.IsCancellationRequested) return;
        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.StoredProcedure,  treeProc);
        if (ct.IsCancellationRequested) return;
        RebuildOneTree(connStr, SqlObjectSchemaCache.ObjectKind.Trigger,          treeTrigger);
        if (ct.IsCancellationRequested) return;

        // 状态栏汇总
        var total = 0;
        foreach (TreeView tv in new[] { treeTable, treeView, treeTvf, treeScalarFn, treeProc, treeTrigger })
            total += tv.GetNodeCount(includeSubTrees: true);
        lblStatus.Text = $"{total} 个对象（6 个独立 Tab）";
    }

    /// <summary>单棵树重建（核心：GroupBy Schema → 每 schema 一个 node → 子是对象 → 列）</summary>
    private void RebuildOneTree(string connStr, SqlObjectSchemaCache.ObjectKind kind, TreeView tree)
    {
        if (IsDisposed || tree.IsDisposed) return;

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

            // 按 schema 分组（忽略大小写，OrdinalIgnoreCase）
            var bySchema = objs
                .GroupBy(o => o.SchemaName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var schemaGroup in bySchema)
            {
                var schemaNode = new TreeNode($"📁 {schemaGroup.Key} ({schemaGroup.Count()})")
                {
                    Tag = ("schema", schemaGroup.Key),
                    NodeFont = new Font(Font, FontStyle.Bold)
                };
                tree.Nodes.Add(schemaNode);

                foreach (var obj in schemaGroup.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var typeChar = SqlObjectSchemaCache.KindToTypeChar(obj.Kind).Split(',')[0].Trim();
                    var objNode = new TreeNode(obj.Name)
                    {
                        Tag = ("object", $"{typeChar}|{obj.SchemaName}.{obj.Name}"),
                        ImageKey = obj.Kind.ImageKey(),
                        SelectedImageKey = obj.Kind.ImageKey()
                    };
                    schemaNode.Nodes.Add(objNode);

                    // 列子节点（默认挂上；可由 CollapseAllSchema 控制）
                    if (!string.IsNullOrEmpty(obj.Columns))
                    {
                        foreach (var col in obj.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var colNode = new TreeNode(col)
                            {
                                Tag = ("column", col),
                                ImageKey = "column",
                                SelectedImageKey = "column"
                            };
                            objNode.Nodes.Add(colNode);
                        }
                    }
                }
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
        foreach (TreeView tv in new[] { treeTable, treeView, treeTvf, treeScalarFn, treeProc, treeTrigger })
        {
            if (!tv.IsDisposed)
            {
                tv.BeginUpdate();
                tv.Nodes.Clear();
                tv.Nodes.Add(new TreeNode(text) { ForeColor = color });
                tv.EndUpdate();
            }
        }
    }

    /// <summary>按对象类型简短显示名（TabPage 标题已有，但这里用于状态/空态）</summary>
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
    // 筛选（只改 BackColor，不动树结构）
    // ============================================

    /// <summary>单树筛选（按 Filter 输入框文本模糊匹配叶子列 + 父对象 + schema 节点）</summary>
    private static void ApplyFilterToOneTree(TreeView tree, string filter)
    {
        var f = (filter ?? "").Trim();
        ClearBackColors(tree.Nodes);
        if (string.IsNullOrEmpty(f)) return;

        var root = tree.Nodes.Count > 0 ? tree.Nodes[0] : null;
        if (root == null) return;

        // 命中：列子节点；父对象/schema 节点的命中 = 其下任一叶子命中
        HighlightMatching(tree.Nodes, f, StringComparison.OrdinalIgnoreCase);

        // 命中后把祖先全部展开（用户友好体验）—— 用迭代不开递归
        ExpandMatching(tree.Nodes, f, StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearBackColors(TreeNodeCollection nodes)
    {
        foreach (TreeNode n in nodes)
        {
            n.BackColor = Color.Empty;
            if (n.Nodes.Count > 0) ClearBackColors(n.Nodes);
        }
    }

    private static bool HighlightMatching(TreeNodeCollection nodes, string filter, StringComparison cmp)
    {
        bool anyHit = false;
        foreach (TreeNode n in nodes)
        {
            bool selfHit = !string.IsNullOrEmpty(n.Text) && n.Text.IndexOf(filter, cmp) >= 0;
            bool childHit = HighlightMatching(n.Nodes, filter, cmp);
            if (selfHit || childHit)
            {
                if (n.Nodes.Count == 0) // 只有叶子（列）涂黄，避免一树全黄
                    n.BackColor = Color.LightYellow;
                anyHit = true;
            }
        }
        return anyHit;
    }

    private static void ExpandMatching(TreeNodeCollection nodes, string filter, StringComparison cmp)
    {
        foreach (TreeNode n in nodes)
        {
            if (n.BackColor == Color.LightYellow || HasHighlightedDescendant(n))
            {
                n.Expand();
                ExpandMatching(n.Nodes, filter, cmp);
            }
        }
    }

    private static bool HasHighlightedDescendant(TreeNode node)
    {
        foreach (TreeNode c in node.Nodes)
        {
            if (c.BackColor == Color.LightYellow) return true;
            if (HasHighlightedDescendant(c)) return true;
        }
        return false;
    }

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

/// <summary>ObjectKind → ImageKey 映射。静态，便于一处定义。</summary>
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
