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

    /// <summary>
    /// 上一次读取是否失败（配置损坏等）。
    /// ⚠️ 非 null 时**禁止写回**，否则会用「读不出来的空列表」覆盖掉用户原有配置。
    /// UI 应据此提示用户。
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>加载所有自定义工具配置；文件不存在返回空列表</summary>
    public List<CustomToolConfig> LoadAll()
    {
        LastError = null;
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
            // ★ 配置文件损坏时：先把原文件备份出来，绝不能静默丢数据。
            //   （否则 UI 上只是「工具不见了」，用户再存一个工具就会把其余配置全部覆盖掉）
            string backup = Path.Combine(_dataFolder, $"custom-tools.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try
            {
                if (File.Exists(_filePath)) File.Copy(_filePath, backup, true);
            }
            catch { /* 备份失败不掩盖原始错误 */ }

            LastError = $"{ex.Message}（原文件已备份到 {backup}）";
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
            WriteAllTextAtomic(_filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存自定义工具配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 原子写：先写临时文件 → 替换目标。
    /// 直接 <see cref="File.WriteAllText(string,string)"/> 在断电/崩溃时可能留下半截文件，
    /// 下次启动就变成「读不出来」→ 配置全丢。写临时文件再替换可避免。
    /// </summary>
    private static void WriteAllTextAtomic(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);      // .NET 默认 UTF-8 无 BOM（与原行为一致）
        try
        {
            if (File.Exists(path))
            {
                // 留一份上一版好数据，方便手滑改错后回滚
                try { File.Copy(path, path + ".bak", true); } catch { /* 备份失败不阻断保存 */ }
                File.Move(tmp, path, true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
        }
    }

    /// <summary>添加或更新（按 Id 匹配）；返回更新后的完整列表</summary>
    public List<CustomToolConfig> Upsert(CustomToolConfig config)
    {
        var list = LoadAll();

        // ★ 读失败时绝不能拿空列表去覆盖原文件（否则用户原有配置会被静默清空）
        if (LastError != null)
            throw new InvalidOperationException(
                $"读取自定义工具配置失败，已阻止保存以免覆盖原有数据。\n{LastError}");

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

        // ★ 同上：读失败时禁止写入
        if (LastError != null)
            throw new InvalidOperationException(
                $"读取自定义工具配置失败，已阻止删除以免覆盖原有数据。\n{LastError}");

        list.RemoveAll(c => c.Id == id);
        SaveAll(list);
        return list;
    }
}