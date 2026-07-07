using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// 工具箱配置服务
/// </summary>
public class ToolsConfigService
{
    private readonly string _toolsConfigFile;
    private readonly string _pluginsDir;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public List<ToolConfig> Tools { get; private set; } = new();
    
    public ToolsConfigService()
    {
        // 优先使用 AppContext.BaseDirectory（.NET 6+，单文件发布下指向 exe 真实目录）
        // AppDomain.CurrentDomain.BaseDirectory 在单文件发布下可能指向解压临时目录，导致找不到 exe 旁边的 Plugins/tools.json
        string appDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
        }
        _pluginsDir = Path.Combine(appDir, "Plugins");
        _toolsConfigFile = Path.Combine(_pluginsDir, "tools.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        EnsurePluginsDirectory();
        LoadConfig();
    }
    
    private void EnsurePluginsDirectory()
    {
        if (!Directory.Exists(_pluginsDir))
            Directory.CreateDirectory(_pluginsDir);
    }
    
    /// <summary>
    /// 加载工具配置
    /// </summary>
    public void LoadConfig()
    {
        if (!File.Exists(_toolsConfigFile))
        {
            // 创建默认配置
            CreateDefaultConfig();
        }
        
        try
        {
            string json = File.ReadAllText(_toolsConfigFile, Encoding.UTF8);
            // 陛下反馈：tools.json 不支持 // 注释。预处理移除 // 单行注释后再解析。
            // System.Text.Json 不支持 JSON5 / 注释，手动剥引号外的 // 后缀。
            json = StripJsonLineComments(json);
            var config = JsonSerializer.Deserialize<ToolsConfiguration>(json, _jsonOptions);
            Tools = config?.Tools ?? new List<ToolConfig>();
            
            // 只保留启用的工具
            Tools = Tools.Where(t => t.Enabled).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载工具配置失败: {ex.Message}");
            Tools = new List<ToolConfig>();
        }
    }
    
    /// <summary>
    /// 剥除 JSON 文本中的 // 单行注释（仅击输入双引号外部分）。
    /// 解决陛下手动注释一个工具后整个文件解析失败 → 工具都消失的问题（2026-07-07）。
    /// </summary>
    private static string StripJsonLineComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        var sb = new StringBuilder(json.Length);
        bool inString = false;
        bool escape = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (escape) { sb.Append(c); escape = false; continue; }
            if (c == '\\') { sb.Append(c); if (inString) escape = true; continue; }
            if (c == '"') { inString = !inString; sb.Append(c); continue; }
            // 在字符串内 原样走
            if (inString) { sb.Append(c); continue; }
            // // 注释起点 → 跳过到行尾
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                if (i < json.Length) sb.Append('\n');
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 保存工具配置
    /// </summary>
    public void SaveConfig()
    {
        try
        {
            var config = new ToolsConfiguration { Tools = Tools };
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_toolsConfigFile, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存工具配置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 创建默认配置示例
    /// </summary>
    private void CreateDefaultConfig()
    {
        var defaultTools = new ToolsConfiguration
        {
            Tools = new List<ToolConfig>
            {
                new ToolConfig
                {
                    Name = "示例工具",
                    Description = "这是一个示例工具配置",
                    Library = "SampleTool.dll",
                    ClassName = "SampleTool.SamplePlugin",
                    MethodName = "Execute",
                    Enabled = true,
                    Category = "示例"
                }
            }
        };
        
        string json = JsonSerializer.Serialize(defaultTools, _jsonOptions);
        File.WriteAllText(_toolsConfigFile, json, Encoding.UTF8);
    }
    
    /// <summary>
    /// 获取所有已启用的工具DLL路径
    /// </summary>
    public List<string> GetToolLibraries()
    {
        var libraries = new HashSet<string>();
        
        foreach (var tool in Tools)
        {
            if (!string.IsNullOrEmpty(tool.Library))
            {
                string dllPath = Path.Combine(_pluginsDir, tool.Library);
                if (File.Exists(dllPath))
                {
                    libraries.Add(dllPath);
                }
            }
        }
        
        return libraries.ToList();
    }
    
    /// <summary>
    /// 添加工具配置
    /// </summary>
    public void AddTool(ToolConfig tool)
    {
        Tools.Add(tool);
        SaveConfig();
    }
    
    /// <summary>
    /// 移除工具配置
    /// </summary>
    public void RemoveTool(string library, string className)
    {
        Tools.RemoveAll(t => t.Library == library && t.ClassName == className);
        SaveConfig();
    }
}
