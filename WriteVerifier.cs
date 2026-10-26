using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    public enum VerifyState { Skipped, Ok, Mismatch, Unreadable }

    // "Cihaza Yaz" sonrası ek doğrulama sonucu (tuş haritası, Fn katmanı, ışık). Aktivasyon/ölü bölge TriggerVerify'da kalır.
    public class WriteCheck
    {
        public VerifyState Base, Fn, Light, Trigger;
        public List<string> Mismatches = new List<string>(); // "<ayar> (yazılan <x>, klavyede <y>)"
        public bool LinkLost;                                 // klavye 5 sn içinde geri gelmedi
    }

    // Tek kapanış kutusunun içeriği ve "Cihazda güncel" işaretinin konup konmayacağı.
    public class WriteOutcome
    {
        public string Text;
        public MessageBoxIcon Icon;
        public bool Mark;
        public string Reason; // günlük için
    }

    // Cihaza Yaz sonrası okuma doğrulamaları (yeni komut göndermez; yalnızca mevcut okuma istekleri). Hid.cs, Protocol.cs ve
    // TriggerVerify.cs'e dokunulmaz: TryReadKeymap, ReadLight, TriggerVerify.EnsureLink olduğu gibi kullanılır.
    // Polling ve profil seçimi bu doğrulamaya girmez (güvenilir okunamıyor).
    public static class WriteVerifier
    {
        private const int Attempts = 3;

        // ------------------------------------------------------------ klavyeden okuyan gerçek akış

        public static WriteCheck Run(W75Device dev, byte[] expBase, byte[] expFn, LightSetting sent, Action<string> log)
        {
            WriteCheck c = new WriteCheck();
            c.Base = c.Fn = c.Light = VerifyState.Skipped;
            Action<string> oldLog = dev.ConnectionLog;
            dev.ConnectionLog = log;
            try
            {
                if (!TriggerVerify.EnsureLink(dev, log)) { c.LinkLost = true; log("Doğrulama: klavye geri gelmedi, tuş haritası ve ışık okunamadı."); return c; }

                c.Base = ReadMap(dev, false, expBase, c, log);
                if (c.LinkLost) return c;
                c.Fn = ReadMap(dev, true, expFn, c, log);
                if (c.LinkLost) return c;
                c.Light = ReadLightCheck(dev, sent, c, log);
            }
            catch (Exception ex)
            {
                log("Doğrulama tamamlanamadı: " + ex.Message);
                if (c.Base == VerifyState.Skipped) c.Base = VerifyState.Unreadable;
                if (c.Fn == VerifyState.Skipped) c.Fn = VerifyState.Unreadable;
                if (c.Light == VerifyState.Skipped) c.Light = VerifyState.Unreadable;
            }
            finally { dev.ConnectionLog = oldLog; }
            return c;
        }

        private static VerifyState ReadMap(W75Device dev, bool fn, byte[] want, WriteCheck c, Action<string> log)
        {
            string name = fn ? "Fn katmanı" : "tuş haritası";
            byte[] got = null;
            for (int a = 1; a <= Attempts && got == null; a++)
            {
                if (a > 1 && !TriggerVerify.EnsureLink(dev, log)) { c.LinkLost = true; return VerifyState.Unreadable; }
                try { got = dev.TryReadKeymap(fn); if (got == null) log("Okunamadı: " + name + ", deneme " + a + "/" + Attempts + " (eksik sayfa)"); }
                catch (Exception ex) { log("Okunamadı: " + name + ", deneme " + a + "/" + Attempts + " (" + ex.Message + ")"); }
                Thread.Sleep(100);
            }
            if (got == null) { log("Doğrulama: " + name + " klavyeden okunamadı."); return VerifyState.Unreadable; }
            VerifyState st = CompareMap(want, got, fn, c.Mismatches);
            log(st == VerifyState.Ok ? "Doğrulama: " + name + " klavyeden aynen okundu (temel katman/Fn: yazma sonrası, bağlantı yenilendikten sonra)." : "UYARI: " + name + " klavyedekiyle uyuşmuyor.");
            return st;
        }

        private static VerifyState ReadLightCheck(W75Device dev, LightSetting sent, WriteCheck c, Action<string> log)
        {
            LightSetting got = null;
            for (int a = 1; a <= 2; a++)
            {
                if (a > 1 && !TriggerVerify.EnsureLink(dev, log)) { c.LinkLost = true; return VerifyState.Unreadable; }
                try { got = dev.ReadLight(false); }
                catch (Exception ex) { log("Işık okunamadı: deneme " + a + "/2 (" + ex.Message + ")"); got = null; }
                // ReadLight cevap gelmezse sahte varsayılan döndürür: yazılan değer varsayılan değilse bu bir okuma sayılmaz.
                if (got != null && IsAllDefault(got) && !IsAllDefault(sent)) { log("Işık okuması varsayılan değerler döndürdü (cevap gelmemiş olabilir), deneme " + a + "/2."); got = null; }
                if (got != null) break;
                Thread.Sleep(100);
            }
            if (got == null) { log("Doğrulama: ışık klavyeden okunamadı (ayrıca doğrulanamadı)."); return VerifyState.Unreadable; }
            VerifyState st = CompareLight(sent, got, c.Mismatches);
            log(st == VerifyState.Ok ? "Doğrulama: ışık klavyeden aynen okundu." : "UYARI: ışık klavyedekiyle uyuşmuyor.");
            return st;
        }

        // ------------------------------------------------------------ karşılaştırmalar (cihazsız da kullanılır)

        public static bool IsAllDefault(LightSetting l)
        {
            LightSetting d = new LightSetting();
            return l.Mode == d.Mode && l.Brightness == d.Brightness && l.Speed == d.Speed && l.Direction == d.Direction && l.FullColor == d.FullColor &&
                   l.Power == d.Power && l.R == d.R && l.G == d.G && l.B == d.B && l.BR == d.BR && l.BG == d.BG && l.BB == d.BB;
        }

        private static string Hex(int r, int g, int b) { return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2"); }
        // Kullanıcıya gösterilen atama metni: bilinen eylemin adı; yoksa Fn'de "atama yok", temelde "devre dışı"; bilinmeyen kod ham gösterilir.
        private static string Entry(byte[] m, int i, bool fn)
        {
            int c1 = m[i * 4], hid = m[i * 4 + 1], c3 = m[i * 4 + 2], c4 = m[i * 4 + 3];
            bool zero = c1 == 0 && hid == 0 && c3 == 0 && c4 == 0;
            if (zero) return fn ? "atama yok" : "devre dışı";
            ActionDef a = Actions.Find(c1, hid, c3, c4);
            if (a != null && a.Code != "__default" && !string.IsNullOrEmpty(a.Label)) return a.Label;
            return "kod " + c1 + "/" + hid.ToString("X2") + "/" + c3 + "/" + c4;
        }

        // Temel katman: 528 baytın tamamı birebir; Fn katmanı: düzendeki tuşların girdileri (boş girdi = atama yok). İlk farkı yazar.
        public static VerifyState CompareMap(byte[] want, byte[] got, bool fn, List<string> mism)
        {
            KeyboardLayout.Load();
            int first = -1;
            if (fn)
            {
                foreach (KeyDef k in KeyboardLayout.Keys)
                {
                    int o = k.Index * 4;
                    if (o + 3 >= want.Length || o + 3 >= got.Length) continue;
                    if (want[o] != got[o] || want[o + 1] != got[o + 1] || want[o + 2] != got[o + 2] || want[o + 3] != got[o + 3]) { first = k.Index; break; }
                }
            }
            else
            {
                for (int i = 0; i < Math.Min(want.Length, got.Length) / 4; i++)
                    if (want[i * 4] != got[i * 4] || want[i * 4 + 1] != got[i * 4 + 1] || want[i * 4 + 2] != got[i * 4 + 2] || want[i * 4 + 3] != got[i * 4 + 3]) { first = i; break; }
            }
            if (first < 0) return VerifyState.Ok;
            KeyDef kd = KeyboardLayout.Get(first);
            string key = kd != null ? KeyLabels.Name(kd) : "slot " + first;
            mism.Add((fn ? "Fn katmanı, " : "tuş ataması, ") + key + " tuşu (yazılan " + Entry(want, first, fn) + ", klavyede " + Entry(got, first, fn) + ")");
            return VerifyState.Mismatch;
        }

        // Yalnızca o ışık modunda geçerli alanlar karşılaştırılır (mod tablosu); klavyeye gönderilen değer (p.Light) esas alınır.
        public static VerifyState CompareLight(LightSetting sent, LightSetting got, List<string> mism)
        {
            int before = mism.Count;
            if (sent.Mode != got.Mode)
            {
                mism.Add("ışık modu (yazılan " + ModeText(sent.Mode) + ", klavyede " + ModeText(got.Mode) + ")");
                return VerifyState.Mismatch;
            }
            ModeOpt o = ModeOptions.Get(sent.Mode);
            if (sent.Power != got.Power) mism.Add("ışık açık/kapalı (yazılan " + (sent.Power == 0 ? "açık" : "kapalı") + ", klavyede " + (got.Power == 0 ? "açık" : "kapalı") + ")");
            if (o.Brightness && sent.Brightness != got.Brightness) mism.Add("parlaklık (yazılan " + sent.Brightness + ", klavyede " + got.Brightness + ")");
            if (o.Speed && sent.Speed != got.Speed) mism.Add("ışık hızı (yazılan " + sent.Speed + ", klavyede " + got.Speed + ")");
            if (o.Dirs.Length > 0 && sent.Direction != got.Direction) mism.Add("ışık yönü (yazılan " + sent.Direction + ", klavyede " + got.Direction + ")");
            if (o.FullColor && sent.FullColor != got.FullColor) mism.Add("tam renk modu (yazılan " + (sent.FullColor != 0 ? "açık" : "kapalı") + ", klavyede " + (got.FullColor != 0 ? "açık" : "kapalı") + ")");
            if (o.FgColor && (sent.R != got.R || sent.G != got.G || sent.B != got.B)) mism.Add("ışık rengi (yazılan " + Hex(sent.R, sent.G, sent.B) + ", klavyede " + Hex(got.R, got.G, got.B) + ")");
            if (o.BgColor && (sent.BR != got.BR || sent.BG != got.BG || sent.BB != got.BB)) mism.Add("arka renk (yazılan " + Hex(sent.BR, sent.BG, sent.BB) + ", klavyede " + Hex(got.BR, got.BG, got.BB) + ")");
            return mism.Count == before ? VerifyState.Ok : VerifyState.Mismatch;
        }

        private static string ModeText(int m)
        {
            string n = ModeOptions.Get(m).Name;
            return string.IsNullOrEmpty(n) ? "mod " + m : n;
        }

        // TriggerVerify günlük satırı "<ne>: beklenen X, okunan Y" → "<ne> (yazılan X, klavyede Y)".
        public static string TriggerDiffText(string logLine)
        {
            int i = logLine.IndexOf(": beklenen ", StringComparison.Ordinal);
            int j = logLine.IndexOf(", okunan ", StringComparison.Ordinal);
            if (i <= 0 || j <= i) return null;
            string what = logLine.Substring(0, i);
            string want = logLine.Substring(i + ": beklenen ".Length, j - (i + ": beklenen ".Length));
            string got = logLine.Substring(j + ", okunan ".Length);
            bool mm = what.IndexOf("modu", StringComparison.Ordinal) < 0;
            return what + " (yazılan " + want + (mm ? " mm" : "") + ", klavyede " + got + (mm ? " mm" : "") + ")";
        }

        // ------------------------------------------------------------ tek sonuç kutusu

        public static WriteOutcome Compose(WriteCheck c, TriggerVerifyResult tv, List<string> triggerDiffs)
        {
            WriteOutcome o = new WriteOutcome();
            c.Trigger = tv.Ok ? VerifyState.Ok : (tv.Diffs > 0 ? VerifyState.Mismatch : VerifyState.Unreadable);

            if (c.LinkLost)
            {
                o.Icon = MessageBoxIcon.Warning; o.Mark = false; o.Reason = "klavye geri gelmedi";
                o.Text = "Klavye yazmadan sonra 5 saniye içinde geri gelmedi, ayarlar doğrulanamadı.\n\nUSB kablosunu çıkarıp takın, sonra 'Cihazdan Oku' ile kontrol edin.";
                return o;
            }

            List<string> all = new List<string>(c.Mismatches);
            foreach (string t in triggerDiffs) all.Add(t);
            if (all.Count == 0 && c.Trigger == VerifyState.Mismatch) all.Add("aktivasyon (" + tv.Diffs + " değer)"); // satır yakalanamadıysa
            if (all.Count > 0)
            {
                o.Icon = MessageBoxIcon.Warning; o.Mark = false; o.Reason = "uyuşmazlık: " + all[0];
                if (all.Count == 1)
                    o.Text = "Klavye şu ayarı farklı gösteriyor: " + all[0] + ".\n\nProgram kendiliğinden tekrar yazmadı. İsterseniz 'Cihaza Yaz'a yeniden basın.";
                else
                {
                    StringBuilder sb = new StringBuilder("Klavye şu ayarları farklı gösteriyor:\n");
                    for (int i = 0; i < all.Count && i < 3; i++) sb.Append("• ").Append(all[i]).Append("\n");
                    if (all.Count > 3) sb.Append("• … ve ").Append(all.Count - 3).Append(" ayar daha (ayrıntı Cihaz sekmesindeki günlükte)\n");
                    sb.Append("\nProgram kendiliğinden tekrar yazmadı. İsterseniz 'Cihaza Yaz'a yeniden basın.");
                    o.Text = sb.ToString();
                }
                return o;
            }

            List<string> unread = new List<string>();
            if (c.Base == VerifyState.Unreadable || c.Fn == VerifyState.Unreadable) unread.Add("tuş atamaları");
            if (c.Trigger == VerifyState.Unreadable) unread.Add("aktivasyon");
            if (unread.Count > 0)
            {
                o.Icon = MessageBoxIcon.Warning; o.Mark = false; o.Reason = "okunamadı: " + string.Join(", ", unread.ToArray());
                o.Text = "Profil yazıldı ama " + string.Join(" ve ", unread.ToArray()) + " klavyeden okunamadığı için doğrulanamadı.\n\n'Cihazdan Oku' ile kontrol edebilirsiniz.";
                return o;
            }

            if (c.Light == VerifyState.Unreadable)
            {
                o.Icon = MessageBoxIcon.Information; o.Mark = true; o.Reason = null;
                o.Text = "Profil yazıldı. Tuş atamaları ve aktivasyon doğrulandı; ışık ayrıca doğrulanamadı.\n\nKlavyenin ışığına bakmanız yeterli.";
                return o;
            }

            o.Icon = MessageBoxIcon.Information; o.Mark = true; o.Reason = null;
            o.Text = "Profil klavyeye yazıldı ve doğrulandı: tuş atamaları, aktivasyon ve ışık klavyedekiyle aynı.";
            return o;
        }

        // ------------------------------------------------------------ test kipi simülasyonu

        // W75_SIM_DOGRULAMA (yalnızca test kipinde): ok | isik-farkli | fn-farkli | isik-okunamadi | geri-gelmedi.
        // Klavyeden okuma yerine, beklenen değerlerden türetilen "okunan" değerler aynı karşılaştırma koduna verilir.
        public static string SimScenario()
        {
            string s = Environment.GetEnvironmentVariable("W75_SIM_DOGRULAMA");
            return string.IsNullOrEmpty(s) ? null : s.Trim().ToLowerInvariant();
        }

        public static WriteCheck Simulate(Profile p, string scenario, byte[] expBase, byte[] expFn)
        {
            WriteCheck c = new WriteCheck();
            if (scenario == "geri-gelmedi") { c.LinkLost = true; c.Base = c.Fn = c.Light = VerifyState.Unreadable; return c; }
            byte[] gotBase = (byte[])expBase.Clone();
            byte[] gotFn = (byte[])expFn.Clone();
            LightSetting sent = p.Light.Clone();
            LightSetting got = sent.Clone();
            if (scenario == "fn-farkli")
            {
                KeyboardLayout.Load();
                int idx = KeyboardLayout.IndexOf("KeyW"); if (idx < 0) idx = KeyboardLayout.Keys[0].Index;
                gotFn[idx * 4] = 0; gotFn[idx * 4 + 1] = 0x1A; // klavye, yazılmayan bir Fn ataması gösteriyor
                if (expFn[idx * 4 + 1] == 0x1A) gotFn[idx * 4 + 1] = 0x04;
            }
            if (scenario == "isik-farkli") { got.R = 0xFF; got.G = 0x00; got.B = 0x00; if (sent.R == 0xFF && sent.G == 0 && sent.B == 0) got.B = 0xFF; }
            c.Base = CompareMap(expBase, gotBase, false, c.Mismatches);
            c.Fn = CompareMap(expFn, gotFn, true, c.Mismatches);
            if (scenario == "isik-okunamadi") got = new LightSetting(); // ReadLight zaman aşımı: sahte varsayılan
            if (IsAllDefault(got) && !IsAllDefault(sent)) c.Light = VerifyState.Unreadable;
            else c.Light = CompareLight(sent, got, c.Mismatches);
            return c;
        }
    }
}
