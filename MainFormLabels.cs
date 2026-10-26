using System;
using System.Drawing;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Keymap sekmesindeki "Tuş yazıları: TR | US" seçici. Yalnızca görünen etiketleri değiştirir;
    // tuş kodları, profiller ve cihaza yazılan veri aynı kalır. Seçim pencere ayarlarıyla (pencere.json) saklanır.
    public partial class MainForm
    {
        private Button _btnLabelTr, _btnLabelUs;

        private Control BuildLabelModeBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Bg;
            bar.Margin = new Padding(0, 0, 0, 4);

            Label cap = new Label();
            cap.Text = "Tuş yazıları:";
            cap.ForeColor = Muted;
            cap.Font = new Font("Segoe UI", 9f);
            cap.AutoSize = true;
            cap.Location = new Point(2, 9);
            bar.Controls.Add(cap);

            _btnLabelTr = MakeButton("TR", false);
            _btnLabelUs = MakeButton("US", false);
            _btnLabelTr.Bounds = new Rectangle(86, 2, 54, 28);
            _btnLabelUs.Bounds = new Rectangle(146, 2, 54, 28);
            _btnLabelTr.Click += delegate { SetLabelMode(true); };
            _btnLabelUs.Click += delegate { SetLabelMode(false); };
            bar.Controls.Add(_btnLabelTr);
            bar.Controls.Add(_btnLabelUs);

            ToolTip tip = new ToolTip();
            tip.SetToolTip(_btnLabelTr, "TR (Türkçe Q): tuş yazıları Türkçe Q düzenine göre");
            tip.SetToolTip(_btnLabelUs, "US (İngilizce Q): tuş yazıları İngilizce Q düzenine göre");

            Label note = new Label();
            note.Text = "Yalnızca ekrandaki yazılar değişir, veri aynı kalır.";
            tip.SetToolTip(note, "Yalnızca görünen yazılar değişir; tuş kodları ve cihaza yazılan veri aynıdır.");
            note.ForeColor = Muted;
            note.Font = new Font("Segoe UI", 8f);
            note.AutoSize = true;
            note.Location = new Point(212, 10);
            bar.Controls.Add(note);

            UpdateLabelButtons();
            return bar;
        }

        private void UpdateLabelButtons()
        {
            if (_btnLabelTr == null) return;
            StyleToggle(_btnLabelTr, KeyLabels.Tr);
            StyleToggle(_btnLabelUs, !KeyLabels.Tr);
        }

        private static void StyleToggle(Button b, bool selected)
        {
            b.BackColor = selected ? Accent : Theme.Raised;
            b.ForeColor = selected ? Theme.AccentText : TxtClr;
            b.FlatAppearance.BorderSize = selected ? 0 : 1;
        }

        private void SetLabelMode(bool tr)
        {
            if (KeyLabels.Tr == tr) return;
            KeyLabels.SetMode(tr, true); // pencere.json'a kaydedilir
            RefreshKeyLabels();
            Log("Tuş yazıları: " + KeyLabels.ModeName + (tr ? " (Türkçe Q)" : " (İngilizce Q)"));
        }

        // Etiketlerin gösterildiği her yeri günceller (veri değişmez).
        private void RefreshKeyLabels()
        {
            UpdateLabelButtons();
            if (_keyboard != null) _keyboard.Invalidate();
            RefreshValueKeyboards();
            if (_grid != null)
            {
                bool old = _loading;
                _loading = true;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    KeyDef k = KeyboardLayout.Get((int)row.Tag);
                    if (k != null) row.Cells[0].Value = KeyboardPanel.DisplayName(k);
                }
                _loading = old;
            }
            UpdateSelectedKeyInfo();
            FillActionListInternal();
            if (_socdTab != null) _socdTab.RefreshLabels();
        }

        // Tuşa atanmış eylemin, seçili düzene uygun etiketi (profil verisindeki etiket değişmez).
        private static string AssignedLabel(KeyAssign a)
        {
            ActionDef d = Actions.Get(a.Code);
            return d != null ? KeyLabels.ActionLabel(d) : a.Label;
        }
    }
}
