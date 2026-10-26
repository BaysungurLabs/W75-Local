using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // SOCD sekmesindeki seçilebilir kart (tuş kutusu ve model seçenekleri). GDI+ ile çizilir.
    public class SocdCard : Control
    {
        private static readonly Color Fill = Theme.Raised;
        private static readonly Color FillSel = Theme.AccentSoft;
        private static readonly Color Border = Theme.Border;
        private static readonly Color Accent = Theme.Accent;
        private static readonly Color Pick = Theme.Cyan;

        public string Title = "";
        public string Subtitle = "";
        public float TitleSize = 10f;
        public float SubtitleSize = 8f;
        public bool SubtitleFirst;     // true: küçük başlık üstte, büyük değer altta (tuş kutuları)
        public bool Selected;          // turuncu vurgu
        public bool Active;            // mavi çerçeve (klavyeden tuş seçme kipi)

        public SocdCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Size = new Size(200, 56);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            RectangleF r = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f);
            Color border = Active ? Pick : (Selected ? Accent : Border);
            using (GraphicsPath path = RoundRect(r, 8f))
            {
                using (SolidBrush b = new SolidBrush(Selected ? FillSel : Fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border, (Active || Selected) ? 2f : 1.2f)) g.DrawPath(p, path);
            }

            Color titleColor = Enabled ? Theme.Text : Theme.Disabled;
            Color subColor = Enabled ? Theme.Muted : Theme.Disabled;
            using (Font tf = new Font("Segoe UI Semibold", TitleSize))
            using (Font sf = new Font("Segoe UI", SubtitleSize))
            using (SolidBrush tb = new SolidBrush(titleColor))
            using (SolidBrush sb = new SolidBrush(subColor))
            {
                StringFormat fmt = new StringFormat();
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                RectangleF inner = new RectangleF(10, 5, Width - 20, Height - 10);
                if (SubtitleFirst)
                {
                    g.DrawString(Subtitle, sf, sb, new RectangleF(inner.X, inner.Y, inner.Width, 16), fmt);
                    StringFormat c = new StringFormat();
                    c.LineAlignment = StringAlignment.Center;
                    c.Trimming = StringTrimming.EllipsisCharacter;
                    c.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(Title, tf, tb, new RectangleF(inner.X, inner.Y + 14, inner.Width, inner.Height - 14), c);
                }
                else
                {
                    float th = g.MeasureString(Title, tf).Height;
                    g.DrawString(Title, tf, tb, new RectangleF(inner.X, inner.Y, inner.Width, th), fmt);
                    g.DrawString(Subtitle, sf, sb, new RectangleF(inner.X, inner.Y + th + 1, inner.Width, inner.Height - th - 1), fmt);
                }
            }
        }

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
    }
}
