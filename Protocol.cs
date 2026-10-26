using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace W75Ayarlayici
{
    public class DeviceStatus
    {
        public string RawInfo = "";
        public string Product = "";
        public string Version = "";
        public int TriMode;
        public bool HighPrecision;
        public bool HasPrcs;
        public bool HasLight;
        public bool HasDeadband;
        public bool HasPollRate;
        public int Battery;
        public bool Charging;
        public int MaxTravel = 170;
        public double Unit = 0.02;
        public int MinTravel = 5;
        public byte[] Flags;
    }

    public class LightSetting
    {
        public int Mode;
        public int Brightness = 2;
        public int Speed = 2;
        public int Direction;
        public int FullColor;
        public int Power; // 0 = on, 1 = off
        public int R = 255, G = 255, B = 255;
        public int BR, BG, BB;

        public LightSetting Clone()
        {
            LightSetting x = new LightSetting();
            x.Mode = Mode; x.Brightness = Brightness; x.Speed = Speed; x.Direction = Direction;
            x.FullColor = FullColor; x.Power = Power; x.R = R; x.G = G; x.B = B;
            x.BR = BR; x.BG = BG; x.BB = BB;
            return x;
        }
    }

    public class TriggerSetting
    {
        public int Mode;       // 0 = fixed, 12 = rapid trigger, 13 = rapid trigger (separate)
        public int Travel = 85; // raw units, unit = 0.02mm
        public int Interval1 = 1;
        public int Interval2 = 1;
        public int DeadTop;
        public int DeadBottom;

        public TriggerSetting Clone()
        {
            TriggerSetting x = new TriggerSetting();
            x.Mode = Mode; x.Travel = Travel; x.Interval1 = Interval1; x.Interval2 = Interval2;
            x.DeadTop = DeadTop; x.DeadBottom = DeadBottom;
            return x;
        }
    }

    public class SocdRule
    {
        public int Index;
        public int Model;  // 0..3
        public int Key1;   // HID kodu
        public int Key2;   // HID kodu
    }

    public class SocdPower
    {
        public bool On;
        public int Shortcut1;
        public int Shortcut2;
    }

    // Klavyenin geneli için ayarlar (profile bağlı değil): tuş kilitleri ve tuş modu.
    public class MiscSettings
    {
        public bool Win;
        public bool ShiftTab;
        public bool AltF4;
        public bool AltTab;
        public bool SixKey;           // true = 6 tuş modu, false = N tuş
        public bool SixKeySupported;  // cihaz 6 tuş anahtarını destekliyor mu

        public MiscSettings Clone()
        {
            MiscSettings m = new MiscSettings();
            m.Win = Win; m.ShiftTab = ShiftTab; m.AltF4 = AltF4; m.AltTab = AltTab;
            m.SixKey = SixKey; m.SixKeySupported = SixKeySupported;
            return m;
        }
    }

    public class W75Device : IDisposable
    {
        public const ushort Vid = 0x2E3C;
        public const ushort Pid = 0xC368;
        public const ushort UsagePage = 0xFF1B;
        public const ushort Usage = 0x91;

        private Hid _hid;
        private string _path;
        public DeviceStatus Status = new DeviceStatus();
        public bool IsOpen { get { return _hid != null; } }

        // Bağlantı olayları (düştü / geri geldi) için isteğe bağlı günlük. Zaman damgasını günlük ekler.
        public Action<string> ConnectionLog;
        // Bağlantı yeniden açıldıkça artar; çağıran taraf bir isteğin sırasında yenilenme olup olmadığını anlar.
        public int Reconnects;

        public static W75Device Open()
        {
            Hid h = Hid.Open(Vid, Pid, UsagePage, Usage);
            if (h == null) return null;
            W75Device d = new W75Device();
            d._hid = h;
            d._path = h.Path;
            return d;
        }

        private void Note(string msg)
        {
            Action<string> a = ConnectionLog;
            if (a == null) return;
            try { a(msg); } catch { }
        }

        // Cihazın HID arayüzü şu an Windows'ta görünüyor mu? Komut göndermez.
        public bool IsPresent()
        {
            if (_path == null) return false;
            foreach (string p in Hid.Paths())
                if (string.Equals(p, _path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Bağlantıyı yeniden açmayı en çok totalMs boyunca stepMs arayla dener. Komut göndermez.
        public bool Reopen(int totalMs, int stepMs)
        {
            if (_hid != null) { _hid.Close(); _hid = null; }
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                Hid h = Hid.Open(Vid, Pid, UsagePage, Usage);
                if (h != null)
                {
                    _hid = h;
                    _path = h.Path;
                    Reconnects++;
                    Note("Cihaz geri geldi");
                    return true;
                }
                if (sw.ElapsedMilliseconds + stepMs > totalMs) return false;
                Thread.Sleep(stepMs);
            }
        }

        public void Close()
        {
            if (_hid != null) { _hid.Close(); _hid = null; }
        }

        public void Dispose() { Close(); }

        // ---------------- low level ----------------

        private byte[] Cmd(params byte[] parts)
        {
            byte[] b = new byte[63];
            for (int i = 0; i < parts.Length && i < 63; i++) b[i] = parts[i];
            return b;
        }

        private void Send(byte[] payload)
        {
            if (_hid == null) throw new InvalidOperationException("Cihaz bağlı değil.");
            if (_hid.Send(payload)) return;
            // Gönderme hatası: en çok 5 saniye boyunca 300 ms arayla bağlantıyı yeniden açmayı dene.
            // Komut otomatik tekrar gönderilmez; çağıran taraf hatayı görür.
            int code = _hid.LastError;
            Note(code == 433 || code == 1167
                ? "Cihaz düştü (Win32 hata kodu " + code + ")"
                : "Gönderme hatası (Win32 hata kodu " + code + "), bağlantı yeniden açılıyor");
            bool back = Reopen(5000, 300);
            throw new InvalidOperationException("HID raporu gönderilemedi (Win32 hata kodu " + code + "). " +
                (back ? "Bağlantı yeniden açıldı, komut tekrar gönderilmedi." : "Cihaz 5 saniye içinde geri gelmedi."));
        }

        private byte[] Wait(byte cmd, int sub, int timeoutMs)
        {
            return _hid.Wait(delegate(byte[] d)
            {
                if (d.Length < 2 || d[0] != cmd) return false;
                if (sub >= 0 && d.Length > 5 && d[5] != sub) return false;
                return true;
            }, timeoutMs);
        }

        public void Drain() { if (_hid != null) _hid.Drain(); }

        // ---------------- info ----------------

        public DeviceStatus ReadStatus()
        {
            DeviceStatus s = new DeviceStatus();
            Drain();
            Send(Cmd(13));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == 13; }, 1500);
            if (d != null)
            {
                int len = d[4];
                if (len > 60) len = 60;
                s.RawInfo = Encoding.ASCII.GetString(d, 5, len).TrimEnd('\0');
                string[] parts = s.RawInfo.Split(',');
                if (parts.Length >= 5) s.Product = parts[4];
                if (parts.Length >= 6) s.Version = parts[5];
            }

            Drain();
            Send(Cmd(1));
            d = _hid.Wait(delegate(byte[] x) { return x.Length > 18 && x[0] == 1; }, 1500);
            if (d != null)
            {
                byte[] flags = new byte[18];
                Array.Copy(d, 5, flags, 0, 18);
                s.Flags = flags;
                s.TriMode = d[10];
                s.Charging = d[11] == 1;
                s.HasPrcs = flags[9] == 1;
                s.HighPrecision = (flags[13] & 1) != 0;
                s.HasLight = true;
                s.HasDeadband = (flags[12] & 16) != 0;
                s.HasPollRate = (flags[12] & 8) != 0;
            }

            Drain();
            Send(Cmd(33, 0, 0, 0, 24, 4));
            d = _hid.Wait(delegate(byte[] x) { return x.Length > 10 && x[0] == 33 && x[5] == 4; }, 1200);
            if (d != null)
            {
                s.MaxTravel = (d[9] << 8) | d[6];
                int unitRaw = d[7] == 0 ? 10 : d[7];
                s.Unit = (s.HighPrecision ? unitRaw / 1000.0 : unitRaw / 100.0);
                s.MinTravel = d[10];
                if (s.MaxTravel <= 0) s.MaxTravel = 170;
                if (s.Unit <= 0) s.Unit = 0.02;
            }
            Status = s;
            return s;
        }

        // Genel ayarlar: cihaz durum paketi (cmd 1, ReadStatus ile aynı istek). Cevap indeksleri:
        // [20] bit0=Win, bit1=Shift+Tab, bit2=Alt+F4, bit3=Alt+Tab; [22] bit0=6 tuş modu; [17] bit7=6 tuş desteği.
        public MiscSettings ReadMisc()
        {
            Drain();
            Send(Cmd(1));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 22 && x[0] == 1; }, 1500);
            if (d == null) throw new InvalidOperationException("Cihaz durumu okunamadı (genel ayarlar).");
            MiscSettings m = new MiscSettings();
            m.Win = (d[20] & 1) != 0;
            m.ShiftTab = (d[20] & 2) != 0;
            m.AltF4 = (d[20] & 4) != 0;
            m.AltTab = (d[20] & 8) != 0;
            m.SixKey = (d[22] & 1) != 0;
            m.SixKeySupported = (d[17] & 128) != 0;
            return m;
        }

        // Genel ayarlar: tek 63 baytlık paket. bayt0=20, bayt4=1, bayt12=Win, bayt13=Shift+Tab,
        // bayt14=Alt+F4, bayt15=Alt+Tab, bayt18=1 ise 6 tuş modu. Diğer baytlar 0.
        public void WriteMisc(MiscSettings s)
        {
            byte[] p = Cmd(20, 0, 0, 0, 1);
            p[12] = (byte)(s.Win ? 1 : 0);
            p[13] = (byte)(s.ShiftTab ? 1 : 0);
            p[14] = (byte)(s.AltF4 ? 1 : 0);
            p[15] = (byte)(s.AltTab ? 1 : 0);
            p[18] = (byte)(s.SixKey && s.SixKeySupported ? 1 : 0);
            Send(p);
        }

        public void SetProfile(int slot)
        {
            Send(Cmd(33, 0, 0, 0, 0, 18, (byte)slot));
            Thread.Sleep(250);
        }

        public void SetPollRate(int rate)
        {
            // rate: 1, 2, 4 or 8 (kHz)
            Send(Cmd(33, 0, 0, 0, 0, 9, 1, (byte)rate));
        }

        public int ReadPollRate()
        {
            Drain();
            Send(Cmd(33, 0, 0, 0, 1, 9));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 6 && x[0] == 33 && x[5] == 9; }, 1200);
            if (d != null && (d[6] == 1 || d[6] == 2 || d[6] == 4 || d[6] == 8)) return d[6];
            return 1;
        }

        // ---------------- keymap ----------------

        public byte[] ReadKeymap(bool fn)
        {
            byte[] result = new byte[528];
            bool[] got = new bool[10];
            int count = 0;
            Drain();
            Send(Cmd(24, (byte)(fn ? 130 : 128)));
            Stopwatch sw = Stopwatch.StartNew();
            while (count < 10 && sw.ElapsedMilliseconds < 2500)
            {
                byte[] d = _hid.Wait(delegate(byte[] x)
                {
                    return x.Length > 5 && x[0] == 24 && x[1] == (fn ? 130 : 128);
                }, 400);
                if (d == null) continue;
                int page = (d[2] << 8) | d[3];
                if (page < 0 || page > 9) continue;
                int len = d[4];
                if (len > 56) len = 56;
                Array.Copy(d, 5, result, page * 56, len);
                if (!got[page]) { got[page] = true; count++; }
            }
            return result;
        }

        // ReadKeymap ile aynı istek (yalnızca okuma); 10 sayfanın hepsi gelmezse eksik veri yerine null döner.
        public byte[] TryReadKeymap(bool fn)
        {
            byte[] result = new byte[528];
            bool[] got = new bool[10];
            int count = 0;
            Drain();
            Send(Cmd(24, (byte)(fn ? 130 : 128)));
            Stopwatch sw = Stopwatch.StartNew();
            while (count < 10 && sw.ElapsedMilliseconds < 2500)
            {
                byte[] d = _hid.Wait(delegate(byte[] x)
                {
                    return x.Length > 5 && x[0] == 24 && x[1] == (fn ? 130 : 128);
                }, 400);
                if (d == null) continue;
                int page = (d[2] << 8) | d[3];
                if (page < 0 || page > 9) continue;
                int len = d[4];
                if (len > 56) len = 56;
                Array.Copy(d, 5, result, page * 56, len);
                if (!got[page]) { got[page] = true; count++; }
            }
            return count == 10 ? result : null;
        }

        public void WriteKeymap(bool fn, byte[] data528)
        {
            if (data528.Length != 528) throw new ArgumentException("Keymap 528 bayt olmalı.");
            const int chunk = 56;
            for (int page = 0; page < 10; page++)
            {
                byte[] p = Cmd(24, (byte)(fn ? 2 : 0), (byte)(page >> 8), (byte)(page & 255));
                int len = Math.Min(chunk, data528.Length - page * chunk);
                p[4] = (byte)len;
                Array.Copy(data528, page * chunk, p, 5, len);
                Send(p);
                Thread.Sleep(4);
            }
        }

        // ---------------- trigger ----------------

        public TriggerSetting ReadTrigger(int index)
        {
            int group = index / 22;
            int offset = index % 22;
            Drain();
            Send(Cmd(33, 0, 0, 0, 24, 5, (byte)group, (byte)offset));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 14 && x[0] == 33 && x[5] == 5; }, 900);
            TriggerSetting t = new TriggerSetting();
            t.Travel = 85; t.Interval1 = 1; t.Interval2 = 1;
            if (d != null)
            {
                t.Mode = d[6];
                t.Travel = (d[11] << 8) | d[7];
                t.Interval1 = (d[13] << 8) | d[9];
                t.Interval2 = (d[14] << 8) | d[10];
            }
            return t;
        }

        // ReadTrigger ile aynı istek; cevap gelmezse yedek değer yerine null döner (doğrulama için).
        public TriggerSetting TryReadTrigger(int index)
        {
            int group = index / 22;
            int offset = index % 22;
            Drain();
            Send(Cmd(33, 0, 0, 0, 24, 5, (byte)group, (byte)offset));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 14 && x[0] == 33 && x[5] == 5; }, 900);
            if (d == null) return null;
            TriggerSetting t = new TriggerSetting();
            t.Mode = d[6];
            t.Travel = (d[11] << 8) | d[7];
            t.Interval1 = (d[13] << 8) | d[9];
            t.Interval2 = (d[14] << 8) | d[10];
            return t;
        }

        public void WriteTrigger(int mode, List<int> indices, int travel, int i1, int i2)
        {
            byte[] payload = new byte[31];
            payload[0] = (byte)mode;
            foreach (int idx in indices)
            {
                int g = idx / 22;
                int o = idx % 22;
                payload[o + 1] = (byte)(payload[o + 1] + (1 << g));
            }
            payload[23] = (byte)(travel & 255);
            payload[24] = (byte)(travel & 255);
            payload[25] = (byte)(i1 & 255);
            payload[26] = (byte)(i2 & 255);
            payload[27] = (byte)((travel >> 8) & 255);
            payload[28] = (byte)((travel >> 8) & 255);
            payload[29] = (byte)((i1 >> 8) & 255);
            payload[30] = (byte)((i2 >> 8) & 255);

            byte[] p = Cmd(33, 0, 0, 0, 24);
            Array.Copy(payload, 0, p, 5, 31);
            Send(p);
        }

        // ---------------- deadband ----------------

        public byte[] ReadDeadband()
        {
            byte[] result = new byte[264];
            bool[] got = new bool[5];
            int count = 0;
            Drain();
            Send(Cmd(38, 0));
            Stopwatch sw = Stopwatch.StartNew();
            while (count < 5 && sw.ElapsedMilliseconds < 2500)
            {
                byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == 38 && x[1] == 0; }, 400);
                if (d == null) continue;
                int page = (d[2] << 8) | d[3];
                if (page < 0 || page > 4) continue;
                int len = Math.Min((int)d[4], 58);
                Array.Copy(d, 5, result, page * 58, len);
                if (!got[page]) { got[page] = true; count++; }
            }
            return result;
        }

        // ReadDeadband ile aynı istek; 5 sayfa da gelmezse eksik veri yerine null döner (doğrulama için).
        public byte[] TryReadDeadband()
        {
            byte[] result = new byte[264];
            bool[] got = new bool[5];
            int count = 0;
            Drain();
            Send(Cmd(38, 0));
            Stopwatch sw = Stopwatch.StartNew();
            while (count < 5 && sw.ElapsedMilliseconds < 2500)
            {
                byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == 38 && x[1] == 0; }, 400);
                if (d == null) continue;
                int page = (d[2] << 8) | d[3];
                if (page < 0 || page > 4) continue;
                int len = Math.Min((int)d[4], 58);
                Array.Copy(d, 5, result, page * 58, len);
                if (!got[page]) { got[page] = true; count++; }
            }
            return count == 5 ? result : null;
        }

        public void WriteDeadband(byte[] data264)
        {
            if (data264.Length != 264) throw new ArgumentException("Deadband 264 bayt olmalı.");
            const int chunk = 58;
            for (int page = 0; page < 5; page++)
            {
                byte[] p = Cmd(38, 1, (byte)(page >> 8), (byte)(page & 255));
                int len = Math.Min(chunk, data264.Length - page * chunk);
                p[4] = (byte)len;
                Array.Copy(data264, page * chunk, p, 5, len);
                Send(p);
                Thread.Sleep(4);
            }
        }

        // ---------------- SOCD (sadece okuma, PROTOKOL.md alt komut 0x01 / 0x02) ----------------

        public List<SocdRule> ReadSocdRules()
        {
            List<SocdRule> rules = new List<SocdRule>();
            bool[] got = new bool[2];
            int count = 0;
            Drain();
            Send(Cmd(0x24, 0x01));
            Stopwatch sw = Stopwatch.StartNew();
            while (count < 2 && sw.ElapsedMilliseconds < 2500)
            {
                byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 44 && x[0] == 0x24 && x[1] == 0x01; }, 400);
                if (d == null) continue;
                int page = (d[2] << 8) | d[3];
                if (page < 0 || page > 1 || got[page]) continue;
                got[page] = true; count++;
                for (int j = 0; j < 10; j++)
                {
                    int o = 5 + 4 * j;
                    int b0 = d[o], model = d[o + 1], k1 = d[o + 2], k2 = d[o + 3];
                    if (b0 == 0) continue;
                    if (b0 == 7 && model == 0xFF && k1 == 0xFF && k2 == 0xFF) continue;
                    SocdRule r = new SocdRule();
                    r.Index = page * 10 + j; r.Model = model; r.Key1 = k1; r.Key2 = k2;
                    rules.Add(r);
                }
            }
            if (count < 2) throw new InvalidOperationException("SOCD kuralları eksik okundu (" + count + "/2 sayfa).");
            rules.Sort(delegate(SocdRule a, SocdRule b) { return a.Index.CompareTo(b.Index); });
            return rules;
        }

        public SocdPower ReadSocdPower()
        {
            Drain();
            Send(Cmd(0x24, 0x02));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 10 && x[0] == 0x24 && x[1] == 0x02; }, 1200);
            if (d == null || d[4] < 6) throw new InvalidOperationException("SOCD durumu okunamadı.");
            SocdPower p = new SocdPower();
            p.On = d[5] == 1;
            p.Shortcut1 = d[9];
            p.Shortcut2 = d[10];
            return p;
        }

        // PROTOKOL.md alt komut 0x00: her zaman iki sayfa (0 ve 1) yazılır, 10 kural x 4 bayt.
        public void WriteSocdRules(List<SocdRule> rules)
        {
            if (rules.Count > 20) throw new ArgumentException("En fazla 20 SOCD kuralı yazılabilir.");
            for (int page = 0; page < 2; page++)
            {
                byte[] p = Cmd(0x24, 0x00, (byte)(page >> 8), (byte)(page & 0xFF), 0x28);
                for (int j = 0; j < 10; j++)
                {
                    int n = page * 10 + j;
                    if (n >= rules.Count) continue;
                    SocdRule r = rules[n];
                    p[5 + 4 * j] = 1;
                    p[5 + 4 * j + 1] = (byte)r.Model;
                    p[5 + 4 * j + 2] = (byte)r.Key1;
                    p[5 + 4 * j + 3] = (byte)r.Key2;
                }
                Send(p);
                Thread.Sleep(20);
            }
        }

        // PROTOKOL.md alt komut 0x03: SOCD aç/kapa.
        public void WriteSocdPower(bool on)
        {
            Send(Cmd(0x24, 0x03, 0, 0, 0x01, (byte)(on ? 1 : 0)));
            Thread.Sleep(100);
        }

        // ---------------- light ----------------

        public LightSetting ReadLight(bool side)
        {
            LightSetting l = new LightSetting();
            Drain();
            Send(Cmd((byte)(side ? 8 : 7), 1));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == (side ? 8 : 7); }, 1200);
            if (d != null && d[4] >= 12)
            {
                l.Mode = d[5];
                l.Brightness = d[6];
                l.Speed = d[7];
                l.R = d[8]; l.G = d[9]; l.B = d[10];
                l.BR = d[11]; l.BG = d[12]; l.BB = d[13];
                l.Direction = d[14];
                if (l.Direction >= 6) l.Direction = 0;
                l.FullColor = d[15];
                l.Power = d[16];
            }
            return l;
        }

        public void WriteLight(bool side, LightSetting l)
        {
            byte[] p = Cmd((byte)(side ? 8 : 7), 0, 0, 0, 14);
            p[5] = (byte)l.Mode;
            p[6] = (byte)l.Brightness;
            p[7] = (byte)l.Speed;
            p[8] = (byte)l.R; p[9] = (byte)l.G; p[10] = (byte)l.B;
            p[11] = (byte)l.BR; p[12] = (byte)l.BG; p[13] = (byte)l.BB;
            p[14] = (byte)l.Direction;
            p[15] = (byte)l.FullColor;
            p[16] = (byte)l.Power;
            Send(p);
        }

        public int[] ReadLightModes(bool side)
        {
            Drain();
            Send(Cmd((byte)(side ? 2 : 10)));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == (side ? 2 : 10); }, 1200);
            if (d == null) return new int[0];
            int len = d[4];
            if (len < 4) return new int[0];
            int n = len - 4;
            int[] modes = new int[n];
            for (int i = 0; i < n; i++) modes[i] = d[5 + i];
            return modes;
        }

        public int[] ReadLightLimits(bool side)
        {
            Drain();
            Send(Cmd((byte)(side ? 2 : 10)));
            byte[] d = _hid.Wait(delegate(byte[] x) { return x.Length > 5 && x[0] == (side ? 2 : 10); }, 1200);
            if (d == null) return new int[] { 4, 4 };
            int len = d[4];
            if (len < 4) return new int[] { 4, 4 };
            return new int[] { d[5 + len - 1], d[5 + len - 2] }; // maxBrightness, maxSpeed
        }
    }
}
