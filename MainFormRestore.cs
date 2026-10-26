using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // İçe Aktar, "Profili Sıfırla" yedeğini (profiles.json'un tam kopyası) okuyunca: önce hangi profil(ler)in geri yükleneceği
    // sorulur, ardından onay istenir; geri yüklemeden hemen önce mevcut durumun otomatik yedeği alınır. Dosya bozuksa ya da
    // yedekte geri yüklenecek profil yoksa hiçbir şey değiştirilmez ve hata gösterilir. Klavyeye hiçbir komut gönderilmez.
    public partial class MainForm
    {
        // Test kipinde (W75_CIHAZSIZ=1) W75_ICE_AKTAR_DOSYA ayarlıysa dosya penceresi açılmaz, bu yol kullanılır.
        private static string TestImportFile()
        {
            if (!TestMode) return null;
            string p = Environment.GetEnvironmentVariable("W75_ICE_AKTAR_DOSYA");
            return string.IsNullOrEmpty(p) ? null : p;
        }

        // Kullanıcıya gösterilen profil adı: cihaz numarasıyla (Günlük = 4. profil). Cihaz sınırı dışındaki yedek satırı program sırası olmadan yazılır.
        private static string SlotName(int i)
        {
            return DeviceLimits.SlotAllowed(i) ? "Klavye profili " + DeviceProfiles.ToDevice(i) : "Yedekteki " + (i + 1) + ". profil";
        }

        private static string ProfileJson(Profile p) { return new JavaScriptSerializer().Serialize(p); }

        private void RestoreFromBackup(string path, List<Profile> list)
        {
            // Geri yüklenebilir profiller: yedekte var (boş değil) ve mevcut slot sayısı içinde.
            // Cihaz yalnızca DeviceLimits.MaxSlots profil saklar: yedekte daha fazla profil varsa (ör. 5 profilli eski yedek)
            // sınırın ötesindekiler listelenir ama geri yüklenemez.
            List<int> slots = new List<int>();
            List<string> satirlar = new List<string>();
            List<bool> isaret = new List<bool>();
            List<bool> izinli = new List<bool>();
            int restorable = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Profile b = list[i];
                if (b == null) continue;
                string adB = string.IsNullOrEmpty(b.Name) ? "(adsız)" : b.Name;
                slots.Add(i);
                if (!DeviceLimits.SlotAllowed(i) || i >= _data.Profiles.Count)
                {
                    satirlar.Add(SlotName(i) + " · " + adB + "   — Cihaz " + DeviceLimits.MaxSlots + " profil destekliyor, bu profil geri yüklenemez");
                    isaret.Add(false); izinli.Add(false);
                    continue;
                }
                Profile cur = _data.Profiles[i];
                bool farkli = ProfileJson(Clone4(b, i)) != ProfileJson(cur);
                satirlar.Add(SlotName(i) + " · " + adB + (adB != cur.Name ? "   (şu an: " + cur.Name + ")" : "") + "   — " + (farkli ? "mevcuttan farklı" : "mevcutla aynı"));
                isaret.Add(farkli); izinli.Add(true);
                restorable++;
            }
            if (restorable == 0) throw new InvalidDataException("Yedekte geri yüklenebilir profil yok (cihaz " + DeviceLimits.MaxSlots + " profil destekliyor).");

            List<int> secilen;
            using (RestoreDialog dlg = new RestoreDialog(satirlar, isaret, izinli, System.IO.Path.GetFileName(path), list.Count))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                secilen = dlg.SelectedIndices;
            }
            if (secilen.Count == 0) { Log("Yedekten geri yükleme: profil seçilmedi, bir şey değiştirilmedi."); return; }

            StringBuilder ozet = new StringBuilder();
            foreach (int k in secilen)
            {
                int slot = slots[k];
                ozet.Append("• ").Append(SlotName(slot)).Append(": yedekteki '").Append(list[slot].Name).Append("' → mevcut '").Append(_data.Profiles[slot].Name).Append("' üzerine yazılacak\n");
            }
            if (MessageBox.Show(this, "Şu profiller geri yüklenecek:\n\n" + ozet +
                "\nGeri yüklemeden hemen önce mevcut durumun otomatik yedeği alınır. Klavyeye bir şey yazılmaz; Cihaza Yaz gerekir.\n\nDevam edilsin mi?",
                "Yedekten geri yükle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            // Geri yüklemeden hemen önce mevcut durumun otomatik yedeği (başarısız olursa hiçbir şey değiştirilmez).
            string onceki = Path.Combine(Store.BackupDir, "profiles-ice-aktarma-oncesi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            try { if (File.Exists(Store.FilePath)) File.Copy(Store.FilePath, onceki, false); }
            catch (Exception ex) { throw new IOException("Otomatik yedek alınamadı: " + ex.Message); }

            AppData tmp = new AppData();
            tmp.Profiles = new List<Profile>();
            for (int i = 0; i < _data.Profiles.Count; i++)
            {
                bool al = false;
                foreach (int k in secilen) if (slots[k] == i) al = true;
                tmp.Profiles.Add(al ? list[i] : _data.Profiles[i]);
            }
            tmp.SelectedSlot = _data.SelectedSlot;
            Store.Normalize(tmp); // boş listeleri düzeltir, slot numaralarını yerine koyar
            _data.Profiles = tmp.Profiles;
            Store.Save(_data);
            RefreshProfileList();
            LoadProfileUi();

            StringBuilder adlar = new StringBuilder();
            foreach (int k in secilen) adlar.Append(adlar.Length > 0 ? ", " : "").Append(DeviceProfiles.ToDevice(slots[k]));
            Log("Yedekten geri yüklendi: klavye profili " + adlar + " (" + path + "). Önceki durumun yedeği: " + onceki + ". Klavyeye bir şey yazılmadı.");
            MessageBox.Show(this, "Geri yüklendi: klavye profili " + adlar + ".\n\nÖnceki durumun yedeği:\n" + onceki + "\n\nKlavyeye yazmak için profili seçip Cihaza Yaz'ı kullanın.",
                "Yedekten geri yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Karşılaştırma için: yedekteki profilin, geri yüklenince alacağı slot numarasıyla kopyası.
        private static Profile Clone4(Profile b, int slot)
        {
            Profile c = b.Clone();
            c.Slot = slot;
            return c;
        }
    }

    // Yedekten geri yüklenecek profilleri seçtiren küçük pencere.
    public class RestoreDialog : Form
    {
        private readonly CheckedListBox _list = new CheckedListBox();
        public List<int> SelectedIndices = new List<int>();

        private readonly List<bool> _allowed;
        private bool _init = true;

        public RestoreDialog(List<string> lines, List<bool> preChecked, List<bool> allowed, string fileName, int profileCount)
        {
            _allowed = allowed;
            AppIcon.Apply(this); WindowTheme.Setup(this);
            Text = "Yedekten geri yükle";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(560, 330);
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);

            Label head = new Label();
            head.Text = "Hangi profil(ler) geri yüklensin?";
            head.Font = new Font("Segoe UI Semibold", 11f);
            head.AutoSize = true; head.Location = new Point(16, 14);
            Controls.Add(head);

            Label sub = new Label();
            sub.Text = "Yedek: " + fileName + "  (" + profileCount + " profil). Yalnızca işaretli profiller geri yüklenir; mevcut profillerin üzerine yazılır. " +
                       "Seçili olanlar mevcuttan farklı olan profillerdir.";
            sub.ForeColor = Theme.Muted;
            sub.SetBounds(16, 42, 528, 44);
            Controls.Add(sub);

            _list.SetBounds(16, 92, 528, 170);
            _list.BackColor = Theme.Raised;
            _list.ForeColor = Theme.Text;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.CheckOnClick = true;
            _list.IntegralHeight = false;
            for (int i = 0; i < lines.Count; i++) _list.Items.Add(lines[i], preChecked[i] && allowed[i]);
            _init = false;
            // Cihaz slot sınırının ötesindeki profil işaretlenemez.
            _list.ItemCheck += delegate(object s, ItemCheckEventArgs e)
            {
                if (_init || e.NewValue != CheckState.Checked || _allowed[e.Index]) return;
                e.NewValue = CheckState.Unchecked;
                MessageBox.Show(this, "Cihaz " + DeviceLimits.MaxSlots + " profil destekliyor, bu profil geri yüklenemez.", "Yedekten geri yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(_list);

            Button ok = MakeButton("Geri yükle", 332);
            Button cancel = MakeButton("Vazgeç", 438);
            ok.DialogResult = DialogResult.OK;
            cancel.DialogResult = DialogResult.Cancel;
            ok.BackColor = Theme.Accent;
            ok.ForeColor = Theme.AccentText;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;

            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (DialogResult != DialogResult.OK) return;
                SelectedIndices.Clear();
                foreach (int i in _list.CheckedIndices) SelectedIndices.Add(i);
            };
        }

        private Button MakeButton(string text, int x)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, 280, 100, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.BackColor = Theme.Raised;
            b.ForeColor = Theme.Text;
            b.UseVisualStyleBackColor = false;
            return b;
        }
    }
}
