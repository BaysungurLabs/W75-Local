using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    public class ModeItem
    {
        public int Value;
        public string Name;
        public override string ToString() { return Name; }
    }

    public partial class MainForm : Form
    {
        private static readonly Color Bg = Theme.Bg;
        private static readonly Color PanelBg = Theme.Panel;
        private static readonly Color PanelBg2 = Theme.Raised;
        private static readonly Color TxtClr = Theme.Text;
        private static readonly Color Muted = Theme.Muted;
        private static readonly Color Accent = Theme.Accent;
        private static readonly Color Accent2 = Theme.Cyan;
        private static readonly Color Sel = Theme.SelRow;
        private static readonly Color Line = Theme.Border;
        private static readonly Color Green = Theme.Green;
        private static readonly Color Red = Theme.Red;

        private W75Device _dev;
        private AppData _data;
        private bool _loading;
        private bool _busy;
        private bool _loadingProfiles;

        private ListBox _profileList;
        private Label _deviceStatus;
        private Button _btnConnect, _btnRead, _btnWrite;
        private KeyboardPanel _keyboard;
        private Label _keyInfo, _assignInfo, _assignRaw;
        private ComboBox _layerCombo;
        private TextBox _actionFilter;
        private ListBox _actionList;
        private DataGridView _grid;
        private NumericUpDown _numTravel, _numI1, _numI2, _numDeadTop, _numDeadBottom;
        private ComboBox _rtCombo;
        private ComboBox _lightMode;
        private DirPicker _lightDir;
        private NumericUpDown _lightBrightness, _lightSpeed;
        private CheckBox _lightPower, _lightFull;
        private Button _btnFg, _btnBg;
        private LightPreview _lightPreview;
        private Label _devProduct, _devVersion, _devTravel, _devUnit, _devPoll;
        private TextBox _log;
        private int[] _lightModes;
        private int _maxBrightness = 4;
        private int _maxSpeed = 4;

        public MainForm()
        {
            AppIcon.Apply(this); WindowTheme.Setup(this);
            KeyboardLayout.Load();
            Actions.Load();
            KeyLabels.LoadSavedMode(); // tuş yazıları düzeni (TR/US), pencere.json'dan
            _data = Store.Load();
            PollMigrationFollowUp(); // v7 göçü: polling profilden çıktı; yazım durumu özetleri yeni biçime taşınır
            BuildUi();
            RefreshProfileList();
            Store.Saved += delegate { if (_profileList != null && !_profileList.IsDisposed) _profileList.Invalidate(); }; // durum yazıları
            Store.SaveFailed += delegate(string msg) { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { ShowSaveErrorNote(msg); }); };
            TryConnect(false);
            LoadProfileUi();
            Log("Yerel profil dosyası: " + Store.FilePath);
            Shown += delegate { ShowReorderNote(); ShowPollMigrationNote(); if (Store.SaveError != null) ShowSaveErrorNote(Store.SaveError); };
        }

        private bool _saveNoteShown;

        // Ayarlar dosyaya yazılamadıysa (salt okunur klasör vb.) oturum başına bir kez sade bir uyarı gösterir; program çalışmaya devam eder.
        private void ShowSaveErrorNote(string detail)
        {
            if (_saveNoteShown) return;
            _saveNoteShown = true;
            string msg = "Ayarlar dosyaya kaydedilemedi: veri klasörü salt okunur olabilir ya da başka bir program dosyayı kullanıyor olabilir.\n\n" +
                "Program çalışmaya devam eder ama değişiklikleriniz bu oturum kapanınca kaybolur.\n\nKlasör: " + Store.Dir + "\n\nAyrıntı: " + detail;
            Log("UYARI: ayarlar kaydedilemedi: " + detail);
            MessageBox.Show(this, msg, "Ayarlar kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // v6 geçişi (5 profil → 4 profil) yapıldıysa: "Etkin" işareti temizlenir (slotlar değişti) ve kullanıcıya bir kez
        // bilgi gösterilir. Geçişten sonra tüm profiller "Cihaza yazılmadı" görünür (yazım durumu kaydı temizlendi).
        private void ShowReorderNote()
        {
            string bak = Store.LastMigrationBackup;
            if (bak == null) return;
            Store.LastMigrationBackup = null;
            MarkActive(-1);
            string note = "Cihaz " + DeviceLimits.MaxSlots + " profil destekliyor. Profiller " + DeviceLimits.MaxSlots + "'e düzenlendi. Eski liste yedeklendi: " + bak;
            Log(note);
            MessageBox.Show(this, note, "Profiller", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Cihaz slotu sınırı (DeviceLimits.MaxSlots): sınırın ötesindeki bir slot için komut gönderilmez; kullanıcıya mesaj gösterilir.
        // Cihaza komut gönderen eylemlerin başında çağrılır (Cihaza Yaz, Cihazdan Oku, yedek alma, etkinleştirme düğmesi).
        private bool SlotGuard(int slot)
        {
            if (DeviceLimits.SlotAllowed(slot)) return true;
            string msg = DeviceLimits.Message(slot);
            Log(msg);
            MessageBox.Show(this, msg, "Slot sınırı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private Profile Current
        {
            get
            {
                int i = _data.SelectedSlot;
                if (i < 0 || i >= _data.Profiles.Count || i >= DeviceLimits.MaxSlots) i = 0;
                return _data.Profiles[i];
            }
        }

        private double Unit
        {
            get
            {
                if (_dev != null && _dev.Status != null && _dev.Status.Unit > 0) return _dev.Status.Unit;
                return 0.02;
            }
        }

        private int MaxTravel
        {
            get
            {
                if (_dev != null && _dev.Status != null && _dev.Status.MaxTravel > 0) return _dev.Status.MaxTravel;
                return 170;
            }
        }

        // ============================================================ UI

        private void BuildUi()
        {
            SuspendLayout();
            Text = "W75 Local — Wraith W75 V2";
            BackColor = Bg;
            ForeColor = TxtClr;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ApplyInitialWindowBounds(); // ilk açılışta büyütülmüş; Normal boyut MainFormWindow.cs'te belirlenir
            MinimumSize = new Size(1080, 600); // etkin masaüstü 1080 genişlik/600 yüksekliğe kadar kullanılabilir (ör. 1366x768 ekranda %125 ölçek)
            DoubleBuffered = true;

            // Docking is applied in reverse z-order: the fill control must be
            // added first, then the edges, so the tab control gets the
            // remaining space instead of the whole client area.
            DarkTabControl tabs = new DarkTabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(126, 36); // 6 sekme en küçük pencerede (1080) kaydırma okları olmadan sığar
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.Padding = new Point(12, 6);
            tabs.Font = new Font("Segoe UI Semibold", 9.5f);
            tabs.DrawItem += Tabs_DrawItem;
            tabs.TabPages.Add(BuildKeysTab());
            tabs.TabPages.Add(BuildLightTab());
            tabs.TabPages.Add(BuildActuationTab());
            _socdTab = new SocdTab(EnsureDevice, delegate { return _dev; }, RunDevice, Log);
            tabs.TabPages.Add(_socdTab.Build());
            _digerTab = new DigerTab(EnsureDevice, delegate { return _dev; }, RunDevice, Log);
            TabPage digerPage = _digerTab.Build();
            Theme.ReplaceCombos(digerPage, _digerTab); // kısıtlı DigerTab.cs' ye dokunmadan koyu/yuvarlak açılır kutu
            tabs.TabPages.Add(digerPage);
            tabs.TabPages.Add(BuildDeviceTab());
            Controls.Add(tabs);

            BuildLeft();
            BuildBottom();
            BuildTop();

            ResumeLayout();
        }

        private void BuildTop()
        {
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 68;
            top.BackColor = PanelBg;
            top.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1);
            };
            Controls.Add(top);

            Label title = new Label();
            title.Text = "W75 LOCAL";
            title.Font = new Font("Segoe UI Semibold", 16f);
            title.ForeColor = TxtClr;
            title.AutoSize = true;
            title.Location = new Point(20, 11);
            top.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Wraith W75 V2 için yerel ayar yöneticisi";
            sub.Font = new Font("Segoe UI", 9f);
            sub.ForeColor = Muted;
            sub.AutoSize = true;
            sub.Location = new Point(22, 40);
            top.Controls.Add(sub);

            _deviceStatus = new Label();
            _deviceStatus.Text = "●  Cihaz aranıyor…";
            _deviceStatus.ForeColor = Muted;
            _deviceStatus.AutoSize = true;
            _deviceStatus.Font = new Font("Segoe UI", 9f);
            _deviceStatus.Location = new Point(360, 25);
            top.Controls.Add(_deviceStatus);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Right;
            flow.Width = 430;
            flow.FlowDirection = FlowDirection.RightToLeft;
            flow.WrapContents = false;
            flow.Padding = new Padding(0, 16, 16, 0);
            flow.BackColor = PanelBg;
            top.Controls.Add(flow);

            _btnWrite = MakeButton("Cihaza Yaz", true);
            _btnRead = MakeButton("Cihazdan Oku", false);
            _btnConnect = MakeButton("Bağlan", false);
            flow.Controls.Add(_btnWrite);
            flow.Controls.Add(_btnRead);
            flow.Controls.Add(_btnConnect);

            _btnConnect.Click += delegate { TryConnect(true); };
            _btnRead.Click += delegate { ReadFromDevice(); };
            _btnWrite.Click += delegate { WriteToDevice(); };
        }

        private void BuildBottom()
        {
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 27;
            bottom.BackColor = PanelBg;
            bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
            };
            Controls.Add(bottom);

            Label path = new Label();
            path.Text = "Otomatik kayıt: " + Store.FilePath;
            path.ForeColor = Muted;
            path.Font = new Font("Segoe UI", 8f);
            path.AutoSize = true;
            path.Location = new Point(16, 7);
            bottom.Controls.Add(path);

            Label ver = new Label();
            ver.Text = "W75 Local v1.0  ·  resmi değildir";
            ver.ForeColor = Muted;
            ver.Font = new Font("Segoe UI", 8f);
            ver.AutoSize = true;
            ver.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ver.Location = new Point(bottom.Width - 230, 7);
            bottom.Controls.Add(ver);
            bottom.Resize += delegate { ver.Left = bottom.Width - ver.Width - 16; };
        }

        private void BuildLeft()
        {
            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 248;
            left.BackColor = PanelBg;
            left.Padding = new Padding(12, 12, 12, 12);
            Controls.Add(left);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
            left.Controls.Add(root);

            Label header = new Label();
            header.Text = "PROFİLLER";
            header.ForeColor = Muted;
            header.Font = new Font("Segoe UI Semibold", 8f);
            header.Dock = DockStyle.Fill;
            header.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(header, 0, 0);

            _profileList = new ListBox();
            _profileList.Dock = DockStyle.Fill;
            _profileList.BorderStyle = BorderStyle.None;
            _profileList.BackColor = Theme.Inset;
            _profileList.ForeColor = TxtClr;
            _profileList.DrawMode = DrawMode.OwnerDrawFixed;
            _profileList.ItemHeight = 50; // ad + yazım durumu + etkin/önerilen ayar bilgisi (en küçük pencerede 5 profil sığar)
            _profileList.IntegralHeight = false;
            _profileList.Font = new Font("Segoe UI", 9.5f);
            _profileList.DrawItem += ProfileList_DrawItem;
            _profileList.SelectedIndexChanged += ProfileList_SelectedIndexChanged;
            root.Controls.Add(_profileList, 0, 1);

            FlowLayoutPanel btns = new FlowLayoutPanel();
            btns.Dock = DockStyle.Fill;
            btns.FlowDirection = FlowDirection.TopDown;
            btns.WrapContents = false;
            btns.BackColor = PanelBg;
            btns.Padding = new Padding(0, 8, 0, 0);
            root.Controls.Add(btns, 0, 2);

            btns.Controls.Add(BuildClickActivateBox()); // MainFormProfileActivate.cs

            Button activate = MakeButton("Bu profili klavyede etkinleştir", false);
            activate.Width = 220;
            activate.Margin = new Padding(0, 0, 0, 6);
            btns.Controls.Add(activate);
            activate.Click += delegate { ActivateProfile(); };

            Button rename = MakeButton("Adı Değiştir", false);
            rename.Width = 220;
            Button reset = MakeButton("Profili Sıfırla", false);
            reset.Width = 220;
            Button export = MakeButton("Dışa Aktar", false);
            export.Width = 220;
            Button import = MakeButton("İçe Aktar", false);
            import.Width = 220;
            rename.Margin = new Padding(0, 0, 0, 6);
            reset.Margin = new Padding(0, 0, 0, 6);
            export.Margin = new Padding(0, 0, 0, 6);
            import.Margin = new Padding(0, 0, 0, 0);
            btns.Controls.Add(rename);
            btns.Controls.Add(reset);
            btns.Controls.Add(export);
            btns.Controls.Add(import);

            rename.Click += delegate { RenameProfile(); };
            reset.Click += delegate { ResetProfile(); };
            new ToolTip().SetToolTip(reset, "Önerilen ayara dön (aktivasyon, Rapid Trigger ve ışık; tuş atamaları korunur)");
            export.Click += delegate { ExportProfiles(); };
            import.Click += delegate { ImportProfiles(); };
        }

        private TabPage NewPage(string title)
        {
            TabPage p = new TabPage(title);
            p.BackColor = Bg;
            p.ForeColor = TxtClr;
            p.Padding = new Padding(0);
            return p;
        }

        private TabPage BuildKeysTab()
        {
            TabPage page = NewPage("Keymap");

            TableLayoutPanel kt = new TableLayoutPanel();
            kt.Dock = DockStyle.Fill;
            kt.ColumnCount = 2;
            kt.RowCount = 1;
            kt.Padding = new Padding(10);
            kt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69f));
            kt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
            page.Controls.Add(kt);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = Bg;
            left.Margin = new Padding(0, 0, 10, 0);
            kt.Controls.Add(left, 0, 0);

            TableLayoutPanel ll = new TableLayoutPanel();
            ll.Dock = DockStyle.Fill;
            ll.ColumnCount = 1;
            ll.RowCount = 3;
            ll.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // "Tuş yazıları: TR | US" seçici
            ll.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            ll.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            left.Controls.Add(ll);

            _keyboard = new KeyboardPanel();
            _keyboard.Dock = DockStyle.Fill;
            _keyboard.Margin = new Padding(0);
            _keyboard.SelectedIndex = KeyboardLayout.IndexOf("KeyA");
            _keyboard.KeySelected += delegate
            {
                UpdateSelectedKeyInfo();
                FillActionList();
                SelectGridRow(_keyboard.SelectedIndex);
                UpdateSelectedKeyInfo();
            };
            ll.Controls.Add(BuildLabelModeBar(), 0, 0);
            ll.Controls.Add(_keyboard, 0, 1);

            Panel info = new Panel();
            info.Dock = DockStyle.Fill;
            info.BackColor = PanelBg;
            info.Margin = new Padding(0, 8, 0, 0);
            info.Padding = new Padding(12, 6, 12, 6);
            ll.Controls.Add(info, 0, 2);

            _keyInfo = new Label();
            _keyInfo.Text = "Bir tuşa tıklayın";
            _keyInfo.Font = new Font("Segoe UI Semibold", 11f);
            _keyInfo.ForeColor = TxtClr;
            _keyInfo.AutoSize = true;
            _keyInfo.Location = new Point(12, 5);
            info.Controls.Add(_keyInfo);

            _assignInfo = new Label();
            _assignInfo.Text = "";
            _assignInfo.Font = new Font("Segoe UI", 9f);
            _assignInfo.ForeColor = Accent;
            _assignInfo.AutoSize = true;
            _assignInfo.Location = new Point(12, 28);
            info.Controls.Add(_assignInfo);

            _assignRaw = new Label();
            _assignRaw.Text = "";
            _assignRaw.Font = new Font("Segoe UI", 8f);
            _assignRaw.ForeColor = Muted;
            _assignRaw.AutoSize = true;
            _assignRaw.Location = new Point(12, 48);
            info.Controls.Add(_assignRaw);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.BackColor = PanelBg;
            right.Padding = new Padding(14, 10, 14, 10);
            kt.Controls.Add(right, 1, 0);

            TableLayoutPanel rt = new TableLayoutPanel();
            rt.Dock = DockStyle.Fill;
            rt.ColumnCount = 1;
            rt.RowCount = 8;
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            rt.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            rt.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            right.Controls.Add(rt);

            Label layerLbl = SectionLabel("KATMAN");
            rt.Controls.Add(layerLbl, 0, 0);

            _layerCombo = new FlatCombo();
            _layerCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _layerCombo.Items.Add("Temel katman");
            _layerCombo.Items.Add("Fn katmanı");
            _layerCombo.SelectedIndex = 0;
            _layerCombo.Dock = DockStyle.Fill;
            _layerCombo.BackColor = Theme.Raised;
            _layerCombo.ForeColor = TxtClr;
            _layerCombo.FlatStyle = FlatStyle.Flat;
            _layerCombo.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                _keyboard.Layer = _layerCombo.SelectedIndex;
                _keyboard.Invalidate();
                UpdateSelectedKeyInfo();
                FillActionList();
            };
            rt.Controls.Add(_layerCombo, 0, 1);

            rt.Controls.Add(SectionLabel("EYLEM ARA"), 0, 2);

            _actionFilter = new TextBox();
            _actionFilter.Dock = DockStyle.Fill;
            _actionFilter.BackColor = Theme.Raised;
            _actionFilter.ForeColor = TxtClr;
            _actionFilter.BorderStyle = BorderStyle.FixedSingle;
            _actionFilter.TextChanged += delegate { if (!_loading) FillActionList(); };
            rt.Controls.Add(_actionFilter, 0, 4);

            _actionList = new ListBox();
            _actionList.Dock = DockStyle.Fill;
            _actionList.BorderStyle = BorderStyle.FixedSingle;
            _actionList.BackColor = Theme.Inset;
            _actionList.ForeColor = TxtClr;
            _actionList.IntegralHeight = false;
            _actionList.Font = new Font("Segoe UI", 9f);
            _actionList.DoubleClick += delegate { AssignAction(); };
            rt.Controls.Add(_actionList, 0, 5);

            FlowLayoutPanel ab = new FlowLayoutPanel();
            ab.Dock = DockStyle.Fill;
            ab.FlowDirection = FlowDirection.LeftToRight;
            ab.WrapContents = false;
            ab.BackColor = PanelBg;
            ab.Padding = new Padding(0, 4, 0, 0);
            rt.Controls.Add(ab, 0, 6);

            Button assign = MakeButton("Ata", true);
            Button clear = MakeButton("Sıfırla", false);
            assign.Click += delegate { AssignAction(); };
            clear.Click += delegate { ClearAction(); };
            ab.Controls.Add(assign);
            ab.Controls.Add(clear);

            Label note = new Label();
            note.Text = "Fn katmanında boş bırakılan tuşlar firmware varsayılanını kullanır. Değişiklikler otomatik kaydedilir.";
            note.ForeColor = Muted;
            note.Font = new Font("Segoe UI", 8f);
            note.Dock = DockStyle.Fill;
            rt.Controls.Add(note, 0, 7);

            return page;
        }

        private Panel _gridHostMesafe, _gridHostDead;

        private TabPage BuildActuationTab()
        {
            TabPage page = NewPage("Aktivasyon");

            _numTravel = MakeNum(0.02m, 3.40m, 0.02m, 2);
            _numI1 = MakeNum(0.02m, 1.00m, 0.02m, 2);
            _numI2 = MakeNum(0.02m, 1.00m, 0.02m, 2);
            _numDeadTop = MakeNum(0m, 1.00m, 0.02m, 2);
            _numDeadBottom = MakeNum(0m, 1.00m, 0.02m, 2);

            _rtCombo = new FlatCombo();
            _rtCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _rtCombo.Items.Add("Kapalı");
            _rtCombo.Items.Add("Rapid Trigger");
            _rtCombo.Items.Add("RT (ayrı)");
            _rtCombo.SelectedIndex = 0;
            _rtCombo.BackColor = Theme.Raised;
            _rtCombo.ForeColor = TxtClr;
            _rtCombo.FlatStyle = FlatStyle.Flat;

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.BackgroundColor = Bg;
            _grid.BorderStyle = BorderStyle.None;
            _grid.RowHeadersVisible = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = true;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersHeight = 30;
            _grid.RowTemplate.Height = 22;
            _grid.GridColor = Theme.Border;
            _grid.DefaultCellStyle.BackColor = Theme.Panel;
            _grid.DefaultCellStyle.ForeColor = TxtClr;
            _grid.DefaultCellStyle.SelectionBackColor = Theme.SelRow;
            _grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.8f);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Raised;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f);
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Raised;

            DataGridViewTextBoxColumn c0 = new DataGridViewTextBoxColumn();
            c0.HeaderText = "Tuş";
            c0.ReadOnly = true;
            c0.FillWeight = 70;
            _grid.Columns.Add(c0);

            DataGridViewTextBoxColumn c1 = new DataGridViewTextBoxColumn();
            c1.HeaderText = "Mesafe (mm)";
            c1.FillWeight = 90;
            _grid.Columns.Add(c1);

            DataGridViewComboBoxColumn c2 = new DataGridViewComboBoxColumn();
            c2.HeaderText = "Rapid Trigger";
            c2.FillWeight = 110;
            c2.FlatStyle = FlatStyle.Flat;
            c2.Items.Add("Kapalı");
            c2.Items.Add("RT");
            c2.Items.Add("RT (ayrı)");
            _grid.Columns.Add(c2);

            DataGridViewTextBoxColumn c3 = new DataGridViewTextBoxColumn();
            c3.HeaderText = "Basma (mm)";
            c3.FillWeight = 88;
            _grid.Columns.Add(c3);

            DataGridViewTextBoxColumn c4 = new DataGridViewTextBoxColumn();
            c4.HeaderText = "Bırakma (mm)";
            c4.FillWeight = 92;
            _grid.Columns.Add(c4);

            DataGridViewTextBoxColumn c5 = new DataGridViewTextBoxColumn();
            c5.HeaderText = "Üst ölü bölge (mm)";
            c5.FillWeight = 84;
            _grid.Columns.Add(c5);

            DataGridViewTextBoxColumn c6 = new DataGridViewTextBoxColumn();
            c6.HeaderText = "Alt ölü bölge (mm)";
            c6.FillWeight = 84;
            _grid.Columns.Add(c6);

            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell != null && _grid.CurrentCell.ColumnIndex == 2)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.SelectionChanged += Grid_SelectionChanged;

            DarkTabControl sub = new DarkTabControl();
            sub.Dock = DockStyle.Fill;
            sub.DrawMode = TabDrawMode.OwnerDrawFixed;
            sub.ItemSize = new Size(130, 32);
            sub.SizeMode = TabSizeMode.Fixed;
            sub.Padding = new Point(12, 6);
            sub.Font = new Font("Segoe UI Semibold", 9.5f);
            sub.DrawItem += Tabs_DrawItem;

            TabPage pMesafe = NewPage("Mesafe");
            TabPage pDead = NewPage("Ölü Bölge");
            TabPage pPoll = NewPage("Polling Rate");
            TabPage pSwitch = NewPage("Switch");
            TabPage pCal = NewPage("Kalibrasyon");
            sub.TabPages.Add(pMesafe);
            sub.TabPages.Add(pDead);
            sub.TabPages.Add(pPoll);
            // Switch ve Kalibrasyon sayfaları henüz boş: sekme başlığı gösterilmesin. Açmak için iki satırı geri ekle.
            // sub.TabPages.Add(pSwitch);
            // sub.TabPages.Add(pCal);

            _gridHostMesafe = BuildGridPage(pMesafe, null, false);
            _gridHostDead = BuildGridPage(pDead, null, true);
            _gridHostMesafe.Controls.Add(_grid);

            BuildPollPage(pPoll);
            BuildEmptyPage(pSwitch);
            BuildEmptyPage(pCal);

            sub.SelectedIndexChanged += delegate
            {
                Panel host = sub.SelectedTab == pMesafe ? _gridHostMesafe : (sub.SelectedTab == pDead ? _gridHostDead : null);
                if (host == null || _grid.Parent == host) return;
                bool old = _loading;
                _loading = true;
                host.Controls.Add(_grid);
                _loading = old;
            };

            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.BackColor = Bg;
            wrap.Padding = new Padding(10);
            wrap.Controls.Add(sub);
            page.Controls.Add(wrap);

            return page;
        }

        private void BuildEmptyPage(TabPage tp)
        {
            Label l = new Label();
            l.Text = "Bu sürümde yok";
            l.ForeColor = Muted;
            l.Font = new Font("Segoe UI", 12f);
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleCenter;
            tp.Controls.Add(l);
        }

        private TabPage BuildLightTab()
        {
            TabPage page = NewPage("Işıklandırma");

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.Padding = new Padding(14);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(root);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = PanelBg;
            left.Padding = new Padding(18, 14, 18, 14);
            left.Margin = new Padding(0, 0, 14, 0);
            left.AutoScroll = true;
            root.Controls.Add(left, 0, 0);
            _lightLeft = left;

            _lblMode = MakeFieldLabel("Işık modu", 0);
            left.Controls.Add(_lblMode);
            _lightMode = new FlatCombo();
            _lightMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _lightMode.Bounds = new Rectangle(0, 20, 380, 28);
            _lightMode.BackColor = Theme.Raised;
            _lightMode.ForeColor = TxtClr;
            _lightMode.FlatStyle = FlatStyle.Flat;
            _lightMode.SelectedIndexChanged += delegate { LightChanged(); };
            left.Controls.Add(_lightMode);

            _lblBright = MakeFieldLabel("Parlaklık", 62);
            left.Controls.Add(_lblBright);
            _lightBrightness = MakeNum(0m, 4m, 1m, 0);
            _lightBrightness.Bounds = new Rectangle(0, 82, 110, 28);
            _lightBrightness.ValueChanged += delegate { LightChanged(); };
            left.Controls.Add(_lightBrightness);

            _lblSpeed = MakeFieldLabel("Hız", 62, 140);
            left.Controls.Add(_lblSpeed);
            _lightSpeed = MakeNum(0m, 4m, 1m, 0);
            _lightSpeed.Bounds = new Rectangle(140, 82, 110, 28);
            _lightSpeed.ValueChanged += delegate { LightChanged(); };
            left.Controls.Add(_lightSpeed);

            _lblDir = MakeFieldLabel("Yön", 122);
            left.Controls.Add(_lblDir);
            _lightDir = BuildDirPicker(142); // görsel yön seçici (MainFormDirection.cs)
            left.Controls.Add(_lightDir);

            _lightPower = new CheckBox();
            _lightPower.Text = "Işık açık";
            _lightPower.ForeColor = TxtClr;
            _lightPower.Bounds = new Rectangle(0, 216, 130, 24);
            _lightPower.CheckedChanged += delegate { LightChanged(); };
            left.Controls.Add(_lightPower);

            _lightFull = new CheckBox();
            _lightFull.Text = "Tam renk modu";
            _lightFull.ForeColor = TxtClr;
            _lightFull.Bounds = new Rectangle(150, 216, 160, 24);
            _lightFull.CheckedChanged += delegate { LightChanged(); };
            left.Controls.Add(_lightFull);

            _btnFg = MakeColorButton("Ön renk", 0, 252);
            _btnFg.Click += delegate { PickColor(true); };
            left.Controls.Add(_btnFg);

            _btnBg = MakeColorButton("Arka renk", 205, 252);
            _btnBg.Click += delegate { PickColor(false); };
            left.Controls.Add(_btnBg);

            int noteY = BuildLiveLightControls(left, 294); // #RRGGBB kutuları + Canlı uygula (MainFormLiveLight.cs)

            Label note = new Label();
            note.Text = "Canlı uygula kapalıyken ışık ayarları yalnızca “Cihaza Yaz” ile klavyeye uygulanır. Yan ışık şeridi bu sürümde ana ışıkla birlikte çalışır.";
            note.ForeColor = Muted;
            note.Font = new Font("Segoe UI", 8.2f);
            note.Bounds = new Rectangle(0, noteY, 380, 60);
            left.Controls.Add(note);
            _lightNote = note;

            _lightPreview = new LightPreview();
            _lightPreview.Dock = DockStyle.Fill;
            _lightPreview.Margin = new Padding(0);
            root.Controls.Add(_lightPreview, 1, 0);

            ApplyModeOptions(Current.Light.Mode); // mod başına seçenekler: ilk yerleşim

            return page;
        }

        private TabPage BuildDeviceTab()
        {
            TabPage page = NewPage("Cihaz");

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Padding = new Padding(12);
            page.Controls.Add(root);

            Panel info = new Panel();
            info.Dock = DockStyle.Fill;
            info.BackColor = PanelBg;
            info.Padding = new Padding(16, 10, 16, 10);
            info.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(info, 0, 0);

            _devProduct = InfoRow(info, "Cihaz", 12);
            _devVersion = InfoRow(info, "Firmware", 40);
            _devTravel = InfoRow(info, "Maks. mesafe", 68);
            _devUnit = InfoRow(info, "Ölçü birimi", 96);
            _devPoll = InfoRow(info, "Polling hızı", 124);
            _devPoll.Text = "Aktivasyon > Polling Rate bölümünden yönetilir";

            FlowLayoutPanel fb = new FlowLayoutPanel();
            fb.Bounds = new Rectangle(16, 162, 900, 44);
            fb.FlowDirection = FlowDirection.LeftToRight;
            fb.WrapContents = false;
            fb.BackColor = PanelBg;
            info.Controls.Add(fb);

            Button refresh = MakeButton("Durumu Yenile", false);
            Button backup = MakeButton("Yedek Al", false);
            Button restore = MakeButton("Yedekten Geri Yükle", false);
            Button folder = MakeButton("Klasörü Aç", false);
            refresh.Click += delegate { TryConnect(true); };
            backup.Click += delegate { BackupDevice(); };
            restore.Click += delegate { RestoreDevice(); };
            folder.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Store.Dir + "\""); }
                catch (Exception ex) { Log("Klasör açılamadı: " + ex.Message); }
            };
            fb.Controls.Add(refresh);
            fb.Controls.Add(backup);
            fb.Controls.Add(restore);
            fb.Controls.Add(folder);

            // Ayar paketi: profiles.json + socd.json + diger.json tek zip içinde (klavyeye hiçbir şey göndermez).
            FlowLayoutPanel fb2 = new FlowLayoutPanel();
            fb2.Bounds = new Rectangle(16, 212, 900, 44);
            fb2.FlowDirection = FlowDirection.LeftToRight;
            fb2.WrapContents = false;
            fb2.BackColor = PanelBg;
            info.Controls.Add(fb2);
            Button bundleOut = MakeButton("Tüm ayarları paketle (dışa aktar)", false);
            Button bundleIn = MakeButton("Paketten yükle (içe aktar)", false);
            bundleOut.Click += delegate { ExportBundle(); };
            bundleIn.Click += delegate { ImportBundle(); };
            fb2.Controls.Add(bundleOut);
            fb2.Controls.Add(bundleIn);

            _log = new TextBox();
            _log.Dock = DockStyle.Fill;
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.WordWrap = false;
            _log.BackColor = Theme.Inset;
            _log.ForeColor = Theme.Text2;
            _log.BorderStyle = BorderStyle.None;
            _log.Font = new Font("Consolas", 9f);
            _log.Margin = new Padding(0);
            root.Controls.Add(_log, 0, 1);

            return page;
        }

        private static Label SectionLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Muted;
            l.Font = new Font("Segoe UI Semibold", 8f);
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.BottomLeft;
            return l;
        }

        private static Label MakeFieldLabel(string text, int y)
        {
            return MakeFieldLabel(text, y, 0);
        }

        private static Label MakeFieldLabel(string text, int y, int x)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Muted;
            l.Font = new Font("Segoe UI", 8.5f);
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        private static Panel Field(string label, Control c, int width)
        {
            Panel p = new Panel();
            p.Width = width;
            p.Height = 58;
            p.Margin = new Padding(0, 0, 10, 4);
            p.BackColor = PanelBg;

            if (!string.IsNullOrEmpty(label))
            {
                Label l = new Label();
                l.Text = label;
                l.ForeColor = Muted;
                l.Font = new Font("Segoe UI", 8f);
                l.AutoSize = true;
                l.Location = new Point(0, 0);
                p.Controls.Add(l);
                c.Location = new Point(0, 19);
            }
            else
            {
                c.Location = new Point(0, 22);
            }
            c.Width = width - 6;
            c.Height = 28;
            p.Controls.Add(c);
            return p;
        }

        private static NumericUpDown MakeNum(decimal min, decimal max, decimal inc, int decimals)
        {
            NumericUpDown n = new DarkNumeric();
            n.Minimum = min;
            n.Maximum = max;
            n.Increment = inc;
            n.DecimalPlaces = decimals;
            n.BackColor = Theme.Raised;
            n.ForeColor = TxtClr;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = new Font("Segoe UI", 9.5f);
            n.TextAlign = HorizontalAlignment.Right;
            return n;
        }

        private static Button MakeButton(string text, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = false;
            b.Height = 32;
            b.Width = TextRenderer.MeasureText(text, new Font("Segoe UI Semibold", 9f)).Width + 30;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = accent ? 0 : 1;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.BackColor = accent ? Accent : Theme.Raised;
            b.ForeColor = accent ? Theme.AccentText : TxtClr;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(0, 0, 8, 0);
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private Button MakeColorButton(string text, int x, int y)
        {
            Button b = new Button();
            b.Text = text;
            b.Bounds = new Rectangle(x, y, 185, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.ForeColor = TxtClr;
            b.Font = new Font("Segoe UI", 9f);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(10, 0, 0, 0);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private Label InfoRow(Panel parent, string caption, int y)
        {
            Label c = new Label();
            c.Text = caption;
            c.ForeColor = Muted;
            c.Font = new Font("Segoe UI", 9f);
            c.AutoSize = true;
            c.Location = new Point(16, y + 2);
            parent.Controls.Add(c);

            Label v = new Label();
            v.Text = "—";
            v.ForeColor = TxtClr;
            v.Font = new Font("Segoe UI Semibold", 9.5f);
            v.AutoSize = true;
            v.Location = new Point(150, y);
            parent.Controls.Add(v);
            return v;
        }

        private void Tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)sender;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            Rectangle r = e.Bounds;
            bool selected = tc.SelectedIndex == e.Index;
            using (SolidBrush b = new SolidBrush(Bg))
                e.Graphics.FillRectangle(b, r);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (selected)
                using (System.Drawing.Drawing2D.GraphicsPath sp = Theme.RoundRect(new RectangleF(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8), Theme.Radius))
                using (SolidBrush sb = new SolidBrush(Theme.Raised)) e.Graphics.FillPath(sb, sp);
            using (Font f = new Font("Segoe UI Semibold", 9.5f))
            {
                TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, f, r,
                    selected ? Theme.Text : Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (selected)
            {
                using (Pen p = new Pen(Accent, 2) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                    e.Graphics.DrawLine(p, r.Left + r.Width / 2 - 12, r.Bottom - 5, r.Left + r.Width / 2 + 12, r.Bottom - 5);
            }
        }

        // ============================================================ profiles

        private void RefreshProfileList()
        {
            _loadingProfiles = true;
            _profileList.Items.Clear();
            // Cihaz yalnızca DeviceLimits.MaxSlots profil saklar: listede en çok o kadar satır (4) gösterilir.
            for (int i = 0; i < _data.Profiles.Count && i < DeviceLimits.MaxSlots; i++) _profileList.Items.Add(_data.Profiles[i]);
            if (_data.SelectedSlot >= 0 && _data.SelectedSlot < _profileList.Items.Count)
                _profileList.SelectedIndex = _data.SelectedSlot;
            _loadingProfiles = false;
        }

        private void ProfileList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Profile p = _profileList.Items[e.Index] as Profile;
            if (p == null) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(_profileList.BackColor))
                e.Graphics.FillRectangle(b, e.Bounds);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (sel)
                using (System.Drawing.Drawing2D.GraphicsPath rp = Theme.RoundRect(new RectangleF(e.Bounds.X + 3, e.Bounds.Y + 1, e.Bounds.Width - 6, e.Bounds.Height - 2), Theme.Radius))
                using (SolidBrush sb = new SolidBrush(Theme.SelRow)) e.Graphics.FillPath(sb, rp);

            Rectangle badge = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 5, 22, 22);
            using (System.Drawing.Drawing2D.GraphicsPath bp = Theme.RoundRect(badge, 6))
            using (SolidBrush b = new SolidBrush(sel ? Accent : Theme.Border))
                e.Graphics.FillPath(b, bp);
            using (Font f = new Font("Segoe UI Semibold", 8.5f))
                TextRenderer.DrawText(e.Graphics, p.Slot.ToString(), f, badge,
                    sel ? Theme.AccentText : TxtClr,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Rectangle tr = new Rectangle(e.Bounds.X + 40, e.Bounds.Y + 3, e.Bounds.Width - 46, 20);
            using (Font f = new Font("Segoe UI Semibold", 9.5f))
                TextRenderer.DrawText(e.Graphics, p.Name, f, tr,
                    sel ? Theme.Text : TxtClr, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            const TextFormatFlags sf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            int sx = e.Bounds.X + 40, sw = e.Bounds.Width - 46;
            using (Font f = new Font("Segoe UI", 7.8f))
            {
                // 1. alt satır: klavyeye yazım durumu (yazim-durumu.json)
                int ws = WriteStatus(p);
                string wt = ws == 1 ? "Cihazda güncel" : (ws == 2 ? "Değişti, Cihaza Yaz yap" : "Cihaza yazılmadı");
                bool liveOnly = ws != 1 && _liveSentSlots.Contains(p.Slot); // canlı ışık gitti ama tam yazma yapılmadı
                if (liveOnly) wt = "Işık cihazda; gerisi: Cihaza Yaz";
                TextRenderer.DrawText(e.Graphics, wt, f, new Rectangle(sx, e.Bounds.Y + 20, sw, 13), ws == 1 ? Green : (ws == 2 || liveOnly ? Accent : Muted), sf);

                // 2. alt satır: etkin (programın bildiği) / önerilen ayardan farklıysa kısa bilgi / slot bilgisi
                bool active = p.Slot == _activeSlot;
                bool offPreset = !OnerilenAyarlar.Matches(p);
                string sub;
                if (active) sub = offPreset ? "●  Etkin  ·  Önerilen ayara dön" : "●  Etkin (programın bildiği)";
                else if (offPreset) sub = "Önerilen ayara dön";
                else sub = "Klavye profili " + DeviceProfiles.ToDevice(p.Slot);
                TextRenderer.DrawText(e.Graphics, sub, f, new Rectangle(sx, e.Bounds.Y + 33, sw, 13), active ? Green : Muted, sf);
            }
        }

        // Programın klavyeye son gönderdiği profil geçişi. Cihazdan okunmuş bir bilgi değildir;
        // klavyedeki kısayolla yapılan geçişleri program bilmez.
        private int _activeSlot = -1;

        private void MarkActive(int slot)
        {
            _activeSlot = slot;
            if (_profileList != null) _profileList.Invalidate();
        }

        private void ActivateProfile()
        {
            if (!SlotGuard(Current.Slot)) return; // slot sınırı
            if (!EnsureDevice()) return;
            Profile p = Current;
            CancelPendingActivate(); // düğmeye basıldı: bekleyen tıklama etkinleştirmesi gereksiz
            RunDevice(delegate
            {
                SendActivate(p); // yalnızca profil seçme komutu (MainFormProfileActivate.cs)
                MessageBox.Show(this, "Klavyenin " + DeviceProfiles.ToDevice(p.Slot) + ". profili etkinleştirildi.", "Profili etkinleştir", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void ProfileList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingProfiles || _loading) return;
            if (_profileList.SelectedIndex < 0) return;
            if (_profileList.SelectedIndex == _data.SelectedSlot) return;
            CancelPendingLiveLight(); // profil değiştirmek ışık göndermez
            _data.SelectedSlot = _profileList.SelectedIndex;
            Store.Save(_data);
            LoadProfileUi();
            ScheduleActivate(_data.SelectedSlot); // "Profile tıklayınca klavyede etkinleştir" açıksa (300 ms sonra)
        }

        private void LoadProfileUi()
        {
            bool old = _loading;
            _loading = true;
            Profile p = Current;
            _keyboard.Profile = p;
            _keyboard.Layer = _layerCombo.SelectedIndex < 0 ? 0 : _layerCombo.SelectedIndex;
            if (_keyboard.SelectedIndex < 0) _keyboard.SelectedIndex = KeyboardLayout.IndexOf("KeyA");
            _keyboard.Invalidate();

            FillActionListInternal();
            FillGridInternal();
            FillLightModesInternal();
            FillLightUiInternal();
            FillPollUi();

            _loading = old;
            UpdateSelectedKeyInfo();
            SelectGridRow(_keyboard.SelectedIndex);
            UpdateSelectedKeyInfo();
            if (_grid.SelectedRows.Count > 0) LoadTriggerFieldsFromRow(_grid.SelectedRows[0]);
        }

        private void RenameProfile()
        {
            string name = PromptForm.Show(this, "Profil adı", Current.Name);
            if (name == null) return;
            name = name.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Profil adı boş olamaz; ad değiştirilmedi.", "Profil adı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Current.Name = name;
            Store.Save(_data);
            RefreshProfileList();
            Log("Profil adı değiştirildi: " + name);
        }

        private void ResetProfile()
        {
            Profile p = Current;
            if (OnerilenAyarlar.HasPreset(p))
            {
                // Aktivasyon / Rapid Trigger / ışık / parlaklık önerilene, tuş atamaları (temel + Fn) varsayılana döner;
                // ölü bölge, anketleme hızı, hız, yön, arka renk ve profil adı korunur. Önce otomatik yedek (profiles.json'un tam kopyası).
                if (MessageBox.Show(this, "'" + p.Name + "'\n\nAktivasyon, Rapid Trigger, ışık ayarları, parlaklık ve tuş atamaları önerilen değerlere dönecek. Otomatik yedek alınır.\n\nDevam edilsin mi?",
                    "Profili sıfırla", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                string bak;
                try
                {
                    bak = Path.Combine(Store.BackupDir, "profiles-sifirlama-oncesi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    if (File.Exists(Store.FilePath)) File.Copy(Store.FilePath, bak, false);
                }
                catch (Exception ex) { Log("Yedek alınamadı, sıfırlama yapılmadı: " + ex.Message); return; }
                OnerilenAyarlar.ResetToRecommended(p);
                Store.Save(_data);
                RefreshProfileList();
                LoadProfileUi();
                Log("Önerilen ayara dönüldü (tuş atamaları varsayılana): " + p.Name + " (yedek: " + bak + "). Klavyeye henüz bir şey yazılmadı; Cihaza Yaz gerekir.");
                return;
            }
            if (MessageBox.Show(this, "'" + p.Name + "' profili " + (DeviceProfiles.ToDevice(p.Slot) + ". profil") + " için hazır ayara sıfırlanacak. Devam edilsin mi?", "Profili sıfırla",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Profile fresh = Store.CreateProfile(p.Slot);
            _data.Profiles[p.Slot] = fresh;
            Store.Save(_data);
            RefreshProfileList();
            LoadProfileUi();
            Log("Profil sıfırlandı: " + fresh.Name);
        }

        private void ExportProfiles()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Profilleri dışa aktar";
                d.Filter = "W75 profilleri (*.json)|*.json";
                d.FileName = "w75-profiller.json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    File.WriteAllText(d.FileName, ser.Serialize(_data.Profiles), new UTF8Encoding(false));
                    Log("Profiller dışa aktarıldı: " + d.FileName);
                    MessageBox.Show(this, "Profiller kaydedildi:\n" + d.FileName, "Dışa aktar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(this, "Dışa aktarılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void ImportProfiles()
        {
            string path = TestImportFile(); // yalnızca test kipinde: dosya penceresi yerine W75_ICE_AKTAR_DOSYA
            if (path == null)
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Profilleri içe aktar";
                    d.Filter = "W75 profilleri (*.json)|*.json";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    path = d.FileName;
                }
            }
            try
            {
                bool fromBackup;
                List<Profile> list = ParseProfilesFile(File.ReadAllText(path, Encoding.UTF8), out fromBackup);
                if (list == null || list.Count == 0) throw new InvalidDataException("Dosyada profil bulunamadı.");
                if (fromBackup) { RestoreFromBackup(path, list); return; } // Profili Sıfırla yedeği: profil seç, onay, önce otomatik yedek
                string fazla = "";
                if (list.Count > DeviceLimits.MaxSlots)
                {
                    fazla = "\n\nDosyada " + list.Count + " profil var; cihaz " + DeviceLimits.MaxSlots + " profil destekliyor, yalnızca ilk " + DeviceLimits.MaxSlots + " profil alınır.";
                    list = list.GetRange(0, DeviceLimits.MaxSlots);
                }
                if (MessageBox.Show(this, "Mevcut profillerin üzerine yazılacak. Devam edilsin mi?" + fazla, "İçe aktar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                AppData nd = new AppData();
                nd.Profiles = list;
                nd.SelectedSlot = 0;
                Store.Normalize(nd);
                _data.Profiles = nd.Profiles;
                _data.SelectedSlot = 0;
                Store.Save(_data);
                RefreshProfileList();
                LoadProfileUi();
                Log("Profiller içe aktarıldı: " + path);
            }
            catch (Exception ex) { MessageBox.Show(this, "İçe aktarılamadı: " + ex.Message + "\n\nHiçbir şey değiştirilmedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // İçe Aktar iki biçimi okur: (1) Dışa Aktar dosyası = profil listesi ([...]), (2) Profili Sıfırla'nın otomatik yedeği =
        // profiles.json'un tam kopyası ({"Profiles":[...], ...}). İkincisinde yalnızca profiller alınır (seçili slot, sürüm gibi
        // diğer alanlar alınmaz). Profiller dosyadaki sırayla (slot 0, 1, ...) yerine konur; eski düzende alınmış bir yedekte
        // (ör. Valorant slot 3'te) sıra o düzendir.
        private static List<Profile> ParseProfilesFile(string text, out bool fromBackup)
        {
            fromBackup = false;
            string t = (text ?? "").TrimStart('﻿', ' ', '\t', '\r', '\n');
            JavaScriptSerializer ser = new JavaScriptSerializer();
            if (t.StartsWith("[")) return ser.Deserialize<List<Profile>>(t);
            if (t.StartsWith("{"))
            {
                AppData ad = ser.Deserialize<AppData>(t);
                fromBackup = true;
                return ad == null ? null : ad.Profiles;
            }
            throw new InvalidDataException("Dosya tanınmadı: profil listesi ya da Profili Sıfırla yedeği bekleniyordu.");
        }

        // ============================================================ key assignment

        private void UpdateSelectedKeyInfo()
        {
            int idx = _keyboard.SelectedIndex;
            KeyDef k = KeyboardLayout.Get(idx);
            if (k == null)
            {
                _keyInfo.Text = "Bir tuşa tıklayın";
                _assignInfo.Text = "";
                _assignRaw.Text = "";
                return;
            }
            bool fn = _layerCombo.SelectedIndex == 1;
            _keyInfo.Text = KeyboardPanel.DisplayName(k) + "   ·   " + (fn ? "Fn katmanı" : "Temel katman");
            KeyAssign a = Current.FindKey(idx, fn);
            if (a != null)
            {
                _assignInfo.Text = "Atanan: " + AssignedLabel(a);
                _assignRaw.Text = string.Format("Kod: {0} / {1:X2} / {2} / {3}", a.C1, a.Hid, a.C3, a.C4);
            }
            else if (fn)
            {
                _assignInfo.Text = "Atanan: firmware varsayılanı";
                _assignRaw.Text = "Fn katmanı boş";
            }
            else
            {
                byte[] d = Keymap.DefaultEntry(k);
                ActionDef def = Actions.Find(d[0], d[1], 0, 0);
                _assignInfo.Text = "Atanan: " + (def != null ? KeyLabels.ActionLabel(def) : "varsayılan");
                _assignRaw.Text = "Fabrika tuş haritası";
            }
        }

        private void FillActionList()
        {
            if (_loading) return;
            FillActionListInternal();
        }

        private void FillActionListInternal()
        {
            bool old = _loading;
            _loading = true;
            int idx = _keyboard.SelectedIndex;
            bool fn = _layerCombo.SelectedIndex == 1;
            KeyAssign cur = idx >= 0 ? Current.FindKey(idx, fn) : null;
            string want = cur != null ? cur.Code : null;
            if (want == null && !fn && idx >= 0)
            {
                KeyDef k = KeyboardLayout.Get(idx);
                if (k != null)
                {
                    byte[] d = Keymap.DefaultEntry(k);
                    ActionDef def = Actions.Find(d[0], d[1], 0, 0);
                    if (def != null) want = def.Code;
                }
            }
            List<ActionDef> list = Actions.Search(_actionFilter.Text);
            _actionList.BeginUpdate();
            _actionList.Items.Clear();
            int selected = -1;
            for (int i = 0; i < list.Count; i++)
            {
                _actionList.Items.Add(list[i]);
                if (want != null && list[i].Code == want) selected = i;
            }
            if (selected >= 0) _actionList.SelectedIndex = selected;
            else if (_actionList.Items.Count > 0) _actionList.SelectedIndex = 0;
            _actionList.EndUpdate();
            _loading = old;
        }

        private void AssignAction()
        {
            int idx = _keyboard.SelectedIndex;
            ActionDef a = _actionList.SelectedItem as ActionDef;
            if (idx < 0 || a == null) return;
            bool fn = _layerCombo.SelectedIndex == 1;
            Current.SetKey(idx, a, fn);
            Store.Save(_data);
            _keyboard.Invalidate();
            UpdateSelectedKeyInfo();
            Log((fn ? "Fn" : "Temel") + " katman · " + KeyboardLayout.Get(idx).Display + " → " + KeyLabels.ActionLabel(a));
        }

        private void ClearAction()
        {
            int idx = _keyboard.SelectedIndex;
            if (idx < 0) return;
            bool fn = _layerCombo.SelectedIndex == 1;
            Current.SetKey(idx, null, fn);
            Store.Save(_data);
            _keyboard.Invalidate();
            UpdateSelectedKeyInfo();
        }

        // ============================================================ trigger grid

        private string Mm(int raw)
        {
            return (raw * Unit).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private bool TryRaw(object value, out int raw)
        {
            raw = 0;
            string s = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (s == null) return false;
            s = s.Replace(',', '.').Trim();
            double mm;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mm)) return false;
            raw = (int)Math.Round(mm / Unit);
            if (raw < 0) raw = 0;
            if (raw > MaxTravel) raw = MaxTravel;
            return true;
        }

        private static string RtText(int mode)
        {
            if (mode == 12) return "RT";
            if (mode == 13) return "RT (ayrı)";
            return "Kapalı";
        }

        private static int RtValue(string text)
        {
            if (text == "RT") return 12;
            if (text == "RT (ayrı)") return 13;
            return 0;
        }

        private static int RtIndex(int mode)
        {
            if (mode == 12) return 1;
            if (mode == 13) return 2;
            return 0;
        }

        private void FillGrid()
        {
            if (_loading) return;
            FillGridInternal();
        }

        private void FillGridInternal()
        {
            bool old = _loading;
            _loading = true;
            _grid.Rows.Clear();
            Profile p = Current;
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                TriggerSetting t = p.EffectiveTrigger(k.Index);
                int ri = _grid.Rows.Add(
                    KeyboardPanel.DisplayName(k),
                    Mm(t.Travel),
                    RtText(t.Mode),
                    Mm(t.Interval1),
                    Mm(t.Interval2),
                    Mm(t.DeadTop),
                    Mm(t.DeadBottom));
                DataGridViewRow row = _grid.Rows[ri];
                row.Tag = k.Index;
                row.DefaultCellStyle.ForeColor = p.FindTrigger(k.Index) != null ? Accent : TxtClr;
            }
            _loading = old;
        }

        private void SelectGridRow(int keyIndex)
        {
            bool old = _loading;
            _loading = true;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if ((int)row.Tag == keyIndex)
                {
                    _grid.ClearSelection();
                    row.Selected = true;
                    if (row.Cells.Count > 0) _grid.CurrentCell = row.Cells[0];
                    LoadTriggerFieldsFromRow(row); // alanlar seçili tuşun gerçek değerlerini göstersin
                    break;
                }
            }
            _loading = old;
            SyncPanelsFromGrid();
            UpdateSelButtons();
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateSelButtons();
            if (_syncing) return;
            if (_loading) return;
            SyncPanelsFromGrid();
            if (_grid.SelectedRows.Count == 1)
            {
                DataGridViewRow row = _grid.SelectedRows[0];
                LoadTriggerFieldsFromRow(row);
                int idx = (int)row.Tag;
                _keyboard.SelectedIndex = idx;
                _keyboard.Invalidate();
                UpdateSelectedKeyInfo();
                FillActionListInternal();
            }
        }

        private void LoadTriggerFieldsFromRow(DataGridViewRow row)
        {
            bool old = _loading;
            _loading = true;
            int travel, i1, i2, dt, db;
            if (TryRaw(row.Cells[1].Value, out travel)) SetNum(_numTravel, travel);
            if (TryRaw(row.Cells[3].Value, out i1)) SetNum(_numI1, i1);
            if (TryRaw(row.Cells[4].Value, out i2)) SetNum(_numI2, i2);
            if (TryRaw(row.Cells[5].Value, out dt)) SetNum(_numDeadTop, dt);
            if (TryRaw(row.Cells[6].Value, out db)) SetNum(_numDeadBottom, db);
            _rtCombo.SelectedIndex = RtIndex(RtValue(Convert.ToString(row.Cells[2].Value)));
            _loading = old;
        }

        private void SetNum(NumericUpDown n, int raw)
        {
            decimal v = (decimal)(raw * Unit);
            if (v < n.Minimum) v = n.Minimum;
            if (v > n.Maximum) v = n.Maximum;
            try { n.Value = v; } catch { }
        }

        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || e.RowIndex < 0) return;
            if (e.ColumnIndex != 2) return;
            ApplyGridRow(e.RowIndex);
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || e.RowIndex < 0) return;
            if (e.ColumnIndex == 0 || e.ColumnIndex == 2) return;
            ApplyGridRow(e.RowIndex);
        }

        private void ApplyGridRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
            DataGridViewRow row = _grid.Rows[rowIndex];
            int idx = (int)row.Tag;
            int travel, i1, i2, dt, db;
            if (!TryRaw(row.Cells[1].Value, out travel)) { RevertRow(row); return; }
            if (!TryRaw(row.Cells[3].Value, out i1)) { RevertRow(row); return; }
            if (!TryRaw(row.Cells[4].Value, out i2)) { RevertRow(row); return; }
            if (!TryRaw(row.Cells[5].Value, out dt)) { RevertRow(row); return; }
            if (!TryRaw(row.Cells[6].Value, out db)) { RevertRow(row); return; }
            int mode = RtValue(Convert.ToString(row.Cells[2].Value));

            Current.SetTrigger(idx, mode, travel, i1, i2, dt, db);
            CleanupTrigger(idx);
            Store.Save(_data);

            bool old = _loading;
            _loading = true;
            row.Cells[1].Value = Mm(travel);
            row.Cells[3].Value = Mm(i1);
            row.Cells[4].Value = Mm(i2);
            row.Cells[5].Value = Mm(dt);
            row.Cells[6].Value = Mm(db);
            row.DefaultCellStyle.ForeColor = Current.FindTrigger(idx) != null ? Accent : TxtClr;
            _loading = old;

            _keyboard.Invalidate();
            RefreshValueKeyboards();
            UpdateSelectedKeyInfo();
        }

        private void RevertRow(DataGridViewRow row)
        {
            int idx = (int)row.Tag;
            TriggerSetting t = Current.EffectiveTrigger(idx);
            bool old = _loading;
            _loading = true;
            row.Cells[1].Value = Mm(t.Travel);
            row.Cells[2].Value = RtText(t.Mode);
            row.Cells[3].Value = Mm(t.Interval1);
            row.Cells[4].Value = Mm(t.Interval2);
            row.Cells[5].Value = Mm(t.DeadTop);
            row.Cells[6].Value = Mm(t.DeadBottom);
            _loading = old;
        }

        private void CleanupTrigger(int index)
        {
            TriggerOverride t = Current.FindTrigger(index);
            if (t == null) return;
            if (t.Mode == Current.DefaultMode && t.Travel == Current.DefaultTravel &&
                t.I1 == Current.DefaultI1 && t.I2 == Current.DefaultI2 &&
                t.DeadTop == 0 && t.DeadBottom == 0)
                Current.Triggers.Remove(t);
        }

        // deadZone=false: yalnızca mesafe, Rapid Trigger, basma, bırakma. deadZone=true: yalnızca üst/alt ölü bölge.
        private void ApplyGlobal(bool selectedOnly, bool deadZone)
        {
            List<int> targets = new List<int>();
            if (selectedOnly)
            {
                foreach (DataGridViewRow row in _grid.SelectedRows) targets.Add((int)row.Tag);
                if (targets.Count == 0) { Log("Seçili tuş yok, bir şey uygulanmadı."); return; }
            }
            else
            {
                foreach (KeyDef k in KeyboardLayout.Keys) targets.Add(k.Index);
            }

            Profile p = Current;
            string msg;
            if (!deadZone)
            {
                int travel = RawFromNum(_numTravel);
                int i1 = RawFromNum(_numI1);
                int i2 = RawFromNum(_numI2);
                int mode = RtIndexToMode(_rtCombo.SelectedIndex);
                if (!selectedOnly)
                {
                    p.DefaultTravel = travel;
                    p.DefaultMode = mode;
                    p.DefaultI1 = i1;
                    p.DefaultI2 = i2;
                }
                foreach (int idx in targets)
                {
                    TriggerSetting t = p.EffectiveTrigger(idx);
                    p.SetTrigger(idx, mode, travel, i1, i2, t.DeadTop, t.DeadBottom);
                    CleanupTrigger(idx);
                }
                msg = string.Format("Uygulanan alanlar: mesafe {0:0.00} mm, Rapid Trigger {1}, basma {2:0.00}, bırakma {3:0.00} (ölü bölgeye dokunulmadı)",
                    travel * Unit, RtText(mode), i1 * Unit, i2 * Unit);
            }
            else
            {
                int dt = RawFromNum(_numDeadTop);
                int db = RawFromNum(_numDeadBottom);
                foreach (int idx in targets)
                {
                    TriggerSetting t = p.EffectiveTrigger(idx);
                    p.SetTrigger(idx, t.Mode, t.Travel, t.Interval1, t.Interval2, dt, db);
                    CleanupTrigger(idx);
                }
                msg = string.Format("Uygulanan alanlar: üst ölü bölge {0:0.00}, alt ölü bölge {1:0.00} (mesafe, Rapid Trigger, basma ve bırakmaya dokunulmadı)",
                    dt * Unit, db * Unit);
            }
            Store.Save(_data);
            if (_profileList != null) _profileList.Invalidate(); // "Önerilen ayara dön" bilgisi güncellensin
            List<int> keep = new List<int>(_multiSel); // uygulamadan sonra seçim korunsun
            FillGridInternal();
            if (keep.Count > 0) ApplySelectionFromList(keep);
            else SelectGridRow(_keyboard.SelectedIndex);
            _keyboard.Invalidate();
            RefreshValueKeyboards();
            Log(targets.Count +(selectedOnly ? " seçili tuşa" : " tuşa") + " uygulandı. " + msg);
        }

        private int RawFromNum(NumericUpDown n)
        {
            int raw = (int)Math.Round((double)n.Value / Unit);
            if (raw < 0) raw = 0;
            if (raw > MaxTravel) raw = MaxTravel;
            return raw;
        }

        private static int RtIndexToMode(int i)
        {
            if (i == 1) return 12;
            if (i == 2) return 13;
            return 0;
        }

        // ============================================================ light

        private void FillLightModes()
        {
            if (_loading) return;
            FillLightModesInternal();
        }

        private void FillLightModesInternal()
        {
            int wanted = Current.Light.Mode;
            int[] modes = _lightModes;
            if (modes == null || modes.Length == 0)
                modes = new int[] { 0, 1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 14, 15, 16 };

            bool old = _loading;
            _loading = true;
            _lightMode.Items.Clear();
            int select = -1;
            for (int i = 0; i < modes.Length; i++)
            {
                ModeItem mi = new ModeItem();
                mi.Value = modes[i];
                mi.Name = LightModeName(modes[i]);
                _lightMode.Items.Add(mi);
                if (modes[i] == wanted) select = i;
            }
            if (select < 0)
            {
                // Profildeki mod listede yoksa (ör. Özel): listeye eklenir; yoksa kutu sessizce başka bir modu gösterirdi.
                ModeItem extra = new ModeItem();
                extra.Value = wanted;
                extra.Name = LightModeName(wanted);
                select = _lightMode.Items.Add(extra);
            }
            if (select >= 0) _lightMode.SelectedIndex = select;
            else if (_lightMode.Items.Count > 0) _lightMode.SelectedIndex = 0;
            _loading = old;
        }

        private static string LightModeName(int mode)
        {
            switch (mode)
            {
                case 0: return "Statik";
                case 1: return "Nefes";
                case 2: return "Dalga";
                case 3: return "Neon";
                case 4: return "Radar";
                case 6: return "Reaktif";
                case 7: return "Aurora";
                case 8: return "Reaktif Dalgalanma";
                case 9: return "Pırıltı";
                case 10: return "Özel";
                case 11: return "Çapraz";
                case 12: return "Hıza Duyarlı";
                case 14: return "Oto Dalgalanma";
                case 15: return "Çizgilenme";
                case 16: return "Havai Fişekler";
                default: return "Mod " + mode;
            }
        }

        private void FillLightUi()
        {
            if (_loading) return;
            FillLightUiInternal();
        }

        private void FillLightUiInternal()
        {
            bool old = _loading;
            _loading = true;
            LightSetting l = Current.Light;

            _lightBrightness.Maximum = _maxBrightness;
            _lightSpeed.Maximum = _maxSpeed;
            _lightBrightness.Value = Clamp(l.Brightness, 0, _maxBrightness);
            _lightSpeed.Value = Clamp(l.Speed, 0, _maxSpeed);

            int sel = -1;
            for (int i = 0; i < _lightMode.Items.Count; i++)
            {
                ModeItem mi = _lightMode.Items[i] as ModeItem;
                if (mi != null && mi.Value == l.Mode) { sel = i; break; }
            }
            if (sel >= 0) _lightMode.SelectedIndex = sel;

            ApplyModeOptions(l.Mode);      // seçenekleri gizle/göster (MainFormModeOptions.cs); profildeki değerlere dokunmaz
            _lightDir.SetSelected(l.Direction);
            _lightPower.Checked = l.Power == 0;
            _lightFull.Checked = l.FullColor != 0;
            _btnFg.BackColor = Color.FromArgb(l.R, l.G, l.B);
            _btnBg.BackColor = Color.FromArgb(l.BR, l.BG, l.BB);
            _btnFg.Text = "Ön renk  #" + HexColor(l.R, l.G, l.B);
            _btnBg.Text = "Arka renk  #" + HexColor(l.BR, l.BG, l.BB);
            _btnFg.ForeColor = Contrast(l.R, l.G, l.B);
            _btnBg.ForeColor = Contrast(l.BR, l.BG, l.BB);
            FillHexBoxes(l);

            UpdatePreviewDirection(l);
            _lightPreview.Light = l;
            _lightPreview.ModeName = LightModeName(l.Mode);
            _lightPreview.Invalidate();
            _loading = old;
        }

        private static decimal Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static string HexColor(int r, int g, int b)
        {
            return string.Format("{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        private static Color Contrast(int r, int g, int b)
        {
            int y = (r * 299 + g * 587 + b * 114) / 1000;
            return y > 140 ? Color.Black : Color.White;
        }

        private void LightChanged()
        {
            if (_loading) return;
            LightSetting l = Current.Light;
            ModeItem mi = _lightMode.SelectedItem as ModeItem;
            int oldMode = l.Mode;
            if (mi != null) l.Mode = mi.Value;
            if (l.Mode != oldMode) OnLightModeChanged(l); // seçenekler gizlenir/gösterilir; geçersiz yön ilk geçerli değere çekilir
            l.Brightness = (int)_lightBrightness.Value;
            l.Speed = (int)_lightSpeed.Value;
            // Yön, yalnızca kullanıcı bir kart seçince ya da mod değişiminde (OnLightModeChanged) güncellenir.
            l.Power = _lightPower.Checked ? 0 : 1;
            l.FullColor = _lightFull.Checked ? 1 : 0;
            Store.Save(_data);
            UpdatePreviewDirection(l);
            _lightPreview.Light = l;
            _lightPreview.ModeName = LightModeName(l.Mode);
            _lightPreview.Invalidate();
            ScheduleLiveLight();
        }

        private void PickColor(bool foreground)
        {
            LightSetting l = Current.Light;
            using (ColorDialog d = new ColorDialog())
            {
                d.FullOpen = true;
                d.Color = foreground ? Color.FromArgb(l.R, l.G, l.B) : Color.FromArgb(l.BR, l.BG, l.BB);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (foreground) { l.R = d.Color.R; l.G = d.Color.G; l.B = d.Color.B; }
                else { l.BR = d.Color.R; l.BG = d.Color.G; l.BB = d.Color.B; }
                Store.Save(_data);
                FillLightUiInternal();
                ScheduleLiveLight();
            }
        }

        // ============================================================ device

        private void TryConnect(bool showError)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (_dev != null) { _dev.Close(); _dev = null; }
                // Test kipi: W75_CIHAZSIZ=1 ise cihaza hiç bağlanılmaz (ekran görüntüsü/arayüz denemeleri için).
                if (Environment.GetEnvironmentVariable("W75_CIHAZSIZ") == "1")
                {
                    // W75_SIM_CIHAZ_YOK=1 (yalnızca test kipinde): "klavye takılı değil" durumunu, gerçek aygıta hiç dokunmadan simüle eder.
                    if (Environment.GetEnvironmentVariable("W75_SIM_CIHAZ_YOK") == "1")
                    {
                        _deviceStatus.Text = "●  Cihaz bulunamadı — USB ile bağlayın";
                        _deviceStatus.ForeColor = Red;
                        UpdateDevInfo();
                        if (showError)
                            MessageBox.Show(this, "W75 klavye bulunamadı.\n\nKlavyenin USB ile bağlı ve özel/mod config modunda olduğundan emin olun.", "Cihaz bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _deviceStatus.Text = "●  Cihaz aranmadı (test kipi)";
                    _deviceStatus.ForeColor = Muted;
                    UpdateDevInfo();
                    return;
                }
                _dev = W75Device.Open();
                if (_dev == null)
                {
                    _deviceStatus.Text = "●  Cihaz bulunamadı — USB ile bağlayın";
                    _deviceStatus.ForeColor = Red;
                    UpdateDevInfo();
                    if (showError)
                        MessageBox.Show(this, "W75 klavye bulunamadı.\n\nKlavyenin USB ile bağlı ve özel/mod config modunda olduğundan emin olun.", "Cihaz bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DeviceStatus s = _dev.ReadStatus();
                _deviceStatus.Text = "●  Bağlı — " + (string.IsNullOrEmpty(s.Product) ? "W75" : s.Product) + "  " + s.Version;
                _deviceStatus.ForeColor = Green;
                try
                {
                    int[] modes = _dev.ReadLightModes(false);
                    if (modes != null && modes.Length > 0) _lightModes = modes;
                }
                catch { }
                try
                {
                    int[] lim = _dev.ReadLightLimits(false);
                    if (lim != null && lim.Length >= 2) { _maxBrightness = lim[0]; _maxSpeed = lim[1]; }
                }
                catch { }
                UpdateDevInfo();
                FillLightModesInternal();
                FillLightUiInternal();
                Log("Cihaz bağlandı: " + s.Product + " · " + s.Version + " · maks. " + (s.MaxTravel * s.Unit).ToString("0.00", CultureInfo.InvariantCulture) + " mm");
            }
            catch (Exception ex)
            {
                _deviceStatus.Text = "●  Bağlantı hatası";
                _deviceStatus.ForeColor = Red;
                Log("Bağlantı hatası: " + ex.Message);
                if (showError) MessageBox.Show(this, "Bağlantı hatası:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _busy = false;
            }
        }

        private void UpdateDevInfo()
        {
            DeviceStatus s = _dev != null ? _dev.Status : null;
            _devProduct.Text = s != null && !string.IsNullOrEmpty(s.Product) ? s.Product + "  (VID 2E3C / PID C368)" : "—";
            _devVersion.Text = s != null && !string.IsNullOrEmpty(s.Version) ? s.Version : "—";
            _devTravel.Text = s != null ? (s.MaxTravel * s.Unit).ToString("0.00", CultureInfo.InvariantCulture) + " mm" : "—";
            _devUnit.Text = s != null ? s.Unit.ToString("0.00", CultureInfo.InvariantCulture) + " mm" : "—";
            // Polling klavyeden okunup gösterilmez: ReadPollRate cevap gelmezse sahte 1 döndürür. Yönlendirme metni gösterilir.
            _devPoll.Text = "Aktivasyon > Polling Rate bölümünden yönetilir";

            double maxMm = (s != null ? s.MaxTravel * s.Unit : 3.4);
            _numTravel.Maximum = (decimal)Math.Max(0.1, maxMm);
            _numDeadTop.Maximum = (decimal)Math.Min(1.0, Math.Max(0.1, maxMm)); // ölü bölge kaydırıcıları 0–1,00 mm
            _numDeadBottom.Maximum = (decimal)Math.Min(1.0, Math.Max(0.1, maxMm));
            RefreshTravelVisuals();
        }

        private bool EnsureDevice()
        {
            if (_dev != null && _dev.IsOpen) return true;
            TryConnect(true);
            return _dev != null && _dev.IsOpen;
        }

        private void RunDevice(MethodInvoker action)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                Cursor = Cursors.WaitCursor;
                action();
            }
            catch (Exception ex)
            {
                Log("HATA: " + ex.Message);
                MessageBox.Show(this, "İşlem başarısız:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _busy = false;
            }
        }

        private void WriteToDevice()
        {
            if (!SlotGuard(Current.Slot)) return; // slot sınırı: cihaza hiçbir şey gönderilmez
            if (TrySimulateWrite()) return; // yalnızca test kipi + W75_SIM_YAZ=1: komut göndermez
            if (!EnsureDevice()) return;
            CancelPendingActivate();
            Profile p = Current;
            if (MessageBox.Show(this,
                "'" + p.Name + "' profili klavyenin " + (DeviceProfiles.ToDevice(p.Slot) + ". profiline") + " yazılacak.\n\n" +
                "Yazma sırasında USB kablosunu çıkarmayın. Devam edilsin mi?",
                "Cihaza yaz", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            RunDevice(delegate
            {
                Log("Yazma başladı: " + p.Name + " → " + DeviceProfiles.Describe(p.Slot));
                DeviceProfiles.Select(_dev, p.Slot);
                MarkActive(p.Slot);
                // Kullanıcının değiştirmediği hiçbir slot cihazdan okunan değerden farklı yazılmasın: önce cihazın mevcut haritası okunur.
                byte[] devBase = _dev.TryReadKeymap(false);
                if (devBase == null)
                    throw new InvalidOperationException("Cihazdaki tuş haritası tam okunamadı; tuş haritası ve ayarlar yazılmadı. Birkaç saniye bekleyip tekrar deneyin.");
                foreach (string w in Keymap.FindDeviceDifferences(p, devBase)) Log("UYARI: " + w);
                byte[] baseMap = Keymap.BuildBase(p, devBase);
                _dev.WriteKeymap(false, baseMap);
                _dev.WriteKeymap(true, Keymap.BuildFn(p));
                WriteTriggerGroups(p);
                _dev.WriteDeadband(Keymap.BuildDeadband(p));
                _dev.WriteLight(false, p.Light);
                Thread.Sleep(250);

                // Erken kontrol (yazmadan ~250 ms sonra, USB düşmeden ÖNCE): yalnızca günlük için ve Settle süresinin eskisi gibi kalması
                // için korunur; karar vermez. Asıl doğrulama bağlantı yenilendikten sonra WriteVerifier ile yapılır.
                byte[] check = _dev.ReadKeymap(false);
                if (BytesEqual(check, baseMap)) Log("Ön kontrol: tuş haritası yazmadan hemen sonra aynen okundu (karar verilmez; asıl doğrulama bağlantı yenilenince).");
                else Log("Ön kontrol: tuş haritası yazmadan hemen sonra farklı/eksik okundu (karar verilmez; asıl doğrulama bağlantı yenilenince).");

                // Aktivasyon + ölü bölge (TriggerVerify, 3 sn bekler, bağlantıyı yeniler). Fark satırları kutuda kullanılmak üzere yakalanır.
                List<string> trigDiffs = new List<string>();
                Action<string> capture = delegate(string s)
                {
                    Log(s);
                    string d = WriteVerifier.TriggerDiffText(s);
                    if (d != null) trigDiffs.Add(d);
                };
                TriggerVerifyResult tv = TriggerVerify.Run(_dev, p, Unit, capture);

                // Bağlantı teyidi + tuş haritası (temel + Fn) + ışık: Settle sonrası okunur (polling ve profil seçimi doğrulanmaz).
                WriteCheck chk = WriteVerifier.Run(_dev, baseMap, Keymap.BuildFn(p), p.Light.Clone(), Log);
                WriteOutcome oc = WriteVerifier.Compose(chk, tv, trigDiffs);

                if (oc.Mark) MarkWritten(p); // profil listesi: "Cihazda güncel"
                else Log("Yazım durumu güncellenmedi: " + oc.Reason);

                Log("Yazma tamamlandı: " + p.Name);
                MessageBox.Show(this, oc.Text, "Cihaza yaz", MessageBoxButtons.OK, oc.Icon);
            });

            RefreshDeviceInfo();
        }

        // Cihaza Yaz bittikten sonra (cihaz geri geldikten sonra) Cihaz sekmesindeki durum satırlarını yeniler.
        // Yalnızca mevcut okuma metodunu kullanır (ReadStatus).
        private void RefreshDeviceInfo()
        {
            if (_busy || _dev == null) return;
            _busy = true;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (!_dev.IsOpen || !_dev.IsPresent())
                {
                    Log("Cihaz durumu yenilenemedi: cihaz bağlı görünmüyor, bağlantı yeniden deneniyor.");
                    if (!TriggerVerify.EnsureLink(_dev, Log)) { Log("Cihaz durumu yenilenemedi: cihaz geri gelmedi. “Durumu Yenile” ile tekrar deneyin."); return; }
                }
                DeviceStatus s = _dev.ReadStatus();
                _deviceStatus.Text = "●  Bağlı — " + (string.IsNullOrEmpty(s.Product) ? "W75" : s.Product) + "  " + s.Version;
                _deviceStatus.ForeColor = Green;
                UpdateDevInfo();
                Log("Cihaz durumu yenilendi.");
            }
            catch (Exception ex)
            {
                Log("Cihaz durumu yenilenemedi: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
                _busy = false;
            }
        }

        private void WriteTriggerGroups(Profile p)
        {
            Dictionary<string, List<int>> groups = new Dictionary<string, List<int>>();
            Dictionary<string, TriggerSetting> settings = new Dictionary<string, TriggerSetting>();
            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                TriggerSetting t = p.EffectiveTrigger(k.Index);
                string key = t.Mode + "|" + t.Travel + "|" + t.Interval1 + "|" + t.Interval2;
                if (!groups.ContainsKey(key)) { groups[key] = new List<int>(); settings[key] = t; }
                groups[key].Add(k.Index);
            }
            foreach (KeyValuePair<string, List<int>> kv in groups)
            {
                TriggerSetting t = settings[kv.Key];
                _dev.WriteTrigger(t.Mode, kv.Value, t.Travel, t.Interval1, t.Interval2);
            }
            Log("Aktivasyon: " + groups.Count + " grup gönderildi.");
        }

        private void ReadFromDevice()
        {
            if (!SlotGuard(Current.Slot)) return; // slot sınırı
            bool simRead = TestMode && SimWrite; // test kipi + W75_SIM_YAZ=1: aynı onay kutusu gösterilir, ama komut gitmez
            if (!simRead && !EnsureDevice()) return;
            Profile p = Current;
            if (MessageBox.Show(this,
                "Klavyenin " + DeviceProfiles.ToDevice(p.Slot) + ". profili okunacak ve '" + p.Name + "' profilinin üzerine yazılacak.\n\nDevam edilsin mi?",
                "Cihazdan oku", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (simRead)
            {
                Log("Cihazdan Oku (test kipi, simülasyon): SetProfile(" + DeviceProfiles.ToDevice(p.Slot) + ") ve okuma GÖNDERİLMEDİ (" + DeviceProfiles.Describe(p.Slot) + ")");
                return;
            }
            RunDevice(delegate
            {
                Log("Okuma başladı: " + DeviceProfiles.Describe(p.Slot));
                DeviceProfiles.Select(_dev, p.Slot);
                MarkActive(p.Slot);
                byte[] kb = _dev.ReadKeymap(false);
                byte[] kf = _dev.ReadKeymap(true);
                Keymap.DecodeBase(p, kb);
                Keymap.DecodeFn(p, kf);

                // Zaman aşımına düşen okumalar (null) için yedek değer kaydedilmez; o tuşun eski değeri korunur.
                int nKeys = KeyboardLayout.Keys.Count;
                TriggerSetting[] oldSet = new TriggerSetting[nKeys];
                for (int i = 0; i < nKeys; i++) oldSet[i] = p.EffectiveTrigger(KeyboardLayout.Keys[i].Index);

                TriggerSetting[] all = new TriggerSetting[nKeys]; // null = okunamadı
                for (int i = 0; i < nKeys; i++)
                {
                    for (int a = 1; a <= 3 && all[i] == null; a++)
                    {
                        try { all[i] = _dev.TryReadTrigger(KeyboardLayout.Keys[i].Index); }
                        catch (Exception ex) { Log("Okuma hatası: " + KeyboardLayout.Keys[i].Display + " (deneme " + a + "/3): " + ex.Message); }
                        Thread.Sleep(8);
                    }
                }

                byte[] db = null;
                for (int a = 1; a <= 3 && db == null; a++)
                {
                    try { db = _dev.TryReadDeadband(); }
                    catch (Exception ex) { Log("Ölü bölge okuma hatası (deneme " + a + "/3): " + ex.Message); }
                    Thread.Sleep(8);
                }

                Dictionary<string, int> counts = new Dictionary<string, int>();
                Dictionary<string, TriggerSetting> first = new Dictionary<string, TriggerSetting>();
                foreach (TriggerSetting t in all)
                {
                    if (t == null) continue;
                    string key = t.Mode + "|" + t.Travel + "|" + t.Interval1 + "|" + t.Interval2;
                    if (!counts.ContainsKey(key)) { counts[key] = 0; first[key] = t; }
                    counts[key] = counts[key] + 1;
                }
                string best = null; int bestCount = -1;
                foreach (KeyValuePair<string, int> kv in counts)
                    if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
                if (best != null)
                {
                    TriggerSetting def0 = first[best];
                    p.DefaultMode = def0.Mode;
                    p.DefaultTravel = def0.Travel;
                    p.DefaultI1 = def0.Interval1;
                    p.DefaultI2 = def0.Interval2;
                }
                p.Triggers.Clear();

                int unreadable = 0;
                if (db == null) Log("Okunamadı: ölü bölge değerleri (tüm tuşlar), eski değerler korundu");
                for (int i = 0; i < nKeys; i++)
                {
                    KeyDef k = KeyboardLayout.Keys[i];
                    TriggerSetting t = all[i];
                    TriggerSetting o = oldSet[i];
                    bool keptAny = false;
                    int mode, travel, i1, i2, dt, dbb;
                    if (t != null) { mode = t.Mode; travel = t.Travel; i1 = t.Interval1; i2 = t.Interval2; }
                    else
                    {
                        mode = o.Mode; travel = o.Travel; i1 = o.Interval1; i2 = o.Interval2;
                        keptAny = true;
                        Log("Okunamadı: " + k.Display + ", eski değer korundu");
                    }
                    if (db != null) { dt = db[k.Index * 2]; dbb = db[k.Index * 2 + 1]; }
                    else { dt = o.DeadTop; dbb = o.DeadBottom; keptAny = true; }
                    if (keptAny) unreadable++;

                    bool differs = mode != p.DefaultMode || travel != p.DefaultTravel || i1 != p.DefaultI1 || i2 != p.DefaultI2;
                    if (differs || dt != 0 || dbb != 0)
                        p.SetTrigger(k.Index, mode, travel, i1, i2, dt, dbb);
                }

                p.Light = _dev.ReadLight(false);
                Store.Save(_data);
                LoadProfileUi();
                Log(string.Format("Okuma tamamlandı: {0} tuş ataması, {1} aktivasyon istisnası, ışık modu {2}.",
                    p.Keys.Count + p.FnKeys.Count, p.Triggers.Count, p.Light.Mode));
                string readNote = "";
                if (unreadable > 0)
                {
                    readNote = unreadable + " tuş okunamadı, eski değerler korundu";
                    Log(readNote);
                }
                MessageBox.Show(this, "Profil cihazdan okundu ve yerel dosyaya kaydedildi." + (readNote.Length > 0 ? "\n\n" + readNote + " (ayrıntı için Cihaz sekmesindeki günlüğe bakın)." : ""), "Cihazdan oku",
                    MessageBoxButtons.OK, unreadable > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            });
        }

        private void BackupDevice()
        {
            if (!SlotGuard(Current.Slot)) return; // slot sınırı
            if (!EnsureDevice()) return;
            RunDevice(delegate
            {
                Profile p = Current;
                DeviceProfiles.Select(_dev, p.Slot);
                MarkActive(p.Slot);
                DeviceBackup b = new DeviceBackup();
                b.Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                b.Product = _dev.Status.Product;
                b.Version = _dev.Status.Version;
                b.Slot = p.Slot;
                b.MaxTravel = _dev.Status.MaxTravel;
                b.Unit = _dev.Status.Unit;
                b.KeymapBase = Convert.ToBase64String(_dev.ReadKeymap(false));
                b.KeymapFn = Convert.ToBase64String(_dev.ReadKeymap(true));
                b.Deadband = Convert.ToBase64String(_dev.ReadDeadband());
                b.Light = _dev.ReadLight(false);
                b.Triggers = new List<TriggerOverride>();
                foreach (KeyDef k in KeyboardLayout.Keys)
                {
                    TriggerSetting t = _dev.ReadTrigger(k.Index);
                    TriggerOverride o = new TriggerOverride();
                    o.Index = k.Index; o.Mode = t.Mode; o.Travel = t.Travel; o.I1 = t.Interval1; o.I2 = t.Interval2;
                    b.Triggers.Add(o);
                }
                byte[] db = _dev.ReadDeadband();
                foreach (TriggerOverride o in b.Triggers)
                {
                    o.DeadTop = db[o.Index * 2];
                    o.DeadBottom = db[o.Index * 2 + 1];
                }
                // Polling yedeklenmez: tüm klavye için tek genel ayardır (genel.json) ve ReadPollRate başarısızlıkta sahte 1 döndürür.

                string path = Path.Combine(Store.BackupDir, "yedek-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(path, ser.Serialize(b), new UTF8Encoding(false));
                Log("Yedek alındı: " + path);
                MessageBox.Show(this, "Cihaz yedeği kaydedildi:\n" + path, "Yedek", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        // Yalnızca test kipinde (W75_CIHAZSIZ=1 ya da W75_SIM_YAZ=1 ile birlikte): W75_CIHAZ_YEDEK_DOSYA ayarlıysa dosya penceresi
        // açılmaz, bu yedek dosyası kullanılır (otomatik testler içindir; normal kullanımda etkisi yoktur).
        private static string TestDeviceBackupFile()
        {
            if (!TestMode && !SimWrite) return null;
            string p = Environment.GetEnvironmentVariable("W75_CIHAZ_YEDEK_DOSYA");
            return string.IsNullOrEmpty(p) ? null : p;
        }

        // Cihaz yedeğini geri yükler. Yedekteki polling değeri YOK SAYILIR (polling tüm klavye için tek genel ayardır,
        // yalnızca Aktivasyon > Polling Rate bölümünden yazılır); eski yedek dosyasında alan kalsa da klavyeye yazılmaz.
        private void RestoreDevice()
        {
            bool sim = TestMode && SimWrite; // test kipi + W75_SIM_YAZ=1: hiçbir komut gönderilmez
            if (!sim && !EnsureDevice()) return;
            string file = TestDeviceBackupFile();
            if (file == null)
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Cihaz yedeğini geri yükle";
                    d.InitialDirectory = Store.BackupDir;
                    d.Filter = "W75 yedeği (*.json)|*.json";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    file = d.FileName;
                }
            }
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                DeviceBackup b = ser.Deserialize<DeviceBackup>(File.ReadAllText(file, Encoding.UTF8));
                if (b == null) throw new InvalidDataException("Yedek okunamadı.");
                if (!DeviceLimits.SlotAllowed(b.Slot)) throw new InvalidDataException("Yedek slot " + b.Slot + " için alınmış. " + DeviceLimits.Message(b.Slot));
                if (MessageBox.Show(this, "Yedek cihaza geri yüklenecek. Mevcut ayarlar değişecek. Devam edilsin mi?", "Geri yükle",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (sim)
                {
                    Log("Yedek geri yükleme (test kipi, simülasyon): SetProfile(" + DeviceProfiles.ToDevice(b.Slot) + "), tuş haritası (temel + Fn), aktivasyon, ölü bölge, ışık GÖNDERİLMEDİ. " +
                        "Polling geri yüklenmez (yedekteki değer yok sayıldı; polling yalnızca Aktivasyon > Polling Rate ile yazılır).");
                    MessageBox.Show(this, "Test kipinde klavyeye hiçbir şey gönderilmedi.", "Geri yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                RunDevice(delegate
                {
                    DeviceProfiles.Select(_dev, b.Slot);
                    MarkActive(b.Slot);
                    _dev.WriteKeymap(false, Convert.FromBase64String(b.KeymapBase));
                    _dev.WriteKeymap(true, Convert.FromBase64String(b.KeymapFn));
                    if (b.Triggers != null)
                    {
                        Dictionary<string, List<int>> groups = new Dictionary<string, List<int>>();
                        Dictionary<string, TriggerOverride> map = new Dictionary<string, TriggerOverride>();
                        foreach (TriggerOverride t in b.Triggers)
                        {
                            string key = t.Mode + "|" + t.Travel + "|" + t.I1 + "|" + t.I2;
                            if (!groups.ContainsKey(key)) { groups[key] = new List<int>(); map[key] = t; }
                            groups[key].Add(t.Index);
                        }
                        foreach (KeyValuePair<string, List<int>> kv in groups)
                        {
                            TriggerOverride t = map[kv.Key];
                            _dev.WriteTrigger(t.Mode, kv.Value, t.Travel, t.I1, t.I2);
                        }
                    }
                    _dev.WriteDeadband(Convert.FromBase64String(b.Deadband));
                    if (b.Light != null) _dev.WriteLight(false, b.Light);
                    Thread.Sleep(200);
                    Log("Yedek geri yüklendi: " + file + " (polling geri yüklenmedi: tüm klavye için ayrı ayardır)");
                    MessageBox.Show(this, "Yedek cihaza geri yüklendi.", "Geri yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Yedek geri yüklenemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void Log(string msg)
        {
            if (_log == null) return;
            if (_log.TextLength > 200000) _log.Clear();
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { Store.Save(_data); } catch { }
            try { if (_dev != null) _dev.Close(); } catch { }
            base.OnFormClosing(e);
        }
    }

    public class LightPreview : Panel
    {
        public LightSetting Light;
        public string ModeName = "";
        public string DirArrow = ""; // seçili yönün oku (Statik modda boş)
        public string DirFlow = "duz"; // "duz": her tuşta aynı ok, "disa": ortadan iki yana dışa, "ice": köşelerden ortaya
        public string DirText = "";
        public bool FullColorUsed = true; // seçili mod Tam renk'i destekliyor mu (Statikte tek renk çizilir)
        public bool BgUsed = true;        // seçili mod Arka rengi kullanıyor mu

        private static readonly string[] Octants = { "→", "↘", "↓", "↙", "←", "↖", "↑", "↗" };

        // 8 yönlü ok: (vx, vy) ekran koordinatında (y aşağı); 0° = →, saat yönünde 45°'lik dilimler.
        private static string Octant(double vx, double vy)
        {
            double a = Math.Atan2(vy, vx) * 180.0 / Math.PI; // -180..180
            int k = (int)Math.Round(a / 45.0);
            return Octants[((k % 8) + 8) % 8];
        }

        // Tuş (r, c) için çizilecek ok. disa: orta sütunun solunda ←, sağında →, ortada ↔.
        // ice: merkeze doğru (8 yönlü) ok; merkez hücrede nokta. donus / donus-ters: merkez etrafında saat yönü / tersi teğet ok.
        private string ArrowAt(int r, int c, int rows, int cols)
        {
            if (DirFlow == "donus" || DirFlow == "donus-ters")
            {
                double rx = c - (cols - 1) / 2.0, ry = (r - (rows - 1) / 2.0) * 2.2; // 15x5 ızgara: dikeyi ağırlıklandır
                if (Math.Abs(rx) < 0.01 && Math.Abs(ry) < 0.01) return "•";
                bool cw = DirFlow == "donus";
                // ekran koordinatında (y aşağı) saat yönü teğeti: (-ry, rx); saat yönünün tersi: (ry, -rx)
                return cw ? Octant(-ry, rx) : Octant(ry, -rx);
            }
            if (DirFlow == "disa")
            {
                int mc = cols / 2;
                return c < mc ? "←" : (c > mc ? "→" : "↔");
            }
            if (DirFlow == "ice")
            {
                double dx = (cols - 1) / 2.0 - c, dy = (rows - 1) / 2.0 - r; // merkeze doğru
                if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) return "•";
                double ax = Math.Abs(dx), ay = Math.Abs(dy) * 2.2; // 15x5 ızgara: dikeyi ağırlıklandır
                bool horiz = ax > 0.01, vert = Math.Abs(dy) > 0.01;
                if (!vert || ay < ax * 0.4) return dx > 0 ? "→" : "←";
                if (!horiz || ax < ay * 0.4) return dy > 0 ? "↓" : "↑";
                if (dx > 0) return dy > 0 ? "↘" : "↗";
                return dy > 0 ? "↙" : "↖";
            }
            return DirArrow;
        }

        public LightPreview()
        {
            DoubleBuffered = true;
            BackColor = Theme.Inset;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (Light == null) return;

            Color fg = Color.FromArgb(Light.R, Light.G, Light.B);
            Color bg = Color.FromArgb(Light.BR, Light.BG, Light.BB);

            int cx = Width / 2;
            int cy = Height / 2 - 30;
            int kw = Math.Min(Width - 80, 640);
            int kh = (int)(kw * 0.36);
            if (kh > Height - 160) { kh = Height - 160; kw = (int)(kh / 0.36); }
            if (kw < 120) return;

            Rectangle body = new Rectangle(cx - kw / 2, cy - kh / 2, kw, kh);

            // glow
            if (Light.Power == 0)
            {
                for (int i = 8; i >= 1; i--)
                {
                    Rectangle gr = Rectangle.Inflate(body, i * 6, i * 6);
                    int alpha = (int)(8 + (9 - i) * 2.2);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Min(60, alpha), fg)))
                    using (System.Drawing.Drawing2D.GraphicsPath p = RoundRect(gr, 26))
                        g.FillPath(b, p);
                }
            }

            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 32, 42)))
            using (System.Drawing.Drawing2D.GraphicsPath p = RoundRect(body, 12))
                g.FillPath(b, p);

            float pad = Math.Max(8, kw * 0.025f);
            int cols = 15, rows = 5;
            float gap = Math.Max(2, kw * 0.006f);
            float cw = (body.Width - pad * 2 - gap * (cols - 1)) / cols;
            float ch = (body.Height - pad * 2 - gap * (rows - 1)) / rows;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = body.X + pad + c * (cw + gap);
                    float y = body.Y + pad + r * (ch + gap);
                    float t = (float)(r * cols + c) / (cols * rows);
                    Color kc;
                    if (Light.Power != 0) kc = Color.FromArgb(38, 42, 52);
                    else if (Light.FullColor != 0 && FullColorUsed) kc = Hsv(t * 360f, 0.75f, 0.9f);
                    else if (Light.Mode == 0) kc = fg; // Statik: tek renk (arka renk kullanılmaz)
                    else kc = Blend(fg, bg, t);
                    using (SolidBrush b = new SolidBrush(kc))
                    using (System.Drawing.Drawing2D.GraphicsPath p = RoundRect(new RectangleF(x, y, cw, ch), 3))
                        g.FillPath(b, p);
                    // Yön oku: her tuşta seçili yönün akış oku (duz: aynı ok; disa/ice: konuma göre).
                    if (Light.Power == 0 && DirArrow.Length > 0 && ch > 8)
                    {
                        int lum = (kc.R * 299 + kc.G * 587 + kc.B * 114) / 1000;
                        using (Font af = new Font("Segoe UI Symbol", Math.Max(6f, ch * 0.5f), FontStyle.Regular, GraphicsUnit.Pixel))
                        using (SolidBrush ab = new SolidBrush(lum > 140 ? Color.FromArgb(120, 20, 20, 20) : Color.FromArgb(150, 255, 255, 255)))
                        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString(ArrowAt(r, c, rows, cols), af, ab, new RectangleF(x, y, cw, ch), sf);
                    }
                }
            }

            using (Font f = new Font("Segoe UI Semibold", 14f))
                TextRenderer.DrawText(g, ModeName, f, new Rectangle(0, body.Bottom + 24, Width, 28), fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Font f = new Font("Segoe UI", 9f))
                TextRenderer.DrawText(g, "Parlaklık " + Light.Brightness + " / " + (Light.Power == 0 ? "Açık" : "Kapalı"), f,
                    new Rectangle(0, body.Bottom + 52, Width, 22), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Rectangle s1 = BgUsed ? new Rectangle(cx - 84, body.Bottom + 84, 76, 30) : new Rectangle(cx - 38, body.Bottom + 84, 76, 30);
            Rectangle s2 = new Rectangle(cx + 8, body.Bottom + 84, 76, 30);
            using (SolidBrush b = new SolidBrush(fg)) g.FillRectangle(b, s1);
            if (BgUsed) using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, s2);
            using (Pen p = new Pen(Color.FromArgb(60, 66, 82))) { g.DrawRectangle(p, s1); if (BgUsed) g.DrawRectangle(p, s2); }
            if (DirText.Length > 0)
                using (Font f = new Font("Segoe UI", 9f))
                    TextRenderer.DrawText(g, DirArrow + "  " + DirText, f, new Rectangle(0, body.Bottom + 124, Width, 22),
                        Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private static Color Hsv(float h, float s, float v)
        {
            int i = (int)Math.Floor(h / 60f) % 6;
            float f = h / 60f - (float)Math.Floor(h / 60f);
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);
            float r = 0, g = 0, b = 0;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(RectangleF r, float radius)
        {
            System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public class PromptForm : Form
    {
        private TextBox _box;

        public static string Show(IWin32Window owner, string title, string value)
        {
            using (PromptForm f = new PromptForm())
            {
                f.Text = title;
                f._box.Text = value;
                return f.ShowDialog(owner) == DialogResult.OK ? f._box.Text : null;
            }
        }

        public PromptForm()
        {
            AppIcon.Apply(this); WindowTheme.Setup(this);
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 120);
            Font = new Font("Segoe UI", 9f);

            _box = new TextBox();
            _box.MaxLength = 32; // çok uzun adlar listede ve düzende taşar
            _box.Bounds = new Rectangle(16, 20, 348, 26);
            _box.BackColor = Theme.Inset;
            _box.ForeColor = Theme.Text;
            _box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_box);

            Button ok = new Button();
            ok.Text = "Tamam";
            ok.DialogResult = DialogResult.OK;
            ok.Bounds = new Rectangle(200, 64, 78, 30);
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Theme.AccentText;
            ok.FlatAppearance.BorderSize = 0;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Vazgeç";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Bounds = new Rectangle(286, 64, 78, 30);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Theme.Raised;
            cancel.ForeColor = Theme.Text;
            cancel.FlatAppearance.BorderColor = Theme.Border;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}

