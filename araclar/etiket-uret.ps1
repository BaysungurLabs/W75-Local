# etiketler.json üretici betiği.
# Tuş yazılarını (US ve TR-Q düzenleri) Windows'un kendi klavye düzeni verisinden üretir:
#   HID kullanım kodu (layout-w75.json'daki hidCode) -> USB HID/PS2 tarama kodu tablosu (aşağıda, herkese açık eşleşme)
#   -> MapVirtualKeyEx (sanal tuş) -> ToUnicodeEx (düz, Shift ve AltGr karakterleri).
# Başka hiçbir kaynaktan etiket alınmaz. Tuş adı olan tuşlar (Esc, Tab, Shift, Ctrl, oklar, F1-F12 ...) her iki düzende aynı kalır.
# Kullanım: powershell -ExecutionPolicy Bypass -File araclar\etiket-uret.ps1 [-Layout layout-w75.json] [-Cikis etiketler.json]
param([string]$Layout = "layout-w75.json", [string]$Cikis = "etiketler.json")
$ErrorActionPreference = "Stop"

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class Kbd
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadKeyboardLayout(string klid, uint flags);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr hkl);
    [DllImport("user32.dll")] static extern int ToUnicodeEx(uint vk, uint sc, byte[] state, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int cch, uint flags, IntPtr hkl);

    public static IntPtr Load(string klid) { return LoadKeyboardLayout(klid, 0x80 /* KLF_NOTELLSHELL */); }

    // Tarama koduna karşılık gelen karakter. state: 0 düz, 1 Shift, 2 AltGr (Ctrl+Alt). Karakter yoksa "".
    public static string Translate(IntPtr hkl, uint sc, int state)
    {
        uint vk = MapVirtualKeyEx(sc, 3 /* MAPVK_VSC_TO_VK_EX */, hkl);
        if (vk == 0) return "";
        byte[] ks = new byte[256];
        if (state == 1) { ks[0x10] = 0x80; ks[0xA0] = 0x80; }
        if (state == 2) { ks[0x11] = 0x80; ks[0xA2] = 0x80; ks[0x12] = 0x80; ks[0xA5] = 0x80; }
        StringBuilder sb = new StringBuilder(8);
        int n = ToUnicodeEx(vk, sc, ks, sb, sb.Capacity, 4 /* durumu değiştirme */, hkl);
        if (n == 0) return "";
        // n < 0: ölü tuş; sb ölü tuş karakterini içerir
        string s = sb.ToString();
        return s.Length > 0 ? s.Substring(0, 1) : "";
    }
}
'@

# HID klavye kullanım kodu -> tarama kodu (Set 1 make). Yalnızca karakter üreten tuşlar (0x04-0x27, 0x2D-0x38 hariç 0x32).
$sc = @{}
$letters = @{ 0x04=0x1E; 0x05=0x30; 0x06=0x2E; 0x07=0x20; 0x08=0x12; 0x09=0x21; 0x0A=0x22; 0x0B=0x23; 0x0C=0x17; 0x0D=0x24; 0x0E=0x25; 0x0F=0x26;
              0x10=0x32; 0x11=0x31; 0x12=0x18; 0x13=0x19; 0x14=0x10; 0x15=0x13; 0x16=0x1F; 0x17=0x14; 0x18=0x16; 0x19=0x2F; 0x1A=0x11; 0x1B=0x2D; 0x1C=0x15; 0x1D=0x2C }
foreach ($k in $letters.Keys) { $sc[[int]$k] = [int]$letters[$k] }
for ($i = 0; $i -lt 10; $i++) { $sc[0x1E + $i] = 0x02 + $i }          # 1..9, 0  -> 0x02..0x0B
$punct = @{ 0x2D=0x0C; 0x2E=0x0D; 0x2F=0x1A; 0x30=0x1B; 0x31=0x2B; 0x33=0x27; 0x34=0x28; 0x35=0x29; 0x36=0x33; 0x37=0x34; 0x38=0x35 }
foreach ($k in $punct.Keys) { $sc[[int]$k] = [int]$punct[$k] }

$hklUs = [Kbd]::Load("00000409")
$hklTr = [Kbd]::Load("0000041F")
if ($hklUs -eq [IntPtr]::Zero -or $hklTr -eq [IntPtr]::Zero) { throw "Klavye düzenleri yüklenemedi (US: $hklUs, TR: $hklTr)." }

$lay = Get-Content $Layout -Raw -Encoding UTF8 | ConvertFrom-Json
$rows = @()
foreach ($k in ($lay.keys | Sort-Object { [int]$_.index })) {
    $hid = [Convert]::ToInt32(($k.hidCode -replace '^0x',''), 16)
    $row = [ordered]@{ index = [string]$k.index; code = $k.code; hid = ('{0:X2}' -f $hid); us = ""; usShift = ""; tr = ""; trShift = ""; trAltGr = "" }
    if ($sc.ContainsKey($hid)) {
        $scan = [uint32]$sc[$hid]
        $cUsN = [Kbd]::Translate($hklUs, $scan, 0); $cUsS = [Kbd]::Translate($hklUs, $scan, 1)
        $cTrN = [Kbd]::Translate($hklTr, $scan, 0); $cTrS = [Kbd]::Translate($hklTr, $scan, 1); $cTrA = [Kbd]::Translate($hklTr, $scan, 2)
        # Harf tuşları büyük harf gösterilir (Shift'li karakter): TR'de i tuşu İ, ı tuşu I
        $row.us      = $(if ($cUsN.Length -gt 0 -and [char]::IsLetter($cUsN[0])) { $cUsS } else { $cUsN })
        $row.usShift = $cUsS
        $row.tr      = $(if ($cTrN.Length -gt 0 -and [char]::IsLetter($cTrN[0])) { $cTrS } else { $cTrN })
        $row.trShift = $cTrS
        $row.trAltGr = $cTrA
        if ($row.us -eq "" -or $row.tr -eq "") { throw "Tuş $($k.code) (HID 0x$($row.hid)) için karakter bulunamadı." }
    } else {
        # Tuş adı olan tuşlar: iki düzende de aynı (layout'taki ad). Sağ Alt TR'de AltGr.
        $name = [string]$k.name
        if ([string]::IsNullOrEmpty($name)) { $name = "Knob" }
        $row.us = $name; $row.tr = $name
        if ($k.code -eq "AltRight") { $row.tr = "AltGr" }
    }
    $rows += [pscustomobject]$row
}

function J($s) { '"' + ([string]$s).Replace('\', '\\').Replace('"', '\"') + '"' }
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("{`n  `"kaynak`": `"Windows klavye düzenleri 00000409 (US) ve 0000041F (TR-Q); ToUnicodeEx/MapVirtualKeyEx`",`n  `"keys`": [`n")
for ($i = 0; $i -lt $rows.Count; $i++) {
    $r = $rows[$i]
    $line = '    {{"index":{0},"code":{1},"hid":{2},"us":{3},"usShift":{4},"tr":{5},"trShift":{6},"trAltGr":{7}}}' -f (J $r.index), (J $r.code), (J $r.hid), (J $r.us), (J $r.usShift), (J $r.tr), (J $r.trShift), (J $r.trAltGr)
    [void]$sb.Append($line); if ($i -lt $rows.Count - 1) { [void]$sb.Append(",") }; [void]$sb.Append("`n")
}
[void]$sb.Append("  ]`n}`n")
[IO.File]::WriteAllText($Cikis, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
"Yazıldı: $Cikis  ($($rows.Count) tuş; karakter üreten: $(($rows | Where-Object { $_.usShift -ne '' }).Count))"
