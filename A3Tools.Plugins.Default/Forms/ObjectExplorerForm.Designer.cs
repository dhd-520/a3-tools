namespace A3Tools.Plugins.Default.Forms;

partial class ObjectExplorerForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 释放所有 ImageList 的 bitmap（GDI handle）
            imageList?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows 窗体设计器生成的代码

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        this.pnlTop = new Panel();
        this.btnRefreshRoot = new Button();
        this.lblStatus = new Label();
        this.btnMode = new Button();

        this.tabExplorer = new TabControl();
        this.tabPageTable = new TabPage();
        this.tabPageView = new TabPage();
        this.tabPageTvf = new TabPage();
        this.tabPageScalarFn = new TabPage();
        this.tabPageProc = new TabPage();
        this.tabPageTrigger = new TabPage();

        BuildTab(ref this.tabPageTable,    "表 (U)",          out this.treeTable,    out this.txtFilterTable);
        BuildTab(ref this.tabPageView,     "视图 (V)",        out this.treeView,     out this.txtFilterView);
        BuildTab(ref this.tabPageTvf,      "表值函数 (IF/TF)", out this.treeTvf,      out this.txtFilterTvf);
        BuildTab(ref this.tabPageScalarFn, "标量函数 (FN)",   out this.treeScalarFn, out this.txtFilterScalarFn);
        BuildTab(ref this.tabPageProc,     "存储过程 (P)",     out this.treeProc,     out this.txtFilterProc);
        BuildTab(ref this.tabPageTrigger,  "触发器 (TR)",      out this.treeTrigger,  out this.txtFilterTrigger);

        this.imageList = new ImageList(components);
        this.contextMenuTree = new ContextMenuStrip(components);
        this.miCopyName = new ToolStripMenuItem();
        this.miCopyFullName = new ToolStripMenuItem();
        this.miOpenScript = new ToolStripMenuItem();
        this.contextMenuTree.SuspendLayout();

        pnlTop.SuspendLayout();
        tabExplorer.SuspendLayout();
        SuspendLayout();

        // ───── pnlTop
        //
        this.pnlTop.Controls.Add(this.btnRefreshRoot);
        this.pnlTop.Controls.Add(this.btnMode);
        this.pnlTop.Controls.Add(this.lblStatus);
        this.pnlTop.Dock = DockStyle.Top;
        this.pnlTop.Location = new System.Drawing.Point(0, 0);
        this.pnlTop.Name = "pnlTop";
        this.pnlTop.Padding = new Padding(6);
        this.pnlTop.Size = new System.Drawing.Size(320, 36);

        this.btnRefreshRoot.Location = new System.Drawing.Point(6, 6);
        this.btnRefreshRoot.Name = "btnRefreshRoot";
        this.btnRefreshRoot.Size = new System.Drawing.Size(56, 24);
        this.btnRefreshRoot.Text = "🔄 刷新";
        this.btnRefreshRoot.UseVisualStyleBackColor = true;

        this.btnMode.Location = new System.Drawing.Point(66, 6);
        this.btnMode.Name = "btnMode";
        this.btnMode.Size = new System.Drawing.Size(86, 24);
        this.btnMode.Text = "✂ 严格筛选";
        this.btnMode.UseVisualStyleBackColor = true;
        this.btnMode.FlatStyle = System.Windows.Forms.FlatStyle.System;

        this.lblStatus.AutoSize = true;
        this.lblStatus.ForeColor = System.Drawing.Color.Gray;
        this.lblStatus.Location = new System.Drawing.Point(158, 10);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(100, 18);
        this.lblStatus.Text = "(未加载)";

        // ───── tabExplorer (6 个 TabPage)
        //
        this.tabExplorer.Controls.Add(this.tabPageTable);
        this.tabExplorer.Controls.Add(this.tabPageView);
        this.tabExplorer.Controls.Add(this.tabPageTvf);
        this.tabExplorer.Controls.Add(this.tabPageScalarFn);
        this.tabExplorer.Controls.Add(this.tabPageProc);
        this.tabExplorer.Controls.Add(this.tabPageTrigger);
        this.tabExplorer.Dock = DockStyle.Fill;
        this.tabExplorer.Location = new System.Drawing.Point(0, 36);
        this.tabExplorer.Name = "tabExplorer";
        this.tabExplorer.Size = new System.Drawing.Size(320, 444);
        this.tabExplorer.TabIndex = 0;

        // ───── contextMenuTree
        //
        this.contextMenuTree.Items.AddRange(new ToolStripItem[] {
            this.miOpenScript,
            this.miCopyName,
            this.miCopyFullName
        });
        this.contextMenuTree.Name = "contextMenuTree";
        this.contextMenuTree.Size = new Size(180, 80);
        //
        // miCopyName
        //
        this.miCopyName.Name = "miCopyName";
        this.miCopyName.Size = new Size(179, 22);
        this.miCopyName.Text = "复制对象名";
        //
        // miCopyFullName
        //
        this.miCopyFullName.Name = "miCopyFullName";
        this.miCopyFullName.Size = new Size(179, 22);
        this.miCopyFullName.Text = "复制完整路径";
        //
        // miOpenScript
        //
        this.miOpenScript.Name = "miOpenScript";
        this.miOpenScript.Size = new Size(179, 22);
        this.miOpenScript.Text = "打开脚本";

        // ───── imageList (7 key: schema/table/view/tvf/fn/proc/trigger/column)
        //
        this.imageList.ImageSize = new System.Drawing.Size(16, 16);
        this.imageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
        this.imageList.TransparentColor = System.Drawing.Color.Transparent;
        AddPlaceholderIcon(this.imageList, "table",   System.Drawing.Color.FromArgb(40, 120, 220));
        AddPlaceholderIcon(this.imageList, "view",    System.Drawing.Color.FromArgb(120, 80, 200));
        AddPlaceholderIcon(this.imageList, "tvf",     System.Drawing.Color.FromArgb(220, 130, 40));
        AddPlaceholderIcon(this.imageList, "fn",      System.Drawing.Color.FromArgb(220, 60, 60));
        AddPlaceholderIcon(this.imageList, "proc",    System.Drawing.Color.FromArgb(40, 160, 80));
        AddPlaceholderIcon(this.imageList, "trigger", System.Drawing.Color.FromArgb(180, 90, 180));
        AddPlaceholderIcon(this.imageList, "column",  System.Drawing.Color.FromArgb(140, 140, 140));

        // ───── ObjectExplorerForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(320, 480);
        this.Controls.Add(this.tabExplorer);
        this.Controls.Add(this.pnlTop);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        this.MinimumSize = new System.Drawing.Size(280, 320);
        this.Name = "ObjectExplorerForm";
        this.ShowInTaskbar = false;
        this.Text = "对象资源管理器";
        this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;

        pnlTop.ResumeLayout(false);
        pnlTop.PerformLayout();
        tabExplorer.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>把每 Tab 包成 [TextBox(过滤) | TreeView] 的两段 Panel</summary>
    private void BuildTab(ref TabPage page, string title, out TreeView tree, out TextBox filter)
    {
        var pnl = new Panel { Dock = DockStyle.Fill };
        filter = new TextBox
        {
            Dock = DockStyle.Top,
            PlaceholderText = $"筛选 {title}…",
            Margin = new Padding(4),
            Height = 24
        };
        tree = new TreeView
        {
            Dock = DockStyle.Fill,
            ImageList = this.imageList,         // 共享一份 ImageList
            BorderStyle = System.Windows.Forms.BorderStyle.None,
            HideSelection = false,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
            ContextMenuStrip = this.contextMenuTree
        };
        // 先添加 TreeView，再添加 TextBox（后加的 Dock=Top 永远在上）
        pnl.Controls.Add(tree);
        pnl.Controls.Add(filter);

        page.Controls.Add(pnl);
        page.Name = title;
        page.Text = title;
        page.UseVisualStyleBackColor = true;
    }

    /// <summary>占位色块图（图标资源就位后直接换 ImageList 索引）</summary>
    private static void AddPlaceholderIcon(ImageList il, string key, System.Drawing.Color color)
    {
        try
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using (var brush = new SolidBrush(color))
                    g.FillRectangle(brush, 2, 2, 12, 12);
                using (var pen = new Pen(System.Drawing.Color.FromArgb(80, 80, 80)))
                    g.DrawRectangle(pen, 2, 2, 12, 12);
            }
            // Clone 一份放进 ImageList 避免 bmp dispose 后 IL 持有一份"尸体"
            var ilBmp = new Bitmap(bmp);
            il.Images.Add(key, ilBmp);
        }
        catch { /* DesignTime skip */ }
    }

    #endregion

    // —— 字段
    private Panel pnlTop;
    private Button btnRefreshRoot;
    private Button btnMode;
    private Label lblStatus;

    private TabControl tabExplorer;
    private TabPage tabPageTable, tabPageView, tabPageTvf, tabPageScalarFn, tabPageProc, tabPageTrigger;

    private TreeView treeTable, treeView, treeTvf, treeScalarFn, treeProc, treeTrigger;
    private TextBox txtFilterTable, txtFilterView, txtFilterTvf, txtFilterScalarFn, txtFilterProc, txtFilterTrigger;

    private ImageList imageList;

    private ContextMenuStrip contextMenuTree;
    private ToolStripMenuItem miCopyName;
    private ToolStripMenuItem miCopyFullName;
    private ToolStripMenuItem miOpenScript;
}
