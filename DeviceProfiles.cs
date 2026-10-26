using System;

namespace W75Ayarlayici
{
    // Program slotu (0-3, profiles.json'daki numara) → cihazın profil numarası (1-4) eşlemesi.
    // Cihaz profilleri 1-4 diye numaralar; 0 etkisizdir (gerçek klavyede görüldü: SetProfile(0) profili değiştirmez, yazmalar o an
    // etkin olan profile gider). Bu yüzden Günlük (slot 0) cihazın 4. profilidir. Eşleme YALNIZCA cihaza giden komutlarda
    // uygulanır; profiles.json, yazim-durumu.json, yedekler ve İçe Aktar biçimleri program slotunu kullanmaya devam eder.
    // Cihaza profil numarası göndermenin tek yolu Select'tir: 1-4 dışında hiçbir değer (0 dahil) gönderilmez.
    public static class DeviceProfiles
    {
        public const int MinNumber = 1;
        public const int MaxNumber = 4;

        // slot 0 → 4, 1 → 1, 2 → 2, 3 → 3.
        public static int ToDevice(int slot)
        {
            if (!DeviceLimits.SlotAllowed(slot)) throw new InvalidOperationException(DeviceLimits.Message(slot));
            int n = slot == 0 ? 4 : slot;
            if (n < MinNumber || n > MaxNumber)
                throw new InvalidOperationException("Cihaz profil numarası " + n + " geçersiz; cihaz yalnızca " + MinNumber + "-" + MaxNumber + " numaralı profilleri tanır. Klavyeye komut gönderilmedi.");
            return n;
        }

        // Ters eşleme: cihaz profil numarası (1-4) → program slotu. Geçersiz numara -1.
        public static int FromDevice(int number)
        {
            if (number < MinNumber || number > MaxNumber) return -1;
            return number == 4 ? 0 : number;
        }

        public static string Describe(int slot)
        {
            int n;
            try { n = ToDevice(slot); } catch { return "slot " + slot + " (cihaza gönderilmez)"; }
            return "slot " + slot + " → cihaz profili " + n;
        }

        // Cihaza profil seçme komutu: tek geçit. Dönüş: cihaza giden profil numarası.
        public static int Select(W75Device dev, int slot)
        {
            int n = ToDevice(slot); // geçersizse burada fırlar, hiçbir şey gönderilmez
            dev.SetProfile(n);
            return n;
        }
    }
}
