using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Tuşun altına yazılacak küçük değer (isteğe bağlı, Aktivasyon sayfalarındaki klavye için).
    public class KeyCaption
    {
        public string Text;      // birden çok satır olabilir
        public bool Highlight;   // true = turuncu (varsayılandan farklı), false = soluk gri
    }

    public class KeyboardPanel : Panel
    {
        public Profile Profile;
        public int Layer; // 0 = base, 1 = fn
        public int SelectedIndex = -1;
        public event EventHandler KeySelected;

        // Çoklu seçim kipi: tıklayınca tuş seçilir, tekrar tıklayınca seçim kalkar.
        public bool MultiSelect;
        public List<int> SelectedList = new List<int>();
        public event EventHandler SelectionListChanged;
        // Verilirse her tuşun altına küçük bir değer yazılır.
        public Func<KeyDef, KeyCaption> Caption;

        private class HitKey
        {
            public int Index;
            public RectangleF Rect;
        }

        private List<HitKey> _hits = new List<HitKey>();
        private int _hover = -1;

        private static readonly Color Bg = Theme.Bg; // yalnızca tuval zemini; tuş renkleri aynı
        private static readonly Color KeyFill = Color.FromArgb(35, 39, 51);
        private static readonly Color KeyBorder = Color.FromArgb(52, 59, 76);
        private static readonly Color KeyText = Color.FromArgb(201, 206, 220);
        private static readonly Color RemapFill = Color.FromArgb(58, 42, 24);
        private static readonly Color RemapBorder = Color.FromArgb(255, 184, 107);
        private static readonly Color FnFill = Color.FromArgb(22, 48, 59);
        private static readonly Color FnBorder = Color.FromArgb(91, 200, 255);
        private static readonly Color TriggerDot = Color.FromArgb(139, 124, 255);

        public KeyboardPanel()
        {
            DoubleBuffered = true;
            BackColor = Bg;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = -1;
            foreach (HitKey h in _hits)
            {
                if (h.Rect.Contains(e.Location)) { idx = h.Index; break; }
            }
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            foreach (HitKey h in _hits)
            {
                if (h.Rect.Contains(e.Location))
                {
                    if (MultiSelect)
                    {
                        if (SelectedList.Contains(h.Index)) SelectedList.Remove(h.Index);
                        else SelectedList.Add(h.Index);
                        Invalidate();
                        if (SelectionListChanged != null) SelectionListChanged(this, EventArgs.Empty);
                        return;
                    }
                    SelectedIndex = h.Index;
                    Invalidate();
                    if (KeySelected != null) KeySelected(this, EventArgs.Empty);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            _hits.Clear();

            KeyboardLayout.Load();
            if (KeyboardLayout.Keys.Count == 0) return;

            float availW = Width - 24;
            float availH = Height - 24;
            if (availW < 100 || availH < 80) return;
            float scale = Math.Min(availW / (float)KeyboardLayout.Width, availH / (float)KeyboardLayout.Height);
            float ox = (Width - (float)KeyboardLayout.Width * scale) / 2f;
            float oy = (Height - (float)KeyboardLayout.Height * scale) / 2f;

            foreach (KeyDef k in KeyboardLayout.Keys)
            {
                RectangleF r = new RectangleF(
                    ox + (float)k.X * scale,
                    oy + (float)k.Y * scale,
                    Math.Max(2, (float)k.W * scale - 2.2f),
                    Math.Max(2, (float)k.H * scale - 2.2f));
                _hits.Add(new HitKey { Index = k.Index, Rect = r });

                bool isKnob = k.Code == "RotaryKnob";
                bool selected = MultiSelect ? SelectedList.Contains(k.Index) : k.Index == SelectedIndex;
                bool hover = k.Index == _hover;

                bool remapped = Profile != null && Profile.FindKey(k.Index, Layer == 1) != null;
                bool customTrigger = Profile != null && Profile.FindTrigger(k.Index) != null;

                Color fill = KeyFill;
                Color border = KeyBorder;
                if (remapped)
                {
                    fill = Layer == 1 ? FnFill : RemapFill;
                    border = Layer == 1 ? FnBorder : RemapBorder;
                }
                if (hover) fill = Lighten(fill, 18);
                if (MultiSelect && selected) fill = Lighten(fill, 22);

                using (GraphicsPath path = RoundRect(r, isKnob ? r.Width / 2f : 5f * scale))
                {
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (Pen p = new Pen(selected ? Color.White : border, selected ? (MultiSelect ? 2.8f : 2.2f) : 1.1f)) g.DrawPath(p, path);
                }

                if (customTrigger)
                {
                    float d = Math.Max(4f, 4f * scale);
                    using (SolidBrush b = new SolidBrush(TriggerDot))
                        g.FillEllipse(b, r.Right - d - 4f, r.Top + 4f, d, d);
                }

                string label = DisplayName(k);
                if (label.Length == 0) continue;

                KeyCaption cap = Caption != null ? Caption(k) : null;
                float fontSize = 8.2f * Math.Max(0.72f, Math.Min(1.25f, scale));
                if (isKnob) fontSize = 7.0f * Math.Max(0.8f, Math.Min(1.2f, scale));
                if (cap != null) fontSize *= 0.85f;
                float maxWidth = Math.Max(10f, r.Width - 6f);
                Font font = null;
                while (true)
                {
                    font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Point);
                    SizeF measured = g.MeasureString(label, font);
                    if (measured.Width <= maxWidth || fontSize <= 5.2f) break;
                    font.Dispose();
                    fontSize -= 0.4f;
                }
                using (font)
                using (SolidBrush tb = new SolidBrush(selected ? Color.White : KeyText))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    sf.Trimming = StringTrimming.None;
                    RectangleF tr = new RectangleF(r.X + 2, r.Y + 1, r.Width - 4, cap != null ? r.Height * 0.46f : r.Height - 2);
                    g.DrawString(label, font, tb, tr, sf);
                }

                if (cap != null && !string.IsNullOrEmpty(cap.Text))
                {
                    // Yer yetmiyorsa (tuş alçaksa) ikinci satır (basma/bırakma) gizlenir; ana değer her zaman görünür.
                    string capText = cap.Text;
                    bool tight = r.Height < 40f;
                    if (tight)
                    {
                        int nl = capText.IndexOf('\n');
                        if (nl >= 0) capText = capText.Substring(0, nl);
                    }
                    float cs = (tight ? 7.4f : 6.4f) * Math.Max(0.8f, Math.Min(1.2f, scale));
                    float csMin = tight ? 5.6f : 4.4f;
                    Font cf = null;
                    while (true)
                    {
                        cf = new Font("Segoe UI", cs, FontStyle.Regular, GraphicsUnit.Point);
                        SizeF m = g.MeasureString(capText, cf);
                        if (m.Width <= r.Width - 2 || cs <= csMin) break;
                        cf.Dispose();
                        cs -= 0.3f;
                    }
                    using (cf)
                    using (SolidBrush cb = new SolidBrush(cap.Highlight ? RemapBorder : Color.FromArgb(112, 118, 134)))
                    {
                        StringFormat cfmt = new StringFormat();
                        cfmt.Alignment = StringAlignment.Center;
                        cfmt.LineAlignment = StringAlignment.Center;
                        cfmt.FormatFlags = StringFormatFlags.NoWrap;
                        RectangleF cr = new RectangleF(r.X + 1, r.Y + r.Height * 0.42f, r.Width - 2, r.Height * 0.56f);
                        g.DrawString(capText, cf, cb, cr, cfmt);
                    }
                }
            }
        }

        private static Color Lighten(Color c, int amount)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + amount),
                Math.Min(255, c.G + amount),
                Math.Min(255, c.B + amount));
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0) d = 0.1f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static string DisplayName(KeyDef k)
        {
            return KeyLabels.Cap(k); // tuş yazıları seçili düzene (TR/US) göre
        }
    }
}

