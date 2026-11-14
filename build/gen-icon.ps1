# 生成 WallpaperDeskClock.ico（多尺寸，PNG 压缩条目）
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File build\gen-icon.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root 'WallpaperDeskClock.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Draw-Icon([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    $rad = [Math]::Max(2.0, $s * 0.19)
    $path = RoundPath 0 0 $s $s $rad
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, $s, $s)),
        (C 255 59 130 246), (C 255 23 37 84),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($lg, $path)
    $lg.Dispose(); $path.Dispose()

    # 表盘
    $pad = $s * 0.21
    $dia = $s - 2 * $pad
    $w = [Math]::Max(1.0, $s * 0.078)
    $pen = New-Object System.Drawing.Pen((C 255 255 255 255), [float]$w)
    $g.DrawEllipse($pen, [float]$pad, [float]$pad, [float]$dia, [float]$dia)
    $pen.Dispose()

    # 指针：分针向上、时针指向右下
    $cx = $s / 2.0; $cy = $s / 2.0
    $pen2 = New-Object System.Drawing.Pen((C 255 255 255 255), [float]$w)
    $pen2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen2.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen2, [float]$cx, [float]$cy, [float]$cx, [float]($cy - $dia * 0.30))
    $g.DrawLine($pen2, [float]$cx, [float]$cy, [float]($cx + $dia * 0.26), [float]($cy + $dia * 0.11))
    $pen2.Dispose()
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
$bw.Write([uint16]0)                 # reserved
$bw.Write([uint16]1)                 # type = icon
$bw.Write([uint16]$sizes.Count)      # count

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim)            # width
    $bw.Write([byte]$dim)            # height
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([uint16]1)             # planes
    $bw.Write([uint16]32)            # bpp
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output ('icon: {0}  {1} KB  sizes={2}' -f (Split-Path -Leaf $icoPath), [math]::Round((Get-Item $icoPath).Length / 1KB, 1), ($sizes -join ','))
