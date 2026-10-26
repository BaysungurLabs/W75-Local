using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Bir NumericUpDown'un değerini yatay kaydırıcıyla gösteren ve değiştiren, GDI+ ile çizilmiş denetim.
    // Değer ve aralık NumericUpDown'dan okunur (Minimum, Maximum, Increment); kaydırıcı yalnızca o alanın
    // değerini değiştirir, başka bir şey yapmaz.
    public class NumSlider : Control
    {
        private static readonly Color TrackColor = Theme.Border;
        private static readonly Color FillColor = Theme.Accent;
        private static readonly Color OffTrack = Theme.DisabledStroke;
        private static readonly Color OffFill = Theme.DisabledThumb;

        private readonly NumericUpDown _num;
        private bool _drag;

        public NumSlider(NumericUpDown num)
        {
            _num = num;
            _num.ValueChanged += delegate { Invalidate(); };
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Height = 28;
            MinimumSize = new Size(60, 24);
        }

        private float Fraction()
        {
            decimal range = _num.Maximum - _num.Minimum;
            if (range <= 0) return 0f;
            float f = (float)((_num.Value - _num.Minimum) / range);
            return Math.Max(0f, Math.Min(1f, f));
        }

        private void SetFromX(int x)
        {
            if (!Enabled) return;
            float f = (x - 10) / (float)Math.Max(1, Width - 20);
            f = Math.Max(0f, Math.Min(1f, f));
            decimal range = _num.Maximum - _num.Minimum;
            decimal step = _num.Increment > 0 ? _num.Increment : 0.02m;
            decimal v = _num.Minimum + Math.Round(range * (decimal)f / step) * step;
            if (v < _num.Minimum) v = _num.Minimum;
            if (v > _num.Maximum) v = _num.Maximum;
            if (v != _num.Value) _num.Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _drag = true;
            Capture = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = false;
            Capture = false;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            decimal step = _num.Increment > 0 ? _num.Increment : 0.02m;
            decimal v = _num.Value;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) v -= step;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) v += step;
            else return;
            if (v < _num.Minimum) v = _num.Minimum;
            if (v > _num.Maximum) v = _num.Maximum;
            if (v != _num.Value) _num.Value = v;
            e.Handled = true;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            float x0 = 10, x1 = Math.Max(x0 + 1, Width - 10), cy = Height / 2f;
            float tx = x0 + Fraction() * (x1 - x0);
            bool on = Enabled;

            using (Pen p = new Pen(on ? TrackColor : OffTrack, 5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, x0, cy, x1, cy);
            }
            if (tx > x0)
            {
                using (Pen p = new Pen(on ? FillColor : OffFill, 5f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, x0, cy, tx, cy);
                }
            }

            float r = 8f;
            RectangleF thumb = new RectangleF(tx - r, cy - r, r * 2, r * 2);
            using (SolidBrush b = new SolidBrush(on ? Theme.Text : Theme.DisabledThumb))
                g.FillEllipse(b, thumb);
            using (Pen p = new Pen(on ? FillColor : OffFill, Focused && on ? 2.5f : 1.5f))
                g.DrawEllipse(p, thumb);
        }
    }
}
