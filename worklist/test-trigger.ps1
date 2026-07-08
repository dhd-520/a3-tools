function IsAfterExecContext([string]$fullSql, [int]$caret) {
    if ([string]::IsNullOrEmpty($fullSql)) { return $false }
    if ($caret -le 0 -or $caret -gt $fullSql.Length) { return $false }
    $i = $caret
    for ($round = 0; $round -lt 2; $round++) {
        while ($i -gt 0) {
            $c = $fullSql[$i - 1]
            if ([char]::IsWhiteSpace($c) -or $c -eq '(' -or $c -eq "`t" -or $c -eq "`r" -or $c -eq "`n") {
                $i--
            } else { break }
        }
        if ($i -le 0) { return $false }
        $wordEnd = $i
        $wordStart = $wordEnd
        while ($wordStart -gt 0) {
            $c = $fullSql[$wordStart - 1]
            if ([char]::IsLetterOrDigit($c) -or $c -eq '_' -or $c -eq '@' -or $c -eq '#') {
                $wordStart--
            } else { break }
        }
        $wordLen = $wordEnd - $wordStart
        if ($wordLen -le 0) { return $false }
        $word = $fullSql.Substring($wordStart, $wordLen)
        if ($word -ieq 'EXEC' -or $word -ieq 'EXECUTE') {
            if ($wordStart -eq 0) { return $true }
            $prev = $fullSql[$wordStart - 1]
            return [char]::IsWhiteSpace($prev) -or $prev -eq '(' -or $prev -eq ';'
        }
        if ($round -eq 1) { return $false }
        $i = $wordStart
    }
    return $false
}

$cases = @(
    @{ sql='EXEC'; caret=4; expect=$true; label='just EXEC' },
    @{ sql='EXEC '; caret=4; expect=$true; label='EXEC + space' },
    @{ sql='EXEC '; caret=5; expect=$true; label='EXEC + space + caret after' },
    @{ sql='EXEC s'; caret=5; expect=$true; label='EXEC + space + caret before s' },
    @{ sql='EXEC s'; caret=6; expect=$true; label='EXEC s caret after s' },
    @{ sql='EXEC sp_'; caret=8; expect=$true; label='EXEC sp_' },
    @{ sql='EXEC sp_helpdb'; caret=14; expect=$true; label='EXEC sp_helpdb' },
    @{ sql='EXECUTE '; caret=7; expect=$true; label='EXECUTE' },
    @{ sql='SELECT '; caret=7; expect=$false; label='SELECT' },
    @{ sql='sp_executesql '; caret=14; expect=$false; label='sp_executesql' },
    @{ sql='SELECT 1; EXEC'; caret=14; expect=$true; label='after semi' },
    @{ sql='SELECT * FROM T1; EXEC'; caret=22; expect=$true; label='after from + semi' },
    @{ sql='SELECT * FROM T1 a, T2 b'; caret=24; expect=$false; label='FROM clause' }
)
foreach ($c in $cases) {
    $got = IsAfterExecContext $c.sql $c.caret
    $status = if ($got -eq $c.expect) { 'OK' } else { 'FAIL' }
    Write-Host "[$status] $($c.label) sql=[$($c.sql)] caret=$($c.caret) -> got=$got expected=$($c.expect)"
}