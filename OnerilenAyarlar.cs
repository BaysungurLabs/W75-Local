using System;
using System.Collections.Generic;
using System.IO;

namespace W75Ayarlayici
{
    // 4 profil (cihazın slot sayısı, bkz. DeviceLimits) için önerilen ("fabrika") ayarlar: aktivasyon mesafesi, Rapid
    // Trigger, basma/bırakma ve ışık. Tetikleyici değerleri cihaz biriminde (0.02 mm). Işıkta yalnızca mod (Statik), ön renk,
    // açık/kapalı, tam renk ve parlaklık ayarlanır; hız, yön ve arka renk korunur. Anketleme hızına ve ölü bölgeye
    // dokunulmaz. Yalnızca yerel profili değiştirir, cihaza yazmaz.
    public static class OnerilenAyarlar
    {
        public const string Gunluk = "Günlük";
        public const string Cyberpunk = "Cyberpunk 2077";
        public const string Rpg = "RPG"; // Mass Effect, BG3, Divinity, Pillars
        public const string MassEffect = "Mass Effect LE"; // eski ad (4 slot öncesi): geçişte "RPG" olur, hazır ayar olarak RPG'ye yönlenir
        public const string Valorant = ValorantPreset.ProfileName;

        private class Grup
        {
            public string[] Codes; public int Mode, Travel, I1, I2;
            public Grup(int mode, int travel, int i1, int i2, params string[] codes)
            { Mode = mode; Travel = travel; I1 = i1; I2 = i2; Codes = codes; }
        }

        private class Preset
        {
            public string Name; public int DefaultTravel; public Grup[] Gruplar; public int R, G, B;
        }

        private static readonly string[] WASD = { "KeyW", "KeyA", "KeyS", "KeyD" };

        // Dizideki sıra = slot sırası (0–3).
        private static readonly Preset[] Presets =
        {
            // Işık renkleri bu klavyenin LED yapısına göre gözle dengelenmiştir (kırmızı kanal mavi ve yeşilden zayıf
            // olduğu için ekrandaki renk kodu klavyedeki görünümden farklıdır).
            // Günlük: tüm tuşlar 2.00 mm, RT kapalı; Statik #FF5038
            new Preset { Name = Gunluk, DefaultTravel = 100, Gruplar = new Grup[0], R = 0xFF, G = 0x50, B = 0x38 },
            // Cyberpunk 2077: WASD 1.20 mm RT (basma 0.30, bırakma 0.30); diğerleri 2.00 mm; Statik #FF8C00
            new Preset { Name = Cyberpunk, DefaultTravel = 100, Gruplar = new[] { new Grup(12, 60, 15, 15, WASD) }, R = 0xFF, G = 0x8C, B = 0x00 },
            // RPG (Mass Effect, BG3, Divinity, Pillars): WASD 1.50 mm RT kapalı; diğerleri 2.00 mm; Statik #3A4FE0
            new Preset { Name = Rpg, DefaultTravel = 100, Gruplar = new[] { new Grup(0, 75, 1, 1, WASD) }, R = 0x3A, G = 0x4F, B = 0xE0 },
            // Valorant: WASD 0.80 mm RT (basma 0.16, bırakma 0.20); C Q E X 1.50 mm; diğerleri 1.60 mm; Statik #FF0000
            new Preset { Name = Valorant, DefaultTravel = 80, Gruplar = new[] {
                new Grup(12, 40, 8, 10, WASD),
                new Grup(0, 75, 1, 1, "KeyC", "KeyQ", "KeyE", "KeyX") }, R = 0xFF, G = 0x00, B = 0x00 },
        };

        public static int Count { get { return Presets.Length; } }

        public static bool HasPreset(int slot) { return slot >= 0 && slot < Presets.Length; }

        public static string Name(int slot) { return HasPreset(slot) ? Presets[slot].Name : null; }

        // Profilin önerilen ayarı: önce ADA göre ("Mass Effect LE" eski adı RPG'ye yönlenir), ad tanınmıyorsa
        // (kullanıcı yeniden adlandırdıysa) slota göre.
        private static Preset For(Profile p)
        {
            if (p == null) return null;
            string name = p.Name == MassEffect ? Rpg : p.Name;
            foreach (Preset s in Presets) if (s.Name == name) return s;
            return HasPreset(p.Slot) ? Presets[p.Slot] : null;
        }

        public static bool HasPreset(Profile p) { return For(p) != null; }

        // Tetikleyici + ışık.
        public static void Apply(Profile p)
        {
            ApplyTriggers(p);
            ApplyLight(p);
        }

        // Profilin tetikleyici ayarlarını önerilen değerlere çevirir. Ölü bölge değerleri tuş tuş korunur.
        public static void ApplyTriggers(Profile p)
        {
            Preset s = For(p);
            if (s == null) return;
            KeyboardLayout.Load();

            Dictionary<int, int[]> dead = new Dictionary<int, int[]>();
            foreach (TriggerOverride t in p.Triggers)
                if (t.DeadTop != 0 || t.DeadBottom != 0) dead[t.Index] = new int[] { t.DeadTop, t.DeadBottom };

            p.DefaultTravel = s.DefaultTravel;
            p.DefaultMode = 0;
            p.DefaultI1 = 1;
            p.DefaultI2 = 1;
            p.Triggers.Clear();

            foreach (Grup g in s.Gruplar)
                foreach (string code in g.Codes)
                {
                    int idx = KeyboardLayout.IndexOf(code);
                    if (idx < 0) continue;
                    int[] d;
                    dead.TryGetValue(idx, out d);
                    p.SetTrigger(idx, g.Mode, g.Travel, g.I1, g.I2, d != null ? d[0] : 0, d != null ? d[1] : 0);
                    dead.Remove(idx);
                }
            // Önerilen listede olmayan ama ölü bölgesi olan tuşlar: varsayılan değerle, ölü bölgesiyle kalır.
            foreach (KeyValuePair<int, int[]> kv in dead)
                p.SetTrigger(kv.Key, 0, s.DefaultTravel, 1, 1, kv.Value[0], kv.Value[1]);
        }

        // Önerilen parlaklık (tüm profiller).
        public const int RecommendedBrightness = 1;

        // Işık: Statik, ön renk, açık, tam renk kapalı, parlaklık 1. Hız, yön ve arka renk olduğu gibi kalır.
        public static void ApplyLight(Profile p)
        {
            Preset s = For(p);
            if (s == null) return;
            if (p.Light == null) p.Light = new LightSetting();
            p.Light.Mode = 0;
            p.Light.R = s.R; p.Light.G = s.G; p.Light.B = s.B;
            p.Light.Power = 0;
            p.Light.FullColor = 0;
            p.Light.Brightness = RecommendedBrightness;
        }

        // Profili Sıfırla: tetikleyici + ışık + parlaklık (Apply) ve tuş atamaları varsayılana. Varsayılan keymap = yeni bir
        // profilin başlangıç hâli (new Profile(): Keys ve FnKeys boş); yazma sırasında ataması olmayan her tuş için
        // Keymap.BuildBase, düzendeki varsayılan HID kodunu yazar. Yeni bir keymap tanımlanmaz. Ölü bölge, anketleme hızı,
        // hız, yön, arka renk ve profil adı korunur.
        public static void ResetToRecommended(Profile p)
        {
            Apply(p);
            if (p.Keys == null) p.Keys = new List<KeyAssign>(); else p.Keys.Clear();
            if (p.FnKeys == null) p.FnKeys = new List<KeyAssign>(); else p.FnKeys.Clear();
        }

        // Aktivasyon / RT / basma / bırakma, ışık, parlaklık ve tuş atamaları önerilenle aynı mı (ölü bölge karşılaştırılmaz)?
        public static bool Matches(Profile p)
        {
            if (For(p) == null) return true;
            KeyboardLayout.Load();
            if ((p.Keys != null && p.Keys.Count > 0) || (p.FnKeys != null && p.FnKeys.Count > 0)) return false;
            Profile want = p.Clone();
            Apply(want);
            LightSetting a = p.Light, b = want.Light;
            if (a.Mode != b.Mode || a.R != b.R || a.G != b.G || a.B != b.B || a.Power != b.Power || a.FullColor != b.FullColor || a.Brightness != b.Brightness) return false;
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                TriggerSetting x = p.EffectiveTrigger(k.Index), y = want.EffectiveTrigger(k.Index);
                if (x.Mode != y.Mode || x.Travel != y.Travel || x.Interval1 != y.Interval1 || x.Interval2 != y.Interval2) return false;
            }
            return true;
        }
    }
}
