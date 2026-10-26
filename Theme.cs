using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Ortak koyu tema: renk paleti (tek yer), yuvarlak köşeli düğme çizimi ve kaydırma çubuğu/liste teması.
    // Yalnızca görünüm; veri ve davranış değişmez. Klavye çizimi, tuş renkleri ve ışık önizlemesi bu paletten bağımsızdır.
    public static class Theme
    {
        // Yüzeyler (nötr koyu gri, düşük kontrast).
        public static readonly Color Bg = Color.FromArgb(0x1E, 0x1F, 0x22);        // pencere zemini
        public static readonly Color Panel = Color.FromArgb(0x26, 0x27, 0x2B);     // yüzey / panel
        public static readonly Color Raised = Color.FromArgb(0x2C, 0x2D, 0x32);    // yükseltilmiş yüzey: kartlar, listeler, düğmeler, giriş kutuları
        public static readonly Color Inset = Color.FromArgb(0x1A, 0x1B, 0x1E);     // gömülü alanlar: günlük, önizleme zemini
        public static readonly Color SelRow = Color.FromArgb(0x38, 0x39, 0x3F);    // seçili/üstüne gelinen satır
        public static readonly Color Border = Color.FromArgb(0x34, 0x36, 0x3C);    // ince kenarlık ve ayırıcı
        // Metin.
        public static readonly Color Text = Color.FromArgb(0xE6, 0xE6, 0xE8);      // ana metin
        public static readonly Color Text2 = Color.FromArgb(0xC8, 0xC9, 0xCE);     // vurgusu azaltılmış ana metin (günlük)
        public static readonly Color Muted = Color.FromArgb(0xA3, 0xA5, 0xAB);     // ikincil metin
        public static readonly Color Disabled = Color.FromArgb(0x80, 0x82, 0x89);  // devre dışı metin (panelde 3.9:1)
        public static readonly Color DisabledStroke = Color.FromArgb(0x7C, 0x7E, 0x85); // devre dışı çizgi/çubuk/kenar (panelde 3.7:1)
        public static readonly Color DisabledThumb = Color.FromArgb(0x96, 0x98, 0x9E);  // devre dışı sürgü topu/dolgu (panelde 5.0:1), vurgu rengi yok
        // Vurgu ve durum renkleri (yumuşatılmış).
        public static readonly Color Accent = Color.FromArgb(0xE8, 0xA0, 0x4C);
        public static readonly Color AccentSoft = Color.FromArgb(0x4E, 0x42, 0x37); // vurgunun %18 karışımı (Raised üzerine): seçili kart/odak
        public static readonly Color AccentText = Color.FromArgb(0x24, 0x1B, 0x0E); // vurgu zemini üzerindeki koyu metin
        public static readonly Color Green = Color.FromArgb(0x7A, 0xC8, 0x9E);
        public static readonly Color Red = Color.FromArgb(0xE8, 0x7D, 0x7D);
        public static readonly Color RedBorder = Color.FromArgb(0xE1, 0x6E, 0x6E);
        public static readonly Color Cyan = Color.FromArgb(0x76, 0xB4, 0xE0);

        public const int Radius = 7; // düğme, kart, giriş köşe yarıçapı (6-8 px)

        // Eski (önceki sürüm) renk değerleri → yeni palet. Kısıtlı dosyalardaki (ör. DigerTab) kendi renk kodları bu
        // tablo üzerinden çalışma anında yeni renge çevrilir (StyleTree); o dosyalara dokunulmaz.
        private static readonly Dictionary<int, Color> Legacy = BuildLegacy();

        private static int Key(Color c) { return (c.R << 16) | (c.G << 8) | c.B; }

        private static Dictionary<int, Color> BuildLegacy()
        {
            Dictionary<int, Color> m = new Dictionary<int, Color>();
            Action<int, int, int, Color> add = delegate(int r, int g, int b, Color to) { m[(r << 16) | (g << 8) | b] = to; };
            add(18, 20, 26, Bg); add(24, 27, 35, Panel);
            add(30, 34, 44, Raised); add(31, 35, 46, Raised); add(23, 26, 34, Raised);
            add(20, 23, 30, Inset); add(13, 15, 20, Inset);
            add(42, 47, 60, SelRow); add(48, 55, 72, SelRow); add(40, 45, 58, SelRow);
            add(44, 50, 64, Border); add(58, 64, 80, Border); add(40, 46, 58, Border); add(38, 43, 55, Border);
            add(230, 232, 239, Text); add(220, 224, 234, Text); add(235, 238, 245, Text); add(240, 242, 248, Text);
            add(184, 192, 208, Text2); add(190, 196, 212, Text2);
            add(139, 144, 160, Muted); add(150, 156, 172, Muted); add(110, 116, 132, Muted);
            add(78, 84, 98, Disabled); add(100, 106, 122, Disabled); add(84, 90, 104, Disabled); add(90, 96, 112, Disabled); add(78, 84, 100, Disabled);
            add(255, 184, 107, Accent); add(255, 176, 64, Accent);
            add(58, 42, 24, AccentSoft); add(52, 44, 30, AccentSoft);
            add(255, 110, 110, Red); add(108, 214, 156, Green); add(91, 200, 255, Cyan); add(224, 72, 72, RedBorder);
            add(28, 20, 8, AccentText); add(30, 22, 10, AccentText);
            return m;
        }

        public static Color Remap(Color c)
        {
            if (c.IsEmpty || c.A != 255) return c;
            Color to;
            return Legacy.TryGetValue(Key(c), out to) ? to : c;
        }

        // Verilen rengi beyaza (amount > 0) ya da siyaha (amount < 0) doğru karıştırır.
        public static Color Mix(Color c, Color with, double t)
        {
            return Color.FromArgb(c.A, (int)(c.R + (with.R - c.R) * t), (int)(c.G + (with.G - c.G) * t), (int)(c.B + (with.B - c.B) * t));
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(0.01f, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------ düğme

        private class BtnState { public bool Hover, Down; }
        private static readonly ConditionalWeakTable<Button, BtnState> Styled = new ConditionalWeakTable<Button, BtnState>();

        // Düz düğmeyi yuvarlak köşeli, ince kenarlıklı çizer; fare üstü ve basılı durumu yumuşak ton farkıyla gösterir.
        public static void StyleButton(Button b)
        {
            BtnState st;
            if (Styled.TryGetValue(b, out st)) return;
            st = new BtnState();
            Styled.Add(b, st);
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = Remap(b.BackColor);
            b.ForeColor = Remap(b.ForeColor);
            b.FlatAppearance.BorderColor = Remap(b.FlatAppearance.BorderColor);
            BtnState s = st;
            b.MouseEnter += delegate { s.Hover = true; b.Invalidate(); };
            b.MouseLeave += delegate { s.Hover = false; s.Down = false; b.Invalidate(); };
            b.MouseDown += delegate { s.Down = true; b.Invalidate(); };
            b.MouseUp += delegate { s.Down = false; b.Invalidate(); };
            b.Paint += delegate(object o, PaintEventArgs e) { PaintButton(b, s, e.Graphics); };
        }

        private static void PaintButton(Button b, BtnState st, Graphics g)
        {
            Color back = b.Parent != null ? b.Parent.BackColor : Bg;
            g.Clear(back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color face = b.BackColor;
            Color fore = b.ForeColor;
            Color border = b.FlatAppearance.BorderColor;
            bool border1 = b.FlatAppearance.BorderSize > 0;
            if (!b.Enabled) { face = Mix(face, Panel, 0.35); fore = Disabled; border = DisabledStroke; border1 = true; }
            else if (st.Down) face = Mix(face, Color.Black, 0.12);
            else if (st.Hover) { face = Mix(face, Color.White, 0.08); border = Mix(border, Color.White, 0.10); }
            RectangleF r = new RectangleF(0.5f, 0.5f, b.Width - 1.5f, b.Height - 1.5f);
            using (GraphicsPath path = RoundRect(r, Radius))
            {
                using (SolidBrush fb = new SolidBrush(face)) g.FillPath(fb, path);
                if (border1) using (Pen p = new Pen(border, 1f)) g.DrawPath(p, path);
                else if (b.Focused && b.Enabled) using (Pen p = new Pen(Mix(face, Color.White, 0.30), 1f)) g.DrawPath(p, path);
            }
            TextFormatFlags fl = TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            switch (b.TextAlign)
            {
                case ContentAlignment.MiddleLeft: case ContentAlignment.TopLeft: case ContentAlignment.BottomLeft: fl |= TextFormatFlags.Left; break;
                case ContentAlignment.MiddleRight: case ContentAlignment.TopRight: case ContentAlignment.BottomRight: fl |= TextFormatFlags.Right; break;
                default: fl |= TextFormatFlags.HorizontalCenter; break;
            }
            Rectangle tr = new Rectangle(b.Padding.Left, 0, Math.Max(1, b.Width - b.Padding.Horizontal), b.Height);
            TextRenderer.DrawText(g, b.Text, b.Font, tr, fore, fl);
        }

        // ------------------------------------------------------------ ağaç gezme

        // Bir denetim ağacını temaya uydurur: düğmeler yuvarlak çizilir, eski renk kodları (kısıtlı dosyalardakiler dahil) yeni
        // palete çevrilir, liste/kaydırma çubukları koyu temaya alınır. Her denetim için tekrar çağrılırsa zararsızdır.
        public static void StyleTree(Control root)
        {
            Control c = root;
            if (!(c is Form)) StyleOne(c);
            else { Color nb = Remap(root.BackColor); if (nb != root.BackColor) root.BackColor = nb; Color nf = Remap(root.ForeColor); if (nf != root.ForeColor) root.ForeColor = nf; }
            foreach (Control ch in c.Controls) StyleTree(ch);
            if (c is DataGridView) StyleGrid((DataGridView)c);
        }

        private static void StyleOne(Control c)
        {
            Button b = c as Button;
            if (b != null) { StyleButton(b); return; }
            // Yalnızca değişen renkler atanır (atanmamış, üstten miras alan renkler olduğu gibi kalır).
            Color nb = Remap(c.BackColor); if (nb != c.BackColor && !(c is CheckBox) && !(c is RadioButton)) c.BackColor = nb;
            Color nf = Remap(c.ForeColor); if (nf != c.ForeColor) c.ForeColor = nf;
            ListBox lb = c as ListBox;
            if (lb != null && !(c is CheckedListBox) && lb.DrawMode == DrawMode.Normal) StyleList(lb);
            if (c is ListBox || c is TextBoxBase || c is UpDownBase || c is ScrollBar || c is DataGridView
                || (c is ScrollableControl && ((ScrollableControl)c).AutoScroll))
                DarkScroll(c);
            if (c is UpDownBase) { UpDownBase ud = (UpDownBase)c; ud.BorderStyle = BorderStyle.None; DarkUpDown(ud); } // beyaz sistem çerçevesi kalmasın
        }

        // Sistem mavisi seçim yerine yumuşak vurgu: ListBox'ı aynı yükseklikte kendimiz çizeriz (davranış aynı).
        private static void StyleList(ListBox lb)
        {
            lb.DrawMode = DrawMode.OwnerDrawFixed;
            lb.DrawItem += delegate(object o, DrawItemEventArgs e)
            {
                ListBox l = (ListBox)o;
                Graphics g = e.Graphics;
                using (SolidBrush bb = new SolidBrush(l.BackColor)) g.FillRectangle(bb, e.Bounds);
                if (e.Index < 0 || e.Index >= l.Items.Count) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                if (sel)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath p = RoundRect(new RectangleF(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height), 4))
                    using (SolidBrush sb = new SolidBrush(AccentSoft)) g.FillPath(sb, p);
                }
                TextRenderer.DrawText(g, l.GetItemText(l.Items[e.Index]), l.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                    l.Enabled ? (sel ? Text : l.ForeColor) : Disabled, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        private static void StyleGrid(DataGridView g)
        {
            g.GridColor = Remap(g.GridColor);
            DataGridViewCellStyle[] ss = { g.DefaultCellStyle, g.ColumnHeadersDefaultCellStyle, g.RowHeadersDefaultCellStyle, g.AlternatingRowsDefaultCellStyle };
            foreach (DataGridViewCellStyle s in ss)
            {
                s.BackColor = Remap(s.BackColor); s.ForeColor = Remap(s.ForeColor);
                s.SelectionBackColor = Remap(s.SelectionBackColor); s.SelectionForeColor = Remap(s.SelectionForeColor);
            }
            g.BackgroundColor = Remap(g.BackgroundColor);
            g.BorderStyle = BorderStyle.None; // açık renkli sistem çerçevesi kalmasın
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single; // 3B (açık gri) başlık kenarı yerine GridColor ile ince çizgi
            foreach (Control ch in g.Controls) if (ch is ScrollBar) DarkScroll(ch);
        }

        // ------------------------------------------------------------ kaydırma çubuğu / liste teması

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        // NumericUpDown'ın iç denetimleri: düzenleme kutusu CFD, ok düğmeleri Explorer koyu temasıyla.
        private static void DarkUpDown(UpDownBase u)
        {
            try
            {
                Action apply = delegate
                {
                    try
                    {
                        foreach (Control ch in u.Controls)
                        {
                            if (!ch.IsHandleCreated) { Control cc = ch; ch.HandleCreated += delegate { try { SetWindowTheme(cc.Handle, cc is TextBox ? "DarkMode_CFD" : "DarkMode_Explorer", null); } catch { } }; }
                            else SetWindowTheme(ch.Handle, ch is TextBox ? "DarkMode_CFD" : "DarkMode_Explorer", null);
                        }
                    }
                    catch { }
                };
                apply();
                u.HandleCreated += delegate { apply(); };
            }
            catch { }
        }

        // Düz (sistem) ComboBox'ları yuvarlak, koyu FlatCombo ile değiştirir; öğeler, seçim ve olay işleyicileri taşınır.
        // owner: ComboBox alanını tutan nesne (alanı yeni denetime yönlendirmek için); kısıtlı dosyalardaki sınıflara dokunmadan çağıran
        // tarafta kullanılır. Herhangi bir adım başarısız olursa eski denetim olduğu gibi kalır.
        public static void ReplaceCombos(Control root, object owner)
        {
            List<ComboBox> olds = new List<ComboBox>();
            Collect(root, olds);
            foreach (ComboBox old in olds)
            {
                try
                {
                    Control parent = old.Parent;
                    if (parent == null) continue;
                    FlatCombo n = new FlatCombo();
                    n.DropDownStyle = old.DropDownStyle;
                    n.Font = old.Font;
                    n.Name = old.Name; n.Tag = old.Tag;
                    n.Anchor = old.Anchor; n.Dock = old.Dock;
                    n.Bounds = old.Bounds;
                    n.TabIndex = old.TabIndex; n.TabStop = old.TabStop;
                    n.BackColor = Remap(old.BackColor); n.ForeColor = Remap(old.ForeColor);
                    n.Enabled = old.Enabled;
                    foreach (object it in old.Items) n.Items.Add(it);
                    n.SelectedIndex = old.SelectedIndex;
                    // olay işleyicilerini taşı
                    System.Reflection.PropertyInfo ev = typeof(System.ComponentModel.Component).GetProperty("Events", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    System.ComponentModel.EventHandlerList list = (System.ComponentModel.EventHandlerList)ev.GetValue(old, null);
                    foreach (string keyName in new string[] { "EVENT_SELECTEDINDEXCHANGED", "EVENT_SELECTIONCHANGECOMMITTED" })
                    {
                        System.Reflection.FieldInfo kf = typeof(ComboBox).GetField(keyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        if (kf == null) continue;
                        Delegate d = list[kf.GetValue(null)];
                        if (d == null) continue;
                        foreach (Delegate h in d.GetInvocationList())
                        {
                            if (keyName == "EVENT_SELECTEDINDEXCHANGED") n.SelectedIndexChanged += (EventHandler)h;
                            else n.SelectionChangeCommitted += (EventHandler)h;
                        }
                    }
                    // alanı yönlendir
                    if (owner != null)
                        foreach (System.Reflection.FieldInfo f in owner.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                            if (f.FieldType == typeof(ComboBox) && f.GetValue(owner) == old) f.SetValue(owner, n);
                    int idx = parent.Controls.GetChildIndex(old);
                    parent.Controls.Add(n);
                    parent.Controls.SetChildIndex(n, idx);
                    parent.Controls.Remove(old);
                    old.Dispose();
                }
                catch { }
            }
        }

        private static void Collect(Control c, List<ComboBox> into)
        {
            foreach (Control ch in c.Controls)
            {
                ComboBox cb = ch as ComboBox;
                if (cb != null && !(ch is FlatCombo)) into.Add(cb);
                Collect(ch, into);
            }
        }

        // Denetimin kaydırma çubuklarını ve iç öğelerini koyu temaya alır (Windows 10 1809+; desteklenmezse sessizce geçer).
        public static void DarkScroll(Control c)
        {
            try
            {
                string theme = (c is TextBoxBase && !((TextBoxBase)c).Multiline) ? "DarkMode_CFD" : "DarkMode_Explorer";
                if (c.IsHandleCreated) SetWindowTheme(c.Handle, theme, null);
                c.HandleCreated += delegate { try { SetWindowTheme(c.Handle, theme, null); } catch { } };
            }
            catch { }
        }
    }
}
