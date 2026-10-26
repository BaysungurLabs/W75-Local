using System;
using System.Drawing;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Işıklandırma sekmesi: "Yön" açılır kutusunun yerine görsel yön seçici (ok kartları) ve önizlemede yön oku/akışı.
    // Gösterilen kartlar modun izin verdiği değerlerdir (mod-secenekleri.json); etiketler yon-etiketleri.json'dadır
    // (yön kullanan beş modun hepsinde gerçek klavyede doğrulandı).
    // Bu dosya cihaza komut göndermez; yön değeri yalnızca profildeki LightSetting.Direction alanına yazılır.
    public partial class MainForm
    {
        private DirPicker BuildDirPicker(int y)
        {
            DirPicker p = new DirPicker();
            p.Bounds = new Rectangle(0, y, 380, 64);
            p.BackColor = PanelBg;
            p.AccentColor = Accent;
            p.TextColor = TxtClr;
            p.MutedColor = Muted;
            // Yalnızca kullanıcı bir kart seçince: yön değeri profile yazılır (mod değişiminde otomatik düzeltme ayrı: OnLightModeChanged).
            p.ValueChanged += delegate
            {
                if (_loading) return;
                Current.Light.Direction = _lightDir.SelectedValue;
                LightChanged();
            };
            return p;
        }

        // Seçili yöne göre önizleme oku/metni. Yön kontrolü olmayan modda (ve Statikte) ok çizilmez; profildeki değer
        // modun izin verdiği değerlerden biri değilse de çizilmez.
        private void UpdatePreviewDirection(LightSetting l)
        {
            if (_lightPreview == null) return;
            bool show = ModeOptions.AllowsDirection(l.Mode, l.Direction);
            DirLabels.Entry en = DirLabels.Get(l.Mode, l.Direction);
            _lightPreview.DirArrow = show ? en.Arrow : "";
            _lightPreview.DirFlow = en.Flow;
            _lightPreview.DirText = show ? en.Text : "";
        }
    }
}
