using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Genel ayarların yerel yedeği: %APPDATA%\W75Ayarlayici\diger.json (profiles.json'dan ayrı).
    public class DigerData
    {
        public string Updated;
        public MiscSettings Current;   // cihazdan son okunan / doğrulanan değerler
        public MiscSettings Previous;  // son yazmadan önce cihazda olan değerler
    }

    public static class DigerStore
    {
        public static string FilePath { get { return Path.Combine(Store.Dir, "diger.json"); } }

        public static DigerData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    DigerData d = new JavaScriptSerializer().Deserialize<DigerData>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (d != null) return d;
                }
            }
            catch { }
            return new DigerData();
        }

        public static void Save(DigerData d)
        {
            d.Updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
        }
    }

    // "Diğer" sekmesi: tuş kilitleri ve tuş modu. Klavyenin geneli içindir, profile bağlı değildir.
    public class DigerTab
    {
        private static readonly Color Bg = Color.FromArgb(18, 20, 26);
        private static readonly Color PanelBg = Color.FromArgb(24, 27, 35);
        private static readonly Color TxtClr = Color.FromArgb(230, 232, 239);
        private static readonly Color Muted = Color.FromArgb(139, 144, 160);
        private static readonly Color Accent = Color.FromArgb(255, 184, 107);

        private readonly Func<bool> _ensureDevice;
        private readonly Func<W75Device> _getDevice;
        private readonly Action<MethodInvoker> _run;
        private readonly Action<string> _log;

        private CheckBox _win, _shiftTab, _altTab, _altF4;
        private ComboBox _mode;
        private Label _modeCaption, _status;
        private bool _loading;
        private readonly HashSet<string> _dirty = new HashSet<string>();

        public DigerTab(Func<bool> ensureDevice, Func<W75Device> getDevice, Action<MethodInvoker> run, Action<string> log)
        {
            _ensureDevice = ensureDevice;
            _getDevice = getDevice;
            _run = run;
            _log = log;
        }

        private static Button MakeBtn(string text, int x, int y, int w, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Bounds = new Rectangle(x, y, w, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = accent ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(58, 64, 80);
            b.BackColor = accent ? Accent : Color.FromArgb(31, 35, 46);
            b.ForeColor = accent ? Color.FromArgb(28, 20, 8) : TxtClr;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private CheckBox MakeCheck(string text, int y, string key)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.ForeColor = TxtClr;
            c.Font = new Font("Segoe UI", 10f);
            c.Bounds = new Rectangle(20, y, 320, 26);
            c.CheckedChanged += delegate { if (!_loading) _dirty.Add(key); };
            return c;
        }

        public TabPage Build()
        {
            TabPage page = new TabPage("Diğer");
            page.BackColor = Bg;
            page.ForeColor = TxtClr;
            page.Padding = new Padding(0);

            Panel box = new Panel();
            box.Dock = DockStyle.Top;
            box.Height = 360;
            box.BackColor = PanelBg;
            box.Padding = new Padding(16);
            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.Padding = new Padding(12);
            wrap.Controls.Add(box);
            page.Controls.Add(wrap);

            Label scope = new Label();
            scope.Text = "Bu ayarlar klavyenin genelidir; profillere bağlı değildir ve profile yazılmaz.";
            scope.Font = new Font("Segoe UI Semibold", 10f);
            scope.ForeColor = Accent;
            scope.Bounds = new Rectangle(20, 14, 900, 24);
            box.Controls.Add(scope);

            _win = MakeCheck("Win tuşunu kilitle", 54, "win");
            _shiftTab = MakeCheck("Shift+Tab kilidi", 88, "shifttab");
            _altTab = MakeCheck("Alt+Tab kilidi", 122, "alttab");
            _altF4 = MakeCheck("Alt+F4 kilidi", 156, "altf4");
            box.Controls.Add(_win);
            box.Controls.Add(_shiftTab);
            box.Controls.Add(_altTab);
            box.Controls.Add(_altF4);

            _modeCaption = new Label();
            _modeCaption.Text = "Tuş modu";
            _modeCaption.ForeColor = Muted;
            _modeCaption.Font = new Font("Segoe UI", 9f);
            _modeCaption.AutoSize = true;
            _modeCaption.Location = new Point(20, 198);
            box.Controls.Add(_modeCaption);

            _mode = new ComboBox();
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Bounds = new Rectangle(20, 218, 200, 28);
            _mode.BackColor = Color.FromArgb(23, 26, 34);
            _mode.ForeColor = TxtClr;
            _mode.FlatStyle = FlatStyle.Flat;
            _mode.Items.Add("N Tuş");
            _mode.Items.Add("6 Tuş");
            _mode.SelectedIndex = 0;
            _mode.SelectedIndexChanged += delegate { if (!_loading) _dirty.Add("mode"); };
            box.Controls.Add(_mode);
            SetModeVisible(false);

            Button read = MakeBtn("Cihazdan Oku", 20, 264, 130, false);
            Button write = MakeBtn("Cihaza Yaz", 160, 264, 120, true);
            read.Click += delegate { ReadFromDevice(); };
            write.Click += delegate { WriteToDevice(); };
            box.Controls.Add(read);
            box.Controls.Add(write);

            _status = new Label();
            _status.ForeColor = Muted;
            _status.Font = new Font("Segoe UI", 8.5f);
            _status.Bounds = new Rectangle(20, 306, 900, 40);
            box.Controls.Add(_status);

            DigerData saved = DigerStore.Load();
            if (saved.Current != null)
            {
                Populate(saved.Current);
                _status.Text = "Son kaydedilen değerler gösteriliyor (" + saved.Updated + "), cihazdan okunmadı. Güncel durum için “Cihazdan Oku”.";
            }
            else _status.Text = "Henüz okunmadı. “Cihazdan Oku” ile klavyenin mevcut durumunu alın. Ayar yedeği: " + DigerStore.FilePath;
            return page;
        }

        // Ayar paketi yüklenince son kaydedilen değerleri diger.json'dan yeniden okur (klavyeye bir şey göndermez).
        public void ReloadFromFile()
        {
            DigerData saved = DigerStore.Load();
            if (saved.Current != null)
            {
                Populate(saved.Current);
                _status.Text = "Son kaydedilen değerler gösteriliyor (" + saved.Updated + "), cihazdan okunmadı. Güncel durum için “Cihazdan Oku”.";
            }
            else _status.Text = "Henüz okunmadı. “Cihazdan Oku” ile klavyenin mevcut durumunu alın. Ayar yedeği: " + DigerStore.FilePath;
        }

        private void SetModeVisible(bool v)
        {
            _modeCaption.Visible = v;
            _mode.Visible = v;
        }

        private void Populate(MiscSettings m)
        {
            _loading = true;
            _win.Checked = m.Win;
            _shiftTab.Checked = m.ShiftTab;
            _altTab.Checked = m.AltTab;
            _altF4.Checked = m.AltF4;
            _mode.SelectedIndex = m.SixKey ? 1 : 0;
            SetModeVisible(m.SixKeySupported); // yalnızca cihaz destekliyorsa göster
            _dirty.Clear();
            _loading = false;
        }

        private static string OnOff(bool v) { return v ? "açık" : "kapalı"; }

        private static string Describe(MiscSettings m)
        {
            return "Win kilidi=" + OnOff(m.Win) + ", Shift+Tab kilidi=" + OnOff(m.ShiftTab) +
                   ", Alt+Tab kilidi=" + OnOff(m.AltTab) + ", Alt+F4 kilidi=" + OnOff(m.AltF4) +
                   (m.SixKeySupported ? ", tuş modu=" + (m.SixKey ? "6 Tuş" : "N Tuş") : ", tuş modu=desteklenmiyor");
        }

        // a ile b arasındaki farkları "ad: a → b" olarak listeler.
        private static List<string> Diff(MiscSettings a, MiscSettings b)
        {
            List<string> r = new List<string>();
            if (a.Win != b.Win) r.Add("Win kilidi: " + OnOff(a.Win) + " → " + OnOff(b.Win));
            if (a.ShiftTab != b.ShiftTab) r.Add("Shift+Tab kilidi: " + OnOff(a.ShiftTab) + " → " + OnOff(b.ShiftTab));
            if (a.AltTab != b.AltTab) r.Add("Alt+Tab kilidi: " + OnOff(a.AltTab) + " → " + OnOff(b.AltTab));
            if (a.AltF4 != b.AltF4) r.Add("Alt+F4 kilidi: " + OnOff(a.AltF4) + " → " + OnOff(b.AltF4));
            if (a.SixKey != b.SixKey) r.Add("Tuş modu: " + (a.SixKey ? "6 Tuş" : "N Tuş") + " → " + (b.SixKey ? "6 Tuş" : "N Tuş"));
            return r;
        }

        private IWin32Window Owner { get { return _win.FindForm(); } }

        // ---------------- okuma ----------------

        private void ReadFromDevice()
        {
            if (!_ensureDevice()) return;
            _run(delegate
            {
                W75Device dev = _getDevice();
                MiscSettings m = dev.ReadMisc();
                Populate(m);
                DigerData d = DigerStore.Load();
                d.Current = m;
                DigerStore.Save(d);
                _status.Text = "Cihazdan okundu: " + Describe(m);
                _log("Diğer ayarlar okundu: " + Describe(m));
            });
        }

        // ---------------- yazma ----------------

        private void WriteToDevice()
        {
            if (_dirty.Count == 0)
            {
                MessageBox.Show(Owner, "Değiştirilmiş bir ayar yok. Önce bir ayarı değiştirin.", "Diğer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!_ensureDevice()) return;
            _run(delegate
            {
                W75Device dev = _getDevice();

                // Yalnızca değiştirilen ayarlar uygulanır; diğerleri cihazdaki güncel değerleriyle aynen yazılır.
                MiscSettings cur = dev.ReadMisc();
                MiscSettings target = cur.Clone();
                if (_dirty.Contains("win")) target.Win = _win.Checked;
                if (_dirty.Contains("shifttab")) target.ShiftTab = _shiftTab.Checked;
                if (_dirty.Contains("alttab")) target.AltTab = _altTab.Checked;
                if (_dirty.Contains("altf4")) target.AltF4 = _altF4.Checked;
                if (_dirty.Contains("mode") && cur.SixKeySupported) target.SixKey = _mode.SelectedIndex == 1;

                List<string> changes = Diff(cur, target);
                if (changes.Count == 0)
                {
                    Populate(cur);
                    _log("Diğer ayarlar: istenen değerler cihazda zaten aynı, yazılmadı.");
                    MessageBox.Show(Owner, "İstenen değerler cihazda zaten böyle; yazmaya gerek yok.", "Diğer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string summary = "Cihaza tek pakette (komut 20) şu değerler yazılacak: " + Describe(target) + ". Değişen: " + string.Join("; ", changes.ToArray());
                _log(summary);
                if (MessageBox.Show(Owner, "Klavyenin genel ayarları (tüm profiller için) yazılacak:\n\n" + string.Join("\n", changes.ToArray()) +
                    "\n\nDiğer ayarlar cihazdaki mevcut değerleriyle aynen yazılır. Devam edilsin mi?",
                    "Diğer — Cihaza Yaz", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    _log("Diğer ayarlar: yazma iptal edildi.");
                    return;
                }

                DigerData saved = DigerStore.Load();
                saved.Previous = cur;
                saved.Current = cur;
                DigerStore.Save(saved);

                dev.WriteMisc(target);
                _log("Diğer ayarlar yazıldı; 3 sn beklenip geri okunacak.");

                // Doğrulama: yazmadan sonra bekle (cihaz yeniden bağlanabilir), geri oku, karşılaştır.
                Action<string> oldLog = dev.ConnectionLog;
                dev.ConnectionLog = _log;
                MiscSettings back = null;
                string problem = null;
                try
                {
                    if (!TriggerVerify.Settle(dev, _log)) problem = "cihaz 5 saniye içinde geri gelmedi";
                    for (int a = 1; a <= 3 && back == null && problem == null; a++)
                    {
                        if (a > 1 && !TriggerVerify.EnsureLink(dev, _log)) { problem = "cihaz geri gelmedi"; break; }
                        try { back = dev.ReadMisc(); }
                        catch (Exception ex) { _log("Diğer ayarlar geri okunamadı: deneme " + a + "/3 (" + ex.Message + ")"); }
                        System.Threading.Thread.Sleep(100);
                    }
                    if (back == null && problem == null) problem = "geri okuma başarısız";
                }
                catch (Exception ex) { problem = ex.Message; }
                finally { dev.ConnectionLog = oldLog; }

                if (back == null)
                {
                    _log("UYARI: Diğer ayarlar yazıldı ama doğrulanamadı: " + problem + ".");
                    MessageBox.Show(Owner, "Ayarlar yazıldı ama doğrulanamadı (" + problem + "). “Cihazdan Oku” ile kontrol edin.", "Diğer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<string> diffs = Diff(target, back);
                saved = DigerStore.Load();
                saved.Previous = cur;
                saved.Current = back;
                DigerStore.Save(saved);
                Populate(back);
                _status.Text = "Cihazdan geri okundu: " + Describe(back);

                if (diffs.Count == 0)
                {
                    _log("Diğer ayarlar doğrulandı: " + Describe(back));
                    MessageBox.Show(Owner, "Genel ayarlar yazıldı ve doğrulandı.", "Diğer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    foreach (string d in diffs) _log("Diğer ayarlar FARK (yazılan → okunan): " + d);
                    MessageBox.Show(Owner, "Ayarlar yazıldı ama cihazdan okunan değerler farklı:\n\n" + string.Join("\n", diffs.ToArray()), "Diğer — doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }
    }
}
