using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Profil listesi: (1) "Profile tıklayınca klavyede etkinleştir" onay kutusu, (2) her profilin altında
    // "Cihazda güncel / Değişti, Cihaza Yaz yap / Cihaza yazılmadı" durum yazısı.
    //
    // (1) Seçim yalnızca profil seçme komutunu (W75Device.SetProfile) gönderir; "Bu profili klavyede etkinleştir"
    //     düğmesiyle aynı çağrı (SendActivate). Başka hiçbir komut gönderilmez, cihaza kendiliğinden bağlanılmaz.
    //     Test kipinde (W75_CIHAZSIZ=1) hiçbir komut gönderilmez.
    // (2) Son başarılı Cihaza Yaz'daki profil içeriğinin özeti yazim-durumu.json dosyasında (profiles.json'dan ayrı) tutulur.
    public class WriteStateData
    {
        public System.Collections.Generic.Dictionary<string, string> Slots = new System.Collections.Generic.Dictionary<string, string>();
    }

    public partial class MainForm
    {
        private const int ClickActivateDebounceMs = 300;

        private CheckBox _chkClickActivate;
        private Timer _activateTimer;
        private int _pendingActivateSlot = -1;
        private bool _clickActivate = true; // varsayılan AÇIK; pencere.json'dan okunur

        private static bool SimWrite { get { return Environment.GetEnvironmentVariable("W75_SIM_YAZ") == "1"; } }

        // Cihaz hazır mı: bağlantı açık. Test kipinde + W75_SIM_YAZ=1 iken cihaz "hazır" sayılır (hiçbir komut gönderilmez;
        // etkinleştirme ve canlı ışık koşullarını cihazsız denemek içindir).
        private bool DeviceReady { get { return (TestMode && SimWrite) || (_dev != null && _dev.IsOpen); } }

        private static string WriteStatePath { get { return Path.Combine(Store.Dir, "yazim-durumu.json"); } }

        // ------------------------------------------------------------ onay kutusu

        private Control BuildClickActivateBox()
        {
            _clickActivate = LoadClickActivate();
            _chkClickActivate = new CheckBox();
            _chkClickActivate.Text = "Profile tıklayınca klavyede etkinleştir";
            _chkClickActivate.ForeColor = TxtClr;
            _chkClickActivate.Font = new System.Drawing.Font("Segoe UI", 8.5f);
            _chkClickActivate.Width = 220;
            _chkClickActivate.Height = 24;
            _chkClickActivate.Margin = new Padding(0, 0, 0, 6);
            _chkClickActivate.Checked = _clickActivate;
            _chkClickActivate.CheckedChanged += delegate
            {
                _clickActivate = _chkClickActivate.Checked;
                if (!_clickActivate) CancelPendingActivate();
                SaveClickActivate(_clickActivate);
            };
            new ToolTip().SetToolTip(_chkClickActivate,
                "Açıkken listeden bir profile tıklamak, klavyeye yalnızca \"bu profile geç\" komutunu gönderir (ayar yazmaz). " +
                "Cihaz bağlı değilse atlanır.");

            _activateTimer = new Timer();
            _activateTimer.Interval = ClickActivateDebounceMs;
            _activateTimer.Tick += delegate { ActivateTimerTick(); };

            // Zaten seçili olan profile yeniden tıklamak da etkinleştirir (seçim değişmediği için SelectedIndexChanged
            // tetiklenmez): açılışta seçili gelen profil klavyede etkin olarak bilinmez, bu yolla bilinir olur.
            _profileList.MouseDown += delegate(object s, MouseEventArgs e)
            {
                int i = _profileList.IndexFromPoint(e.Location);
                if (i >= 0 && i == _data.SelectedSlot && i != _activeSlot) ScheduleActivate(i);
            };
            return _chkClickActivate;
        }

        private static bool LoadClickActivate()
        {
            try
            {
                string path = Path.Combine(Store.Dir, "pencere.json");
                if (!File.Exists(path)) return true;
                WindowBoundsData d = new JavaScriptSerializer().Deserialize<WindowBoundsData>(File.ReadAllText(path, Encoding.UTF8));
                return d == null || !d.ClickActivateOff;
            }
            catch { return true; }
        }

        private static void SaveClickActivate(bool on)
        {
            try
            {
                string path = Path.Combine(Store.Dir, "pencere.json");
                WindowBoundsData d = null;
                try { if (File.Exists(path)) d = new JavaScriptSerializer().Deserialize<WindowBoundsData>(File.ReadAllText(path, Encoding.UTF8)); }
                catch { }
                if (d == null) d = new WindowBoundsData();
                d.ClickActivateOff = !on;
                File.WriteAllText(path, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }

        // ------------------------------------------------------------ tıklayınca etkinleştirme

        // Kullanıcı listeden başka bir profil seçti (ProfileList_SelectedIndexChanged). 300 ms içinde yeni seçim
        // gelirse yalnızca son seçilen profil gider.
        private void ScheduleActivate(int slot)
        {
            if (!_clickActivate || _activateTimer == null) return;
            _pendingActivateSlot = slot;
            _activateTimer.Stop();
            _activateTimer.Start();
        }

        private void CancelPendingActivate()
        {
            _pendingActivateSlot = -1;
            if (_activateTimer != null) _activateTimer.Stop();
        }

        // Canlı ışık, etkinleştirme beklerken araya girmesin.
        private bool ActivatePending { get { return _activateTimer != null && _activateTimer.Enabled; } }

        private void ActivateTimerTick()
        {
            _activateTimer.Stop();
            int slot = _pendingActivateSlot;
            _pendingActivateSlot = -1;
            if (!_clickActivate || slot < 0 || slot >= _data.Profiles.Count) return;
            if (!DeviceLimits.SlotAllowed(slot))
            {
                // Slot sınırı: komut gönderilmez (profil listesi zaten en çok 4 satır gösterir; savunma amaçlı).
                Log(DeviceLimits.Message(slot));
                if (_liveOn) LiveOff(DeviceLimits.Message(slot));
                return;
            }
            Profile p = _data.Profiles[slot];

            if (TestMode && !SimWrite)
            {
                Log("Profile tıklama (test kipi): klavyeye komut GÖNDERİLMEDİ (" + DeviceProfiles.Describe(slot) + ")");
                // Cihaz yok sayılır: gerçekte de "cihaz bağlı değil" ile aynı sonuç (canlı ışık kendini kapatır).
                if (_liveOn) LiveOff("cihaz bağlı değil (test kipi)");
                return;
            }
            if (!DeviceReady)
            {
                Log("Profile tıklama: cihaz bağlı değil, etkinleştirme atlandı (" + DeviceProfiles.Describe(slot) + ")");
                if (_liveOn) LiveOff("cihaz bağlı değil; sağ üstteki Bağlan'a basın");
                return;
            }
            if (_busy)
            {
                Log("Profile tıklama: başka bir cihaz işlemi sürüyor, etkinleştirme atlandı (" + DeviceProfiles.Describe(slot) + ")");
                return;
            }
            _busy = true;
            bool ok = false;
            string err = null;
            try
            {
                if (TestMode) SimActivate(p); // test kipi + W75_SIM_YAZ=1: komut gitmez
                else SendActivate(p);
                ok = true;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                Log("Profil etkinleştirilemedi (yeniden denenmeyecek): " + err);
            }
            finally { _busy = false; }

            if (!ok)
            {
                // Profil seçme başarısız: yeni profilin ışığı gönderilmez, canlı ışık nedenini yazarak kapanır.
                if (_liveOn) LiveOff("profil seçme komutu gönderilemedi (yeniden denenmeyecek): " + err);
                return;
            }
            LightAfterProfileSwitch(p); // canlı ışık açıksa yeni profilin ışığı bir kez gönderilir; kapalıysa hiçbir şey
        }

        // "Bu profili klavyede etkinleştir" düğmesinin de kullandığı ortak çağrı: yalnızca profil seçme komutu.
        // SetProfile komuttan sonra kendisi 250 ms bekler (Protocol.cs); sonraki komut (canlı ışık) bu beklemeden sonra gider.
        // Hata olursa hangi profilin etkin olduğu bilinemez: "Etkin" işareti temizlenir (yanlış işaret kalmasın).
        private void SendActivate(Profile p)
        {
            if (!DeviceLimits.SlotAllowed(p.Slot)) throw new InvalidOperationException(DeviceLimits.Message(p.Slot)); // slot sınırı
            try { DeviceProfiles.Select(_dev, p.Slot); }
            catch { MarkActive(-1); throw; }
            MarkActive(p.Slot);
            Log("Klavye profili " + DeviceProfiles.ToDevice(p.Slot) + " etkinleştirildi (program slotu " + p.Slot + "): " + p.Name);
        }

        // Test kipi + W75_SIM_YAZ=1: SetProfile'ın simülasyonu (hiçbir komut gitmez). W75_SIM_PROFIL_HATA=1 ise SetProfile hatası simüle edilir.
        private void SimActivate(Profile p)
        {
            if (!DeviceLimits.SlotAllowed(p.Slot)) throw new InvalidOperationException(DeviceLimits.Message(p.Slot)); // slot sınırı
            if (Environment.GetEnvironmentVariable("W75_SIM_PROFIL_HATA") == "1")
            {
                MarkActive(-1);
                throw new InvalidOperationException("simüle edilmiş profil seçme hatası");
            }
            MarkActive(p.Slot);
            Log("Profile tıklama (test kipi, simülasyon): SetProfile(" + DeviceProfiles.ToDevice(p.Slot) + ") GÖNDERİLMEDİ (" + DeviceProfiles.Describe(p.Slot) + ") (gerçekte komuttan sonra 250 ms beklenir); slot " + p.Slot + " etkin sayıldı");
        }

        // Profil seçme komutu BAŞARILI olduktan sonra: canlı ışık açıksa yeni profilin ışığı bir kez gönderilir (klavye, profil
        // geçişinde önceki profilin ışığını gösterebiliyordu). Bekleyen eski gönderim iptal edilir; gönderim anında seçili profilin
        // ışığı (LiveLightSend: Current) kullanılır. Canlı ışık kapalıyken yalnızca profil seçme komutu gitmiş olur.
        private void LightAfterProfileSwitch(Profile p)
        {
            if (!_liveOn) return;
            if (_liveDebounce != null) _liveDebounce.Stop();      // eski profile ait bekleyen gönderimi iptal et
            if (Current.Slot != p.Slot) return;                    // seçim bu arada değiştiyse yeni tıklama kendi akışını başlatır
            Log("Canlı ışık: profil geçişi (cihaz profili " + DeviceProfiles.ToDevice(p.Slot) + "); yeni profilin ışığı gönderiliyor");
            LiveLightSend();
        }

        // ------------------------------------------------------------ yazım durumu

        // Profil içeriğinin özeti (ad hariç: yeniden adlandırma klavyedeki veriyi değiştirmez; slot dahil).
        private static string ProfileHash(Profile p)
        {
            Profile c = p.Clone();
            c.Name = "";
            string json = new JavaScriptSerializer().Serialize(c);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static WriteStateData LoadWriteState()
        {
            try
            {
                if (File.Exists(WriteStatePath))
                {
                    WriteStateData d = new JavaScriptSerializer().Deserialize<WriteStateData>(File.ReadAllText(WriteStatePath, Encoding.UTF8));
                    if (d != null && d.Slots != null) return d;
                }
            }
            catch { }
            return new WriteStateData();
        }

        // Cihaza Yaz'ın başarı yolunda çağrılır: bu slotta klavyeye yazılan içeriğin özetini kaydeder.
        private void MarkWritten(Profile p)
        {
            try
            {
                WriteStateData d = LoadWriteState();
                d.Slots[p.Slot.ToString()] = ProfileHash(p);
                File.WriteAllText(WriteStatePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log("Yazım durumu kaydedilemedi: " + ex.Message); }
            _liveSentSlots.Remove(p.Slot); // tam yazma yapıldı: "Işık cihazda; ayarlar için Cihaza Yaz" notu kalkar
            if (_profileList != null) _profileList.Invalidate();
        }

        // 0 = Cihaza yazılmadı, 1 = Cihazda güncel, 2 = Değişti, Cihaza Yaz yap
        private int WriteStatus(Profile p)
        {
            string saved;
            if (!LoadWriteState().Slots.TryGetValue(p.Slot.ToString(), out saved)) return 0;
            return saved == ProfileHash(p) ? 1 : 2;
        }

        // Test kipi simülasyonu (yalnızca W75_CIHAZSIZ=1 ve W75_SIM_YAZ=1): Cihaza Yaz'a basılınca klavyeye hiçbir şey
        // gönderilmeden başarılı yazma sonrası kayıt güncellenir. Durum yazılarını cihazsız göstermek içindir.
        private bool TrySimulateWrite()
        {
            if (!TestMode || !SimWrite) return false;
            string scenario = WriteVerifier.SimScenario(); // W75_SIM_DOGRULAMA: yazma sonrası doğrulama kutularını cihazsız denemek için
            if (scenario != null)
            {
                Profile sp = Current;
                byte[] eb = Keymap.BuildBase(sp, new byte[528]);
                byte[] ef = Keymap.BuildFn(sp);
                WriteCheck chk = WriteVerifier.Simulate(sp, scenario, eb, ef);
                TriggerVerifyResult tvOk = new TriggerVerifyResult(); // simülasyonda aktivasyon doğrulaması başarılı sayılır
                WriteOutcome oc = WriteVerifier.Compose(chk, tvOk, new System.Collections.Generic.List<string>());
                MarkActive(sp.Slot);
                if (oc.Mark) MarkWritten(sp); else Log("Yazım durumu güncellenmedi: " + oc.Reason);
                Log("Cihaza Yaz (test kipi, simülasyon, senaryo=" + scenario + "): klavyeye komut GÖNDERİLMEDİ; doğrulama sonucu: " + (oc.Mark ? "yazım durumu güncellendi" : "yazım durumu güncellenmedi (" + oc.Reason + ")") + ": " + sp.Name);
                MessageBox.Show(this, oc.Text, "Cihaza yaz", MessageBoxButtons.OK, oc.Icon);
                return true;
            }
            MarkWritten(Current);
            MarkActive(Current.Slot); // gerçek Cihaza Yaz da profil seçme komutuyla slotu etkin yapar
            Log("Cihaza Yaz (test kipi, simülasyon): SetProfile(" + DeviceProfiles.ToDevice(Current.Slot) + "), anahtar eşlemesi, WriteLight, aktivasyon hepsi GÖNDERİLMEDİ (polling artık Cihaza Yaz ile yazılmaz) (" + DeviceProfiles.Describe(Current.Slot) + "); yazım durumu kaydı güncellendi: " + Current.Name);
            return true;
        }
    }
}
