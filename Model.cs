using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace W75Ayarlayici
{
    public class KeyDef
    {
        public int Index;
        public string Name;
        public string Code;
        public int HidCode;
        public double X, Y, W, H;

        public string Display
        {
            get
            {
                return KeyLabels.Name(this); // seçili tuş yazısı düzenine (TR/US) uyar
            }
        }
    }

    public class ActionDef
    {
        public string Group;
        public string Label;
        public string Code;
        public int C1, Hid, C3, C4;

        public override string ToString()
        {
            return KeyLabels.ActionLabel(this) + "  ·  " + Group;
        }
    }

    public class KeyAssign
    {
        public int Index;
        public string Code;
        public string Label;
        public int C1, Hid, C3, C4;
    }

    public class TriggerOverride
    {
        public int Index;
        public int Mode;
        public int Travel = 85;
        public int I1 = 1;
        public int I2 = 1;
        public int DeadTop;
        public int DeadBottom;
    }

    public class Profile
    {
        public string Name = "Profil";
        public int Slot;
        public List<KeyAssign> Keys = new List<KeyAssign>();
        public List<KeyAssign> FnKeys = new List<KeyAssign>();
        public List<TriggerOverride> Triggers = new List<TriggerOverride>();
        public LightSetting Light = new LightSetting();
        // Polling artık profile özel değil (genel.json, GenelAyar). Eski dosyalardaki "PollRate" yalnızca göç için okunur
        // (set-only özellik: dosyaya yazılmaz); değer LegacyPollRate'te tutulur, -1 = dosyada yoktu.
        [ScriptIgnore] public int LegacyPollRate = -1;
        public int PollRate { set { LegacyPollRate = value; } }
        public int DefaultTravel = 85;
        public int DefaultMode;
        public int DefaultI1 = 1;
        public int DefaultI2 = 1;

        public KeyAssign FindKey(int index, bool fn)
        {
            List<KeyAssign> list = fn ? FnKeys : Keys;
            for (int i = 0; i < list.Count; i++) if (list[i].Index == index) return list[i];
            return null;
        }

        public void SetKey(int index, ActionDef a, bool fn)
        {
            List<KeyAssign> list = fn ? FnKeys : Keys;
            KeyAssign cur = FindKey(index, fn);
            if (a == null || a.Code == "__default")
            {
                if (cur != null) list.Remove(cur);
                return;
            }
            if (cur == null) { cur = new KeyAssign(); cur.Index = index; list.Add(cur); }
            cur.Code = a.Code;
            cur.Label = a.Label;
            cur.C1 = a.C1;
            cur.Hid = a.Hid;
            cur.C3 = a.C3;
            cur.C4 = a.C4;
        }

        public void SetKeyRaw(int index, string code, string label, int c1, int hid, int c3, int c4, bool fn)
        {
            List<KeyAssign> list = fn ? FnKeys : Keys;
            KeyAssign cur = FindKey(index, fn);
            if (cur == null) { cur = new KeyAssign(); cur.Index = index; list.Add(cur); }
            cur.Code = code; cur.Label = label; cur.C1 = c1; cur.Hid = hid; cur.C3 = c3; cur.C4 = c4;
        }

        public TriggerOverride FindTrigger(int index)
        {
            for (int i = 0; i < Triggers.Count; i++) if (Triggers[i].Index == index) return Triggers[i];
            return null;
        }

        public TriggerOverride EnsureTrigger(int index)
        {
            TriggerOverride t = FindTrigger(index);
            if (t == null)
            {
                t = new TriggerOverride();
                t.Index = index;
                t.Mode = DefaultMode;
                t.Travel = DefaultTravel;
                t.I1 = DefaultI1;
                t.I2 = DefaultI2;
                Triggers.Add(t);
            }
            return t;
        }

        public TriggerSetting EffectiveTrigger(int index)
        {
            TriggerOverride t = FindTrigger(index);
            TriggerSetting s = new TriggerSetting();
            if (t != null)
            {
                s.Mode = t.Mode; s.Travel = t.Travel; s.Interval1 = t.I1; s.Interval2 = t.I2;
                s.DeadTop = t.DeadTop; s.DeadBottom = t.DeadBottom;
            }
            else
            {
                s.Mode = DefaultMode; s.Travel = DefaultTravel; s.Interval1 = DefaultI1; s.Interval2 = DefaultI2;
            }
            return s;
        }

        public void SetTrigger(int index, int mode, int travel, int i1, int i2, int deadTop, int deadBottom)
        {
            TriggerOverride t = EnsureTrigger(index);
            t.Mode = mode; t.Travel = travel; t.I1 = i1; t.I2 = i2;
            t.DeadTop = deadTop; t.DeadBottom = deadBottom;
        }

        public Profile Clone()
        {
            Profile p = new Profile();
            p.Name = Name; p.Slot = Slot; p.LegacyPollRate = LegacyPollRate;
            p.DefaultTravel = DefaultTravel; p.DefaultMode = DefaultMode; p.DefaultI1 = DefaultI1; p.DefaultI2 = DefaultI2;
            p.Light = Light.Clone();
            foreach (KeyAssign k in Keys) p.Keys.Add(Copy(k));
            foreach (KeyAssign k in FnKeys) p.FnKeys.Add(Copy(k));
            foreach (TriggerOverride t in Triggers)
            {
                TriggerOverride x = new TriggerOverride();
                x.Index = t.Index; x.Mode = t.Mode; x.Travel = t.Travel; x.I1 = t.I1; x.I2 = t.I2;
                x.DeadTop = t.DeadTop; x.DeadBottom = t.DeadBottom;
                p.Triggers.Add(x);
            }
            return p;
        }

        private static KeyAssign Copy(KeyAssign k)
        {
            KeyAssign x = new KeyAssign();
            x.Index = k.Index; x.Code = k.Code; x.Label = k.Label; x.C1 = k.C1; x.Hid = k.Hid; x.C3 = k.C3; x.C4 = k.C4;
            return x;
        }
    }

    public class AppData
    {
        public List<Profile> Profiles = new List<Profile>();
        public int SelectedSlot;
        public int SchemaVersion;
    }

    public static class KeyboardLayout
    {
        public static List<KeyDef> Keys = new List<KeyDef>();
        public static Dictionary<int, KeyDef> ByIndex = new Dictionary<int, KeyDef>();
        public static double Width = 648, Height = 269;

        // Alanlar JavaScriptSerializer ile doldurulur; "= null" yalnızca derleyici uyarısını (CS0649) önler.
        private class Root { public List<Item> keys = null; }
        private class Item
        {
            public string index = null, name = null, code = null, hidCode = null, width = null, height = null, x = null, y = null;
        }

        public static void Load()
        {
            if (Keys.Count > 0) return;
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string json = ReadResource("layout.json");
            Root r = ser.Deserialize<Root>(json);
            Keys.Clear(); ByIndex.Clear();
            double maxX = 0, maxY = 0;
            foreach (Item it in r.keys)
            {
                KeyDef k = new KeyDef();
                k.Index = int.Parse(it.index, CultureInfo.InvariantCulture);
                k.Name = it.name;
                k.Code = it.code;
                k.HidCode = ParseHex(it.hidCode);
                k.W = ParseDouble(it.width);
                k.H = ParseDouble(it.height);
                k.X = ParseDouble(it.x);
                k.Y = ParseDouble(it.y);
                Keys.Add(k);
                ByIndex[k.Index] = k;
                if (k.X + k.W > maxX) maxX = k.X + k.W;
                if (k.Y + k.H > maxY) maxY = k.Y + k.H;
            }
            Width = maxX + 15;
            Height = maxY + 13;
        }

        public static KeyDef Get(int index)
        {
            KeyDef k;
            return ByIndex.TryGetValue(index, out k) ? k : null;
        }

        public static KeyDef GetByCode(string code)
        {
            foreach (KeyDef k in Keys) if (k.Code == code) return k;
            return null;
        }

        public static int IndexOf(string code)
        {
            KeyDef k = GetByCode(code);
            return k == null ? -1 : k.Index;
        }

        private static double ParseDouble(string s)
        {
            double d;
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
            return d;
        }

        private static int ParseHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            int v;
            int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
            return v;
        }

        public static string ReadResource(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream st = asm.GetManifestResourceStream(name))
            {
                if (st == null) throw new FileNotFoundException("Gömülü kaynak bulunamadı: " + name);
                using (StreamReader rd = new StreamReader(st, Encoding.UTF8))
                {
                    return rd.ReadToEnd();
                }
            }
        }
    }

    public static class Actions
    {
        public static List<ActionDef> All = new List<ActionDef>();
        private static Dictionary<string, ActionDef> _byCode = new Dictionary<string, ActionDef>();
        private static Dictionary<int, List<ActionDef>> _byGroup = new Dictionary<int, List<ActionDef>>();

        public static void Load()
        {
            if (All.Count > 0) return;
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string json = KeyboardLayout.ReadResource("actions.json");
            List<ActionDef> list = ser.Deserialize<List<ActionDef>>(json);
            All.Clear(); _byCode.Clear();
            foreach (ActionDef a in list)
            {
                All.Add(a);
                _byCode[a.Code] = a;
            }
        }

        public static ActionDef Get(string code)
        {
            ActionDef a;
            return _byCode.TryGetValue(code, out a) ? a : null;
        }

        public static ActionDef Find(int c1, int hid, int c3, int c4)
        {
            ActionDef best = null;
            foreach (ActionDef a in All)
            {
                if (a.Code == "__default") continue;
                if (a.C1 == c1 && a.Hid == hid && a.C3 == c3 && a.C4 == c4)
                {
                    if (a.Code == "__none") return a;
                    if (best == null) best = a;
                }
            }
            return best;
        }

        public static List<ActionDef> Search(string text)
        {
            if (string.IsNullOrEmpty(text)) return All;
            text = text.ToLowerInvariant();
            List<ActionDef> r = new List<ActionDef>();
            foreach (ActionDef a in All)
                if (KeyLabels.ActionLabel(a).ToLowerInvariant().Contains(text) || a.Label.ToLowerInvariant().Contains(text) || a.Group.ToLowerInvariant().Contains(text) || a.Code.ToLowerInvariant().Contains(text))
                    r.Add(a);
            return r;
        }
    }

    public static class Keymap
    {
        public const int DefaultTravel = 85;
        public const int DefaultI1 = 1;
        public const int DefaultI2 = 1;

        // Yazılacak 528 baytlık temel tuş haritasını kurar. Başlangıç noktası cihazdan OKUNAN haritadır (deviceBase):
        // düzendeki bir tuşa kullanıcı atama yaptıysa o slot atamayla, atama yoksa tuşun varsayılan değeriyle yazılır;
        // düzende olmayan hiçbir slot (döner düğme slotları dahil) ve döner düğme çizim tuşu cihazdan okunan değerden
        // farklı yazılmaz.
        public static byte[] BuildBase(Profile p, byte[] deviceBase)
        {
            if (deviceBase == null || deviceBase.Length != 528) throw new ArgumentException("Cihazdan okunan tuş haritası 528 bayt olmalı.");
            KeyboardLayout.Load();
            byte[] b = (byte[])deviceBase.Clone();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                KeyAssign a = p.FindKey(k.Index, false);
                if (a == null && k.Code == "RotaryKnob") continue; // gerçek bir tuş değil: cihaz değeri korunur
                int c1 = k.Code == "KeyFn" ? 1 : 0;
                int hid = k.HidCode;
                int c3 = 0, c4 = 0;
                if (a != null) { c1 = a.C1; hid = a.Hid; c3 = a.C3; c4 = a.C4; }
                int o = k.Index * 4;
                b[o] = (byte)c1; b[o + 1] = (byte)hid; b[o + 2] = (byte)c3; b[o + 3] = (byte)c4;
            }
            return b;
        }

        // Yazmadan önce uyarı için: profilde ataması olmayan ama cihazda varsayılandan farklı değer taşıyan tuşlar.
        // (Bu tuşlar yazma sonunda varsayılana döner.)
        public static List<string> FindDeviceDifferences(Profile p, byte[] deviceBase)
        {
            List<string> r = new List<string>();
            KeyboardLayout.Load();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                if (k.Code == "RotaryKnob" || p.FindKey(k.Index, false) != null) continue;
                byte[] def = DefaultEntry(k);
                int o = k.Index * 4;
                if (deviceBase[o] != def[0] || deviceBase[o + 1] != def[1] || deviceBase[o + 2] != 0 || deviceBase[o + 3] != 0)
                    r.Add(k.Display + ": cihazda " + deviceBase[o] + "/" + deviceBase[o + 1].ToString("X2") + "/" + deviceBase[o + 2] + "/" + deviceBase[o + 3] +
                          " var, profilde atama yok; yazma sonunda varsayılana dönecek");
            }
            return r;
        }

        public static byte[] BuildFn(Profile p)
        {
            byte[] b = new byte[528];
            foreach (KeyAssign a in p.FnKeys)
            {
                if (a.Index < 0 || a.Index >= 132) continue;
                int o = a.Index * 4;
                b[o] = (byte)a.C1; b[o + 1] = (byte)a.Hid; b[o + 2] = (byte)a.C3; b[o + 3] = (byte)a.C4;
            }
            return b;
        }

        public static byte[] BuildDeadband(Profile p)
        {
            byte[] b = new byte[264];
            foreach (TriggerOverride t in p.Triggers)
            {
                if (t.Index < 0 || t.Index >= 132) continue;
                b[t.Index * 2] = (byte)t.DeadTop;
                b[t.Index * 2 + 1] = (byte)t.DeadBottom;
            }
            return b;
        }

        public static byte[] DefaultEntry(KeyDef k)
        {
            byte[] b = new byte[4];
            b[0] = (byte)(k.Code == "KeyFn" ? 1 : 0);
            b[1] = (byte)k.HidCode;
            return b;
        }

        public static void DecodeBase(Profile p, byte[] raw)
        {
            p.Keys.Clear();
            KeyboardLayout.Load();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                int o = k.Index * 4;
                if (o + 3 >= raw.Length) continue;
                byte[] def = DefaultEntry(k);
                if (raw[o] == def[0] && raw[o + 1] == def[1] && raw[o + 2] == 0 && raw[o + 3] == 0) continue;
                AddDecoded(p, k.Index, raw[o], raw[o + 1], raw[o + 2], raw[o + 3], false);
            }
        }

        public static void DecodeFn(Profile p, byte[] raw)
        {
            p.FnKeys.Clear();
            KeyboardLayout.Load();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                int o = k.Index * 4;
                if (o + 3 >= raw.Length) continue;
                if (raw[o] == 0 && raw[o + 1] == 0 && raw[o + 2] == 0 && raw[o + 3] == 0) continue;
                AddDecoded(p, k.Index, raw[o], raw[o + 1], raw[o + 2], raw[o + 3], true);
            }
        }

        private static void AddDecoded(Profile p, int index, int c1, int hid, int c3, int c4, bool fn)
        {
            ActionDef a = Actions.Find(c1, hid, c3, c4);
            if (a != null && a.Code != "__default")
            {
                p.SetKeyRaw(index, a.Code, a.Label, c1, hid, c3, c4, fn);
                return;
            }
            string label;
            if (c1 == 16) label = "Makro " + (c3 + 1);
            else if (c1 == 23) label = "Gelişmiş tuş";
            else if (c1 == 0 && hid == 0 && c3 == 0 && c4 == 0) label = "Devre dışı";
            else label = string.Format("Özel ({0}/{1:X2}/{2}/{3})", c1, hid, c3, c4);
            p.SetKeyRaw(index, "__custom", label, c1, hid, c3, c4, fn);
        }
    }

    // Cihazın profil slotu sınırı TEK yerde: klavye yalnızca 4 profil (slot 0-3) saklar. Slot 4 ve üstü cihazda başa sarar
    // (slot 4 → 0 olup Günlük'ü ezer; gerçek klavyede doğrulandı). Program hiçbir yerde bu sınırın ötesinde SetProfile,
    // WriteLight ya da yazma göndermez.
    public static class DeviceLimits
    {
        public const int MaxSlots = 4;

        public static bool SlotAllowed(int slot) { return slot >= 0 && slot < MaxSlots; }

        public static string Message(int slot)
        {
            return "Cihaz " + MaxSlots + " profil destekliyor (slot 0–" + (MaxSlots - 1) + "); slot " + slot + " için klavyeye komut gönderilmedi.";
        }
    }

    public static class Store
    {
        private const int CurrentSchemaVersion = 7;

        private const string DataDirName = "W75Ayarlayici";

        // Test için: W75_VERI_KLASORU ayarlıysa veri (profiles.json, socd.json, backups\ ...) o klasörde tutulur.
        // Ayarlı değilse her zaman %APPDATA%\W75Ayarlayici kullanılır.
        public static string Dir
        {
            get
            {
                string over = Environment.GetEnvironmentVariable("W75_VERI_KLASORU");
                if (!string.IsNullOrEmpty(over)) return EnsureDirectory(over);
                return EnsureDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DataDirName));
            }
        }

        // Klasör yoksa (ilk açılış) boş olarak oluşturur; varsa içeriğine dokunmaz.
        public static string EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public static string FilePath { get { return Path.Combine(Dir, "profiles.json"); } }
        public static string BackupDir
        {
            get
            {
                string d = Path.Combine(Dir, "backups");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static AppData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    AppData data = ser.Deserialize<AppData>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (data != null)
                    {
                        Normalize(data);
                        Migrate(data);
                        return data;
                    }
                }
            }
            catch
            {
                try
                {
                    string bad = FilePath + ".bozuk-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Copy(FilePath, bad, true);
                }
                catch { }
            }
            AppData fresh = new AppData();
            fresh.Profiles = CreateDefaults();
            fresh.SelectedSlot = 0;
            fresh.SchemaVersion = CurrentSchemaVersion;
            Save(fresh);
            return fresh;
        }

        private static void Migrate(AppData data)
        {
            if (data.SchemaVersion >= CurrentSchemaVersion) return;

            KeyboardLayout.Load();

            // v2 ve v3 eski düzende çalışır (o zaman Valorant slot 3'teydi); v5 sonra ada göre slot 4'e taşır.
            // v3: "Valorant" (slot 3) profili hazır tanıma geri döndürülür. Önce mevcut dosyanın
            // kopyası profiles.json.oncesi-v3-* olarak alınır.
            if (data.SchemaVersion < 3 && data.Profiles.Count > 3 && data.Profiles[3].Name == ValorantPreset.ProfileName)
            {
                try { File.Copy(FilePath, FilePath + ".oncesi-v3-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
                data.Profiles[3] = ValorantPreset.Create(data.Profiles[0]);
            }

            // v2: Dokunulmamış "Boş 4" (slot 3) profilini Valorant hazır profiline çevir.
            // Işık ve anketleme hızı kullanıcının mevcut Günlük profilinden alınır.
            if (data.SchemaVersion < 2 && data.Profiles.Count > 3 && ValorantPreset.IsUntouchedPlaceholder(data.Profiles[3]))
                data.Profiles[3] = ValorantPreset.Create(data.Profiles[0]);

            // v1: Eski hazır profillere otomatik eklenen Caps Lock -> Sol Ctrl
            // atamasını kaldır. Diğer tuş atamaları korunur.
            int caps = data.SchemaVersion < 1 ? KeyboardLayout.IndexOf("CapsLock") : -1;
            if (caps >= 0)
            {
                for (int i = 0; i < data.Profiles.Count && i <= 2; i++)
                {
                    Profile p = data.Profiles[i];
                    KeyAssign a = p.FindKey(caps, false);
                    if (a == null) continue;
                    if (a.Code == "ControlLeft" || (a.C1 == 0 && a.Hid == 0xE0 && a.C3 == 0 && a.C4 == 0))
                        p.Keys.Remove(a);
                }
            }

            // v6: cihaz 4 profil saklar; 5 profilli dosya 4'e düzenlenir (MigrateV6). Önce profiles.json'un tam kopyası
            // alınır; yedek alınamazsa geçiş yapılmaz (sonraki açılışta yeniden denenir). Bir kez çalışır (sürüm numarası).
            if (data.SchemaVersion < 6)
            {
                bool recommended = data.SchemaVersion < 5; // eski düzenden gelen veriye önerilen ayarlar (v5 davranışı)
                if (!MigrateV6(data, recommended)) return;
            }

            // v7: polling profile özel olmaktan çıktı, tek genel ayar oldu (genel.json). Önce profiles.json yedeklenir; yedek
            // alınamazsa geçiş yapılmaz (sonraki açılışta yeniden denenir). Bir kez çalışır (sürüm numarası).
            if (data.SchemaVersion < 7)
            {
                if (!MigrateV7(data)) return;
            }

            data.SchemaVersion = CurrentSchemaVersion;
            try { Save(data); } catch { }
        }

        // v7 göçü sonucu (bilgi kutusu için) ve göç anındaki profil başına eski polling değerleri (yazım durumu özetini
        // yeni biçime taşımak için). Geçiş yapılmadıysa null.
        public static PollMigrationInfo LastPollMigration;
        public static int[] LegacyPollBySlot;

        private static bool MigrateV7(AppData data)
        {
            bool any = false;
            foreach (Profile p in data.Profiles) if (p != null && p.LegacyPollRate != -1) any = true;
            LegacyPollBySlot = new int[data.Profiles.Count];
            for (int i = 0; i < data.Profiles.Count; i++) LegacyPollBySlot[i] = data.Profiles[i] != null ? data.Profiles[i].LegacyPollRate : -1;
            if (!any) return true; // dosyada polling alanı yoktu (yeni sürümle kaydedilmiş): yapılacak bir şey yok

            string bak = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    bak = Path.Combine(BackupDir, "profiles-polling-global-oncesi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Copy(FilePath, bak, false);
                }
            }
            catch { LegacyPollBySlot = null; return false; }

            bool differed; string detail, selName;
            int value = GenelStore.ChooseFromProfiles(data.Profiles, data.SelectedSlot, out differed, out detail, out selName);
            if (value < 0) return true;
            PollMigrationInfo info = new PollMigrationInfo();
            info.Value = value; info.Differed = differed; info.Detail = detail; info.SelectedName = selName; info.BackupPath = bak;
            try
            {
                if (GenelStore.Exists()) info.KeptExisting = true; // genel ayar zaten var: eski profil değerleri onu ezmez
                else { GenelData g = new GenelData(); g.PollRate = value; GenelStore.Save(g); }
            }
            catch { LegacyPollBySlot = null; return false; }
            LastPollMigration = info;
            return true;
        }

        // v6 geçişi sonrası bilgi penceresi için: eski 5 profilli listenin yedek yolu (geçiş yapılmadıysa null).
        public static string LastMigrationBackup;

        // 5 (ya da daha fazla) profilli listeyi cihazın 4 slotuna düzenler. Eski 0 ve 1 aynen; eski 2 (Mass Effect) slot 2
        // olur, içeriği korunur, yalnızca adı hâlâ varsayılan "Mass Effect LE" ise "RPG" yapılır; Valorant (eski slot 4,
        // ya da adı Valorant olan profil) slot 3 olur, içeriği aynen. Diğer profiller (eski CRPG) listeden çıkar, yedek
        // dosyada kalır (İçe Aktar ile bakılabilir). Yazım durumu kaydı temizlenir (slotlar kaydı: hepsi "Cihaza yazılmadı").
        private static bool MigrateV6(AppData data, bool applyRecommended)
        {
            List<Profile> old = data.Profiles ?? new List<Profile>();
            string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string bak = null;
            if (old.Count > DeviceLimits.MaxSlots)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        bak = Path.Combine(BackupDir, "profiles-5-slot-eski-" + ts + ".json");
                        File.Copy(FilePath, bak, false);
                    }
                }
                catch { return false; }
            }

            int vi = old.FindIndex(delegate(Profile x) { return x != null && x.Name == ValorantPreset.ProfileName; });
            if (vi <= 2) vi = old.Count > DeviceLimits.MaxSlots ? 4 : (old.Count > 3 ? 3 : -1);
            List<Profile> nw = new List<Profile>();
            for (int i = 0; i < 3; i++) nw.Add(i < old.Count && old[i] != null ? old[i] : CreateProfile(i));
            nw.Add(vi >= 0 && vi < old.Count && old[vi] != null ? old[vi] : CreateProfile(3));
            if (nw[2].Name == OnerilenAyarlar.MassEffect) nw[2].Name = OnerilenAyarlar.Rpg;

            Profile selected = data.SelectedSlot >= 0 && data.SelectedSlot < old.Count ? old[data.SelectedSlot] : null;
            data.Profiles = nw;
            data.SelectedSlot = Math.Max(0, nw.IndexOf(selected));
            Normalize(data);
            if (applyRecommended) foreach (Profile p in data.Profiles) OnerilenAyarlar.Apply(p);

            // Yazım durumu kaydı: eski slot düzenine aitti; temizlenir (kopyası yedek klasöründe kalır).
            try
            {
                string ys = Path.Combine(Dir, "yazim-durumu.json");
                if (File.Exists(ys))
                {
                    File.Copy(ys, Path.Combine(BackupDir, "yazim-durumu-5-slot-eski-" + ts + ".json"), false);
                    File.WriteAllText(ys, "{\"Slots\":{}}", new UTF8Encoding(false));
                }
            }
            catch { }

            LastMigrationBackup = bak;
            return true;
        }

        // Profiller kaydedilince tetiklenir (arayüz profil listesindeki durum yazılarını tazeler).
        public static event Action Saved;

        // Kaydetme başarısız olursa (salt okunur klasör, başka program dosyayı kullanıyor) program çökmez: hata bir kez kullanıcıya
        // bildirilir (MainForm). SaveError, ilk başarısız kaydetmenin hata metnidir; başarılı kaydetme onu temizler.
        public static string SaveError;
        public static event Action<string> SaveFailed;

        public static void Save(AppData data)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string json = ser.Serialize(data);
            try
            {
                File.WriteAllText(FilePath, json, new UTF8Encoding(false));
                SaveError = null;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                bool first = SaveError == null;
                SaveError = ex.Message;
                Action<string> f = SaveFailed;
                if (first && f != null) { try { f(ex.Message); } catch { } }
                return;
            }
            Action h = Saved;
            if (h != null) { try { h(); } catch { } }
        }

        public static void Normalize(AppData data)
        {
            if (data.Profiles == null) data.Profiles = new List<Profile>();
            while (data.Profiles.Count < DeviceLimits.MaxSlots)
            {
                int slot = data.Profiles.Count;
                data.Profiles.Add(CreateProfile(slot));
            }
            for (int i = 0; i < data.Profiles.Count; i++)
            {
                Profile p = data.Profiles[i];
                p.Slot = i;
                if (p.Keys == null) p.Keys = new List<KeyAssign>();
                if (p.FnKeys == null) p.FnKeys = new List<KeyAssign>();
                if (p.Triggers == null) p.Triggers = new List<TriggerOverride>();
                if (p.Light == null) p.Light = new LightSetting();
                if (p.DefaultTravel < 0) p.DefaultTravel = Keymap.DefaultTravel;
                if (p.DefaultI1 <= 0) p.DefaultI1 = 1;
                if (p.DefaultI2 <= 0) p.DefaultI2 = 1;
            }
            if (data.SelectedSlot < 0 || data.SelectedSlot >= data.Profiles.Count || data.SelectedSlot >= DeviceLimits.MaxSlots) data.SelectedSlot = 0;
        }

        public static Profile CreateProfile(int slot)
        {
            Profile p = new Profile();
            p.Slot = slot;
            switch (slot)
            {
                case 0: return DailyPreset();
                case 1: return CyberpunkPreset();
                case 2: return RpgPreset();
                case 3: return ValorantPreset.Create(DailyPreset());
                default:
                    p.Name = "Boş " + (slot + 1);
                    p.Light.Mode = 0; p.Light.Brightness = OnerilenAyarlar.RecommendedBrightness; p.Light.Speed = 2;
                    p.Light.R = 255; p.Light.G = 217; p.Light.B = 160;
                    return p;
            }
        }

        public static List<Profile> CreateDefaults()
        {
            List<Profile> list = new List<Profile>();
            for (int i = 0; i < DeviceLimits.MaxSlots; i++) list.Add(CreateProfile(i));
            return list;
        }

        // Hazır profillerin tetikleyici ayarları ve ışık modu/ön rengi OnerilenAyarlar'dan gelir;
        // burada yalnızca ad, parlaklık, hız ve arka renk tanımlıdır.
        private static Profile DailyPreset()
        {
            Profile p = new Profile();
            p.Slot = 0;
            p.Name = OnerilenAyarlar.Gunluk;
            p.Light.Brightness = OnerilenAyarlar.RecommendedBrightness; p.Light.Speed = 2;
            OnerilenAyarlar.Apply(p);
            return p;
        }

        private static Profile CyberpunkPreset()
        {
            Profile p = new Profile();
            p.Slot = 1;
            p.Name = OnerilenAyarlar.Cyberpunk;
            p.Light.Brightness = OnerilenAyarlar.RecommendedBrightness; p.Light.Speed = 3;
            p.Light.BR = 0; p.Light.BG = 229; p.Light.BB = 255;
            OnerilenAyarlar.Apply(p);
            return p;
        }

        private static Profile RpgPreset()
        {
            Profile p = new Profile();
            p.Slot = 2;
            p.Name = OnerilenAyarlar.Rpg;
            p.Light.Brightness = OnerilenAyarlar.RecommendedBrightness; p.Light.Speed = 2;
            p.Light.BR = 255; p.Light.BG = 255; p.Light.BB = 255;
            OnerilenAyarlar.Apply(p);
            return p;
        }
    }

    public class DeviceBackup
    {
        public string Created;
        public string Product;
        public string Version;
        public int Slot;
        public int MaxTravel;
        public double Unit;
        public string KeymapBase;   // base64
        public string KeymapFn;     // base64
        public string Deadband;     // base64
        public LightSetting Light;
        public List<TriggerOverride> Triggers = new List<TriggerOverride>();
        public int PollRate;
    }
}


