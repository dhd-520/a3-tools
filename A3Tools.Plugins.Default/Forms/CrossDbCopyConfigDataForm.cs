using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Plugins;
using A3Tools.Services;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 跨库复制配置数据工具
/// 仿照 CrossDbCopyTableForm 的控件结构，但复制的是数据行（DML）而不是数据库对象（DDL）
/// 支持三种数据类型：标准查询（S_DATASELECT）/ 系统参数（S_SYSTEMSETTING）/ 自定义数据源（S_CUSTOMDATA）
/// </summary>
public partial class CrossDbCopyConfigDataForm : Form
{
    private readonly IToolContext _context;
    private readonly Account? _currentAccount;

    /// <summary>
    /// 复合主键分隔符：用于在内存中拼接/拆分多列主键
    /// </summary>
    private const string KEY_SEPARATOR = "§§§";

    /// <summary>
    /// 配置数据类型：定义每种数据类型的搜索/复制行为
    /// </summary>
    private class ConfigDataType
    {
        public string Value { get; set; } = "";                          // 内部唯一标识
        public string Display { get; set; } = "";                        // 下拉显示名
        public string TableName { get; set; } = "";                      // 实际操作的表
        public string[] KeyColumns { get; set; } = Array.Empty<string>(); // 主键列（1 或 2 个）
        public string SearchSql { get; set; } = "";                      // 搜索用的 SELECT SQL
        public string[] WhereColumns { get; set; } = Array.Empty<string>(); // 搜索 WHERE 涉及的列
    }

    /// <summary>
    /// 三种数据类型配置
    /// 注意：标准查询和自定义数据源对应不同的表（S_DATASELECT vs S_CUSTOMDATA），字段集也不一样
    /// </summary>
    private static readonly Dictionary<string, ConfigDataType> ConfigDataTypeMap = new()
    {
        { "DataSearch", new ConfigDataType {
            Value = "DataSearch",
            Display = "标准查询",
            TableName = "S_DATASELECT",
            KeyColumns = new[] { "CODE" },
            SearchSql = "SELECT CODE,NAME,DESCRIPTION FROM S_DATASELECT",
            WhereColumns = new[] { "CODE", "NAME", "DESCRIPTION" }
        }},
        { "SystemSetting", new ConfigDataType {
            Value = "SystemSetting",
            Display = "系统参数",
            TableName = "S_SYSTEMSETTING",
            KeyColumns = new[] { "GROUPSETNAME", "SETNAME" },
            SearchSql = "SELECT GROUPSETNAME,SETNAME,CAPTION FROM S_SYSTEMSETTING",
            WhereColumns = new[] { "GROUPSETNAME", "SETNAME", "CAPTION" }
        }},
        { "CustomData", new ConfigDataType {
            Value = "CustomData",
            Display = "自定义数据源",
            TableName = "S_CUSTOMDATA",
            KeyColumns = new[] { "CODE" },
            SearchSql = "SELECT CODE,NAME,DESCRIPTION FROM S_CUSTOMDATA",
            WhereColumns = new[] { "CODE", "NAME", "DESCRIPTION" }
        }},
    };

    // 搜索区过滤行：存原始 DataTable + 可过滤的 DataView
    private DataTable? _originalDt;
    private DataView? _dataView;

    public CrossDbCopyConfigDataForm(IToolContext context, Account? currentAccount)
    {
        _context = context;
        _currentAccount = currentAccount;
        InitializeComponent();
        InitConfigTypeCombo();
        LoadCurrentAccount();

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
        txtFilterCol1.TextChanged += (s, e) => ApplyFilter();
        txtFilterCol2.TextChanged += (s, e) => ApplyFilter();

        // 列宽变化时同步过滤行 TextBox 位置/宽度
        dgvSearchResults.ColumnWidthChanged += (s, e) => SyncFilterRowPositions();
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

    private void InitConfigTypeCombo()
    {
        cboObjectType.Items.Clear();
        foreach (var kv in ConfigDataTypeMap)
        {
            cboObjectType.Items.Add(kv.Value);
        }
        cboObjectType.DisplayMember = "Display";
        cboObjectType.ValueMember = "Value";
        cboObjectType.SelectedIndex = 0;
    }

    /// <summary>
    /// 数据类型切换时更新过滤行占位文字 + 标签
    /// </summary>
    private void CboObjectType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cboObjectType.SelectedItem is ConfigDataType type)
        {
            if (type.WhereColumns.Length >= 1)
                txtFilterCol1.PlaceholderText = $"过滤 {type.WhereColumns[0]}";
            if (type.WhereColumns.Length >= 2)
                txtFilterCol2.PlaceholderText = $"过滤 {type.WhereColumns[1]}";

            // 标签也提示一下数据类型对应的判断依据
            lblKeys.Text = $"{type.Display}判断依据：";
            lblKeysHint.Text = $"提示：多个数据用分号(;)分隔。" +
                (type.KeyColumns.Length == 1
                    ? $"【{type.Display}】以 [{type.KeyColumns[0]}] 为唯一判断依据，源库存在但目标库不存在的数据可点击【缺失对象】自动列出。"
                    : $"【{type.Display}】以 [{string.Join("+", type.KeyColumns)}] 组合为唯一判断依据，源库存在但目标库不存在的数据可点击【缺失对象】自动列出（多个值用 [{KEY_SEPARATOR}] 连接）。");
        }
    }

    private void BtnSelectSource_Click(object? sender, EventArgs e) => SelectAccount(true);
    private void BtnSelectTarget_Click(object? sender, EventArgs e) => SelectAccount(false);
    private void BtnCancel_Click(object? sender, EventArgs e) => this.Close();

    /// <summary>
    /// 搜索按钮：根据当前选中的数据类型 + 关键字查询源库
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
            MessageBox.Show("请先选择数据类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var keyword = txtSearchKeyword.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show("请输入搜索关键字！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSearchKeyword.Focus();
            return;
        }

        var type = (ConfigDataType)cboObjectType.SelectedItem;

        // 点击查询时清空过滤框
        txtFilterCol1.Clear();
        txtFilterCol2.Clear();

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
                    : $"Server={server};Database={dbName};User Id={user};Password={EncryptionService.Decrypt(password)};TrustServerCertificate=True;";

                // 构造 WHERE 子句（OR 多列模糊匹配）
                var whereConditions = type.WhereColumns.Select(c => $"[{c}] LIKE @kw");
                var whereClause = "(" + string.Join(" OR ", whereConditions) + ")";
                var orderBy = type.KeyColumns[0];
                var sql = $"{type.SearchSql} WHERE {whereClause} ORDER BY [{orderBy}]";

                using var conn = new SqlConnection(connString);
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@kw", "%" + keyword + "%");
                using var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                this.Invoke(new Action(() =>
                {
                    if (dgvSearchResults.Columns.Contains("chk"))
                    {
                        dgvSearchResults.Columns.Remove("chk");
                    }
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
                    ApplyFilter();
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
                this.Invoke(new Action(() => btnSearch.Enabled = true));
            }
        });
    }

    /// <summary>
    /// 缺失对象：根据当前数据类型的判断依据，分别从源库和目标库取主键集合，求差集（源有目标无）展示在下方，默认全选
    /// </summary>
    private void BtnFindMissing_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSourceServer.Text) || string.IsNullOrWhiteSpace(txtSourceDbName.Text))
        {
            MessageBox.Show("请填写源数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtTargetServer.Text) || string.IsNullOrWhiteSpace(txtTargetDbName.Text))
        {
            MessageBox.Show("请填写目标数据库连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboObjectType.SelectedItem == null)
        {
            MessageBox.Show("请先选择数据类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var type = (ConfigDataType)cboObjectType.SelectedItem;
        var keyword = txtSearchKeyword.Text.Trim();

        txtFilterCol1.Clear();
        txtFilterCol2.Clear();

        lblSearchProgress.Text = "查询缺失数据中...";
        lblSearchProgress.ForeColor = Color.Blue;
        dgvSearchResults.DataSource = null;
        btnFindMissing.Enabled = false;
        btnSearch.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var srcConnStr = BuildConnString(txtSourceServer.Text.Trim(), txtSourceDbName.Text.Trim(), txtSourceUser.Text.Trim(), txtSourcePassword.Text);
                var tgtConnStr = BuildConnString(txtTargetServer.Text.Trim(), txtTargetDbName.Text.Trim(), txtTargetUser.Text.Trim(), txtTargetPassword.Text);

                // 1. 源库: 取所有数据的展示字段（默认全量；有关键字时按关键字过滤）
                var hasKeyword = !string.IsNullOrWhiteSpace(keyword);
                var whereConditions = type.WhereColumns.Select(c => $"[{c}] LIKE @kw");
                var whereClause = hasKeyword
                    ? "(" + string.Join(" OR ", whereConditions) + ")"
                    : "1=1";
                var selectCols = string.Join(",", type.WhereColumns.Select(c => $"[{c}]"));
                var srcSql = $"{type.SearchSql} WHERE {whereClause} ORDER BY [{type.KeyColumns[0]}]";

                var srcDt = new DataTable();
                int srcTotal;
                using (var conn = new SqlConnection(srcConnStr))
                {
                    conn.Open();
                    using var cmd = new SqlCommand(srcSql, conn);
                    if (hasKeyword) cmd.Parameters.AddWithValue("@kw", "%" + keyword + "%");
                    using var adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(srcDt);
                    srcTotal = srcDt.Rows.Count;
                }

                // 2. 目标库: 取所有主键集合（拼接为单字符串便于 HashSet 比对）
                // 双主键时用 '+KEY_SEPARATOR+' 连接成一列
                var keyCols = type.KeyColumns;
                string tgtKeySelect;
                if (keyCols.Length == 1)
                {
                    tgtKeySelect = $"[{keyCols[0]}]";
                }
                else
                {
                    tgtKeySelect = string.Join($"+'{KEY_SEPARATOR}'+", keyCols.Select(c => $"ISNULL([{c}], '')") + $" AS [{KEY_SEPARATOR}]");
                    // 上面会带 AS 别名，但要兼容 2 列/3 列... 改为：
                    tgtKeySelect = string.Join($"+'{KEY_SEPARATOR}'+", keyCols.Select(c => $"ISNULL([{c}], ''"));
                    // 实际拼出来形如：ISNULL([GROUPSETNAME], '')+'§§§'+ISNULL([SETNAME], ''  ← 缺一个 )
                    // 重写（避免眼花）
                }
                // 用 String.Format 重新拼接：每个列包 ISNULL，最后再整体拼接
                var concatCols = string.Join($"+'{KEY_SEPARATOR}'+", keyCols.Select(c => $"ISNULL([{c}], '')"));
                tgtKeySelect = $"{concatCols} AS [{KEY_SEPARATOR}]";
                var tgtSql = $"SELECT {tgtKeySelect} FROM {type.TableName}";

                var tgtKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var conn = new SqlConnection(tgtConnStr))
                {
                    conn.Open();
                    using var cmd = new SqlCommand(tgtSql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var k = reader[KEY_SEPARATOR]?.ToString();
                        if (!string.IsNullOrEmpty(k)) tgtKeys.Add(k);
                    }
                }

                // 3. 差集：源库行的拼接主键不在目标库集合里
                var missingRows = srcDt.AsEnumerable()
                    .Where(r =>
                    {
                        var key = keyCols.Length == 1
                            ? r[keyCols[0]]?.ToString() ?? ""
                            : string.Join(KEY_SEPARATOR, keyCols.Select(c => r[c]?.ToString() ?? ""));
                        return !tgtKeys.Contains(key);
                    })
                    .ToList();

                this.Invoke(new Action(() =>
                {
                    if (dgvSearchResults.Columns.Contains("chk"))
                    {
                        dgvSearchResults.Columns.Remove("chk");
                    }
                    if (missingRows.Count > 0)
                    {
                        _originalDt = missingRows.CopyToDataTable();
                        _dataView = new DataView(_originalDt);
                        dgvSearchResults.DataSource = _dataView;
                        var checkCol = new DataGridViewCheckBoxColumn
                        {
                            HeaderText = "选择",
                            Width = 50,
                            Name = "chk"
                        };
                        dgvSearchResults.Columns.Insert(0, checkCol);
                        dgvSearchResults.AutoResizeColumns();

                        // 默认全选，方便一键【添加选中】到复制列表
                        foreach (DataGridViewRow row in dgvSearchResults.Rows)
                        {
                            row.Selected = true;
                            var checkCell = row.Cells["chk"] as DataGridViewCheckBoxCell;
                            if (checkCell != null) checkCell.Value = true;
                        }
                    }
                    else
                    {
                        _originalDt = null;
                        _dataView = null;
                        dgvSearchResults.DataSource = null;
                    }

                    lblSearchProgress.Location = new Point(dgvSearchResults.Left, dgvSearchResults.Bottom + 5);
                    var missing = missingRows.Count;
                    var hint = hasKeyword ? "（已按关键字过滤）" : "";
                    lblSearchProgress.Text = $"源库共 {srcTotal} 条{type.Display}{hint}，缺失 {missing} 条";
                    lblSearchProgress.ForeColor = missing > 0 ? Color.FromArgb(228, 94, 29) : Color.Green;
                    ApplyFilter();
                    SyncFilterRowPositions();
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    lblSearchProgress.Text = "查询缺失数据失败";
                    lblSearchProgress.ForeColor = Color.Red;
                    MessageBox.Show($"查询缺失数据失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    /// 应用过滤：根据 txtFilterCol1/txtFilterCol2 文本动态过滤 DataView
    /// 多列 OR 逻辑（任一列命中即显示），大小写不敏感子串匹配
    /// </summary>
    private void ApplyFilter()
    {
        if (_dataView == null) return;
        var filters = new List<string>();
        var k1 = txtFilterCol1.Text.Trim();
        var k2 = txtFilterCol2.Text.Trim();

        if (_originalDt != null)
        {
            // 第一个过滤框：对所有列做 OR
            if (!string.IsNullOrEmpty(k1))
            {
                var colFilters = _originalDt.Columns.Cast<DataColumn>()
                    .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{EscapeLike(k1)}%'");
                filters.Add("(" + string.Join(" OR ", colFilters) + ")");
            }
            // 第二个过滤框：对所有列做 OR（与第一个 AND）
            if (!string.IsNullOrEmpty(k2))
            {
                var colFilters = _originalDt.Columns.Cast<DataColumn>()
                    .Select(c => $"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{EscapeLike(k2)}%'");
                filters.Add("(" + string.Join(" OR ", colFilters) + ")");
            }
        }
        _dataView.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
    }

    /// <summary>
    /// 同步过滤行 TextBox 位置/宽度与 dgvSearchResults 列对齐
    /// chk 列不参与过滤（占位宽度）；两个 TextBox 对应第 1/2 显示列
    /// </summary>
    private void SyncFilterRowPositions()
    {
        if (dgvSearchResults.Columns.Count == 0) return;
        int x = dgvSearchResults.Left - pnlFilterRow.Left;
        if (dgvSearchResults.RowHeadersVisible)
        {
            x += dgvSearchResults.RowHeadersWidth;
        }
        if (dgvSearchResults.Columns.Contains("chk") && dgvSearchResults.Columns["chk"].Visible)
        {
            x += dgvSearchResults.Columns["chk"].Width;
        }
        int y = (pnlFilterRow.ClientSize.Height - txtFilterCol1.PreferredHeight) / 2;
        if (y < 0) y = 0;

        // 第一个过滤框：定位到第 1 个数据列
        if (cboObjectType.SelectedItem is ConfigDataType type && type.WhereColumns.Length >= 1)
        {
            var col1 = type.WhereColumns[0];
            if (dgvSearchResults.Columns.Contains(col1) && dgvSearchResults.Columns[col1].Visible)
            {
                txtFilterCol1.Visible = true;
                txtFilterCol1.Location = new Point(x, y);
                txtFilterCol1.Width = dgvSearchResults.Columns[col1].Width;
                x += txtFilterCol1.Width;
            }
            else
            {
                txtFilterCol1.Visible = false;
            }
        }
        else
        {
            txtFilterCol1.Visible = false;
        }

        // 第二个过滤框：定位到第 2 个数据列
        if (cboObjectType.SelectedItem is ConfigDataType type2 && type2.WhereColumns.Length >= 2)
        {
            var col2 = type2.WhereColumns[1];
            if (dgvSearchResults.Columns.Contains(col2) && dgvSearchResults.Columns[col2].Visible)
            {
                txtFilterCol2.Visible = true;
                txtFilterCol2.Location = new Point(x, y);
                txtFilterCol2.Width = dgvSearchResults.Columns[col2].Width;
            }
            else
            {
                txtFilterCol2.Visible = false;
            }
        }
        else
        {
            txtFilterCol2.Visible = false;
        }
    }

    /// <summary>
    /// 转义 DataView.RowFilter LIKE 模式中的单引号（双重转义）
    /// </summary>
    private static string EscapeLike(string s) => s.Replace("'", "''");

    /// <summary>
    /// 添加选中：把搜索结果中勾选的数据主键追加到 txtKeys（去重）
    /// 单主键：直接拼接；多主键：用 KEY_SEPARATOR 连接
    /// </summary>
    private void BtnAddSelected_Click(object? sender, EventArgs e)
    {
        if (dgvSearchResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("请先在搜索结果中勾选要添加的数据！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (cboObjectType.SelectedItem is not ConfigDataType type) return;

        var selectedKeys = new List<string>();
        foreach (DataGridViewRow row in dgvSearchResults.SelectedRows)
        {
            if (type.KeyColumns.Length == 1)
            {
                var k = row.Cells[type.KeyColumns[0]].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(k)) selectedKeys.Add(k);
            }
            else
            {
                var parts = type.KeyColumns.Select(c => row.Cells[c].Value?.ToString() ?? "").ToArray();
                if (parts.Any(p => !string.IsNullOrWhiteSpace(p)))
                    selectedKeys.Add(string.Join(KEY_SEPARATOR, parts));
            }
        }
        if (selectedKeys.Count == 0) return;

        var currentText = txtKeys.Text.Trim();
        var separator = string.IsNullOrEmpty(currentText) ? "" : ";";

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(currentText))
        {
            currentText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                .ForEach(n => existing.Add(n.Trim()));
        }

        var newKeys = selectedKeys.Where(n => !existing.Contains(n)).ToList();
        if (newKeys.Count == 0)
        {
            MessageBox.Show("选中的数据已全部添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var addedText = string.Join(";", newKeys);
        txtKeys.Text = currentText + separator + addedText;

        lblSearchProgress.Text = $"已添加 {newKeys.Count} 条数据到列表";
        lblSearchProgress.ForeColor = Color.Green;
    }

    /// <summary>
    /// 清空选项：一键清空数据键名文本框
    /// </summary>
    private void BtnClearSelected_Click(object? sender, EventArgs e)
    {
        txtKeys.Clear();
        dgvSearchResults.ClearSelection();
        lblSearchProgress.Text = "已清空数据列表";
        lblSearchProgress.ForeColor = Color.Gray;
    }

    /// <summary>
    /// 加载当前账套作为源库
    /// </summary>
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
    /// 账套选择对话框（与 CrossDbCopyTableForm 一致）
    /// </summary>
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

    /// <summary>
    /// 确认复制主流程
    /// </summary>
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
        if (string.IsNullOrWhiteSpace(txtKeys.Text))
        {
            MessageBox.Show("请填写数据键名（或点击【缺失对象】自动填入）！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cboObjectType.SelectedItem is not ConfigDataType type)
        {
            MessageBox.Show("请选择数据类型！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 解析键名列表（双主键时用 KEY_SEPARATOR 拆分）
        var rawKeys = txtKeys.Text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

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

        lblProgress.Text = "正在复制数据...";
        progressBar.Value = 50;

        var success = await CopyConfigDataAsync(
            txtSourceServer.Text, txtSourceDbName.Text, txtSourceUser.Text, txtSourcePassword.Text,
            txtTargetServer.Text, txtTargetDbName.Text, txtTargetUser.Text, txtTargetPassword.Text,
            type, rawKeys, deleteIfExists);

        if (success)
        {
            progressBar.Value = 100;
            lblProgress.Text = "复制完成";
            // 成功/部分成功的弹窗在 CopyConfigDataAsync 内部处理
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
                using var conn = new SqlConnection(connStr);
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

    /// <summary>
    /// 复制配置数据主逻辑：参照 Win 表单复制 S_CONTROL/S_DATA 的方式，
    /// 使用 TableCopyService.GetTableColumns + SqlBulkCopy 复制整行数据。
    /// 目标库有的列才复制，避免源/目标库表结构字段差异导致失败。
    /// </summary>
    private async Task<bool> CopyConfigDataAsync(
        string srcServer, string srcDbName, string srcUser, string srcPassword,
        string tgtServer, string tgtDbName, string tgtUser, string tgtPassword,
        ConfigDataType type, List<string> rawKeys, bool deleteIfExists)
    {
        return await Task.Run(() =>
        {
            try
            {
                var srcConnStr = "Server=" + srcServer + ";Database=" + srcDbName + ";User Id=" + srcUser + ";Password=" + EncryptionService.Decrypt(srcPassword) + ";TrustServerCertificate=True;";
                var tgtConnStr = "Server=" + tgtServer + ";Database=" + tgtDbName + ";User Id=" + tgtUser + ";Password=" + EncryptionService.Decrypt(tgtPassword) + ";TrustServerCertificate=True;";

                using var srcConn = new SqlConnection(srcConnStr);
                using var tgtConn = new SqlConnection(tgtConnStr);
                srcConn.Open();
                tgtConn.Open();

                int total = rawKeys.Count;
                int current = 0;
                int copiedCount = 0;
                int skippedCount = 0;
                int notFoundCount = 0;
                var failKeys = new List<string>();

                foreach (var rawKey in rawKeys)
                {
                    current++;
                    var progress = 50 + (current * 50 / Math.Max(total, 1));
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = progress;
                        lblProgress.Text = $"正在复制：{rawKey} ({current}/{total})";
                    }));

                    var keyValues = rawKey.Split(new[] { KEY_SEPARATOR }, StringSplitOptions.None);
                    if (keyValues.Length != type.KeyColumns.Length)
                    {
                        failKeys.Add($"{rawKey}(主键字段数不匹配，期望 {type.KeyColumns.Length} 个)");
                        continue;
                    }

                    try
                    {
                        var result = CopyTableDataByKeys(srcConn, tgtConn, type.TableName, type.KeyColumns, keyValues, deleteIfExists, $"[{type.Display}]");
                        if (result == CopyResult.Copied) copiedCount++;
                        else if (result == CopyResult.Skipped) skippedCount++;
                        else if (result == CopyResult.NotFound) notFoundCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"复制 {rawKey} 失败: {ex.Message}");
                        failKeys.Add($"{rawKey}({ex.Message})");
                    }
                }

                this.Invoke(new Action(() =>
                {
                    if (failKeys.Count > 0 || notFoundCount > 0)
                    {
                        var failPreview = string.Join("\n", failKeys.Take(20));
                        if (failKeys.Count > 20) failPreview += $"\n... 还有 {failKeys.Count - 20} 条";
                        MessageBox.Show(
                            $"复制完成！\n成功 {copiedCount} 条，跳过 {skippedCount} 条，源库未找到 {notFoundCount} 条，失败 {failKeys.Count} 条" +
                            (failKeys.Count > 0 ? $"\n\n失败明细：\n{failPreview}" : ""),
                            "部分成功", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"复制完成！\n成功 {copiedCount} 条" + (skippedCount > 0 ? $"，跳过 {skippedCount} 条" : ""),
                            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
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

    private enum CopyResult
    {
        Copied,
        Skipped,
        NotFound
    }

    /// <summary>
    /// 按一个或多个主键复制表数据。
    /// 实现方式与 TableCopyService.CopyTableData 一致：
    /// 目标库列名 -> 源库 SELECT -> SqlBulkCopy 写入。
    /// 单主键/复合主键统一支持。
    /// </summary>
    private static CopyResult CopyTableDataByKeys(
        SqlConnection srcConn,
        SqlConnection tgtConn,
        string tableName,
        string[] keyColumns,
        string[] keyValues,
        bool deleteFirst,
        string tag = "")
    {
        var columns = TableCopyService.GetTableColumns(tgtConn, tableName);
        if (columns.Count == 0)
            throw new Exception($"目标表 {tableName} 不存在或没有列");

        var whereClause = string.Join(" AND ", keyColumns.Select((c, i) => $"[{c}] = @key{i}"));

        if (deleteFirst)
        {
            using var delCmd = new SqlCommand($"DELETE FROM dbo.[{tableName}] WHERE {whereClause}", tgtConn);
            for (int i = 0; i < keyColumns.Length; i++)
                delCmd.Parameters.AddWithValue($"@key{i}", keyValues[i]);
            delCmd.ExecuteNonQuery();
        }
        else
        {
            using var chkCmd = new SqlCommand($"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE {whereClause}", tgtConn);
            for (int i = 0; i < keyColumns.Length; i++)
                chkCmd.Parameters.AddWithValue($"@key{i}", keyValues[i]);
            if (Convert.ToInt32(chkCmd.ExecuteScalar()) > 0)
            {
                Debug.WriteLine($"{tag}表{tableName}中{string.Join(",", keyColumns)}={string.Join(",", keyValues)}已存在，跳过");
                return CopyResult.Skipped;
            }
        }

        var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
        var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE {whereClause}";
        using var selCmd = new SqlCommand(selSql, srcConn);
        for (int i = 0; i < keyColumns.Length; i++)
            selCmd.Parameters.AddWithValue($"@key{i}", keyValues[i]);

        var dt = new DataTable();
        using (var adapter = new SqlDataAdapter(selCmd))
        {
            adapter.Fill(dt);
        }
        if (dt.Rows.Count == 0)
            return CopyResult.NotFound;

        using var bulk = new SqlBulkCopy(tgtConn);
        bulk.DestinationTableName = $"dbo.[{tableName}]";
        foreach (var col in columns)
            bulk.ColumnMappings.Add(col, col);
        bulk.WriteToServer(dt);

        return CopyResult.Copied;
    }
}

