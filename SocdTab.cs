using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    public class SocdBackup
    {
        public string Created;
        public bool On;
        public List<SocdRule> Rules = new List<SocdRule>();
    }

    // SOCD sekmesi: kuralları okur, düzenler ve cihaza yazar (PROTOKOL.md alt komut 0x01, 0x02, 0x00, 0x03).
    // Kurallar cihaz geneli olduğu için profillerden ayrı, socd.json içinde saklanır.
    public class SocdTab
    {
        private static readonly Color Bg = Theme.Bg;
        private static readonly Color PanelBg = Theme.Panel;
        private static readonly Color TxtClr = Theme.Text;
        private static readonly Color Muted = Theme.Muted;
        private static readonly Color Accent = Theme.Accent;
        private static readonly Color Green = Theme.Green;
        private static readonly Color Red = Theme.Red;

        private static readonly string[] ModelNames = new string[]
        {
            "Model 1 — son basılan kazanır",
            "Model 2 — Tuş 1 öncelikli",
            "Model 3 — Tuş 2 öncelikli",
            "Model 4 — ilk basılan kalır"
        };

        private readonly Func<bool> _ensureDevice;
        private readonly Func<W75Device> _getDevice;
        private readonly Action<MethodInvoker> _run;
        private readonly Action<string> _log;

        private List<SocdRule> _rules = new List<SocdRule>();
        private Label _note;
        private Panel _badge;
        private string _badgeText = "SOCD: henüz okunmadı";
        private Color _badgeColor = Theme.Muted;
        private Label _shortcut, _count, _emptyHint, _pickHint, _editorEmpty, _lblModel, _lblArrow;
        private ListBox _cards;
        private Panel _editor;
        private SocdCard _boxKey1, _boxKey2;
        private readonly SocdCard[] _modelCards = new SocdCard[4];
        private KeyboardPanel _kb;
        private TableLayoutPanel _root;
        private TabPage _page;
        private int _pick;          // 0 = kapalı, 1/2 = Tuş 1 / Tuş 2 klavyeden seçiliyor
        private bool _loadingUi;
        private bool _compact;      // pencere yüksekliği < 850

        // PROTOKOL.md'deki model açıklamaları.
        private static readonly string[] ModelDescriptions = new string[]
        {
            "Her iki tuşa birlikte basılıyken sonradan basılan tuş geçerli olur; tuşlar birbirini durdurmaz.",
            "Tuş 1, Tuş 2'yi keser (Tuş 1 öncelikli).",
            "Tuş 2, Tuş 1'i keser (Tuş 2 öncelikli).",
            "Sonradan basılan çalışmaz, ilk basılan kalır."
        };

        public SocdTab(Func<bool> ensureDevice, Func<W75Device> getDevice, Action<MethodInvoker> run, Action<string> log)
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
            b.FlatAppearance.BorderColor = Theme.Border;
            b.BackColor = accent ? Accent : Theme.Raised;
            b.ForeColor = accent ? Theme.AccentText : TxtClr;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        public TabPage Build()
        {
            TabPage page = new TabPage("SOCD");
            _page = page;
            page.BackColor = Bg;
            page.ForeColor = TxtClr;
            page.Padding = new Padding(0);

            _root = new TableLayoutPanel();
            _root.Dock = DockStyle.Fill;
            _root.ColumnCount = 1;
            _root.RowCount = 4;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // üstteki not
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 290));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.Padding = new Padding(12);
            page.Controls.Add(_root);

            // ---------------- üst çubuk ----------------
            Panel top = new Panel();
            top.Dock = DockStyle.Fill;
            top.BackColor = PanelBg;
            top.Margin = new Padding(0, 0, 0, 10);
            _note = new Label();
            _note.Dock = DockStyle.Fill;
            _note.ForeColor = Muted;
            _note.Font = new Font("Segoe UI", 8.8f);
            _note.TextAlign = ContentAlignment.MiddleLeft;
            _note.Padding = new Padding(4, 0, 0, 0);
            _note.Text = NoteText("Fn", "R-Shift");
            _root.Controls.Add(_note, 0, 0);

            _root.Controls.Add(top, 0, 1);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Right;
            flow.Width = 440;
            flow.FlowDirection = FlowDirection.RightToLeft;
            flow.WrapContents = false;
            flow.Padding = new Padding(0, 15, 12, 0);
            flow.BackColor = PanelBg;
            top.Controls.Add(flow);

            Button read = MakeBtn("Cihazdan Oku", 0, 0, 130, false);
            Button write = MakeBtn("Cihaza Yaz", 0, 0, 120, true);
            Button power = MakeBtn("SOCD Aç/Kapa", 0, 0, 130, false);
            read.Margin = new Padding(8, 0, 0, 0);
            write.Margin = new Padding(8, 0, 0, 0);
            power.Margin = new Padding(8, 0, 0, 0);
            read.Click += delegate { ReadFromDevice(); };
            write.Click += delegate { WriteToDevice(); };
            power.Click += delegate { TogglePower(); };
            flow.Controls.Add(power);
            flow.Controls.Add(write);
            flow.Controls.Add(read);

            _badge = new Panel();
            _badge.Bounds = new Rectangle(16, 16, 184, 32);
            _badge.BackColor = PanelBg;
            _badge.Paint += delegate(object s, PaintEventArgs pe)
            {
                Graphics g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                RectangleF r = new RectangleF(1, 1, _badge.Width - 3, _badge.Height - 3);
                using (GraphicsPath path = RoundRect(r, 15f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(44, _badgeColor))) g.FillPath(b, path);
                    using (Pen p = new Pen(_badgeColor, 1.6f)) g.DrawPath(p, path);
                }
                StringFormat f = new StringFormat();
                f.Alignment = StringAlignment.Center;
                f.LineAlignment = StringAlignment.Center;
                using (Font font = new Font("Segoe UI Semibold", 10f))
                using (SolidBrush tb = new SolidBrush(_badgeColor))
                    g.DrawString(_badgeText, font, tb, new RectangleF(0, 0, _badge.Width, _badge.Height - 1), f);
            };
            top.Controls.Add(_badge);

            _shortcut = new Label();
            _shortcut.Text = "Kısayol: okunmadı";
            _shortcut.Font = new Font("Segoe UI", 9.5f);
            _shortcut.ForeColor = Muted;
            _shortcut.AutoSize = true;
            _shortcut.Location = new Point(216, 12);
            top.Controls.Add(_shortcut);

            _count = new Label();
            _count.Text = "Kurallar: 0/20";
            _count.Font = new Font("Segoe UI Semibold", 9.5f);
            _count.ForeColor = TxtClr;
            _count.AutoSize = true;
            _count.Location = new Point(216, 36);
            top.Controls.Add(_count);

            ToolTip tip = new ToolTip();
            tip.SetToolTip(_count, "Kurallar cihaz geneldir; profillerden bağımsız olarak socd.json dosyasında saklanır. " +
                                   "Ekleme/silme/düzenleme yalnızca bu listeyi değiştirir; klavyeye “Cihaza Yaz” ile yazılır. Kısayol yalnızca gösterilir.");

            // ---------------- orta bölüm: sol kart listesi, sağ düzenleyici ----------------
            TableLayoutPanel mid = new TableLayoutPanel();
            mid.Dock = DockStyle.Fill;
            mid.ColumnCount = 2;
            mid.RowCount = 1;
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mid.Margin = new Padding(0, 0, 0, 10);
            _root.Controls.Add(mid, 0, 2);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = PanelBg;
            left.Padding = new Padding(12, 10, 12, 10);
            left.Margin = new Padding(0, 0, 10, 0);
            mid.Controls.Add(left, 0, 0);

            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.BackColor = PanelBg;

            _cards = new ListBox();
            _cards.Dock = DockStyle.Fill;
            _cards.BorderStyle = BorderStyle.None;
            _cards.BackColor = PanelBg;
            _cards.ForeColor = TxtClr;
            _cards.DrawMode = DrawMode.OwnerDrawFixed;
            _cards.ItemHeight = 58;
            _cards.IntegralHeight = false;
            _cards.DrawItem += Cards_DrawItem;
            _cards.SelectedIndexChanged += delegate
            {
                if (_loadingUi) return;
                _pick = 0;
                LoadEditor();
                if (_kb != null) _kb.Invalidate();
            };

            _emptyHint = new Label();
            _emptyHint.Dock = DockStyle.Fill;
            _emptyHint.Text = "Henüz kural yok.\r\n“Kural ekle” ile başlayın.";
            _emptyHint.ForeColor = Muted;
            _emptyHint.Font = new Font("Segoe UI", 10f);
            _emptyHint.TextAlign = ContentAlignment.MiddleCenter;
            holder.Controls.Add(_cards);
            holder.Controls.Add(_emptyHint);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 44;
            bottom.BackColor = PanelBg;
            Button add = MakeBtn("Kural ekle", 0, 8, 140, false);
            Button del = MakeBtn("Kuralı sil", 148, 8, 120, false);
            add.Click += delegate { AddRule(); };
            del.Click += delegate { DeleteRule(); };
            bottom.Controls.Add(add);
            bottom.Controls.Add(del);

            Label lh = new Label();
            lh.Text = "KURALLAR";
            lh.Dock = DockStyle.Top;
            lh.Height = 22;
            lh.ForeColor = Muted;
            lh.Font = new Font("Segoe UI Semibold", 8f);

            left.Controls.Add(holder);
            left.Controls.Add(bottom);
            left.Controls.Add(lh);

            // ---------------- sağ: seçili kuralın düzenleyicisi ----------------
            _editor = new Panel();
            _editor.Dock = DockStyle.Fill;
            _editor.BackColor = PanelBg;
            _editor.Margin = new Padding(0);
            mid.Controls.Add(_editor, 1, 0);

            Label et = new Label();
            et.Text = "SEÇİLİ KURAL";
            et.ForeColor = Muted;
            et.Font = new Font("Segoe UI Semibold", 8f);
            et.AutoSize = true;
            et.Location = new Point(16, 10);
            _editor.Controls.Add(et);

            _boxKey1 = new SocdCard();
            _boxKey1.SubtitleFirst = true;
            _boxKey1.Subtitle = "TUŞ 1";
            _boxKey1.TitleSize = 15f;
            _boxKey1.Click += delegate { BeginPick(1); };
            _boxKey2 = new SocdCard();
            _boxKey2.SubtitleFirst = true;
            _boxKey2.Subtitle = "TUŞ 2";
            _boxKey2.TitleSize = 15f;
            _boxKey2.Click += delegate { BeginPick(2); };
            _lblArrow = new Label();
            _lblArrow.Text = "↔";
            _lblArrow.ForeColor = Muted;
            _lblArrow.Font = new Font("Segoe UI", 16f);
            _lblArrow.AutoSize = true;
            _pickHint = new Label();
            _pickHint.ForeColor = Muted;
            _pickHint.Font = new Font("Segoe UI", 8.5f);
            _lblModel = new Label();
            _lblModel.Text = "MODEL";
            _lblModel.ForeColor = Muted;
            _lblModel.Font = new Font("Segoe UI Semibold", 8f);
            _lblModel.AutoSize = true;
            _editor.Controls.Add(_boxKey1);
            _editor.Controls.Add(_boxKey2);
            _editor.Controls.Add(_lblArrow);
            _editor.Controls.Add(_pickHint);
            _editor.Controls.Add(_lblModel);
            for (int i = 0; i < 4; i++)
            {
                int model = i;
                SocdCard c = new SocdCard();
                c.Title = ModelName(i);
                c.Subtitle = ModelDescriptions[i];
                c.TitleSize = 9.5f;
                c.SubtitleSize = 8f;
                c.Click += delegate { SetModel(model); };
                _modelCards[i] = c;
                _editor.Controls.Add(c);
            }
            _editorEmpty = new Label();
            _editorEmpty.ForeColor = Muted;
            _editorEmpty.Font = new Font("Segoe UI", 10f);
            _editorEmpty.AutoSize = true;
            _editorEmpty.Location = new Point(16, 46);
            _editor.Controls.Add(_editorEmpty);
            _editor.Resize += delegate { LayoutEditor(); };

            // ---------------- alt: klavye resmi (tuş seçmek için) ----------------
            _kb = new KeyboardPanel();
            _kb.Dock = DockStyle.Fill;
            _kb.Margin = new Padding(0);
            _kb.SelectedIndex = -1;
            _kb.Caption = KbCaption;
            _kb.KeySelected += delegate { OnKbKey(); };
            _root.Controls.Add(_kb, 0, 3);

            page.Resize += delegate { ApplyCompact(); };

            _rules = SocdStore.Load();
            RefreshList();
            LayoutEditor();
            return page;
        }

        // Tuş yazıları düzeni (TR/US) değişince kart başlıkları, düzenleyici ve klavye yeniden çizilir.
        public void RefreshLabels()
        {
            if (_cards == null) return;
            RefreshList();
            _cards.Invalidate();
            if (_kb != null) _kb.Invalidate();
        }

        // Ayar paketi yüklenince kuralları socd.json'dan yeniden okur (yalnızca yerel; klavyeye bir şey göndermez).
        public void ReloadFromFile()
        {
            _rules = SocdStore.Load();
            _pick = 0;
            RefreshList(0);
        }

        // Pencere yüksekliği 850'nin altındaysa orta bölüm alçalır; klavye resmi kalan alanı alır.
        private void ApplyCompact()
        {
            Form f = _page != null ? _page.FindForm() : null;
            bool c = f != null && f.WindowState != FormWindowState.Minimized && f.Height < 850;
            if (c != _compact)
            {
                _compact = c;
                _root.RowStyles[2].Height = c ? 250 : 290;
            }
            LayoutEditor();
        }

        private void LayoutEditor()
        {
            if (_editor == null || _boxKey1 == null) return;
            int w = _editor.ClientSize.Width;
            int pad = 16;
            int keyY = _compact ? 32 : 40;
            int keyH = _compact ? 50 : 60;
            int keyW = Math.Max(110, Math.Min(200, (w - 2 * pad - 44) / 2));
            _boxKey1.SetBounds(pad, keyY, keyW, keyH);
            _lblArrow.Location = new Point(pad + keyW + 8, keyY + (keyH - _lblArrow.Height) / 2);
            _boxKey2.SetBounds(pad + keyW + 44, keyY, keyW, keyH);

            int hintY = keyY + keyH + 4;
            _pickHint.SetBounds(pad, hintY, Math.Max(10, w - 2 * pad), 18);
            int modelLblY = hintY + 22;
            _lblModel.Location = new Point(pad, modelLblY);
            int cardsY = modelLblY + 20;
            int cardH = _compact ? 44 : 54;
            int cardW = Math.Max(120, (w - 2 * pad - 8) / 2);
            for (int i = 0; i < 4; i++)
                _modelCards[i].SetBounds(pad + (i % 2) * (cardW + 8), cardsY + (i / 2) * (cardH + 6), cardW, cardH);
        }

        private SocdRule Sel
        {
            get
            {
                int i = _cards != null ? _cards.SelectedIndex : -1;
                return i >= 0 && i < _rules.Count ? _rules[i] : null;
            }
        }

        private void RefreshList()
        {
            RefreshList(_cards.SelectedIndex);
        }

        private void RefreshList(int select)
        {
            _loadingUi = true;
            _cards.BeginUpdate();
            _cards.Items.Clear();
            foreach (SocdRule r in _rules) _cards.Items.Add(r);
            if (_rules.Count > 0)
            {
                if (select < 0) select = 0;
                if (select >= _rules.Count) select = _rules.Count - 1;
                _cards.SelectedIndex = select;
            }
            _cards.EndUpdate();
            _loadingUi = false;

            _count.Text = "Kurallar: " + _rules.Count + "/20";
            _emptyHint.Visible = _rules.Count == 0;
            _cards.Visible = _rules.Count > 0;
            LoadEditor();
            if (_kb != null) _kb.Invalidate();
        }

        private static string KeyOrQ(int hid)
        {
            return hid == 0 ? "?" : KeyName(hid);
        }

        private static string ShortModel(int model)
        {
            string n = ModelName(model);
            int i = n.IndexOf("— ");
            if (i >= 0) n = n.Substring(i + 2);
            return n.Length > 0 ? char.ToUpper(n[0]) + n.Substring(1) : n;
        }

        private static string RuleTitle(SocdRule r)
        {
            return KeyOrQ(r.Key1) + " ↔ " + KeyOrQ(r.Key2);
        }

        private void Cards_DrawItem(object sender, DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(_cards.BackColor)) g.FillRectangle(bg, e.Bounds);
            if (e.Index < 0 || e.Index >= _rules.Count) return;
            SocdRule r = _rules[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF c = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 3, e.Bounds.Width - 6, e.Bounds.Height - 6);
            using (GraphicsPath path = RoundRect(c, 8f))
            {
                using (SolidBrush b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Raised)) g.FillPath(b, path);
                using (Pen p = new Pen(sel ? Accent : Theme.Border, sel ? 2f : 1.2f)) g.DrawPath(p, path);
            }
            bool incomplete = r.Key1 == 0 || r.Key2 == 0;
            using (Font tf = new Font("Segoe UI Semibold", 13f))
            using (Font sf = new Font("Segoe UI", 8.5f))
            using (SolidBrush tb = new SolidBrush(Theme.Text))
            using (SolidBrush sb = new SolidBrush(incomplete ? Red : Muted))
            using (SolidBrush nb = new SolidBrush(Theme.Muted))
            {
                g.DrawString(RuleTitle(r), tf, tb, new PointF(c.X + 12, c.Y + 4));
                g.DrawString(incomplete ? "Tuş seçimi eksik" : ShortModel(r.Model), sf, sb, new PointF(c.X + 13, c.Y + 29));
                StringFormat rf = new StringFormat();
                rf.Alignment = StringAlignment.Far;
                g.DrawString("#" + (e.Index + 1), sf, nb, new RectangleF(c.X, c.Y + 6, c.Width - 10, 16), rf);
            }
        }

        // Seçili kuralın düzenleyicisini günceller.
        private void LoadEditor()
        {
            if (_editor == null) return;
            SocdRule r = Sel;
            bool has = r != null;
            _boxKey1.Visible = _boxKey2.Visible = _lblArrow.Visible = _pickHint.Visible = _lblModel.Visible = has;
            foreach (SocdCard mc in _modelCards) mc.Visible = has;
            _editorEmpty.Visible = !has;
            _editorEmpty.Text = _rules.Count == 0 ? "Kural yok. “Kural ekle” ile başlayın." : "Soldan bir kural seçin.";
            if (!has) return;

            _boxKey1.Title = r.Key1 == 0 ? "Tuş seçin" : KeyName(r.Key1);
            _boxKey2.Title = r.Key2 == 0 ? "Tuş seçin" : KeyName(r.Key2);
            _boxKey1.Active = _pick == 1;
            _boxKey2.Active = _pick == 2;
            _boxKey1.Invalidate();
            _boxKey2.Invalidate();
            for (int i = 0; i < 4; i++)
            {
                _modelCards[i].Selected = r.Model == i;
                _modelCards[i].Invalidate();
            }
            _pickHint.Text = _pick == 0
                ? "Bir tuşu değiştirmek için kutusuna, sonra aşağıdaki klavyeden bir tuşa tıklayın."
                : "Klavyeden Tuş " + _pick + " için bir tuşa tıklayın… (vazgeçmek için kutuya tekrar tıklayın)";
        }

        private void BeginPick(int n)
        {
            if (Sel == null) return;
            _pick = _pick == n ? 0 : n;
            LoadEditor();
        }

        private KeyCaption KbCaption(KeyDef k)
        {
            SocdRule r = Sel;
            if (r == null || k.HidCode == 0 || k.Code == "RotaryKnob" || k.Code == "KeyFn") return null;
            KeyCaption c = new KeyCaption();
            c.Highlight = true;
            if (k.HidCode == r.Key1) { c.Text = "T1"; return c; }
            if (k.HidCode == r.Key2) { c.Text = "T2"; return c; }
            return null;
        }

        // Klavye resminde tıklanan tuşu, seçili kuralın Tuş 1 / Tuş 2 kutusuna yazar.
        private void OnKbKey()
        {
            SocdRule r = Sel;
            if (r == null)
            {
                _pickHint.Text = "Önce bir kural seçin ya da “Kural ekle”ye basın.";
                return;
            }
            if (_pick == 0)
            {
                _pickHint.Visible = true;
                _pickHint.Text = "Önce Tuş 1 ya da Tuş 2 kutusuna tıklayın, sonra klavyeden tuşu seçin.";
                return;
            }
            KeyDef k = KeyboardLayout.Get(_kb.SelectedIndex);
            if (k == null || k.HidCode == 0 || k.Code == "RotaryKnob" || k.Code == "KeyFn")
            {
                _pickHint.Text = "Bu tuş SOCD için seçilemez. Başka bir tuş seçin.";
                return;
            }
            int hid = k.HidCode;
            string name = KeyName(hid);
            int other = _pick == 1 ? r.Key2 : r.Key1;
            if (hid == other)
            {
                MessageBox.Show(Owner, "Tuş 1 ile Tuş 2 aynı olamaz (" + name + ").", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            for (int j = 0; j < _rules.Count; j++)
            {
                SocdRule o = _rules[j];
                if (o == r) continue;
                if (o.Key1 == hid || o.Key2 == hid)
                {
                    MessageBox.Show(Owner, "Aynı tuş iki farklı kuralda geçemez: " + name + " zaten " + (j + 1) + ". kuralda kullanılıyor.",
                        "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            if (_pick == 1) r.Key1 = hid; else r.Key2 = hid;
            SocdStore.Save(_rules);
            _log("SOCD kuralı güncellendi (henüz cihaza yazılmadı): Tuş " + _pick + " = " + name + " → " + RuleTitle(r));
            // Boş kalan diğer kutuya otomatik geç.
            if (_pick == 1 && r.Key2 == 0) _pick = 2;
            else if (_pick == 2 && r.Key1 == 0) _pick = 1;
            else _pick = 0;
            RefreshList();
        }

        private void SetModel(int model)
        {
            SocdRule r = Sel;
            if (r == null || r.Model == model) return;
            r.Model = model;
            SocdStore.Save(_rules);
            _log("SOCD kural modeli değişti (henüz cihaza yazılmadı): " + RuleTitle(r) + " → " + ModelName(model));
            RefreshList();
        }

        private void ShowPower(SocdPower power)
        {
            _badgeText = "SOCD: " + (power.On ? "Açık" : "Kapalı");
            _badgeColor = power.On ? Green : Red;
            _badge.Invalidate();
            _shortcut.Text = "Kısayol: " + ShortcutName(power.Shortcut1) + " + " + ShortcutName(power.Shortcut2);
            _note.Text = NoteText(ShortcutName(power.Shortcut1), ShortcutName(power.Shortcut2)); // okunan gerçek kısayol
        }

        private static string NoteText(string key1, string key2)
        {
            return "Oyun sırasında " + key1 + " + " + key2 + " SOCD'yi açıp kapatır; kapatırsanız kurallar çalışmaz.";
        }

        private IWin32Window Owner { get { return _page != null ? _page.FindForm() : null; } }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static bool SameRules(List<SocdRule> a, List<SocdRule> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Model != b[i].Model || a[i].Key1 != b[i].Key1 || a[i].Key2 != b[i].Key2) return false;
            return true;
        }

        private static string RuleText(SocdRule r)
        {
            return KeyName(r.Key1) + " + " + KeyName(r.Key2) + ", " + ModelName(r.Model).Split(' ')[0] + " " + ModelName(r.Model).Split(' ')[1];
        }

        // ---------------- okuma ----------------

        private void ReadFromDevice()
        {
            if (!_ensureDevice()) return;
            _run(delegate
            {
                W75Device dev = _getDevice();
                _log("SOCD okuma başladı.");
                SocdPower power = dev.ReadSocdPower();
                List<SocdRule> rules = dev.ReadSocdRules();
                ShowPower(power);

                if (_rules.Count > 0 && !SameRules(_rules, rules))
                {
                    if (MessageBox.Show(Owner, "Listedeki kurallar cihazdakinden farklı. Cihazdaki kurallar listenin üzerine yazılsın mı?\n(Listedeki değişiklikler kaybolur.)",
                        "SOCD", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        _log("SOCD okundu ama liste değiştirilmedi.");
                        return;
                    }
                }
                _rules = rules;
                SocdStore.Save(_rules);
                RefreshList();
                _log(string.Format("SOCD okundu: {0}, {1} kural.", power.On ? "açık" : "kapalı", rules.Count));
                if (rules.Count == 0)
                    MessageBox.Show(Owner, "Klavyede tanımlı SOCD kuralı yok.", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        // ---------------- düzenleme (yalnızca yerel liste) ----------------

        // Yeni boş bir kural ekler; tuşlar sağdaki düzenleyiciden klavyeye tıklanarak seçilir.
        // Tuşlar seçilmeden “Cihaza Yaz” kuralları doğrulayıp yazmayı reddeder.
        private void AddRule()
        {
            if (_rules.Count >= 20)
            {
                MessageBox.Show(Owner, "En fazla 20 SOCD kuralı olabilir.", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SocdRule r = new SocdRule();
            r.Model = 0;
            _rules.Add(r);
            SocdStore.Save(_rules);
            _pick = 1;
            RefreshList(_rules.Count - 1);
            _log("SOCD'ye yeni boş kural eklendi (tuşlar seçilene kadar cihaza yazılamaz).");
        }

        private void DeleteRule()
        {
            SocdRule r = Sel;
            if (r == null)
            {
                MessageBox.Show(Owner, "Silmek için listeden bir kural seçin.", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int i = _cards.SelectedIndex;
            if (MessageBox.Show(Owner, "“" + RuleTitle(r) + "” kuralı silinsin mi?\n\nSilme yalnızca listeyi değiştirir; klavyeye “Cihaza Yaz” ile yazılır.",
                "SOCD kuralını sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _rules.RemoveAt(i);
            SocdStore.Save(_rules);
            _pick = 0;
            RefreshList(Math.Min(i, _rules.Count - 1));
            _log("SOCD kuralı silindi (henüz cihaza yazılmadı): " + RuleTitle(r));
        }

        // ---------------- yazma ----------------

        private void WriteToDevice()
        {
            string err = SocdStore.Validate(_rules);
            if (err != null)
            {
                MessageBox.Show(Owner, err + "\n\nHiçbir şey yazılmadı.", "SOCD kuralları geçersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!_ensureDevice()) return;
            _run(delegate
            {
                W75Device dev = _getDevice();

                // 1) mevcut cihaz kurallarını oku ve yedekle
                SocdPower power = dev.ReadSocdPower();
                List<SocdRule> before = dev.ReadSocdRules();
                SocdBackup b = new SocdBackup();
                b.Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                b.On = power.On;
                b.Rules = before;
                string path = Path.Combine(Store.BackupDir, "socd-yedek-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                File.WriteAllText(path, new JavaScriptSerializer().Serialize(b), new UTF8Encoding(false));
                _log("SOCD yedeği alındı: " + path);

                // 2) onay
                if (MessageBox.Show(Owner, _rules.Count + " kural yazılacak, devam edilsin mi?\n\nÖnce cihazdaki " + before.Count +
                    " kural şuraya yedeklendi:\n" + path + "\n\nYazma sırasında USB kablosunu çıkarmayın.",
                    "SOCD Cihaza Yaz", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    _log("SOCD yazma iptal edildi.");
                    return;
                }

                // 3) yaz (her zaman iki sayfa), geri oku, karşılaştır
                _log("SOCD yazma başladı: " + _rules.Count + " kural.");
                dev.WriteSocdRules(_rules);
                System.Threading.Thread.Sleep(150);
                List<SocdRule> after = dev.ReadSocdRules();

                List<string> diffs = new List<string>();
                int n = Math.Max(_rules.Count, after.Count);
                for (int i = 0; i < n; i++)
                {
                    SocdRule want = i < _rules.Count ? _rules[i] : null;
                    SocdRule got = i < after.Count ? after[i] : null;
                    if (want != null && got != null && want.Model == got.Model && want.Key1 == got.Key1 && want.Key2 == got.Key2) continue;
                    diffs.Add((i + 1) + ". kural: beklenen " + (want == null ? "(boş)" : RuleText(want)) +
                              ", cihazda " + (got == null ? "(boş)" : RuleText(got)));
                }
                if (diffs.Count == 0)
                {
                    _log("SOCD doğrulandı: cihazdan okunan kurallar yazılanlarla aynı.");
                    MessageBox.Show(Owner, "SOCD kuralları yazıldı ve doğrulandı.", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    foreach (string d in diffs) _log("SOCD FARK — " + d);
                    MessageBox.Show(Owner, "Kurallar yazıldı ama cihazdan okunan değerler farklı:\n\n" + string.Join("\n", diffs.ToArray()) +
                        "\n\nAyrıntı için Cihaz sekmesindeki günlüğe bakın.", "SOCD doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        private void TogglePower()
        {
            if (!_ensureDevice()) return;
            _run(delegate
            {
                W75Device dev = _getDevice();
                SocdPower cur = dev.ReadSocdPower();
                ShowPower(cur);
                bool target = !cur.On;
                if (MessageBox.Show(Owner, "SOCD şu an " + (cur.On ? "AÇIK" : "KAPALI") + ". " + (target ? "AÇILSIN" : "KAPATILSIN") + " mı?",
                    "SOCD Aç/Kapa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                dev.WriteSocdPower(target);
                SocdPower now = dev.ReadSocdPower();
                ShowPower(now);
                if (now.On == target)
                {
                    _log("SOCD " + (target ? "açıldı" : "kapatıldı") + " ve doğrulandı.");
                    MessageBox.Show(Owner, "SOCD " + (target ? "açıldı" : "kapatıldı") + " (doğrulandı).", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _log("SOCD UYARI: durum doğrulanamadı (beklenen " + (target ? "açık" : "kapalı") + ", cihaz " + (now.On ? "açık" : "kapalı") + ").");
                    MessageBox.Show(Owner, "Komut gönderildi ama cihaz beklenen durumu göstermiyor.", "SOCD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        // ---------------- adlar ----------------

        // Kısayol yazısı: 0xFA = Fn, 0xE5 = R-Shift (PROTOKOL.md'deki cihaz kodları).
        private static string ShortcutName(int hid)
        {
            if (hid == 0xFA) return "Fn";
            if (hid == 0xE5) return "R-Shift";
            return "Bilinmeyen (0x" + hid.ToString("X2") + ")";
        }

        // HID kodundan tuş adı (layout-w75.json içindeki hidCode alanları).
        public static string KeyName(int hid)
        {
            KeyboardLayout.Load();
            foreach (KeyDef k in KeyboardLayout.Keys)
                if (k.HidCode == hid && k.Code != "RotaryKnob" && k.Code != "KeyFn")
                    return KeyLabels.Name(k); // seçili tuş yazısı düzenine (TR/US) uyar
            return "Bilinmeyen (0x" + hid.ToString("X2") + ")";
        }

        public static string ModelName(int model)
        {
            if (model >= 0 && model < ModelNames.Length) return ModelNames[model];
            return "Bilinmeyen model (" + model + ")";
        }

        public static string[] AllModelNames { get { return ModelNames; } }
    }

    // "Kural Ekle" penceresi: Tuş 1, Tuş 2 ve model seçimi.
    public class AddRuleForm : Form
    {
        private class KeyItem
        {
            public int Hid;
            public string Name;
            public override string ToString() { return Name; }
        }

        private ComboBox _k1, _k2, _model;
        private List<SocdRule> _existing;
        public SocdRule Result;

        public static SocdRule Show(IWin32Window owner, List<SocdRule> existing)
        {
            using (AddRuleForm f = new AddRuleForm(existing))
                return f.ShowDialog(owner) == DialogResult.OK ? f.Result : null;
        }

        private AddRuleForm(List<SocdRule> existing)
        {
            _existing = existing;
            AppIcon.Apply(this); WindowTheme.Setup(this);
            Text = "SOCD kuralı ekle";
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 230);
            Font = new Font("Segoe UI", 9f);

            _k1 = MakeKeyCombo(20, 34, "Tuş 1");
            _k2 = MakeKeyCombo(220, 34, "Tuş 2");

            Label ml = new Label();
            ml.Text = "Model";
            ml.ForeColor = Theme.Muted;
            ml.AutoSize = true;
            ml.Location = new Point(20, 76);
            Controls.Add(ml);

            _model = new ComboBox();
            _model.DropDownStyle = ComboBoxStyle.DropDownList;
            _model.Bounds = new Rectangle(20, 96, 380, 28);
            foreach (string m in SocdTab.AllModelNames) _model.Items.Add(m);
            _model.SelectedIndex = 0;
            Controls.Add(_model);

            Label hint = new Label();
            hint.Text = "İki tuş birlikte basılıyken davranış. Karşılıklı yönleri durdurmak için Model 1 kullanılır.";
            hint.ForeColor = Theme.Muted;
            hint.Font = new Font("Segoe UI", 8.2f);
            hint.Bounds = new Rectangle(20, 132, 380, 34);
            Controls.Add(hint);

            Button ok = new Button();
            ok.Text = "Ekle";
            ok.Bounds = new Rectangle(236, 180, 78, 30);
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Theme.AccentText;
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += delegate { TryAccept(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Vazgeç";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Bounds = new Rectangle(322, 180, 78, 30);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Theme.Raised;
            cancel.ForeColor = Theme.Text;
            cancel.FlatAppearance.BorderColor = Theme.Border;
            Controls.Add(cancel);
            CancelButton = cancel;
        }

        private ComboBox MakeKeyCombo(int x, int y, string caption)
        {
            Label l = new Label();
            l.Text = caption;
            l.ForeColor = Theme.Muted;
            l.AutoSize = true;
            l.Location = new Point(x, y - 20);
            Controls.Add(l);

            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Bounds = new Rectangle(x, y, 180, 28);
            KeyItem none = new KeyItem();
            none.Hid = 0; none.Name = "(tuş seçin)";
            c.Items.Add(none);
            KeyboardLayout.Load();
            Dictionary<int, bool> seen = new Dictionary<int, bool>();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                if (k.HidCode == 0 || k.Code == "RotaryKnob" || k.Code == "KeyFn") continue;
                if (seen.ContainsKey(k.HidCode)) continue;
                seen[k.HidCode] = true;
                KeyItem it = new KeyItem();
                it.Hid = k.HidCode;
                it.Name = SocdTab.KeyName(k.HidCode);
                c.Items.Add(it);
            }
            c.SelectedIndex = 0;
            Controls.Add(c);
            return c;
        }

        private void TryAccept()
        {
            KeyItem a = _k1.SelectedItem as KeyItem;
            KeyItem b = _k2.SelectedItem as KeyItem;
            SocdRule r = new SocdRule();
            r.Key1 = a == null ? 0 : a.Hid;
            r.Key2 = b == null ? 0 : b.Hid;
            r.Model = _model.SelectedIndex < 0 ? 0 : _model.SelectedIndex;

            List<SocdRule> test = new List<SocdRule>(_existing);
            test.Add(r);
            string err = SocdStore.Validate(test);
            if (err != null)
            {
                MessageBox.Show(this, err, "SOCD kuralı eklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Result = r;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
