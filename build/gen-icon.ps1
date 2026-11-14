# 生成 WallpaperDeskClock.ico（多尺寸，PNG 压缩条目）
# 设计：圆角方胶囊 + 蓝紫渐变 + 顶部高光 + 白色表盘 + 10:10 指针 + 中心点
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root 'WallpaperDeskClock.ico'
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    if ($d -le 0 -or $d -ge $w -or $d -ge $h) { $p.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h))); return $p }
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

    # 底：圆角方胶囊 + 对角渐变
    $rad = [float]($s * 0.245)
    $body = RoundPath 0 0 $s $s $rad
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, $s, $s)),
        (C 255 74 142 255), (C 255 24 48 132),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($lg, $body)
    $lg.Dispose()

    # 顶部高光（只在上半部）
    $g.SetClip($body)
    $hlRect = New-Object System.Drawing.Rectangle(0, 0, $s, [int]($s * 0.55))
    $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $hlRect, (C 55 255 255 255), (C 0 255 255 255),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($hl, $hlRect)
    $hl.Dispose()
    $g.ResetClip()

    # 外圈描边（大尺寸才加，保证小图标干净）
    if ($s -ge 32) {
        $rim = New-Object System.Drawing.Pen((C 40 255 255 255), [float][Math]::Max(1, $s * 0.012))
        $g.DrawPath($rim, $body)
        $rim.Dispose()
    }

    # 表盘
    $pad = [float]($s * 0.245)
    $dia = [float]($s - 2 * $pad)
    $w = [float][Math]::Max(1.2, $s * 0.062)
    $pen = New-Object System.Drawing.Pen((C 255 255 255 255), $w)
    $g.DrawEllipse($pen, $pad, $pad, $dia, $dia)

    # 指针：分针指向 12 点，时针指向 3 点（小尺寸下轮廓最清晰的组合）
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cx = [float]($s / 2.0)
    $cy = [float]($s / 2.0)
    $minLen = [float]($dia * 0.40)
    $hourLen = [float]($dia * 0.27)

    $g.DrawLine($pen, $cx, $cy, $cx, [float]($cy - $minLen))
    $g.DrawLine($pen, $cx, $cy, [float]($cx + $hourLen), $cy)
    $pen.Dispose()

    # 中心点
    $d2 = [float]($w * 1.15)
    $b = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
    $g.FillEllipse($b, [float]($cx - $d2 / 2), [float]($cy - $d2 / 2), $d2, $d2)
    $b.Dispose()

    $body.Dispose()
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
