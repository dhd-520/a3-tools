using System.IO;
using System.Text;
using System.Text.Json;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Services;

/// <summary>
/// 数据服务 - 负责账套数据的持久化
/// </summary>
public class DataService
{
    private readonly string _dataFolder;
    private readonly string _accountsFile;
    private readonly string _settingsFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataService()
    {
        // DATA文件夹在应用程序所在目录
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        _dataFolder = Path.Combine(appDir, "DATA");
        _accountsFile = Path.Combine(_dataFolder, "accounts.json");
        _settingsFile = Path.Combine(_dataFolder, "settings.json");

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
    /// 加载所有账套
    /// </summary>
    public List<Account> LoadAccounts()
    {
        if (!File.Exists(_accountsFile))
            return new List<Account>();

        try
        {
            string json = File.ReadAllText(_accountsFile);
            return JsonSerializer.Deserialize<List<Account>>(json, _jsonOptions) ?? new List<Account>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载账套失败: {ex.Message}");
            return new List<Account>();
        }
    }

    /// <summary>
    /// 更新所有账套的拼音字段并保存
    /// </summary>
    public void UpdateAllPinyin()
    {
        var accounts = LoadAccounts();
        bool hasUpdate = false;
        foreach (var acc in accounts)
        {
            if (string.IsNullOrEmpty(acc.Pinyin) && !string.IsNullOrEmpty(acc.Name))
            {
                acc.Pinyin = PinyinHelper.GetPinyinInitial(acc.Name);
                hasUpdate = true;
            }
        }
        if (hasUpdate)
            SaveAccounts(accounts);
    }

    /// <summary>
    /// 加载并解密所有账套（供UI使用）
    /// </summary>
    public List<Account> LoadAndDecryptAccounts()
    {
        var accounts = LoadAccounts();
        foreach (var account in accounts)
        {
            account.DbPassword = DecryptIfEncrypted(account.DbPassword ?? "");
            account.RemotePassword = DecryptIfEncrypted(account.RemotePassword ?? "");
        }
        return accounts;
    }

    /// <summary>
    /// 保存所有账套（自动加密密码）
    /// </summary>
    public void SaveAccounts(List<Account> accounts)
    {
        // 加密所有密码后再保存（已加密的不再重复加密）
        foreach (var account in accounts)
        {
            if (!string.IsNullOrEmpty(account.DbPassword) && !IsEncrypted(account.DbPassword))
                account.DbPassword = EncryptionService.Encrypt(account.DbPassword);
            if (!string.IsNullOrEmpty(account.RemotePassword) && !IsEncrypted(account.RemotePassword))
                account.RemotePassword = EncryptionService.Encrypt(account.RemotePassword);
        }
        string json = JsonSerializer.Serialize(accounts, _jsonOptions);
        File.WriteAllText(_accountsFile, json);
    }

    /// <summary>
    /// 判断字符串是否已加密。
    /// 不能只按 Base64 判断：例如 4 位数字 "1234" 也是合法 Base64，
    /// 会被误判为已加密，导致保存明文、下次加载解密失败变空。
    /// </summary>
    private bool IsEncrypted(string text)
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
    /// 用于兼容旧版本误保存的 4 位 Base64-like 明文密码。
    /// </summary>
    private string DecryptIfEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return IsEncrypted(text) ? EncryptionService.Decrypt(text) : text;
    }

    /// <summary>
    /// 添加账套
    /// </summary>
    public void AddAccount(Account account)
    {
        var accounts = LoadAccounts();
        accounts.Add(account);
        SaveAccounts(accounts);
    }

    /// <summary>
    /// 更新账套
    /// </summary>
    public void UpdateAccount(string code, Account account)
    {
        var accounts = LoadAccounts();
        int index = accounts.FindIndex(a => a.Code == code);
        if (index >= 0)
        {
            // 保留原加密密码（如果新密码为空）
            if (string.IsNullOrEmpty(account.DbPassword))
                account.DbPassword = accounts[index].DbPassword;
            if (string.IsNullOrEmpty(account.RemotePassword))
                account.RemotePassword = accounts[index].RemotePassword;
            // 保留账套用户名（如果新用户名为空）
            if (string.IsNullOrEmpty(account.ServerUsername))
                account.ServerUsername = accounts[index].ServerUsername;

            accounts[index] = account;
            SaveAccounts(accounts);
        }
    }

    /// <summary>
    /// 删除账套，并重排后续编码保持连贯
    /// </summary>
    public void DeleteAccount(string code)
    {
        var accounts = LoadAccounts();
        
        // 先找到被删除账套的编码数值
        int deletedNum = 0;
        Account? toDelete = null;
        foreach (var acc in accounts)
        {
            if (acc.Code == code)
            {
                toDelete = acc;
                if (int.TryParse(acc.Code.TrimStart('0'), out int num))
                    deletedNum = num;
                break;
            }
        }
        
        if (toDelete == null) return; // 没找到
        
        // 先移除要删除的账套
        accounts.Remove(toDelete);
        
        // 重排后续编码：大于被删除编码的减1
        foreach (var acc in accounts)
        {
            if (int.TryParse(acc.Code.TrimStart('0'), out int num) && num > deletedNum)
            {
                int newNum = num - 1;
                acc.Code = newNum.ToString(new string('0', acc.Code.Length));
            }
        }
        
        SaveAccounts(accounts);
    }

    /// <summary>
    /// 根据代码查找账套
    /// </summary>
    public Account? FindAccount(string code)
    {
        var accounts = LoadAndDecryptAccounts();
        return accounts.FirstOrDefault(a => a.Code == code);
    }

    /// <summary>
    /// 搜索账套
    /// </summary>
    public List<Account> SearchAccounts(string keyword)
    {
        var allAccounts = LoadAndDecryptAccounts();
        if (string.IsNullOrWhiteSpace(keyword))
            return allAccounts;

        keyword = keyword.ToLower();
        return allAccounts.Where(a =>
            a.Code.ToLower().Contains(keyword) ||
            a.Name.ToLower().Contains(keyword) ||
            a.Server.ToLower().Contains(keyword) ||
            a.Database.ToLower().Contains(keyword)
        ).ToList();
    }

    /// <summary>
    /// 解密账套密码（仅内存使用）
    /// </summary>
    public Account DecryptAccount(Account account)
    {
        account.DbPasswordDecrypted = DecryptIfEncrypted(account.DbPassword);
        account.RemotePasswordDecrypted = DecryptIfEncrypted(account.RemotePassword);
        return account;
    }

    /// <summary>
    /// 加载设置（自动解密 DevToolsPassword 等加密字段）
    /// </summary>
    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsFile))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(_settingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();

            // 解密集成开发工具密码；如果是旧版本误保存的明文，直接保留并在下次保存时重新加密。
            if (!string.IsNullOrEmpty(settings.DevToolsPassword))
                settings.DevToolsPassword = DecryptIfEncrypted(settings.DevToolsPassword);

            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// 检查设置文件是否存在
    /// </summary>
    public bool HasSettings()
    {
        return File.Exists(_settingsFile);
    }

    /// <summary>
    /// 保存设置（自动加密 DevToolsPassword 等加密字段）
    /// </summary>
    public void SaveSettings(AppSettings settings)
    {
        // 加密集成开发工具密码（避免重复加密）
        if (!string.IsNullOrEmpty(settings.DevToolsPassword) && !IsEncrypted(settings.DevToolsPassword))
            settings.DevToolsPassword = EncryptionService.Encrypt(settings.DevToolsPassword);

        string json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsFile, json);
    }
}
