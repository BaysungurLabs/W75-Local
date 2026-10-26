using System;

namespace W75Ayarlayici
{
    // Slot 3 için Valorant profili (cihaz 4 profil saklar: slot 0-3). Yalnızca yerel profili üretir, cihaza yazmaz.
    // (5 slotlu eski düzende Valorant slot 4'teydi; v6 geçişi onu slot 3'e taşır.)
    public static class ValorantPreset
    {
        public const string ProfileName = "Valorant";

        // dailyBase: ışığın (parlaklık, hız, yön, arka renk) ve anketleme hızının kopyalanacağı Günlük profili.
        public static Profile Create(Profile dailyBase)
        {
            Profile p = new Profile();
            p.Slot = 3;
            p.Name = ProfileName;
            p.Light = dailyBase.Light.Clone();
            OnerilenAyarlar.Apply(p); // WASD 0.80 mm RT, C Q E X 1.50 mm, diğerleri 1.60 mm, Statik #FF0000
            return p;
        }

        // Yalnızca v2 geçişi (eski düzen) için: slot 3 hâlâ "Boş 4" ise ve içinde gerçek bir atama yoksa true.
        // Boş cihaz slotundan okunmuş "hepsi Devre dışı" tuş listesi de boş sayılır.
        public static bool IsUntouchedPlaceholder(Profile p)
        {
            if (p == null || p.Name != "Boş 4" || p.FnKeys.Count != 0 || p.Triggers.Count != 0) return false;
            foreach (KeyAssign k in p.Keys)
                if (k.C1 != 0 || k.Hid != 0 || k.C3 != 0 || k.C4 != 0) return false;
            return true;
        }
    }
}
