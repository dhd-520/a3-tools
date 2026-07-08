using System;
using A3Tools.Plugins.Default.Forms;

class TestContext {
    static int pass = 0, fail = 0;
    
    static void Check(string sql, int caret, SqlIntelliSenseProvider.SqlContextKind expected, string label) {
        var got = SqlIntelliSenseProvider.DetectContext(sql, caret);
        var status = got == expected ? "OK" : "FAIL";
        if (got == expected) pass++; else fail++;
        Console.WriteLine($"[{status}] {label} sql=[{sql}] caret={caret} -> got={got} expected={expected}");
    }
    
    static void Main() {
        Check("EXEC", 4, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "just EXEC");
        Check("EXEC ", 4, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC + space caret at 4");
        Check("EXEC ", 5, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC + space caret at 5");
        Check("EXEC s", 6, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC s caret at 6");
        Check("EXEC sp_helpdb", 14, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXEC sp_helpdb");
        Check("EXECUTE ", 7, SqlIntelliSenseProvider.SqlContextKind.AfterExec, "EXECUTE + space");
        Check("SELECT * FROM T1 a", 18, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * FROM T1 a caret at a end");
        Check("SELECT", 6, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "just SELECT");
        Check("SELECT ", 7, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT + space");
        Check("SELECT *", 8, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * caret at 8");
        Check("SELECT * ", 9, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * + space");
        Check("SELECT * FROM T1 a WHERE", 24, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "... WHERE");
        Check("SELECT * FROM T1 a WHERE ", 25, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "... WHERE + space");
        Check("SELECT * FROM", 13, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "SELECT * FROM");
        Check("SELECT * FROM ", 14, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "SELECT * FROM + space");
        Check("SELECT * FROM T1", 18, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "SELECT * FROM T1 caret at T1 end");
        Check("SELECT * FROM T1 a, T2", 24, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "FROM list with comma AfterColumn");
        Check("SELECT * FROM T1 a, T2 b", 25, SqlIntelliSenseProvider.SqlContextKind.Generic, "mid FROM list no operator");
        Check("", 0, SqlIntelliSenseProvider.SqlContextKind.Generic, "empty");
        Check("SELECT 1+", 10, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT 1+ caret at 10");
        Check("WHERE x = 1", 11, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "WHERE x = 1 (after 1)");
        Check("SELECT a.ID, a.NAME FROM T1 a WHERE a.", 39, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "WHERE a. (column context)");
        Check("JOIN", 4, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "just JOIN");
        Check("SELECT TOP 10", 14, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT TOP 10 (after 10)");
        Check("SELECT 1;", 10, SqlIntelliSenseProvider.SqlContextKind.Generic, "SELECT 1; (after semi)");
        Check("SELECT * FROM dbo.S_SCM_SEORDER a WHERE a.", 43, SqlIntelliSenseProvider.SqlContextKind.AfterColumnKeyword, "after a.");
        Check("UPDATE", 6, SqlIntelliSenseProvider.SqlContextKind.AfterObjectKeyword, "just UPDATE");
        
        Console.WriteLine();
        Console.WriteLine($"==== {pass} pass / {fail} fail ====");
    }
}