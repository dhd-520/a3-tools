using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 账套字段配置解析器 —— 纯逻辑（不依赖 WinForms，可直接单测）。
///
/// 职责：
///   ① 把用户存下来的配置与 <see cref="AccountFieldCatalog"/> 的字段目录合并成一份「权威配置」；
///   ② 纠正非法值（宽度越界、密码字段想进列表、必填字段想从录入界面隐藏…）；
///   ③ 把排序号归一化（允许重复/空洞，保存后重排成 0..n-1，保证顺序稳定可预期）；
///   ④ 产出两个消费方各自需要的视图：
///        · <see cref="GetListColumns"/>  → 账套列表要显示哪些列、什么顺序、什么宽度
///        · <see cref="GetEditorFields"/> → 录入界面要显示哪些字段、什么顺序
/// </summary>
public static class AccountFieldResolver
{
    /// <summary>列表列宽上限（像素），防止手滑填个 10 万把表格撑爆</summary>
    public const int MaxListWidth = 2000;

    /// <summary>
    /// 合并用户配置与字段目录 → 权威配置（覆盖所有目录里的字段，且顺序与目录一致）。
    /// </summary>
    /// <param name="stored">用户保存的配置（可能为 null / 缺字段 / 有垃圾字段）</param>
    public static List<AccountFieldConfig> Normalize(IEnumerable<AccountFieldConfig>? stored)
    {
        // 用户存的值按字段名建索引（忽略大小写，容忍手改 JSON）
        var userValues = new Dictionary<string, AccountFieldConfig>(StringComparer.OrdinalIgnoreCase);
        if (stored != null)
        {
            foreach (var s in stored)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Field)) continue;
                userValues[s.Field] = s;
            }
        }

        var merged = new List<AccountFieldConfig>(AccountFieldCatalog.All.Count);
        foreach (var def in AccountFieldCatalog.All)
        {
            var m = new AccountFieldConfig
            {
                Field = def.Field,
                ShowInList = def.ShowInList,
                ListWidth = def.ListWidth,
                ListOrder = def.ListOrder,
                ShowInEditor = def.ShowInEditor,
                EditorOrder = def.EditorOrder,
                DisplayName = def.DisplayName,
                ListHeader = def.ListHeader,
                CanShowInList = def.CanShowInList,
                ListWeight = def.ListWeight,
                EditorRowAdvance = def.EditorRowAdvance,
                EditorGapBefore = def.EditorGapBefore,
                EditorGroup = def.EditorGroup,
                Required = def.Required,
            };

            // 叠加用户改过的值
            if (userValues.TryGetValue(def.Field, out var u))
            {
                m.ShowInList = u.ShowInList;
                m.ListWidth = u.ListWidth;
                m.ListOrder = u.ListOrder;
                m.ShowInEditor = u.ShowInEditor;
                m.EditorOrder = u.EditorOrder;
            }

            // —— 强制约束（用户改不动的部分）——
            // 密码类字段禁止进列表：列表会明文显示，泄露风险
            if (!m.CanShowInList) m.ShowInList = false;
            // 必填字段（代码/账套名称）不允许从录入界面隐藏：
            // 新增账套时它们必须能填，否则校验会卡在不可见的输入框上
            if (m.Required) m.ShowInEditor = true;
            // 宽度钳制
            if (m.ListWidth < 0) m.ListWidth = 0;
            if (m.ListWidth > MaxListWidth) m.ListWidth = MaxListWidth;

            merged.Add(m);
        }

        // 列表至少要有一列，否则 DataGridView 没有列 → 界面直接崩
        if (!merged.Any(x => x.ShowInList))
        {
            var code = merged.FirstOrDefault(x => x.Field == nameof(Account.Code));
            if (code != null) code.ShowInList = true;
        }

        // 排序号归一化（0..n-1，稳定排序：同号时保持目录里的先后）
        Renumber(merged, x => x.ListOrder, (x, v) => x.ListOrder = v);
        Renumber(merged, x => x.EditorOrder, (x, v) => x.EditorOrder = v);

        return merged;
    }

    /// <summary>
    /// 账套列表要显示的列：按列表排序号升序。
    /// </summary>
    public static List<AccountFieldConfig> GetListColumns(List<AccountFieldConfig> config)
    {
        if (config == null) return new List<AccountFieldConfig>();
        return config
            .Where(x => x.ShowInList && x.CanShowInList)
            .OrderBy(x => x.ListOrder)
            .ToList();
    }

    /// <summary>
    /// 把某个字段移到第 <paramref name="newPos"/> 位（1 基），其余字段依次顺延，
    /// 最后把排序号重排为连续的 0..n-1。
    ///
    /// ★ 为什么需要它（2026-10-29 踩坑）：
    ///   排序号如果只是「排序键」，用户把某字段的序号改成 3（想放到第 3 位）时，
    ///   若原来就有个字段是 3，稳定排序会让这两个字段按原有先后排 —— 用户的意图被无视。
    ///   所以设置页一律按「位置语义」处理：填几就是第几位，这里负责把位置算出来。
    /// </summary>
    /// <param name="configs">要重排的配置集合（会被就地修改）</param>
    /// <param name="field">目标字段名</param>
    /// <param name="newPos">目标位置（1 基，1 = 最前；超出范围则钳到边界）</param>
    /// <param name="isEditorOrder">true = 排录入界面顺序；false = 排列表顺序</param>
    public static void MoveTo(List<AccountFieldConfig> configs, string field, int newPos, bool isEditorOrder)
    {
        if (configs == null || string.IsNullOrEmpty(field)) return;

        // OrderBy 是稳定排序：序号相同时保持原有先后（不乱）
        var ordered = configs
            .OrderBy(c => isEditorOrder ? c.EditorOrder : c.ListOrder)
            .ToList();

        var item = ordered.FirstOrDefault(c =>
            string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
        if (item == null) return;

        ordered.Remove(item);
        int idx = Math.Max(0, Math.Min(newPos - 1, ordered.Count));
        ordered.Insert(idx, item);

        for (int i = 0; i < ordered.Count; i++)
        {
            if (isEditorOrder) ordered[i].EditorOrder = i;
            else ordered[i].ListOrder = i;
        }
    }

    /// <summary>
    /// 录入界面要显示的字段：按录入排序号升序。
    ///
    /// ★ 分组字段（如「账套用户名」在「网页版自动登录」面板里）**也在流里**——
    ///   它的排序号决定整个分组面板在流中的位置（分组跟着一起移动）。
    /// </summary>
    public static List<AccountFieldConfig> GetEditorFields(List<AccountFieldConfig> config)
    {
        if (config == null) return new List<AccountFieldConfig>();
        return config
            .Where(x => x.ShowInEditor)
            .OrderBy(x => x.EditorOrder)
            .ToList();
    }

    /// <summary>
    /// 把某个排序字段重新编号成 0..n-1（稳定排序：同号时保持原有先后）。
    /// </summary>
    private static void Renumber(List<AccountFieldConfig> items,
                                 Func<AccountFieldConfig, int> getOrder,
                                 Action<AccountFieldConfig, int> setOrder)
    {
        var ordered = items
            .Select((x, i) => (x, i))
            .OrderBy(t => getOrder(t.x))
            .ThenBy(t => t.i)
            .Select(t => t.x)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
            setOrder(ordered[i], i);
    }
}
