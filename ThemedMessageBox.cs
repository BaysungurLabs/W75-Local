using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Koyu temalı MessageBox. Bu ad alanında "MessageBox" adlı bir tür tanımlandığı için, aynı ad alanındaki tüm kodlar
    // (kısıtlı DigerTab.cs dahil) "MessageBox.Show(...)" dediğinde sistem kutusu yerine bunu kullanır; dosyalara dokunulmaz.
    // Kullanım ve dönüş değerleri System.Windows.Forms.MessageBox ile aynıdır. Simgeler palet renkleriyle çizilir.
    public static class MessageBox
    {
        public static DialogResult Show(string text) { return Core(null, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption) { return Core(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { return Core(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Core(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def) { return Core(null, text, caption, buttons, icon, def); }
        public static DialogResult Show(IWin32Window owner, string text) { return Core(owner, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Core(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Core(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Core(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def) { return Core(owner, text, caption, buttons, icon, def); }

        private delegate DialogResult ShowDelegate();

        private static DialogResult Core(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
        {
            try
            {
                Control host = owner as Control;
                if (host == null && owner == null && Application.OpenForms.Count > 0) host = Application.OpenForms[0];
                // Arka plan iş parçacığından çağrılırsa arayüz iş parçacığına taşınır (kilitlenme/çökme olmasın).
                if (host != null && host.IsHandleCreated && host.InvokeRequired)
                {
                    IWin32Window o = owner != null ? owner : host;
                    return (DialogResult)host.Invoke(new ShowDelegate(delegate { return Run(o, text, caption, buttons, icon, def); }));
                }
                return Run(owner != null ? owner : host, text, caption, buttons, icon, def);
            }
            catch (InvalidOperationException)
            {
                // Tema kutusu açılamazsa (ör. pencere yok edilmiş) sistem kutusuna düş: sonuç yine de döner.
                return System.Windows.Forms.MessageBox.Show(text, caption, buttons, icon, def);
            }
        }

        private static DialogResult Run(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
        {
            using (MessageDialog dlg = new MessageDialog(text, caption, buttons, icon, def, owner != null))
            {
                try { PlaySound(icon); } catch { }
                return owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
            }
        }

        private static void PlaySound(MessageBoxIcon icon)
        {
            switch ((int)icon)
            {
                case 64: SystemSounds.Asterisk.Play(); break;
                case 48: SystemSounds.Exclamation.Play(); break;
                case 16: SystemSounds.Hand.Play(); break;
            }
        }
    }

    internal class MessageDialog : Form
    {
        private readonly MessageBoxButtons _buttons;
        private readonly MessageBoxIcon _icon;
        private DialogResult _esc = DialogResult.None;
        private DialogResult _close = DialogResult.None;
        private readonly Dictionary<DialogResult, Button> _byResult = new Dictionary<DialogResult, Button>();

        public MessageDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def, bool hasOwner)
        {
            _buttons = buttons;
            _icon = icon;
            if (text == null) text = "";
            AppIcon.Apply(this);
            WindowTheme.Setup(this);
            Text = caption ?? "";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = hasOwner ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);

            // Düğmeler ve Esc / X sonucu (MessageBox kuralları).
            List<KeyValuePair<string, DialogResult>> defs = new List<KeyValuePair<string, DialogResult>>();
            switch (buttons)
            {
                case MessageBoxButtons.OK: defs.Add(P("Tamam", DialogResult.OK)); _esc = DialogResult.OK; _close = DialogResult.OK; break;
                case MessageBoxButtons.OKCancel: defs.Add(P("Tamam", DialogResult.OK)); defs.Add(P("İptal", DialogResult.Cancel)); _esc = DialogResult.Cancel; _close = DialogResult.Cancel; break;
                case MessageBoxButtons.YesNo: defs.Add(P("Evet", DialogResult.Yes)); defs.Add(P("Hayır", DialogResult.No)); _esc = DialogResult.No; _close = DialogResult.No; break;
                case MessageBoxButtons.YesNoCancel: defs.Add(P("Evet", DialogResult.Yes)); defs.Add(P("Hayır", DialogResult.No)); defs.Add(P("İptal", DialogResult.Cancel)); _esc = DialogResult.Cancel; _close = DialogResult.Cancel; break;
                case MessageBoxButtons.RetryCancel: defs.Add(P("Yeniden Dene", DialogResult.Retry)); defs.Add(P("İptal", DialogResult.Cancel)); _esc = DialogResult.Cancel; _close = DialogResult.Cancel; break;
                case MessageBoxButtons.AbortRetryIgnore: defs.Add(P("Durdur", DialogResult.Abort)); defs.Add(P("Yeniden Dene", DialogResult.Retry)); defs.Add(P("Yoksay", DialogResult.Ignore)); break; // sistem kutusu gibi: Esc / X kapatmaz
            }

            int defIndex = def == MessageBoxDefaultButton.Button2 ? 1 : (def == MessageBoxDefaultButton.Button3 ? 2 : 0);
            if (defIndex >= defs.Count) defIndex = 0;

            // Ölçüler.
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            const int pad = 24, gap = 16, strip = 60, minW = 340;
            int iconSize = icon == MessageBoxIcon.None ? 0 : 40;
            int maxTextW = Math.Min(520, (int)(wa.Width * 0.8) - pad * 2 - (iconSize > 0 ? iconSize + gap : 0));
            if (maxTextW < 220) maxTextW = 220;
            TextFormatFlags tf = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top;
            Size ts = TextRenderer.MeasureText(text, Font, new Size(maxTextW, int.MaxValue), tf);
            int textW = Math.Max(ts.Width, 120);
            int textH = Math.Max(ts.Height, 20);

            int btnW = 92, btnH = 32, btnGap = 8;
            foreach (KeyValuePair<string, DialogResult> d in defs) btnW = Math.Max(btnW, TextRenderer.MeasureText(d.Key, Font).Width + 36);
            int btnRowW = defs.Count * btnW + (defs.Count - 1) * btnGap;

            int bodyW = pad + (iconSize > 0 ? iconSize + gap : 0) + textW + pad;
            int clientW = Math.Max(Math.Max(bodyW, btnRowW + pad * 2), minW);
            int maxBodyH = (int)(wa.Height * 0.8) - strip - 40; // başlık çubuğu payı
            int bodyH = Math.Max(textH, iconSize) + pad * 2;
            bool scroll = false;
            if (bodyH > maxBodyH) { bodyH = maxBodyH; scroll = true; }
            ClientSize = new Size(clientW, bodyH + strip);

            // Gövde: simge + metin.
            Panel body = new Panel();
            body.SetBounds(0, 0, clientW, bodyH);
            body.BackColor = Theme.Panel;
            Controls.Add(body);

            int tx = pad;
            if (iconSize > 0)
            {
                Panel ip = new Panel();
                ip.SetBounds(pad, pad, iconSize, iconSize);
                ip.BackColor = Theme.Panel;
                ip.Paint += delegate(object o, PaintEventArgs e) { PaintIcon(e.Graphics, ip.ClientRectangle); };
                body.Controls.Add(ip);
                tx = pad + iconSize + gap;
            }
            Label lbl = new Label();
            lbl.UseMnemonic = false;
            lbl.AutoSize = false;
            lbl.Text = text;
            lbl.ForeColor = Theme.Text;
            lbl.BackColor = Theme.Panel;
            lbl.Font = Font;
            int availW = clientW - tx - pad;
            if (scroll)
            {
                Panel sp = new Panel();
                sp.AutoScroll = true;
                sp.BackColor = Theme.Panel;
                sp.SetBounds(tx, pad, availW, bodyH - pad * 2);
                lbl.SetBounds(0, 0, availW - SystemInformation.VerticalScrollBarWidth, textH);
                sp.Controls.Add(lbl);
                body.Controls.Add(sp);
                Theme.DarkScroll(sp);
            }
            else
            {
                lbl.SetBounds(tx, pad + (iconSize > textH ? (iconSize - textH) / 2 : 0), availW, textH);
                body.Controls.Add(lbl);
            }

            // Alt şerit: düğmeler.
            Panel bar = new Panel();
            bar.SetBounds(0, bodyH, clientW, strip);
            bar.BackColor = Theme.Bg;
            bar.Paint += delegate(object o, PaintEventArgs e) { using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bar.Width, 0); };
            Controls.Add(bar);

            int x = clientW - pad - btnRowW;
            Button defBtn = null;
            for (int i = 0; i < defs.Count; i++)
            {
                Button b = new Button();
                b.Text = defs[i].Key;
                b.DialogResult = defs[i].Value;
                b.SetBounds(x, (strip - btnH) / 2, btnW, btnH);
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.Font = new Font("Segoe UI Semibold", 9.5f);
                b.Cursor = Cursors.Hand;
                bool isDef = i == defIndex;
                b.BackColor = isDef ? Theme.Accent : Theme.Raised;
                b.ForeColor = isDef ? Theme.AccentText : Theme.Text;
                b.FlatAppearance.BorderSize = isDef ? 0 : 1;
                b.FlatAppearance.BorderColor = Theme.Border;
                Theme.StyleButton(b);
                bar.Controls.Add(b);
                _byResult[defs[i].Value] = b;
                if (isDef) defBtn = b;
                x += btnW + btnGap;
            }
            if (defBtn != null) { AcceptButton = defBtn; ActiveControl = defBtn; }
            Shown += delegate { if (defBtn != null) defBtn.Focus(); };
        }

        private static KeyValuePair<string, DialogResult> P(string t, DialogResult r) { return new KeyValuePair<string, DialogResult>(t, r); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                if (_esc != DialogResult.None) DialogResult = _esc;
                return;
            }
            if (e.Modifiers == Keys.None)
            {
                if ((e.KeyCode == Keys.Y || e.KeyCode == Keys.E) && _byResult.ContainsKey(DialogResult.Yes)) { e.SuppressKeyPress = true; DialogResult = DialogResult.Yes; return; }
                if ((e.KeyCode == Keys.N || e.KeyCode == Keys.H) && _byResult.ContainsKey(DialogResult.No)) { e.SuppressKeyPress = true; DialogResult = DialogResult.No; return; }
            }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.None && e.CloseReason == CloseReason.UserClosing)
            {
                if (_close == DialogResult.None) { e.Cancel = true; return; } // AbortRetryIgnore: X kapatmaz
                DialogResult = _close;
            }
            base.OnFormClosing(e);
        }

        // Simge: palet renkli daire içinde i, ?, !, × (sistem simgesi kullanılmaz).
        private void PaintIcon(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Color c; string glyph = null;
            switch ((int)_icon)
            {
                case 64: c = Theme.Cyan; glyph = "i"; break;
                case 32: c = Theme.Cyan; glyph = "?"; break;
                case 48: c = Theme.Accent; glyph = "!"; break;
                case 16: c = Theme.Red; glyph = null; break;
                default: return;
            }
            RectangleF cr = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
            bool ring = (int)_icon == 32;
            if (ring)
            {
                using (Pen p = new Pen(c, 2.5f)) g.DrawEllipse(p, cr);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cr);
            }
            Color gc = ring ? c : Theme.Bg;
            if (glyph != null)
            {
                using (Font f = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Point))
                using (SolidBrush b = new SolidBrush(gc))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(glyph, f, b, new RectangleF(cr.X, cr.Y + 1, cr.Width, cr.Height), sf);
            }
            else
            {
                float m = cr.Width * 0.30f;
                using (Pen p = new Pen(gc, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(p, cr.X + m, cr.Y + m, cr.Right - m, cr.Bottom - m);
                    g.DrawLine(p, cr.Right - m, cr.Y + m, cr.X + m, cr.Bottom - m);
                }
            }
        }
    }
}
