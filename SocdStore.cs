using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace W75Ayarlayici
{
    // SOCD kuralları cihaz geneli olduğu için profillerden ayrı, %APPDATA%\W75Ayarlayici\socd.json içinde tutulur.
    public class SocdData
    {
        public List<SocdRule> Rules = new List<SocdRule>();
    }

    public static class SocdStore
    {
        public static string FilePath { get { return Path.Combine(Store.Dir, "socd.json"); } }

        public static List<SocdRule> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    SocdData d = ser.Deserialize<SocdData>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (d != null && d.Rules != null) return d.Rules;
                }
            }
            catch { }
            return new List<SocdRule>();
        }

        public static void Save(List<SocdRule> rules)
        {
            SocdData d = new SocdData();
            d.Rules = rules;
            JavaScriptSerializer ser = new JavaScriptSerializer();
            File.WriteAllText(FilePath, ser.Serialize(d), new UTF8Encoding(false));
        }

        // Cihaza yazmadan önce kuralları denetler. Hata varsa Türkçe mesaj döner, yoksa null.
        public static string Validate(List<SocdRule> rules)
        {
            if (rules.Count > 20) return "En fazla 20 SOCD kuralı olabilir (şu an " + rules.Count + ").";
            for (int i = 0; i < rules.Count; i++)
            {
                SocdRule r = rules[i];
                int no = i + 1;
                if (r.Key1 == 0 || r.Key2 == 0) return no + ". kuralda iki tuş da seçilmeli.";
                if (r.Key1 == r.Key2) return no + ". kuralda Tuş 1 ile Tuş 2 aynı olamaz.";
                if (r.Model < 0 || r.Model > 3) return no + ". kuralda model geçersiz.";
                for (int j = 0; j < i; j++)
                {
                    SocdRule o = rules[j];
                    if (o.Key1 == r.Key1 || o.Key1 == r.Key2 || o.Key2 == r.Key1 || o.Key2 == r.Key2)
                        return "Aynı tuş iki farklı kuralda geçemez (" + (j + 1) + ". ve " + no + ". kurallar).";
                }
            }
            return null;
        }
    }
}
