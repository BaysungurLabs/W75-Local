using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Tuş yolu grafiği (GDI+ ile çizilir): sade bir switch kesiti ve dikey mesafe çubuğu.
    // İki kip: mesafe kipinde tetikleme çizgisi ve Rapid Trigger işaretleri, ölü bölge kipinde
    // çubuğun üst/alt ucunda taralı ölü bölge bantları. Değerler doğrudan sayfadaki alanlardan okunur;
    // alanlar değişince kendini yeniler. Çizim, denetimin boyutuna göre ölçeklenir.
    public class TravelGraph : Control
    {
        private static readonly Color Accent = Color.FromArgb(255, 184, 107);
        private static readonly Color PressColor = Color.FromArgb(91, 200, 255);
        private static readonly Color ReleaseColor = Color.FromArgb(108, 214, 156);
        private static readonly Color MutedText = Color.FromArgb(150, 156, 172);

        private readonly bool _dead;
        private readonly NumericUpDown _travel, _i1, _i2, _deadTop, _deadBottom;
        private readonly ComboBox _rt;

        // Mesafe kipi.
        public TravelGraph(NumericUpDown travel, ComboBox rt, NumericUpDown i1, NumericUpDown i2)
        {
            _travel = travel; _rt = rt; _i1 = i1; _i2 = i2;
            _travel.ValueChanged += delegate { Invalidate(); };
            _i1.ValueChanged += delegate { Invalidate(); };
            _i2.ValueChanged += delegate { Invalidate(); };
            _rt.SelectedIndexChanged += delegate { Invalidate(); };
            Init();
        }

        // Ölü bölge kipi. travelForMax yalnızca maksimum mesafeyi (çubuğun uzunluğunu) verir.
        public TravelGraph(NumericUpDown travelForMax, NumericUpDown deadTop, NumericUpDown deadBottom)
        {
            _dead = true;
            _travel = travelForMax; _deadTop = deadTop; _deadBottom = deadBottom;
            _deadTop.ValueChanged += delegate { Invalidate(); };
            _deadBottom.ValueChanged += delegate { Invalidate(); };
            Init();
        }

        private void Init()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MinimumSize = new Size(200, 150);
        }

        private static string Fmt(decimal mm)
        {
            return mm.ToString("0.00", CultureInfo.CurrentCulture);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            float k = Math.Max(0.8f, Math.Min(2.2f, Math.Min(Width / 244f, Height / 212f)));
            decimal max = _travel.Maximum > 0.1m ? _travel.Maximum : 3.40m;
            decimal trig = _dead ? 0m : Math.Min(max, Math.Max(0m, _travel.Value));
            bool rtOn = !_dead && _rt.SelectedIndex > 0;

            float y0 = 16f * k, y1 = Height - 24f * k;
            if (y1 - y0 < 60) return;
            float barW = 12f * k;
            float barX = Width * 0.56f;
            Func<decimal, float> yOf = delegate(decimal mm)
            {
                decimal m = Math.Max(0m, Math.Min(max, mm));
                return y0 + (float)(m / max) * (y1 - y0);
            };
            float frac = (float)(trig / max);

            DrawSwitch(g, k, y0, y1, barX, frac);

            // ---- mesafe çubuğu ----
            RectangleF bar = new RectangleF(barX, y0, barW, y1 - y0);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 34, 44))) g.FillRectangle(b, bar);

            float yT = yOf(trig);
            if (_dead)
            {
                decimal top = Math.Min(max, _deadTop.Value), bot = Math.Min(max, _deadBottom.Value);
                float yTopEnd = yOf(top);
                float yBotStart = yOf(max - bot);
                if (yBotStart < yTopEnd) yBotStart = yTopEnd;
                // aktif alan
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, Accent)))
                    g.FillRectangle(b, barX, yTopEnd, barW, Math.Max(0f, yBotStart - yTopEnd));
                // ölü bölge bantları: koyu, taralı
                using (HatchBrush hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(120, 128, 150), Color.FromArgb(20, 22, 28)))
                {
                    if (yTopEnd > y0) g.FillRectangle(hb, barX, y0, barW, yTopEnd - y0);
                    if (y1 > yBotStart) g.FillRectangle(hb, barX, yBotStart, barW, y1 - yBotStart);
                }
                using (Pen p = new Pen(Color.FromArgb(200, 210, 230), 1.4f))
                {
                    g.DrawLine(p, barX - 6f * k, yTopEnd, barX + barW + 6f * k, yTopEnd);
                    g.DrawLine(p, barX - 6f * k, yBotStart, barX + barW + 6f * k, yBotStart);
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, Accent)))
                    g.FillRectangle(b, barX, y0, barW, Math.Max(0f, yT - y0));
            }
            using (Pen p = new Pen(Color.FromArgb(70, 78, 96), 1.4f)) g.DrawRectangle(p, bar.X, bar.Y, bar.Width, bar.Height);

            // ---- çentikler ve etiketler ----
            using (Font tf = new Font("Segoe UI", 6.6f * k))
            using (SolidBrush tb = new SolidBrush(MutedText))
            using (Pen tp = new Pen(Color.FromArgb(110, 118, 140), 1f))
            {
                StringFormat right = new StringFormat();
                right.Alignment = StringAlignment.Far;
                right.LineAlignment = StringAlignment.Center;
                float lw = 40f * k, lh = 14f * k;
                for (decimal t = 0m; t <= max + 0.001m; t += 0.5m)
                {
                    float y = yOf(t);
                    g.DrawLine(tp, barX - 6f * k, y, barX, y);
                    g.DrawString(t.ToString("0.0", CultureInfo.CurrentCulture), tf, tb, new RectangleF(barX - 8f * k - lw, y - lh / 2f, lw, lh), right);
                }
                float yM = yOf(max);
                g.DrawLine(tp, barX - 6f * k, yM, barX, yM);
                using (Font bf = new Font("Segoe UI", 6.6f * k, FontStyle.Bold))
                    g.DrawString(Fmt(max), bf, tb, new RectangleF(barX - 8f * k - lw, yM - lh / 2f + 9f * k, lw, lh), right);
                g.DrawString("mm", tf, tb, new PointF(barX - 14f * k, 0f));
            }

            float labelX = barX + barW + 12f * k;
            float labelW = Math.Max(10f, Width - labelX - 2f);
            if (_dead)
            {
                decimal top = Math.Min(max, _deadTop.Value), bot = Math.Min(max, _deadBottom.Value);
                float yTopEnd = yOf(top);
                float yBotStart = yOf(max - bot);
                using (Font lf = new Font("Segoe UI Semibold", 8f * k))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(215, 220, 232)))
                using (SolidBrush ab = new SolidBrush(Accent))
                {
                    StringFormat left = new StringFormat();
                    left.LineAlignment = StringAlignment.Center;
                    float lh = 16f * k;
                    float yTopLbl = Math.Max(y0 + lh / 2f, yTopEnd / 2f + y0 / 2f);
                    float yBotLbl = Math.Min(y1 - lh / 2f, (yBotStart + y1) / 2f);
                    g.DrawString("Üst " + Fmt(top) + " mm", lf, lb, new RectangleF(labelX, yTopLbl - lh / 2f, labelW, lh), left);
                    g.DrawString("Alt " + Fmt(bot) + " mm", lf, lb, new RectangleF(labelX, yBotLbl - lh / 2f, labelW, lh), left);
                    float yMid = (yTopEnd + yBotStart) / 2f;
                    if (yBotStart - yTopEnd > 2f * lh)
                        g.DrawString("aktif alan", lf, ab, new RectangleF(labelX, yMid - lh / 2f, labelW, lh), left);
                }
                return;
            }

            // ---- tetikleme çizgisi + değer etiketi ----
            using (Pen p = new Pen(Accent, 2.4f)) g.DrawLine(p, barX - 10f * k, yT, barX + barW + 8f * k, yT);
            using (Font lf = new Font("Segoe UI Semibold", 8.5f * k))
            using (SolidBrush lb = new SolidBrush(Accent))
            {
                StringFormat left = new StringFormat();
                left.LineAlignment = StringAlignment.Center;
                float lh = 18f * k;
                float ly = Math.Max(y0 + lh / 2f, Math.Min(y1 - lh / 2f, yT));
                g.DrawString(Fmt(trig) + " mm", lf, lb, new RectangleF(labelX, ly - lh / 2f, labelW, lh), left);
            }

            // ---- Rapid Trigger: basma (altta) ve bırakma (üstte) işaretleri ----
            if (rtOn)
            {
                decimal press = _i1.Value, release = _i2.Value;
                float yP = yOf(trig + press);
                float yR = yOf(trig - release);
                DrawMarker(g, barX + barW, yP, PressColor, false, k);
                DrawMarker(g, barX + barW, yR, ReleaseColor, true, k);
                using (Font lf = new Font("Segoe UI", 7f * k))
                {
                    float ly = y1 - 28f * k;
                    using (SolidBrush b = new SolidBrush(PressColor))
                        g.DrawString("▼ basma " + Fmt(press), lf, b, new PointF(labelX - 4f * k, ly));
                    using (SolidBrush b = new SolidBrush(ReleaseColor))
                        g.DrawString("▲ bırakma " + Fmt(release), lf, b, new PointF(labelX - 4f * k, ly + 14f * k));
                }
            }
        }

        // Sade switch kesiti: tuş başlığı + mil gövdenin içine iner, altında yay.
        private static void DrawSwitch(Graphics g, float k, float y0, float y1, float barX, float frac)
        {
            float travelPx = Math.Min(36f * k, (y1 - y0) * 0.22f);
            float capY = y0 + 4f * k + frac * travelPx;
            float hx = 8f * k;
            float hw = Math.Max(40f * k, Math.Min(76f * k, barX - 60f * k - hx));
            float hTop = y0 + 62f * k, hBot = y1 + 4f * k;
            RectangleF housing = new RectangleF(hx, hTop, hw, hBot - hTop);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 34, 44))) g.FillRectangle(b, housing);
            using (Pen p = new Pen(Color.FromArgb(78, 86, 106), 1.6f)) g.DrawRectangle(p, housing.X, housing.Y, housing.Width, housing.Height);

            float cx = hx + hw / 2f;
            float stemTop = capY + 16f * k;
            float stemBot = stemTop + 46f * k;
            using (Pen sp = new Pen(Color.FromArgb(130, 138, 160), 1.7f))
            {
                int n = 7;
                PointF prev = new PointF(cx, stemBot);
                float sy1 = hBot - 6f * k;
                for (int i = 1; i <= n; i++)
                {
                    float yy = stemBot + (sy1 - stemBot) * i / n;
                    float xx = cx + (i % 2 == 0 ? -14f * k : 14f * k);
                    if (i == n) xx = cx;
                    PointF next = new PointF(xx, yy);
                    g.DrawLine(sp, prev, next);
                    prev = next;
                }
            }
            RectangleF stem = new RectangleF(cx - 6f * k, stemTop, 12f * k, stemBot - stemTop);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 112, 138))) g.FillRectangle(b, stem);
            RectangleF cap = new RectangleF(hx + 6f * k, capY, hw - 12f * k, 16f * k);
            using (GraphicsPath cp = RoundRect(cap, 4f * k))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 64, 82))) g.FillPath(b, cp);
                using (Pen p = new Pen(Color.FromArgb(110, 118, 144), 1.3f)) g.DrawPath(p, cp);
            }
        }

        // Çubuğun sağ kenarında küçük üçgen işaret (up=true: yukarı bakar).
        private static void DrawMarker(Graphics g, float x, float y, Color c, bool up, float k)
        {
            float w = 8f * k, h = 7f * k;
            PointF[] pts = up
                ? new PointF[] { new PointF(x + k, y), new PointF(x + k + w, y), new PointF(x + k + w / 2f, y - h) }
                : new PointF[] { new PointF(x + k, y), new PointF(x + k + w, y), new PointF(x + k + w / 2f, y + h) };
            using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, pts);
            using (Pen p = new Pen(c, 1.3f)) g.DrawLine(p, x - 2f * k, y, x + k + w + 2f * k, y);
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
