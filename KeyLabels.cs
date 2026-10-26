using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace W75Ayarlayici
{
    public class LabelRow
    {
        public string index, code, hid, us, usShift, tr, trShift, trAltGr;
    }

    public class LabelRoot
    {
        public List<LabelRow> keys;
    }

    // Tuş yazıları: "TR (Türkçe Q)" ve "US (İngilizce Q)" düzenleri arasında seçilebilir.
    // Yalnızca GÖRÜNEN etiketleri değiştirir; tuş kodları (HID), profiller ve cihaza yazılan veri aynı kalır.
    // Etiket tablosu etiketler.json'dan gelir (araclar\etiket-uret.ps1, Windows klavye düzeni verisi).
    public static class KeyLabels
    {
        public static bool Tr = true; // varsayılan TR
        private static Dictionary<string, LabelRow> _byCode;

        public static string ModeName { get { return Tr ? "TR" : "US"; } }

        private static void Ensure()
        {
            if (_byCode != null) return;
            _byCode = new Dictionary<string, LabelRow>();
            try
            {
                LabelRoot r = new JavaScriptSerializer().Deserialize<LabelRoot>(KeyboardLayout.ReadResource("etiketler.json"));
                if (r != null && r.keys != null) foreach (LabelRow row in r.keys) _byCode[row.code] = row;
            }
            catch { }
        }

        private static LabelRow Get(string code)
        {
            Ensure();
            LabelRow r;
            return code != null && _byCode.TryGetValue(code, out r) ? r : null;
        }

        private static string SettingsPath { get { return Path.Combine(Store.Dir, "pencere.json"); } }

        // Kayıtlı seçimi (pencere.json) okur. Test için W75_ETIKET=TR|US ortam değişkeni kaydı geçersiz kılar (kaydedilmez).
        public static void LoadSavedMode()
        {
            string env = Environment.GetEnvironmentVariable("W75_ETIKET");
            if (!string.IsNullOrEmpty(env)) { Tr = !string.Equals(env, "US", StringComparison.OrdinalIgnoreCase); return; }
            try
            {
                if (!File.Exists(SettingsPath)) return;
                WindowBoundsData d = new JavaScriptSerializer().Deserialize<WindowBoundsData>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                if (d != null && !string.IsNullOrEmpty(d.KeyLabelMode)) Tr = !string.Equals(d.KeyLabelMode, "US", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        public static void SetMode(bool tr, bool persist)
        {
            Tr = tr;
            if (!persist) return;
            try
            {
                WindowBoundsData d = null;
                try { if (File.Exists(SettingsPath)) d = new JavaScriptSerializer().Deserialize<WindowBoundsData>(File.ReadAllText(SettingsPath, Encoding.UTF8)); }
                catch { }
                if (d == null) d = new WindowBoundsData();
                d.KeyLabelMode = ModeName;
                File.WriteAllText(SettingsPath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }

        // Karakter üreten (harf, rakam, noktalama) tuş mu? Bu tuşların yazısı düzene göre değişir.
        public static bool IsCharKey(string code)
        {
            LabelRow r = Get(code);
            return r != null && !string.IsNullOrEmpty(r.usShift);
        }

        // Listelerde ve günlükte kullanılan tam ad ("L-Shift", "AltGr", "Ğ" ...).
        public static string Name(KeyDef k)
        {
            LabelRow r = Get(k.Code);
            if (r != null)
            {
                string s = Tr ? r.tr : r.us;
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return string.IsNullOrEmpty(k.Name) ? "Knob" : k.Name;
        }

        // Klavye çiziminde tuşun üstündeki kısa yazı.
        public static string Cap(KeyDef k)
        {
            if (string.IsNullOrEmpty(k.Name)) return "KNOB";
            if (IsCharKey(k.Code)) return Name(k);
            switch (k.Code)
            {
                case "Backspace": return "Bksp";
                case "Enter": return "Ent";
                case "Tab": return "Tab";
                case "CapsLock": return "Caps";
                case "ShiftLeft": return "Shift";
                case "ShiftRight": return "Shift";
                case "ControlLeft": return "Ctrl";
                case "ControlRight": return "Ctrl";
                case "AltLeft": return "Alt";
                case "AltRight": return Tr ? "AltGr" : "Alt";
                case "MetaLeft": return "Win";
                case "MetaRight": return "Win";
                case "Space": return "Space";
                case "RotaryKnob": return "KNOB";
                case "KeyFn": return "Fn";
                case "Delete": return "Del";
                case "PageUp": return "PgUp";
                case "PageDown": return "PgDn";
                case "Escape": return "Esc";
                case "PrintScreen": return "PrtSc";
                case "ScrollLock": return "ScrLk";
                case "ContextMenu": return "Menu";
            }
            string n = Name(k);
            if (n == "Back") return "Bksp";
            return n;
        }

        // Eylem listesinde görünen ad. Eylemin gerçek kodu/içeriği değişmez; yalnızca yazısı düzene uyar.
        public static string ActionLabel(ActionDef a)
        {
            if (a == null) return "";
            if (IsCharKey(a.Code)) { LabelRow r = Get(a.Code); return Tr ? r.tr : r.us; }
            if (a.Code == "AltRight") return Tr ? "AltGr" : "Sağ Alt";
            return a.Label;
        }
    }
}
