using System.IO;
using System.Reflection;
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
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
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
            string json = File.ReadAllText(_toolsConfigFile);
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
    /// 保存工具配置
    /// </summary>
    public void SaveConfig()
    {
        try
        {
            var config = new ToolsConfiguration { Tools = Tools };
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_toolsConfigFile, json);
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
        File.WriteAllText(_toolsConfigFile, json);
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
