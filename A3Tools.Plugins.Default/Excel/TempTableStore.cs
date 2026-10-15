using System;
using System.Collections.Generic;
using System.Linq;
using A3Tools.Common.DataAccess;

namespace A3Tools.Plugins.Default.Excel;

/// <summary>
/// Excel 导入产生的内存临时表仓库
/// 生命周期 = 持有它的 SqlQueryForm 的生命周期
/// 关闭 SqlQueryForm 时调用 <see cref="Clear"/> 释放内存引用
///
/// 设计要点:
/// - Key 不区分大小写 (alias 在 SQL 里通常大写, 但程序里输入可能小写)
/// - 加锁: import 是 UI 线程, query 是后台线程, 会有并发
/// - 只存 <see cref="ResultTable"/>, 不解析 SQL, 不执行 SQL
///   (执行是 <see cref="TempTableExecutor"/> 的职责)
/// </summary>
public sealed class TempTableStore
{
    private readonly Dictionary<string, ResultTable> _tables =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>导入或覆盖一张临时表</summary>
    public void AddOrReplace(string alias, ResultTable table)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("alias 不能为空", nameof(alias));
        if (table is null)
            throw new ArgumentNullException(nameof(table));
        // alias 规范化: SQL Server 标识符规范 (字母/下划线开头, 后跟字母数字下划线)
        var key = NormalizeAlias(alias);

        lock (_lock)
        {
            _tables[key] = table;
        }
    }

    /// <summary>尝试取一张临时表</summary>
    public bool TryGet(string alias, out ResultTable? table)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            table = null;
            return false;
        }
        var key = NormalizeAlias(alias);
        lock (_lock)
        {
            return _tables.TryGetValue(key, out table);
        }
    }

    /// <summary>移除一张临时表 (返回是否真移除了一行)</summary>
    public bool Remove(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return false;
        var key = NormalizeAlias(alias);
        lock (_lock)
        {
            return _tables.Remove(key);
        }
    }

    /// <summary>清空全部 (SqlQueryForm 关闭时调用)</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tables.Clear();
        }
    }

    /// <summary>所有别名 (UI 列表用)</summary>
    public IReadOnlyList<string> ListAliases()
    {
        lock (_lock)
        {
            return _tables.Keys.ToList();
        }
    }

    public int Count
    {
        get { lock (_lock) return _tables.Count; }
    }

    private static string NormalizeAlias(string alias)
    {
        var s = alias.Trim();
        if (s.Length == 0)
            throw new ArgumentException("alias 去掉空格后为空", nameof(alias));
        // 防止 alias 带 ## 前缀进 store (locator 会先剥掉 ##)
        if (s.StartsWith("##", StringComparison.Ordinal))
            s = s.Substring(2);
        return s;
    }
}
