namespace A3Tools.Forms;

partial class KnowledgeBaseForm
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
        this.components = new System.ComponentModel.Container();

        this.tsTop = new System.Windows.Forms.ToolStrip();
        this.tsbNewBase = new System.Windows.Forms.ToolStripButton();
        this.tsbDeleteBase = new System.Windows.Forms.ToolStripButton();
        this.tsbRenameBase = new System.Windows.Forms.ToolStripButton();
        this.tsSeparator1 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbSetWatchFolder = new System.Windows.Forms.ToolStripButton();
        this.tsbScanFolder = new System.Windows.Forms.ToolStripButton();
        this.tsSeparator2 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbAddEntry = new System.Windows.Forms.ToolStripButton();
        this.tsbDeleteEntry = new System.Windows.Forms.ToolStripButton();
        this.tsSeparator3 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbAiExtract = new System.Windows.Forms.ToolStripButton();
        this.tsbSearch = new System.Windows.Forms.ToolStripButton();
        this.tsSeparator4 = new System.Windows.Forms.ToolStripSeparator();
        this.tsbSelectAll = new System.Windows.Forms.ToolStripButton();
        this.tsbSelectNone = new System.Windows.Forms.ToolStripButton();
        this.tsbDeleteSelected = new System.Windows.Forms.ToolStripButton();
        this.tslSelectedCount = new System.Windows.Forms.ToolStripLabel();

        this.scMain = new System.Windows.Forms.SplitContainer();
        this.pnlBaseList = new System.Windows.Forms.Panel();
        this.lblBases = new System.Windows.Forms.Label();
        this.lstBases = new System.Windows.Forms.ListBox();

        this.scRight = new System.Windows.Forms.SplitContainer();
        this.pnlEntryList = new System.Windows.Forms.Panel();
        this.lblEntries = new System.Windows.Forms.Label();
        this.txtSearch = new System.Windows.Forms.TextBox();
        this.lstEntries = new System.Windows.Forms.ListView();

        this.pnlEntryEdit = new System.Windows.Forms.Panel();
        this.lblTitle = new System.Windows.Forms.Label();
        this.txtTitle = new System.Windows.Forms.TextBox();
        this.lblTags = new System.Windows.Forms.Label();
        this.txtTags = new System.Windows.Forms.TextBox();
        this.lblSource = new System.Windows.Forms.Label();
        this.txtSource = new System.Windows.Forms.TextBox();
        this.lblContent = new System.Windows.Forms.Label();
        this.txtContent = new System.Windows.Forms.TextBox();
        this.lblCharCount = new System.Windows.Forms.Label();
        this.btnSaveEntry = new System.Windows.Forms.Button();
        this.btnCancelEdit = new System.Windows.Forms.Button();

        this.ssBottom = new System.Windows.Forms.StatusStrip();
        this.tsslStatus = new System.Windows.Forms.ToolStripStatusLabel();

        this.tsTop.SuspendLayout();
        this.scMain.Panel1.SuspendLayout();
        this.scMain.Panel2.SuspendLayout();
        this.scMain.SuspendLayout();
        this.pnlBaseList.SuspendLayout();
        this.scRight.Panel1.SuspendLayout();
        this.scRight.Panel2.SuspendLayout();
        this.scRight.SuspendLayout();
        this.pnlEntryList.SuspendLayout();
        this.pnlEntryEdit.SuspendLayout();
        this.SuspendLayout();

        // ━━━━━ tsTop 工具栏 ━━━━━
        this.tsTop.Dock = System.Windows.Forms.DockStyle.Top;
        this.tsTop.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
        this.tsTop.ImageScalingSize = new System.Drawing.Size(20, 20);
        this.tsTop.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
        this.tsTop.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbNewBase, this.tsbDeleteBase, this.tsbRenameBase,
            this.tsSeparator1,
            this.tsbSetWatchFolder, this.tsbScanFolder,
            this.tsSeparator2,
            this.tsbAddEntry, this.tsbDeleteEntry,
            this.tsSeparator3,
            this.tsbAiExtract,
            this.tsbSearch,
            this.tsSeparator4,
            this.tsbSelectAll, this.tsbSelectNone, this.tsbDeleteSelected, this.tslSelectedCount
        });

        this.tsbNewBase.Text = "➕ 新建知识库";
        this.tsbDeleteBase.Text = "🗑 删除知识库";
        this.tsbRenameBase.Text = "✏ 重命名";
        this.tsbSetWatchFolder.Text = "📁 设置扫描文件夹";
        this.tsbScanFolder.Text = "🔄 扫描文件夹";
        this.tsbAddEntry.Text = "➕ 新建条目";
        this.tsbDeleteEntry.Text = "🗑 删除条目";
        this.tsbAiExtract.Text = "🤖 AI 提取";
        this.tsbAiExtract.Click += new System.EventHandler(this.TsbAiExtract_Click);
        this.tsbSearch.Text = "🔍 搜索";
        this.tsbSelectAll.Text = "☑️ 全选";
        this.tsbSelectAll.Click += new System.EventHandler(this.TsbSelectAll_Click);
        this.tsbSelectNone.Text = "☐️ 全不选";
        this.tsbSelectNone.Click += new System.EventHandler(this.TsbSelectNone_Click);
        this.tsbDeleteSelected.Text = "🗑 删除选中(0)";
        this.tsbDeleteSelected.Click += new System.EventHandler(this.TsbDeleteSelected_Click);
        this.tslSelectedCount.Text = "";

        // ━━━━━ scMain 左右分栏 ━━━━━
        this.scMain.Dock = System.Windows.Forms.DockStyle.Fill;
        this.scMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
        this.scMain.SplitterDistance = 250;
        this.scMain.SplitterWidth = 6;

        // pnlBaseList 左侧
        this.pnlBaseList.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pnlBaseList.Padding = new System.Windows.Forms.Padding(8);
        this.pnlBaseList.Controls.Add(this.lstBases);
        this.pnlBaseList.Controls.Add(this.lblBases);

        this.lblBases.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblBases.Text = "📚 知识库";
        this.lblBases.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.lblBases.Height = 28;
        this.lblBases.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        this.lstBases.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lstBases.IntegralHeight = false;
        this.lstBases.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this.lstBases.SelectedIndexChanged += new System.EventHandler(this.LstBases_SelectedIndexChanged);

        this.scMain.Panel1.Controls.Add(this.pnlBaseList);

        // ━━━━━ scRight 右上/右下分栏 ━━━━━
        this.scRight.Dock = System.Windows.Forms.DockStyle.Fill;
        this.scRight.Orientation = System.Windows.Forms.Orientation.Horizontal;
        this.scRight.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
        this.scRight.SplitterDistance = 280;
        this.scRight.SplitterWidth = 6;

        // pnlEntryList 中上
        this.pnlEntryList.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pnlEntryList.Padding = new System.Windows.Forms.Padding(8);
        this.pnlEntryList.Controls.Add(this.lstEntries);
        this.pnlEntryList.Controls.Add(this.txtSearch);
        this.pnlEntryList.Controls.Add(this.lblEntries);

        this.lblEntries.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblEntries.Text = "📄 条目列表";
        this.lblEntries.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.lblEntries.Height = 28;
        this.lblEntries.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        this.txtSearch.Dock = System.Windows.Forms.DockStyle.Top;
        this.txtSearch.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this.txtSearch.PlaceholderText = "🔍 输入关键词搜索(标题/标签/内容)";
        this.txtSearch.TextChanged += new System.EventHandler(this.TxtSearch_TextChanged);

        this.lstEntries.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lstEntries.View = System.Windows.Forms.View.Details;
        this.lstEntries.FullRowSelect = true;
        this.lstEntries.GridLines = true;
        this.lstEntries.MultiSelect = true;
        this.lstEntries.CheckBoxes = true;
        this.lstEntries.HideSelection = false;
        this.lstEntries.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.lstEntries.Columns.Add("标题", 280);
        this.lstEntries.Columns.Add("来源", 100);
        this.lstEntries.Columns.Add("标签", 180);
        this.lstEntries.Columns.Add("文件路径", 220);
        this.lstEntries.Columns.Add("更新时间", 140);
        this.lstEntries.SelectedIndexChanged += new System.EventHandler(this.LstEntries_SelectedIndexChanged);
        this.lstEntries.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.LstEntries_ItemChecked);

        this.scRight.Panel1.Controls.Add(this.pnlEntryList);

        // pnlEntryEdit 右下
        this.pnlEntryEdit.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pnlEntryEdit.Padding = new System.Windows.Forms.Padding(8);
        this.pnlEntryEdit.Controls.Add(this.lblCharCount);
        this.pnlEntryEdit.Controls.Add(this.btnCancelEdit);
        this.pnlEntryEdit.Controls.Add(this.btnSaveEntry);
        this.pnlEntryEdit.Controls.Add(this.txtContent);
        this.pnlEntryEdit.Controls.Add(this.lblContent);
        this.pnlEntryEdit.Controls.Add(this.txtSource);
        this.pnlEntryEdit.Controls.Add(this.lblSource);
        this.pnlEntryEdit.Controls.Add(this.txtTags);
        this.pnlEntryEdit.Controls.Add(this.lblTags);
        this.pnlEntryEdit.Controls.Add(this.txtTitle);
        this.pnlEntryEdit.Controls.Add(this.lblTitle);

        this.lblTitle.AutoSize = true;
        this.lblTitle.Location = new System.Drawing.Point(8, 10);
        this.lblTitle.Text = "标题:";

        this.txtTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtTitle.Location = new System.Drawing.Point(50, 7);
        this.txtTitle.Size = new System.Drawing.Size(this.pnlEntryEdit.Width - 60, 27);
        this.txtTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);

        this.lblTags.AutoSize = true;
        this.lblTags.Location = new System.Drawing.Point(8, 42);
        this.lblTags.Text = "标签:";

        this.txtTags.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtTags.Location = new System.Drawing.Point(50, 39);
        this.txtTags.Size = new System.Drawing.Size(this.pnlEntryEdit.Width - 60, 27);
        this.txtTags.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this.txtTags.PlaceholderText = "逗号分隔,如: 账套,启动,A3Client";

        this.lblSource.AutoSize = true;
        this.lblSource.Location = new System.Drawing.Point(8, 74);
        this.lblSource.Text = "来源:";

        this.txtSource.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtSource.Location = new System.Drawing.Point(50, 71);
        this.txtSource.Size = new System.Drawing.Size(this.pnlEntryEdit.Width - 60, 27);
        this.txtSource.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.txtSource.ReadOnly = true;
        this.txtSource.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);

        this.lblContent.AutoSize = true;
        this.lblContent.Location = new System.Drawing.Point(8, 106);
        this.lblContent.Text = "内容(Markdown):";

        this.txtContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtContent.Location = new System.Drawing.Point(8, 128);
        this.txtContent.Multiline = true;
        this.txtContent.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        this.txtContent.WordWrap = false;
        this.txtContent.Font = new System.Drawing.Font("Cascadia Mono, Consolas, Microsoft YaHei UI", 10F);
        this.txtContent.AcceptsTab = true;
        this.txtContent.AcceptsReturn = true;

        this.btnSaveEntry.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnSaveEntry.Location = new System.Drawing.Point(this.pnlEntryEdit.Width - 180, this.pnlEntryEdit.Height - 42);
        this.btnSaveEntry.Size = new System.Drawing.Size(80, 32);
        this.btnSaveEntry.Text = "💾 保存";
        this.btnSaveEntry.UseVisualStyleBackColor = true;
        this.btnSaveEntry.Click += new System.EventHandler(this.BtnSaveEntry_Click);

        this.btnCancelEdit.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnCancelEdit.Location = new System.Drawing.Point(this.pnlEntryEdit.Width - 90, this.pnlEntryEdit.Height - 42);
        this.btnCancelEdit.Size = new System.Drawing.Size(80, 32);
        this.btnCancelEdit.Text = "↩ 取消";
        this.btnCancelEdit.UseVisualStyleBackColor = true;
        this.btnCancelEdit.Click += new System.EventHandler(this.BtnCancelEdit_Click);

        this.lblCharCount.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.lblCharCount.AutoSize = true;
        this.lblCharCount.Location = new System.Drawing.Point(8, this.pnlEntryEdit.Height - 36);
        this.lblCharCount.Text = "字符:0";
        this.lblCharCount.ForeColor = System.Drawing.Color.Gray;

        this.scRight.Panel2.Controls.Add(this.pnlEntryEdit);
        this.scMain.Panel2.Controls.Add(this.scRight);

        // ━━━━━ ssBottom 状态栏 ━━━━━
        this.ssBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.ssBottom.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsslStatus });
        this.tsslStatus.Text = "就绪";

        // ━━━━━ Form ━━━━━
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1200, 700);
        this.MinimumSize = new System.Drawing.Size(900, 500);
        this.Controls.Add(this.scMain);
        this.Controls.Add(this.tsTop);
        this.Controls.Add(this.ssBottom);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "知识库管理 - A3Tools";
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.KnowledgeBaseForm_FormClosing);

        this.tsTop.ResumeLayout(false);
        this.scMain.Panel1.ResumeLayout(false);
        this.scMain.Panel2.ResumeLayout(false);
        this.scMain.ResumeLayout(false);
        this.pnlBaseList.ResumeLayout(false);
        this.scRight.Panel1.ResumeLayout(false);
        this.scRight.Panel2.ResumeLayout(false);
        this.scRight.ResumeLayout(false);
        this.pnlEntryList.ResumeLayout(false);
        this.pnlEntryEdit.ResumeLayout(false);
        this.pnlEntryEdit.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.ToolStrip tsTop;
    private System.Windows.Forms.ToolStripButton tsbNewBase;
    private System.Windows.Forms.ToolStripButton tsbDeleteBase;
    private System.Windows.Forms.ToolStripButton tsbRenameBase;
    private System.Windows.Forms.ToolStripSeparator tsSeparator1;
    private System.Windows.Forms.ToolStripButton tsbSetWatchFolder;
    private System.Windows.Forms.ToolStripButton tsbScanFolder;
    private System.Windows.Forms.ToolStripSeparator tsSeparator2;
    private System.Windows.Forms.ToolStripButton tsbAddEntry;
    private System.Windows.Forms.ToolStripButton tsbDeleteEntry;
    private System.Windows.Forms.ToolStripSeparator tsSeparator3;
    private System.Windows.Forms.ToolStripButton tsbAiExtract;
    private System.Windows.Forms.ToolStripButton tsbSearch;
    private System.Windows.Forms.ToolStripSeparator tsSeparator4;
    private System.Windows.Forms.ToolStripButton tsbSelectAll;
    private System.Windows.Forms.ToolStripButton tsbSelectNone;
    private System.Windows.Forms.ToolStripButton tsbDeleteSelected;
    private System.Windows.Forms.ToolStripLabel tslSelectedCount;

    private System.Windows.Forms.SplitContainer scMain;
    private System.Windows.Forms.Panel pnlBaseList;
    private System.Windows.Forms.Label lblBases;
    private System.Windows.Forms.ListBox lstBases;

    private System.Windows.Forms.SplitContainer scRight;
    private System.Windows.Forms.Panel pnlEntryList;
    private System.Windows.Forms.Label lblEntries;
    private System.Windows.Forms.TextBox txtSearch;
    private System.Windows.Forms.ListView lstEntries;

    private System.Windows.Forms.Panel pnlEntryEdit;
    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.TextBox txtTitle;
    private System.Windows.Forms.Label lblTags;
    private System.Windows.Forms.TextBox txtTags;
    private System.Windows.Forms.Label lblSource;
    private System.Windows.Forms.TextBox txtSource;
    private System.Windows.Forms.Label lblContent;
    private System.Windows.Forms.TextBox txtContent;
    private System.Windows.Forms.Label lblCharCount;
    private System.Windows.Forms.Button btnSaveEntry;
    private System.Windows.Forms.Button btnCancelEdit;

    private System.Windows.Forms.StatusStrip ssBottom;
    private System.Windows.Forms.ToolStripStatusLabel tsslStatus;
}