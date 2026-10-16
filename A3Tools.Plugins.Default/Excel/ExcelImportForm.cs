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
/// v2.4 (2026-10-15) 修 200 行 bug:
///   - 导入前用 ReadSheet(maxRows: 0) 重读全量, 避免预览 200 行被当成导入数据
///
/// v2.5 (2026-10-16 陛下反馈修复):
///   - 每个 Sheet 独立保存编辑状态 (alias + 列映射 + 类型 + 导入勾选)
///     切换 Sheet 时自动保存 / 恢复, 不再丢失用户编辑
///   - 预览仍限 200 行不卡 UI, 导入读全量 (缓存 _fullData, 不再重读)
///   - 状态栏: "全部 N 行 × M 列  (预览前 200 行, 导入读全部)" 让用户安心
/// </summary>
public partial class ExcelImportForm : Form
{
    private const int PreviewRowLimit = 200;

    private string _filePath = "";
    private DataTable? _previewData;       // 预览用, 200 行限制 (UI 性能)
    private DataTable? _fullData;          // ★ v2.5 导入用, 全量, 缓存避免重读

    /// <summary>★ v2.5 当前显示的 sheet 名, 切换时用于保存/恢复状态</summary>
    private string _currentSheet = "";

    /// <summary>★ v2.5 每个 sheet 独立保存用户编辑 (alias + 列映射), 切换 Sheet 不再丢失</summary>
    private readonly Dictionary<string, SheetState> _sheetStates = new(StringComparer.OrdinalIgnoreCase);

    private class ColumnMappingState
    {
        public bool Import = true;
        public string SourceName = "";
        public string TargetName = "";
        public string Type = "NVARCHAR";
    }

    private class SheetState
    {
        public string Alias = "";
        public List<ColumnMappingState> Columns = new();
    }

    /// <summary>用户点 [导入] 确认的结果, 调用方读这个</summary>
    public TempTableImportRequest? Result { get; private set; }

    /// <summary>
    /// ★ 2026-10-15 v2.4: [全部 Sheet 导入] 的批量结果 (多 sheet, 每 sheet 一个 #temp 表)
    /// 单 sheet 精细导入仍走 <see cref="Result"/>
    /// </summary>
    public List<TempTableImportRequest>? Results { get; private set; }

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

            // ★ v2.5 换文件时清空旧 sheet 状态 (不同文件 sheet 名可能撞, 状态会乱)
            _sheetStates.Clear();
            _currentSheet = "";
            _fullData = null;
            _previewData = null;

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

        // ★ v2.5 陛下反馈修复: 切换 Sheet 时丢失用户编辑 (临时表名 / 字段映射)
        //   之前直接 txtAlias.Text = safeAlias 覆盖用户编辑, PopulateMappingGrid 重置列映射
        //   现在按 sheet 名缓存, 切换时 save → switch → restore
        // 1. 先把当前 sheet 的 UI 状态存到 _sheetStates
        SaveCurrentSheetState();
        // 2. 切到新 sheet
        _currentSheet = sheetName;
        // 3. 加载新 sheet 的预览 + 全量
        LoadSheetPreview(sheetName);
        // 4. 恢复新 sheet 的状态 (如有), 否则用默认 alias
        if (_sheetStates.ContainsKey(sheetName))
        {
            TryRestoreSheetState(sheetName);
        }
        else
        {
            // 首次进入, 默认 alias = sheet 名
            var safeAlias = SanitizeAlias(sheetName);
            if (safeAlias.Length > 0) txtAlias.Text = safeAlias;
        }
    }

    // ★ v2.5 把当前 sheet 的 UI 状态 (alias + 列映射) 存到 _sheetStates
    private void SaveCurrentSheetState()
    {
        if (string.IsNullOrEmpty(_currentSheet)) return;
        var state = new SheetState
        {
            Alias = txtAlias.Text.Trim()
        };
        foreach (DataGridViewRow row in dgvMapping.Rows)
        {
            var src = row.Cells["colExcelName"].Value?.ToString() ?? "";
            var tgt = (row.Cells["colTargetName"].Value?.ToString() ?? "").Trim();
            var type = (row.Cells["colType"].Value?.ToString() ?? "NVARCHAR").Trim().ToUpperInvariant();
            var import = row.Cells["colImport"].Value is bool b && b;
            state.Columns.Add(new ColumnMappingState
            {
                Import = import,
                SourceName = src,
                TargetName = tgt,
                Type = type
            });
        }
        _sheetStates[_currentSheet] = state;
    }

    // ★ v2.5 把 _sheetStates[sheetName] 的状态恢复到 UI (按 SourceName 匹配列)
    private void TryRestoreSheetState(string sheetName)
    {
        if (!_sheetStates.TryGetValue(sheetName, out var state)) return;

        // 恢复 alias (空也恢复 = 清空, 让用户看到上次清空的状态)
        txtAlias.Text = state.Alias;

        // 恢复每列映射 (按 Excel 原列名匹配 → 用户编辑后的值回来)
        foreach (DataGridViewRow row in dgvMapping.Rows)
        {
            var src = row.Cells["colExcelName"].Value?.ToString() ?? "";
            var saved = state.Columns.FirstOrDefault(c =>
                string.Equals(c.SourceName, src, StringComparison.OrdinalIgnoreCase));
            if (saved is not null)
            {
                row.Cells["colImport"].Value = saved.Import;
                row.Cells["colTargetName"].Value = saved.TargetName;
                row.Cells["colType"].Value = saved.Type;
            }
        }
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
        // ★ v2.5 失败时清掉旧引用, 避免拿到陈旧数据
        _previewData = null;
        _fullData = null;
        try
        {
            // ★ v2.5 陛下反馈: 之前状态栏 "200行X4列(已截断)" 让用户以为导入也被限制
            //   现在: 预览限 200 行 (UI 性能), 导入读全量 (缓存到 _fullData, 不重读)
            _previewData = ExcelImporter.ReadSheet(_filePath, sheetName, maxRows: PreviewRowLimit);
            _fullData = ExcelImporter.ReadSheet(_filePath, sheetName, maxRows: 0);

            PopulateMappingGrid();
            PopulatePreviewGrid();

            var totalRows = _fullData.Rows.Count;
            var previewRows = _previewData.Rows.Count;
            var totalCols = _fullData.Columns.Count;
            var msg = $"Sheet '{sheetName}': 全部 {totalRows} 行 × {totalCols} 列";
            if (previewRows < totalRows)
            {
                msg += $"  (预览前 {previewRows} 行, 导入读全部)";
            }
            SetStatus(msg);
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
            dgvMapping.Rows.Add(true, col.ColumnName, col.ColumnName, type, s1, s2, s3);
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

        // 3. 验证有数据 (用 _fullData, 全量, 跟实际导入的数据源一致)
        if (_fullData is null || _fullData.Rows.Count == 0)
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
            // ★ 2026-10-15 v2.1 列映射"忽略"功能: 未勾选 "导入" 复选框的列跳过
            //   例: Excel 有 30 列但只需要 5 列 → 取消勾选 25 列, 只导入 5 列
            var isImport = row.Cells["colImport"].Value;
            bool importFlag = isImport is bool b && b;
            if (!importFlag) continue;

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
            // 找原列索引 (用 _fullData 的列, 跟 rawRows 数据源一致)
            int origIdx = -1;
            for (int c = 0; c < _fullData.Columns.Count; c++)
            {
                if (_fullData.Columns[c].ColumnName == src) { origIdx = c; break; }
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

        // ★ 2026-10-15 v2.1 校验: 至少勾选 1 列才能导入
        if (columns.Count == 0)
        {
            MessageBox.Show(this,
                "没有任何列被勾选导入。\n\n请至少勾选 1 列 (\"导入\" 复选框), 或点 [全选] 按钮。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 5. 收集 RawRows (按用户选定的列顺序, 跳过未勾选的列)
        // ★ v2.5 缓存: 直接用 _fullData (LoadSheetPreview 已读全量, 不重读), 跟 BtnImportAllSheets 走的 BuildAutoRequest 也对得上
        var fullData = _fullData!; // 上面已 null 检查
        var rawRows = new List<object?[]>(fullData.Rows.Count);
        foreach (DataRow srcRow in fullData.Rows)
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

    // ===== 全选 / 全不选 =====
    private void BtnSelectAll_Click(object? sender, EventArgs e)
    {
        foreach (DataGridViewRow row in dgvMapping.Rows)
        {
            row.Cells["colImport"].Value = true;
        }
    }

    private void BtnUnselectAll_Click(object? sender, EventArgs e)
    {
        foreach (DataGridViewRow row in dgvMapping.Rows)
        {
            row.Cells["colImport"].Value = false;
        }
    }

    // ===== v2.4: 一键导入所有 Sheet =====
    private void BtnImportAllSheets_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            MessageBox.Show(this, "请先选择 Excel 文件", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        List<string> sheets;
        try { sheets = ExcelImporter.ListSheets(_filePath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"读取文件失败:\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (sheets.Count == 0)
        {
            MessageBox.Show(this, "Excel 里没有 Sheet", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 每个 sheet 一个导入请求 (列名 = 表头, 类型自动推断, 别名 = sheet 名去重)
        var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<TempTableImportRequest>(sheets.Count);
        foreach (var sheetName in sheets)
        {
            try
            {
                var req = BuildAutoRequest(_filePath, sheetName, usedAliases);
                if (req is not null) results.Add(req);
            }
            catch { /* 空 sheet / 坏 sheet 静默跳过 */ }
        }

        if (results.Count == 0)
        {
            MessageBox.Show(this, "所有 Sheet 都没有数据行, 无法导入", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 确认摘要
        var summary = string.Join("\n", results.Select(r =>
            $"  #{r.Alias}  ←  {r.SheetName}  ({r.RawRows.Count} 行 × {r.Columns.Count} 列)"));
        var confirm = MessageBox.Show(this,
            $"将导入 {results.Count} 个 Sheet (列名 = Excel 表头, 类型自动推断):\n\n{summary}\n\n点 [是] 确认导入。",
            "全部 Sheet 导入", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        Results = results;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// ★ 2026-10-15 v2.4: 自动构建单 sheet 导入请求 (无 UI)
    /// 列名 = Excel 表头 (清理为合法标识符), 类型 = TypeInferrer 推断, 别名 = sheet 名 (冲突自动加 _2/_3)
    /// 用于 [全部 Sheet 导入] + SqlQueryForm 多文件自动导入
    /// </summary>
    public static TempTableImportRequest? BuildAutoRequest(string filePath, string sheetName, HashSet<string> usedAliases)
    {
        var data = ExcelImporter.ReadSheet(filePath, sheetName, maxRows: 0); // 全量
        if (data.Rows.Count == 0) return null;

        var inferred = TypeInferrer.Infer(data, sampleSize: Math.Min(100, data.Rows.Count));
        var columns = new List<TempTableColumn>(data.Columns.Count);
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < data.Columns.Count; c++)
        {
            var colName = data.Columns[c].ColumnName;
            var tgt = SanitizeAlias(colName);
            if (string.IsNullOrEmpty(tgt)) tgt = $"COL{c + 1}";
            if (!seenTargets.Add(tgt)) tgt = $"{tgt}_{c + 1}";
            var type = inferred.TryGetValue(colName, out var t) ? t : "NVARCHAR";
            columns.Add(new TempTableColumn
            {
                SourceName = colName,
                TargetName = tgt,
                TypeName = type,
                OriginalIndex = c
            });
        }

        // 别名 = sheet 名, 被占用则加 _2/_3
        var baseAlias = SanitizeAlias(sheetName);
        if (string.IsNullOrEmpty(baseAlias)) baseAlias = "SHEET";
        var alias = baseAlias;
        int suffix = 2;
        while (!usedAliases.Add(alias))
        {
            alias = $"{baseAlias}_{suffix++}";
        }

        // 全量 RawRows
        var rawRows = new List<object?[]>(data.Rows.Count);
        foreach (DataRow row in data.Rows)
        {
            var arr = new object?[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                var v = row[columns[i].OriginalIndex];
                arr[i] = (v is null || v is DBNull) ? null : v.ToString();
            }
            rawRows.Add(arr);
        }

        return new TempTableImportRequest
        {
            Alias = alias,
            SourceFile = filePath,
            SheetName = sheetName,
            Columns = columns,
            RawRows = rawRows
        };
    }

    private void SetStatus(string text)
    {
        lblStatus.Text = text;
    }
}
