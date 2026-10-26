using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Koyu temaya uygun, tek parça (metin ve ok aynı kutuda) açılır liste.
    // FlatStyle.Flat ComboBox, ok düğmesini metin alanından ayrı küçük bir kutu olarak çiziyor ve bu
    // "yanında boş ikinci bir açılır kutu" gibi görünüyordu; bu sınıf kutuyu bütün olarak kendisi çizer.
    public class FlatCombo : ComboBox
    {
        private static readonly Color BorderColor = Theme.Border;
        private static readonly Color HoverBorder = Theme.Accent;
        private bool _hover;

        public FlatCombo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 18; // kutu yüksekliği = 18 + 6 = 24, satıra (30 - kenar boşlukları) tam sığar; alt çizgi kesilmez
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            RectangleF r = new RectangleF(0.5f, 0.5f, ClientSize.Width - 1.5f, ClientSize.Height - 1.5f);
            using (GraphicsPath path = Theme.RoundRect(r, Theme.Radius))
            {
                using (SolidBrush fb = new SolidBrush(BackColor)) g.FillPath(fb, path);
                using (Pen p = new Pen(_hover || Focused ? HoverBorder : BorderColor, 1f)) g.DrawPath(p, path);
            }

            string text = SelectedItem != null ? SelectedItem.ToString() : "";
            Color fore = Enabled ? ForeColor : Theme.Disabled;
            TextRenderer.DrawText(g, text, Font, new Rectangle(8, 0, ClientSize.Width - 30, ClientSize.Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // ok
            float cx = ClientSize.Width - 14f, cy = ClientSize.Height / 2f;
            PointF[] tri = new PointF[] { new PointF(cx - 4f, cy - 2f), new PointF(cx + 4f, cy - 2f), new PointF(cx, cy + 3f) };
            using (SolidBrush b = new SolidBrush(Enabled ? Theme.Text2 : Theme.Disabled)) g.FillPolygon(b, tri);
        }

        // Açılan listenin satırları da koyu çizilir.
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(sel ? Theme.AccentSoft : BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                sel ? Theme.Accent : ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
