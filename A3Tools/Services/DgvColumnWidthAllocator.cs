using System;
using System.Collections.Generic;

namespace A3Tools.Services;

/// <summary>
/// DataGridView 列宽分配算法（纯逻辑，不依赖 WinForms，便于单元测试）。
///
/// 需求（2026-10-26 陛下）：首页「账套管理」拖动列宽时
///   ① 只有【相邻列】跟着变，其余列纹丝不动；
///   ② 总宽度恒定，表格始终占满 100%（不出现水平滚动条）。
///
/// 病根：列用 AutoSizeMode=Fill 时，WinForms 会在任何一列宽度变化后
///       把【所有】Fill 列按 FillWeight 重新分配 → 视觉上「一拖全动」。
///       实测：col[2] 设为 200px → col[3] 103→79、col[4] 92→70 全被连坐。
///
/// 修法：所有列改成 AutoSizeMode.None，宽度改由本类统一分配：
///   · 拖动某列 → 该列取新宽，差值由【相邻列】吸收，列宽总和保持不变；
///   · 窗口/滚动条变化 → 在锁定列之外，按当前宽度比例重新铺满可用宽度。
/// </summary>
public static class DgvColumnWidthAllocator
{
    /// <summary>
    /// 按当前宽度比例，把 <paramref name="totalWidth"/> 铺满所有列。
    /// </summary>
    /// <param name="current">当前各列宽度（同时作为比例基准）。</param>
    /// <param name="minWidths">各列最小宽度（不可突破），可为 null。</param>
    /// <param name="locked">locked[i]=true 的列保持原宽不动（如「代码」列），其余列分剩余空间；可为 null。</param>
    /// <param name="totalWidth">目标总宽（各列宽度之和）。</param>
    /// <returns>新的列宽数组；空间允许时总和 == totalWidth。</returns>
    public static int[] ScaleToFit(int[] current, int[]? minWidths, bool[]? locked, int totalWidth)
    {
        int n = current.Length;
        var result = (int[])current.Clone();
        if (n == 0 || totalWidth <= 0) return result;

        var mins = BuildMins(n, minWidths);
        var weights = new double[n];
        for (int i = 0; i < n; i++) weights[i] = current[i];

        return FitCore(weights, result, mins, BuildLocks(n, locked), totalWidth);
    }

    /// <summary>
    /// 按权重（FillWeight）首次分配列宽，把 <paramref name="totalWidth"/> 铺满。
    /// </summary>
    /// <param name="weights">各列权重（0 表示不参与分配）。</param>
    /// <param name="minWidths">各列最小宽度，可为 null。</param>
    /// <param name="fixedWidths">fixedWidths[i] &gt; 0 的列固定该宽度，其余列分剩余空间；可为 null。</param>
    /// <param name="totalWidth">目标总宽。</param>
    /// <returns>新的列宽数组；总和 == totalWidth（空间不足时除外）。</returns>
    public static int[] DistributeByWeight(double[] weights, int[]? minWidths, int[]? fixedWidths, int totalWidth)
    {
        int n = weights.Length;
        var result = new int[n];
        if (n == 0 || totalWidth <= 0) return result;

        var mins = BuildMins(n, minWidths);
        var locks = new bool[n];
        for (int i = 0; i < n; i++)
        {
            int fw = fixedWidths != null && i < fixedWidths.Length ? fixedWidths[i] : 0;
            if (fw > 0) { result[i] = fw; locks[i] = true; }
        }

        return FitCore(weights, result, mins, locks, totalWidth);
    }

    /// <summary>
    /// 模拟用户拖动某一列：被拖列取新宽，差值由【相邻列】吸收，列宽总和保持不变。
    /// 吸收顺序：先右侧（用户拖的是该列右边的分隔线），再往左回退。
    /// </summary>
    /// <param name="current">拖动前的各列宽度。</param>
    /// <param name="minWidths">各列最小宽度，可为 null。</param>
    /// <param name="changedIndex">被拖动列的下标。</param>
    /// <param name="newWidth">被拖动列的新宽度。</param>
    /// <returns>新的列宽数组；总和与入参一致。</returns>
    public static int[] ApplyUserResize(int[] current, int[]? minWidths, int changedIndex, int newWidth)
    {
        int n = current.Length;
        var result = (int[])current.Clone();
        if (changedIndex < 0 || changedIndex >= n) return result;

        var mins = BuildMins(n, minWidths);

        if (newWidth < mins[changedIndex]) newWidth = mins[changedIndex];

        int delta = newWidth - current[changedIndex];
        result[changedIndex] = newWidth;
        if (delta == 0) return result;

        // 吸收顺序：先右侧（用户拖的是该列右边的分隔线），再往左回退
        var order = new List<int>();
        for (int i = changedIndex + 1; i < n; i++) order.Add(i);
        for (int i = changedIndex - 1; i >= 0; i--) order.Add(i);

        // pending > 0：还需要从别的列身上「扣掉」多少像素
        // pending < 0：别的列需要「长出」多少像素
        int pending = delta;
        foreach (int idx in order)
        {
            if (pending == 0) break;
            if (pending > 0)
            {
                int canTake = result[idx] - mins[idx];
                if (canTake <= 0) continue;
                int take = Math.Min(pending, canTake);
                result[idx] -= take;
                pending -= take;
            }
            else
            {
                result[idx] += -pending;
                pending = 0;
            }
        }

        if (pending > 0)
        {
            // 其它列都到底了 → 把自己缩回去，保证总宽恒定
            result[changedIndex] = Math.Max(mins[changedIndex], result[changedIndex] - pending);
        }

        return result;
    }

    /// <summary>
    /// 核心：把 totalWidth 按权重铺到所有列（锁定列不动），并尊重每列最小宽。
    /// 水位填充 + 最大余数法，保证总和精确等于 totalWidth。
    /// </summary>
    private static int[] FitCore(double[] weights, int[] result, int[] mins, bool[] locks, int totalWidth)
    {
        int n = weights.Length;

        var flex = new List<int>();
        int lockedSum = 0;
        for (int i = 0; i < n; i++)
        {
            if (locks[i]) lockedSum += result[i];
            else flex.Add(i);
        }
        if (flex.Count == 0) return result;

        // 锁定列占掉的空间太多 → 放弃锁定，全部按权重分（否则填不满 100%）
        int flexMinSum = 0;
        foreach (int i in flex) flexMinSum += mins[i];
        if (totalWidth - lockedSum < flexMinSum)
        {
            flex = new List<int>();
            for (int i = 0; i < n; i++) flex.Add(i);
            lockedSum = 0;
        }

        int budget = totalWidth - lockedSum;
        if (budget <= 0) return result;

        // 水位填充：反复把「按比例会低于最小宽」的列钉在最小宽上，剩余空间重新按比例分
        var pinned = new bool[n];
        for (int guard = 0; guard <= n + 1; guard++)
        {
            int pinnedSum = 0, freeCount = 0;
            double freeWeight = 0;
            foreach (int i in flex)
            {
                if (pinned[i]) pinnedSum += result[i];
                else { freeWeight += Math.Max(0, weights[i]); freeCount++; }
            }
            if (freeCount == 0) break;

            int freeBudget = budget - pinnedSum;
            if (freeBudget <= 0) break;

            if (freeWeight <= 0)
            {
                // 权重全为 0 → 均分
                int each = freeBudget / freeCount, extra = freeBudget % freeCount, k = 0;
                foreach (int i in flex)
                {
                    if (pinned[i]) continue;
                    result[i] = each;
                    if (k < extra) { result[i]++; k++; }
                }
                break;
            }

            int toPin = -1;
            foreach (int i in flex)
            {
                if (pinned[i]) continue;
                double exact = Math.Max(0, weights[i]) / freeWeight * freeBudget;
                if ((int)Math.Floor(exact) < mins[i]) { toPin = i; break; }
            }
            if (toPin >= 0)
            {
                pinned[toPin] = true;
                result[toPin] = mins[toPin];
                continue;
            }

            // 全部可分配 → 按比例取整 + 最大余数法补齐，保证总和精确等于 freeBudget
            var order = new List<int>();
            var frac = new List<double>();
            int assigned = 0;
            foreach (int i in flex)
            {
                if (pinned[i]) continue;
                double exact = Math.Max(0, weights[i]) / freeWeight * freeBudget;
                int fl = (int)Math.Floor(exact);
                result[i] = fl;
                assigned += fl;
                order.Add(i);
                frac.Add(exact - fl);
            }
            int rest = freeBudget - assigned;
            var byFrac = new List<int>();
            for (int k = 0; k < order.Count; k++) byFrac.Add(k);
            byFrac.Sort((a, b) => frac[b].CompareTo(frac[a]));
            for (int k = 0; rest > 0 && byFrac.Count > 0; k++, rest--)
                result[order[byFrac[k % byFrac.Count]]]++;
            break;
        }

        // ★ 空间不足时水位填充会提前退出（freeBudget 变负），未处理的列会停在 0 → 突破最小宽。
        //   这里补一道底线：任何列都不允许小于它的 MinimumWidth。
        //   （此时总宽会超出 totalWidth → 出水平滚动条，这是空间不够时的必然结果，
        //     总比把列压成 0 看不见内容强。）
        for (int i = 0; i < n; i++)
        {
            if (result[i] < mins[i]) result[i] = mins[i];
        }

        return result;
    }

    private static int[] BuildMins(int n, int[]? minWidths)
    {
        var mins = new int[n];
        for (int i = 0; i < n; i++)
            mins[i] = minWidths != null && i < minWidths.Length ? Math.Max(0, minWidths[i]) : 0;
        return mins;
    }

    private static bool[] BuildLocks(int n, bool[]? locked)
    {
        var locks = new bool[n];
        for (int i = 0; i < n; i++)
            locks[i] = locked != null && i < locked.Length && locked[i];
        return locks;
    }
}
