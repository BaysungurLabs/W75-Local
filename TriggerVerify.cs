using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace W75Ayarlayici
{
    public class TriggerVerifyResult
    {
        public int Diffs;       // okunup profille uyuşmayan değer sayısı
        public int Unreadable;  // 3 denemede de okunamayan tuş sayısı
        public bool Aborted;    // doğrulama yarıda bırakıldı
        public bool Ok { get { return Diffs == 0 && Unreadable == 0 && !Aborted; } }
    }

    // "Cihaza Yaz" sonrası aktivasyon doğrulaması. Yeni komut göndermez; yalnızca
    // okuma isteklerini (ReadTrigger / ReadDeadband ile aynı baytlar) kullanır. Hata verirse
    // programı durdurmaz, durumu günlüğe yazar. Okunamayan değer için değer uydurmaz.
    // Cihaz USB'den düşüp geri gelirse bağlantının yenilenmesi beklenir; komut kendiliğinden
    // tekrar gönderilmez, okunamayan tuş bağlantı geri gelince yeniden sorulur.
    public static class TriggerVerify
    {
        private const int MaxLoggedDiffs = 40;
        private const int Attempts = 3;
        private const int SettleMs = 3000;      // yazma bittikten sonra cihazın toparlanması için
        private const int PollMs = 100;         // bekleme sırasında cihazın varlığını yoklama aralığı
        private const int AfterRequestMs = 8;   // her istekten sonra
        private const int RetryPauseMs = 100;   // tekrar denemeden önce
        private const int ReopenTotalMs = 5000; // cihazın geri gelmesi için en çok bekleme
        private const int ReopenStepMs = 300;
        private const int MaxReconnectBonus = 3; // bir tuşta bağlantı yenilenmesi deneme hakkından düşmez (en çok bu kadar)
        private const int MaxConsecutiveFails = 3;

        public static TriggerVerifyResult Run(W75Device dev, Profile p, double unit, Action<string> log)
        {
            TriggerVerifyResult res = new TriggerVerifyResult();
            Action<string> oldLog = dev.ConnectionLog;
            dev.ConnectionLog = log;
            try
            {
                if (!Settle(dev, log))
                {
                    log("Doğrulama tamamlanamadı: cihaz " + (ReopenTotalMs / 1000) + " saniye içinde geri gelmedi.");
                    res.Aborted = true;
                    return Finish(res, log);
                }

                byte[] db = null;
                for (int a = 1; a <= Attempts && db == null; a++)
                {
                    if (a > 1 && !EnsureLink(dev, log)) { res.Aborted = true; break; }
                    int before = dev.Reconnects;
                    try { db = dev.TryReadDeadband(); if (db == null) log("Ölü bölge okunamadı: deneme " + a + "/" + Attempts + " (cevap eksik)"); }
                    catch (Exception ex) { log("Ölü bölge okunamadı: deneme " + a + "/" + Attempts + " (" + ex.Message + ")"); }
                    Thread.Sleep(a < Attempts && db == null ? RetryPauseMs : AfterRequestMs);
                    if (db == null && dev.Reconnects != before) a--; // bağlantı yenilendi: bu deneme sayılmasın
                }
                if (res.Aborted)
                {
                    log("Doğrulama tamamlanamadı: cihaz geri gelmedi.");
                    return Finish(res, log);
                }
                if (db == null)
                {
                    log("Ölü bölge değerleri okunamadı, bu değerler doğrulanmadı.");
                    res.Unreadable++;
                }

                int consecutive = 0;
                foreach (KeyDef k in KeyboardLayout.Keys)
                {
                    string name = KeyLabels.Name(k);
                    TriggerSetting got = null;
                    int bonus = 0;
                    bool linkLost = false;
                    for (int a = 1; a <= Attempts && got == null; a++)
                    {
                        // Okunamayan tuşu yeniden sormadan önce bağlantının kurulu olmasını bekle.
                        if (a > 1 && !EnsureLink(dev, log)) { linkLost = true; break; }
                        int before = dev.Reconnects;
                        try
                        {
                            got = dev.TryReadTrigger(k.Index);
                            if (got == null) log("Okunamadı: " + name + " (indeks " + k.Index + "), deneme " + a + "/" + Attempts + " (cevap gelmedi)");
                        }
                        catch (Exception ex)
                        {
                            log("Okunamadı: " + name + " (indeks " + k.Index + "), deneme " + a + "/" + Attempts + " (" + ex.Message + ")");
                        }
                        Thread.Sleep(a < Attempts && got == null ? RetryPauseMs : AfterRequestMs);
                        if (got == null && dev.Reconnects != before && bonus < MaxReconnectBonus) { bonus++; a--; } // bağlantı yenilendi: bu deneme sayılmasın
                    }

                    if (linkLost)
                    {
                        log("Doğrulama tamamlanamadı: " + name + " (indeks " + k.Index + ") okunurken cihaz " + (ReopenTotalMs / 1000) + " saniye içinde geri gelmedi.");
                        res.Unreadable++;
                        res.Aborted = true;
                        break;
                    }
                    if (got == null)
                    {
                        log(name + " (indeks " + k.Index + "): okunamadı, " + Attempts + " denemede de cevap alınamadı.");
                        res.Unreadable++;
                        consecutive++;
                        if (consecutive >= MaxConsecutiveFails)
                        {
                            log("Doğrulama tamamlanamadı: art arda " + consecutive + " tuş okunamadı, cihaz yanıt vermiyor olabilir.");
                            res.Aborted = true;
                            break;
                        }
                        continue;
                    }
                    consecutive = 0;

                    TriggerSetting want = p.EffectiveTrigger(k.Index);
                    if (want.Travel != got.Travel) res.Diffs += Report(log, res.Diffs, name + " mesafe", Mm(want.Travel, unit), Mm(got.Travel, unit));
                    if (want.Mode != got.Mode) res.Diffs += Report(log, res.Diffs, name + " Rapid Trigger modu", ModeText(want.Mode), ModeText(got.Mode));
                    if (want.Interval1 != got.Interval1) res.Diffs += Report(log, res.Diffs, name + " basma hassasiyeti", Mm(want.Interval1, unit), Mm(got.Interval1, unit));
                    if (want.Interval2 != got.Interval2) res.Diffs += Report(log, res.Diffs, name + " bırakma hassasiyeti", Mm(want.Interval2, unit), Mm(got.Interval2, unit));
                    if (db != null)
                    {
                        int gotTop = db[k.Index * 2];
                        int gotBottom = db[k.Index * 2 + 1];
                        if (want.DeadTop != gotTop) res.Diffs += Report(log, res.Diffs, name + " üst ölü bölge", Mm(want.DeadTop, unit), Mm(gotTop, unit));
                        if (want.DeadBottom != gotBottom) res.Diffs += Report(log, res.Diffs, name + " alt ölü bölge", Mm(want.DeadBottom, unit), Mm(gotBottom, unit));
                    }
                }
            }
            catch (Exception ex)
            {
                log("Doğrulama tamamlanamadı: " + ex.Message);
                res.Aborted = true;
            }
            finally
            {
                dev.ConnectionLog = oldLog;
            }
            return Finish(res, log);
        }

        private static TriggerVerifyResult Finish(TriggerVerifyResult res, Action<string> log)
        {
            if (res.Ok) log("Doğrulama: tetikleyici ayarları doğrulandı.");
            else
            {
                log("UYARI: tetikleyici doğrulaması eksik/farklı — " + res.Diffs + " fark, " + res.Unreadable + " okunamayan" +
                    (res.Aborted ? ", doğrulama yarıda kaldı" : "") + (res.Diffs > MaxLoggedDiffs ? " (ilk " + MaxLoggedDiffs + " fark yazıldı)." : "."));
            }
            return res;
        }

        // Yazma sonrası 3 saniye bekler. Bu sürede cihazın düşüp geri gelmesini günlüğe yazar;
        // düşmüşse bağlantıyı yeniler. Doğrulamaya devam edilebiliyorsa true döner.
        public static bool Settle(W75Device dev, Action<string> log)
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool wasPresent = dev.IsPresent();
            bool dropped = false;
            if (!wasPresent) { log("Cihaz düştü"); dropped = true; }
            while (sw.ElapsedMilliseconds < SettleMs)
            {
                Thread.Sleep(PollMs);
                bool present = dev.IsPresent();
                if (wasPresent && !present) { log("Cihaz düştü"); dropped = true; }
                wasPresent = present; // "Cihaz geri geldi" yazısı bağlantı yenilenince Reopen içinde yazılır
            }
            if (dropped || !dev.IsOpen)
            {
                // Eski bağlantı geçersiz olabilir: yeniden aç (geri gelme günlüğü Reopen içinde yazılır).
                if (!wasPresent) log("Cihaz hâlâ yok, geri gelmesi bekleniyor…");
                return dev.Reopen(ReopenTotalMs, ReopenStepMs);
            }
            return true;
        }

        // Bağlantı kopuksa en çok 5 saniye boyunca 300 ms arayla yeniden açmayı dener.
        public static bool EnsureLink(W75Device dev, Action<string> log)
        {
            if (dev.IsOpen && dev.IsPresent()) return true;
            if (!dev.IsOpen) return dev.Reopen(ReopenTotalMs, ReopenStepMs);
            // Arayüz kayboldu ama eski bağlantı nesnesi duruyor: düştü say, yeniden aç.
            log("Cihaz düştü");
            return dev.Reopen(ReopenTotalMs, ReopenStepMs);
        }

        private static int Report(Action<string> log, int soFar, string what, string want, string got)
        {
            if (soFar < MaxLoggedDiffs) log(what + ": beklenen " + want + ", okunan " + got);
            return 1;
        }

        private static string Mm(int raw, double unit)
        {
            return (raw * unit).ToString("0.00");
        }

        private static string ModeText(int mode)
        {
            if (mode == 0) return "Kapalı";
            if (mode == 12) return "RT";
            if (mode == 13) return "RT (ayrı)";
            return "mod " + mode;
        }
    }
}
