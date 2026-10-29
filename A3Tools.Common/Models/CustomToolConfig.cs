using System.Text.Json.Serialization;
using A3Tools.Services;

namespace A3Tools.Models;

/// <summary>
/// 自定义复制工具配置。
/// 由用户在「自定义工具」配置窗体填写并保存到 DATA\custom-tools.json，
/// 启动时由 MainForm 加载并生成工具箱按钮，点击后调用通用复制窗体执行。
/// </summary>
public class CustomToolConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;          // 工具名（按钮显示）
    public string Description { get; set; } = string.Empty;   // 描述（按钮副标题）
    public string MainTable { get; set; } = string.Empty;     // 主表（如 S_REPORT）
    public string PrimaryKey { get; set; } = string.Empty;    // 主键字段（如 CODE）
    public string RelatedTables { get; set; } = string.Empty; // 关联表，英文分号分隔（如 S_REPORTLINKDEFINE;S_REPORTDOUBLECLICK）

    /// <summary>
    /// 关联表外键字段，英文分号分隔。
    /// 只填 1 个 → 所有关联表共用这一个（兼容旧配置，旧数据无需迁移）；
    /// 填多个 → 必须与关联表数量一致，按顺序一一对应（如 S_REPORTLINKDEFINE;S_REPORTDOUBLECLICK → REPORTGUID;DOUBLECLICKGUID）。
    /// </summary>
    public string ForeignKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>搜索列名，英文分号分隔（如 GUID;CODE;SUBSYSTEMGUID;NAME;NOTES）。空表示用旧行为：主键列 + 存在 NAME 则加上。</summary>
    public string SearchColumns { get; set; } = string.Empty;

    /// <summary>列显示名称，英文分号分隔（如 GUID;代码;分类;名称;备注）。数量必须与 SearchColumns 完全一致；缺失则保留数据库原列名。</summary>
    public string ColumnDisplayNames { get; set; } = string.Empty;

    /// <summary>隐藏列，英文分号分隔（如 GUID;SUBSYSTEMGUID）。即便在 SearchColumns 中出现也会强制隐藏；但 PrimaryKey 不受此影响（永远显示）。</summary>
    public string HiddenColumns { get; set; } = string.Empty;

    /// <summary>
    /// 二级关联表：**分号 = 组边界**（第 N 组对应第 N 个一级关联表），**逗号 = 组内多个表**。
    /// 例：<c>S_PRINTSCHEMEDATA,S_PRINTSCHEMEDATA2;S_PRINTSCHEMEDATA3</c>
    /// → 第 1 个一级关联表下挂 2 张二级表，第 2 个一级关联表下挂 1 张。
    /// 留空 = 不复制二级表（旧配置完全兼容，无需迁移）。
    /// </summary>
    public string SecondaryRelatedTables { get; set; } = string.Empty;

    /// <summary>
    /// 二级关联字段：分组规则与 <see cref="SecondaryRelatedTables"/> 完全一致。
    /// 值 = 二级表里指向一级关联表主键的外键列名（平行于一级关联字段）。
    /// </summary>
    public string SecondaryForeignKey { get; set; } = string.Empty;

    /// <summary>
    /// 二级关联指向的「一级关联表主键列名」，英文分号分隔（与一级关联表按顺序一一对应）。
    /// 留空 = 自动探测（先查数据库定义的 PRIMARY KEY，再退 GUID / OBJECTGUID / CODE）。
    /// </summary>
    public string SecondaryParentKey { get; set; } = string.Empty;

    /// <summary>把 RelatedTables 解析为字符串列表（去空白 + 去空）</summary>
    [JsonIgnore]
    public List<string> RelatedTableList =>
        string.IsNullOrWhiteSpace(RelatedTables)
            ? new List<string>()
            : RelatedTables.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 ForeignKey 解析为字段列表（去空白 + 去空项）</summary>
    [JsonIgnore]
    public List<string> ForeignKeyList =>
        CustomToolForeignKeyResolver.SplitList(ForeignKey);

    /// <summary>
    /// 关联字段与关联表的对应关系（Shared=所有表共用一个 / PerTable=一一对应 / None=不复制关联表）。
    /// 解析规则见 <see cref="CustomToolForeignKeyResolver"/>。
    /// </summary>
    [JsonIgnore]
    public ForeignKeyResolution ForeignKeys =>
        CustomToolForeignKeyResolver.Resolve(RelatedTables, ForeignKey);

    /// <summary>
    /// 二级关联表与二级关联字段的分组对应关系（每组挂在哪个一级关联表下）。
    /// 解析规则见 <see cref="CustomToolSecondaryLinkResolver"/>。
    /// </summary>
    [JsonIgnore]
    public SecondaryLinkResolution SecondaryLinks =>
        CustomToolSecondaryLinkResolver.Resolve(RelatedTables, ForeignKey, SecondaryRelatedTables, SecondaryForeignKey);

    /// <summary>把 SecondaryParentKey 解析为字符串列表（去空白 + 去空项）。</summary>
    [JsonIgnore]
    public List<string> SecondaryParentKeyList =>
        CustomToolForeignKeyResolver.SplitList(SecondaryParentKey);

    /// <summary>把 SearchColumns 解析为字符串列表</summary>
    [JsonIgnore]
    public List<string> SearchColumnList =>
        string.IsNullOrWhiteSpace(SearchColumns)
            ? new List<string>()
            : SearchColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 ColumnDisplayNames 解析为字符串列表</summary>
    [JsonIgnore]
    public List<string> ColumnDisplayNameList =>
        string.IsNullOrWhiteSpace(ColumnDisplayNames)
            ? new List<string>()
            : ColumnDisplayNames.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>把 HiddenColumns 解析为字符串列表（转大写用于大小写不敏感比较）</summary>
    [JsonIgnore]
    public HashSet<string> HiddenColumnSet =>
        string.IsNullOrWhiteSpace(HiddenColumns)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                HiddenColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
}