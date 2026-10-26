using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Aktivasyon > Mesafe / Ölü Bölge sayfalarındaki değerli klavye resimleri ve çoklu seçim.
    // Seçimin tek kaynağı tuş tablosudur (_grid.SelectedRows); klavye resimleri onu yansıtır.
    public partial class MainForm
    {
        private KeyboardPanel _kbMesafe, _kbDead;
        private List<int> _multiSel = new List<int>(); // seçim sırası (ilk eleman = ilk seçilen)
        private bool _syncing;
        private bool _showList;
        private readonly List<TableLayoutPanel> _listRoots = new List<TableLayoutPanel>();
        private readonly List<Button> _selButtons = new List<Button>();

        private const int ListRowHeight = 230;

        // "Seçili tuşlara" düğmeleri, hiç tuş seçili değilse pasif olur.
        private void UpdateSelButtons()
        {
            bool has = _grid != null && _grid.SelectedRows.Count > 0;
            foreach (Button b in _selButtons) b.Enabled = has;
        }

        private string MmLocal(int raw)
        {
            return (raw * Unit).ToString("0.00", CultureInfo.CurrentCulture);
        }

        private KeyCaption TravelCaption(KeyDef k)
        {
            Profile p = Current;
            TriggerSetting t = p.EffectiveTrigger(k.Index);
            KeyCaption c = new KeyCaption();
            string s = MmLocal(t.Travel);
            if (t.Mode == 12 || t.Mode == 13) s += "\n" + MmLocal(t.Interval1) + " " + MmLocal(t.Interval2);
            c.Text = s;
            c.Highlight = t.Mode != p.DefaultMode || t.Travel != p.DefaultTravel ||
                          t.Interval1 != p.DefaultI1 || t.Interval2 != p.DefaultI2;
            return c;
        }

        private KeyCaption DeadCaption(KeyDef k)
        {
            TriggerSetting t = Current.EffectiveTrigger(k.Index);
            KeyCaption c = new KeyCaption();
            c.Text = MmLocal(t.DeadTop) + " " + MmLocal(t.DeadBottom);
            c.Highlight = t.DeadTop != 0 || t.DeadBottom != 0;
            return c;
        }

        private void RefreshValueKeyboards()
        {
            if (_kbMesafe != null) _kbMesafe.Invalidate();
            if (_kbDead != null) _kbDead.Invalidate();
        }

        private void PushSelectionToPanels()
        {
            if (_kbMesafe != null) { _kbMesafe.SelectedList = new List<int>(_multiSel); _kbMesafe.Invalidate(); }
            if (_kbDead != null) { _kbDead.SelectedList = new List<int>(_multiSel); _kbDead.Invalidate(); }
        }

        // Tablodaki seçili satırlardan klavye resimlerinin seçimini kurar (eski seçim sırası korunur).
        private void SyncPanelsFromGrid()
        {
            List<int> cur = new List<int>();
            foreach (DataGridViewRow r in _grid.SelectedRows) cur.Add((int)r.Tag);
            List<int> ordered = new List<int>();
            foreach (int i in _multiSel) if (cur.Contains(i)) ordered.Add(i);
            cur.Sort();
            foreach (int i in cur) if (!ordered.Contains(i)) ordered.Add(i);
            _multiSel = ordered;
            PushSelectionToPanels();
        }

        private void OnValueKbSelection(object sender, EventArgs e)
        {
            KeyboardPanel src = (KeyboardPanel)sender;
            ApplySelectionFromList(new List<int>(src.SelectedList));
        }

        // Verilen tuşları seçer: tabloyu, iki klavye resmini ve alanları (ilk seçilen tuşun değerleri) eşler.
        private void ApplySelectionFromList(List<int> list)
        {
            _multiSel = list;
            DataGridViewRow firstRow = null;
            _syncing = true;
            try
            {
                _grid.ClearSelection();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    int idx = (int)row.Tag;
                    if (!list.Contains(idx)) continue;
                    row.Selected = true;
                    if (list.Count > 0 && idx == list[0]) firstRow = row;
                }
            }
            finally { _syncing = false; }

            PushSelectionToPanels();
            UpdateSelButtons();
            if (firstRow != null)
            {
                LoadTriggerFieldsFromRow(firstRow);
                if (list.Count == 1)
                {
                    _keyboard.SelectedIndex = list[0];
                    _keyboard.Invalidate();
                    UpdateSelectedKeyInfo();
                    FillActionListInternal();
                }
            }
        }

        private void SelectAllKeys()
        {
            List<int> l = new List<int>();
            foreach (KeyDef k in KeyboardLayout.Keys) l.Add(k.Index);
            ApplySelectionFromList(l);
        }

        private void InvertSelection()
        {
            List<int> l = new List<int>();
            foreach (KeyDef k in KeyboardLayout.Keys) if (!_multiSel.Contains(k.Index)) l.Add(k.Index);
            ApplySelectionFromList(l);
        }

        private void ClearKeySelection()
        {
            ApplySelectionFromList(new List<int>());
        }

        private void ToggleListView()
        {
            _showList = !_showList;
            foreach (TableLayoutPanel r in _listRoots) r.RowStyles[4].Height = _showList ? ListRowHeight : 0;
            foreach (Action a in _topAdjusters) a();
        }

        private KeyboardPanel BuildValueKeyboard(bool deadZone)
        {
            KeyboardPanel kb = new KeyboardPanel();
            kb.Dock = DockStyle.Fill;
            kb.Margin = new Padding(0);
            kb.MultiSelect = true;
            if (deadZone) kb.Caption = DeadCaption;
            else kb.Caption = TravelCaption;
            kb.SelectionListChanged += OnValueKbSelection;
            return kb;
        }

        // ---------------- Mesafe sayfası: görsel ayar alanları ----------------

        private TravelGraph _travelGraph, _deadGraph;
        private readonly List<Action> _topAdjusters = new List<Action>();
        private NumSlider _sliderTravel, _sliderI1, _sliderI2;
        private Panel _rowI1, _rowI2;

        // Cihazın maksimum mesafesi değişince (UpdateDevInfo) kaydırıcı ve grafik yeniden çizilsin.
        private void RefreshTravelVisuals()
        {
            if (_travelGraph != null) _travelGraph.Invalidate();
            if (_deadGraph != null) _deadGraph.Invalidate();
            if (_sliderTravel != null) _sliderTravel.Invalidate();
        }

        // Rapid Trigger kapalıysa basma/bırakma kaydırıcıları pasif ve soluk olur.
        private void UpdateRtEnabled()
        {
            bool on = _rtCombo.SelectedIndex > 0;
            if (_rowI1 != null) _rowI1.Enabled = on;
            if (_rowI2 != null) _rowI2.Enabled = on;
        }

        private static void StepNum(NumericUpDown n, int dir)
        {
            decimal step = n.Increment > 0 ? n.Increment : 0.02m;
            decimal v = n.Value + dir * step;
            if (v < n.Minimum) v = n.Minimum;
            if (v > n.Maximum) v = n.Maximum;
            if (v != n.Value) n.Value = v;
        }

        private static Button SmallStepButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(30, 28);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.BackColor = Theme.Raised;
            b.ForeColor = TxtClr;
            b.Font = new Font("Segoe UI Semibold", 10f);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            return b;
        }

        private const int SliderMaxWidth = 560;

        // Kompakt düzen: pencere yüksekliği 850 pikselin altındaysa satır aralığı 44, grafik en çok 220 piksel.
        private const int CompactBelowHeight = 850;
        private bool _compact;
        private bool _compactHooked;
        private readonly List<Action> _compactActions = new List<Action>(); // kompakt kip değişince çalışır

        private int RowPitch { get { return _compact ? 44 : 56; } } // satırlar arası düzenli aralık

        // Satırları yukarıdan aşağıya eşit aralıkla dizer; genişlik panele uyar, hiçbiri alta bağlanmaz.
        private void StackRows(Panel host, Control[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].SetBounds(0, i * RowPitch, Math.Max(0, host.ClientSize.Width), RowPitch - 2);
            }
        }

        private Panel StackHost(Control[] rows)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = PanelBg;
            foreach (Control c in rows) host.Controls.Add(c);
            host.Resize += delegate { StackRows(host, rows); };
            _compactActions.Add(delegate { StackRows(host, rows); });
            StackRows(host, rows);
            return host;
        }

        // Pencere yüksekliğine göre kompakt kipi açar/kapatır ve ilgili yerleşimleri yeniler.
        private void UpdateCompactMode()
        {
            if (WindowState == FormWindowState.Minimized) return;
            bool c = Height < CompactBelowHeight;
            if (c == _compact) return;
            _compact = c;
            foreach (Action a in _compactActions) a();
            foreach (Action a in _topAdjusters) a();
            RefreshValueKeyboards();
        }

        // Başlık + [kaydırıcı][-][sayı kutusu][+] satırı. Sayı kutusu mevcut NumericUpDown'dur.
        // Kaydırıcı en çok 560 piksel, sola hizalı; sayı kutusu ve -/+ düğmeleri hemen yanında durur.
        private Panel SliderRow(string caption, NumericUpDown num, out NumSlider slider)
        {
            // Satırın yeri (üstten) StackRows ile belirlenir; hiçbir şey alta bağlanmaz.
            Panel p = new Panel();
            p.Height = RowPitch - 2;
            p.BackColor = PanelBg;

            Label l = new Label();
            l.Text = caption;
            l.ForeColor = Muted;
            l.Font = new Font("Segoe UI", 8.5f);
            l.AutoSize = true;
            l.Location = new Point(0, 2);
            p.Controls.Add(l);

            // Kaydırıcı satırı etiketin hemen altında durur.
            Panel row = new Panel();
            row.Location = new Point(0, 22);
            row.Height = 30;
            row.BackColor = PanelBg;
            p.Controls.Add(row);
            Action layoutRow = delegate
            {
                l.Top = _compact ? 0 : 2;
                row.SetBounds(0, _compact ? 15 : 22, p.ClientSize.Width, _compact ? 28 : 30);
            };
            p.Resize += delegate { layoutRow(); };
            _compactActions.Add(layoutRow);

            NumSlider s = new NumSlider(num);
            slider = s;
            Button minus = SmallStepButton("−");
            Button plus = SmallStepButton("+");
            minus.Click += delegate { StepNum(num, -1); };
            plus.Click += delegate { StepNum(num, 1); };
            num.Dock = DockStyle.None;
            num.Width = 78;

            row.Controls.Add(s);
            row.Controls.Add(minus);
            row.Controls.Add(num);
            row.Controls.Add(plus);

            row.Resize += delegate
            {
                int rest = 4 + 30 + 4 + num.Width + 4 + 30; // boşluklar + - + sayı kutusu + +
                int sw = Math.Max(80, Math.Min(SliderMaxWidth, row.Width - rest));
                int y = Math.Max(0, (row.Height - 28) / 2);
                int x = 0;
                s.SetBounds(x, y, sw, 28); x += sw + 4;
                minus.SetBounds(x, y, 30, 28); x += 34;
                num.Location = new Point(x, Math.Max(0, (row.Height - num.Height) / 2)); x += num.Width + 4;
                plus.SetBounds(x, y, 30, 28);
            };

            p.EnabledChanged += delegate { l.ForeColor = p.Enabled ? Muted : Theme.Disabled; };
            return p;
        }

        // Üst alan yüksekliği pencereye göre ayarlanır: klavye resmine en az ~260 piksel kalır (kompaktta ~200),
        // grafik büyük düzende 236–372, kompaktta en çok 220 piksel yüksekliğe uyar. Grafik sütunu da uyar.
        private const int KeyboardMinHeight = 260;
        private const int KeyboardMinHeightCompact = 200;

        private void AdjustTop(TableLayoutPanel root, TableLayoutPanel content)
        {
            int avail = root.ClientSize.Height - root.Padding.Vertical;
            if (avail <= 0) return;
            int h = avail - 40 - 38 - (_showList ? ListRowHeight : 0) - (_compact ? KeyboardMinHeightCompact : KeyboardMinHeight);
            h = _compact ? Math.Max(200, Math.Min(230, h)) : Math.Max(236, Math.Min(372, h));
            if ((int)root.RowStyles[0].Height != h) root.RowStyles[0].Height = h;
            if (content != null && content.ColumnStyles.Count > 0)
            {
                int gw = Math.Max(_compact ? 210 : 250, Math.Min(390, (int)((h - 10) * 1.05)));
                if ((int)content.ColumnStyles[0].Width != gw) content.ColumnStyles[0].Width = gw;
            }
        }

        // Mesafe: sol tuş yolu grafiği; sağ: Rapid Trigger seçimi + uygula düğmeleri, altında kaydırıcılar.
        private Control BuildMesafeTop(Panel applyButtons)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.RowCount = 1;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.BackColor = PanelBg;

            _travelGraph = new TravelGraph(_numTravel, _rtCombo, _numI1, _numI2);
            _travelGraph.Dock = DockStyle.Fill;
            _travelGraph.Margin = new Padding(0, 0, 12, 0);
            t.Controls.Add(_travelGraph, 0, 0);

            // Sıra: Rapid Trigger + düğmeler, Mesafe, Basma, Bırakma (eşit aralıklı, üstten başlar).
            Panel rtRow = new Panel();
            rtRow.BackColor = PanelBg;
            Label rtLbl = new Label();
            rtLbl.Text = "Rapid Trigger";
            rtLbl.ForeColor = Muted;
            rtLbl.Font = new Font("Segoe UI", 8f);
            rtLbl.AutoSize = true;
            _rtCombo.Width = 164;
            rtRow.Controls.Add(rtLbl);
            rtRow.Controls.Add(_rtCombo);
            rtRow.Controls.Add(applyButtons);
            Action layoutRt = delegate
            {
                rtLbl.Location = new Point(0, _compact ? 0 : 2);
                _rtCombo.Location = new Point(0, _compact ? 15 : 19);
                applyButtons.Location = new Point(190, 2);
            };
            layoutRt();
            _compactActions.Add(layoutRt);

            Panel rowTravel = SliderRow("Mesafe (mm)", _numTravel, out _sliderTravel);
            _rowI1 = SliderRow("Basma hassasiyeti", _numI1, out _sliderI1);
            _rowI2 = SliderRow("Bırakma hassasiyeti", _numI2, out _sliderI2);
            Panel r = StackHost(new Control[] { rtRow, rowTravel, _rowI1, _rowI2 });
            t.Controls.Add(r, 1, 0);

            _rtCombo.SelectedIndexChanged += delegate { UpdateRtEnabled(); };
            UpdateRtEnabled();
            return t;
        }

        // Ölü Bölge: sol ölü bölge grafiği; sağ: uygula düğmeleri, altında üst/alt ölü bölge kaydırıcıları (0–1,00 mm).
        private Control BuildDeadTop(Panel applyButtons)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.RowCount = 1;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.BackColor = PanelBg;

            _deadGraph = new TravelGraph(_numTravel, _numDeadTop, _numDeadBottom);
            _deadGraph.Dock = DockStyle.Fill;
            _deadGraph.Margin = new Padding(0, 0, 12, 0);
            t.Controls.Add(_deadGraph, 0, 0);

            // Sıra: düğmeler, Üst ölü bölge, Alt ölü bölge (eşit aralıklı, üstten başlar).
            Panel btnRow = new Panel();
            btnRow.BackColor = PanelBg;
            applyButtons.Location = new Point(0, 2);
            btnRow.Controls.Add(applyButtons);

            NumSlider s1, s2;
            Panel rowTop = SliderRow("Üst ölü bölge (mm)", _numDeadTop, out s1);
            Panel rowBottom = SliderRow("Alt ölü bölge (mm)", _numDeadBottom, out s2);
            Panel r = StackHost(new Control[] { btnRow, rowTop, rowBottom });
            t.Controls.Add(r, 1, 0);
            return t;
        }

        // Mesafe / Ölü Bölge alt sekmeleri: alanlar + uygula düğmeleri, seçim düğmeleri, klavye resmi,
        // "Liste görünümü" düğmesi ve (varsayılan gizli) tuş tablosu için alan. Tablonun yerini döndürür.
        private Panel BuildGridPage(TabPage tp, Panel[] fieldPanels, bool deadZone)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, deadZone ? 124 : 236));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, _showList ? ListRowHeight : 0));
            root.Padding = new Padding(10);
            tp.Controls.Add(root);
            _listRoots.Add(root);

            Panel top = new Panel();
            top.Dock = DockStyle.Fill;
            top.BackColor = PanelBg;
            top.Padding = new Padding(10, 6, 10, 4);
            top.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(top, 0, 0);

            Button all = MakeButton("Tümüne uygula", false);
            all.Width = 150;
            all.Height = 30;
            all.Click += delegate { ApplyGlobal(false, deadZone); };
            Button sel = MakeButton("Seçili tuşlara", true);
            sel.Width = 140;
            sel.Height = 30;
            sel.Click += delegate { ApplyGlobal(true, deadZone); };
            sel.Enabled = _grid != null && _grid.SelectedRows.Count > 0;
            _selButtons.Add(sel);

            Panel ba = new Panel();
            ba.Width = 300;
            ba.Height = 52;
            all.Location = new Point(0, 18);
            sel.Location = new Point(158, 18);
            ba.Controls.Add(all);
            ba.Controls.Add(sel);
            _compactActions.Add(delegate { all.Top = sel.Top = _compact ? 4 : 18; });

            if (!_compactHooked)
            {
                _compactHooked = true;
                SizeChanged += delegate { UpdateCompactMode(); };
                Load += delegate { UpdateCompactMode(); };
            }

            TableLayoutPanel content = (TableLayoutPanel)(deadZone ? BuildDeadTop(ba) : BuildMesafeTop(ba));
            top.Controls.Add(content);
            root.Resize += delegate { AdjustTop(root, content); };
            _topAdjusters.Add(delegate { AdjustTop(root, content); });

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.BackColor = Bg;
            bar.Margin = new Padding(0, 0, 0, 4);
            Button bAll = MakeButton("Tümünü seç", false);
            Button bInv = MakeButton("Tersini seç", false);
            Button bNone = MakeButton("Seçimi bırak", false);
            bAll.Click += delegate { SelectAllKeys(); };
            bInv.Click += delegate { InvertSelection(); };
            bNone.Click += delegate { ClearKeySelection(); };
            bar.Controls.Add(bAll);
            bar.Controls.Add(bInv);
            bar.Controls.Add(bNone);
            Label hint = new Label();
            hint.Text = "Tıkla: seç, tekrar tıkla: kaldır. Turuncu = varsayılandan farklı.";
            hint.ForeColor = Muted;
            hint.Font = new Font("Segoe UI", 8f);
            hint.AutoSize = true;
            hint.Margin = new Padding(12, 9, 0, 0);
            bar.Controls.Add(hint);
            root.Controls.Add(bar, 0, 1);

            KeyboardPanel kb = BuildValueKeyboard(deadZone);
            if (deadZone) _kbDead = kb; else _kbMesafe = kb;
            root.Controls.Add(kb, 0, 2);

            Panel tgl = new Panel();
            tgl.Dock = DockStyle.Fill;
            tgl.BackColor = Bg;
            Button list = MakeButton("Liste görünümü", false);
            list.Location = new Point(0, 4);
            list.Click += delegate { ToggleListView(); };
            tgl.Controls.Add(list);
            root.Controls.Add(tgl, 0, 3);

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Bg;
            host.Margin = new Padding(0);
            root.Controls.Add(host, 0, 4);
            return host;
        }
    }
}
