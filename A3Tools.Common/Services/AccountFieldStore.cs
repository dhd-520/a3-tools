using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 账套字段显示配置的持久化：DATA\account-fields.json。
///
/// 为什么不塞进 settings.json？
///   settings.json 是 AppSettings 的序列化产物（DataService 读写），塞一个字段数组进去
///   会和 AppSettings 的强类型模型打架（多字段/字典结构塞不进单一模型），
///   而且 SettingsStore 是给插件用的 Dictionary 通道，语义也对不上。
///   单独一个文件更干净：和 custom-tools.json 一个套路。
///
/// 文件格式（camelCase，与仓库其它 JSON 一致）：
/// {
///   "fields": [
///     { "field": "Code", "showInList": true, "listWidth": 120,
///       "listOrder": 0, "showInEditor": true, "editorOrder": 0 }
///   ]
/// }
/// </summary>
public class AccountFieldStore
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AccountFieldStore()
    {
        string appDir = AppContext.BaseDirectory;
        _filePath = Path.Combine(appDir, "DATA", "account-fields.json");
    }

    /// <summary>配置文件路径（设置界面提示用）</summary>
    public string FilePath => _filePath;

    /// <summary>
    /// 读取配置并归一化（缺字段自动补默认、非法值自动纠正）。
    /// 文件不存在/损坏 → 返回默认配置（等价于「用户没配置过」）。
    /// </summary>
    public List<AccountFieldConfig> Load()
    {
        List<AccountFieldConfig>? raw = null;
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                var doc = JsonSerializer.Deserialize<AccountFieldSettingsFile>(json, _jsonOptions);
                raw = doc?.Fields;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountFieldStore] 读取失败，回退默认: {ex.Message}");
            raw = null;
        }

        return AccountFieldResolver.Normalize(raw);
    }

    /// <summary>保存配置（覆盖式）。保存前先归一化，保证落盘的一定是合法值。</summary>
    public void Save(List<AccountFieldConfig> configs)
    {
        var normalized = AccountFieldResolver.Normalize(configs);

        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var payload = new AccountFieldSettingsFile { Fields = normalized };
        string json = JsonSerializer.Serialize(payload, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>恢复默认（删除配置文件，等价于「从来没配过」）</summary>
    public void Reset()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountFieldStore] 删除配置文件失败: {ex.Message}");
        }
    }

    /// <summary>配置文件根对象</summary>
    private class AccountFieldSettingsFile
    {
        public List<AccountFieldConfig> Fields { get; set; } = new List<AccountFieldConfig>();
    }
}
