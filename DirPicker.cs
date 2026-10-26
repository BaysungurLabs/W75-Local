using System;
using System.Collections.Generic;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Işık yönü değerlerinin anlamı (gömülü yon-etiketleri.json). Yön kullanan beş modun hepsinde kullanıcı tarafından
    // gerçek klavyede doğrulandı; 'modlar' bir modda anlam farklıysa (Radar) o modun değerlerini taşır. Hangi modda hangi değerlerin
    // gösterileceği mod-secenekleri.json'dadır (ModeOptions). Dosya okunamazsa yerleşik varsayılanlar kullanılır.
    public static class DirLabels
    {
        // Flow: önizleme çizimi. "duz" = her tuşta aynı ok, "disa" = ortadan iki yana dışa, "ice" = köşelerden ortaya,
        // "donus" = saat yönü, "donus-ters" = saat yönünün tersi.
        public class Entry { public string Arrow; public string Text; public string Short; public string Flow; }

        private static readonly string[] DefaultArrows = { "→", "←", "↑", "↓", "← →", "→ ←" };
        private static readonly string[] DefaultTexts = { "Soldan sağa", "Sağdan sola", "Aşağıdan yukarıya", "Yukarıdan aşağıya", "Ortadan iki yana, dışa doğru", "Köşelerden ortaya, içe doğru" };
        private static readonly string[] DefaultFlows = { "duz", "duz", "duz", "duz", "disa", "ice" };
        private static Dictionary<string, object> _root;
        private static bool _loaded;

        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            try { _root = new JavaScriptSerializer().DeserializeObject(KeyboardLayout.ReadResource("yon-etiketleri.json")) as Dictionary<string, object>; }
            catch { _root = null; }
        }

        private static object Dig(object o, params string[] path)
        {
            foreach (string k in path)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null || !d.TryGetValue(k, out o)) return null;
            }
            return o;
        }

        public static Entry Get(int mode, int value)
        {
            Ensure();
            string v = value.ToString();
            object e = Dig(_root, "modlar", mode.ToString(), v);
            if (e == null) e = Dig(_root, "varsayilan", v);
            int i = ((value % DefaultArrows.Length) + DefaultArrows.Length) % DefaultArrows.Length;
            Entry r = new Entry();
            r.Arrow = Dig(e, "ok") as string;
            r.Text = Dig(e, "metin") as string;
            r.Short = Dig(e, "kisa") as string;
            r.Flow = Dig(e, "akis") as string;
            if (string.IsNullOrEmpty(r.Arrow)) r.Arrow = DefaultArrows[i];
            if (string.IsNullOrEmpty(r.Text)) r.Text = DefaultTexts[i];
            if (string.IsNullOrEmpty(r.Short)) r.Short = r.Text;
            if (r.Flow != "duz" && r.Flow != "disa" && r.Flow != "ice" && r.Flow != "donus" && r.Flow != "donus-ters") r.Flow = DefaultFlows[i];
            return r;
        }
    }

    // Işık yönü seçici: ok kartları, seçili olan vurgulu. Gösterilen kartlar modun izin verdiği yön değerleridir
    // (ModeOptions); kart sırası tablodaki sıradır. Ipucu kartın yön açıklamasını gösterir.
    public class DirPicker : Control
    {
        public Color CardBack = Theme.Raised;
        public Color CardBorder = Theme.Border;
        public Color SelBack = Theme.AccentSoft;
        public Color AccentColor = Theme.Accent;
        public Color TextColor = Theme.Text;
        public Color MutedColor = Theme.Muted;

        private int _mode, _selValue = -1, _hover = -1;
        private int[] _values = new int[0];
        private readonly ToolTip _tip = new ToolTip();

        // Yalnızca KULLANICI bir kart seçince tetiklenir (SetSelected tetiklemez).
        public event EventHandler ValueChanged;

        public DirPicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 8f);
            AccessibleName = "Yön seçici";
        }

        public int Mode { get { return _mode; } }
        public int[] Values { get { return _values; } }
        public int SelectedValue { get { return _selValue; } }

        // Mod değişince gösterilen kartlar tazelenir. Seçili değer değişmez (modda yoksa hiçbir kart vurgulanmaz).
        public void SetMode(int mode)
        {
            _mode = mode;
            _values = ModeOptions.Get(mode).Dirs;
            string list = string.Join(",", Array.ConvertAll(_values, delegate(int v) { return v.ToString(); }));
            AccessibleDescription = list;
            AccessibleName = "Yön seçici: " + list; // erişilebilirlik / otomatik test: gösterilen yön değerleri
            _hover = -1;
            Invalidate();
        }

        // Program tarafından seçim (olay yok).
        public void SetSelected(int value)
        {
            _selValue = value;
            Invalidate();
        }

        private void UserSelect(int value)
        {
            if (value == _selValue) return;
            _selValue = value;
            Invalidate();
            EventHandler h = ValueChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private RectangleF Card(int i)
        {
            int n = _values.Length;
            float gap = 6f;
            float w = (Width - gap * (n - 1)) / Math.Max(1, n);
            if (n <= 3) w = Math.Min(120f, w); // az kartlı modlarda kartlar geniş olur, başlık yazılabilir
            return new RectangleF(i * (w + gap), 0, w, Height - 1);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _values.Length; i++) if (Card(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h == _hover) return;
            _hover = h;
            if (h >= 0)
            {
                DirLabels.Entry en = DirLabels.Get(_mode, _values[h]);
                _tip.SetToolTip(this, "Yön " + _values[h] + ": " + en.Text);
            }
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int h = HitTest(e.Location);
            if (h >= 0) UserSelect(_values[h]);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_values.Length == 0) return;
            int cur = Array.IndexOf(_values, _selValue);
            if (e.KeyCode == Keys.Left) { UserSelect(_values[Math.Max(0, cur - 1)]); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { UserSelect(_values[Math.Min(_values.Length - 1, cur + 1)]); e.Handled = true; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int n = _values.Length;
            using (Font arrowFont = new Font("Segoe UI Symbol", 17f, FontStyle.Regular, GraphicsUnit.Point))
            using (Font arrowFont2 = new Font("Segoe UI Symbol", 12f, FontStyle.Regular, GraphicsUnit.Point)) // çift oklu kartlar (ortadan dışa / içe)
            using (Font small = new Font("Segoe UI", 7.5f))
            {
                for (int i = 0; i < n; i++)
                {
                    RectangleF c = Card(i);
                    int value = _values[i];
                    bool sel = value == _selValue;
                    DirLabels.Entry en = DirLabels.Get(_mode, value);
                    using (System.Drawing.Drawing2D.GraphicsPath path = Round(Rectangle.Round(c), 6))
                    {
                        using (SolidBrush b = new SolidBrush(sel ? SelBack : (i == _hover ? Theme.SelRow : CardBack))) g.FillPath(b, path);
                        using (Pen p = new Pen(sel ? AccentColor : CardBorder, sel ? 2f : 1f)) g.DrawPath(p, path);
                    }
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                        sf.Trimming = StringTrimming.EllipsisCharacter;
                        RectangleF ar = new RectangleF(c.X, c.Y + 4, c.Width, c.Height * 0.62f);
                        using (SolidBrush b = new SolidBrush(sel ? AccentColor : TextColor)) g.DrawString(en.Arrow, en.Arrow.Length > 1 ? arrowFont2 : arrowFont, b, ar, sf);
                        RectangleF tr = new RectangleF(c.X + 2, c.Y + c.Height * 0.62f, c.Width - 4, c.Height * 0.36f);
                        // Az kartlı modlarda kart geniş: kısa açıklama; çok kartlı modlarda "Yön N" (tam açıklama ipucunda).
                        string cap = n <= 3 ? en.Short : "Yön " + value;
                        using (SolidBrush b = new SolidBrush(sel ? AccentColor : MutedColor)) g.DrawString(cap, small, b, tr, sf);
                    }
                }
            }
            if (Focused)
                using (Pen p = new Pen(Color.FromArgb(90, AccentColor)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                    g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        private static System.Drawing.Drawing2D.GraphicsPath Round(Rectangle r, int rad)
        {
            int d = rad * 2;
            System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
