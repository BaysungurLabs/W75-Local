# actions.json üretici betiği (kendi verilerimizden).
# Tek kaynak: (B) herkese açık USB HID Usage Tables, klavye sayfası (0x07). Kodlar aşağıda elle yazıldı.
# Cihaz sınıfı kodu (c1) yalnızca klavye tuşları için 0'dır (cihaz yedeğindeki varsayılan tuş haritasında doğrulandı: (A)).
# Medya, fare, ışık, kilit ve kısayol eylemleri doğrulanamadığı için KONMAZ (cihazın bu eylemler için kodlaması doğrulanamadı).
# Kullanım: powershell -ExecutionPolicy Bypass -File araclar\eylem-uret.ps1 -Cikis actions.json
param([string]$Cikis = "actions.json")
$ErrorActionPreference = "Stop"

$list = New-Object System.Collections.ArrayList
function Add($group, $label, $code, $hid) { [void]$script:list.Add([ordered]@{ group = $group; label = $label; code = $code; c1 = 0; hid = [int]$hid; c3 = 0; c4 = 0 }) }

# Temel (programın iç eylemleri)
[void]$list.Add([ordered]@{ group = "Temel"; label = "Varsayılan (sıfırla)"; code = "__default"; c1 = 0; hid = 0; c3 = 0; c4 = 0 })
[void]$list.Add([ordered]@{ group = "Temel"; label = "Devre dışı"; code = "__none"; c1 = 0; hid = 0; c3 = 0; c4 = 0 })   # HID 0x00 = olay yok

# Harf / Rakam / Noktalama (0x04-0x38)
$letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
for ($i = 0; $i -lt 26; $i++) { Add "Harf / Rakam" ([string]$letters[$i]) ("Key" + $letters[$i]) (0x04 + $i) }
$digits = "1234567890"
for ($i = 0; $i -lt 10; $i++) { Add "Harf / Rakam" ([string]$digits[$i]) ("Digit" + $digits[$i]) (0x1E + $i) }
Add "Harf / Rakam" "- (tire)" "Minus" 0x2D
Add "Harf / Rakam" "= (eşittir)" "Equal" 0x2E
Add "Harf / Rakam" "[" "BracketLeft" 0x2F
Add "Harf / Rakam" "]" "BracketRight" 0x30
Add "Harf / Rakam" "\ (ters bölü)" "Backslash" 0x31
Add "Harf / Rakam" "; (noktalı virgül)" "Semicolon" 0x33
Add "Harf / Rakam" "' (tırnak)" "Quote" 0x34
Add "Harf / Rakam" '` (ters tırnak)' "Backquote" 0x35
Add "Harf / Rakam" ", (virgül)" "Comma" 0x36
Add "Harf / Rakam" ". (nokta)" "Period" 0x37
Add "Harf / Rakam" "/ (bölü)" "Slash" 0x38

# Standart tuşlar
Add "Standart" "Enter" "Enter" 0x28
Add "Standart" "Esc" "Escape" 0x29
Add "Standart" "Backspace" "Backspace" 0x2A
Add "Standart" "Tab" "Tab" 0x2B
Add "Standart" "Boşluk" "Space" 0x2C
Add "Standart" "Caps Lock" "CapsLock" 0x39
for ($i = 1; $i -le 12; $i++) { Add "Standart" ("F" + $i) ("F" + $i) (0x3A + $i - 1) }
Add "Standart" "Print Screen" "PrintScreen" 0x46
Add "Standart" "Scroll Lock" "ScrollLock" 0x47
Add "Standart" "Pause" "Pause" 0x48
Add "Standart" "Insert" "Insert" 0x49
Add "Standart" "Home" "Home" 0x4A
Add "Standart" "Page Up" "PageUp" 0x4B
Add "Standart" "Delete" "Delete" 0x4C
Add "Standart" "End" "End" 0x4D
Add "Standart" "Page Down" "PageDown" 0x4E
Add "Standart" "Sağ ok" "ArrowRight" 0x4F
Add "Standart" "Sol ok" "ArrowLeft" 0x50
Add "Standart" "Aşağı ok" "ArrowDown" 0x51
Add "Standart" "Yukarı ok" "ArrowUp" 0x52
Add "Standart" "Num Lock" "NumLock" 0x53
Add "Standart" "Numpad /" "NumpadDivide" 0x54
Add "Standart" "Numpad *" "NumpadMultiply" 0x55
Add "Standart" "Numpad -" "NumpadSubtract" 0x56
Add "Standart" "Numpad +" "NumpadAdd" 0x57
Add "Standart" "Numpad Enter" "NumpadEnter" 0x58
for ($i = 1; $i -le 9; $i++) { Add "Standart" ("Numpad " + $i) ("Numpad" + $i) (0x59 + $i - 1) }
Add "Standart" "Numpad 0" "Numpad0" 0x62
Add "Standart" "Numpad ." "NumpadDecimal" 0x63
Add "Standart" "Menü tuşu" "ContextMenu" 0x65
for ($i = 13; $i -le 24; $i++) { Add "Standart" ("F" + $i) ("F" + $i) (0x68 + $i - 13) }
Add "Standart" "Sol Ctrl" "ControlLeft" 0xE0
Add "Standart" "Sol Shift" "ShiftLeft" 0xE1
Add "Standart" "Sol Alt" "AltLeft" 0xE2
Add "Standart" "Sol Win" "MetaLeft" 0xE3
Add "Standart" "Sağ Ctrl" "ControlRight" 0xE4
Add "Standart" "Sağ Shift" "ShiftRight" 0xE5
Add "Standart" "Sağ Alt (AltGr)" "AltRight" 0xE6
Add "Standart" "Sağ Win" "MetaRight" 0xE7

# Kod tekrarı var mı?
$dup = $list | Group-Object { $_.code } | Where-Object { $_.Count -gt 1 }
if ($dup) { throw "Yinelenen eylem kodu: " + (($dup | % Name) -join ', ') }

$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("[`n")
for ($i = 0; $i -lt $list.Count; $i++) {
    $e = $list[$i]
    $lbl = ([string]$e.label).Replace('\', '\\').Replace('"', '\"')
    $row = ' {{"group":"{0}","label":"{1}","code":"{2}","c1":{3},"hid":{4},"c3":{5},"c4":{6}}}' -f $e.group, $lbl, $e.code, $e.c1, $e.hid, $e.c3, $e.c4
    [void]$sb.Append($row); if ($i -lt $list.Count - 1) { [void]$sb.Append(",") }; [void]$sb.Append("`n")
}
[void]$sb.Append("]`n")
[IO.File]::WriteAllText($Cikis, $sb.ToString(), (New-Object Text.UTF8Encoding($false)))
"Yazıldı: $Cikis  (eylem sayısı: $($list.Count))"
