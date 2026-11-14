# 生成 WallpaperDeskClock.ico（多尺寸，PNG 压缩条目）
# 设计：深色胶囊 + 白色沙漏轮廓 + 蓝色沙堆（时间流逝）
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root 'WallpaperDeskClock.ico'
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function Squircle([float]$s, [float]$rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $p.AddArc(0, 0, $d, $d, 180, 90)
    $p.AddArc($s - $d, 0, $d, $d, 270, 90)
    $p.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $p.AddArc(0, $s - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Draw-Icon([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # 底：深色胶囊 + 竖向渐变
    $body = Squircle $s ($s * 0.235)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, $s, $s)),
        (C 255 26 36 62), (C 255 11 17 32),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($lg, $body)
    $lg.Dispose()
    $g.SetClip($body)

    # 几何
    $cx = [float]($s / 2.0)
    $top = [float]($s * 0.20)
    $bot = [float]($s * 0.80)
    $mid = [float]($s * 0.50)
    $halfW = [float]($s * 0.205)
    $neck = [float]($halfW * 0.13)
    $w = [float][Math]::Max(1.15, $s * 0.038)

    # 沙（下半三角）
    $sand = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sand.AddPolygon(@(
        (New-Object System.Drawing.PointF([float]($cx - $halfW * 0.88), [float]($bot - $s * 0.015))),
        (New-Object System.Drawing.PointF([float]($cx + $halfW * 0.88), [float]($bot - $s * 0.015))),
        (New-Object System.Drawing.PointF($cx, [float]($mid + $s * 0.15)))))
    $g.FillPath((New-Object System.Drawing.SolidBrush((C 255 92 170 255))), $sand)
    $sand.Dispose()

    # 玻璃轮廓
    $glass = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glass.AddPolygon(@(
        (New-Object System.Drawing.PointF([float]($cx - $halfW), $top)),
        (New-Object System.Drawing.PointF([float]($cx + $halfW), $top)),
        (New-Object System.Drawing.PointF([float]($cx + $neck), $mid)),
        (New-Object System.Drawing.PointF([float]($cx + $halfW), $bot)),
        (New-Object System.Drawing.PointF([float]($cx - $halfW), $bot)),
        (New-Object System.Drawing.PointF([float]($cx - $neck), $mid))))
    $pen = New-Object System.Drawing.Pen((C 255 255 255 255), $w)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $glass)
    $pen.Dispose()
    $glass.Dispose()

    # 上下盖
    $capW = [float]($halfW * 2 + $s * 0.055)
    $capH = [float][Math]::Max(1.4, $s * 0.042)
    $cap = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
    $g.FillRectangle($cap, [float]($cx - $capW / 2), [float]($top - $capH * 1.05), $capW, $capH)
    $g.FillRectangle($cap, [float]($cx - $capW / 2), [float]($bot + $capH * 0.05), $capW, $capH)
    $cap.Dispose()

    $g.ResetClip()
    $g.Dispose()
    $bmp
}

$pngs = @()
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output ('icon: {0}  {1} KB  sizes={2}' -f (Split-Path -Leaf $icoPath), [math]::Round((Get-Item $icoPath).Length / 1KB, 1), ($sizes -join ','))
