using System.Data;
using System.Diagnostics;
using System.Text;
using A3Tools.Common.DataAccess;
using A3Tools.Models;
using A3Tools.Services;

namespace A3Tools.Plugins.Default.Forms;

/// <summary>
/// 代理模式工具类：帮助各工具窗体在 Http 模式下通过 IDataAccess 执行 SQL。
/// 直连模式保持原逻辑不变。
/// </summary>
internal static class ProxyHelper
{
    /// <summary>
    /// 根据账套创建 IDataAccess（Direct 或 Http）
    /// </summary>
    public static IDataAccess? CreateDataAccess(Account? account)
    {
        if (account == null) return null;

        var connStr = BuildConnStr(account);

        if (account.ConnectionMode == DataAccessMode.Http)
        {
            return new HttpDataAccess(
                endpoint: account.HttpEndpoint,
                connStr: connStr,
                secretKey: account.HttpSecretKey,
                serverPublicKey: account.HttpServerPublicKey,
                displayName: account.Name);
        }
        return new DirectDataAccess(connStr, account.Name);
    }

    /// <summary>
    /// 根据账套构建连接字符串
    /// </summary>
    public static string BuildConnStr(Account account)
    {
        if (string.IsNullOrEmpty(account.DbUser))
            return $"Server={account.Database};Database={account.DatabaseName};Integrated Security=True;TrustServerCertificate=True;";
        return $"Server={account.Database};Database={account.DatabaseName};User Id={account.DbUser};Password={EncryptionService.Decrypt(account.DbPassword)};TrustServerCertificate=True;";
    }

    /// <summary>
    /// 根据连接信息构建连接字符串（兼容旧工具的 server/db/user/password 参数模式）
    /// </summary>
    public static string BuildConnStr(string server, string dbName, string user, string encryptedPassword)
    {
        if (string.IsNullOrEmpty(user))
            return $"Server={server};Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        var pwd = string.IsNullOrEmpty(encryptedPassword) ? "" : EncryptionService.Decrypt(encryptedPassword);
        return $"Server={server};Database={dbName};User Id={user};Password={pwd};TrustServerCertificate=True;";
    }

    /// <summary>
    /// 用 IDataAccess 执行查询，返回 DataTable（兼容直连和 Http 代理）
    /// </summary>
    public static async Task<DataTable> ExecuteQueryToDataTableAsync(IDataAccess dataAccess, string sql)
    {
        var result = await dataAccess.ExecuteQueryAsync(sql);
        if (!result.Success || result.Tables.Count == 0)
            return new DataTable();

        var table = result.Tables[0];
        var dt = new DataTable();
        // 对齐 SqlDataAdapter 行为：重复列名自动加 _2/_3/... 后缀（直连模式 SqlDataAdapter 内部就这样处理）。
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in table.Columns)
        {
            var name = col.Name;
            if (!usedNames.Add(name))
            {
                int suffix = 2;
                string unique;
                do { unique = $"{name}_{suffix++}"; } while (!usedNames.Add(unique));
                name = unique;
            }
            dt.Columns.Add(name, typeof(object));
        }

        foreach (var row in table.Rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < row.Length; i++)
                dr[i] = row[i] ?? DBNull.Value;
            dt.Rows.Add(dr);
        }
        return dt;
    }

    /// <summary>
    /// 用 IDataAccess 执行查询，返回第一行第一列的值（object?）
    /// </summary>
    public static async Task<object?> ExecuteScalarAsync(IDataAccess dataAccess, string sql)
    {
        var dt = await ExecuteQueryToDataTableAsync(dataAccess, sql);
        if (dt.Rows.Count == 0 || dt.Columns.Count == 0)
            return null;
        return dt.Rows[0][0];
    }

    /// <summary>
    /// 用 IDataAccess 执行 NonQuery（INSERT/UPDATE/DELETE/DDL），返回影响行数
    /// </summary>
    public static async Task<int> ExecuteNonQueryAsync(IDataAccess dataAccess, string sql)
    {
        return await dataAccess.ExecuteNonQueryAsync(sql);
    }

    /// <summary>
    /// 用 IDataAccess 执行批量 SQL（GO 切分），返回执行结果
    /// </summary>
    public static async Task<QueryResult> ExecuteBatchAsync(IDataAccess dataAccess, string batchSql)
    {
        return await dataAccess.ExecuteBatchAsync(batchSql);
    }

    /// <summary>
    /// 测试连接是否可用
    /// </summary>
    public static async Task<bool> TestConnectionAsync(IDataAccess? dataAccess)
    {
        if (dataAccess == null) return false;
        return await dataAccess.TestConnectionAsync();
    }

    /// <summary>
    /// 判断账套是否走 Http 代理
    /// </summary>
    public static bool IsHttp(Account? account)
        => account != null && account.ConnectionMode == DataAccessMode.Http;

    /// <summary>
    /// Http 模式下弹窗警告并返回 true（调用方据此关闭窗体）。
    /// 直连模式返回 false（继续）。
    /// 用途：某些工具窗体尚未迁移到 IDataAccess，Http 模式打开会假死，预先拦截。
    /// </summary>
    public static bool WarnIfHttp(Account? account, string formName)
    {
        if (!IsHttp(account)) return false;

        MessageBox.Show(
            $"[{formName}] 暂不支持 Http 代理模式，请切换为直连账套后重试。",
            "提示",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return true;
    }

    // ==================== SqlBulkCopy 替代方案 ====================

    /// <summary>
    /// 通过 IDataAccess 复制表数据（替代 SqlBulkCopy）。
    /// 逻辑：1. 获取目标表列名 2. 从源查询数据 3. 调 BulkCopyAsync 高效写入目标。
    /// 直连模式：BulkCopyAsync 走 SqlBulkCopy（保效率）
    /// Http 模式：BulkCopyAsync 每 500 行一条批量 INSERT（保效率）
    /// </summary>
    public static async Task<int> CopyTableDataAsync(
        IDataAccess srcDA, IDataAccess tgtDA,
        string tableName, string whereField, string whereValue,
        bool deleteFirst, string tag = "")
    {
        // 1. 获取目标表列名
        var columns = await GetTableColumnsAsync(tgtDA, tableName);
        if (columns.Count == 0)
            throw new Exception($"目标表 {tableName} 不存在或没有列");

        // 2. 删除已有数据 / 检查存在性
        if (deleteFirst)
        {
            var delSql = $"DELETE FROM dbo.[{tableName}] WHERE [{whereField}] = '{EscapeSql(whereValue)}'";
            await tgtDA.ExecuteNonQueryAsync(delSql);
        }
        else
        {
            var chkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{whereField}] = '{EscapeSql(whereValue)}'";
            var countObj = await ExecuteScalarAsync(tgtDA, chkSql);
            if (Convert.ToInt32(countObj ?? 0) > 0)
            {
                Debug.WriteLine($"{tag}表{tableName}中{whereField}={whereValue}已存在，跳过");
                return 0;
            }
        }

        // 3. 从源查询（DataTable → ResultTable）
        var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
        var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{whereField}] = '{EscapeSql(whereValue)}'";
        var srcDt = await ExecuteQueryToDataTableAsync(srcDA, selSql);
        if (srcDt.Rows.Count == 0) return 0;

        var resultTable = DataTableToResultTable(srcDt, columns);

        // 4. 高效 BulkCopy
        var copied = await tgtDA.BulkCopyAsync(resultTable, tableName);
        Debug.WriteLine($"{tag}复制表{tableName}: {copied} 行（模式：{tgtDA.Mode}）");
        return copied;
    }

    /// <summary>
    /// 通过 IDataAccess 按父GUID复制子表数据（替代 SqlBulkCopy）
    /// </summary>
    public static async Task<int> CopyTableDataByParentGuidAsync(
        IDataAccess srcDA, IDataAccess tgtDA,
        string tableName, string parentField, string parentGuid,
        bool deleteFirst, string tag = "")
    {
        var columns = await GetTableColumnsAsync(tgtDA, tableName);
        if (columns.Count == 0)
            throw new Exception($"目标表 {tableName} 不存在或没有列");

        if (deleteFirst)
        {
            var delSql = $"DELETE FROM dbo.[{tableName}] WHERE [{parentField}] = '{EscapeSql(parentGuid)}'";
            await tgtDA.ExecuteNonQueryAsync(delSql);
        }

        var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
        var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{parentField}] = '{EscapeSql(parentGuid)}'";
        var srcDt = await ExecuteQueryToDataTableAsync(srcDA, selSql);
        if (srcDt.Rows.Count == 0) return 0;

        var resultTable = DataTableToResultTable(srcDt, columns);
        var copied = await tgtDA.BulkCopyAsync(resultTable, tableName);
        Debug.WriteLine($"{tag}复制表{tableName}: {copied} 行（模式：{tgtDA.Mode}）");
        return copied;
    }

    /// <summary>
    /// 通过 IDataAccess 按多个主键值复制表数据（替代 SqlBulkCopy）
    /// </summary>
    public static async Task<(int Copied, int Skipped, int NotFound)> CopyTableDataByKeysAsync(
        IDataAccess srcDA, IDataAccess tgtDA,
        string tableName, string[] keyColumns, string[] keyValues,
        bool deleteFirst, string tag = "")
    {
        var columns = await GetTableColumnsAsync(tgtDA, tableName);
        if (columns.Count == 0)
            throw new Exception($"目标表 {tableName} 不存在或没有列");

        var whereParts = keyColumns.Select((c, i) => $"[{c}] = '{EscapeSql(keyValues[i])}'");
        var whereClause = string.Join(" AND ", whereParts);

        if (deleteFirst)
        {
            await tgtDA.ExecuteNonQueryAsync($"DELETE FROM dbo.[{tableName}] WHERE {whereClause}");
        }
        else
        {
            var chkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE {whereClause}";
            var countObj = await ExecuteScalarAsync(tgtDA, chkSql);
            if (Convert.ToInt32(countObj ?? 0) > 0)
            {
                Debug.WriteLine($"{tag}表{tableName}中{string.Join(",", keyColumns)}={string.Join(",", keyValues)}已存在，跳过");
                return (0, 1, 0);
            }
        }

        var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
        var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE {whereClause}";
        var srcDt = await ExecuteQueryToDataTableAsync(srcDA, selSql);
        if (srcDt.Rows.Count == 0)
            return (0, 0, 1);

        var resultTable = DataTableToResultTable(srcDt, columns);
        var copied = await tgtDA.BulkCopyAsync(resultTable, tableName);
        Debug.WriteLine($"{tag}复制表{tableName}: {copied} 行（模式：{tgtDA.Mode}）");
        return (copied, 0, 0);
    }

    /// <summary>
    /// DataTable → ResultTable 转换（BulkCopyAsync 入参）
    /// </summary>
    private static ResultTable DataTableToResultTable(DataTable dt, List<string> columns)
    {
        var resultTable = new ResultTable();
        foreach (var col in columns)
            resultTable.Columns.Add(new ColumnInfo { Name = col, TypeName = "System.Object" });

        foreach (DataRow row in dt.Rows)
        {
            var rowArr = new object?[columns.Count];
            for (int i = 0; i < columns.Count; i++)
                rowArr[i] = row[i] == DBNull.Value ? null : row[i];
            resultTable.Rows.Add(rowArr);
        }
        return resultTable;
    }

    /// <summary>
    /// 通过 IDataAccess 获取表的列名列表
    /// </summary>
    public static async Task<List<string>> GetTableColumnsAsync(IDataAccess dataAccess, string tableName)
    {
        var sql = $@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = '{EscapeSql(tableName)}' AND TABLE_SCHEMA = 'dbo'
            ORDER BY ORDINAL_POSITION";
        var dt = await ExecuteQueryToDataTableAsync(dataAccess, sql);
        var columns = new List<string>();
        foreach (DataRow row in dt.Rows)
        {
            columns.Add(row[0]?.ToString() ?? "");
        }
        return columns;
    }

    // ==================== 工具方法 ====================

    /// <summary>
    /// 转义 SQL 字符串值中的单引号
    /// </summary>
    public static string EscapeSql(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("'", "''");
    }

    /// <summary>
    /// 将 .NET 值格式化为 SQL 字面量
    /// </summary>
    public static string FormatSqlValue(object? val)
    {
        if (val == null || val == DBNull.Value)
            return "NULL";
        if (val is bool b)
            return b ? "1" : "0";
        if (val is DateTime dt)
            return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
        if (val is decimal || val is double || val is float || val is int || val is long)
            return val.ToString()!;
        if (val is Guid g)
            return $"'{g}'";
        if (val is byte[] bytes)
            return "0x" + Convert.ToHexString(bytes);
        // 默认按字符串处理
        return $"'{EscapeSql(val.ToString())}'";
    }
}
