using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace A3Tools.Common.DataAccess
{
    /// <summary>
    /// 直连数据库实现（包装 SqlConnection）
    /// 用于可以直连数据库的账套
    /// </summary>
    public class DirectDataAccess : IDataAccess
    {
        private readonly string _connStr;

        public DataAccessMode Mode => DataAccessMode.Direct;
        public string DisplayName { get; }

        public DirectDataAccess(string connStr, string displayName = "")
        {
            _connStr = connStr;
            DisplayName = string.IsNullOrEmpty(displayName) ? "直连" : displayName;
        }

        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(ct);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)
        {
            var result = new QueryResult { Success = true };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(ct);
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                using var reader = await cmd.ExecuteReaderAsync(ct);

                do
                {
                    var table = new ResultTable();

                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var colName = reader.GetName(c);
                        if (string.IsNullOrEmpty(colName)) colName = $"Column{c + 1}";
                        var colType = reader.GetFieldType(c);
                        table.Columns.Add(new ColumnInfo
                        {
                            Name = colName,
                            TypeName = colType?.FullName ?? "System.Object"
                        });
                    }

                    int rowCount = 0;
                    while (await reader.ReadAsync(ct))
                    {
                        var row = new object?[reader.FieldCount];
                        for (int c = 0; c < reader.FieldCount; c++)
                        {
                            row[c] = await reader.IsDBNullAsync(c) ? null : reader.GetValue(c);
                        }
                        table.Rows.Add(row);
                        rowCount++;

                        if (rowCount >= 100000)
                        {
                            table.Truncated = true;
                            break;
                        }
                    }

                    result.Tables.Add(table);
                    result.TotalRows += rowCount;
                }
                while (reader.NextResult());

                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = $"{result.Tables.Count} result set(s) / {result.TotalRows} row(s)";
            }
            catch (SqlException ex)
            {
                sw.Stop();
                result.Success = false;
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = $"SQL Error {ex.Number}: {ex.Message}";
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = ex.Message;
            }

            return result;
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<TableInfo>> GetTablesAsync(string? schemaFilter = null, CancellationToken ct = default)
        {
            var tables = new List<TableInfo>();
            string sql = @"
                SELECT t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE 1=1";
            if (!string.IsNullOrEmpty(schemaFilter))
                sql += $" AND t.TABLE_SCHEMA = '{schemaFilter.Replace("'", "''")}'";
            sql += " ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                });
            }
            return tables;
        }

        public async Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName, CancellationToken ct = default)
        {
            var columns = new List<ColumnInfo>();
            string sql = @"
                SELECT c.COLUMN_NAME, c.DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @tableName
                ORDER BY c.ORDINAL_POSITION";

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    TypeName = reader.GetString(1)
                });
            }
            return columns;
        }
    }
}
