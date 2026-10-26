# W75 Local simgesini üretir: ikon\w75.ico (16, 24, 32, 48, 64, 128, 256) ve ikon\w75-256.png.
# Tamamen özgün, basit geometrik çizim (koyu zemin, klavye silüeti, turuncu WASD tuşları). Yalnızca System.Drawing kullanır.
# Tekrar çalıştırılabilir: çıktıyı her seferinde baştan üretir.  Kullanım: powershell -ExecutionPolicy Bypass -File araclar\ikon-uret.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "ikon"
New-Item -ItemType Directory -Force $outDir | Out-Null

function RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Max(0.01, 2 * $r)
    if ($d -gt $w) { $d = $w }; if ($d -gt $h) { $d = $h }
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Bir boyutu doğrudan o boyutta çizer (küçültme yok).
function Draw-Icon([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Zemin: yuvarlak köşeli kare, koyu lacivert, üstte biraz açık dikey degrade.
    $tile = RoundRect 0.5 0.5 ($S - 1) ($S - 1) ($S * 0.18)
    $tileRect = New-Object System.Drawing.RectangleF 0, 0, $S, $S
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $tileRect, ([System.Drawing.Color]::FromArgb(255, 0x1C, 0x25, 0x40)), ([System.Drawing.Color]::FromArgb(255, 0x12, 0x18, 0x2A)), 90.0
    $g.FillPath($bg, $tile)

    # Düzen: küçük boyutlarda daha az ve daha büyük tuş, kalın aralık.
    $tiny = $S -le 24
    if ($S -le 16) { $cols = 3; $rows = 2 } elseif ($S -le 24) { $cols = 3; $rows = 2 } elseif ($S -le 48) { $cols = 6; $rows = 3 } else { $cols = 7; $rows = 3 }
    # Klavye silüeti: önceki sürüme göre ~%17 büyük, karede yatay ve dikey ortalı (köşe ve kenar çizgisinden pay kalır).
    $aw = $S * 0.84; $ah = $S * 0.55
    if ($tiny) { $aw = $S * 0.78; $ah = $S * 0.56 }   # 16/24 px: yalnızca turuncu blok, daha büyük
    $ax = ($S - $aw) / 2; $ay = ($S - $ah) / 2
    $gap = [Math]::Max(1.0, $S * 0.028)
    if ($tiny) { $gap = [Math]::Max(1.0, $S * 0.05) }
    $kw = ($aw - ($cols - 1) * $gap) / $cols
    $kh = ($ah - ($rows - 1) * $gap) / $rows
    $kr = [Math]::Min($kw, $kh) * 0.22

    # Tuş listesi: x, y, genişlik (sütun), vurgulu mu.
    $keys = New-Object System.Collections.ArrayList
    if ($tiny) {
        # 16 ve 24: 2 sıra, yalnızca turuncu blok (W üstte ortada, altta A S D); soluk tuş yok, netlik için.
        [void]$keys.Add(@(1, 0, 1, $true))
        foreach ($c in 0, 1, 2) { [void]$keys.Add(@($c, 1, 1, $true)) }
    } else {
        for ($c = 0; $c -lt $cols; $c++) { [void]$keys.Add(@($c, 0, 1, ($c -eq 2))) }                 # üst sıra, W = sütun 2
        for ($c = 0; $c -lt $cols; $c++) { [void]$keys.Add(@($c, 1, 1, ($c -ge 1 -and $c -le 3))) }   # orta sıra, A S D = sütun 1-3
        [void]$keys.Add(@(0, 2, 1, $false))                                                            # alt sıra: gri, geniş boşluk, gri
        [void]$keys.Add(@(1, 2, ($cols - 2), $false))
        [void]$keys.Add(@(($cols - 1), 2, 1, $false))
    }

    $gray = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x33, 0x3D, 0x58))
    $clip = $tile
    $g.SetClip($clip)

    # Parıltı: vurgulu tuşların çevresinde kademeli yarı saydam turuncu hale (bulanıklık taklidi).
    $layers = 5; if ($S -le 16) { $layers = 0 } elseif ($tiny) { $layers = 1 }
    $spread = [Math]::Max(1.0, $S * 0.012)
    for ($i = $layers; $i -ge 1; $i--) {
        $base = 12; if ($tiny) { $base = 26 }; $a = [int]($base * (1 + ($layers - $i) * 0.35)); if ($a -gt 60) { $a = 60 }
        $hb = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, 0xFF, 0x8C, 0x00))
        foreach ($k in $keys) {
            if (-not $k[3]) { continue }
            $x = $ax + $k[0] * ($kw + $gap); $y = $ay + $k[1] * ($kh + $gap); $w = $k[2] * $kw + ($k[2] - 1) * $gap
            $e = $i * $spread
            $p = RoundRect ($x - $e) ($y - $e) ($w + 2 * $e) ($kh + 2 * $e) ($kr + $e)
            $g.FillPath($hb, $p); $p.Dispose()
        }
        $hb.Dispose()
    }

    # Tuşlar.
    foreach ($k in $keys) {
        $x = $ax + $k[0] * ($kw + $gap); $y = $ay + $k[1] * ($kh + $gap); $w = $k[2] * $kw + ($k[2] - 1) * $gap
        $p = RoundRect $x $y $w $kh $kr
        if ($k[3]) {
            $rect = New-Object System.Drawing.RectangleF $x, $y, $w, $kh
            $ob = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 0xFF, 0x8C, 0x00)), ([System.Drawing.Color]::FromArgb(255, 0xFF, 0x64, 0x19)), 90.0
            $g.FillPath($ob, $p); $ob.Dispose()
        } else { $g.FillPath($gray, $p) }
        $p.Dispose()
    }
    $g.ResetClip()

    # İnce, yarı saydam beyaz kenar çizgisi (1 px).
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 255, 255, 255)), 1
    $g.DrawPath($pen, $tile)
    $pen.Dispose(); $gray.Dispose(); $bg.Dispose(); $tile.Dispose(); $g.Dispose()
    return $bmp
}

function PngBytes($bmp) { $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); return ,$ms.ToArray() }

# <256 için klasik DIB (BMP) girdisi: BGRA, aşağıdan yukarı + AND maskesi (sıfır; alfa kullanılır).
function DibBytes($bmp) {
    $S = $bmp.Width
    $ms = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([int]40); $w.Write([int]$S); $w.Write([int]($S * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]($S * $S * 4)); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $S - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $S; $x++) { $c = $bmp.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) } }
    $maskRow = [int]([Math]::Ceiling($S / 32.0) * 4)
    $w.Write((New-Object byte[] ($maskRow * $S)))
    $w.Flush(); return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @{}
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    $images[$s] = $bmp
    if ($s -eq 256) { $bmp.Save((Join-Path $outDir "w75-256.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
}

$data = @{}
foreach ($s in $sizes) { $data[$s] = if ($s -eq 256) { PngBytes $images[$s] } else { DibBytes $images[$s] } }

$icoPath = Join-Path $outDir "w75.ico"
$fs = [System.IO.File]::Create($icoPath); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -eq 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$data[$s].Length); $bw.Write([int]$offset)
    $offset += $data[$s].Length
}
foreach ($s in $sizes) { $bw.Write($data[$s]) }
$bw.Flush(); $bw.Close(); $fs.Close()
foreach ($s in $sizes) { $images[$s].Dispose() }
Write-Host ("Simge uretildi: " + $icoPath + " (" + (Get-Item $icoPath).Length + " bayt), " + (Join-Path $outDir "w75-256.png"))
