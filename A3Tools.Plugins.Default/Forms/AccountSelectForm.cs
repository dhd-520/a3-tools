using A3Tools.Models;

namespace A3Tools.Plugins.Default.Forms;

public class AccountSelectForm : Form
{
    private readonly List<Account> _accounts;
    private DataGridView dgvAccounts = null!;
    private Button btnOK = null!;
    private Button btnCancel = null!;
    private TextBox txtSearch = null!;

    public Account? SelectedAccount { get; private set; }

    public AccountSelectForm(List<Account> accounts)
    {
        _accounts = accounts;
        InitializeComponent();
        LoadAccounts();
    }

    private void InitializeComponent()
    {
        dgvAccounts = new DataGridView();
        btnOK = new Button();
        btnCancel = new Button();
        txtSearch = new TextBox();

        SuspendLayout();
        // 
        // txtSearch
        // 
        txtSearch.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        txtSearch.Location = new Point(12, 12);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "搜索账套...";
        txtSearch.Size = new Size(500, 38);
        txtSearch.TabIndex = 0;
        txtSearch.TextChanged += TxtSearch_TextChanged;
        // 
        // dgvAccounts
        // 
        dgvAccounts.AllowUserToAddRows = false;
        dgvAccounts.AllowUserToDeleteRows = false;
        dgvAccounts.BackgroundColor = Color.White;
        dgvAccounts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvAccounts.Location = new Point(12, 55);
        dgvAccounts.Name = "dgvAccounts";
        dgvAccounts.ReadOnly = true;
        dgvAccounts.RowTemplate.Height = 30;
        dgvAccounts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvAccounts.Size = new Size(500, 350);
        dgvAccounts.TabIndex = 1;
        dgvAccounts.DoubleClick += DgvAccounts_DoubleClick;
        // 
        // btnOK
        // 
        btnOK.BackColor = Color.FromArgb(24, 145, 176);
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.FlatStyle = FlatStyle.Flat;
        btnOK.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnOK.ForeColor = Color.White;
        btnOK.Location = new Point(200, 415);
        btnOK.Name = "btnOK";
        btnOK.Size = new Size(120, 40);
        btnOK.TabIndex = 2;
        btnOK.Text = "确定";
        btnOK.UseVisualStyleBackColor = false;
        btnOK.Click += BtnOK_Click;
        // 
        // btnCancel
        // 
        btnCancel.BackColor = Color.White;
        btnCancel.FlatAppearance.BorderColor = Color.Gray;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        btnCancel.ForeColor = Color.Gray;
        btnCancel.Location = new Point(330, 415);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(120, 40);
        btnCancel.TabIndex = 3;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = false;
        btnCancel.Click += BtnCancel_Click;
        // 
        // AccountSelectForm
        // 
        AutoScaleDimensions = new SizeF(14F, 30F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(524, 467);
        Controls.Add(txtSearch);
        Controls.Add(dgvAccounts);
        Controls.Add(btnOK);
        Controls.Add(btnCancel);
        Font = new Font("微软雅黑", 10F, FontStyle.Regular, GraphicsUnit.Point);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "AccountSelectForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "选择账套";
        ResumeLayout(false);
        PerformLayout();
    }

    private void LoadAccounts(IEnumerable<Account>? filteredAccounts = null)
    {
        var accounts = filteredAccounts ?? _accounts;
        dgvAccounts.DataSource = null;
        dgvAccounts.DataSource = accounts.Select(a => new
        {
            a.Code,
            a.Name,
            Database = a.Database,
            a.DatabaseName,
            a.Remark
        }).ToList();
        dgvAccounts.Columns[0].HeaderText = "账套代码";
        dgvAccounts.Columns[1].HeaderText = "账套名称";
        dgvAccounts.Columns[2].HeaderText = "数据库地址";
        dgvAccounts.Columns[3].HeaderText = "数据库名称";
        dgvAccounts.Columns[4].HeaderText = "备注";
        dgvAccounts.AutoResizeColumns();
    }

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        var keyword = txtSearch.Text.Trim().ToLower();
        if (string.IsNullOrEmpty(keyword))
        {
            LoadAccounts();
        }
        else
        {
            var filtered = _accounts.Where(a =>
                a.Code.ToLower().Contains(keyword) ||
                a.Name.ToLower().Contains(keyword) ||
                a.Pinyin.ToLower().Contains(keyword) ||
                (a.Remark?.ToLower().Contains(keyword) ?? false)
            ).ToList();
            LoadAccounts(filtered);
        }
    }

    private void DgvAccounts_DoubleClick(object? sender, EventArgs e)
    {
        SelectAndClose();
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        SelectAndClose();
    }

    private void SelectAndClose()
    {
        if (dgvAccounts.SelectedRows.Count > 0)
        {
            var code = dgvAccounts.SelectedRows[0].Cells["Code"].Value?.ToString();
            SelectedAccount = _accounts.FirstOrDefault(a => a.Code == code);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
