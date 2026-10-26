using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Işıklandırma sekmesi: "Canlı uygula" ve #RRGGBB renk kutuları.
    // Canlı uygula YALNIZCA ışık komutunu gönderir: Cihaza Yaz'ın ışık için kullandığı _dev.WriteLight(false, ...)
    // çağrısının aynısı. Tuş haritası, aktivasyon, ölü bölge, SOCD, Diğer ve anketleme komutu
    // gönderilmez (tek istisna: kutu işaretlenirken profil etkin değilse bir kez profil seçme, aşağıya bakın); cihaza otomatik bağlanılmaz. Test kipinde (W75_CIHAZSIZ=1) hiçbir komut gönderilmez.
    // Onay kutusu ayar olarak kaydedilmez, her açılışta kapalı başlar.
    //
    // Canlı uygulamanın İKİ koşulu vardır (Cihaza Yaz bir koşul DEĞİLDİR; yalnızca bu ikisini sağlamanın bir yoludur):
    //   1. Cihaz bağlı (DeviceReady): _dev açık. Program otomatik bağlanmaz; açılışta ya da Bağlan'la bağlanılır.
    //   2. Seçili profil klavyede etkin olarak biliniyor (Current.Slot == _activeSlot): _activeSlot, profil seçme komutu
    //      başarıyla gidince (profile tıklama, "Bu profili klavyede etkinleştir" ya da Cihaza Yaz) işaretlenir.
    //      Kutu işaretlenirken cihaz bağlı ama profil etkin değilse bu komut BİR kez kendiliğinden gönderilir
    //      (AutoActivateForLive); canlı ışık açıkken yapılan düzenlemelerde ise yalnızca WriteLight gider.
    public partial class MainForm
    {
        private const int LiveDebounceMs = 400;
        private const int LiveMaxSends = 200;
        private const int LiveMaxMinutes = 10;

        private CheckBox _liveLight;
        private Label _liveWarn, _liveStatus;
        private Timer _liveDebounce, _liveLimit;
        private bool _liveOn, _liveSuppress;
        private int _liveCount;
        private Stopwatch _liveClock;
        // Bu oturumda canlı ışık gönderilen slotlar: profil listesinde "Işık cihazda; ayarlar için Cihaza Yaz" yazar.
        // Cihaza Yaz başarılı olunca slot listeden çıkar.
        private readonly System.Collections.Generic.HashSet<int> _liveSentSlots = new System.Collections.Generic.HashSet<int>();

        private TextBox _hexFg, _hexBg;
        private Panel _hexFgFrame, _hexBgFrame;
        private static readonly Color HexBorder = Theme.Border;

        private static bool TestMode { get { return Environment.GetEnvironmentVariable("W75_CIHAZSIZ") == "1"; } }

        // BuildLightTab'dan çağrılır: renk düğmelerinin altına #RRGGBB kutuları, altına Canlı uygula.
        // Dönen değer: eklenen denetimlerin altındaki ilk boş y.
        private int BuildLiveLightControls(Panel left, int y)
        {
            _hexFgFrame = MakeHexBox(0, y, true, out _hexFg);
            _hexBgFrame = MakeHexBox(205, y, false, out _hexBg);
            left.Controls.Add(_hexFgFrame);
            left.Controls.Add(_hexBgFrame);
            y += 40;

            _liveLight = new CheckBox();
            _liveLight.Text = "Canlı uygula";
            _liveLight.ForeColor = TxtClr;
            _liveLight.Checked = false;
            _liveLight.Bounds = new Rectangle(0, y, 160, 24);
            _liveLight.CheckedChanged += delegate { LiveCheckChanged(); };
            left.Controls.Add(_liveLight);

            Label warn = new Label();
            warn.Text = "Işığı klavyede anında gösterir. Kalıcı kayıt için Cihaza Yaz kullan.";
            warn.ForeColor = Muted;
            warn.Font = new Font("Segoe UI", 8.2f);
            warn.Bounds = new Rectangle(0, y + 26, 380, 34);
            left.Controls.Add(warn);
            _liveWarn = warn;

            // Canlı ışık kapanınca nedeni burada görünür (günlüğe de yazılır); açıkken boştur.
            _liveStatus = new Label();
            _liveStatus.ForeColor = Accent;
            _liveStatus.Font = new Font("Segoe UI", 8.2f);
            _liveStatus.Bounds = new Rectangle(0, y + 62, 380, 46);
            _liveStatus.Visible = false;
            left.Controls.Add(_liveStatus);

            _liveDebounce = new Timer();
            _liveDebounce.Interval = LiveDebounceMs;
            _liveDebounce.Tick += delegate { LiveLightSend(); };
            _liveLimit = new Timer();
            _liveLimit.Interval = LiveMaxMinutes * 60 * 1000;
            _liveLimit.Tick += delegate { LiveOff(LiveMaxMinutes + " dakika süre sınırı doldu"); };
            return y + 64;
        }

        private Panel MakeHexBox(int x, int y, bool fg, out TextBox box)
        {
            // TextBox çerçeve rengini desteklemediği için 1 px kenarlıklı bir panel içine konur (geçersizde kırmızı).
            Panel frame = new Panel();
            frame.Bounds = new Rectangle(x, y, 110, 28);
            frame.BackColor = HexBorder;
            frame.Padding = new Padding(1);
            TextBox t = new TextBox();
            t.BorderStyle = BorderStyle.None;
            t.Multiline = true; // dikey ortalama için yükseklik ayarlanabilsin
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Raised;
            t.ForeColor = TxtClr;
            t.Font = new Font("Consolas", 10.5f);
            t.MaxLength = 7;
            t.TextAlign = HorizontalAlignment.Center;
            t.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true; // çok satırlı kutuda Enter yeni satır eklemesin
                ApplyHex(fg);
            };
            t.Leave += delegate { ApplyHex(fg); };
            t.TextChanged += delegate { if (!_loading) frame.BackColor = HexBorder; };
            frame.Controls.Add(t);
            new ToolTip().SetToolTip(t, (fg ? "Ön renk" : "Arka renk") + ": #RRGGBB yazıp Enter'a basın");
            box = t;
            return frame;
        }

        // FillLightUiInternal'dan çağrılır (profil yüklenirken / renk değişince): kutuları günceller, gönderim yapmaz.
        private void FillHexBoxes(LightSetting l)
        {
            if (_hexFg == null) return;
            _hexFg.Text = "#" + HexColor(l.R, l.G, l.B);
            _hexBg.Text = "#" + HexColor(l.BR, l.BG, l.BB);
            _hexFgFrame.BackColor = HexBorder;
            _hexBgFrame.BackColor = HexBorder;
        }

        private void ApplyHex(bool fg)
        {
            TextBox t = fg ? _hexFg : _hexBg;
            Panel frame = fg ? _hexFgFrame : _hexBgFrame;
            string s = t.Text.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            int v;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
            {
                frame.BackColor = Theme.RedBorder;
                return;
            }
            int r = (v >> 16) & 0xFF, g = (v >> 8) & 0xFF, b = v & 0xFF;
            LightSetting l = Current.Light;
            bool same = fg ? (l.R == r && l.G == g && l.B == b) : (l.BR == r && l.BG == g && l.BB == b);
            if (same) { FillHexBoxes(l); return; }
            if (fg) { l.R = r; l.G = g; l.B = b; } else { l.BR = r; l.BG = g; l.BB = b; }
            Store.Save(_data);
            FillLightUiInternal();
            ScheduleLiveLight();
        }

        // Kullanıcı ışık ayarını değiştirdi: canlı mod açıksa 400 ms sonra (son değişiklikten itibaren) gönder.
        private void ScheduleLiveLight()
        {
            if (!_liveOn || _liveDebounce == null) return;
            _liveDebounce.Stop();
            _liveDebounce.Start();
        }

        // Profil değiştirilirken bekleyen gönderim iptal edilir (profil seçmek gönderim yapmaz).
        private void CancelPendingLiveLight()
        {
            if (_liveDebounce != null) _liveDebounce.Stop();
        }

        // Canlı uygula satırlarını (onay kutusu, açıklama, varsa durum satırı) y'den başlayarak dizer; sonraki boş y'yi döner.
        private int LayoutLiveLightRows(int y)
        {
            _liveLight.Top = y;
            _liveWarn.Top = y + 26;
            y += 64;
            _liveStatus.Visible = _liveStatus.Text.Length > 0;
            if (_liveStatus.Visible) { _liveStatus.Top = y - 2; y += 48; }
            return y;
        }

        private void SetLiveStatus(string text)
        {
            if (_liveStatus == null || _liveStatus.Text == text) return;
            _liveStatus.Text = text;
            ApplyModeOptions(Current.Light.Mode); // satırı göster/gizle ve yeniden diz
        }

        // Canlı ışığın önündeki engel (yoksa null). Test kipinde (simülasyon dışında) çağrılmaz.
        private string LiveBlockReason()
        {
            if (!DeviceReady)
                return "cihaz bağlı değil; sağ üstteki Bağlan'a basın";
            if (!DeviceLimits.SlotAllowed(Current.Slot))
                return DeviceLimits.Message(Current.Slot); // slot sınırı: ışık da gönderilmez
            if (Current.Slot != _activeSlot)
                return "seçili profil (" + Current.Name + ") klavyede etkin değil; Canlı uygula'yı yeniden işaretleyin " +
                       "(profil bir kez kendiliğinden etkinleştirilir)";
            return null;
        }

        private void LiveCheckChanged()
        {
            if (_liveSuppress) return;
            if (!_liveLight.Checked) { LiveOff("kullanıcı kapattı"); return; }
            SetLiveStatus("");
            _liveOn = true;
            _liveCount = 0;
            _liveClock = Stopwatch.StartNew();
            _liveLimit.Stop();
            _liveLimit.Start();
            Log("Canlı ışık açıldı (en çok " + LiveMaxSends + " gönderim / " + LiveMaxMinutes + " dakika)");
            if (!AutoActivateForLive()) return; // gerekirse profil seçme komutu BİR kez gider
            LiveLightSend(); // işaretlenince mevcut ışık ayarı bir kez gönderilir
        }

        // Kutu işaretlenirken cihaz bağlıysa ama seçili profil etkin değilse: profil seçme komutu (SetProfile, profile
        // tıklamadakiyle aynı çağrı) BİR kez gönderilir ve canlı ışık devam eder. Yalnızca işaretleme anında; sonraki
        // düzenlemelerde yalnızca ışık komutu gider. Hata olursa yeniden denenmez, canlı ışık kapanır.
        // Cihaz bağlı değilse hiçbir şey yapılmaz (LiveLightSend nedenini söyler); otomatik bağlanma yok.
        private bool AutoActivateForLive()
        {
            if (!DeviceReady || Current.Slot == _activeSlot) return true;
            if (!DeviceLimits.SlotAllowed(Current.Slot)) { LiveOff(DeviceLimits.Message(Current.Slot)); return false; } // slot sınırı
            if (_busy) return true; // başka cihaz işlemi sürüyor: LiveLightSend sonra yeniden dener / nedenini söyler
            CancelPendingActivate();
            Profile p = Current;
            if (TestMode)
            {
                // Yalnızca test kipi + W75_SIM_YAZ=1 buraya gelir (cihaz hazır sayılır): komut gitmez.
                MarkActive(p.Slot);
                Log("Canlı ışık: seçili profil etkin değildi; profil seçme komutu bir kez gönderildi (test kipi, simülasyon: SetProfile(" + DeviceProfiles.ToDevice(p.Slot) + ") GÖNDERİLMEDİ; " + DeviceProfiles.Describe(p.Slot) + ")");
                return true;
            }
            _busy = true;
            try
            {
                SendActivate(p);
                Log("Canlı ışık: seçili profil etkin değildi; profil seçme komutu bir kez gönderildi (cihaz profili " + DeviceProfiles.ToDevice(p.Slot) + ")");
                return true;
            }
            catch (Exception ex)
            {
                _busy = false;
                LiveOff("profil seçme komutu gönderilemedi (yeniden denenmeyecek): " + ex.Message);
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        private void LiveOff(string reason)
        {
            bool wasOn = _liveOn;
            _liveOn = false;
            if (_liveDebounce != null) _liveDebounce.Stop();
            if (_liveLimit != null) _liveLimit.Stop();
            if (_liveLight != null && _liveLight.Checked)
            {
                _liveSuppress = true;
                _liveLight.Checked = false;
                _liveSuppress = false;
            }
            if (wasOn) Log("Canlı ışık kapatıldı: " + reason);
            // Kullanıcı kapattıysa durum satırı boş; otomatik kapanmada nedeni görünür (kutu sessizce işaretsiz kalmasın).
            SetLiveStatus(reason == "kullanıcı kapattı" ? "" : "Canlı ışık kapandı: " + reason);
        }

        private void LiveLightSend()
        {
            _liveDebounce.Stop();
            if (!_liveOn) return;
            // Başka bir cihaz işlemi sürüyorsa (ör. Cihaza Yaz) araya girme; bitince yalnızca son değer gider.
            if (_busy) { _liveDebounce.Start(); return; }
            // Profile tıklayınca etkinleştirme bekliyorsa (300 ms) onun bitmesini bekle: ışık yanlış profile gitmesin.
            if (ActivatePending) { _liveDebounce.Start(); return; }
            if (_liveClock != null && _liveClock.Elapsed.TotalMinutes >= LiveMaxMinutes) { LiveOff(LiveMaxMinutes + " dakika süre sınırı doldu"); return; }
            if (_liveCount >= LiveMaxSends) { LiveOff(LiveMaxSends + " gönderim sınırı doldu"); return; }

            bool sim = TestMode && SimWrite; // test kipi + W75_SIM_YAZ=1: cihaz hazır sayılır, hiçbir komut gitmez
            if (TestMode && !sim)
            {
                // Test kipi: _dev'e hiç dokunulmaz, hiçbir komut gönderilmez.
                _liveCount++;
                Log("Canlı ışık (test kipi): klavyeye komut GÖNDERİLMEDİ (#" + _liveCount + ")");
                return;
            }
            // Otomatik bağlanma yok (bağlanmak da okuma komutları gönderir). Koşullar: cihaz bağlı + seçili profil etkin.
            // Profil seçme komutu canlı modda gönderilmediği için ışık klavyede o an etkin profile gider; programın
            // bildiği etkin profil seçili profil değilse başka bir profilin ışığını değiştirmemek için gönderilmez.
            string block = LiveBlockReason();
            if (block != null) { LiveOff(block); return; }
            if (sim)
            {
                _liveCount++;
                _liveSentSlots.Add(Current.Slot);
                if (_profileList != null) _profileList.Invalidate();
                Log("Canlı ışık (test kipi, simülasyon): klavyeye komut GÖNDERİLMEDİ (#" + _liveCount + ", WriteLight etkin profile = cihaz profili " + DeviceProfiles.ToDevice(Current.Slot) + ", " + DeviceProfiles.Describe(Current.Slot) + ")");
                return;
            }

            _busy = true;
            try
            {
                _dev.WriteLight(false, Current.Light.Clone()); // Cihaza Yaz'daki ışık komutunun aynısı
                _liveCount++;
                _liveSentSlots.Add(Current.Slot);
                if (_profileList != null) _profileList.Invalidate();
                Log("Canlı ışık gönderildi (#" + _liveCount + ")");
            }
            catch (Exception ex)
            {
                // Hid/Protocol bağlantıyı kendi mantığıyla yeniden açar ama komutu tekrar göndermez; burada da tekrar yok.
                _busy = false;
                LiveOff(ex.Message);
                return;
            }
            finally
            {
                _busy = false;
            }
            if (_liveCount >= LiveMaxSends) LiveOff(LiveMaxSends + " gönderim sınırına ulaşıldı");
        }
    }
}
