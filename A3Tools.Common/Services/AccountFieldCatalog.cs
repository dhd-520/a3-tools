using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 账套字段目录 —— 【单一数据源】。
///
/// 需求（2026-10-28 陛下）：账套管理增加字段设置页面，
///   每个字段可单独设置：是否显示列表 / 列表宽度 / 列表排序 / 是否显示录入界面 / 录入界面排序。
///
/// 为什么要有这个目录：
///   设置页面、账套列表（MainForm）、录入窗体（AccountDialog）三处都要用同一份字段清单，
///   各写各的必然漂移（设置页多一个字段、列表少一个）。
///   这里定义「有哪些字段 + 默认表现」，三处都从这里取；默认值 = 改造前的行为（零回归）。
///
/// 改造前的行为快照（作为默认值，保证不配置时与原来完全一致）：
///   列表 9 列：代码/账套名称/账套地址/数据库地址/数据库名称/DB用户/远程方式/远程地址/远程用户
///   录入界面 14 个可重排字段 + 1 个固定在「网页版自动登录」分组内的字段
///   录入界面行距 45px，远程方式前额外留白 10px，备注行 80px
/// </summary>
public static class AccountFieldCatalog
{
    /// <summary>字段清单（顺序 = 默认列表顺序）</summary>
    public static readonly IReadOnlyList<AccountFieldConfig> All = new List<AccountFieldConfig>
    {
        // ===== 列表 + 录入界面 =====
        new() { Field = nameof(Account.Code),           DisplayName = "代码",       ListHeader = "代码",       ShowInList = true,  ListWidth = 120, ListOrder = 0,  ShowInEditor = true, EditorOrder = 0,  ListWeight = 100, Required = true },
        new() { Field = nameof(Account.Name),           DisplayName = "账套名称",   ListHeader = "账套名称",   ShowInList = true,  ListWidth = 0,   ListOrder = 1,  ShowInEditor = true, EditorOrder = 1,  ListWeight = 25 },
        new() { Field = nameof(Account.Server),         DisplayName = "账套地址",   ListHeader = "账套地址",   ShowInList = true,  ListWidth = 0,   ListOrder = 2,  ShowInEditor = true, EditorOrder = 2,  ListWeight = 30 },
        new() { Field = nameof(Account.ServerBackup),   DisplayName = "备用地址",   ListHeader = "备用地址",   ShowInList = false, ListWidth = 0,   ListOrder = 9,  ShowInEditor = true, EditorOrder = 3,  ListWeight = 20 },
        new() { Field = nameof(Account.ServerPassword), DisplayName = "账套密码",   ListHeader = "账套密码",   ShowInList = false, ListWidth = 0,   ListOrder = 12, ShowInEditor = true, EditorOrder = 4,  CanShowInList = false },
        new() { Field = nameof(Account.Database),       DisplayName = "数据库地址", ListHeader = "数据库地址", ShowInList = true,  ListWidth = 0,   ListOrder = 3,  ShowInEditor = true, EditorOrder = 5,  ListWeight = 20 },
        new() { Field = nameof(Account.DatabaseName),   DisplayName = "数据库名称", ListHeader = "数据库名称", ShowInList = true,  ListWidth = 0,   ListOrder = 4,  ShowInEditor = true, EditorOrder = 6,  ListWeight = 18 },
        new() { Field = nameof(Account.DbUser),         DisplayName = "DB用户名",   ListHeader = "DB用户",     ShowInList = true,  ListWidth = 0,   ListOrder = 5,  ShowInEditor = true, EditorOrder = 7,  ListWeight = 12 },
        new() { Field = nameof(Account.DbPassword),     DisplayName = "DB密码",     ListHeader = "DB密码",     ShowInList = false, ListWidth = 0,   ListOrder = 13, ShowInEditor = true, EditorOrder = 8,  CanShowInList = false },
        new() { Field = nameof(Account.RemoteType),     DisplayName = "远程方式",   ListHeader = "远程方式",   ShowInList = true,  ListWidth = 0,   ListOrder = 6,  ShowInEditor = true, EditorOrder = 9,  ListWeight = 10, EditorGapBefore = 10 },
        new() { Field = nameof(Account.RemoteAddress),  DisplayName = "远程地址",   ListHeader = "远程地址",   ShowInList = true,  ListWidth = 0,   ListOrder = 7,  ShowInEditor = true, EditorOrder = 10, ListWeight = 25 },
        new() { Field = nameof(Account.RemoteUser),     DisplayName = "远程用户名", ListHeader = "远程用户",   ShowInList = true,  ListWidth = 0,   ListOrder = 8,  ShowInEditor = true, EditorOrder = 11, ListWeight = 12 },
        new() { Field = nameof(Account.RemotePassword), DisplayName = "远程密码",   ListHeader = "远程密码",   ShowInList = false, ListWidth = 0,   ListOrder = 14, ShowInEditor = true, EditorOrder = 12, CanShowInList = false },
        new() { Field = nameof(Account.Remark),         DisplayName = "备注",       ListHeader = "备注",       ShowInList = false, ListWidth = 0,   ListOrder = 10, ShowInEditor = true, EditorOrder = 13, ListWeight = 20, EditorRowAdvance = 80 },

        // ===== 只在录入界面（普通字段，可自由排序）=====
        new() { Field = nameof(Account.ServerUsername), DisplayName = "账套用户名", ListHeader = "账套用户名", ShowInList = false, ListWidth = 0, ListOrder = 15, ShowInEditor = true, EditorOrder = 14, ListWeight = 15 },
    };

    /// <summary>按字段名取定义（找不到返回 null）</summary>
    public static AccountFieldConfig? Find(string? field)
    {
        if (string.IsNullOrEmpty(field)) return null;
        foreach (var f in All)
        {
            if (string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase))
                return f;
        }
        return null;
    }

    /// <summary>
    /// 是否内置字段（2026-10-29 陛下：内置的不能删，只有自定义字段可删）。
    /// 内置 = 在 <see cref="All"/> 目录里的那些；其余都是用户自己新增的。
    /// </summary>
    public static bool IsBuiltIn(string? field) => Find(field) != null;

    /// <summary>默认配置的一份深拷贝（每个字段都是新对象，避免改到目录里的默认值）</summary>
    public static List<AccountFieldConfig> CreateDefaults()
    {
        var list = new List<AccountFieldConfig>(All.Count);
        foreach (var f in All)
        {
            list.Add(new AccountFieldConfig
            {
                Field = f.Field,
                ShowInList = f.ShowInList,
                ListWidth = f.ListWidth,
                ListOrder = f.ListOrder,
                ShowInEditor = f.ShowInEditor,
                EditorOrder = f.EditorOrder,
                DisplayName = f.DisplayName,
                ListHeader = f.ListHeader,
                CanShowInList = f.CanShowInList,
                ListWeight = f.ListWeight,
                EditorRowAdvance = f.EditorRowAdvance,
                EditorGapBefore = f.EditorGapBefore,
                Required = f.Required,
            });
        }
        return list;
    }
}
