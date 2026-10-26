using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Koyu temalı NumericUpDown: beyaz sistem ok düğmeleri gizlenir, yerine palet renkli iki küçük ok çizilir.
    // Değer girişi, klavyeyle artırma/azaltma (Yukarı/Aşağı), fare tekerleği ve Minimum/Maksimum davranışı NumericUpDown'ınkiyle
    // aynıdır; oklara tıklamak UpButton()/DownButton() çağırır. Pasifken sistem gri metni yerine okunaklı metin çizilir.
    public class DarkNumeric : NumericUpDown
    {
        private const int ArrowW = 16;

        public DarkNumeric()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BorderStyle = BorderStyle.None;
        }

        private TextBox Edit
        {
            get { foreach (Control c in Controls) { TextBox t = c as TextBox; if (t != null) return t; } return null; }
        }

        private void Prepare()
        {
            try
            {
                foreach (Control c in Controls) if (!(c is TextBox)) c.Visible = false; // sistem ok düğmeleri
                TextBox t = Edit;
                if (t != null) t.Visible = Enabled; // pasifken metni kendimiz çizeriz
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Prepare(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Prepare(); Invalidate(); }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Prepare(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Color arrow = Enabled ? Theme.Muted : Theme.DisabledStroke;
            float cx = Width - ArrowW / 2f, h = Height;
            using (SolidBrush b = new SolidBrush(arrow))
            {
                g.FillPolygon(b, new PointF[] { new PointF(cx - 3.5f, h / 2f - 2f), new PointF(cx + 3.5f, h / 2f - 2f), new PointF(cx, h / 2f - 5.5f) });
                g.FillPolygon(b, new PointF[] { new PointF(cx - 3.5f, h / 2f + 2f), new PointF(cx + 3.5f, h / 2f + 2f), new PointF(cx, h / 2f + 5.5f) });
            }
            if (!Enabled)
            {
                TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
                fl |= TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : (TextAlign == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(2, 0, Math.Max(1, Width - ArrowW - 4), Height), Theme.Disabled, fl);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Enabled && e.Button == MouseButtons.Left && e.X >= Width - ArrowW)
            {
                Focus();
                if (e.Y < Height / 2) UpButton(); else DownButton();
            }
        }
    }
}
