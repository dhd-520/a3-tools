function Test-AfterExec {
    param($fullSql, $caret, $prefix)
    if ([string]::IsNullOrEmpty($fullSql)) { return $false }
    $beforeStart = $caret - $prefix.Length
    if ($beforeStart -le 0) { return $false }
    $before = $fullSql.Substring(0, $beforeStart)
    while ($before.Length -gt 0) {
        $c = $before[$before.Length - 1]
        if ([char]::IsWhiteSpace($c) -or $c -eq '(') { $before = $before.Substring(0, $before.Length - 1) } else { break }
    }
    if ($before.EndsWith("EXEC") -or $before.EndsWith("EXECUTE")) {
        $kwStart = if ($before.EndsWith("EXECUTE")) { $before.Length - 7 } else { $before.Length - 4 }
        if ($kwStart -eq 0) { return $true }
        $prev = $before[$kwStart - 1]
        return [char]::IsWhiteSpace($prev) -or $prev -eq '('
    }
    return $false
}

$cases = @(
    @{ sql='EXEC '; caret=4; prefix=''; expect=$true; desc='EXEC + space, prefix empty' },
    @{ sql='EXECUTE '; caret=7; prefix=''; expect=$true; desc='EXECUTE + space, prefix empty' },
    @{ sql='EXEC Cust'; caret=8; prefix='Cust'; expect=$true; desc='EXEC + partial proc name' },
    @{ sql='EXEC sp_'; caret=7; prefix='sp_'; expect=$true; desc='EXEC + sp_ prefix' },
    @{ sql='SELECT 1'; caret=8; prefix='1'; expect=$false; desc='after SELECT, not EXEC' },
    @{ sql='sp_executesql '; caret=14; prefix=''; expect=$false; desc='sp_executesql contains EXEC but preceded by sp_' },
    @{ sql='SELECT * FROM s WHERE x = 1; EXEC '; caret=34; prefix=''; expect=$true; desc='EXEC after semicolon' },
    @{ sql='EXEC(Cust'; caret=9; prefix='Cust'; expect=$true; desc='EXEC with paren before' }
)
foreach ($c in $cases) {
    $result = Test-AfterExec -fullSql $c.sql -caret $c.caret -prefix $c.prefix
    $status = if ($result -eq $c.expect) { 'OK' } else { 'FAIL' }
    Write-Host ("[{0}] {1} -> got={2} expected={3}" -f $status, $c.desc, $result, $c.expect)
}

Write-Host '----'
$regex = "(?ms),\s*(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+(?:OUTER\s+)?)?(?<obj>(?:\[[^\]]+\]|\w+)\.(?:\[[^\]]+\]|\w+)|\[[^\]]+\]|\w+)\s+(?:AS\s+)?(?<alias>\w+)\b"
$multi = @(
    'SELECT * FROM S_SCM_SEORDER a, dbo.S_CUSTOMER c',
    'SELECT * FROM T1 a, T2 b, T3 c',
    'SELECT * FROM T1 a LEFT JOIN T2 b ON a.id = b.id'
)
foreach ($m in $multi) {
    Write-Host "--- $m"
    $ms = [regex]::Matches($m, $regex, 'IgnoreCase')
    foreach ($mm in $ms) {
        Write-Host ("  obj={0} alias={1}" -f $mm.Groups['obj'].Value, $mm.Groups['alias'].Value)
    }
}