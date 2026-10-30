namespace A3Tools.Services;

/// <summary>
/// 组内二级关联字段的对应模式（与一级关联字段的 Shared / PerTable 完全对称）。
/// </summary>
public enum SecondaryFieldMatchMode
{
    /// <summary>只填了 1 个关联字段：该组所有二级表共用这一个字段。</summary>
    Shared = 1,

    /// <summary>填了多个关联字段：与该组二级表按顺序一一对应。</summary>
    PerTable = 2
}

/// <summary>
/// 一个「一级关联表」下面的二级关联表组。
/// 组内每张二级表带一个外键列，指向该一级关联表的主键。
/// </summary>
public sealed class SecondaryLinkGroup
{
    /// <summary>对应的第几个一级关联表（0 基）。</summary>
    public int FirstLevelIndex { get; }

    /// <summary>本组的二级关联表（顺序与 <see cref="ForeignKeys"/> 一致）。</summary>
    public IReadOnlyList<string> Tables { get; }

    /// <summary>
    /// 每张二级表各自的外键列（= 二级表里指向一级关联表主键的列）。
    /// <b>已按组内模式展开</b>：Shared 模式下每张表都是同一个字段，所以与 <see cref="Tables"/> 始终等长同序。
    /// </summary>
    public IReadOnlyList<string> ForeignKeys { get; }

    /// <summary>组内对应模式（Shared = 共用一个 / PerTable = 一一对应）。</summary>
    public SecondaryFieldMatchMode Mode { get; }

    /// <summary>本组的二级关联表数量。</summary>
    public int Count => Tables.Count;

    public SecondaryLinkGroup(int firstLevelIndex, IReadOnlyList<string> tables, IReadOnlyList<string> foreignKeys,
        SecondaryFieldMatchMode mode)
    {
        FirstLevelIndex = firstLevelIndex;
        Tables = tables;
        ForeignKeys = foreignKeys;
        Mode = mode;
    }

    /// <summary>逐个取出「二级表 + 其外键列」的配对。</summary>
    public IEnumerable<(string Table, string ForeignKey)> Pairs
    {
        get
        {
            for (int i = 0; i < Tables.Count; i++)
                yield return (Tables[i], ForeignKeys[i]);
        }
    }
}

/// <summary>
/// 二级关联解析结果。配置不合法时 <see cref="Error"/> 有值。
/// </summary>
public sealed class SecondaryLinkResolution
{
    /// <summary>是否配置了二级关联。</summary>
    public bool IsEnabled { get; }

    /// <summary>各一级关联表下面的二级关联组（按一级索引升序，只含有二级表的组）。</summary>
    public IReadOnlyList<SecondaryLinkGroup> Groups { get; }

    /// <summary>配置不合法时的错误说明；null 表示合法。</summary>
    public string? Error { get; }

    /// <summary>配置是否合法。</summary>
    public bool IsValid => Error == null;

    /// <summary>是否存在可用的二级关联。</summary>
    public bool HasSecondaryLink => IsEnabled && Groups.Count > 0;

    internal SecondaryLinkResolution(bool isEnabled, IReadOnlyList<SecondaryLinkGroup> groups, string? error)
    {
        IsEnabled = isEnabled;
        Groups = groups;
        Error = error;
    }

    /// <summary>取挂在第 index 个一级关联表下面的二级组；没有则返回 null。</summary>
    public SecondaryLinkGroup? GetGroup(int firstLevelIndex)
    {
        foreach (var g in Groups)
            if (g.FirstLevelIndex == firstLevelIndex)
                return g;
        return null;
    }
}

/// <summary>
/// 自定义工具「二级关联」解析器（纯逻辑，无 WinForms 依赖，可单测）。
///
/// 规则（2026-10-29 陛下需求：主表 → 一级关联表 → 二级关联表）：
/// <list type="bullet">
/// <item>二级配置留空 → 不复制二级表（与旧配置完全兼容，旧数据无需迁移）。</item>
/// <item><b>分号 = 组边界</b>（第 N 组对应第 N 个一级关联表），<b>逗号 = 组内多个表</b>。
/// 例：<c>S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3</c>
/// → 第 1 个一级表下挂 2 张二级表，第 2 个一级表下挂 1 张。</item>
/// <item>二级关联字段与二级关联表<b>分组一一对应</b>（第 N 组 → 第 N 个一级关联表）。
/// 组内规则与一级关联字段<b>完全对称</b>：
/// 只填 1 个 → 该组所有二级表共用这一个字段；填多个 → 必须与该组表数一致、一一对应。</item>
/// <item>分组总数必须等于一级关联表数量（否则无法确定每组挂在哪个一级表下）。</item>
/// </list>
///
/// 二级表的「父值」= 对应一级关联表那一行的<b>主键值</b>（复制时按主键列名在源库取）。
/// 主键列优先取数据库里定义的 PRIMARY KEY，也可在配置里用「二级关联父键」手动指定。
/// </summary>
public static class CustomToolSecondaryLinkResolver
{
    /// <summary>按英文分号切成「组」（保留中间的空组：空组 = 该一级表没有二级表）。</summary>
    public static List<string> SplitGroups(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

        var parts = raw.Split(';').Select(p => p.Trim()).ToList();

        // 只去掉首尾的空组；中间的保留（中间空组表示"这一级表下面没有二级表"）
        while (parts.Count > 0 && parts[0].Length == 0) parts.RemoveAt(0);
        while (parts.Count > 0 && parts[parts.Count - 1].Length == 0) parts.RemoveAt(parts.Count - 1);

        return parts;
    }

    /// <summary>按英文逗号切成组内的多个项（去空白、去空项）。</summary>
    public static List<string> SplitItems(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(',')
                   .Select(p => p.Trim())
                   .Where(p => p.Length > 0)
                   .ToList();
    }

    /// <summary>
    /// 解析二级关联表与二级关联字段的分组对应关系。
    /// </summary>
    /// <param name="relatedTables">一级关联表（英文分号分隔）。</param>
    /// <param name="foreignKey">一级关联字段（英文分号分隔）。</param>
    /// <param name="secondaryTables">二级关联表（分号分组，逗号组内）。</param>
    /// <param name="secondaryFields">二级关联字段（分号分组，逗号组内）。</param>
    public static SecondaryLinkResolution Resolve(
        string? relatedTables,
        string? foreignKey,
        string? secondaryTables,
        string? secondaryFields)
    {
        var firstLevelTables = CustomToolForeignKeyResolver.SplitList(relatedTables);
        var firstLevelKeys = CustomToolForeignKeyResolver.SplitList(foreignKey);

        var tableGroups = SplitGroups(secondaryTables);
        var fieldGroups = SplitGroups(secondaryFields);

        bool hasTables = tableGroups.Count > 0;
        bool hasFields = fieldGroups.Count > 0;

        // 没配二级 → 不复制二级表（旧行为）
        if (!hasTables && !hasFields)
            return new SecondaryLinkResolution(false, new List<SecondaryLinkGroup>(), null);

        // 只填一半 → 报错
        if (hasTables != hasFields)
            return Fail("二级关联表和二级关联字段要么都填写，要么都不填写。");

        // 没有一级关联表 → 二级无从挂载
        if (firstLevelTables.Count == 0)
            return Fail("配置了二级关联，但没有配置一级关联表（关联表）。二级关联必须挂在一级关联表下面。");

        // 一级都没配关联字段 → 一级表本身不会被复制，二级自然也没有意义
        if (firstLevelKeys.Count == 0)
            return Fail("配置了二级关联，但一级关联字段为空。一级关联表不会被复制，二级关联也无法生效。");

        // 两边的分组数必须一样
        if (tableGroups.Count != fieldGroups.Count)
            return Fail($"二级关联表分组数（{tableGroups.Count}）与二级关联字段分组数（{fieldGroups.Count}）不一致。");

        // 分组数必须等于一级关联表数量（按顺序一一对应）
        if (tableGroups.Count != firstLevelTables.Count)
            return Fail(
                $"二级关联分组数（{tableGroups.Count}）与一级关联表数量（{firstLevelTables.Count}）不一致。" +
                "分组按顺序对应一级关联表：第 1 组 → 第 1 个一级关联表，第 2 组 → 第 2 个，以此类推。" +
                $"一级关联表：{string.Join(",", firstLevelTables)}");

        var groups = new List<SecondaryLinkGroup>();
        for (int i = 0; i < tableGroups.Count; i++)
        {
            var tables = SplitItems(tableGroups[i]);
            var fields = SplitItems(fieldGroups[i]);

            // 该一级表没有二级表 → 跳过
            if (tables.Count == 0 && fields.Count == 0) continue;

            // 表有但字段没配 → 报错
            if (tables.Count > 0 && fields.Count == 0)
                return Fail(
                    $"第 {i + 1} 组（对应一级关联表 {firstLevelTables[i]}）：配了 {tables.Count} 个二级关联表，" +
                    "却没有配二级关联字段。");

            // ★ 组内规则与一级关联字段完全对称：
            //   只填 1 个 → 该组所有二级表共用这一个（Shared）
            //   填多个   → 必须与该组表数一致、一一对应（PerTable）
            IReadOnlyList<string> effectiveFields;
            SecondaryFieldMatchMode mode;
            if (fields.Count == tables.Count)
            {
                effectiveFields = fields;
                mode = SecondaryFieldMatchMode.PerTable;
            }
            else if (fields.Count == 1)
            {
                effectiveFields = Enumerable.Repeat(fields[0], tables.Count).ToList();
                mode = SecondaryFieldMatchMode.Shared;
            }
            else
            {
                return Fail(
                    $"第 {i + 1} 组（对应一级关联表 {firstLevelTables[i]}）：二级关联表 {tables.Count} 个，" +
                    $"但二级关联字段 {fields.Count} 个。只填 1 个关联字段时该组所有表共用；" +
                    "填多个时必须与该组表数一致、一一对应。" +
                    $"表：{string.Join(",", tables)}；字段：{string.Join(",", fields)}");
            }

            groups.Add(new SecondaryLinkGroup(i, tables, effectiveFields, mode));
        }

        return new SecondaryLinkResolution(true, groups, null);
    }

    private static SecondaryLinkResolution Fail(string message) =>
        new SecondaryLinkResolution(false, new List<SecondaryLinkGroup>(), message);
}
