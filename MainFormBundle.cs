using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Ayar paketi: profiles.json, socd.json ve diger.json tek zip içinde. Dışa/içe aktarma yalnızca
    // yerel dosyalarla çalışır; klavyeye hiçbir şey göndermez.
    public static class SettingsBundle
    {
        public static readonly string[] Names = new string[] { "profiles.json", "socd.json", "diger.json" };
        // İsteğe bağlı: polling hızı (tüm klavye için tek ayar). Eski paketlerde yoktur; varsa okunur ve uygulanır.
        public const string GenelName = "genel.json";

        public static string PathOf(string name)
        {
            return Path.Combine(Store.Dir, name);
        }

        public static void Export(string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            try
            {
                using (ZipArchive z = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (string n in Names) z.CreateEntryFromFile(PathOf(n), n);
                    if (File.Exists(PathOf(GenelName))) z.CreateEntryFromFile(PathOf(GenelName), GenelName);
                }
            }
            catch
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                throw;
            }
        }

        // Zip'i okur ve içeriğini doğrular. Mevcut dosyalara dokunmaz. Hata varsa false + Türkçe mesaj.
        public static bool TryRead(string zipPath, out Dictionary<string, byte[]> files, out string error)
        {
            files = new Dictionary<string, byte[]>();
            error = null;
            try
            {
                using (ZipArchive z = ZipFile.OpenRead(zipPath))
                {
                    List<string> missing = new List<string>();
                    foreach (string n in Names)
                    {
                        ZipArchiveEntry found = null;
                        foreach (ZipArchiveEntry e in z.Entries)
                        {
                            if (string.Equals(e.FullName.Replace('\\', '/'), n, StringComparison.OrdinalIgnoreCase)) { found = e; break; }
                        }
                        if (found == null) { missing.Add(n); continue; }
                        if (found.Length == 0 || found.Length > 20 * 1024 * 1024)
                        {
                            error = n + " dosyası boş ya da çok büyük.";
                            return false;
                        }
                        using (Stream s = found.Open())
                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            files[n] = ms.ToArray();
                        }
                    }
                    foreach (ZipArchiveEntry e in z.Entries) // isteğe bağlı genel.json
                    {
                        if (!string.Equals(e.FullName.Replace('\\', '/'), GenelName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (e.Length == 0 || e.Length > 1024 * 1024) { error = GenelName + " dosyası boş ya da çok büyük."; return false; }
                        using (Stream s = e.Open())
                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            files[GenelName] = ms.ToArray();
                        }
                        break;
                    }
                    if (missing.Count > 0)
                    {
                        error = "Pakette şu dosyalar eksik: " + string.Join(", ", missing.ToArray()) + ".";
                        return false;
                    }
                }
            }
            catch (InvalidDataException)
            {
                error = "Bu dosya bozuk ya da geçerli bir zip değil.";
                return false;
            }
            catch (Exception ex)
            {
                error = "Zip okunamadı: " + ex.Message;
                return false;
            }

            JavaScriptSerializer ser = new JavaScriptSerializer();
            try
            {
                AppData a = ser.Deserialize<AppData>(Decode(files["profiles.json"]));
                if (a == null || a.Profiles == null || a.Profiles.Count == 0) { error = "profiles.json içinde profil yok."; return false; }
            }
            catch { error = "profiles.json bozuk."; return false; }
            try
            {
                SocdData s = ser.Deserialize<SocdData>(Decode(files["socd.json"]));
                if (s == null || s.Rules == null) { error = "socd.json bozuk."; return false; }
            }
            catch { error = "socd.json bozuk."; return false; }
            try
            {
                DigerData d = ser.Deserialize<DigerData>(Decode(files["diger.json"]));
                if (d == null) { error = "diger.json bozuk."; return false; }
            }
            catch { error = "diger.json bozuk."; return false; }
            if (files.ContainsKey(GenelName))
            {
                try
                {
                    GenelData g = ser.Deserialize<GenelData>(Decode(files[GenelName]));
                    if (g == null || !GenelStore.ValidRate(g.PollRate)) { error = "genel.json bozuk (polling hızı geçersiz)."; return false; }
                }
                catch { error = "genel.json bozuk."; return false; }
            }
            return true;
        }

        private static string Decode(byte[] b)
        {
            string t = Encoding.UTF8.GetString(b);
            return t.Length > 0 && t[0] == '\uFEFF' ? t.Substring(1) : t;
        }

        // Mevcut dosyaların kopyasını yedek klasörüne alır; klasör yolunu döndürür.
        public static string BackupExisting()
        {
            string dir = Path.Combine(Store.BackupDir, "ayarlar-yedek-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            foreach (string n in Names)
                if (File.Exists(PathOf(n))) File.Copy(PathOf(n), Path.Combine(dir, n), true);
            if (File.Exists(PathOf(GenelName))) File.Copy(PathOf(GenelName), Path.Combine(dir, GenelName), true);
            return dir;
        }

        // Önce hepsi geçici dosyaya yazılır, sonra yerine konur (yazma aşamasında hata olursa mevcut dosyalar değişmez).
        public static void Apply(Dictionary<string, byte[]> files)
        {
            List<string> all = new List<string>(Names);
            if (files.ContainsKey(GenelName)) all.Add(GenelName);
            List<string> tmps = new List<string>();
            try
            {
                foreach (string n in all)
                {
                    string tmp = PathOf(n) + ".yeni";
                    File.WriteAllBytes(tmp, files[n]);
                    tmps.Add(tmp);
                }
            }
            catch
            {
                foreach (string t in tmps) { try { File.Delete(t); } catch { } }
                throw;
            }
            for (int i = 0; i < all.Count; i++)
            {
                File.Copy(tmps[i], PathOf(all[i]), true);
                try { File.Delete(tmps[i]); } catch { }
            }
        }
    }

    public partial class MainForm
    {
        private SocdTab _socdTab;
        private DigerTab _digerTab;

        private void ExportBundle()
        {
            try
            {
                Store.Save(_data);
                // Hiç oluşmamış dosyalar için güncel (boş olabilir) içerik yazılır ki paket eksiksiz olsun.
                if (!File.Exists(SettingsBundle.PathOf("socd.json"))) SocdStore.Save(SocdStore.Load());
                if (!File.Exists(SettingsBundle.PathOf("diger.json"))) DigerStore.Save(DigerStore.Load());
                if (!File.Exists(SettingsBundle.PathOf(SettingsBundle.GenelName))) GenelStore.Save(GenelStore.Load()); // polling (tüm klavye) pakete girer

                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Title = "Tüm ayarları paketle";
                    d.Filter = "W75 ayar paketi (*.zip)|*.zip";
                    d.FileName = "w75-ayarlar-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    SettingsBundle.Export(d.FileName);
                    Log("Ayar paketi oluşturuldu: " + d.FileName);
                    MessageBox.Show(this, "Ayarlar paketlendi (profiller, SOCD, Diğer):\n" + d.FileName, "Dışa aktar",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log("Ayar paketi oluşturulamadı: " + ex.Message);
                MessageBox.Show(this, "Paket oluşturulamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Yalnızca test kipinde (W75_CIHAZSIZ=1 veya W75_SIM_YAZ=1 ile birlikte): W75_PAKET_DOSYA ayarlıysa dosya penceresi açılmaz,
        // bu yol kullanılır (otomatik testler içindir). Normal kullanımda hiçbir etkisi yoktur.
        private static string TestBundleFile()
        {
            if (!TestMode && !SimWrite) return null;
            string p = Environment.GetEnvironmentVariable("W75_PAKET_DOSYA");
            return string.IsNullOrEmpty(p) ? null : p;
        }

        private void ImportBundle()
        {
            string path = TestBundleFile();
            if (path == null)
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Ayar paketini seç";
                    d.Filter = "W75 ayar paketi (*.zip)|*.zip";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    path = d.FileName;
                }
            }
            Dictionary<string, byte[]> files;
            string error;
            if (!SettingsBundle.TryRead(path, out files, out error))
            {
                Log("Ayar paketi reddedildi: " + error);
                MessageBox.Show(this, error + "\n\nMevcut ayarlarınıza dokunulmadı.", "Paket yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show(this,
                "Paketteki profiller, SOCD ve Diğer ayarları mevcut ayarlarınızın yerine konacak.\n\n" +
                "Önce mevcut dosyalarınızın yedeği alınır. Klavyeye hiçbir şey gönderilmez; yüklemeden sonra " +
                "klavyeye yazmak için “Cihaza Yaz”ı kendiniz kullanırsınız.\n\nDevam edilsin mi?",
                "Paketten yükle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            string backupDir = null;
            try
            {
                Store.Save(_data); // yedek, ekrandaki güncel durumu yansıtsın
                backupDir = SettingsBundle.BackupExisting();
                Log("Mevcut ayarlar yedeklendi: " + backupDir);
                SettingsBundle.Apply(files);
            }
            catch (Exception ex)
            {
                Log("Ayar paketi yüklenemedi: " + ex.Message);
                MessageBox.Show(this, "Paket yüklenemedi: " + ex.Message +
                    (backupDir != null ? "\n\nMevcut ayarlarınızın yedeği:\n" + backupDir : ""),
                    "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Programı yeniden yükle: bellekteki veriler dosyalardan tazelenir (kapanışta eski veri geri yazılmasın).
            _data = Store.Load();
            PollMigrationFollowUp();
            string pollMsg = ApplyBundlePolling(files); // pakette (eski) polling varsa global ayara uygulanır
            RefreshProfileList();
            LoadProfileUi();
            if (_socdTab != null) _socdTab.ReloadFromFile();
            if (_digerTab != null) _digerTab.ReloadFromFile();
            Log("Ayar paketi yüklendi: " + path + " (klavyeye bir şey gönderilmedi)");
            MessageBox.Show(this, "Ayarlar yüklendi ve ekran yenilendi.\n\nKlavyeye yazmak için profili seçip “Cihaza Yaz”ı kullanın. " +
                "Önceki ayarlarınızın yedeği:\n" + backupDir + (pollMsg != null ? "\n\n" + pollMsg : ""), "Paketten yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowReorderNote(); // eski bir paket yüklendiyse v5 geçişi profilleri yeniden sıralamış olabilir
        }
    }
}
