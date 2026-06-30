using System.Data;
using System.Diagnostics;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;


namespace A3Tools.Plugins.Default.Forms;

public partial class CrossDbCopyTableForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    // 对象类型常量
    private static readonly Dictionary<string, string> ObjectTypeMap = new()
    {
        { "U",   "表结构" },
        { "V",   "视图" },
        { "TF",  "表值函数" },
        { "FN",  "标量值函数" },
        { "P",   "存储过程" }
    };

    // 搜索区过滤行：存原始 DataTable + 可过滤的 DataView
    private DataTable? _originalDt;
    private DataView? _dataView;

    public CrossDbCopyTableForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        InitObjectTypeCombo();
        LoadPresetAccounts();

        FormHotkeyHelper.Setup(this, () => BtnConfirm_Click(this, EventArgs.Empty));

        // Ctrl+S 选择源账套，Ctrl+D 选择目标账套
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.S && e.Modifiers == Keys.Control) { BtnSelectSource_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D && e.Modifiers == Keys.Control) { BtnSelectTarget_Click(this, EventArgs.Empty); e.SuppressKeyPress = true; }
        };

        // 搜索区 DataGridView 多选 + 复选框联动
        dgvSearchResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSearchResults.MultiSelect = true;

        // 过滤行 TextBox：TextChanged 触发实时过滤
        txtFilterName.TextChanged += (s, e) => ApplyFilter();
        txtFilterType.TextChanged += (s, e) => ApplyFilter();

        // 列宽变化时同步过滤行 TextBox 位置/宽度
        dgvSearchResults.ColumnWidthChanged += (s, e) => SyncFilterRowPositions();
        // 列增删 / DataSource 变化时也要重同步
        dgvSearchResults.ColumnAdded += (s, e) => SyncFilterRowPositions();
        dgvSearchResults.DataSourceChanged += (s, e) => SyncFilterRowPositions();

        dgvSearchResults.SelectionChanged += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk")) return;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null) checkCell.Value = row.Selected;
            }
        };

        // 表头点击全选/取消全选
        dgvSearchResults.ColumnHeaderMouseClick += (s, e) =>
        {
            if (!dgvSearchResults.Columns.Contains("chk") || e.ColumnIndex != 0) return;
            var allChecked = true;
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell == null || checkCell.Value == null || !(bool)checkCell.Value)
                {
                    allChecked = false;
                    break;
                }
            }
            foreach (DataGridViewRow row in dgvSearchResults.Rows)
            {
                var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                if (checkCell != null)
                {
                    checkCell.Value = !allChecked;
                    row.Selected = !allChecked;
                }
            }
        };
    }

    private void InitObjectTypeCombo()
    {
        cboObjectType.Items.Clear();
        foreach (var kv in ObjectTypeMap)
        {
            cboObjectType.Items.Add(new ObjectTypeItem { Value = kv.Key, Display = kv.Value });
        }
        cboObjectType.DisplayMember = "Display";
        cboObjectType.ValueMember = "Value";
        cboObjectType.SelectedIndex = 0;
    }

    private void BtnSelectSource_Click(object? sender, EventArgs e)
    {
        SelectAccount(true);
    }

    private void BtnSelectTarget_Click(object? sender, EventArgs e)
    {
        SelectAccount(false);
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    /// <summary>
    /// 搜索按钮：根据当前选中的对象类型查询源数据库中所有匹配的对象
    /// </summary>
    private void BtnSearch_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text))
        {
            MessageBox.Show("请填写源数据库地址！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtSourceDbName.Text))
        {
            MessageBox.Show("请填写源数据库名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请先选择对象类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var keyword = txtSearchKeyword.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show("请输入搜索关键字！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSearchKeyword.Focus();
            return;
        }

        var objectType = ((ObjectTypeItem)cboObjectType.SelectedItem).Value;

        // 点击查询时清空过滤框（避免旧过滤误伤新结果）
        txtFilterName.Clear();
        txtFilterType.Clear();

        lblSearchProgress.Text = "查询中...";
        lblSearchProgress.ForeColor = Color.Blue;
        dgvSearchResults.DataSource = null;
        btnSearch.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var server = txtSourceServer.Text.Trim();
                var dbName = txtSourceDbName.Text.Trim();
                var user = txtSourceUser.Text.Trim();
                var password = txtSourcePassword.Text;

                var connString = string.IsNullOrEmpty(user)
                    ? $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={server};Database={dbName};User Id={user};Password=" + EncryptionService.Decrypt(password) + ";TrustServerCertificate=True;";

                // 按对象类型查询 sys.objects（过滤系统对象）
                var sql = @"
SELECT o.name AS 对象名称,
       o.type_desc AS 类型描述,
       o.create_date AS 创建时间,
       o.modify_date AS 修改时间
FROM sys.objects o
WHERE o.type = @objType
  AND o.is_ms_shipped = 0
  AND o.name LIKE @keyword
ORDER BY o.name";

                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connString);
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@objType", objectType);
                cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");
                using var adapter = new Microsoft.Data.SqlClient.SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                this.Invoke(new Action(() =>
                {
                    if (dgvSearchResults.Columns.Contains("chk"))
                    {
                        dgvSearchResults.Columns.Remove("chk");
                    }
                    // 用 DataView 包衰装源 DataTable，过滤只走 RowFilter（不修改源数据）
                    _originalDt = dt;
                    _dataView = new DataView(dt);
                    dgvSearchResults.DataSource = _dataView;
                    var checkCol = new DataGridViewCheckBoxColumn
                    {
                        HeaderText = "选择",
                        Width = 50,
                        Name = "chk"
                    };
                    dgvSearchResults.Columns.Insert(0, checkCol);
                    dgvSearchResults.AutoResizeColumns();
                    if (dgvSearchResults.Columns.Contains("创建时间"))
                        dgvSearchResults.Columns["创建时间"].Visible = false;
                    if (dgvSearchResults.Columns.Contains("修改时间"))
                        dgvSearchResults.Columns["修改时间"].Visible = false;
                    if (dgvSearchResults.Rows.Count > 0)
                    {
                        dgvSearchResults.Rows[0].Selected = true;
                    }
                    foreach (DataGridViewRow row in dgvSearchResults.Rows)
                    {
                        var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                        if (checkCell != null) checkCell.Value = row.Selected;
                    }
                    lblSearchProgress.Location = new Point(dgvSearchResults.Left, dgvSearchResults.Bottom + 5);
                    lblSearchProgress.Text = $"查询完成，共 {dt.Rows.Count} 条记录";
                    lblSearchProgress.ForeColor = Color.Green;
                    // 重新应用过滤（新数据加载后过滤条件仍生效）
                    ApplyFilter();
                    // 列都设好了才能拿到真实列宽，此处同步过滤行 TextBox 位置/宽度
                    SyncFilterRowPositions();
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblSearchProgress.Text = "查询失败";
                    lblSearchProgress.ForeColor = Color.Red;
                    MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                this.Invoke(new Action(() =>
                {
                    btnSearch.Enabled = true;
                }));
            }
        });
    }

    /// <summary>
    /// 缺失对象：分别从源库和目标库查询当前选中类型的全部对象名，求差集（源有目标无）展示在下方，默认全选
    /// </summary>
    private void BtnFindMissing_Click(object? sender, EventArgs e)
    {
        // 1. 校验源库
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text) || string.IsNullOrWhiteSpace(txtSourceDbName.Text))
        {
            MessageBox.Show("请填写源数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 2. 校验目标库
        if (string.IsNullOrWhiteSpace(txtTargetServer.Text) || string.IsNullOrWhiteSpace(txtTargetDbName.Text))
        {
            MessageBox.Show("请填写目标数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 3. 校验对象类型
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请先选择对象类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var objectType = ((ObjectTypeItem)cboObjectType.SelectedItem).Value;
        var keyword = txtSearchKeyword.Text.Trim();
        var typeDisplay = GetTypeDisplay(objectType);

        // 点击缺失对象时清空过滤框（避免旧过滤误伤新结果）
        txtFilterName.Clear();
        txtFilterType.Clear();

        lblSearchProgress.Text = "查询缺失对象中...";
        lblSearchProgress.ForeColor = Color.Blue;
        dgvSearchResults.DataSource = null;
        btnFindMissing.Enabled = false;
        btnSearch.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var srcServer = txtSourceServer.Text.Trim();
                var srcDbName = txtSourceDbName.Text.Trim();
                var srcUser = txtSourceUser.Text.Trim();
                var srcPassword = txtSourcePassword.Text;

                var tgtServer = txtTargetServer.Text.Trim();
                var tgtDbName = txtTargetDbName.Text.Trim();
                var tgtUser = txtTargetUser.Text.Trim();
                var tgtPassword = txtTargetPassword.Text;

                var srcConnStr = BuildConnString(srcServer, srcDbName, srcUser, srcPassword);
                var tgtConnStr = BuildConnString(tgtServer, tgtDbName, tgtUser, tgtPassword);

                // 1. 取源库当前类型全部对象（含元数据，筛选 is_ms_shipped=0）
                var hasKeyword = !string.IsNullOrWhiteSpace(keyword);
                var srcSql = @"
SELECT o.name AS 对象名称,
       o.type_desc AS 类型描述,
       o.create_date AS 创建时间,
       o.modify_date AS 修改时间
FROM sys.objects o
WHERE o.type = @objType
  AND o.is_ms_shipped = 0" + (hasKeyword ? "  AND o.name LIKE @keyword" : "") + @"
ORDER BY o.name";

                var srcData = new DataTable();
                int srcTotal = 0;
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(srcConnStr))
                {
                    conn.Open();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(srcSql, conn);
                    cmd.Parameters.AddWithValue("@objType", objectType);
                    if (hasKeyword) cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");
                    using var adapter = new Microsoft.Data.SqlClient.SqlDataAdapter(cmd);
                    adapter.Fill(srcData);
                    srcTotal = srcData.Rows.Count;
                }

                // 2. 取目标库当前类型全部对象名（仅 name）
                var tgtSql = @"
SELECT o.name
FROM sys.objects o
WHERE o.type = @objType
  AND o.is_ms_shipped = 0";
                var tgtNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(tgtConnStr))
                {
                    conn.Open();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(tgtSql, conn);
                    cmd.Parameters.AddWithValue("@objType", objectType);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var n = reader["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(n)) tgtNames.Add(n);
                    }
                }

                // 3. 差集：源有 - 目标有
                var missingRows = srcData.AsEnumerable()
                    .Where(r => !tgtNames.Contains(r["对象名称"]?.ToString() ?? ""))
                    .ToList();

                var missingDt = missingRows.Count > 0
                    ? missingRows.CopyToDataTable()
                    : srcData.Clone();

                this.Invoke(new Action(() =>
                {
                    if (dgvSearchResults.Columns.Contains("chk"))
                    {
                        dgvSearchResults.Columns.Remove("chk");
                    }
                    if (missingRows.Count > 0)
                    {
                        // 用 DataView 包装以支持过滤
                        _originalDt = missingDt;
                        _dataView = new DataView(missingDt);
                        dgvSearchResults.DataSource = _dataView;
                    }
                    else
                    {
                        _originalDt = null;
                        _dataView = null;
                        dgvSearchResults.DataSource = null;
                    }

                    if (missingRows.Count > 0)
                    {
                        var checkCol = new DataGridViewCheckBoxColumn
                        {
                            HeaderText = "选择",
                            Width = 50,
                            Name = "chk"
                        };
                        dgvSearchResults.Columns.Insert(0, checkCol);
                        dgvSearchResults.AutoResizeColumns();
                        if (dgvSearchResults.Columns.Contains("创建时间"))
                            dgvSearchResults.Columns["创建时间"].Visible = false;
                        if (dgvSearchResults.Columns.Contains("修改时间"))
                            dgvSearchResults.Columns["修改时间"].Visible = false;

                        // 默认全选，方便一键【添加选中】到复制列表
                        foreach (DataGridViewRow row in dgvSearchResults.Rows)
                        {
                            row.Selected = true;
                            var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                            if (checkCell != null) checkCell.Value = true;
                        }
                    }

                    lblSearchProgress.Location = new Point(dgvSearchResults.Left, dgvSearchResults.Bottom + 5);
                    var missing = missingRows.Count;
                    var hint = hasKeyword ? "（已按关键字过滤）" : "";
                    lblSearchProgress.Text = $"源库共 {srcTotal} 个{typeDisplay}{hint}，缺失 {missing} 个";
                    lblSearchProgress.ForeColor = missing > 0 ? Color.FromArgb(228, 94, 29) : Color.Green;
                    // 重新应用过滤（新数据加载后过滤条件仍生效）
                    ApplyFilter();
                    // 列都设好了才能拿到真实列宽，此处同步过滤行 TextBox 位置/宽度
                    SyncFilterRowPositions();
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblSearchProgress.Text = "查询缺失对象失败";
                    lblSearchProgress.ForeColor = Color.Red;
                    MessageBox.Show($"查询缺失对象失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                this.Invoke(new Action(() =>
                {
                    btnFindMissing.Enabled = true;
                    btnSearch.Enabled = true;
                }));
            }
        });
    }

    /// <summary>
    /// 构造 SqlClient 连接串：Windows 集成身份验证 vs 用户名密码
    /// </summary>
    private string BuildConnString(string server, string dbName, string user, string password)
    {
        if (string.IsNullOrEmpty(user))
        {
            return $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        }
        var pwd = string.IsNullOrEmpty(password) ? "" : EncryptionService.Decrypt(password);
        return $"Server={server};Database={dbName};User Id={user};Password={pwd};TrustServerCertificate=True;";
    }

    /// <summary>
    /// 对象类型代码 → 中文显示名
    /// </summary>
    private string GetTypeDisplay(string objectType)
    {
        return objectType switch
        {
            "U" => "表",
            "V" => "视图",
            "TF" => "表值函数",
            "FN" => "标量值函数",
            "P" => "存储过程",
            _ => "对象"
        };
    }

    /// <summary>
    /// 应用过滤：根据 txtFilterName/txtFilterType 文本动态过滤 DataView
    /// 多列 AND 逻辑，大小写不敏感子串匹配
    /// </summary>
    private void ApplyFilter()
    {
        if (_dataView == null) return;
        var filters = new List<string>();
        var name = txtFilterName.Text.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            filters.Add($"[对象名称] LIKE '%{EscapeLike(name)}%'");
        }
        var type = txtFilterType.Text.Trim();
        if (!string.IsNullOrEmpty(type))
        {
            filters.Add($"[类型描述] LIKE '%{EscapeLike(type)}%'");
        }
        _dataView.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
    }

    /// <summary>
    /// 同步过滤行 TextBox 位置/宽度与 dgvSearchResults 列对齐
    /// chk 列不参与过滤（占位宽度）
    /// </summary>
    private void SyncFilterRowPositions()
    {
        if (dgvSearchResults.Columns.Count == 0) return;
        // pnlFilterRow 与 dgvSearchResults 同为 pnlSearch 的子控件
        // TextBox 是 pnlFilterRow 的子控件，其 X 是相对 pnlFilterRow 的
        // 对齐公式：dgvSearchResults.Left + RowHeaders + chk列宽 - pnlFilterRow.Left
        int x = dgvSearchResults.Left - pnlFilterRow.Left;
        if (dgvSearchResults.RowHeadersVisible)
        {
            x += dgvSearchResults.RowHeadersWidth;
        }
        if (dgvSearchResults.Columns.Contains("chk") && dgvSearchResults.Columns["chk"].Visible)
        {
            x += dgvSearchResults.Columns["chk"].Width;
        }
        // 垂直居中（考虑 FixedSingle 边框占 2px）
        int y = (pnlFilterRow.ClientSize.Height - txtFilterName.PreferredHeight) / 2;
        if (y < 0) y = 0;

        if (dgvSearchResults.Columns.Contains("对象名称") && dgvSearchResults.Columns["对象名称"].Visible)
        {
            txtFilterName.Visible = true;
            txtFilterName.Location = new Point(x, y);
            txtFilterName.Width = dgvSearchResults.Columns["对象名称"].Width;
            x += txtFilterName.Width;
        }
        else
        {
            txtFilterName.Visible = false;
        }
        if (dgvSearchResults.Columns.Contains("类型描述") && dgvSearchResults.Columns["类型描述"].Visible)
        {
            txtFilterType.Visible = true;
            txtFilterType.Location = new Point(x, y);
            txtFilterType.Width = dgvSearchResults.Columns["类型描述"].Width;
        }
        else
        {
            txtFilterType.Visible = false;
        }
    }

    /// <summary>
    /// 转义 DataView.RowFilter LIKE 模式中的单引号（双重转义）
    /// </summary>
    private static string EscapeLike(string s) => s.Replace("'", "''");

    /// <summary>
    /// 添加选中：把搜索结果中勾选的对象名追加到 txtObjects（去重）
    /// </summary>
    private void BtnAddSelected_Click(object? sender, EventArgs e)
    {
        if (dgvSearchResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先在搜索结果中勾选要添加的对象！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedNames = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            var name = row.Cells["对象名称"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                selectedNames.Add(name);
            }
        }

        if (selectedNames.Count == 0) return;

        var currentText = txtObjects.Text.Trim();
        var separator = string.IsNullOrEmpty(currentText) ? "" : ";";

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
        {
            currentText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                .ForEach(n => existing.Add(n.Trim()));
        }

        var newNames = selectedNames.Where(n => !existing.Contains(n)).ToList();
        if (newNames.Count == 0)
        {
            MessageBox.Show("选中的对象已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var addedText = string.Join(";", newNames);
        txtObjects.Text = currentText + separator + addedText;

        lblSearchProgress.Text = $"已添加 {newNames.Count} 个对象到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    /// <summary>
    /// 清空选项：一键清空对象名称文本框
    /// </summary>
    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        txtObjects.Clear();
        dgvSearchResults.ClearSelection();
        lblSearchProgress.Text = "已清空对象列表";
        lblSearchProgress.ForeColor = Color.Gray;
    }

    /// <summary>
    /// 对比表结构：先校验【类型=表结构】【目标库已填】【搜索区有勾选】，然后弹出对比窗体
    /// </summary>
    private void BtnCompareTables_Click(object? sender, EventArgs e)
    {
        // 1. 校验对象类型
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请先选择对象类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var objectType = ((ObjectTypeItem)cboObjectType.SelectedItem).Value;
        if (objectType != "U")
        {
            MessageBox.Show("对比表结构功能仅支持【表结构】类型，请先将对象类型切换为表结构！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            cboObjectType.Focus();
            return;
        }

        // 2. 校验源库
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text) || string.IsNullOrWhiteSpace(txtSourceDbName.Text))
        {
            MessageBox.Show("请填写源数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 3. 校验目标库
        if (string.IsNullOrWhiteSpace(txtTargetServer.Text) || string.IsNullOrWhiteSpace(txtTargetDbName.Text))
        {
            MessageBox.Show("请填写目标数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 4. 校验搜索区有勾选
        if (dgvSearchResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先在搜索结果中勾选要对比的表！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedTables = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            var name = row.Cells["对象名称"].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(name)) selectedTables.Add(name);
        }
        if (selectedTables.Count == 0)
        {
            MessageBox.Show("未获取到勾选的表名！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 5. 弹出对比窗体
        var compareForm = new CompareTablesForm(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            selectedTables);
        compareForm.ShowDialog();
    }

    private void LoadCurrentAccount()
    {
        if (_currentAccount != null)
        {
            txtSourceServer.Text = _currentAccount.Database ?? "";
            txtSourceDbName.Text = _currentAccount.DatabaseName ?? "";
            txtSourceUser.Text = _currentAccount.DbUser ?? "";
            txtSourcePassword.Text = _currentAccount.DbPassword ?? "";
        }
    }

    /// <summary>
    /// 根据主窗体工具箱 Tab 中的源/目标预选账套自动带入连接信息。
    /// 预选为空时，源库和目标库均保持空白。
    /// 带入后用户仍可在工具内自行修改或重新选择。
    /// </summary>
    private void LoadPresetAccounts()
    {
        var preset = _context.GetToolDatabasePreset();
        ApplyAccountToDatabaseFields(preset.SourceAccount, true);
        ApplyAccountToDatabaseFields(preset.TargetAccount, false);
    }

    private void ApplyAccountToDatabaseFields(Account? account, bool isSource)
    {
        if (account == null) return;

        if (isSource)
        {
            txtSourceServer.Text = account.Database ?? "";
            txtSourceDbName.Text = account.DatabaseName ?? "";
            txtSourceUser.Text = account.DbUser ?? "";
            txtSourcePassword.Text = account.DbPassword ?? "";
        }
        else
        {
            txtTargetServer.Text = account.Database ?? "";
            txtTargetDbName.Text = account.DatabaseName ?? "";
            txtTargetUser.Text = account.DbUser ?? "";
            txtTargetPassword.Text = account.DbPassword ?? "";
        }
    }

    private void SelectAccount(bool isSource)
    {
        var accounts = _context.GetAllAccounts();
        if (accounts.Count == 0)
        {
            MessageBox.Show("没有可用的账套！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = "选择账套",
            Size = new Size(600, 600),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White
        };

        var lbl = new Label { Text = "请选择账套（支持搜索）", Left = 20, Top = 15, Width = 540, Height = 25, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(lbl);

        var txtSearch = new TextBox
        {
            Left = 20,
            Top = 45,
            Width = 540,
            Height = 30,
            Font = new Font("微软雅黑", 11F),
            PlaceholderText = "输入账套编码或名称搜索..."
        };
        dialog.Controls.Add(txtSearch);

        var listBox = new ListBox { Left = 20, Top = 85, Width = 540, Height = 380, Font = new Font("微软雅黑", 11F) };
        dialog.Controls.Add(listBox);

        // 快捷键：`键定位搜索框，上/下键快速进入列表选择，ESC关闭，Enter确认
        dialog.KeyPreview = true;
        bool justFocused = false;
        dialog.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Oemtilde) { txtSearch.Focus(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { dialog.Close(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { if (listBox.SelectedIndex >= 0) btnOkClick(); e.SuppressKeyPress = true; }
            else if ((e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && !listBox.Focused && listBox.Items.Count > 0)
            {
                listBox.Focus();
                listBox.SelectedIndex = 0;
                justFocused = true;
                e.SuppressKeyPress = true;
            }
            else if (justFocused && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                justFocused = false;
                e.SuppressKeyPress = true;
            }
        };
        // 搜索框也支持`键清空内容
        txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Oemtilde) { txtSearch.SelectionStart = 0; txtSearch.SelectionLength = txtSearch.Text.Length; e.SuppressKeyPress = true; } };

        void PopulateList(string filter)
        {
            listBox.Items.Clear();
            foreach (var acc in accounts)
            {
                var item = acc.Code + " - " + acc.Name;
                bool matchCode = (acc.Code ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchName = (acc.Name ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
                bool matchPinyin = (acc.Pinyin ?? "").Contains(filter.ToLower(), StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(filter) || matchCode || matchName || matchPinyin)
                {
                    listBox.Items.Add(item);
                }
            }
        }

        PopulateList("");
        txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);

        var btnOk = new Button { Text = "确定", Left = 170, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(24, 145, 176), ForeColor = Color.White, Font = new Font("微软雅黑", 11F) };
        var btnCancelDialog = new Button { Text = "取消", Left = 310, Top = 480, Width = 120, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Gray, Font = new Font("微软雅黑", 11F) };

        void btnOkClick()
        {
            if (listBox.SelectedIndex >= 0)
            {
                var selectedText = listBox.SelectedItem?.ToString() ?? "";
                var selectedAcc = accounts.FirstOrDefault(a => (a.Code + " - " + a.Name) == selectedText);
                if (selectedAcc != null)
                {
                    if (isSource)
                    {
                        txtSourceServer.Text = selectedAcc.Database ?? "";
                        txtSourceDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtSourceUser.Text = selectedAcc.DbUser ?? "";
                        txtSourcePassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    else
                    {
                        txtTargetServer.Text = selectedAcc.Database ?? "";
                        txtTargetDbName.Text = selectedAcc.DatabaseName ?? "";
                        txtTargetUser.Text = selectedAcc.DbUser ?? "";
                        txtTargetPassword.Text = selectedAcc.DbPassword ?? "";
                    }
                    dialog.Close();
                }
            }
        }
        btnOk.Click += (s, e) => btnOkClick();
        btnCancelDialog.Click += (s, e) => dialog.Close();
        listBox.DoubleClick += (s, e) => btnOkClick();

        dialog.Controls.Add(btnOk);
        dialog.Controls.Add(btnCancelDialog);
        dialog.ShowDialog();
    }

    private async void BtnConfirm_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text))
        {
            MessageBox.Show("请填写源数据库地址！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtTargetServer.Text))
        {
            MessageBox.Show("请填写目标数据库地址！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtObjects.Text))
        {
            MessageBox.Show("请输入对象名称！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请选择对象类型！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var objectNames = txtObjects.Text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();

        var objectType = ((ObjectTypeItem)cboObjectType.SelectedItem).Value;
        var deleteIfExists = chkDeleteIfExists.Checked;

        lblProgress.Text = "正在连接源数据库...";
        progressBar.Value = 10;

        if (!await TestConnectionAsync(txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text))
        {
            MessageBox.Show("源数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在连接目标数据库...";
        progressBar.Value = 30;

        if (!await TestConnectionAsync(txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text))
        {
            MessageBox.Show("目标数据库连接失败！请检查连接信息。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblProgress.Text = "";
            progressBar.Value = 0;
            return;
        }

        lblProgress.Text = "正在复制对象...";
        progressBar.Value = 50;

        var success = await CopyObjectsAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            objectNames, objectType, deleteIfExists);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            MessageBox.Show("成功复制 " + objectNames.Count + " 个对象！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // 不自动关闭，方便继续操作
        }
        else
        {
            progressBar.Value = 0;
            lblProgress.Text = "";
        }
    }

    private async Task<bool> TestConnectionAsync(string server, string dbName, string user, string password)
    {
        return await Task.Run(() =>
        {
            try
            {
                var connStr = "Server=" + server + ";Database=" + dbName + ";User Id=" + user + ";Password=" + EncryptionService.Decrypt(password) + ";TrustServerCertificate=True;";
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                conn.Open();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("连接测试失败: " + ex.Message);
                return false;
            }
        });
    }

    private async Task<bool> CopyObjectsAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        List<string> objectNames, string objectType, bool deleteIfExists)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";Database=" + srcDbName + ";User Id=" + srcUser + ";Password=" + EncryptionService.Decrypt(srcPassword) + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";Database=" + tgtDbName + ";User Id=" + tgtUser + ";Password=" + EncryptionService.Decrypt(tgtPassword) + ";TrustServerCertificate=True;";

                using var srcConn = new Microsoft.Data.SqlClient.SqlConnection(srcConnStr);
                using var tgtConn = new Microsoft.Data.SqlClient.SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                int total = objectNames.Count;
                int current = 0;

                foreach (var objectName in objectNames)
                {
                    current++;
                    var progress = 50 + (current * 50 / total);
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progress;
                        lblProgress.Text = "正在复制：" + objectName + " (" + current + "/" + total + ")";
                    }));

                    string? script = objectType switch
                    {
                        "U" => GenerateCreateTableScript(srcConn, objectName),
                        "V" => GenerateCreateViewScript(srcConn, objectName),
                        "TF" => GenerateCreateFunctionScript(srcConn, objectName),
                        "FN" => GenerateCreateFunctionScript(srcConn, objectName),
                        "P" => GenerateCreateProcScript(srcConn, objectName),
                        _ => null
                    };

                    if (string.IsNullOrEmpty(script))
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show("无法获取 " + objectName + " 的定义（对象不存在或无权访问）。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                        continue;
                    }

                    // 删除目标已存在对象
                    if (deleteIfExists)
                    {
                        var dropScript = GetDropScript(objectName, objectType);
                        using var dropCmd = new Microsoft.Data.SqlClient.SqlCommand(dropScript, tgtConn);
                        dropCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        // 检查是否存在
                        if (ObjectExists(tgtConn, objectName, objectType))
                        {
                            Debug.WriteLine($"跳过已存在的对象: {objectName}");
                            continue;
                        }
                    }

                    // 创建目标对象
                    using var createCmd = new Microsoft.Data.SqlClient.SqlCommand(script, tgtConn);
                    createCmd.ExecuteNonQuery();

                    // 如果是表结构，同时复制触发器
                    if (objectType == "U")
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblProgress.Text = $"正在复制触发器... ({objectName})";
                        }));

                        var triggers = GetTriggersForTable(srcConn, objectName);
                        Debug.WriteLine($"表 {objectName} 有 {triggers.Count} 个触发器");

                        if (triggers.Count == 0)
                        {
                            Debug.WriteLine($"警告: 表 {objectName} 没有找到触发器（或查询失败）");
                        }

                        foreach (var triggerScript in triggers)
                        {
                            try
                            {
                                Debug.WriteLine($"完整触发器脚本:\n{triggerScript}\n=========");
                                using var triggerCmd = new Microsoft.Data.SqlClient.SqlCommand(triggerScript, tgtConn);
                                triggerCmd.ExecuteNonQuery();
                                Debug.WriteLine("触发器执行成功");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"复制触发器失败: {ex.Message}");
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("复制失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
                return false;
            }
        });
    }

    private bool ObjectExists(Microsoft.Data.SqlClient.SqlConnection conn, string objectName, string type)
    {
        var sql = "SELECT 1 FROM sys.objects WHERE name = @name AND type = @type";
        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", objectName);
        cmd.Parameters.AddWithValue("@type", type);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    private string GetDropScript(string objectName, string type)
    {
        return type switch
        {
            "U" => $"IF OBJECT_ID('{objectName}', 'U') IS NOT NULL DROP TABLE [{objectName}]",
            "V" => $"IF OBJECT_ID('{objectName}', 'V') IS NOT NULL DROP VIEW [{objectName}]",
            "TF" => $"IF OBJECT_ID('{objectName}', 'TF') IS NOT NULL DROP FUNCTION [{objectName}]",
            "FN" => $"IF OBJECT_ID('{objectName}', 'FN') IS NOT NULL DROP FUNCTION [{objectName}]",
            "P" => $"IF OBJECT_ID('{objectName}', 'P') IS NOT NULL DROP PROCEDURE [{objectName}]",
            _ => $"IF OBJECT_ID('{objectName}', 'U') IS NOT NULL DROP TABLE [{objectName}]"
        };
    }

    // ========== 表结构 ==========
    private string? GenerateCreateTableScript(Microsoft.Data.SqlClient.SqlConnection conn, string tableName)
    {
        try
        {
            var columnsSql = @"
                SELECT c.name, t.name AS data_type, c.max_length, c.precision, c.scale, c.is_nullable,
                       CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
                       dc.definition AS default_value
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN (SELECT ic.column_id, ic.object_id FROM sys.index_columns ic JOIN sys.indexes i ON ic.index_id = i.index_id AND ic.object_id = i.object_id WHERE i.is_primary_key = 1) pk
                       ON c.column_id = pk.column_id AND c.object_id = pk.object_id
                LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
                WHERE c.object_id = OBJECT_ID(@tableName)
                ORDER BY c.column_id";

            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(columnsSql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var reader = cmd.ExecuteReader();

            var columnDefs = new List<string>();
            while (reader.Read())
            {
                var colName = reader["name"]?.ToString() ?? "";
                var dataType = reader["data_type"]?.ToString() ?? "";
                var maxLen = Convert.ToInt32(reader["max_length"]);
                var precision = Convert.ToInt32(reader["precision"]);
                var scale = Convert.ToInt32(reader["scale"]);
                var isNullable = Convert.ToBoolean(reader["is_nullable"]);
                var isPk = Convert.ToInt32(reader["is_primary_key"]) == 1;
                var defaultValue = reader["default_value"]?.ToString();

                var colDef = "[" + colName + "] " + SqlDataTypeFormatter.Format(dataType, maxLen, precision, scale);
                if (!isNullable) colDef += " NOT NULL";
                else colDef += " NULL";
                if (!string.IsNullOrEmpty(defaultValue)) colDef += " DEFAULT " + defaultValue;
                if (isPk) colDef += " PRIMARY KEY";
                columnDefs.Add(colDef);
            }
            reader.Close();

            if (columnDefs.Count == 0) return null;

            return "CREATE TABLE [" + tableName + "] (" + string.Join(", ", columnDefs) + ")";
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成建表脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 触发器 ==========
    private List<string> GetTriggersForTable(Microsoft.Data.SqlClient.SqlConnection conn, string tableName)
    {
        var triggers = new List<string>();
        try
        {
            // sys.triggers 没有 schema_id，需要通过 sys.objects 获取
            var sql = @"
                SELECT 
                    t.name AS trigger_name,
                    o.type_desc AS trigger_type,
                    m.definition
                FROM sys.triggers t
                JOIN sys.sql_modules m ON t.object_id = m.object_id
                JOIN sys.objects o ON t.object_id = o.object_id
                WHERE t.parent_id = OBJECT_ID(@tableName)"
;
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var triggerName = reader["trigger_name"]?.ToString() ?? "";
                var triggerType = reader["trigger_type"]?.ToString() ?? "SQL_TRIGGER";
                var triggerDef = reader["definition"]?.ToString();

                Debug.WriteLine($"触发器名称: {triggerName}, 类型: {triggerType}");
                Debug.WriteLine($"触发器定义前100字符: {triggerDef?.Substring(0, Math.Min(100, triggerDef.Length))}");

                if (!string.IsNullOrEmpty(triggerDef))
                {
                    var trimmed = triggerDef.Trim();

                    // 检查definition是否已包含CREATE TRIGGER头，可能嵌在注释中间（如 Author/Description 注释块之后）
                    var createIdx = trimmed.ToUpperInvariant().IndexOf("CREATE TRIGGER");
                    if (createIdx >= 0)
                    {
                        // 取从CREATE TRIGGER开始的部分（忽略前面的注释块）
                        var actualScript = trimmed.Substring(createIdx).Trim();
                        triggers.Add(actualScript);
                    }
                    else
                    {
                        // 没有CREATE TRIGGER，说明definition只有AS之后的部分，需要手动构建
                        var eventSql = @"
                            SELECT te.type_desc FROM sys.trigger_events te
                            WHERE te.object_id = OBJECT_ID(@triggerName)"
;
                        using var evtCmd = new Microsoft.Data.SqlClient.SqlCommand(eventSql, conn);
                        evtCmd.Parameters.AddWithValue("@triggerName", triggerName);
                        var events = new List<string>();
                        try
                        {
                            using var evtReader = evtCmd.ExecuteReader();
                            while (evtReader.Read())
                            {
                                events.Add(evtReader["type_desc"]?.ToString() ?? "");
                            }
                            evtReader.Close();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"查触发器事件失败: {ex.Message}");
                        }

                        var eventClause = events.Count > 0
                            ? string.Join(", ", events.Select(e => e.Replace("SQL_TRIGGER_EVENT_", "").Replace("_", " ")).ToArray())
                            : "INSERT";

                        triggers.Add($"CREATE TRIGGER [{triggerName}] ON [{tableName}] FOR {eventClause} AS{trimmed}");
                    }
                }
            }
            reader.Close();
            Debug.WriteLine($"找到 {triggers.Count} 个触发器 for {tableName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取触发器失败: {ex.Message}");
        }
        return triggers;
    }

    // ========== 视图 ==========
    private string? GenerateCreateViewScript(Microsoft.Data.SqlClient.SqlConnection conn, string viewName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", viewName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成视图脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 函数（表值/标量值） ==========
    private string? GenerateCreateFunctionScript(Microsoft.Data.SqlClient.SqlConnection conn, string funcName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", funcName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成函数脚本失败: " + ex.Message);
            return null;
        }
    }

    // ========== 存储过程 ==========
    private string? GenerateCreateProcScript(Microsoft.Data.SqlClient.SqlConnection conn, string procName)
    {
        try
        {
            var sql = "SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID(@name)";
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", procName);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("生成存储过程脚本失败: " + ex.Message);
            return null;
        }
    }

    private class ObjectTypeItem
    {
        public string Value { get; set; } = "";
        public string Display { get; set; } = "";
    }
}
