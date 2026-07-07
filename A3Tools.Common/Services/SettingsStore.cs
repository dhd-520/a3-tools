using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace A3Tools.Common.Services;

/// <summary>
/// 独立的 settings.json 读写工具（让 Plugin DLL 也能读写主程序配置）。
///
/// 为什么不引用 A3Tools.Models.AppSettings？
/// - A3Tools.Common 是被 Plugins 引用的底层库，不能反向引用 A3Tools 项目
/// - 直接用 Dictionary 读写 raw JSON 最简单，字段名由调用方控制
///
/// 文件路径：{AppDir}/DATA/settings.json（与 A3Tools.DataService 一致）
/// </summary>
public static class SettingsStore
{
    private static string GetSettingsFile()
    {
        var appDir = AppContext.BaseDirectory;
        var dataDir = Path.Combine(appDir, "DATA");
        return Path.Combine(dataDir, "settings.json");
    }

    /// <summary>读整个 settings.json 为 Dictionary。失败或不存在 → 返空 dict。</summary>
    /// <remarks>
    /// JSON 里是 camelCase key（如 sqlQueryFormWidth）→ 转回 PascalCase 让 Read/ReadInt/ReadBool 调用方用属性名检索。
    /// </remarks>
    public static Dictionary<string, object?> LoadAll()
    {
        var file = GetSettingsFile();
        if (!File.Exists(file))
            return new Dictionary<string, object?>();

        try
        {
            var json = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, object?>();

            var raw = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (raw == null) return new Dictionary<string, object?>();

            // key 转 PascalCase
            var result = new Dictionary<string, object?>(raw.Count);
            foreach (var kv in raw)
                result[ToPascalCase(kv.Key)] = kv.Value;
            return result;
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>camelCase → PascalCase（首字母转大写）。</summary>
    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s) || !char.IsLower(s[0])) return s;
        if (s.Length == 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>保存整个 Dictionary 到 settings.json（覆盖）。</summary>
    /// <remarks>
    /// 【关键】把 Dictionary 的 key 从 PascalCase 转成 camelCase 后再写。
    /// 原因：A3Tools.DataService 写 settings.json 时用 JsonNamingPolicy.CamelCase，
    /// AppSettings 反序列化走 camelCase 映射。如果 Plugin 写 PascalCase，
    /// A3Tools 读 settings.json 时找不到对应字段 → 字段恢复成默认值 → 状态丢失。
    /// </remarks>
    public static void SaveAll(Dictionary<string, object?> data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var file = GetSettingsFile();
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // 把 PascalCase key 转成 camelCase（让 A3Tools 主程序能正确反序列化）
        var camelData = new Dictionary<string, object?>(data.Count);
        foreach (var kv in data)
            camelData[ToCamelCase(kv.Key)] = kv.Value;

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            // 避免数字 0 / false 被吞掉（不过要写 null 才被忽略）
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        };
        var json = JsonSerializer.Serialize(camelData, opts);
        File.WriteAllText(file, json);
    }

    /// <summary>PascalCase → camelCase（首字母转小写，其它不动）。</summary>
    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0])) return s;
        if (s.Length == 1) return s.ToLowerInvariant();
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>读指定字段（int）。字段不存在或不是 int → 返 defaultValue。</summary>
    public static int ReadInt(string key, int defaultValue = 0)
    {
        var dict = LoadAll();
        if (!dict.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        if (raw is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)) return v;
        }
        return defaultValue;
    }

    /// <summary>读指定字段（bool）。</summary>
    public static bool ReadBool(string key, bool defaultValue = false)
    {
        var dict = LoadAll();
        if (!dict.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        if (raw is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    /// <summary>写指定字段。线程安全（文件级锁）。</summary>
    public static void Write(string key, object? value)
    {
        lock (_fileLock)
        {
            var dict = LoadAll();
            dict[key] = value;
            SaveAll(dict);
        }
    }

    /// <summary>批量修改：用 loader 读 dict → 修改 → save。线程安全。</summary>
    public static void Update(Action<Dictionary<string, object?>> mutate)
    {
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));

        lock (_fileLock)
        {
            var dict = LoadAll();
            mutate(dict);
            SaveAll(dict);
        }
    }

    private static readonly object _fileLock = new();
}