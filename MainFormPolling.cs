using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Polling hızı (anketleme): klavyede profile özel değil, cihaz geneli; bu yüzden profilden çıkarıldı, tek GLOBAL ayar
    // (genel.json). "Cihaza Yaz" polling YAZMAZ; polling yalnızca bu bölümdeki "Klavyeye yaz" ile kendi akışında yazılır:
    // yaz, Settle bekle, ReadPollRate ile geri oku. Not: ReadPollRate cevap gelmezse SAHTE 1 döndürür; bu yüzden hedef 1 iken
    // geri okuma "doğrulandı" diyemez. Uyuşmazlıkta program kendiliğinden yeniden yazmaz.
    public partial class MainForm
    {
        private FlatCombo _pollCombo;
        private Label _pollStatus;
        private bool _pollLoading;
        private int _globalPoll = 1;

        // Test kipi (W75_CIHAZSIZ=1 ya da W75_SIM_YAZ=1) ile birlikte: W75_SIM_POLL_OKU=<kHz> geri okunan değeri simüle eder.
        private static int SimPollReadBack(int target)
        {
            string s = Environment.GetEnvironmentVariable("W75_SIM_POLL_OKU");
            int v;
            if ((TestMode || SimWrite) && !string.IsNullOrEmpty(s) && int.TryParse(s, out v) && GenelStore.ValidRate(v)) return v;
            return target;
        }

        // ------------------------------------------------------------ yazım durumu / göç sonrası

        // Eski profil özeti (polling dahil) → yeni özet: v7 göçünde polling profilden çıktığı için özet değişir; eskiden
        // "Cihazda güncel" olan profiller öyle kalsın (klavyedeki içerik değişmedi). Yeni bir yazma yapılmaz.
        private void PollMigrationFollowUp()
        {
            int[] legacy = Store.LegacyPollBySlot;
            if (legacy == null) return;
            Store.LegacyPollBySlot = null;
            try
            {
                WriteStateData d = LoadWriteState();
                bool changed = false;
                for (int i = 0; i < _data.Profiles.Count && i < legacy.Length; i++)
                {
                    if (!GenelStore.ValidRate(legacy[i])) continue;
                    string saved;
                    if (!d.Slots.TryGetValue(i.ToString(), out saved)) continue;
                    if (saved == OldProfileHash(_data.Profiles[i], legacy[i])) { d.Slots[i.ToString()] = ProfileHash(_data.Profiles[i]); changed = true; }
                }
                if (changed) File.WriteAllText(WriteStatePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }

        // Eski sürümdeki ProfileHash: profil JSON'unda "PollRate" alanı DefaultTravel'dan hemen önce yer alıyordu.
        private static string OldProfileHash(Profile p, int poll)
        {
            Profile c = p.Clone();
            c.Name = "";
            string json = new JavaScriptSerializer().Serialize(c);
            int at = json.IndexOf("\"DefaultTravel\":", StringComparison.Ordinal);
            if (at >= 0) json = json.Insert(at, "\"PollRate\":" + poll + ",");
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Göç bilgisi: eski profillerde farklı polling değerleri varsa kullanıcıya söylenir; hepsi aynıysa yalnızca günlüğe yazılır.
        private void ShowPollMigrationNote()
        {
            PollMigrationInfo m = Store.LastPollMigration;
            if (m == null) return;
            Store.LastPollMigration = null;
            int now = GenelStore.Load().PollRate;
            if (m.KeptExisting)
            {
                Log("Polling hızı artık tüm klavye için tek ayar (genel.json); kayıtlı genel ayar korundu: " + now + " kHz. Eski profil dosyası yedeklendi: " + m.BackupPath);
                return;
            }
            Log("Polling hızı profilden çıkarıldı, tüm klavye için tek ayar oldu: " + now + " kHz (genel.json). Yedek: " + m.BackupPath);
            if (!m.Differed) return;
            MessageBox.Show(this,
                "Polling hızı artık profile özel değil, tüm klavye için tek ayar.\n\n" +
                "Eski profillerde farklı değerler vardı (" + m.Detail + "). En son seçili profilin değeri alındı: " + m.Value + " kHz" +
                (string.IsNullOrEmpty(m.SelectedName) ? "" : " (" + m.SelectedName + ")") + ".\n\n" +
                "Değiştirmek için Aktivasyon > Polling Rate. Klavyeye henüz bir şey yazılmadı.\n\nEski profil dosyası yedeklendi:\n" + m.BackupPath,
                "Polling hızı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Paketten yükle: pakette genel.json (tüm klavye için polling) varsa o, yoksa (eski paket) profillerdeki polling değerleri
        // global ayara uygulanır. Dönüş: kutuya eklenecek metin (yoksa null).
        private string ApplyBundlePolling(Dictionary<string, byte[]> files)
        {
            Store.LastPollMigration = null; // bu yükleme kendi mesajını gösterir
            if (files.ContainsKey(SettingsBundle.GenelName))
            {
                // genel.json SettingsBundle.Apply ile zaten yazıldı (TryRead geçerli polling hızını doğruladı).
                _globalPoll = GenelStore.Load().PollRate;
                FillPollUi();
                Log("Pakette genel ayar vardı: polling " + _globalPoll + " kHz yapıldı (klavyeye yazılmadı).");
                return "Polling hızı " + _globalPoll + " kHz olarak ayarlandı (klavyeye henüz yazılmadı).";
            }
            byte[] profilesJson = files["profiles.json"];
            try
            {
                string text = Encoding.UTF8.GetString(profilesJson);
                if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
                AppData a = new JavaScriptSerializer().Deserialize<AppData>(text);
                if (a == null || a.Profiles == null) return null;
                bool differed; string detail, selName;
                int v = GenelStore.ChooseFromProfiles(a.Profiles, a.SelectedSlot, out differed, out detail, out selName);
                if (v < 0) return null;
                GenelData g = GenelStore.Load();
                g.PollRate = v;
                GenelStore.Save(g);
                _globalPoll = v;
                FillPollUi();
                Log("Pakette polling bilgisi vardı: genel ayar " + v + " kHz yapıldı (klavyeye yazılmadı)." + (differed ? " Farklı değerler: " + detail : ""));
                return "Polling hızı " + v + " kHz olarak ayarlandı (klavyeye henüz yazılmadı)." +
                    (differed ? "\nPaketteki profillerde farklı değerler vardı (" + detail + "); " + (string.IsNullOrEmpty(selName) ? "ilk" : selName) + " profilinin değeri alındı." : "");
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ arayüz

        // Aktivasyon > Polling Rate alt sekmesi: tüm klavye için polling hızı.
        private void BuildPollPage(TabPage tp)
        {
            _globalPoll = GenelStore.Load().PollRate;

            Panel box = new Panel();
            box.Dock = DockStyle.Top;
            box.Height = 200;
            box.BackColor = PanelBg;
            box.Padding = new Padding(16);
            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.Padding = new Padding(10);
            wrap.Controls.Add(box);
            tp.Controls.Add(wrap);

            Label cap = new Label();
            cap.Text = "Polling hızı (tüm klavye, profillerden bağımsız)";
            cap.ForeColor = TxtClr;
            cap.Font = new Font("Segoe UI Semibold", 10.5f);
            cap.AutoSize = true;
            cap.Location = new Point(16, 14);
            box.Controls.Add(cap);

            Label note = new Label();
            note.Text = "Bu ayar bütün profilleri etkiler. Profil değiştirmez ve “Cihaza Yaz” ile yazılmaz; klavyeye yalnızca buradaki “Klavyeye yaz” ile gider.";
            note.ForeColor = Muted;
            note.Font = new Font("Segoe UI", 8.8f);
            note.AutoSize = false;
            note.SetBounds(16, 42, 640, 36);
            box.Controls.Add(note);

            _pollCombo = new FlatCombo();
            _pollCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int r in GenelStore.PollRates) { ModeItem it = new ModeItem(); it.Value = r; it.Name = r + " kHz"; _pollCombo.Items.Add(it); }
            _pollCombo.BackColor = Theme.Raised;
            _pollCombo.ForeColor = TxtClr;
            _pollCombo.FlatStyle = FlatStyle.Flat;
            _pollCombo.Bounds = new Rectangle(16, 92, 150, 30);
            _pollCombo.SelectedIndexChanged += delegate
            {
                if (_pollLoading || _pollCombo.SelectedItem == null) return;
                SetGlobalPoll(((ModeItem)_pollCombo.SelectedItem).Value);
            };
            box.Controls.Add(_pollCombo);

            Button write = MakeButton("Klavyeye yaz", true);
            write.Location = new Point(180, 90);
            write.Click += delegate { WritePollRate(); };
            box.Controls.Add(write);

            Button read = MakeButton("Klavyeden oku", false);
            read.Location = new Point(180 + write.Width + 8, 90);
            read.Click += delegate { ReadPollRateFromDevice(); };
            box.Controls.Add(read);

            _pollStatus = new Label();
            _pollStatus.ForeColor = Muted;
            _pollStatus.Font = new Font("Segoe UI", 8.8f);
            _pollStatus.AutoSize = false;
            _pollStatus.SetBounds(16, 132, 760, 36);
            box.Controls.Add(_pollStatus);

            Label pollNote = new Label();
            pollNote.Text = "1 kHz varsayılan değer olduğu için klavyeden ayrıca doğrulanamaz; bu normaldir.";
            pollNote.ForeColor = Muted;
            pollNote.Font = new Font("Segoe UI", 8.2f);
            pollNote.AutoSize = false;
            pollNote.SetBounds(16, 170, 760, 18);
            box.Controls.Add(pollNote);

            FillPollUi();
        }

        private void FillPollUi()
        {
            if (_pollCombo == null) return;
            _pollLoading = true;
            try
            {
                for (int i = 0; i < _pollCombo.Items.Count; i++)
                    if (((ModeItem)_pollCombo.Items[i]).Value == _globalPoll) { _pollCombo.SelectedIndex = i; break; }
            }
            finally { _pollLoading = false; }
            if (_pollStatus != null && string.IsNullOrEmpty(_pollStatus.Text))
                _pollStatus.Text = "Kayıtlı ayar: " + _globalPoll + " kHz. Klavyeye henüz yazılmadı (bu oturumda).";
        }

        private void SetGlobalPoll(int rate)
        {
            if (!GenelStore.ValidRate(rate) || rate == _globalPoll) return;
            GenelData g = GenelStore.Load();
            g.PollRate = rate;
            try { GenelStore.Save(g); }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw;
                Log("UYARI: polling ayarı kaydedilemedi: " + ex.Message);
                FillPollUi(); // seçimi eski değere döndür
                MessageBox.Show(this, "Polling ayarı kaydedilemedi: veri klasörü salt okunur olabilir ya da başka bir program dosyayı kullanıyor olabilir.\n\nAyar değişmedi.\n\nAyrıntı: " + ex.Message,
                    "Polling hızı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _globalPoll = rate;
            if (_pollStatus != null) _pollStatus.Text = "Kayıtlı ayar: " + rate + " kHz. Klavyeye yazmak için “Klavyeye yaz”a basın.";
            Log("Polling hızı (tüm klavye) ayarı: " + rate + " kHz (klavyeye yazılmadı).");
        }

        // Sonuç kutusu (yazma sonrası). back = geri okunan değer; problem = bağlantı sorunu metni.
        private void ShowPollWriteResult(int rate, int back, string problem)
        {
            string msg; MessageBoxIcon icon = MessageBoxIcon.Information;
            if (problem != null)
            {
                msg = "Polling hızı yazıldı ama doğrulanamadı (" + problem + ").\n\n“Klavyeden oku” ile kontrol edin.";
                icon = MessageBoxIcon.Warning;
                Log("UYARI: polling " + rate + " kHz yazıldı ama doğrulanamadı: " + problem + ".");
            }
            else if (rate == 1)
            {
                msg = "Polling hızı 1 kHz olarak yazıldı.";
                Log("Polling 1 kHz yazıldı (1 kHz varsayılan değer olduğu için klavyeden ayrıca doğrulanamaz).");
            }
            else if (back == rate)
            {
                msg = "Polling hızı " + rate + " kHz olarak yazıldı ve doğrulandı.";
                Log("Polling " + rate + " kHz yazıldı ve doğrulandı.");
            }
            else
            {
                msg = "Polling hızı yazıldı ama klavye " + back + " kHz gösteriyor (beklenen " + rate + " kHz).\n\n" +
                    (back == 1 ? "Klavyeden okuma başarısız olduğunda da 1 kHz görünür; bu yüzden emin olunamadı.\n\n" : "") +
                    "Program kendiliğinden tekrar yazmadı. İsterseniz “Klavyeye yaz”a yeniden basın.";
                icon = MessageBoxIcon.Warning;
                Log("UYARI: polling uyuşmazlığı: yazılan " + rate + " kHz, okunan " + back + " kHz. Yeniden yazılmadı.");
            }
            if (_pollStatus != null) _pollStatus.Text = msg.Replace("\n\n", " ").Replace("\n", " ");
            MessageBox.Show(this, msg, "Polling hızı", MessageBoxButtons.OK, icon);
        }

        private void WritePollRate()
        {
            int rate = _globalPoll;
            bool sim = TestMode && SimWrite; // test kipi + W75_SIM_YAZ=1: komut gitmez
            if (!sim && !EnsureDevice()) return;
            if (MessageBox.Show(this,
                "Polling hızı " + rate + " kHz olarak klavyeye yazılacak.\n\nBu ayar tüm profilleri etkiler. Yazma sırasında USB kablosunu çıkarmayın. Devam edilsin mi?",
                "Polling hızı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (sim)
            {
                Log("Polling yazma (test kipi, simülasyon): SetPollRate(" + rate + ") GÖNDERİLMEDİ; ReadPollRate GÖNDERİLMEDİ; tüm klavye için tek ayar (profil seçilmedi).");
                ShowPollWriteResult(rate, SimPollReadBack(rate), null);
                return;
            }
            RunDevice(delegate
            {
                Log("Polling yazma başladı: " + rate + " kHz (tüm klavye, profil seçilmeden).");
                _dev.SetPollRate(rate);
                Action<string> oldLog = _dev.ConnectionLog;
                _dev.ConnectionLog = Log;
                int back = -1; string problem = null;
                try
                {
                    if (!TriggerVerify.Settle(_dev, Log)) problem = "klavye 5 saniye içinde geri gelmedi";
                    for (int a = 1; a <= 3 && back < 0 && problem == null; a++)
                    {
                        if (a > 1 && !TriggerVerify.EnsureLink(_dev, Log)) { problem = "klavye geri gelmedi"; break; }
                        try { back = _dev.ReadPollRate(); }
                        catch (Exception ex) { Log("Polling geri okunamadı: deneme " + a + "/3 (" + ex.Message + ")"); }
                        Thread.Sleep(100);
                    }
                    if (back < 0 && problem == null) problem = "geri okuma başarısız";
                }
                catch (Exception ex) { problem = ex.Message; }
                finally { _dev.ConnectionLog = oldLog; }
                ShowPollWriteResult(rate, back, problem);
            });
        }

        // "Klavyeden oku": normal durumda kutu çıkmaz. 1 kHz okunursa (varsayılan değer, ayrıca doğrulanamaz) yalnızca küçük gri yazı
        // gösterilir ve ayar değişmez. 2/4/8 okunur ve programdakinden farklıysa sade bir soru çıkar (varsayılan cevap Hayır);
        // okuma hatasında hiçbir şey değişmez.
        private void HandlePollRead(int back)
        {
            if (back == 1)
            {
                if (_pollStatus != null) _pollStatus.Text = "Klavye 1 kHz gösteriyor (varsayılan değer olduğu için ayrıca doğrulanamaz).";
                Log("Polling okundu: 1 kHz (varsayılan değer olduğu için ayrıca doğrulanamaz); ayar değişmedi.");
                return;
            }
            if (back == _globalPoll)
            {
                if (_pollStatus != null) _pollStatus.Text = "Klavye " + back + " kHz gösteriyor; programdaki ayarla aynı.";
                Log("Polling okundu: " + back + " kHz, programdaki ayarla aynı.");
                return;
            }
            if (MessageBox.Show(this, "Klavye " + back + " kHz gösteriyor. Programdaki polling ayarı " + back + " kHz olarak güncellensin mi?",
                "Polling hızı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                Log("Polling okundu (" + back + " kHz); kullanıcı uygulamadı, ayar değişmedi.");
                return;
            }
            SetGlobalPoll(back);
            FillPollUi();
            Log("Polling ayarı klavyeden okunan değere çekildi: " + back + " kHz.");
        }

        private void ReadPollRateFromDevice()
        {
            bool sim = TestMode && SimWrite;
            if (!sim && !EnsureDevice()) return;
            if (sim)
            {
                int v = SimPollReadBack(-1); // W75_SIM_POLL_OKU verilmişse o değer "okunmuş" sayılır
                if (v < 0)
                {
                    Log("Polling okuma (test kipi, simülasyon): ReadPollRate GÖNDERİLMEDİ; hiçbir şey değişmedi.");
                    MessageBox.Show(this, "Test kipinde klavyeden okunmadı; polling ayarı değişmedi.", "Polling hızı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Log("Polling okuma (test kipi, simülasyon): ReadPollRate GÖNDERİLMEDİ; simüle okunan değer " + v + " kHz.");
                HandlePollRead(v);
                return;
            }
            RunDevice(delegate
            {
                int back;
                try { back = _dev.ReadPollRate(); }
                catch (Exception ex) { Log("Polling okunamadı: " + ex.Message + " (ayar değişmedi)"); if (_pollStatus != null) _pollStatus.Text = "Klavyeden okunamadı; ayar değişmedi."; return; }
                HandlePollRead(back);
            });
        }    }
}
