using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 自定义工具配置持久化：保存到 DATA\custom-tools.json。
/// 路径约定与 DataService 保持一致（应用程序根目录下的 DATA 文件夹）。
/// </summary>
public class CustomToolStorage
{
    private readonly string _dataFolder;
    private readonly string _filePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CustomToolStorage()
    {
        string appDir = AppContext.BaseDirectory;
        _dataFolder = Path.Combine(appDir, "DATA");
        _filePath = Path.Combine(_dataFolder, "custom-tools.json");
    }

    /// <summary>当前配置文件路径（供 UI 提示用）</summary>
    public string FilePath => _filePath;

    /// <summary>加载所有自定义工具配置；文件不存在返回空列表</summary>
    public List<CustomToolConfig> LoadAll()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new List<CustomToolConfig>();

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<CustomToolConfig>>(json, _jsonOptions)
                   ?? new List<CustomToolConfig>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomToolStorage] LoadAll 失败: {ex.Message}");
            return new List<CustomToolConfig>();
        }
    }

    /// <summary>保存（覆盖式），文件不存在则创建</summary>
    public void SaveAll(List<CustomToolConfig> configs)
    {
        try
        {
            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            string json = JsonSerializer.Serialize(configs, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存自定义工具配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>添加或更新（按 Id 匹配）；返回更新后的完整列表</summary>
    public List<CustomToolConfig> Upsert(CustomToolConfig config)
    {
        var list = LoadAll();
        int idx = list.FindIndex(c => c.Id == config.Id);
        if (idx >= 0) list[idx] = config;
        else list.Add(config);
        SaveAll(list);
        return list;
    }

    /// <summary>按 Id 删除；返回更新后的完整列表</summary>
    public List<CustomToolConfig> Delete(string id)
    {
        var list = LoadAll();
        list.RemoveAll(c => c.Id == id);
        SaveAll(list);
        return list;
    }
}