using System.Data;
using Microsoft.Data.SqlClient;

namespace A3Tools.Services;

/// <summary>
/// 跨库表数据复制服务（供所有工具统一调用）
/// </summary>
public static class TableCopyService
{
    /// <summary>
    /// 根据条件字段+值复制表数据
    /// </summary>
    /// <param name="srcConn">源数据库连接</param>
    /// <param name="tgtConn">目标数据库连接</param>
    /// <param name="tableName">表名</param>
    /// <param name="whereField">条件字段名</param>
    /// <param name="whereValue">条件字段值</param>
    /// <param name="deleteFirst">是否先删除目标端已有记录</param>
    /// <param name="tag">日志前缀标签，如"[报表]"</param>
    public static void CopyTableData(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string whereField, string whereValue, bool deleteFirst, string tag = "")
    {
        try
        {
            var columns = GetTableColumns(tgtConn, tableName);
            if (columns.Count == 0) return;

            // 先删除
            if (deleteFirst)
            {
                var delSql = $"DELETE FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var delCmd = new SqlCommand(delSql, tgtConn);
                delCmd.Parameters.AddWithValue("@value", whereValue);
                delCmd.ExecuteNonQuery();
            }

            // 检查是否已存在（仅在非deleteFirst模式）
            if (!deleteFirst)
            {
                var chkSql = $"SELECT COUNT(*) FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
                using var chkCmd = new SqlCommand(chkSql, tgtConn);
                chkCmd.Parameters.AddWithValue("@value", whereValue);
                if (Convert.ToInt32(chkCmd.ExecuteScalar()) > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"{tag}表{tableName}中{whereField}={whereValue}已存在，跳过");
                    return;
                }
            }

            // 从源读取
            var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
            var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{whereField}] = @value";
            using var selCmd = new SqlCommand(selSql, srcConn);
            selCmd.Parameters.AddWithValue("@value", whereValue);
            var dt = new DataTable();
            using var adapter = new SqlDataAdapter(selCmd);
            adapter.Fill(dt);
            if (dt.Rows.Count == 0) return;

            // SqlBulkCopy 写入
            using var bulk = new SqlBulkCopy(tgtConn);
            bulk.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in columns)
                bulk.ColumnMappings.Add(col, col);
            bulk.WriteToServer(dt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{tag}复制表{tableName}失败: {ex.Message}");
            throw new Exception($"{tag}复制表{tableName}失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据父GUID字段批量复制子表数据
    /// </summary>
    public static void CopyTableDataByParentGuid(SqlConnection srcConn, SqlConnection tgtConn, string tableName, string parentField, string parentGuid, bool deleteFirst, string tag = "")
    {
        try
        {
            var columns = GetTableColumns(tgtConn, tableName);
            if (columns.Count == 0) return;

            if (deleteFirst)
            {
                var delSql = $"DELETE FROM dbo.[{tableName}] WHERE [{parentField}] = @parentGuid";
                using var delCmd = new SqlCommand(delSql, tgtConn);
                delCmd.Parameters.AddWithValue("@parentGuid", parentGuid);
                delCmd.ExecuteNonQuery();
            }

            var cols = string.Join(", ", columns.Select(c => "[" + c + "]"));
            var selSql = $"SELECT {cols} FROM dbo.[{tableName}] WHERE [{parentField}] = @parentGuid";
            using var selCmd = new SqlCommand(selSql, srcConn);
            selCmd.Parameters.AddWithValue("@parentGuid", parentGuid);
            var dt = new DataTable();
            using var adapter = new SqlDataAdapter(selCmd);
            adapter.Fill(dt);
            if (dt.Rows.Count == 0) return;

            using var bulk = new SqlBulkCopy(tgtConn);
            bulk.DestinationTableName = $"dbo.[{tableName}]";
            foreach (var col in columns)
                bulk.ColumnMappings.Add(col, col);
            bulk.WriteToServer(dt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{tag}复制表{tableName}失败: {ex.Message}");
            throw new Exception($"{tag}复制表{tableName}失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取目标表的列名列表
    /// </summary>
    public static List<string> GetTableColumns(SqlConnection conn, string tableName)
    {
        var columns = new List<string>();
        var sql = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = 'dbo'
            ORDER BY ORDINAL_POSITION";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(0));
        return columns;
    }
}