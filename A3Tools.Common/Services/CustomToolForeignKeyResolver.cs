namespace A3Tools.Services;

/// <summary>
/// 关联字段与关联表的对应模式。
/// </summary>
public enum ForeignKeyMatchMode
{
    /// <summary>未配置关联字段：只复制主表，不复制关联表。</summary>
    None = 0,

    /// <summary>只配置了一个关联字段：所有关联表共用这一个字段（兼容旧配置）。</summary>
    Shared = 1,

    /// <summary>配置了多个关联字段：与关联表按顺序一一对应。</summary>
    PerTable = 2
}

/// <summary>
/// 关联字段解析结果。<see cref="ForeignKeys"/> 与 <see cref="RelatedTables"/> 始终等长、同序。
/// </summary>
public sealed class ForeignKeyResolution
{
    /// <summary>对应模式。</summary>
    public ForeignKeyMatchMode Mode { get; }

    /// <summary>解析出的关联表列表（去空白、去空项）。</summary>
    public IReadOnlyList<string> RelatedTables { get; }

    /// <summary>与 RelatedTables 一一对应的外键字段；null 表示该表没有外键（不复制）。</summary>
    public IReadOnlyList<string?> ForeignKeys { get; }

    /// <summary>配置不合法时的错误说明；null 表示合法。</summary>
    public string? Error { get; }

    /// <summary>配置是否合法。</summary>
    public bool IsValid => Error == null;

    /// <summary>是否存在可用于复制关联表的外键。</summary>
    public bool HasForeignKey => Mode != ForeignKeyMatchMode.None;

    internal ForeignKeyResolution(
        ForeignKeyMatchMode mode,
        IReadOnlyList<string> relatedTables,
        IReadOnlyList<string?> foreignKeys,
        string? error)
    {
        Mode = mode;
        RelatedTables = relatedTables;
        ForeignKeys = foreignKeys;
        Error = error;
    }

    /// <summary>取第 index 个关联表对应的外键字段；越界或未配置时返回 null。</summary>
    public string? GetForeignKey(int index)
    {
        if (index < 0 || index >= ForeignKeys.Count) return null;
        return ForeignKeys[index];
    }
}

/// <summary>
/// 自定义工具「关联字段」解析器（纯逻辑，无 WinForms 依赖，可单测）。
///
/// 规则（2026-10-26 陛下需求）：
/// - 关联字段留空 → 只复制主表，不复制关联表。
/// - 只填 1 个 → 所有关联表共用这一个字段（完全兼容旧配置，旧数据无需迁移）。
/// - 填多个 → 必须与关联表数量一致，按顺序一一对应。
/// </summary>
public static class CustomToolForeignKeyResolver
{
    /// <summary>按英文分号切分并去掉空白项与空项。</summary>
    public static List<string> SplitList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// 解析关联表与关联字段的对应关系。
    /// </summary>
    /// <param name="relatedTables">关联表，英文分号分隔。</param>
    /// <param name="foreignKey">关联字段，英文分号分隔。</param>
    public static ForeignKeyResolution Resolve(string? relatedTables, string? foreignKey)
    {
        var tables = SplitList(relatedTables);
        var fks = SplitList(foreignKey);

        // 未配置关联字段：只复制主表。
        if (fks.Count == 0)
            return new ForeignKeyResolution(ForeignKeyMatchMode.None, tables, new string?[tables.Count], null);

        // 没有关联表时关联字段无意义，保持旧行为（不复制，也不报错）。
        if (tables.Count == 0)
            return new ForeignKeyResolution(ForeignKeyMatchMode.None, tables, new string?[0], null);

        // 只配一个：所有关联表共用（兼容旧配置）。
        if (fks.Count == 1)
        {
            var shared = new string?[tables.Count];
            for (int i = 0; i < tables.Count; i++) shared[i] = fks[0];
            return new ForeignKeyResolution(ForeignKeyMatchMode.Shared, tables, shared, null);
        }

        // 配多个：必须与关联表数量一致，一一对应。
        if (fks.Count == tables.Count)
        {
            var perTable = new string?[tables.Count];
            for (int i = 0; i < tables.Count; i++) perTable[i] = fks[i];
            return new ForeignKeyResolution(ForeignKeyMatchMode.PerTable, tables, perTable, null);
        }

        var message =
            $"关联字段数量（{fks.Count}）与关联表数量（{tables.Count}）不一致。" +
            "只填 1 个关联字段时所有关联表共用；填多个时必须与关联表按顺序一一对应。";
        return new ForeignKeyResolution(ForeignKeyMatchMode.None, tables, new string?[tables.Count], message);
    }
}
