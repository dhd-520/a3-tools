using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL Server 数据库对象 Schema 缓存 + IntelliSense 数据源：
/// - 表 (TABLE)
/// - 视图 (VIEW)
/// - 表值函数 (IF = Inline TVF / TF = Multi-statement TVF)
/// - 标量函数 (FN = Scalar Function)
/// - 每个对象的列名（用于后续扩展列名联想）
///
/// 缓存生命周期：进程内。按 (server, database) 维度隔离。
/// - 后台预热（不卡 UI）
/// - 切库时自动 invalidate 旧缓存（即使新库不存在也清空）
/// - 用户切回原库 → 命中缓存 → 0 IO
///
/// 并发：每个 (server, database) 键只允许一个加载在跑（其他线程等结果）
/// </summary>
public static class SqlObjectSchemaCache
{
    public enum ObjectKind
    {
        Table,                       // U
        View,                        // V
        TableValuedFunction,         // IF / TF
        ScalarFunction,              // FN
        StoredProcedure,             // P
        Trigger                      // TR
    }

    /// <summary>将 ObjectKind 拼成 SQL Server sys.objects.type 列表</summary>
    public static string KindToTypeChar(ObjectKind kind) => kind switch
    {
        ObjectKind.Table => "U",
        ObjectKind.View => "V",
        ObjectKind.TableValuedFunction => "IF,TF",
        ObjectKind.ScalarFunction => "FN",
        ObjectKind.StoredProcedure => "P",
        ObjectKind.Trigger => "TR",
        _ => "U"
    };

    public record DbObject(string SchemaName, string Name, ObjectKind Kind, string? Columns = null);

    /// <summary>缓存条目（databaseName -> objects + 时间戳）</summary>
    private record CacheEntry(string Server, string Database, List<DbObject> Objects, DateTime LoadedAt);

    /// <summary>缓存本体（线程安全）</summary>
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>正在加载的 key（防止并发加载同一库）</summary>
    private static readonly ConcurrentDictionary<string, Task<List<DbObject>>> _loadingTasks = new();

    // ============================================
    // 公共 API
    // ============================================

    /// <summary>
    /// 根据 connectionString 异步加载/获取缓存。
    /// 同 (server, database) 并发只加载一次。
    /// 返回后调用 GetObjectsForPrefix 之类的 API 拿候选。
    /// </summary>
    /// <param name="connectionString">当前账套的连接串（含 InitialCatalog）</param>
    /// <param name="forceReload">强制重新拉（切库后调用）</param>
    public static async Task WarmupAsync(string connectionString, bool forceReload = false)
    {
        if (string.IsNullOrEmpty(connectionString)) return;

        string key;
        ServerDb? sd;
        try
        {
            (key, sd) = ParseKey(connectionString);
        }
        catch
        {
            // 连接串解析失败（极端情况）→ 清空所有缓存兜底
            _cache.Clear();
            return;
        }

        if (sd == null || string.IsNullOrEmpty(sd.Database))
        {
            // 未指定库 → 清掉所有同 server 缓存
            InvalidateServer(sd?.Server ?? "");
            return;
        }

        if (!forceReload && _cache.TryGetValue(key, out var hit) && !IsStale(hit))
            return;

        // 同 key 已加载 → 等结果
        var existing = _loadingTasks.GetOrAdd(key, _ => LoadFromDbAsync(connectionString));
        try
        {
            var objects = await existing;
            _cache[key] = new CacheEntry(sd.Server, sd.Database, objects, DateTime.UtcNow);
        }
        finally
        {
            _loadingTasks.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 从缓存拿前缀匹配的对象名候选（用于 IntelliSense 弹窗）。
    /// 自动按 Schema 限定：
    /// - 输入 "SELECT * FROM dbo." → 只返 dbo 下
    /// - 输入 "SELECT * FROM " → 返所有 schema 下的对象
    /// </summary>
    /// <param name="connectionString">当前连接的 connectionString（决定用哪个 (server,db) 的缓存）</param>
    /// <param name="word">光标前的单词（已含 schema. 前缀时传入完整字符串；否则纯名字）</param>
    public static List<string> GetObjectSuggestions(string connectionString, string word)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(word))
            return new();

        ServerDb? sd;
        try { (_, sd) = ParseKey(connectionString); }
        catch { return new(); }
        if (sd == null || string.IsNullOrEmpty(sd.Database)) return new();

        var key = $"{sd.Server}|{sd.Database}";
        if (!_cache.TryGetValue(key, out var entry)) return new();

        // Schema 限定解析
        string? schemaFilter = null;
        string namePrefix = word;
        if (word.Contains('.'))
        {
            var parts = word.Split('.');
            schemaFilter = parts[0];   // "dbo" / "Sales" 等
            namePrefix = parts.Length > 1 ? parts[1] : "";
        }

        // 跟 SSMS 一致：补全是大小写不敏感前缀匹配
        var matches = entry.Objects
            .Where(o => string.IsNullOrEmpty(schemaFilter)
                || o.SchemaName.Equals(schemaFilter, StringComparison.OrdinalIgnoreCase))
            .Where(o => string.IsNullOrEmpty(namePrefix)
                || o.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(o => $"{o.SchemaName}.{o.Name}")
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
        return matches;
    }

    /// <summary>
    /// 从缓存取按 kind 过滤的所有对象（用于对象资源管理器）。
    /// 注意：返回时直接给出 DbObject 列表，方便 explorer 拿到 Schema+Name+Columns。
    /// </summary>
    /// <param name="connectionString">当前连接的 connectionString</param>
    /// <param name="kinds">返回哪些类型的对象；同时包含 Column 数据。</param>
    public static List<DbObject> GetObjectsByKind(string connectionString, IEnumerable<ObjectKind> kinds)
    {
        var result = new List<DbObject>();
        if (string.IsNullOrEmpty(connectionString)) return result;

        ServerDb? sd;
        try { (_, sd) = ParseKey(connectionString); }
        catch { return result; }
        if (sd == null || string.IsNullOrEmpty(sd.Database)) return result;

        var key = $"{sd.Server}|{sd.Database}";
        if (!_cache.TryGetValue(key, out var entry)) return result;

        var kindSet = new HashSet<ObjectKind>(kinds);
        result = entry.Objects.Where(o => kindSet.Contains(o.Kind)).ToList();
        return result;
    }

    /// <summary>取某个对象的列名（暂未用到，先留接口）</summary>
    public static List<string> GetColumnSuggestions(string connectionString, string? schema, string objectName, string columnPrefix)
    {
        if (string.IsNullOrEmpty(connectionString)) return new();
        ServerDb? sd;
        try { (_, sd) = ParseKey(connectionString); }
        catch { return new(); }
        if (sd == null || string.IsNullOrEmpty(sd.Database)) return new();

        var key = $"{sd.Server}|{sd.Database}";
        if (!_cache.TryGetValue(key, out var entry)) return new();

        var obj = entry.Objects.FirstOrDefault(o =>
            o.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(schema) || o.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)));
        if (obj == null || string.IsNullOrEmpty(obj.Columns)) return new();

        // Columns 是 "ColA,ColB,ColC" 格式（轻量，不引入 second map）
        var cols = obj.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var matches = cols
            .Where(c => string.IsNullOrEmpty(columnPrefix)
                || c.StartsWith(columnPrefix, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();
        return matches;
    }

    /// <summary>清空整个缓存（账套变更时调用）</summary>
    public static void InvalidateAll()
    {
        _cache.Clear();
    }

    /// <summary>清掉指定 server 下所有库的缓存</summary>
    public static void InvalidateServer(string server)
    {
        if (string.IsNullOrEmpty(server)) return;
        var prefix = server + "|";
        foreach (var k in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            _cache.TryRemove(k, out _);
    }

    // ============================================
    // 内部
    // ============================================

    private sealed record ServerDb(string Server, string Database);

    private static (string key, ServerDb? sd) ParseKey(string connStr)
    {
        var b = new SqlConnectionStringBuilder(connStr);
        var sd = new ServerDb(b.DataSource ?? "", b.InitialCatalog ?? "");
        var key = $"{sd.Server}|{sd.Database}";
        return (key, sd);
    }

    /// <summary>缓存条目 1 小时有效（一般切换是用户主动，1h 太长；2 分钟更友好）</summary>
    private static bool IsStale(CacheEntry e) => (DateTime.UtcNow - e.LoadedAt) > TimeSpan.FromMinutes(2);

    /// <summary>从数据库拉"用户可见的所有 schema-bounded 对象"（含存储过程/触发器）</summary>
    private static async Task<List<DbObject>> LoadFromDbAsync(string connStr)
    {
        var list = new List<DbObject>();
        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // sys.objects.type 列对照：U=Table, V=View, IF=Inline TVF, TF=Multi-stmt TVF, FN=Scalar Function, P=Procedure, TR=Trigger
            // 存储过程/触发器没有列，跳过 ColumnsCsv
            const string sql = @"
SELECT
    s.name           AS SchemaName,
    o.name           AS ObjectName,
    o.type           AS ObjectType,
    OBJECT_SCHEMA_NAME(o.object_id) AS SchemaName2,
    CASE WHEN o.type IN ('U','V','IF','TF','FN') THEN
        (
            SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY c.column_id)
            FROM sys.columns c
            WHERE c.object_id = o.object_id
        )
    END AS ColumnsCsv
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('U','V','IF','TF','FN','P','TR')  -- 7 种
  AND o.is_ms_shipped = 0                           -- 排除系统对象
  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
ORDER BY s.name, o.name";

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 5 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var schema = r.GetString(0);
                var name = r.GetString(1);
                var type = r.GetString(2).Trim();
                var cols = r.IsDBNull(4) ? null : r.GetString(4);

                var kind = type switch
                {
                    "U" => ObjectKind.Table,
                    "V" => ObjectKind.View,
                    "IF" or "TF" => ObjectKind.TableValuedFunction,
                    "FN" => ObjectKind.ScalarFunction,
                    "P" => ObjectKind.StoredProcedure,
                    "TR" => ObjectKind.Trigger,
                    _ => ObjectKind.Table
                };
                list.Add(new DbObject(schema, name, kind, cols));
            }
        }
        catch
        {
            // 加载失败（无权限/网络抖）→ 返回空列表，让 UI 退化为只显示关键字
        }
        return list;
    }
}
