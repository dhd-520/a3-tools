namespace A3Tools.Plugins.Default.Forms;

partial class ObjectExplorerForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows 窗体设计器生成的代码

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources =
            new System.ComponentModel.ComponentResourceManager(typeof(ObjectExplorerForm));

        this.txtFilter = new System.Windows.Forms.TextBox();
        this.btnRefresh = new System.Windows.Forms.Button();
        this.tree = new System.Windows.Forms.TreeView();
        this.toolStrip = new System.Windows.Forms.FlowLayoutPanel();
        this.chkTable = new System.Windows.Forms.CheckBox();
        this.chkView = new System.Windows.Forms.CheckBox();
        this.chkTvf = new System.Windows.Forms.CheckBox();
        this.chkScalarFn = new System.Windows.Forms.CheckBox();
        this.chkProc = new System.Windows.Forms.CheckBox();
        this.chkTrigger = new System.Windows.Forms.CheckBox();
        this.lblStatus = new System.Windows.Forms.Label();
        this.imageList = new System.Windows.Forms.ImageList(components);

        this.toolStrip.SuspendLayout();
        this.SuspendLayout();

        // ──────────────────────────────────────────────────────
        // txtFilter
        // ──────────────────────────────────────────────────────
        this.txtFilter.Location = new System.Drawing.Point(8, 8);
        this.txtFilter.Name = "txtFilter";
        this.txtFilter.Size = new System.Drawing.Size(180, 23);
        this.txtFilter.TabIndex = 0;
        this.txtFilter.PlaceholderText = "筛选名称…";

        // ──────────────────────────────────────────────────────
        // btnRefresh
        // ──────────────────────────────────────────────────────
        this.btnRefresh.Location = new System.Drawing.Point(192, 7);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new System.Drawing.Size(38, 24);
        this.btnRefresh.TabIndex = 1;
        this.btnRefresh.Text = "🔄";
        this.btnRefresh.UseVisualStyleBackColor = true;

        // ──────────────────────────────────────────────────────
        // toolStrip（包含 6 个 CheckBox）
        // ──────────────────────────────────────────────────────
        this.toolStrip.AutoSize = true;
        this.toolStrip.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        this.toolStrip.WrapContents = false;
        this.toolStrip.Location = new System.Drawing.Point(8, 36);
        this.toolStrip.Name = "toolStrip";
        this.toolStrip.Size = new System.Drawing.Size(280, 28);
        this.toolStrip.TabIndex = 2;

        ConfigureCheck(ref this.chkTable, "表", "U");
        ConfigureCheck(ref this.chkView, "视图", "V");
        ConfigureCheck(ref this.chkTvf, "表值函数", "IF/TF");
        ConfigureCheck(ref this.chkScalarFn, "标量函数", "FN");
        ConfigureCheck(ref this.chkProc, "存储过程", "P");
        ConfigureCheck(ref this.chkTrigger, "触发器", "TR");

        this.toolStrip.Controls.Add(this.chkTable);
        this.toolStrip.Controls.Add(this.chkView);
        this.toolStrip.Controls.Add(this.chkTvf);
        this.toolStrip.Controls.Add(this.chkScalarFn);
        this.toolStrip.Controls.Add(this.chkProc);
        this.toolStrip.Controls.Add(this.chkTrigger);

        // ──────────────────────────────────────────────────────
        // tree
        // ──────────────────────────────────────────────────────
        this.tree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.tree.HideSelection = false;
        this.tree.Location = new System.Drawing.Point(8, 70);
        this.tree.Name = "tree";
        this.tree.Size = new System.Drawing.Size(284, 380);
        this.tree.TabIndex = 3;
        this.tree.ImageList = this.imageList;
        this.tree.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.Tree_NodeMouseDoubleClick);

        // ──────────────────────────────────────────────────────
        // lblStatus
        // ──────────────────────────────────────────────────────
        this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.lblStatus.Location = new System.Drawing.Point(8, 455);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(284, 18);
        this.lblStatus.TabIndex = 4;
        this.lblStatus.Text = "（未加载）";
        this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this.lblStatus.ForeColor = System.Drawing.Color.Gray;

        // ──────────────────────────────────────────────────────
        // imageList
        // ──────────────────────────────────────────────────────
        this.imageList.ImageSize = new System.Drawing.Size(16, 16);
        this.imageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
        this.imageList.TransparentColor = System.Drawing.Color.Transparent;

        // 7 个 key：schema / table / view / tvf / fn / proc / trigger / column
        // 用空 ImageList 起步，运行时如果需要可注入图标（先以 emoji 替代）
        // ━━ 这里如果 A3Tools/Icons 下有现成图标可直接读取，先 make placeholders
        TryAddPlaceholderImage(this.imageList, "schema", System.Drawing.Color.LightSlateGray);
        TryAddPlaceholderImage(this.imageList, "table", System.Drawing.Color.FromArgb(40, 120, 220));
        TryAddPlaceholderImage(this.imageList, "view", System.Drawing.Color.FromArgb(120, 80, 200));
        TryAddPlaceholderImage(this.imageList, "tvf", System.Drawing.Color.FromArgb(220, 130, 40));
        TryAddPlaceholderImage(this.imageList, "fn", System.Drawing.Color.FromArgb(220, 60, 60));
        TryAddPlaceholderImage(this.imageList, "proc", System.Drawing.Color.FromArgb(40, 160, 80));
        TryAddPlaceholderImage(this.imageList, "trigger", System.Drawing.Color.FromArgb(180, 90, 180));
        TryAddPlaceholderImage(this.imageList, "column", System.Drawing.Color.FromArgb(140, 140, 140));

        // ──────────────────────────────────────────────────────
        // ObjectExplorerForm
        // ──────────────────────────────────────────────────────
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(300, 480);
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.tree);
        this.Controls.Add(this.toolStrip);
        this.Controls.Add(this.btnRefresh);
        this.Controls.Add(this.txtFilter);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        this.KeyPreview = true;
        this.MinimumSize = new System.Drawing.Size(260, 320);
        this.Name = "ObjectExplorerForm";
        this.ShowInTaskbar = false;
        this.Text = "对象资源管理器";
        this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        // 默认放在父窗体右侧
        if (Owner != null)
        {
            this.Location = new System.Drawing.Point(Owner.Right + 4, Owner.Top);
            this.Height = Owner.Height;
            this.MinimumSize = new System.Drawing.Size(260, Owner.Height);
        }

        this.toolStrip.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    /// <summary>创建占位色块图（如果项目里有更合适的图标再替）</summary>
    private static void TryAddPlaceholderImage(ImageList il, string key, Color color)
    {
        try
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, 2, 2, 12, 12);
                using var pen = new Pen(Color.FromArgb(80, 80, 80));
                g.DrawRectangle(pen, 2, 2, 12, 12);
            }
            il.Images.Add(key, bmp);
        }
        catch { /* DesignTime skip */ }
    }

    private static void ConfigureCheck(ref CheckBox c, string text, string tag)
    {
        c.AutoSize = true;
        c.Margin = new Padding(2);
        c.Text = text;
        c.Tag = tag;
        c.UseVisualStyleBackColor = true;
    }

    #endregion

    private TextBox txtFilter;
    private Button btnRefresh;
    private FlowLayoutPanel toolStrip;
    private CheckBox chkTable, chkView, chkTvf, chkScalarFn, chkProc, chkTrigger;
    private TreeView tree;
    private Label lblStatus;
    private ImageList imageList;
}
