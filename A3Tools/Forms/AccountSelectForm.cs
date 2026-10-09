using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using A3Tools.Models;

namespace A3Tools.Forms;

/// <summary>
/// ★ 2026-10-09 账套选择对话框
///   陛下要求：AI 助理顶部选模型左侧加账套选择（非下拉）
///   参照工具箱上方 SelectToolAccount 模式（MainForm.cs L166 引用了 AccountSelectForm 但实际文件丢失）
///   自己实现一个简单选择器：搜索框 + DataGridView 4 列（编码/名称/服务器/备注）+ 确定/取消
///   双击行 = 确定；Enter = 确定
///   SelectedAccount 在 DialogResult.OK 时有效
/// </summary>
public class AccountSelectForm : Form
{
    private TextBox _txtSearch = null!;
    private DataGridView _grid = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;
    private List<Account> _allAccounts = new();

    /// <summary>陛下选中的账套（DialogResult.OK 时有效）</summary>
    public Account? SelectedAccount { get; private set; }

    public AccountSelectForm(List<Account> accounts)
    {
        _allAccounts = accounts ?? new List<Account>();
        InitializeComponent();
        LoadAccounts();
    }

    private void InitializeComponent()
    {
        Text = "选择账套";
        Size = new Size(900, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(250, 251, 253);
        Font = new Font("Microsoft YaHei UI", 9F);

        // 标题
        var lblTitle = new Label
        {
            Text = "请选择账套",
            Location = new Point(15, 15),
            Size = new Size(855, 32),
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 144, 255)
        };

        // 搜索框
        var lblSearch = new Label
        {
            Text = "搜索：",
            Location = new Point(15, 58),
            Size = new Size(50, 28),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _txtSearch = new TextBox
        {
            Location = new Point(70, 55),
            Size = new Size(800, 28),
            PlaceholderText = "输入编码 / 名称 / 服务器 / 备注 模糊匹配（不区分大小写）"
        };
        _txtSearch.TextChanged += (_, _) => LoadAccounts();

        // 账套列表 DataGridView
        _grid = new DataGridView
        {
            Location = new Point(15, 95),
            Size = new Size(855, 360),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            DefaultCellStyle = new DataGridViewCellStyle
            {
                SelectionBackColor = Color.FromArgb(220, 240, 255),
                SelectionForeColor = Color.FromArgb(24, 144, 255),
                Padding = new Padding(4, 6, 4, 6)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(4, 8, 4, 8)
            },
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 32 }
        };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "编码", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称", FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "Server", HeaderText = "服务器", FillWeight = 28 },
            new DataGridViewTextBoxColumn { Name = "Remark", HeaderText = "备注", FillWeight = 20 }
        );
        _grid.CellDoubleClick += (_, _) => { if (_grid.CurrentRow != null) Confirm(); };
        _grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && _grid.CurrentRow != null)
            {
                e.SuppressKeyPress = true;
                Confirm();
            }
        };

        // 底部提示
        var lblHint = new Label
        {
            Text = "💡 双击行 / 按 Enter 选中并确定",
            Location = new Point(15, 465),
            Size = new Size(500, 28),
            ForeColor = Color.FromArgb(150, 150, 150),
            Font = new Font("Microsoft YaHei UI", 8.5F)
        };

        // 确定按钮
        _btnOk = new Button
        {
            Text = "确定",
            Location = new Point(680, 465),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(24, 144, 255),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += (_, _) => Confirm();

        // 取消按钮
        _btnCancel = new Button
        {
            Text = "取消",
            Location = new Point(780, 465),
            Size = new Size(90, 36),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(80, 80, 80),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(lblTitle);
        Controls.Add(lblSearch);
        Controls.Add(_txtSearch);
        Controls.Add(_grid);
        Controls.Add(lblHint);
        Controls.Add(_btnOk);
        Controls.Add(_btnCancel);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void LoadAccounts()
    {
        string kw = _txtSearch.Text.Trim();
        var filtered = string.IsNullOrEmpty(kw)
            ? _allAccounts
            : _allAccounts.Where(a =>
                Contains(a.Code, kw) ||
                Contains(a.Name, kw) ||
                Contains(a.Server, kw) ||
                Contains(a.Remark, kw)
            ).ToList();

        _grid.Rows.Clear();
        foreach (var a in filtered.OrderBy(a => a.Code, StringComparer.OrdinalIgnoreCase))
        {
            _grid.Rows.Add(a.Code, a.Name, a.Server, a.Remark);
        }
        // 默认选中第一行
        if (_grid.Rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _grid.Rows[0].Selected = true;
        }
    }

    private static bool Contains(string? value, string keyword)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private void Confirm()
    {
        if (_grid.CurrentRow == null)
        {
            MessageBox.Show(this, "请先选择账套", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int idx = _grid.CurrentRow.Index;
        if (idx < 0 || idx >= _grid.Rows.Count) return;
        string code = _grid.CurrentRow.Cells["Code"].Value?.ToString() ?? "";
        SelectedAccount = _allAccounts.FirstOrDefault(a => a.Code == code);
        if (SelectedAccount == null)
        {
            MessageBox.Show(this, "账套不存在", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
