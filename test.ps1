Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
Write-Output "Count: $($sizes.Count)"
Write-Output "Sizes: $($sizes -join ',')"

$bmp = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 20, 60, 120))
$g.Dispose()
$bmp.Dispose()
Write-Output "Test passed"
