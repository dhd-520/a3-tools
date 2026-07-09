using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace A3Tools.Common.DataAccess
{
    /// <summary>
    /// 数据访问抽象接口：直连和 HTTP 代理两种实现
    /// 所有工具（SQL 查询、跨库复制、对比表结构等）统一走这个接口
    /// </summary>
    public interface IDataAccess
    {
        /// <summary>
        /// 连接模式：Direct 或 Http
        /// </summary>
        DataAccessMode Mode { get; }

        /// <summary>
        /// 显示名称（UI 用）
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 是否可用（测试连接）
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken ct = default);

        /// <summary>
        /// 执行查询，返回多结果集
        /// </summary>
        Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default);

        /// <summary>
        /// 执行 NonQuery（INSERT/UPDATE/DELETE/DDL）
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default);

        /// <summary>
        /// 获取表/视图列表
        /// </summary>
        Task<List<TableInfo>> GetTablesAsync(string? schemaFilter = null, CancellationToken ct = default);

        /// <summary>
        /// 获取表结构（列信息）
        /// </summary>
        Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName, CancellationToken ct = default);
    }

    /// <summary>
    /// 连接模式
    /// </summary>
    public enum DataAccessMode
    {
        /// <summary>直连数据库</summary>
        Direct,
        /// <summary>HTTP 代理（走 A3ToolsHub 服务端转发）</summary>
        Http
    }

    /// <summary>
    /// 查询结果（多结果集）
    /// </summary>
    public class QueryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long ElapsedMs { get; set; }
        public int AffectedRows { get; set; }
        public int TotalRows { get; set; }
        public List<ResultTable> Tables { get; set; } = new();
    }

    public class ResultTable
    {
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<object?[]> Rows { get; set; } = new();
        public bool Truncated { get; set; }
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
    }

    public class TableInfo
    {
        public string Schema { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";  // BASE TABLE / VIEW
    }
}
