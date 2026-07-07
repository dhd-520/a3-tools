using System.Drawing;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Forms;

partial class SqlQueryTabPage
{
    private System.ComponentModel.IContainer components = null;

    private Panel pnlToolBar;
    private Button btnExecute;
    private Button btnExecuteSelected;
    private Button btnStop;
    private Button btnSave;
    private Label lblHint;

    private SplitContainer splitContainer;

    private Panel pnlEditorContainer;
    private LineNumberPanel pnlLineNumbers;
    private SqlEditor rtbEditor;

    private TabControl tabResultSwitcher;
    private TabPage tabResult;
    private DataGridView dgvResult;
    private TabPage tabMessages;
    private RichTextBox rtbMessages;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        pnlToolBar = new Panel();
        btnExecute = new Button();
        btnExecuteSelected = new Button();
        btnStop = new Button();
        btnSave = new Button();
        lblHint = new Label();

        splitContainer = new SplitContainer();

        pnlEditorContainer = new Panel();
        pnlLineNumbers = new LineNumberPanel();
        rtbEditor = new SqlEditor();

        tabResultSwitcher = new TabControl();
        tabResult = new TabPage();
        dgvResult = new DataGridView();
        tabMessages = new TabPage();
        rtbMessages = new RichTextBox();

        // ===== 内工具栏 =====
        pnlToolBar.Dock = DockStyle.Top;
        pnlToolBar.Height = 44;
        pnlToolBar.Padding = new Padding(6, 4, 6, 4);

        btnExecute.Text = "▶ 执行 (F5)";
        btnExecute.Location = new Point(6, 4);
        btnExecute.Size = new Size(105, 36);
        btnExecute.BackColor = Color.FromArgb(24, 144, 255);
        btnExecute.ForeColor = Color.White;
        btnExecute.FlatStyle = FlatStyle.Flat;
        btnExecute.FlatAppearance.BorderSize = 0;
        btnExecute.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        btnExecute.Click += BtnExecute_Click;

        btnExecuteSelected.Text = "▶ 选中 (Ctrl+F5)";
        btnExecuteSelected.Location = new Point(118, 4);
        btnExecuteSelected.Size = new Size(140, 36);
        btnExecuteSelected.Click += BtnExecuteSelected_Click;

        btnStop.Text = "⏹ 停止";
        btnStop.Location = new Point(265, 4);
        btnStop.Size = new Size(100, 36);
        btnStop.Enabled = false;
        btnStop.Click += BtnStop_Click;

        btnSave.Text = "💾 保存脚本";
        btnSave.Location = new Point(372, 4);
        btnSave.Size = new Size(110, 36);
        btnSave.Click += BtnSave_Click;

        lblHint.AutoSize = true;
        lblHint.Location = new Point(495, 14);
        lblHint.Text = "提示: F5=执行  Ctrl+F5=执行选中  Ctrl+L=清空消息  Ctrl+/=注释  Ctrl+Shift+/=取消注释";
        lblHint.ForeColor = Color.FromArgb(140, 140, 140);

        pnlToolBar.Controls.AddRange(new Control[] {
            btnExecute, btnExecuteSelected, btnStop, btnSave, lblHint
        });

        // ===== 编辑器区 =====
        pnlEditorContainer.Dock = DockStyle.Fill;
        pnlEditorContainer.Padding = new Padding(0);

        pnlLineNumbers.Dock = DockStyle.Left;
        pnlLineNumbers.Width = 44;

        rtbEditor.Dock = DockStyle.Fill;
        rtbEditor.BorderStyle = BorderStyle.None;
        rtbEditor.Font = new Font("Consolas", 12F);
        rtbEditor.BackColor = Color.White;
        rtbEditor.WordWrap = false;
        rtbEditor.AcceptsTab = true;
        rtbEditor.HideSelection = false;
        rtbEditor.DetectUrls = false;

        pnlEditorContainer.Controls.Add(rtbEditor);
        pnlEditorContainer.Controls.Add(pnlLineNumbers);

        pnlLineNumbers.Bind(rtbEditor);

        // ===== SplitContainer =====
        splitContainer.Dock = DockStyle.Fill;
        splitContainer.Orientation = Orientation.Horizontal;
        splitContainer.SplitterDistance = 280;
        splitContainer.Panel1MinSize = 80;
        splitContainer.Panel2MinSize = 80;
        splitContainer.FixedPanel = FixedPanel.None;

        splitContainer.Panel1.Controls.Add(pnlEditorContainer);

        // ===== 结果/消息 Tab =====
        tabResultSwitcher.Dock = DockStyle.Fill;
        tabResultSwitcher.Padding = new Point(8, 4);

        // 结果 Tab
        tabResult.Text = "结果";
        dgvResult.Dock = DockStyle.Fill;
        dgvResult.AllowUserToAddRows = false;
        dgvResult.AllowUserToDeleteRows = false;
        dgvResult.ReadOnly = true;
        dgvResult.RowHeadersVisible = false;
        dgvResult.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        dgvResult.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvResult.BackgroundColor = Color.White;
        dgvResult.BorderStyle = BorderStyle.None;
        dgvResult.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        dgvResult.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 242, 245);
        dgvResult.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 230, 245);
        dgvResult.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        // 右键复制
        var ctx = new ContextMenuStrip(components);
        var miCopyCell = new ToolStripMenuItem("复制单元格");
        miCopyCell.Click += (s, e) => CopySelectedCell();
        var miCopyRow = new ToolStripMenuItem("复制整行（TSV）");
        miCopyRow.Click += (s, e) => CopySelectedRow();
        var miCopyAll = new ToolStripMenuItem("复制全部（TSV）");
        miCopyAll.Click += (s, e) => CopyAllToClipboard();
        ctx.Items.AddRange(new ToolStripItem[] { miCopyCell, miCopyRow, new ToolStripSeparator(), miCopyAll });
        dgvResult.ContextMenuStrip = ctx;
        tabResult.Controls.Add(dgvResult);

        // 消息 Tab
        tabMessages.Text = "消息";
        rtbMessages.Dock = DockStyle.Fill;
        rtbMessages.ReadOnly = true;
        rtbMessages.BackColor = Color.FromArgb(30, 30, 30);
        rtbMessages.ForeColor = Color.FromArgb(220, 220, 220);
        rtbMessages.Font = new Font("Consolas", 9.5F);
        rtbMessages.BorderStyle = BorderStyle.None;
        rtbMessages.WordWrap = false;
        tabMessages.Controls.Add(rtbMessages);

        tabResultSwitcher.TabPages.AddRange(new TabPage[] { tabResult, tabMessages });
        splitContainer.Panel2.Controls.Add(tabResultSwitcher);

        // ===== UserControl =====
        Controls.Add(splitContainer);
        Controls.Add(pnlToolBar);
    }
}