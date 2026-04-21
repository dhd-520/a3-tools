using System.IO;
using System.Reflection;
using A3Tools.Models;
using A3Tools.Plugins;

namespace A3Tools.Services;

/// <summary>
/// 工具执行服务
/// </summary>
public class ToolExecutorService
{
    private readonly string _pluginsDir;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly List<LoadedTool> _loadedTools = new();
    
    public class LoadedTool
    {
        public ToolConfig Config { get; set; } = null!;
        public Type? ToolType { get; set; }
        public object? Instance { get; set; }
    }
    
    public IReadOnlyList<LoadedTool> Tools => _loadedTools.AsReadOnly();
    
    public ToolExecutorService()
    {
        _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        EnsurePluginsDirectory();
    }
    
    private void EnsurePluginsDirectory()
    {
        if (!Directory.Exists(_pluginsDir))
            Directory.CreateDirectory(_pluginsDir);
    }
    
    /// <summary>
    /// 加载所有工具
    /// </summary>
    public void LoadTools(ToolsConfigService configService, IToolContext context)
    {
        _loadedTools.Clear();
        _loadedAssemblies.Clear();
        
        foreach (var config in configService.Tools)
        {
            var tool = TryLoadTool(config);
            if (tool != null)
            {
                _loadedTools.Add(tool);
            }
        }
    }
    
    private LoadedTool? TryLoadTool(ToolConfig config)
    {
        if (string.IsNullOrEmpty(config.Library) || 
            string.IsNullOrEmpty(config.ClassName) || 
            string.IsNullOrEmpty(config.MethodName))
        {
            return null;
        }
        
        string dllPath = Path.Combine(_pluginsDir, config.Library);
        if (!File.Exists(dllPath))
        {
            System.Diagnostics.Debug.WriteLine($"工具DLL不存在: {dllPath}");
            return null;
        }
        
        try
        {
            Assembly assembly;
            if (!_loadedAssemblies.TryGetValue(dllPath, out assembly!))
            {
                assembly = Assembly.LoadFrom(dllPath);
                _loadedAssemblies[dllPath] = assembly;
            }
            
            var type = assembly.GetType(config.ClassName);
            if (type == null)
            {
                System.Diagnostics.Debug.WriteLine($"找不到类: {config.ClassName}");
                return null;
            }
            
            // 创建实例
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch
            {
                // 如果无参数构造函数失败，尝试其他方式
            }
            
            return new LoadedTool
            {
                Config = config,
                ToolType = type,
                Instance = instance
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载工具失败: {config.Name} - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 执行指定工具
    /// </summary>
    public bool ExecuteTool(LoadedTool tool, Account? account, IToolContext context)
    {
        if (tool.ToolType == null)
        {
            context.ShowError($"工具 {tool.Config.Name} 未正确加载");
            return false;
        }
        
        try
        {
            var method = tool.ToolType.GetMethod(tool.Config.MethodName);
            if (method == null)
            {
                context.ShowError($"找不到方法: {tool.Config.MethodName}");
                return false;
            }
            
            // 调用方法
            // 支持多种签名
            var parameters = method.GetParameters();
            
            if (parameters.Length == 0)
            {
                method.Invoke(tool.Instance, null);
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Account))
            {
                method.Invoke(tool.Instance, new object[] { account! });
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IToolContext))
            {
                method.Invoke(tool.Instance, new object[] { context });
            }
            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Account) 
                     && parameters[1].ParameterType == typeof(IToolContext))
            {
                method.Invoke(tool.Instance, new object[] { account!, context });
            }
            else
            {
                // 默认：传递 account
                method.Invoke(tool.Instance, new object[] { account! });
            }
            
            return true;
        }
        catch (Exception ex)
        {
            context.ShowError($"执行工具失败: {ex.Message}");
            return false;
        }
    }
}
