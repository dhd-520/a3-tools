using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace A3Tools.Services;

/// <summary>
/// DataGridView 列宽管理（生产代码，供窗体直接使用；逻辑与控件定义分离，便于单测）。
///
/// 需求（2026-10-26 陛下）：首页「账套管理」调整列宽时
///   ① 只改【相邻列】，其余列纹丝不动；
///   ② 总宽度恒定，表格始终占满 100%（不出水平滚动条）。
///
/// 病根：列用 AutoSizeMode=Fill 时，WinForms 会在任何一列宽度变化后
///       把【所有】Fill 列按 FillWeight 重新分配 → 视觉上「一拖全动」。
///       实测：col[2] 设为 200px → col[3] 103→79、col[4] 92→70 全被连坐。
///
/// 修法：列全部改成 AutoSizeMode.None，宽度由本类统一分配：
///   · 首次按 FillWeight 权重铺满（尊重每列 MinimumWidth）；
///   · 拖动某列 → 该列取新宽，差值由【相邻列】吸收，总宽恒定；
///   · 窗口缩放 / 垂直滚动条出现 → 按当前比例重新铺满可用宽度。
///
/// ★ 2026-10-28 新增（陛下要的「列表宽度」设置）：
///   账套字段设置页允许给每个字段指定固定像素宽度。固定列在铺列宽时
///   **严格保持指定宽度**，不会被窗口缩放改写，也不会被相邻列拖动挤占；
///   其余列分剩余空间（不够时退到各自 MinimumWidth，允许出水平滚动条）。
/// </summary>
public sealed class DgvColumnLayoutManager : IDisposable
{
    private readonly DataGridView _grid;
    private readonly string? _fixedColumnName;

    /// <summary>各列当前生效宽度（列名 → 像素）。列重建后据此恢复用户调好的宽度。</summary>
    private readonly Dictionary<string, int> _widths = new(StringComparer.Ordinal);

    /// <summary>用户手动调整过的列 → 窗口缩放时不再锁定宽度（沿用 v2.6.7 语义）。</summary>
    private readonly HashSet<string> _userSized = new(StringComparer.Ordinal);

    /// <summary>配置指定的固定宽度（列名 → 像素）。0/缺省 = 自动。</summary>
    private readonly Dictionary<string, int> _configFixedWidths = new(StringComparer.Ordinal);

    /// <summary>程序自己写宽度时置 true，避免与用户拖动互相递归。</summary>
    private bool _applying;

    /// <summary>挂起期间忽略 ColumnWidthChanged（列重建时用）。</summary>
    private bool _suspended;

    private bool _disposed;

    public DgvColumnLayoutManager(DataGridView grid, string? fixedColumnName = null)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _fixedColumnName = fixedColumnName;

        _grid.ColumnWidthChanged += OnColumnWidthChanged;
        _grid.SizeChanged += (s, e) => ScheduleLayout();
        // 垂直滚动条出现/消失 → 可用宽度变了（实测 600 ↔ 583），必须重新铺满
        foreach (Control c in _grid.Controls)
        {
            if (c is VScrollBar vbar) vbar.VisibleChanged += (s, e) => ScheduleLayout();
        }
    }

    /// <summary>窗体在重建列（Columns.Clear + AddRange）期间调用，屏蔽回调。</summary>
    public void Suspend() => _suspended = true;

    public void Resume() => _suspended = false;

    /// <summary>
    /// 设置「配置固定宽度」的列（列名 → 像素宽）。
    /// 宽度 ≤ 0 的列视为自动（按权重分剩余空间）。
    /// </summary>
    public void SetConfigFixedWidths(IEnumerable<KeyValuePair<string, int>>? widths)
    {
        _configFixedWidths.Clear();
        if (widths != null)
        {
            foreach (var kv in widths)
            {
                if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0)
                    _configFixedWidths[kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>
    /// 清空「记忆中的列宽」，下次 <see cref="ColumnsChanged"/> 会按当前配置重新播种。
    /// 账套字段设置变更后调用（否则旧的记忆宽度会盖住新配置）。
    /// </summary>
    public void InvalidateLayout()
    {
        _widths.Clear();
        _userSized.Clear();
    }

    /// <summary>
    /// 列重建完成后调用：首次按 FillWeight 权重分配；已有记录则沿用用户调好的宽度。
    /// </summary>
    public void ColumnsChanged()
    {
        if (_disposed) return;
        SeedIfNeeded();
        ApplyLayout();
    }

    /// <summary>按当前可用宽度重新铺满列（窗口缩放后调用）。</summary>
    public void ApplyLayout()
    {
        if (_disposed || _grid.IsDisposed) return;
        int count = _grid.Columns.Count;
        if (count == 0) return;

        int available = GetAvailableWidth();
        if (available <= 0) return;

        var current = new int[count];
        var mins = new int[count];
        for (int i = 0; i < count; i++)
        {
            var col = _grid.Columns[i];
            current[i] = _widths.TryGetValue(col.Name, out var w) ? w : col.Width;
            mins[i] = col.MinimumWidth;
        }

        // 固定宽度列：严格保持配置值，不参与缩放
        var fixedWidths = BuildFixedWidths(out _);

        var next = DgvColumnWidthAllocator.ScaleToFitKeepFixed(current, mins, fixedWidths, available);
        ApplyWidths(next);
    }

    /// <summary>
    /// 表格可用宽度 = DisplayRectangle.Width - 1px。
    /// DisplayRectangle 已扣除垂直滚动条（实测：无滚动条 600，出现滚动条 583）；
    /// 再减 1px 是因为实测总宽正好等于 600 时水平滚动条会冒出来（599 才不出）。
    /// </summary>
    public int GetAvailableWidth()
    {
        int w = _grid.DisplayRectangle.Width;
        if (w <= 0) w = _grid.ClientSize.Width;
        return Math.Max(0, w - 1);
    }

    /// <summary>把算好的宽度写回控件并记录（写回期间屏蔽 ColumnWidthChanged 回调）。</summary>
    private void ApplyWidths(int[] widths)
    {
        if (_disposed || _grid.IsDisposed) return;
        int count = Math.Min(widths.Length, _grid.Columns.Count);
        _applying = true;
        try
        {
            for (int i = 0; i < count; i++)
            {
                var col = _grid.Columns[i];
                int w = Math.Max(col.MinimumWidth, widths[i]);
                if (col.Width != w) col.Width = w;
                _widths[col.Name] = w;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>首次铺列宽：固定列保持指定宽度，其余列按 FillWeight 权重分剩余空间。</summary>
    private void SeedIfNeeded()
    {
        int count = _grid.Columns.Count;
        if (count == 0) return;

        // 已经有全部列的记录 → 沿用用户调好的宽度，不重新播种
        bool allKnown = true;
        foreach (DataGridViewColumn c in _grid.Columns)
        {
            if (!_widths.ContainsKey(c.Name)) { allKnown = false; break; }
        }
        if (allKnown) return;

        int available = GetAvailableWidth();
        if (available <= 0) return;

        var weights = new double[count];
        var mins = new int[count];
        for (int i = 0; i < count; i++)
        {
            var c = _grid.Columns[i];
            weights[i] = c.FillWeight;
            mins[i] = c.MinimumWidth;
        }

        var fixedWidths = BuildFixedWidths(out _);

        // 固定列严格保持指定宽度；其余列按权重分剩余空间（尊重 MinimumWidth）
        var seed = DgvColumnWidthAllocator.DistributeByWeightKeepFixed(weights, mins, fixedWidths, available);
        ApplyWidths(seed);
    }

    /// <summary>
    /// 算出每列的「固定宽度」：配置指定 &gt; 0 → 用配置值；否则沿用旧版的固定列语义。
    /// </summary>
    private int[] BuildFixedWidths(out bool[] locked)
    {
        int count = _grid.Columns.Count;
        var fixedWidths = new int[count];
        locked = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int fw = GetFixedWidth(_grid.Columns[i]);
            if (fw > 0) { fixedWidths[i] = fw; locked[i] = true; }
        }
        return fixedWidths;
    }

    /// <summary>某列的固定像素宽（0 = 不固定）。</summary>
    private int GetFixedWidth(DataGridViewColumn col)
    {
        // 用户手动调过的列 → 不再锁定（沿用 v2.6.7「手动调整后不再固定」的语义）
        if (_userSized.Contains(col.Name)) return 0;

        if (_configFixedWidths.TryGetValue(col.Name, out int fw) && fw > 0)
            return fw;

        // 兼容旧调用：构造时指定的固定列（保持它自身的宽度）
        if (!string.IsNullOrEmpty(_fixedColumnName) && col.Name == _fixedColumnName)
            return col.Width;

        return 0;
    }

    /// <summary>用户拖动列宽 → 只让相邻列吸收差值，总宽恒定。</summary>
    private void OnColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
    {
        if (_disposed || _grid.IsDisposed) return;
        if (_applying || _suspended) return;

        var column = e.Column;
        if (column == null) return;

        int count = _grid.Columns.Count;
        int idx = column.Index;
        if (idx < 0 || idx >= count) return;

        // 当前宽度表：被拖的列用控件里的新值，其余用上次记录的生效值
        var current = new int[count];
        var mins = new int[count];
        for (int i = 0; i < count; i++)
        {
            var c = _grid.Columns[i];
            current[i] = _widths.TryGetValue(c.Name, out var w) ? w : c.Width;
            mins[i] = c.MinimumWidth;
        }

        // 用户手动拖了「固定列」→ 该列就此解除锁定（配置值仍然保留在文件里，下次启动再生效）
        if (GetFixedWidth(column) > 0)
            _userSized.Add(column.Name);

        var fixedWidths = BuildFixedWidths(out var locked);

        var result = DgvColumnWidthAllocator.ApplyUserResize(current, mins, idx, column.Width, locked);
        ApplyWidths(result);

        // 用户手动调过的列 → 窗口缩放时不再锁定宽度
        _userSized.Add(column.Name);
    }

    /// <summary>延迟一拍再重排（避开正在进行的布局/滚动条切换，避免重入）。</summary>
    private void ScheduleLayout()
    {
        if (_disposed || _suspended) return;
        if (!_grid.IsHandleCreated) return;
        try
        {
            _grid.BeginInvoke(new Action(() =>
            {
                if (_disposed) return;
                ApplyLayout();
            }));
        }
        catch (Exception)
        {
            // 句柄已销毁等情况忽略
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _grid.ColumnWidthChanged -= OnColumnWidthChanged;
        }
        catch (Exception)
        {
            // 控件已销毁时忽略
        }
    }
}
