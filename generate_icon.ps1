Add-Type -AssemblyName System.Drawing

$szList = @(16, 24, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.ArrayList
$imgDataList = New-Object System.Collections.ArrayList

foreach ($sz in $szList) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $sz / 256.0

    # Deep blue circle background
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 10, 50, 120))
    $g.FillEllipse($bgBrush, 0, 0, $sz-1, $sz-1)
    $bgBrush.Dispose()

    # === BIG WRENCH - thick white diagonal ===
    $wpen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 248, 255), [int][Math]::Max(5, 24*$s))
    $wpen.StartCap = 'Round'
    $wpen.EndCap = 'Round'
    $wpen.LineJoin = 'Round'

    # Main diagonal handle
    $g.DrawLine($wpen, [int](55*$s), [int](210*$s), [int](200*$s), [int](50*$s))
    $wpen.Dispose()

    # === Wrench head at top-right (big open C-jaw) ===
    $jawPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 248, 255), [int][Math]::Max(5, 24*$s))
    $jawPen.StartCap = 'Round'
    $jawPen.EndCap = 'Round'
    # Outer arc
    $g.DrawArc($jawPen, [int](172*$s), [int](28*$s), [int](58*$s), [int](58*$s), 210, 130)
    $jawPen.Dispose()

    # === Inner jaw cutout (draw opposite arc to create C-shape) ===
    $innerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 10, 50, 120), [int][Math]::Max(3, 14*$s))
    $innerPen.StartCap = 'Round'
    $innerPen.EndCap = 'Round'
    $g.DrawArc($innerPen, [int](183*$s), [int](38*$s), [int](36*$s), [int](36*$s), 225, 110)
    $innerPen.Dispose()

    # === Wrench bottom ring ===
    $ringPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 248, 255), [int][Math]::Max(4, 20*$s))
    $ringPen.StartCap = 'Round'
    $ringPen.EndCap = 'Round'
    $ringRect = New-Object System.Drawing.Rectangle([int](40*$s), [int](200*$s), [int](40*$s), [int](40*$s))
    $g.DrawArc($ringPen, $ringRect, 45, 270)
    $ringPen.Dispose()

    # === Gold star/sparkle accent ===
    $goldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 210, 40))
    $sparkS = [int][Math]::Max(3, 10*$s)
    $g.FillEllipse($goldBrush, [int](16*$s), [int](16*$s), $sparkS, $sparkS)
    $goldBrush.Dispose()

    # === A3Tool text - LARGE, bold, gold ===
    $fontSize = [int][Math]::Max(8, 38*$s)
    $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
    $goldBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 210, 55))
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160, 0, 0, 0))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'
    $sf.LineAlignment = 'Center'

    $tRectS = New-Object System.Drawing.RectangleF(0, [int](203*$s), $sz, [int](52*$s))
    $g.DrawString("A3Tool", $font, $shadowBrush, ([float](0.8*$s)), ([float](205*$s)), $sf)
    $g.DrawString("A3Tool", $font, $goldBrush2, $tRectS, $sf)

    $shadowBrush.Dispose()
    $goldBrush2.Dispose()
    $font.Dispose()
    $sf.Dispose()
    $g.Dispose()
    [void]$images.Add($bmp)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$imgDataList.Add($ms.ToArray())
    $ms.Dispose()
}

$images[6].Save("D:\work\A3Tools\A3Tools\A3Tool.png", [System.Drawing.Imaging.ImageFormat]::Png)

$icoPath = "D:\work\A3Tools\A3Tools\A3Tool.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$szList.Count)
$offset = 6 + ($szList.Count * 16)

for ($i = 0; $i -lt $szList.Count; $i++) {
    $sz = $szList[$i]
    $data = $imgDataList[$i]
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
for ($i = 0; $i -lt $szList.Count; $i++) {
    $bw.Write($imgDataList[$i])
}
$bw.Close()
$fs.Close()
foreach ($b in $images) { $b.Dispose() }

Write-Output "Done"
Get-ChildItem D:\work\A3Tools\A3Tools\A3Tool.*
