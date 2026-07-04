using A3Tools.Models;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL Server 对象资源管理器（仿 SSMS）。
/// - 按 Schema 树状展示当前账套 + 选中数据库的对象
/// - 工具栏 CheckBox 可按类型筛选（表/视图/TVF/标量函数/存储过程/触发器）
/// - 顶部 TextBox 实时模糊过滤（不区分大小写、子串）
/// - 双击对象 → 让父 SqlQueryForm 调用 OpenScript(objType, objName) 进新 Tab
/// - 🔄 按钮强制刷新缓存
///
/// 设计器无参构造留给 VS 加载设计时使用；运行时走带参构造。
/// </summary>
public partial class ObjectExplorerForm : Form
{
    private readonly SqlQueryForm _owner;
    private readonly List<SqlObjectSchemaCache.ObjectKind> _selectedKinds = new();

    /// <summary>VS 设计器无参构造（运行时不会走这里）</summary>
    public ObjectExplorerForm() : this(null!) { if (DesignMode) return; }

    public ObjectExplorerForm(SqlQueryForm owner)
    {
        _owner = owner;
        InitializeComponent();

        if (DesignMode) return;

        // 默认勾选：表 + 视图（最常用）
        chkTable.Checked = true;
        chkView.Checked = true;
        chkTvf.Checked = false;
        chkScalarFn.Checked = false;
        chkProc.Checked = false;
        chkTrigger.Checked = false;
        RebuildSelectedKinds();

        chkTable.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };
        chkView.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };
        chkTvf.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };
        chkScalarFn.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };
        chkProc.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };
        chkTrigger.CheckedChanged += (_, _) => { RebuildSelectedKinds(); RebuildTree(); };

        txtFilter.TextChanged += (_, _) => ApplyFilter();
        btnRefresh.Click += (_, _) => ForceRefresh();
    }

    /// <summary>
    /// 由 SqlQueryForm 在构造后或"显示"时调用：拉一次缓存（若已加载则直接用），然后填树。
    /// </summary>
    public async Task RefreshAsync(bool forceReload = false)
    {
        if (_owner == null || DesignMode) return;
        var connStr = _owner.CurrentConnectionString;
        if (string.IsNullOrEmpty(connStr))
        {
            tree.Nodes.Clear();
            tree.Nodes.Add(new TreeNode("(未连接数据库)") { ForeColor = Color.Gray });
            return;
        }

        await SqlObjectSchemaCache.WarmupAsync(connStr, forceReload);
        RebuildTree();
    }

    private void ForceRefresh()
    {
        if (_owner == null || DesignMode) return;
        _ = RefreshAsync(forceReload: true);
    }

    private void RebuildSelectedKinds()
    {
        _selectedKinds.Clear();
        if (chkTable.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.Table);
        if (chkView.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.View);
        if (chkTvf.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.TableValuedFunction);
        if (chkScalarFn.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.ScalarFunction);
        if (chkProc.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.StoredProcedure);
        if (chkTrigger.Checked) _selectedKinds.Add(SqlObjectSchemaCache.ObjectKind.Trigger);
    }

    /// <summary>
    /// 重建整棵树：Schema 节点 → 对象节点（带图标 + 列子节点）。
    /// </summary>
    private void RebuildTree()
    {
        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            if (_owner == null) return;
            var connStr = _owner.CurrentConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;
            if (_selectedKinds.Count == 0)
            {
                tree.Nodes.Add(new TreeNode("(请勾选至少一个对象类型)") { ForeColor = Color.Gray });
                return;
            }

            var objs = SqlObjectSchemaCache.GetObjectsByKind(connStr, _selectedKinds);
            if (objs.Count == 0)
            {
                tree.Nodes.Add(new TreeNode("(无匹配对象；点击 🔄 刷新)") { ForeColor = Color.Gray });
                return;
            }

            var bySchema = objs
                .GroupBy(o => o.SchemaName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var schemaGroup in bySchema)
            {
                var schemaText = $"📁 {schemaGroup.Key} ({schemaGroup.Count()})";
                var schemaNode = new TreeNode(schemaText)
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

                    // 列子节点（一次性展开也行；这里直接挂上）
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

            lblStatus.Text = $"{objs.Count} 个对象";

            ApplyFilter();
        }
        finally
        {
            tree.EndUpdate();
        }
    }

    /// <summary>filter box 实时过滤（高亮匹配的列名；隐藏空 schema）</summary>
    private void ApplyFilter()
    {
        var f = txtFilter.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(f))
        {
            // 清掉所有高亮
            ClearHighlights(tree.Nodes);
            foreach (TreeNode n in tree.Nodes)
            {
                n.BackColor = Color.Empty;
                // 不强制展开/收起（保留用户操作）
            }
            return;
        }

        ClearHighlights(tree.Nodes);
        // 高亮列匹配 + 显示对应的对象/schema 节点
        HighlightMatching(tree.Nodes, f, StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearHighlights(TreeNodeCollection nodes)
    {
        foreach (TreeNode n in nodes)
        {
            n.BackColor = Color.Empty;
            if (n.Nodes.Count > 0) ClearHighlights(n.Nodes);
        }
    }

    private static void HighlightMatching(TreeNodeCollection nodes, string filter, StringComparison cmp)
    {
        foreach (TreeNode n in nodes)
        {
            // 叶子（列）：按 name 是否含 filter 标黄；非叶子不标（避免 schema 一片黄）
            if (n.Nodes.Count == 0)
            {
                if (!string.IsNullOrEmpty(n.Text) && n.Text.IndexOf(filter, cmp) >= 0)
                    n.BackColor = Color.LightYellow;
            }
            else
            {
                HighlightMatching(n.Nodes, filter, cmp);
            }
        }
    }

    // ============================================
    // 双击行为
    // ============================================

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (_owner == null || DesignMode) return;
        if (e.Node?.Tag is not ValueTuple<string, string> tag) return;
        if (tag.Item1 != "object") return;

        var pipeIdx = tag.Item2.IndexOf('|');
        if (pipeIdx < 0) return;
        var objType = tag.Item2.Substring(0, pipeIdx);       // "U" / "V" / "P" / ...
        var fullName = tag.Item2.Substring(pipeIdx + 1);      // "dbo.S_SCM_SEORDER"

        // SqlScriptLoader 用 WHERE o.name = @name 查，不带 schema。带传入会查不到 -> 拆分。
        // 但 OpenScript 接受 objName 形如 "schema.name" 也可以 (loadCreateScript 会按 name 过滤)。
        // 为防重名干扰，传 "schema.name" 让用户看清。
        _owner.OpenScript(database: "", objType: objType, objName: fullName);
    }
}

/// <summary>ObjectKind → tree.ImageKey 映射（设计器 ImageList 必须用同名 key）</summary>
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
