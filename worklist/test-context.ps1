function DetectContext([string]$fullSql, [int]$caret) {
    if ([string]::IsNullOrEmpty($fullSql)) { return 'Generic' }
    if ($caret -le 0 -or $caret -gt $fullSql.Length) { return 'Generic' }
    $i = $caret
    for ($round = 0; $round -lt 8; $round++) {
        while ($i -gt 0) {
            $c = $fullSql[$i - 1]
            if ([char]::IsWhiteSpace($c) -or $c -eq "`t" -or $c -eq "`r" -or $c -eq "`n") { $i-- } else { break }
        }
        if ($i -le 0) { return 'Generic' }
        $segEnd = $i
        $firstChar = $fullSql[$i - 1]
        if ($firstChar -eq ',') { $i = $segEnd - 1; continue }
        if ($firstChar -eq ';') { return 'Generic' }
        if ($firstChar -in @('+','-','=','>','<','!',')')) { $i = $segEnd - 1; continue }
        if ($firstChar -eq '*') { $i = $segEnd - 1; continue }
        if ($firstChar -eq '(') { $i = $segEnd - 1; continue }
        if ([char]::IsDigit($firstChar)) { $i = $segEnd - 1; continue }
        if ($firstChar -eq '.') { $i = $segEnd - 1; continue }
        $wordStart = $segEnd
        while ($wordStart -gt 0) {
            $c = $fullSql[$wordStart - 1]
            if ([char]::IsLetterOrDigit($c) -or $c -eq '_' -or $c -eq '@' -or $c -eq '#' -or $c -eq '.') { $wordStart-- } else { break }
        }
        $wordLen = $segEnd - $wordStart
        if ($wordLen -le 0) { return 'Generic' }
        $word = $fullSql.Substring($wordStart, $wordLen)
        if ($word -ieq 'EXEC' -or $word -ieq 'EXECUTE') {
            if ($wordStart -eq 0) { return 'AfterExec' }
            $prev = $fullSql[$wordStart - 1]
            if ([char]::IsWhiteSpace($prev) -or $prev -eq '(' -or $prev -eq ';') { return 'AfterExec' }
        }
        if ($word -ieq 'FROM' -or $word -ieq 'JOIN' -or $word -ieq 'APPLY' -or $word -ieq 'INTO' -or $word -ieq 'UPDATE' -or $word -ieq 'TABLE') {
            if ($wordStart -eq 0) { return 'AfterObjectKeyword' }
            $prev = $fullSql[$wordStart - 1]
            if ([char]::IsWhiteSpace($prev) -or $prev -eq '(' -or $prev -eq ';') { return 'AfterObjectKeyword' }
        }
        if ($word -ieq 'SELECT' -or $word -ieq 'WHERE' -or $word -ieq 'ON' -or $word -ieq 'HAVING' -or $word -ieq 'BY' -or $word -ieq 'AND' -or $word -ieq 'OR') {
            if ($wordStart -eq 0) { return 'AfterColumnKeyword' }
            $prev = $fullSql[$wordStart - 1]
            if ([char]::IsWhiteSpace($prev) -or $prev -eq '(' -or $prev -eq ';') { return 'AfterColumnKeyword' }
        }
        $i = $wordStart
    }
    return 'Generic'
}

$cases = @(
    @{ sql='EXEC'; caret=4; ctx='AfterExec'; label='just EXEC' },
    @{ sql='EXEC '; caret=4; ctx='AfterExec'; label='EXEC + space caret at 4' },
    @{ sql='EXEC '; caret=5; ctx='AfterExec'; label='EXEC + space caret at 5' },
    @{ sql='EXEC s'; caret=6; ctx='AfterExec'; label='EXEC s caret at 6' },
    @{ sql='EXEC sp_helpdb'; caret=14; ctx='AfterExec'; label='EXEC sp_helpdb' },
    @{ sql='EXECUTE '; caret=7; ctx='AfterExec'; label='EXECUTE + space' },
    @{ sql='SELECT * FROM T1 a'; caret=18; ctx='AfterColumnKeyword'; label='SELECT * FROM T1 a caret at a end' },
    @{ sql='SELECT'; caret=6; ctx='AfterColumnKeyword'; label='just SELECT' },
    @{ sql='SELECT '; caret=7; ctx='AfterColumnKeyword'; label='SELECT + space' },
    @{ sql='SELECT *'; caret=8; ctx='AfterColumnKeyword'; label='SELECT * caret at 8' },
    @{ sql='SELECT * '; caret=9; ctx='AfterColumnKeyword'; label='SELECT * + space' },
    @{ sql='SELECT * FROM T1 a WHERE'; caret=24; ctx='AfterColumnKeyword'; label='... WHERE' },
    @{ sql='SELECT * FROM T1 a WHERE '; caret=25; ctx='AfterColumnKeyword'; label='... WHERE + space' },
    @{ sql='SELECT * FROM'; caret=13; ctx='AfterObjectKeyword'; label='SELECT * FROM' },
    @{ sql='SELECT * FROM '; caret=14; ctx='AfterObjectKeyword'; label='SELECT * FROM + space' },
    @{ sql='SELECT * FROM T1'; caret=18; ctx='AfterColumnKeyword'; label='SELECT * FROM T1 caret at T1 end (now AfterColumn)' },
    @{ sql='SELECT * FROM T1 a, T2'; caret=24; ctx='AfterColumnKeyword'; label='FROM list with comma AfterColumn' },
    @{ sql='SELECT * FROM T1 a, T2 b'; caret=25; ctx='Generic'; label='mid FROM list no operator' },
    @{ sql=''; caret=0; ctx='Generic'; label='empty' },
    @{ sql='SELECT 1+'; caret=10; ctx='Generic'; label='SELECT 1+ caret at 10' },
    @{ sql='WHERE x = 1'; caret=11; ctx='AfterColumnKeyword'; label='WHERE x = 1 (after 1)' },
    @{ sql='SELECT a.ID, a.NAME FROM T1 a WHERE a.'; caret=39; ctx='AfterColumnKeyword'; label='WHERE a. (column context)' },
    @{ sql='JOIN'; caret=4; ctx='AfterObjectKeyword'; label='just JOIN' },
    @{ sql='SELECT TOP 10'; caret=14; ctx='Generic'; label='SELECT TOP 10 (after 10)' },
    @{ sql='SELECT 1;'; caret=10; ctx='Generic'; label='SELECT 1; (after semi)' },
    @{ sql='SELECT * FROM dbo.S_SCM_SEORDER a WHERE a.'; caret=43; ctx='AfterColumnKeyword'; label='after a.' },
    @{ sql='UPDATE'; caret=6; ctx='AfterObjectKeyword'; label='just UPDATE' }
)
foreach ($c in $cases) {
    $got = DetectContext $c.sql $c.caret
    $status = if ($got -eq $c.ctx) { 'OK' } else { 'FAIL' }
    Write-Host "[$status] $($c.label) -> got=$got expected=$($c.ctx)"
}