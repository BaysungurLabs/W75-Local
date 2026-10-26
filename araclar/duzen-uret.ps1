# layout-w75.json üretici betiği (kendi verilerimizden).
# Değerlerin kaynakları (başka hiçbir dosyadan değer alınmaz):
#   (A) Cihaz yedeği (yedek-*.json) içindeki tuş haritası: hangi dizinde (index) hangi varsayılan HID kodu var.
#       Fiziksel tuşun kimliği bu varsayılan kodla belirlenir (örn. 0x04 = A).
#   (B) Herkese açık USB HID Usage Tables, klavye sayfası (0x07): kod -> tuş adı eşleşmesi aşağıda elle yazıldı.
#   (C) Standart %75 ANSI klavye geometrisi (1u = 1 birim), aşağıda elle yazıldı.
# Kullanım: powershell -ExecutionPolicy Bypass -File araclar\duzen-uret.ps1 -Yedek <yedek-*.json yolu> -Cikis layout-w75.json
param(
    [Parameter(Mandatory = $true)][string]$Yedek,
    [string]$Cikis = "layout-w75.json"
)
$ErrorActionPreference = "Stop"

$ok = @{ Up = [string][char]0x2191; Left = [string][char]0x2190; Down = [string][char]0x2193; Right = [string][char]0x2192 }

# (B)+(C): hidKodu -> kod (W3C UI Events adı), görünen ad, satır, x (u), genişlik (u)
$T = @{}
function Key($hid, $code, $name, $row, $x, $w) { $script:T[[int]$hid] = @{ Code = $code; Name = $name; Row = $row; X = [double]$x; W = [double]$w } }

# Satır 0: Esc (x=0); F1-F4 x=1.25'ten başlar (Esc ile F1 arasında 0.25u ekstra boşluk), F5-F8 ve F9-F12 de önceki gruptan 0.25u ekstra boşlukla; Del F12'den 0.25u sonra x=14, sağ kenarı (15) Backspace'in sağ kenarıyla aynı hizada
Key 0x29 "Escape" "Esc" 0 0 1
$f = @(0x3A,0x3B,0x3C,0x3D, 0x3E,0x3F,0x40,0x41, 0x42,0x43,0x44,0x45)
$fx = @(1.25,2.25,3.25,4.25, 5.5,6.5,7.5,8.5, 9.75,10.75,11.75,12.75)
for ($i = 0; $i -lt 12; $i++) { Key $f[$i] ("F" + ($i + 1)) ("F" + ($i + 1)) 0 $fx[$i] 1 }
Key 0x4C "Delete" "Del" 0 14 1
# Satır 1: ` 1..0 - = Backspace, Home
Key 0x35 "Backquote" "``" 1 0 1
$d = @(0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27); $dn = @("1","2","3","4","5","6","7","8","9","0")
for ($i = 0; $i -lt 10; $i++) { Key $d[$i] ("Digit" + $dn[$i]) $dn[$i] 1 ($i + 1) 1 }
Key 0x2D "Minus" "-" 1 11 1
Key 0x2E "Equal" "=" 1 12 1
Key 0x2A "Backspace" "Backspace" 1 13 2
Key 0x4A "Home" "Home" 1 15.5 1
# Satır 2: Tab Q..P [ ] \ , PgUp
Key 0x2B "Tab" "Tab" 2 0 1.5
$q = @(0x14,0x1A,0x08,0x15,0x17,0x1C,0x18,0x0C,0x12,0x13); $qn = @("Q","W","E","R","T","Y","U","I","O","P")
for ($i = 0; $i -lt 10; $i++) { Key $q[$i] ("Key" + $qn[$i]) $qn[$i] 2 (1.5 + $i) 1 }
Key 0x2F "BracketLeft" "[" 2 11.5 1
Key 0x30 "BracketRight" "]" 2 12.5 1
Key 0x31 "Backslash" "\" 2 13.5 1.5
Key 0x4B "PageUp" "PgUp" 2 15.5 1
# Satır 3: Caps A..L ; ' Enter, PgDn
Key 0x39 "CapsLock" "Caps Lock" 3 0 1.75
$a = @(0x04,0x16,0x07,0x09,0x0A,0x0B,0x0D,0x0E,0x0F); $an = @("A","S","D","F","G","H","J","K","L")
for ($i = 0; $i -lt 9; $i++) { Key $a[$i] ("Key" + $an[$i]) $an[$i] 3 (1.75 + $i) 1 }
Key 0x33 "Semicolon" ";" 3 10.75 1
Key 0x34 "Quote" "'" 3 11.75 1
Key 0x28 "Enter" "Enter" 3 12.75 2.25
Key 0x4E "PageDown" "PgDn" 3 15.5 1
# Satır 4: Sol Shift Z..M , . / Sağ Shift, Yukarı
Key 0xE1 "ShiftLeft" "L-Shift" 4 0 2.25
$z = @(0x1D,0x1B,0x06,0x19,0x05,0x11,0x10); $zn = @("Z","X","C","V","B","N","M")
for ($i = 0; $i -lt 7; $i++) { Key $z[$i] ("Key" + $zn[$i]) $zn[$i] 4 (2.25 + $i) 1 }
Key 0x36 "Comma" "," 4 9.25 1
Key 0x37 "Period" "." 4 10.25 1
Key 0x38 "Slash" "/" 4 11.25 1
Key 0xE5 "ShiftRight" "R-Shift" 4 12.25 1.75
Key 0x52 "ArrowUp" $ok.Up 4 14.5 1
# Satır 5: Sol Ctrl, Win, Alt, Space, Sağ Alt, (Fn), Sağ Ctrl, ok tuşları (ters T; Sağ Ctrl'dan 0.5u boşlukla, Yukarı ok da aynı sütunda)
Key 0xE0 "ControlLeft" "L-Ctrl" 5 0 1.25
Key 0xE3 "MetaLeft" "L-Win" 5 1.25 1.25
Key 0xE2 "AltLeft" "L-Alt" 5 2.5 1.25
Key 0x2C "Space" "Space" 5 3.75 6.25
Key 0xE6 "AltRight" "R-Alt" 5 10 1
Key 0xE4 "ControlRight" "R-Ctrl" 5 12 1
Key 0x50 "ArrowLeft" $ok.Left 5 13.5 1
Key 0x51 "ArrowDown" $ok.Down 5 14.5 1
Key 0x4F "ArrowRight" $ok.Right 5 15.5 1
$FnX = 11.0   # Fn tuşu: cihazda c1=1 ile işaretli slot; sağ Alt ile sağ Ctrl arasında
$KnobX = 15.5 # Döner düğme: Home/PgUp/PgDn sütunuyla aynı x (sütun Backspace'in bitişinden 0.5u sonra), F satırı yüksekliğinde, 1u çaplı daire

# (A) cihaz yedeğini oku
$j = Get-Content $Yedek -Raw -Encoding UTF8 | ConvertFrom-Json
$km = [Convert]::FromBase64String($j.KeymapBase)
if ($km.Length -ne 528) { throw "Tuş haritası 528 bayt olmalı (şu an $($km.Length))." }
$slots = @()
for ($i = 0; $i -lt 132; $i++) {
    if ($km[$i*4] -or $km[$i*4+1] -or $km[$i*4+2] -or $km[$i*4+3]) { $slots += [pscustomobject]@{ Index = $i; C1 = $km[$i*4]; Hid = $km[$i*4+1]; C3 = $km[$i*4+2]; C4 = $km[$i*4+3] } }
}

# Birim -> piksel (kendi ölçeğimiz): 1u = 40 px, tuş boşluğu 3 px, sol/üst kenar 12 px, F satırından sonra 0.2u boşluk
function Px($v) { [Math]::Round($v, 1).ToString([Globalization.CultureInfo]::InvariantCulture) }
function KeyJson($index, $name, $code, $hid, $row, $xu, $wu) {
    $x = 12 + $xu * 40
    $w = $wu * 40 - 3
    $y = 12 + $row * 40 + $(if ($row -gt 0) { 8 } else { 0 })
    $nameEsc = $name.Replace('\', '\\').Replace('"', '\"')
    return ('    {{"index":"{0}","name":"{1}","code":"{2}","hidCode":"{3}","width":"{4}","height":"37","x":"{5}","y":"{6}"}}' -f $index, $nameEsc, $code, $hid, (Px $w), (Px $x), (Px $y))
}

$lines = @(); $used = @{}; $keyCount = 0; $knobSlots = @()
foreach ($s in $slots) {
    if ($s.C1 -eq 1 -and $s.Hid -eq 0) { $lines += @{ I = $s.Index; J = (KeyJson $s.Index "Fn" "KeyFn" "00" 5 $FnX 1) }; $keyCount++; continue }
    if ($s.C1 -ne 0) { $knobSlots += $s; continue }   # c1 != 0: klavye tuşu değil (döner düğme slotları)
    if (-not $T.ContainsKey([int]$s.Hid)) { throw "(A)'daki dizin $($s.Index) için HID 0x$('{0:X2}' -f $s.Hid) tablomda yok; durdum." }
    if ($used.ContainsKey([int]$s.Hid)) { throw "HID 0x$('{0:X2}' -f $s.Hid) iki dizinde var; durdum." }
    $used[[int]$s.Hid] = $true
    $e = $T[[int]$s.Hid]
    $lines += @{ I = $s.Index; J = (KeyJson $s.Index $e.Name $e.Code ('{0:X2}' -f $s.Hid) $e.Row $e.X $e.W) }
    $keyCount++
}
# Tabloda olup (A)'da olmayan tuş var mı? (geometri tablosu gereksiz tuş içermesin)
$missing = $T.Keys | Where-Object { -not $used.ContainsKey($_) }
if ($missing) { throw "Tabloda olup cihaz haritasında bulunmayan HID kodları var: " + (($missing | % { '0x{0:X2}' -f $_ }) -join ', ') }
# Döner düğme: (A)'da bu sütun boş (dizin 15, Del'in yanındaki sütun); yalnızca çizim için kullanılır.
$knobIndex = 15
if (($slots | Where-Object { $_.Index -eq $knobIndex }).Count -ne 0) { throw "Dizin 15 doluymuş; düğme varsayımı geçersiz." }
$lines += @{ I = $knobIndex; J = (KeyJson $knobIndex "" "RotaryKnob" "00" 0 $KnobX 1) }

$sorted = $lines | Sort-Object { $_.I }
$json = "{`n  `"keys`": [`n" + (($sorted | % { $_.J }) -join ",`n") + "`n  ]`n}`n"
[IO.File]::WriteAllText($Cikis, $json, (New-Object Text.UTF8Encoding($false)))
"Yazıldı: $Cikis  (tuş: $keyCount + döner düğme çizimi 1; (A)'daki döner düğme slotları: " + (($knobSlots | % { $_.Index }) -join ',') + ")"
