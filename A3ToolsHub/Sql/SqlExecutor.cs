using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Newtonsoft.Json;

namespace A3ToolsHub.Sql
{
    /// <summary>
    /// SQL 执行器：接收连接串 + SQL，返回结果
    /// 服务端无状态，每次请求自带连接串，用完即丢
    /// </summary>
    public class SqlExecutor
    {
        /// <summary>
        /// 执行查询，返回多结果集
        /// </summary>
        public QueryResult ExecuteQuery(string connStr, string sql, int timeoutSec = 60, int maxRows = 100000)
        {
            var result = new QueryResult { Success = true };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = timeoutSec;
                        using (var reader = cmd.ExecuteReader())
                        {
                            int resultIndex = 0;
                            do
                            {
                                var table = new ResultTable();

                                // 列定义
                                for (int c = 0; c < reader.FieldCount; c++)
                                {
                                    var colName = reader.GetName(c);
                                    if (string.IsNullOrEmpty(colName)) colName = "Column" + (c + 1);
                                    var colType = reader.GetFieldType(c);
                                    table.Columns.Add(new ColumnInfo
                                    {
                                        Name = colName,
                                        TypeName = colType != null ? colType.FullName : "System.Object"
                                    });
                                }

                                // 读行
                                int rowCount = 0;
                                while (reader.Read())
                                {
                                    var row = new object[reader.FieldCount];
                                    for (int c = 0; c < reader.FieldCount; c++)
                                    {
                                        row[c] = reader.IsDBNull(c) ? null : reader.GetValue(c);
                                    }
                                    table.Rows.Add(row);
                                    rowCount++;

                                    if (rowCount >= maxRows)
                                    {
                                        table.Truncated = true;
                                        break;
                                    }
                                }

                                result.Tables.Add(table);
                                result.TotalRows += rowCount;
                                resultIndex++;
                            }
                            while (reader.NextResult());
                        }
                    }
                }

                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = string.Format("{0} result set(s) / {1} row(s)", result.Tables.Count, result.TotalRows);
            }
            catch (SqlException ex)
            {
                sw.Stop();
                result.Success = false;
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = string.Format("SQL Error {0}: {1}", ex.Number, ex.Message);
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

        /// <summary>
        /// 执行 NonQuery（INSERT/UPDATE/DELETE/DDL）
        /// </summary>
        public QueryResult ExecuteNonQuery(string connStr, string sql, int timeoutSec = 60)
        {
            var result = new QueryResult { Success = true };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = timeoutSec;
                        int affected = cmd.ExecuteNonQuery();
                        result.AffectedRows = affected;
                        result.Message = string.Format("{0} row(s) affected", affected);
                    }
                }

                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
            }
            catch (SqlException ex)
            {
                sw.Stop();
                result.Success = false;
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Message = string.Format("SQL Error {0}: {1}", ex.Number, ex.Message);
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

        /// <summary>
        /// 批量执行（多条语句，用 GO 分隔），返回每条结果
        /// </summary>
        public QueryResult ExecuteBatch(string connStr, string batchSql, int timeoutSec = 120, int maxRows = 100000)
        {
            var result = new QueryResult { Success = true };
            var statements = SplitByGo(batchSql);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                foreach (var stmt in statements)
                {
                    if (string.IsNullOrWhiteSpace(stmt)) continue;

                    // 判断是查询还是 non-query（简单判断：是否以 SELECT 开头）
                    string trimmed = stmt.TrimStart().ToUpperInvariant();
                    bool isQuery = trimmed.StartsWith("SELECT") || trimmed.StartsWith("WITH") ||
                                   trimmed.StartsWith("(") || trimmed.StartsWith("EXEC") ||
                                   trimmed.StartsWith("SP_");

                    if (isQuery)
                    {
                        var subResult = ExecuteQuery(connStr, stmt, timeoutSec, maxRows);
                        if (!subResult.Success)
                        {
                            result.Success = false;
                            result.Message = subResult.Message;
                            break;
                        }
                        // 合并结果集
                        foreach (var t in subResult.Tables)
                            result.Tables.Add(t);
                        result.TotalRows += subResult.TotalRows;
                    }
                    else
                    {
                        var subResult = ExecuteNonQuery(connStr, stmt, timeoutSec);
                        if (!subResult.Success)
                        {
                            result.Success = false;
                            result.Message = subResult.Message;
                            break;
                        }
                        result.AffectedRows += subResult.AffectedRows;
                    }
                }

                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
                if (result.Success)
                {
                    result.Message = string.Format("Batch done: {0} statement(s), {1} result set(s), {2} row(s), {3} affected",
                        statements.Count, result.Tables.Count, result.TotalRows, result.AffectedRows);
                }
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

        /// <summary>
        /// 获取表/视图列表
        /// </summary>
        public QueryResult GetTables(string connStr, string schemaFilter = null)
        {
            string sql = @"
                SELECT t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE 1=1";
            if (!string.IsNullOrEmpty(schemaFilter))
                sql += " AND t.TABLE_SCHEMA = '" + schemaFilter.Replace("'", "''") + "'";
            sql += " ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            return ExecuteQuery(connStr, sql);
        }

        /// <summary>
        /// 获取表结构（列信息）
        /// </summary>
        public QueryResult GetTableSchema(string connStr, string tableName)
        {
            string sql = @"
                SELECT c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH,
                       c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.IS_NULLABLE,
                       c.COLUMN_DEFAULT, c.ORDINAL_POSITION
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = '" + tableName.Replace("'", "''") + @"'
                ORDER BY c.ORDINAL_POSITION";

            return ExecuteQuery(connStr, sql);
        }

        // ==================== GO 切分（与客户端 T-SQL 解析器对齐）====================

        private List<string> SplitByGo(string sql)
        {
            var result = new List<string>();
            var lines = sql.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var current = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.AppendLine(line);
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }
    }

    // ==================== 结果模型 ====================

    public class QueryResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long ElapsedMs { get; set; }
        public int AffectedRows { get; set; }
        public int TotalRows { get; set; }
        public List<ResultTable> Tables { get; set; } = new List<ResultTable>();
    }

    public class ResultTable
    {
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
        public List<object[]> Rows { get; set; } = new List<object[]>();
        public bool Truncated { get; set; }
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
    }
}
