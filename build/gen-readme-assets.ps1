Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root 'docs'
New-Item -ItemType Directory -Force -Path $out | Out-Null

function C([int]$a,[int]$r,[int]$g,[int]$b){ [System.Drawing.Color]::FromArgb($a,$r,$g,$b) }
function F([string]$name,[float]$px,[string]$style){
  $st = [System.Drawing.FontStyle]::Regular
  if($style -eq 'b'){ $st = [System.Drawing.FontStyle]::Bold }
  New-Object System.Drawing.Font($name,$px,$st,[System.Drawing.GraphicsUnit]::Pixel)
}
function New-Canvas([int]$w,[int]$h){
  $bmp = New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  return @($bmp,$g)
}
function RoundPath([float]$x,[float]$y,[float]$w,[float]$h,[float]$r){
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r*2
  $p.AddArc($x,$y,$d,$d,180,90)
  $p.AddArc($x+$w-$d,$y,$d,$d,270,90)
  $p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90)
  $p.AddArc($x,$y+$h-$d,$d,$d,90,90)
  $p.CloseFigure()
  $p
}
function Draw-Cover($g,$img,[float]$x,[float]$y,[float]$w,[float]$h){
  $s = [Math]::Max($w/$img.Width, $h/$img.Height)
  $nw = [int][Math]::Round($img.Width*$s)
  $nh = [int][Math]::Round($img.Height*$s)
  $rect = New-Object System.Drawing.Rectangle([int]($x+($w-$nw)/2),[int]($y+($h-$nh)/2),$nw,$nh)
  $g.DrawImage($img,$rect)
}
function Radial($g,[float]$cx,[float]$cy,[float]$rad,[System.Drawing.Color]$col,[float]$focus){
  $rect = New-Object System.Drawing.RectangleF(($cx-$rad),($cy-$rad),($rad*2),($rad*2))
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddEllipse($rect)
  $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
  $pgb.CenterColor = $col
  $pgb.SurroundColors = @((C 0 $col.R $col.G $col.B))
  $pgb.FocusScales = New-Object System.Drawing.PointF($focus,$focus)
  $g.FillPath($pgb,$path)
  $pgb.Dispose(); $path.Dispose()
}

# ---------------------------------------------------------------- 0. 演示壁纸
$bmp,$g = New-Canvas 1920 1080
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Rectangle(0,0,1920,1080)), (C 255 10 14 30), (C 255 22 30 56),
  [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$g.FillRectangle($bg,0,0,1920,1080); $bg.Dispose()
Radial $g 1560 240 760 (C 120 42 108 224) 0.35
Radial $g 320 880 700 (C 95 116 62 214) 0.4
Radial $g 980 620 520 (C 60 30 190 190) 0.5
$grid = New-Object System.Drawing.Pen((C 14 255 255 255),1)
for($i=0;$i -le 1920;$i+=60){ $g.DrawLine($grid,$i,0,$i,1080) }
for($j=0;$j -le 1080;$j+=60){ $g.DrawLine($grid,0,$j,1920,$j) }
$grid.Dispose()
$g.Dispose()
$demoPath = Join-Path $out 'demo-wallpaper.png'
$bmp.Save($demoPath,[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$demo = [System.Drawing.Image]::FromFile($demoPath)

# 复刻 ClockWindow.Render() 的文字排布
function Draw-ClockBlock($g,[System.Drawing.RectangleF]$win,[string]$time,[string]$date,[string]$week,[string]$sec,[float]$k){
  $w = $win.Width; $h = $win.Height
  $rightX = $win.X + $w*0.831
  $clockY = $win.Y + $h*0.323
  $dateY  = $win.Y + $h*0.695
  $gapSec = 14*$k; $gapWeek = 26*$k; $secDy = 10*$k
  $col = C 255 242 242 242
  $fClock = F 'Bahnschrift' (64*$k) ''
  $fDate  = F 'Consolas'    (18*$k) ''
  $fWeek  = F 'SimHei'      (18*$k) ''
  $fSec   = F 'Consolas'    (18*$k) ''

  $fmt = New-Object System.Drawing.StringFormat
  $fmt.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

  function Txt($text,$font,$x,$y,$align){
    $fmt.Alignment = $align
    $boxW = [Math]::Max(420*$k, $font.Size*$text.Length*1.8)
    $boxH = $font.Size*2.4
    if($align -eq [System.Drawing.StringAlignment]::Far){ $rx = $x-$boxW } else { $rx = $x }
    $r = New-Object System.Drawing.RectangleF([float]$rx,[float]($y-$boxH/2),[float]$boxW,[float]$boxH)
    $off = [Math]::Max(1.5,$font.Size/24)
    $sb = New-Object System.Drawing.SolidBrush((C 175 0 0 0))
    $r2 = $r; $r2.Offset($off,$off)
    $g.DrawString($text,$font,$sb,$r2,$fmt); $sb.Dispose()
    $b = New-Object System.Drawing.SolidBrush($col)
    $g.DrawString($text,$font,$b,$r,$fmt); $b.Dispose()
  }

  $dateSize = $g.MeasureString($date,$fDate,(New-Object System.Drawing.PointF(0,0)),[System.Drawing.StringFormat]::GenericTypographic)
  Txt $date $fDate $rightX $dateY ([System.Drawing.StringAlignment]::Far)
  Txt $week $fWeek ($rightX-$dateSize.Width-$gapWeek) $dateY ([System.Drawing.StringAlignment]::Far)
  Txt $time $fClock $rightX $clockY ([System.Drawing.StringAlignment]::Far)
  Txt $sec $fSec ($rightX+$gapSec) ($clockY+$secDy) ([System.Drawing.StringAlignment]::Near)

  $fClock.Dispose();$fDate.Dispose();$fWeek.Dispose();$fSec.Dispose();$fmt.Dispose()
}

# ---------------------------------------------------------------- 1. 桌面预览
$bmp,$g = New-Canvas 1920 1080
Draw-Cover $g $demo 0 0 1920 1080
Radial $g 1560 900 700 (C 78 0 0 0) 0.45
$winRect = New-Object System.Drawing.RectangleF([float](1920*0.575),[float](1080*0.790),[float](1920*0.425),[float](1080*0.210))
Draw-ClockBlock $g $winRect '23:47' '2026.11.14' '星期六' '08' 1.0
$g.Dispose()
$bmp.Save((Join-Path $out 'preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$prev = [System.Drawing.Bitmap]::FromFile((Join-Path $out 'preview.png'))

# ---------------------------------------------------------------- 2. 局部特写
$crop = New-Object System.Drawing.Rectangle(1360,820,560,260)
$dest = New-Object System.Drawing.Rectangle(0,0,1120,520)
$bmp,$g = New-Canvas 1120 520
$g.DrawImage($prev,$dest,$crop,[System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$bmp.Save((Join-Path $out 'preview-zoom.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ---------------------------------------------------------------- 3. 编辑模式
$crop = New-Object System.Drawing.Rectangle(1360, 820, 560, 260)
$bmp,$g = New-Canvas 1120 520
$prev2 = [System.Drawing.Bitmap]::FromFile((Join-Path $out 'preview.png'))
$g.DrawImage($prev2, (New-Object System.Drawing.Rectangle(0, 0, 1120, 520)), $crop, [System.Drawing.GraphicsUnit]::Pixel)
$prev2.Dispose()

# 编辑态的淡底色（对应 alpha 14）
$tint = New-Object System.Drawing.SolidBrush((C 20 70 100 170))
$wx = (1104 - 1360) * 2; $wy = (853 - 820) * 2
$g.FillRectangle($tint, $wx, $wy, 816 * 2, 227 * 2); $tint.Dispose()

# 窗口边框
$pen = New-Object System.Drawing.Pen((C 170 90 180 255), 4)
$g.DrawRectangle($pen, $wx + 2, $wy + 2, 816 * 2 - 4, 227 * 2 - 4); $pen.Dispose()

# 时间 / 日期块
$fC = F 'Bahnschrift' 128 ''
$fD = F 'Consolas' 36 ''
$fW = F 'SimHei' 36 ''
$fS = F 'Consolas' 36 ''
$sf = [System.Drawing.StringFormat]::GenericTypographic
$p0 = New-Object System.Drawing.PointF(0, 0)
$ts = $g.MeasureString('23:47', $fC, $p0, $sf)
$ss = $g.MeasureString('08', $fS, $p0, $sf)
$ds = $g.MeasureString('2026.11.14', $fD, $p0, $sf)
$ws = $g.MeasureString('星期六', $fW, $p0, $sf)

$rightX = (1104 + 0.831 * 816 - 1360) * 2
$clockY = (853 + 0.323 * 227 - 820) * 2
$dateY = (853 + 0.695 * 227 - 820) * 2
$gapSec = 14 * 2; $gapWeek = 26 * 2

$timeRect = New-Object System.Drawing.RectangleF(
  [float]($rightX - $ts.Width), [float]($clockY - $ts.Height / 2),
  [float]($ts.Width + $gapSec + $ss.Width), [float]$ts.Height)
$dateRect = New-Object System.Drawing.RectangleF(
  [float]($rightX - $ds.Width - $gapWeek - $ws.Width), [float]($dateY - $ds.Height / 2),
  [float]($ds.Width + $gapWeek + $ws.Width), [float]$ds.Height)

function BlockBox($rect, [string]$label, $color) {
    $box = [System.Drawing.RectangleF]::Inflate($rect, 12, 8)
    $p = New-Object System.Drawing.Pen($color, 2.5)
    $p.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawRectangle($p, $box.X, $box.Y, $box.Width, $box.Height)
    $p.Dispose()
    $f = F 'Microsoft YaHei' 22 ''
    $b = New-Object System.Drawing.SolidBrush($color)
    $ly = $box.Y - 26
    if ($ly -lt 0) { $ly = $box.Y + 3 }
    $g.DrawString($label, $f, $b, ($box.X + 5), $ly)
    $f.Dispose(); $b.Dispose()
}

BlockBox $timeRect '时间' (C 200 90 180 255)
BlockBox $dateRect '日期' (C 200 110 200 140)

$fC.Dispose(); $fD.Dispose(); $fW.Dispose(); $fS.Dispose()
$g.Dispose()
$bmp.Save((Join-Path $out 'edit-mode.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ---------------------------------------------------------------- 4. Hero 横幅
$bmp,$g = New-Canvas 1280 620
Draw-Cover $g $demo 0 0 1280 620
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Rectangle(0,0,1280,620)),
  (C 246 8 10 22), (C 30 8 10 22), [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
$blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
$blend.Colors = @((C 250 6 8 18),(C 220 6 8 18),(C 60 6 8 18))
$blend.Positions = @([float]0,[float]0.52,[float]1)
$lg.InterpolationColors = $blend
$g.FillRectangle($lg,0,0,1280,620)
$lg.Dispose()

$cardW = 470; $cardH = 300; $cardX = 752; $cardY = 160
$shadow = New-Object System.Drawing.SolidBrush((C 110 0 0 0))
$sp = RoundPath ($cardX+8) ($cardY+12) $cardW $cardH 18
$g.FillPath($shadow,$sp); $sp.Dispose(); $shadow.Dispose()
$cp = RoundPath $cardX $cardY $cardW $cardH 18
$g.SetClip($cp)
$src = New-Object System.Drawing.Rectangle(1430,800,438,280)
$dst = New-Object System.Drawing.Rectangle($cardX,$cardY,$cardW,$cardH)
$g.DrawImage($prev,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel)
$g.ResetClip()
$bPen = New-Object System.Drawing.Pen((C 90 255 255 255),2)
$g.DrawPath($bPen,$cp); $bPen.Dispose(); $cp.Dispose()

$fTitle = F 'Microsoft YaHei' 60 'b'
$bTitle = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
$g.DrawString('壁纸桌面时钟',$fTitle,$bTitle,58,124)
$fSub = F 'Microsoft YaHei' 25 ''
$bSub = New-Object System.Drawing.SolidBrush((C 210 226 232 245))
$g.DrawString('WallpaperDeskClock · 把时间和日期真正画进桌面壁纸',$fSub,$bSub,62,226)
$fTag = F 'Consolas' 23 ''
$bTag = New-Object System.Drawing.SolidBrush((C 165 150 200 255))
$g.DrawString('C# / WinForms / UpdateLayeredWindow / 零第三方依赖',$fTag,$bTag,62,278)

$chips = @('多显示器','点击穿透','不抢焦点','单文件 exe','约 0% CPU','任务栏入口')
$cx = 62; $cy = 348
$fChip = F 'Microsoft YaHei' 19 ''
foreach($c in $chips){
  $sz = $g.MeasureString($c,$fChip)
  $cw = $sz.Width + 30
  if($cx + $cw -gt 700){ $cx = 62; $cy += 56 }
  $cp2 = RoundPath $cx $cy $cw 42 21
  $cb = New-Object System.Drawing.SolidBrush((C 40 255 255 255))
  $g.FillPath($cb,$cp2)
  $pb = New-Object System.Drawing.Pen((C 64 255 255 255),1)
  $g.DrawPath($pb,$cp2)
  $tb = New-Object System.Drawing.SolidBrush((C 235 240 245 255))
  $g.DrawString($c,$fChip,$tb,($cx+15),($cy+10))
  $cb.Dispose();$pb.Dispose();$tb.Dispose();$cp2.Dispose()
  $cx += $cw + 12
}
$fChip.Dispose();$fTitle.Dispose();$bTitle.Dispose();$fSub.Dispose();$bSub.Dispose();$fTag.Dispose();$bTag.Dispose()
$g.Dispose()
$bmp.Save((Join-Path $out 'hero.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ---------------------------------------------------------------- 5. 设置面板
$bmp,$g = New-Canvas 900 1000
$bg2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Rectangle(0,0,900,1000)), (C 255 234 240 250), (C 255 212 222 240),
  [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillRectangle($bg2,0,0,900,1000); $bg2.Dispose()

$dx = 60; $dy = 50; $dw = 780; $dh = 900
$sh = New-Object System.Drawing.SolidBrush((C 40 20 40 80))
$ps = RoundPath ($dx+6) ($dy+12) $dw $dh 16
$g.FillPath($sh,$ps); $ps.Dispose(); $sh.Dispose()
$card = RoundPath $dx $dy $dw $dh 16
$cb2 = New-Object System.Drawing.SolidBrush((C 255 250 250 252))
$g.FillPath($cb2,$card); $cb2.Dispose()
$bp = New-Object System.Drawing.Pen((C 255 226 230 238),1)
$g.DrawPath($bp,$card); $bp.Dispose()
$g.SetClip($card)
$tb2 = New-Object System.Drawing.SolidBrush((C 255 243 245 250))
$g.FillRectangle($tb2,$dx,$dy,$dw,58); $tb2.Dispose()
$g.ResetClip()
$fWin = F 'Microsoft YaHei' 20 ''
$bWin = New-Object System.Drawing.SolidBrush((C 255 60 64 74))
$g.DrawString('壁纸桌面时钟',$fWin,$bWin,($dx+20),($dy+16))
$g.DrawString('✕',$fWin,$bWin,($dx+$dw-34),($dy+16))
$fWin.Dispose(); $bWin.Dispose()

$k = 1.7; $ox = $dx + 22; $oy = $dy + 74
$fTitle2 = F 'Microsoft YaHei' (18.7*$k) 'b'
$bT = New-Object System.Drawing.SolidBrush((C 255 24 26 32))
$g.DrawString('壁纸桌面时钟',$fTitle2,$bT,($ox+2),$oy)
$fTitle2.Dispose(); $bT.Dispose()

$fBody = F 'Microsoft YaHei' (12.7*$k) ''
$fSmall = F 'Microsoft YaHei' (11.5*$k) ''
$bBody = New-Object System.Drawing.SolidBrush((C 255 32 34 40))
$bGray = New-Object System.Drawing.SolidBrush((C 255 110 110 118))
$bBlue = New-Object System.Drawing.SolidBrush((C 255 40 110 220))
$bRed = New-Object System.Drawing.SolidBrush((C 255 190 60 60))

function Chk([float]$x,[float]$y,[string]$text,[bool]$on){
  $s = 17*$k
  $r = RoundPath $x $y $s $s 3
  $fill = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
  $g.FillPath($fill,$r); $fill.Dispose()
  $pen = New-Object System.Drawing.Pen((C 255 140 145 158),1.4)
  $g.DrawPath($pen,$r); $pen.Dispose(); $r.Dispose()
  if($on){
    $pen2 = New-Object System.Drawing.Pen((C 255 40 110 220),2.6)
    $g.DrawLines($pen2,@(
      (New-Object System.Drawing.PointF(($x+$s*0.22),($y+$s*0.52))),
      (New-Object System.Drawing.PointF(($x+$s*0.42),($y+$s*0.72))),
      (New-Object System.Drawing.PointF(($x+$s*0.80),($y+$s*0.26)))))
    $pen2.Dispose()
  }
  $g.DrawString($text,$fBody,$bBody,($x+$s+12),($y-2))
}

$y = $oy + 52*$k
Chk $ox $y '开机自动启动（默认关闭）' $false
$y += 34*$k
$sep = New-Object System.Drawing.Pen((C 255 224 227 235),1)
$g.DrawLine($sep,$ox,($y-6),($ox+390*$k),($y-6)); $sep.Dispose()
$y += 14*$k
$g.DrawString('显示位置',$fBody,$bBody,$ox,($y-6))
$y += 26*$k
$combo = RoundPath $ox $y (380*$k) (26*$k) 4
$cf = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
$g.FillPath($cf,$combo); $cf.Dispose()
$cpen = New-Object System.Drawing.Pen((C 255 160 166 180),1.2)
$g.DrawPath($cpen,$combo); $cpen.Dispose(); $combo.Dispose()
$g.DrawString('全部显示器',$fBody,$bBody,($ox+10),($y+5*$k))
$g.DrawString('▾',$fBody,$bGray,($ox+380*$k-26),($y+3*$k))
$y += 36*$k
Chk $ox $y '自定义布局模式（勾选后可用鼠标拖动时钟）' $false
$y += 30*$k
$g.DrawString('提示：编辑模式下时钟窗口会置顶并显示蓝色边框；',$fSmall,$bGray,($ox+20*$k),($y-4))
$g.DrawString('拖动结束后取消勾选即保存位置。',$fSmall,$bGray,($ox+20*$k),($y+22))
$y += 56*$k
$sep2 = New-Object System.Drawing.Pen((C 255 224 227 235),1)
$g.DrawLine($sep2,$ox,($y-14),($ox+390*$k),($y-14)); $sep2.Dispose()
$y += 6*$k
$g.DrawString('字号 / 缩放',$fBody,$bBody,$ox,($y-6))
$fVal = F 'Microsoft YaHei' (13.3*$k) 'b'
$g.DrawString('1.00×',$fVal,$bBlue,($ox+300*$k),($y-6))
$fVal.Dispose()
$y += 30*$k
$track = New-Object System.Drawing.Pen((C 255 205 210 222),4)
$g.DrawLine($track,$ox,($y+8),($ox+390*$k),($y+8)); $track.Dispose()
$fillp = New-Object System.Drawing.Pen((C 255 40 110 220),4)
$g.DrawLine($fillp,$ox,($y+8),($ox+150*$k),($y+8)); $fillp.Dispose()
$thumb = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
$g.FillEllipse($thumb,($ox+150*$k-9),($y-1),18,18)
$tpen = New-Object System.Drawing.Pen((C 255 40 110 220),1.6)
$g.DrawEllipse($tpen,($ox+150*$k-9),($y-1),18,18)
$thumb.Dispose(); $tpen.Dispose()
$y += 46*$k

function Btn([float]$x,[float]$y,[float]$w,[string]$text,[bool]$primary){
  $b = RoundPath $x $y ($w*$k) (32*$k) 5
  $fill = New-Object System.Drawing.SolidBrush((C 255 242 244 249))
  if($primary){ $fill = New-Object System.Drawing.SolidBrush((C 255 40 110 220)) }
  $g.FillPath($fill,$b); $fill.Dispose()
  if(-not $primary){
    $p = New-Object System.Drawing.Pen((C 255 205 210 222),1.2)
    $g.DrawPath($p,$b); $p.Dispose()
  }
  $b.Dispose()
  $col = $bBody
  if($primary){ $col = New-Object System.Drawing.SolidBrush((C 255 255 255 255)) }
  $sz = $g.MeasureString($text,$fBody)
  $g.DrawString($text,$fBody,$col,($x + ($w*$k - $sz.Width)/2),($y+7*$k))
}

Btn $ox $y 140 '恢复默认布局' $false
Btn ($ox+152*$k) $y 120 '打开配置目录' $false
Btn ($ox+284*$k) $y 100 '关闭' $false
$y += 44*$k
Btn $ox $y 140 '退出程序' $false
$g.DrawString('关闭只收回任务栏，程序继续运行。',$fSmall,$bGray,($ox+152*$k),($y+2))
$g.DrawString('重新打开：点任务栏或托盘图标。',$fSmall,$bGray,($ox+152*$k),($y+24))
$y += 54*$k
$bGreen = New-Object System.Drawing.SolidBrush((C 255 60 130 80))
$g.DrawString('已恢复默认布局',$fSmall,$bGreen,$ox,$y)
$bGreen.Dispose()

$fBody.Dispose();$fSmall.Dispose();$bBody.Dispose();$bGray.Dispose();$bBlue.Dispose();$bRed.Dispose()
$g.Dispose()
$bmp.Save((Join-Path $out 'settings.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ---------------------------------------------------------------- 6. 资源占用面板
$bmp,$g = New-Canvas 720 560
$bg3 = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Rectangle(0,0,720,560)), (C 255 234 240 250), (C 255 212 222 240),
  [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillRectangle($bg3,0,0,720,560); $bg3.Dispose()
$dx = 50; $dy = 50; $dw = 620; $dh = 460
$sh = New-Object System.Drawing.SolidBrush((C 40 20 40 80))
$ps = RoundPath ($dx+6) ($dy+12) $dw $dh 14
$g.FillPath($sh,$ps); $ps.Dispose(); $sh.Dispose()
$card = RoundPath $dx $dy $dw $dh 14
$cb3 = New-Object System.Drawing.SolidBrush((C 255 250 250 252))
$g.FillPath($cb3,$card); $cb3.Dispose()
$bp = New-Object System.Drawing.Pen((C 255 226 230 238),1)
$g.DrawPath($bp,$card); $bp.Dispose()
$g.SetClip($card)
$tb3 = New-Object System.Drawing.SolidBrush((C 255 243 245 250))
$g.FillRectangle($tb3,$dx,$dy,$dw,54); $tb3.Dispose()
$g.ResetClip()
$fWin = F 'Microsoft YaHei' 19 ''
$bWin = New-Object System.Drawing.SolidBrush((C 255 60 64 74))
$g.DrawString('资源占用 / 运行状态',$fWin,$bWin,($dx+18),($dy+14))
$fWin.Dispose(); $bWin.Dispose()
$rows = @(@('CPU 占用','0.0 %'),@('内存占用','41.2 MB'),@('线程数','6'),@('句柄数','161'),@('运行时长','00:12:31'))
$fLab = F 'Microsoft YaHei' 22 ''
$fVal2 = F 'Consolas' 24 'b'
$bLab = New-Object System.Drawing.SolidBrush((C 255 100 100 108))
$bVal = New-Object System.Drawing.SolidBrush((C 255 30 90 200))
$yy = $dy + 92
foreach($r in $rows){
  $g.DrawString($r[0],$fLab,$bLab,($dx+34),$yy)
  $g.DrawString($r[1],$fVal2,$bVal,($dx+200),($yy-2))
  $yy += 52
}
$fLab.Dispose();$fVal2.Dispose();$bLab.Dispose();$bVal.Dispose()
$btnR = RoundPath ($dx+34) ($yy+16) 150 46 5
$bR = New-Object System.Drawing.SolidBrush((C 255 242 244 249))
$g.FillPath($bR,$btnR); $bR.Dispose()
$bRp = New-Object System.Drawing.Pen((C 255 205 210 222),1.2)
$g.DrawPath($bRp,$btnR); $bRp.Dispose(); $btnR.Dispose()
$fBtn = F 'Microsoft YaHei' 20 ''
$bBtn = New-Object System.Drawing.SolidBrush((C 255 32 34 40))
$g.DrawString('刷新',$fBtn,$bBtn,($dx+80),($yy+28))
$chkR = RoundPath ($dx+210) ($yy+32) 20 20 3
$bChk = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
$g.FillPath($bChk,$chkR); $bChk.Dispose()
$chkP = New-Object System.Drawing.Pen((C 255 40 110 220),2.4)
$g.DrawLines($chkP,@(
  (New-Object System.Drawing.PointF(($dx+215),($yy+42))),
  (New-Object System.Drawing.PointF(($dx+219),($yy+47))),
  (New-Object System.Drawing.PointF(($dx+225),($yy+36)))))
$chkP.Dispose(); $chkR.Dispose()
$g.DrawString('自动刷新(1s)',$fBtn,$bBtn,($dx+240),($yy+28))
$g.DrawString('关闭',$fBtn,$bBtn,($dx+500),($yy+28))
$fBtn.Dispose(); $bBtn.Dispose()
$g.Dispose()
$bmp.Save((Join-Path $out 'stats.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# ---------------------------------------------------------------- 7. Logo
$bmp,$g = New-Canvas 256 256
$lp = RoundPath 8 8 240 240 56
$lg2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Rectangle(0,0,256,256)), (C 255 44 92 214), (C 255 20 26 48),
  [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
$g.FillPath($lg2,$lp); $lg2.Dispose()
$g.DrawPath((New-Object System.Drawing.Pen((C 60 255 255 255),2)),$lp)
$fLogo = F 'Bahnschrift' 92 'b'
$bLogo = New-Object System.Drawing.SolidBrush((C 255 255 255 255))
$g.DrawString('23:47',$fLogo,$bLogo,16,74)
$fLogo.Dispose(); $bLogo.Dispose()
$fL2 = F 'Microsoft YaHei' 21 'b'
$bL2 = New-Object System.Drawing.SolidBrush((C 225 180 215 255))
$g.DrawString('WDC',$fL2,$bL2,26,180)
$fL2.Dispose(); $bL2.Dispose()
$g.Dispose()
$bmp.Save((Join-Path $out 'logo.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$prev.Dispose()
$demo.Dispose()
Write-Output 'assets generated:'
Get-ChildItem $out | ForEach-Object { ' - {0}  {1} KB' -f $_.Name, [math]::Round($_.Length/1KB,1) }
