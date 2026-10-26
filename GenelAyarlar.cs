using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace W75Ayarlayici
{
    // Klavyenin geneli için ayarlar (profile bağlı değil): şimdilik yalnızca polling hızı. %APPDATA%\W75Ayarlayici\genel.json
    // (diger.json ve socd.json ayrı kalır). Polling cihazda profile özel değil, cihaz geneli (kullanıcı web sürücüsünde doğruladı).
    public class GenelData
    {
        public int PollRate = 1;   // kHz: 1, 2, 4 ya da 8 (Protocol.SetPollRate / arayüz düğmeleriyle aynı değerler)
        public string Updated;
    }

    public static class GenelStore
    {
        public static readonly int[] PollRates = new int[] { 1, 2, 4, 8 };

        public static string FilePath { get { return Path.Combine(Store.Dir, "genel.json"); } }

        public static bool ValidRate(int r) { return r == 1 || r == 2 || r == 4 || r == 8; }

        public static bool Exists() { return File.Exists(FilePath); }

        // Dosya yoksa ya da bozuksa varsayılan (1 kHz) döner.
        public static GenelData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    GenelData d = new JavaScriptSerializer().Deserialize<GenelData>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (d != null)
                    {
                        if (!ValidRate(d.PollRate)) d.PollRate = 1;
                        return d;
                    }
                }
            }
            catch { }
            return new GenelData();
        }

        public static void Save(GenelData d)
        {
            if (!ValidRate(d.PollRate)) d.PollRate = 1;
            d.Updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
        }

        // Eski profillerdeki polling değerlerinden TEK değer seçer: hepsi aynıysa o; farklıysa seçili (en son kullanılan)
        // profilinki. Dosyada hiç geçerli değer yoksa -1. differed: değerler birbirinden farklıydı. detail: "Günlük 1 kHz, Valorant 8 kHz".
        public static int ChooseFromProfiles(IList<Profile> profiles, int selectedSlot, out bool differed, out string detail, out string selectedName)
        {
            differed = false; detail = ""; selectedName = "";
            List<int> distinct = new List<int>();
            StringBuilder sb = new StringBuilder();
            int selValue = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                Profile p = profiles[i];
                if (p == null || !ValidRate(p.LegacyPollRate)) continue;
                if (!distinct.Contains(p.LegacyPollRate)) distinct.Add(p.LegacyPollRate);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(string.IsNullOrEmpty(p.Name) ? "Profil " + (i + 1) : p.Name).Append(" ").Append(p.LegacyPollRate).Append(" kHz");
                if (i == selectedSlot) { selValue = p.LegacyPollRate; selectedName = p.Name; }
            }
            if (distinct.Count == 0) return -1;
            detail = sb.ToString();
            if (distinct.Count == 1) return distinct[0];
            differed = true;
            if (selValue >= 0) return selValue;
            selectedName = profiles[0] != null ? profiles[0].Name : "";
            return distinct[0];
        }
    }

    // Göçün (şema 7) sonucu: arayüz bilgi kutusu için.
    public class PollMigrationInfo
    {
        public int Value;           // global değer olarak seçilen polling (kHz)
        public bool Differed;       // eski profillerde farklı değerler vardı
        public string Detail;       // hangi profilde ne vardı
        public string SelectedName; // değeri veren (en son seçili) profil
        public string BackupPath;   // göç öncesi profiles.json kopyası
        public bool KeptExisting;   // genel.json zaten vardı, dokunulmadı
    }
}
