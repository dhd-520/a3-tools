using System.IO;
using System.Text.Json;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// AI 客服配置服务
/// <para>职责：</para>
/// <list type="bullet">
///   <item>加载/保存 AI 厂商配置（ApiKey AES 加密落盘）</item>
///   <item>加载/保存 AI 全局配置（默认厂商、存储策略、历史保留天数等）</item>
///   <item>提供厂商增删改查、切换默认等高频操作</item>
/// </list>
/// <para>★ 2026-08-26 陛下要求：跟 DataService 风格保持一致，ApiKey 复用 EncryptionService</para>
/// </summary>
public class AiConfigService
{
    private readonly string _dataFolder;
    private readonly string _configFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public AiConfigService()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        _dataFolder = Path.Combine(appDir, "DATA");
        _configFile = Path.Combine(_dataFolder, "ai_chat_config.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDataFolder();
    }

    private void EnsureDataFolder()
    {
        if (!Directory.Exists(_dataFolder))
            Directory.CreateDirectory(_dataFolder);
    }

    /// <summary>
    /// 加载 AI 全局配置（不存在则返回默认配置 + 空厂商列表）
    /// </summary>
    public AiChatConfig LoadConfig()
    {
        if (!File.Exists(_configFile))
            return new AiChatConfig();

        try
        {
            string json = File.ReadAllText(_configFile);
            var config = JsonSerializer.Deserialize<AiChatConfig>(json, _jsonOptions) ?? new AiChatConfig();

            // 解密所有厂商的 ApiKey
            foreach (var p in config.Providers)
            {
                p.ApiKey = DecryptIfEncrypted(p.ApiKey);
            }

            return config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AiConfigService] 加载 AI 配置失败: {ex.Message}");
            return new AiChatConfig();
        }
    }

    /// <summary>
    /// 保存 AI 全局配置（自动加密 ApiKey）
    /// </summary>
    public void SaveConfig(AiChatConfig config)
    {
        // 加密所有厂商 ApiKey（避免重复加密）
        foreach (var p in config.Providers)
        {
            if (!string.IsNullOrEmpty(p.ApiKey) && !IsEncrypted(p.ApiKey))
                p.ApiKey = EncryptionService.Encrypt(p.ApiKey);
        }

        string json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(_configFile, json);
    }

    /// <summary>
    /// 添加厂商（如果还没有任何厂商，自动设为默认）
    /// </summary>
    public void AddProvider(AiProviderConfig provider)
    {
        var config = LoadConfig();

        // 防重名：相同 Name 已存在则覆盖（陛下手动改的）
        var existing = config.Providers.FirstOrDefault(p => p.Name == provider.Name);
        if (existing != null)
        {
            existing.ApiUrl = provider.ApiUrl;
            existing.ApiKey = provider.ApiKey;
            existing.Model = provider.Model;
            existing.Enabled = provider.Enabled;
            existing.Type = provider.Type;
            existing.Remark = provider.Remark;
        }
        else
        {
            // 第一个厂商自动设为默认
            if (config.Providers.Count == 0)
                provider.IsDefault = true;

            config.Providers.Add(provider);
        }

        SaveConfig(config);
    }

    /// <summary>
    /// 按 ID 删除厂商
    /// </summary>
    public bool DeleteProvider(string providerId)
    {
        var config = LoadConfig();
        var p = config.Providers.FirstOrDefault(x => x.Id == providerId);
        if (p == null) return false;

        bool wasDefault = p.IsDefault;
        config.Providers.Remove(p);

        // 如果删的是默认厂商，把第一个启用的设为新默认
        if (wasDefault)
        {
            var newDefault = config.Providers.FirstOrDefault(x => x.Enabled);
            if (newDefault != null)
            {
                newDefault.IsDefault = true;
                config.DefaultProviderId = newDefault.Id;
            }
            else
            {
                config.DefaultProviderId = string.Empty;
            }
        }

        SaveConfig(config);
        return true;
    }

    /// <summary>
    /// 设置某个厂商为默认
    /// </summary>
    public void SetDefaultProvider(string providerId)
    {
        var config = LoadConfig();
        foreach (var p in config.Providers)
            p.IsDefault = (p.Id == providerId);

        config.DefaultProviderId = providerId;
        SaveConfig(config);
    }

    /// <summary>
    /// 获取默认厂商（优先用 DefaultProviderId，否则取第一个启用的）
    /// </summary>
    public AiProviderConfig? GetDefaultProvider()
    {
        var config = LoadConfig();
        if (config.Providers.Count == 0) return null;

        if (!string.IsNullOrEmpty(config.DefaultProviderId))
        {
            var p = config.Providers.FirstOrDefault(x => x.Id == config.DefaultProviderId);
            if (p != null) return p;
        }

        return config.Providers.FirstOrDefault(x => x.Enabled) ?? config.Providers.First();
    }

    /// <summary>
    /// 判断字符串是否已加密（沿用 DataService 同样的安全判定）
    /// </summary>
    private static bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(text);

            // AES-CBC + PKCS7 密文长度至少 16 字节，且必须是 16 的整数倍。
            if (cipherBytes.Length < 16 || cipherBytes.Length % 16 != 0)
                return false;

            // 能用当前机器密钥成功解密，才认为是本程序加密过的密文。
            return !string.IsNullOrEmpty(EncryptionService.Decrypt(text));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 仅当字段确认为加密密文时才解密；否则按明文兼容处理。
    /// </summary>
    private static string DecryptIfEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return IsEncrypted(text) ? EncryptionService.Decrypt(text) : text;
    }
}
