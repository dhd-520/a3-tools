using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// SQL Server 数据库对象 Schema 缓存 + IntelliSense 数据源：
/// - 表 (TABLE)
/// - 视图 (VIEW)
/// - 表值函数 (IF = Inline TVF / TF = Multi-stmt TVF)
/// - 标量函数 (FN = Scalar Function)
/// - 每个对象的列名（用于后续扩展列名联想）
///
/// 缓存生命周期：进程内。按 (server, database) 维度隔离。
/// - 后台预热（不卡 UI）
/// - 切库时自动 invalidate 旧缓存（即使新库不存在也清空）
/// - 用户切回原库 -> 命中缓存 -> 0 IO
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

    /// <summary>
    /// 可选的 IDataAccess 代理（Http 模式下由 SqlQueryForm 注入）。
    /// null = 直连模式，用 SqlConnection。
    /// </summary>
    private static A3Tools.Common.DataAccess.IDataAccess? _dataAccess;

    // ============================================
    // 公共 API
    // ============================================

    /// <summary>
    /// 注入 IDataAccess（Http 模式下由 SqlQueryForm 调用）。
    /// 设为 null 恢复直连模式。
    /// </summary>
    public static void SetDataAccess(A3Tools.Common.DataAccess.IDataAccess? dataAccess)
    {
        _dataAccess = dataAccess;
    }

    // ============================================
    // 事件：IntelliSense 异步加载后重弹
    // ============================================

    /// <summary>
    /// 缓存加载完成事件（key 已就绪）。
    /// 订阅者在事件中可重调 GetSuggestions 拿全部结果。
    /// 同步调用 (UI 线程) 需谨慎: 事件发布不在 UI 线程。
    /// </summary>
    public static event Action<string>? Loaded;

    /// <summary>检查指定连接串缓存是否已加载 (UI 线程快查, 不阻塞)</summary>
    public static bool IsLoaded(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return false;
        try
        {
            var (key, _) = ParseKey(connectionString);
            return _cache.ContainsKey(key);
        }
        catch { return false; }
    }

    /// <summary>
    /// 异步触发缓存加载 (fire-and-forget)。
    /// 不会阻塞调用线程, 加载完成后触发 Loaded 事件。
    /// 多线程安全: 内部 _loadingTasks 保证同 key 只加载一次。
    /// </summary>
    public static void EnsureLoadingAsync(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return;
        if (IsLoaded(connectionString)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await WarmupAsync(connectionString).ConfigureAwait(false);
                string key;
                try { (key, _) = ParseKey(connectionString); } catch { return; }
                if (_cache.ContainsKey(key))
                    Loaded?.Invoke(key);
            }
            catch { /* 加载失败静默 */ }
        });
    }

    /// <summary>
    /// 当前是否走 Http 代理
    /// </summary>
    public static bool IsHttpMode => _dataAccess != null && _dataAccess.Mode == A3Tools.Common.DataAccess.DataAccessMode.Http;

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
            // 连接串解析失败（极端情况）-> 清空所有缓存兜底
            _cache.Clear();
            return;
        }

        if (sd == null || string.IsNullOrEmpty(sd.Database))
        {
            // 未指定库 -> 清掉所有同 server 缓存
            InvalidateServer(sd?.Server ?? "");
            return;
        }

        if (!forceReload && _cache.TryGetValue(key, out var hit) && !IsStale(hit))
            return;

        // 同 key 已加载 -> 等结果
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
    /// 同步等待缓存加载完成（供 IntelliSense 同步调用 GetSuggestions 使用）。
    /// 陛下反馈 EXEC 弹不出存储过程 - 原因：WarmupAsync 是 fire-and-forget，
    /// 缓存未就绪时 GetObjectsByKind 返空。修复：GetSuggestions 内调本方法同步等。
    /// 注意：不能直接 Wait() 已有 _loadingTasks（会 UI context 死锁）。
    /// 严格走 Task.Run + ConfigureAwait(false) 避免 WinForms 同步上下文死锁。
    /// </summary>
    public static bool EnsureLoadedSync(string connectionString, int timeoutMs = 10000)
    {
        if (string.IsNullOrEmpty(connectionString)) return false;
        var key = KeyOf(connectionString);

        // 已就绪 -> 返 true
        if (_cache.ContainsKey(key)) return true;

        // 不管_loadingTasks 是否有进行中任务，统一起一个新 Task
        // 内部 WarmupAsync 会重用已有 loading task（GetOrAdd 逻辑）
        // 关键：Task.Run 让所有 continuation 留在 ThreadPool，不回 UI context -> 不死锁
        var t = Task.Run(async () =>
        {
            try
            {
                await WarmupAsync(connectionString).ConfigureAwait(false);
            }
            catch { /* 加载失败，缓存仍空 */ }
        });
        try { t.Wait(timeoutMs); } catch { /* timeout 也返 false */ }
        return _cache.ContainsKey(key);
    }

    private static string KeyOf(string connectionString)
    {
        try { var (key, _) = ParseKey(connectionString); return key; }
        catch { return connectionString; }
    }

    /// <summary>
    /// 从缓存拿前缀匹配的对象名候选（用于 IntelliSense 弹窗）。
    /// 自动按 Schema 限定：
    /// - 输入 "SELECT * FROM dbo." -> 只返 dbo 下
    /// - 输入 "SELECT * FROM " -> 返所有 schema 下的对象
    /// </summary>
    /// <param name="connectionString">当前连接的 connectionString（决定用哪个 (server,db) 的缓存）</param>
    /// <param name="word">光标前的单词（已含 schema. 前缀时传入完整字符串；否则纯名字）</param>
    /// <param name="kinds">
    /// 限定返回的对象类型集合。null 或空 = 返所有 6 类（向后兼容）。
    /// FROM/JOIN/UPDATE/TABLE 上下文应只传 { Table, View, TableValuedFunction }，
    /// 否则会混入存储过程 / 标量函数 / 触发器（陛下 2026-07-17 反馈）。
    /// </param>
    public static List<string> GetObjectSuggestions(string connectionString, string word, IEnumerable<ObjectKind>? kinds = null)
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

        // 对象类型过滤（FROM/JOIN 上下文只取表/视图/表值函数，排除存储过程/标量函数/触发器）
        HashSet<ObjectKind>? kindSet = null;
        if (kinds != null)
        {
            var arr = kinds as ObjectKind[] ?? kinds.ToArray();
            if (arr.Length > 0) kindSet = new HashSet<ObjectKind>(arr);
        }

        // 跟 SSMS 一致：补全是大小写不敏感前缀匹配
        var matches = entry.Objects
            .Where(o => kindSet == null || kindSet.Contains(o.Kind))
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
        if (obj == null) return new();

        // 列名懒加载：cache 没有时同步调 LoadColumnsForObject（限制超时 3秒避免卡 UI）
        string colsCsv = obj.Columns ?? "";
        if (string.IsNullOrEmpty(colsCsv) && !string.IsNullOrEmpty(schema))
        {
            var lazy = LoadColumnsForObject(connectionString, schema, objectName, timeoutMs: 3000);
            colsCsv = string.Join(",", lazy);
        }
        if (string.IsNullOrEmpty(colsCsv)) return new();

        // Columns 是 "ColA,ColB,ColC" 格式（轻量，不引入 second map）
        var cols = colsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
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

    /// <summary>
    /// 按需加载单个对象的列名（兼容 SQL Server 2008+，避开 STRING_AGG）。
    /// 调用者：弹出 IntelliSense 列名 / 对象资源管理器展开节点。
    /// </summary>
    public static List<string> LoadColumnsForObject(string connectionString, string schemaName, string objectName, int timeoutMs = 3000)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(schemaName) || string.IsNullOrEmpty(objectName))
            return new List<string>();

        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            const string sql = @"SELECT c.name FROM sys.columns c INNER JOIN sys.objects o ON c.object_id = o.object_id INNER JOIN sys.schemas s ON o.schema_id = s.schema_id WHERE s.name = @schema AND o.name = @object ORDER BY c.column_id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@schema", schemaName);
            cmd.Parameters.AddWithValue("@object", objectName);
            cmd.CommandTimeout = Math.Max(5, timeoutMs / 1000);
            using var r = cmd.ExecuteReader();
            var cols = new List<string>();
            while (r.Read())
                cols.Add(r.GetString(0));
            return cols;
        }
        catch (Exception ex)
        {
            LogLoadFailure("LoadColumnsForObject", connectionString, ex);
            return new List<string>();
        }
    }

    /// <summary>统一的失败日志入口（不让 catch 静默吞）</summary>
    private static void LogLoadFailure(string source, string connStr, Exception ex)
    {
        try
        {
            string dbHint = "";
            try
            {
                var b = new SqlConnectionStringBuilder(connStr);
                dbHint = $"[{b.DataSource}/{b.InitialCatalog}] ";
            }
            catch { }
            var dir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "diag");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "schema-load-error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {dbHint}{source}: {ex}\n---\n");
        }
        catch { /* 日志也写不动就别挣扎了 */ }
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
        // Http 代理模式：走 IDataAccess.ExecuteQueryAsync
        if (_dataAccess != null && _dataAccess.Mode == A3Tools.Common.DataAccess.DataAccessMode.Http)
        {
            return await LoadFromDbViaHttpAsync();
        }

        var list = new List<DbObject>();
        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // 列名懒加载（避开老 SQL Server 没有 STRING_AGG 的兼容问题 + 5000+ 函数相关子查询性能）
            const string sql = @"
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type AS ObjectType
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('U','V','IF','TF','FN','P','TR')
  AND o.is_ms_shipped = 0
  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
ORDER BY s.name, o.name";

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var schema = r.GetString(0);
                var name = r.GetString(1);
                var type = r.GetString(2).Trim();
                var cols = null as string;  // 列名懒加载

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
        catch (Exception ex)
        {
            // 失败：只记录日志，让外层 return list 兜底（list 此时是空的，等价于空列表）
            LogLoadFailure("LoadFromDbAsync", connStr, ex);
        }
        return list;
    }

    /// <summary>
    /// Http 代理模式下通过 IDataAccess 查对象列表
    /// </summary>
    private static async Task<List<DbObject>> LoadFromDbViaHttpAsync()
    {
        var list = new List<DbObject>();
        try
        {
            const string sql = @"
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type AS ObjectType
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('U','V','IF','TF','FN','P','TR')
  AND o.is_ms_shipped = 0
  AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
ORDER BY s.name, o.name";

            var result = await _dataAccess!.ExecuteQueryAsync(sql);
            if (!result.Success || result.Tables.Count == 0) return list;

            var table = result.Tables[0];
            foreach (var row in table.Rows)
            {
                var schema = row[0]?.ToString() ?? "";
                var name = row[1]?.ToString() ?? "";
                var type = (row[2]?.ToString() ?? "").Trim();
                var cols = null as string;  // 列名懒加载

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
        catch (Exception ex)
        {
            // 失败：只记录日志，让外层 return list 兜底
            LogLoadFailure("LoadFromDbViaHttpAsync", "", ex);
        }
        return list;
    }
}
