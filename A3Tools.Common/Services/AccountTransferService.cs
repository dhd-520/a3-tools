using System.Text.Json;
using System.Text.Json.Serialization;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 账套导出/导入（跨机器迁移）服务 —— 纯逻辑，不依赖 WinForms，可直接单测。
///
/// ★ 为什么要单独做一套格式（2026-10-30 陛下：导出全部账套 → 换台电脑导入）：
///
///   1. **直接拷 accounts.json 过去是行不通的**。
///      账套密码用 <see cref="EncryptionService"/> 加密，而它的密钥是
///      SHA256(机器名 + 用户名 + "A3Tools-2026") 派生的 —— **跟机器绑定**。
///      换台电脑后同一份密文根本解不开（Decrypt 直接返回空串），
///      表现就是「账套在、密码全没了」，而且没有任何报错。
///      所以跨机迁移必须用一份**独立格式**，密码以**明文**存放。
///
///   2. **老格式（A3 客户端导出的 XML，&lt;TbLYBillSet&gt;）字段太少**：
///      只有 NAME / ZTADDRESS / ZTPWD / SQLADDRESS / USERCODE / USERPWD，
///      备用地址、远程方式、备注、数据库名称、自定义字段全都丢了。
///      它适合「从 A3 客户端搬一批基础账套过来」，不适合「整机迁移」。
///
/// 导出格式（JSON，扁平，和知识库导出一个套路）：
/// {
///   "$type": "a3accounts-v1",              ← 判别字段（导入时靠它分流）
///   "exportedAt": "2026-10-30T10:45:00",
///   "exportedFromVersion": "2.6.15",
///   "sourceMachine": "LAPTOP-KT54K35H",
///   "accounts": [ { ...Account 全字段，密码明文... } ],
///   "fieldConfig": [ { ...账套字段显示配置... } ]   ← 可选，迁移自定义字段用
/// }
///
/// ⚠ 安全提醒：导出文件里是**明文密码**，等同于把所有账套密码写在纸上。
///   因此导出入口只放在 Root 模式下，UI 上还会再确认一次。
/// </summary>
public static class AccountTransferService
{
    /// <summary>导出文件的判别字段名（与知识库导出同一套路）</summary>
    public const string DiscriminatorField = "$type";

    /// <summary>导出文件的类型标识</summary>
    public const string ExportType = "a3accounts-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 枚举存字符串（"Direct"/"Http"）而不是数字，跨版本更耐看、也更耐改
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>导出文件顶层结构</summary>
    public class TransferFile
    {
        [JsonPropertyName("$type")] public string Type { get; set; } = ExportType;
        [JsonPropertyName("exportedAt")] public string ExportedAt { get; set; } = "";
        [JsonPropertyName("exportedFromVersion")] public string ExportedFromVersion { get; set; } = "";
        [JsonPropertyName("sourceMachine")] public string SourceMachine { get; set; } = "";
        [JsonPropertyName("accounts")] public List<Account> Accounts { get; set; } = new();
        [JsonPropertyName("fieldConfig")] public List<AccountFieldConfig>? FieldConfig { get; set; }
    }

    /// <summary>
    /// 生成导出 JSON 文本。
    /// </summary>
    /// <param name="accounts">账套列表（**密码必须已是明文**）</param>
    /// <param name="fieldConfig">账套字段显示配置（可空；带上它新机器才有自定义字段的名字）</param>
    /// <param name="appVersion">导出方版本号</param>
    /// <param name="sourceMachine">导出方机器名（可空）</param>
    public static string BuildJson(
        IEnumerable<Account> accounts,
        IEnumerable<AccountFieldConfig>? fieldConfig,
        string? appVersion,
        string? sourceMachine)
    {
        var file = new TransferFile
        {
            Type = ExportType,
            ExportedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            ExportedFromVersion = appVersion ?? "",
            SourceMachine = sourceMachine ?? "",
            Accounts = accounts?.Where(a => a != null).ToList() ?? new List<Account>(),
            FieldConfig = fieldConfig?.ToList()
        };

        // 密码必须是明文 —— 万一调用方传了本机密文，这里直接拒绝（否则导出去的是废数据）
        foreach (var a in file.Accounts)
        {
            if (EncryptionService.IsEncrypted(a.DbPassword))
                throw new InvalidOperationException(
                    $"账套「{a.Name}」的 DB 密码还是本机密文，无法跨机导出。请先用 LoadAndDecryptAccounts() 取明文。");
            if (EncryptionService.IsEncrypted(a.RemotePassword))
                throw new InvalidOperationException(
                    $"账套「{a.Name}」的远程密码还是本机密文，无法跨机导出。");
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    /// <summary>
    /// 判断一段文本是不是本工具导出的账套文件（靠 $type 判别）。
    /// 故意做得很宽松：只要 JSON 里带 $type=a3accounts-v1 就算。
    /// </summary>
    public static bool LooksLikeTransferFile(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty(DiscriminatorField, out var t)) return false;
            return t.ValueKind == JsonValueKind.String
                && string.Equals(t.GetString(), ExportType, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析本工具导出的账套文件。
    /// </summary>
    /// <exception cref="InvalidDataException">文件不是本工具导出的格式，或内容已损坏</exception>
    public static TransferFile Parse(string content)
    {
        TransferFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TransferFile>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"账套文件格式损坏，无法解析：{ex.Message}", ex);
        }

        if (file == null) throw new InvalidDataException("账套文件内容为空。");
        if (!string.Equals(file.Type, ExportType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"不是本工具导出的账套文件（$type={file.Type}）。");

        file.Accounts ??= new List<Account>();
        foreach (var a in file.Accounts)
        {
            if (a == null) continue;
            a.CustomFields ??= new Dictionary<string, string>();
        }

        return file;
    }

    /// <summary>导入结果的合并计划（纯逻辑，方便单测）</summary>
    public class MergePlan
    {
        /// <summary>本地没有、可以直接新增的账套</summary>
        public List<Account> ToAdd { get; } = new();

        /// <summary>代码与本地重复、将被覆盖的账套</summary>
        public List<Account> ToOverwrite { get; } = new();

        /// <summary>代码为空、需要自动分配代码的账套</summary>
        public List<Account> ToAssignCode { get; } = new();

        /// <summary>合并后本地账套的最终数量（待分配代码的那批算在 ToAdd 里）</summary>
        public int TotalAfter => ToAdd.Count + ToOverwrite.Count;

        /// <summary>出错信息</summary>
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// 把导入的账套和本地账套合并，算出「新增 / 覆盖 / 待分配代码」三拨。
    ///
    /// 合并策略（按代码匹配）：
    ///   · 代码在本地不存在 → 新增；
    ///   · 代码在本地已存在 → **覆盖**（导入文件里的那份赢）。
    ///     这正是「整机迁移」想要的语义：另一台电脑上代码是 0001/0002/0003，
    ///     本机也恰好有 0001，那就让导入的 0001 顶掉本机的 0001。
    ///   · 代码为空 → 交给调用方自动分配。
    /// </summary>
    public static MergePlan PlanMerge(IEnumerable<Account>? local, IEnumerable<Account>? incoming)
    {
        var plan = new MergePlan();
        var localList = local?.Where(a => a != null).ToList() ?? new List<Account>();
        var localCodes = new HashSet<string>(
            localList.Select(a => (a.Code ?? "").Trim()),
            StringComparer.OrdinalIgnoreCase);

        // ⚠ 导入文件内部出现相同代码时，**后者顶掉前者**（同在 ToAdd 或同在 ToOverwrite 里原地替换）。
        //   注意：不能把导入的代码塞进 localCodes —— 那会让第二份被误判成「覆盖本地」。
        var addIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ovwIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in incoming?.Where(x => x != null) ?? Enumerable.Empty<Account>())
        {
            string code = (a.Code ?? "").Trim();
            if (code.Length == 0)
            {
                plan.ToAssignCode.Add(a);
                continue;
            }

            a.Code = code;

            if (localCodes.Contains(code))
            {
                if (ovwIndex.TryGetValue(code, out int oi))
                    plan.ToOverwrite[oi] = a;          // 同代码 → 后者顶掉前者
                else
                {
                    ovwIndex[code] = plan.ToOverwrite.Count;
                    plan.ToOverwrite.Add(a);
                }
            }
            else
            {
                if (addIndex.TryGetValue(code, out int ai))
                    plan.ToAdd[ai] = a;                // 同代码 → 后者顶掉前者
                else
                {
                    addIndex[code] = plan.ToAdd.Count;
                    plan.ToAdd.Add(a);
                }
            }
        }

        return plan;
    }

    /// <summary>
    /// 把导入文件里的自定义字段配置合并进本地配置。
    ///
    /// 规则：**只补本地没有的自定义字段，不删、不改本地已有的**。
    ///   · 自定义字段的键是 Cust_ + Ticks，跨机器是同一个键 → 值能对上；
    ///   · 内置字段（Code/Name/...）的显示配置属于「本机偏好」，不该被别的机器覆盖。
    /// </summary>
    public static List<AccountFieldConfig> MergeFieldConfig(
        IEnumerable<AccountFieldConfig>? local,
        IEnumerable<AccountFieldConfig>? incoming)
    {
        var result = (local?.Where(c => c != null).ToList() ?? new List<AccountFieldConfig>());
        var existing = new HashSet<string>(
            result.Select(c => (c.Field ?? "").Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var c in incoming?.Where(x => x != null) ?? Enumerable.Empty<AccountFieldConfig>())
        {
            string field = (c.Field ?? "").Trim();
            if (field.Length == 0) continue;
            // 只有带名字的自定义字段才算数（陌生字段直接丢，见 AccountFieldResolver 的护栏）
            if (string.IsNullOrWhiteSpace(c.CustomName)) continue;
            if (!existing.Add(field)) continue;

            result.Add(new AccountFieldConfig
            {
                Field = field,
                CustomName = c.CustomName,
                ShowInList = c.ShowInList,
                ListWidth = c.ListWidth,
                ListOrder = c.ListOrder,
                ShowInEditor = c.ShowInEditor,
                EditorOrder = c.EditorOrder
            });
        }

        return result;
    }
}
