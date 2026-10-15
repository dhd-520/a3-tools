using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// Excel 导入到临时表的 UI
/// 流程:
///   1. 用户选 Excel 文件 (或构造时传入)
///   2. 选 Sheet, 默认 alias = sheet 名
///   3. 自动推断每列类型 (用户可下拉改)
///   4. 可编辑映射名 (默认 = Excel 列名)
///   5. 点 [导入] → 返回 <see cref="TempTableImportRequest"/>
///   6. 调用方负责把 RawRows 解析成 ResultTable 加到 <see cref="TempTableStore"/>
///
/// v1 简化:
///   - 预览只读前 200 行 (DataGridView 性能)
///   - 真实导入仍按预览这 200 行 (后续可加大)
///   - 全空行跳过, 重复列名自动加 _N 后缀
/// </summary>
public partial class ExcelImportForm : Form
{
    private const int PreviewRowLimit = 200;

    private string _filePath = "";
    private DataTable? _previewData;

    /// <summary>用户点 [导入] 确认的结果, 调用方读这个</summary>
    public TempTableImportRequest? Result { get; private set; }

    private static readonly Regex AliasRegex =
        new(@"^[A-Za-z_][A-Za-z_0-9]*$", RegexOptions.Compiled);

    public ExcelImportForm()
    {
        InitializeComponent();
    }

    public ExcelImportForm(string filePath) : this()
    {
        LoadFile(filePath);
    }

    // ===== 文件选择 =====
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls|所有文件 (*.*)|*.*",
            Title = "选择 Excel 文件"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            LoadFile(ofd.FileName);
        }
    }

    private void LoadFile(string path)
    {
        try
        {
            _filePath = path;
            txtFile.Text = path;

            var sheets = ExcelImporter.ListSheets(path);
            cmbSheet.Items.Clear();
            foreach (var s in sheets) cmbSheet.Items.Add(s);
            if (sheets.Count == 0)
            {
                SetStatus("⚠ Excel 文件没有任何 Sheet");
                return;
            }
            cmbSheet.SelectedIndex = 0;
            SetStatus($"已加载: {path} ({sheets.Count} 个 Sheet)");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ 读取文件失败: {ex.Message}");
            MessageBox.Show(this, $"读取 Excel 失败:\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ===== Sheet 切换 → 重新读 + 推断类型 =====
    private void CmbSheet_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbSheet.SelectedItem is null) return;
        var sheetName = cmbSheet.SelectedItem.ToString() ?? "";
        if (string.IsNullOrEmpty(sheetName)) return;

        // 默认 alias = sheet 名 (用户可改)
        if (string.IsNullOrWhiteSpace(txtAlias.Text) || AliasRegex.IsMatch(txtAlias.Text))
        {
            var safeAlias = SanitizeAlias(sheetName);
            if (safeAlias.Length > 0) txtAlias.Text = safeAlias;
        }

        LoadSheetPreview(sheetName);
    }

    private static string SanitizeAlias(string name)
    {
        // sheet 名可能含中文/数字开头 → 转成 SQL 标识符
        // 规则: 非 [A-Za-z_0-9] 替换成 _, 开头必须是字母或 _
        var s = Regex.Replace(name, @"[^A-Za-z_0-9]", "_");
        if (s.Length == 0 || char.IsDigit(s[0])) s = "_" + s;
        if (s.Length > 60) s = s.Substring(0, 60);
        return s;
    }

    private void LoadSheetPreview(string sheetName)
    {
        try
        {
            _previewData = ExcelImporter.ReadSheet(_filePath, sheetName, maxRows: PreviewRowLimit);
            PopulateMappingGrid();
            PopulatePreviewGrid();
            SetStatus($"Sheet '{sheetName}': {_previewData.Rows.Count} 行 × {_previewData.Columns.Count} 列" +
                      (_previewData.Rows.Count >= PreviewRowLimit ? " (已截断)" : ""));
        }
        catch (Exception ex)
        {
            SetStatus($"❌ 读取 Sheet 失败: {ex.Message}");
            MessageBox.Show(this, $"读取 Sheet '{sheetName}' 失败:\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ===== 列映射 DataGridView =====
    private void PopulateMappingGrid()
    {
        dgvMapping.Rows.Clear();
        if (_previewData is null) return;

        var inferred = TypeInferrer.Infer(_previewData, sampleSize: Math.Min(100, _previewData.Rows.Count));
        foreach (DataColumn col in _previewData.Columns)
        {
            var type = inferred.TryGetValue(col.ColumnName, out var t) ? t : "NVARCHAR";
            var s1 = GetSample(col.ColumnName, 0);
            var s2 = GetSample(col.ColumnName, 1);
            var s3 = GetSample(col.ColumnName, 2);
            dgvMapping.Rows.Add(col.ColumnName, col.ColumnName, type, s1, s2, s3);
        }
    }

    private string GetSample(string colName, int rowIdx)
    {
        if (_previewData is null) return "";
        if (rowIdx >= _previewData.Rows.Count) return "";
        var v = _previewData.Rows[rowIdx][colName];
        if (v is null || v is DBNull) return "(空)";
        var s = v.ToString();
        if (string.IsNullOrEmpty(s)) return "(空)";
        // 长字符串截断
        if (s.Length > 30) s = s.Substring(0, 30) + "...";
        return s;
    }

    // ===== 数据预览 DataGridView =====
    private void PopulatePreviewGrid()
    {
        dgvPreview.DataSource = null;
        if (_previewData is null) return;

        // 用 TypeInferrer + ValueParser 把前 3 行按用户当前选择的类型解析展示
        // 这里只读前 3 行, 不动全表
        var preview = _previewData.Clone(); // 结构相同, 数据空
        int previewRowCount = Math.Min(3, _previewData.Rows.Count);
        for (int r = 0; r < previewRowCount; r++)
        {
            var srcRow = _previewData.Rows[r];
            var newRow = preview.NewRow();
            for (int c = 0; c < preview.Columns.Count; c++)
            {
                var raw = srcRow[c];
                if (raw is null || raw is DBNull) { newRow[c] = "(空)"; continue; }
                newRow[c] = raw.ToString() ?? "";
            }
            preview.Rows.Add(newRow);
        }
        dgvPreview.DataSource = preview;
        dgvPreview.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
    }

    // ===== 确认导入 =====
    private void BtnImport_Click(object? sender, EventArgs e)
    {
        // 1. 验证 alias
        var alias = txtAlias.Text.Trim();
        if (string.IsNullOrEmpty(alias))
        {
            MessageBox.Show(this, "请输入别名 (SQL 里用 ##别名 引用)", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtAlias.Focus();
            return;
        }
        if (!AliasRegex.IsMatch(alias))
        {
            MessageBox.Show(this,
                "别名格式不合法\n\n规则: 以字母或下划线开头, 后跟字母/数字/下划线\n\n例如: Stock, _price_2026, ItemList",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtAlias.Focus();
            return;
        }

        // 2. 验证 sheet
        if (cmbSheet.SelectedItem is null)
        {
            MessageBox.Show(this, "请选择 Sheet", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var sheetName = cmbSheet.SelectedItem.ToString() ?? "";

        // 3. 验证有数据
        if (_previewData is null || _previewData.Rows.Count == 0)
        {
            MessageBox.Show(this, "Sheet 里没有任何数据行, 无法导入", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 4. 验证列映射
        if (dgvMapping.Rows.Count == 0)
        {
            MessageBox.Show(this, "没有任何列, 无法导入", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new List<TempTableColumn>();
        for (int i = 0; i < dgvMapping.Rows.Count; i++)
        {
            var row = dgvMapping.Rows[i];
            var src = row.Cells["colExcelName"].Value?.ToString() ?? "";
            var tgt = (row.Cells["colTargetName"].Value?.ToString() ?? "").Trim();
            var type = (row.Cells["colType"].Value?.ToString() ?? "NVARCHAR").Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(tgt)) tgt = src;
            if (string.IsNullOrEmpty(src))
            {
                MessageBox.Show(this, $"第 {i + 1} 行 Excel 列名为空, 请检查文件", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!AliasRegex.IsMatch(tgt))
            {
                MessageBox.Show(this,
                    $"第 {i + 1} 行映射名 '{tgt}' 不合法\n\n规则: 字母/下划线开头, 后跟字母/数字/下划线",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!seenTargets.Add(tgt))
            {
                MessageBox.Show(this, $"映射名 '{tgt}' 重复, 请修改", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 找原列索引
            int origIdx = -1;
            for (int c = 0; c < _previewData.Columns.Count; c++)
            {
                if (_previewData.Columns[c].ColumnName == src) { origIdx = c; break; }
            }
            if (origIdx < 0)
            {
                MessageBox.Show(this, $"找不到原列 '{src}', 请刷新 Sheet", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            columns.Add(new TempTableColumn
            {
                SourceName = src,
                TargetName = tgt,
                TypeName = type,
                OriginalIndex = origIdx
            });
        }

        // 5. 收集 RawRows (按用户选定的列顺序)
        var rawRows = new List<object?[]>(_previewData.Rows.Count);
        foreach (DataRow srcRow in _previewData.Rows)
        {
            var arr = new object?[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                var v = srcRow[columns[i].OriginalIndex];
                arr[i] = (v is null || v is DBNull) ? null : v.ToString();
            }
            rawRows.Add(arr);
        }

        Result = new TempTableImportRequest
        {
            Alias = alias,
            SourceFile = _filePath,
            SheetName = sheetName,
            Columns = columns,
            RawRows = rawRows
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void SetStatus(string text)
    {
        lblStatus.Text = text;
    }
}
