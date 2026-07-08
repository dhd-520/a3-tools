using System;
using System.Collections.Generic;
using System.Linq;
using A3Tools.Plugins.Default.Forms;

class TestContext {
    static int pass = 0, fail = 0;

    static void Check(string sql, int caret, SqlIntelliSenseProvider.SqlContextKind expected, string label) {
        var got = SqlIntelliSenseProvider.DetectContext(sql, caret);
        var status = got == expected ? "OK  " : "FAIL";
        if (got == expected) pass++; else fail++;
        Console.WriteLine($"[{status}] {label,-50} sql=[{sql}] caret={caret,-3} -> got={got,-20} expected={expected}");
    }

    static void CheckAlias(string sql, string expectedKey, string? expectedSchema, string expectedName, string label) {
        var map = SqlAliasResolver.Parse(sql, 0);
        var status = "FAIL";
        if (map.TryGetValue(expectedKey, out var obj)
            && obj.ObjectName.Equals(expectedName, StringComparison.OrdinalIgnoreCase)
            && ((obj.SchemaName ?? "") == (expectedSchema ?? "")))
        {
            status = "OK  ";
            pass++;
        }
        else
        {
            fail++;
        }
        var got = map.TryGetValue(expectedKey, out var o2)
            ? $"({o2.SchemaName ?? "null"}, {o2.ObjectName})"
            : "MISSING";
        Console.WriteLine($"[{status}] {label,-50} sql=[{sql}] key=[{expectedKey}] -> got={got} expected=({expectedSchema ?? "null"}, {expectedName})");
        if (status == "FAIL")
        {
            Console.WriteLine($"        actual keys: [{string.Join(",", map.Keys)}]");
        }
    }

    static void CheckPrefix(string word, string objFull, string expectedMatch, string label) {
        // 模拟 GetSuggestions 的 prefix 匹配
        // objFull = "schema.name"
        var lastDot = objFull.LastIndexOf('.');
        var schema = objFull.Substring(0, lastDot);
        var name = objFull.Substring(lastDot + 1);

        // 重置 EXEC/EXECUTE 关键字（与生产代码一致）
        var effectivePrefix = word;
        if (effectivePrefix.Equals("EXEC", StringComparison.OrdinalIgnoreCase) ||
            effectivePrefix.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
            effectivePrefix = "";

        bool matches;
        if (effectivePrefix.Contains('.'))
        {
            var dIdx = effectivePrefix.LastIndexOf('.');
            var sPart = effectivePrefix.Substring(0, dIdx).Trim('[', ']');
            var nPart = effectivePrefix.Substring(dIdx + 1);
            matches = schema.Equals(sPart, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(nPart) || name.StartsWith(nPart, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // startsWith
            matches = name.StartsWith(effectivePrefix, StringComparison.OrdinalIgnoreCase);
            // contains 兑底（所有长度）
            if (!matches && effectivePrefix.Length >= 1)
            {
                matches = name.IndexOf(effectivePrefix, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        var status = matches == (expectedMatch == "YES") ? "OK  " : "FAIL";
        if (matches == (expectedMatch == "YES")) pass++; else fail++;
        Console.WriteLine($"[{status}] {label,-60} word=[{word}] obj=[{objFull}] -> matches={matches} expected={expectedMatch}");
    }

    static void Main() {
        // ===== DetectContext 27 个用例 =====
        Check("EXEC", 4, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "just EXEC");
        Check("EXEC ", 4, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC + space caret at 4");
        Check("EXEC ", 5, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC + space caret at 5");
        Check("EXEC s", 6, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC s caret at 6");
        Check("EXEC sp_helpdb", 14, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC sp_helpdb");
        Check("EXECUTE ", 7, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXECUTE + space");

        Check("SELECT", 6, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "just SELECT");
        Check("SELECT ", 7, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT + space");
        Check("SELECT *", 8, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * caret at 8");
        Check("SELECT * ", 9, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * + space");
        Check("SELECT * FROM T1 a WHERE", 24, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "... WHERE");
        Check("SELECT * FROM T1 a WHERE ", 25, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "... WHERE + space");

        Check("SELECT * FROM", 13, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "SELECT * FROM");
        Check("SELECT * FROM ", 14, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "SELECT * FROM + space");

        Check("SELECT * FROM T1", 16, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * FROM T1 caret at T1 end");
        Check("SELECT * FROM T1 a", 18, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * FROM T1 a caret at a end");
        Check("SELECT * FROM T1 a, T2", 22, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "FROM list with comma AfterColumn");
        Check("SELECT * FROM T1 a, T2 b", 24, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "mid FROM list b end (弹 T2 列名)");

        Check("", 0, SqlIntelliSenseProvider.SqlContextKind.Generic, "empty");
        Check("SELECT 1+", 10, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT 1+ caret at 10");
        Check("WHERE x = 1", 11, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "WHERE x = 1 (after 1)");
        Check("SELECT a.ID, a.NAME FROM T1 a WHERE a.", 38, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "WHERE a. (column context)");

        Check("JOIN", 4, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "just JOIN");
        Check("SELECT TOP 10", 14, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT TOP 10 (after 10)");
        Check("SELECT 1;", 10, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT 1; (after semi)");
        Check("SELECT * FROM dbo.S_SCM_SEORDER a WHERE a.", 42, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "after a.");
        Check("UPDATE", 6, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "just UPDATE");

        // ===== SqlAliasResolver 用例 =====
        CheckAlias("SELECT BI FROM S_SCM_SEORDER", "S_SCM_SEORDER", null, "S_SCM_SEORDER", "FROM S_SCM_SEORDER 无别名 → 表名末段作 alias");
        CheckAlias("SELECT * FROM dbo.T1", "T1", "dbo", "T1", "FROM dbo.T1 无别名 → 表名末段作 alias");
        CheckAlias("SELECT * FROM T1 a", "a", null, "T1", "FROM T1 a 有别名 a (回归用例)");
        CheckAlias("SELECT * FROM T1 a, T2 b", "a", null, "T1", "FROM T1 a, T2 b → a->T1 (回归)");
        CheckAlias("SELECT * FROM T1 a, T2 b", "b", null, "T2", "FROM T1 a, T2 b → b->T2 (回归)");
        CheckAlias("SELECT * FROM T1 a, T2", "T2", null, "T2", "FROM T1 a, T2 无别名 (T2 是表名末段)");

        // ===== EXEC 存储过程 prefix 匹配算法 =====
        // 模拟：proc 列表 = [dbo.S_SCM_TEST, dbo.S_SCM_HELLO, dbo.SP_OTHER, dbo.MyProc]
        // 陛下输入 EXEC S_ → 应匹配 S_ 开头的存储过程名
        CheckPrefix("S_", "dbo.S_SCM_TEST", "YES", "EXEC S_ 应匹配 dbo.S_SCM_TEST");
        CheckPrefix("S_", "dbo.SP_OTHER", "NO", "EXEC S_ 不应匹配 dbo.SP_OTHER");
        CheckPrefix("S_SCM_", "dbo.S_SCM_TEST", "YES", "EXEC S_SCM_ 应匹配 dbo.S_SCM_TEST");
        CheckPrefix("S_SCM_", "dbo.SP_OTHER", "NO", "EXEC S_SCM_ 不应匹配 dbo.SP_OTHER");
        CheckPrefix("", "dbo.S_SCM_TEST", "YES", "EXEC 空 应匹配所有");
        CheckPrefix("EXEC", "dbo.S_SCM_TEST", "YES", "EXEC EXEC 应匹配所有 (重置 prefix)");
        CheckPrefix("dbo.S_", "dbo.S_SCM_TEST", "YES", "EXEC dbo.S_ 应匹配 dbo.S_SCM_TEST (含 schema 点)");
        CheckPrefix("other.S_", "dbo.S_SCM_TEST", "NO", "EXEC other.S_ 不应匹配 dbo.S_SCM_TEST (schema 不匹配)");
        CheckPrefix("sp_helpdb", "dbo.sp_helpdb", "YES", "EXEC sp_helpdb 应匹配 dbo.sp_helpdb (陛下原 bug)");

        // 陛下反馈 2026-07-06：EXEC CR 弹、EXEC CRM 不弹 → contains 阈值问题
        CheckPrefix("CR", "dbo.APP_S_CRM", "YES", "EXEC CR (2 字符) 应包含匹配 dbo.APP_S_CRM");
        CheckPrefix("CRM", "dbo.APP_S_CRM", "YES", "EXEC CRM (3 字符) 应包含匹配 dbo.APP_S_CRM (陛下 bug)");
        CheckPrefix("CRM", "dbo.S_CRM_HELLO", "YES", "EXEC CRM 应包含匹配 dbo.S_CRM_HELLO");
        CheckPrefix("APP_S_", "dbo.APP_S_CRM", "YES", "EXEC APP_S_ 应 startsWith 匹配 dbo.APP_S_CRM");
        CheckPrefix("S_CR", "dbo.APP_S_CRM", "YES", "EXEC S_CR 应 contains 匹配 dbo.APP_S_CRM");
        CheckPrefix("xyz", "dbo.APP_S_CRM", "NO", "EXEC xyz 不应匹配 dbo.APP_S_CRM");

        Console.WriteLine();
        Console.WriteLine($"==== {pass} pass / {fail} fail ====");
    }
}
