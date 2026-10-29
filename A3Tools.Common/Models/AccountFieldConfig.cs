using System.Text.Json.Serialization;

namespace A3Tools.Models;

/// <summary>
/// 账套字段显示配置（每个账套字段一条记录）。
///
/// 需求（2026-10-28 陛下）：账套管理增加设置页面，可单独设置每个账套字段
///   ① 是否显示在列表   ② 列表宽度   ③ 列表排序
///   ④ 是否显示在录入界面   ⑤ 录入界面排序
///
/// 设计要点：
///   · Field 用 <see cref="Account"/> 的属性名做主键（如 "Code"），保证列表绑定与录入界面一致；
///   · ListWidth = 0 表示「自动」（按权重铺满可用宽度，非固定像素）；> 0 表示固定像素宽度；
///   · 排序号允许重复/有空洞，保存时由 AccountFieldResolver 归一化（重排成 0..n-1）；
///   · 密码类字段禁止进列表（CanShowInList=false），防止明文泄露到列表。
/// </summary>
public class AccountFieldConfig
{
    /// <summary>Account 上的属性名（如 Code / Name / RemoteType），主键</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>是否在「账套管理」列表中显示</summary>
    public bool ShowInList { get; set; } = true;

    /// <summary>列表列宽（像素）。0 = 自动（按权重铺满可用宽度）</summary>
    public int ListWidth { get; set; }

    /// <summary>列表排序（数字越小越靠前，保存时归一化）</summary>
    public int ListOrder { get; set; }

    /// <summary>是否在「账套录入」界面显示</summary>
    public bool ShowInEditor { get; set; } = true;

    /// <summary>录入界面排序（数字越小越靠前，保存时归一化）</summary>
    public int EditorOrder { get; set; }

    /// <summary>字段显示名（设置页面用；不参与持久化）</summary>
    [JsonIgnore]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>列表表头文本（不参与持久化）</summary>
    [JsonIgnore]
    public string ListHeader { get; set; } = string.Empty;

    /// <summary>是否允许出现在列表（密码类字段禁止，防止明文泄露）</summary>
    [JsonIgnore]
    public bool CanShowInList { get; set; } = true;

    /// <summary>自动列的分配权重（权重越大分到的空间越多）</summary>
    [JsonIgnore]
    public double ListWeight { get; set; } = 10;

    /// <summary>录入界面行距（像素），决定该字段在录入窗体里占多高</summary>
    [JsonIgnore]
    public int EditorRowAdvance { get; set; } = 45;

    /// <summary>录入界面字段前额外留白（像素），保留原布局的分组间距</summary>
    [JsonIgnore]
    public int EditorGapBefore { get; set; }

    /// <summary>录入界面排序是否固定（在分组面板内，不参与重排）</summary>
    [JsonIgnore]
    public bool EditorOrderFixed { get; set; }

    /// <summary>录入界面是否必填（必填字段不允许隐藏）</summary>
    [JsonIgnore]
    public bool Required { get; set; }
}
