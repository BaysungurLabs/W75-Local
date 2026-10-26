using System;
using System.Drawing;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Işıklandırma sekmesi: her ışık modunda yalnızca o modun desteklediği kontrolleri gösterir (mod-secenekleri.json).
    // Gizlenen kontrol yer kaplamaz (kalanlar yukarı kayar). Gizlenen kontrolün değeri profilde korunur ve WriteLight
    // paketi değişmez; yalnızca arayüz değişir.
    public partial class MainForm
    {
        private Panel _lightLeft;
        private Label _lblMode, _lblBright, _lblSpeed, _lblDir, _lightNote;
        private const int RowGap = 0;

        // Seçili mod için gizle/göster ve yeniden diz. Cihaza komut göndermez, profili değiştirmez.
        private void ApplyModeOptions(int mode)
        {
            if (_lightLeft == null) return;
            ModeOpt o = ModeOptions.Get(mode);
            _lightDir.SetMode(mode);
            if (_lightPreview != null)
            {
                // Özel (10): önizleme eski davranışında kalır (tuş boyama için yeni bir şey eklenmedi).
                _lightPreview.FullColorUsed = mode == 10 || o.FullColor;
                _lightPreview.BgUsed = mode == 10 || o.BgColor;
                _lightPreview.Invalidate();
            }

            _lightLeft.SuspendLayout();
            int y = 0;
            _lblMode.Top = y; _lightMode.Top = y + 20;
            y += 62;

            // Parlaklık / Hız aynı satırda: biri gizliyse diğeri sol sütuna geçmez (Hız her zaman x=140).
            _lblBright.Visible = _lightBrightness.Visible = o.Brightness;
            _lblSpeed.Visible = _lightSpeed.Visible = o.Speed;
            if (o.Brightness || o.Speed)
            {
                _lblBright.Top = y; _lightBrightness.Top = y + 20;
                _lblSpeed.Top = y; _lightSpeed.Top = y + 20;
                y += 60;
            }

            bool dir = o.Dirs.Length > 0;
            _lblDir.Visible = _lightDir.Visible = dir;
            if (dir) { _lblDir.Top = y; _lightDir.Top = y + 20; y += 94; }

            _lightPower.Top = y;
            _lightFull.Visible = o.FullColor;
            _lightFull.Top = y;
            y += 36;

            _btnFg.Visible = _hexFgFrame.Visible = o.FgColor;
            _btnBg.Visible = _hexBgFrame.Visible = o.BgColor;
            _btnFg.Top = y; _btnBg.Top = y;
            _hexFgFrame.Top = y + 42; _hexBgFrame.Top = y + 42;
            y += 82;

            y = LayoutLiveLightRows(y);   // Canlı uygula + açıklama + durum satırı (MainFormLiveLight.cs)
            _lightNote.Top = y;
            _lightLeft.ResumeLayout(true);
        }

        // Kullanıcı modu değiştirdi: yön, modun izin verdiği değerlerden biri değilse modun ilk geçerli değerine çekilir
        // (yalnızca kullanıcı mod değiştirdiğinde; dosyadan yüklenen değer sessizce değiştirilmez).
        private void OnLightModeChanged(LightSetting l)
        {
            ApplyModeOptions(l.Mode);
            int[] d = ModeOptions.Get(l.Mode).Dirs;
            if (d.Length > 0 && Array.IndexOf(d, l.Direction) < 0)
            {
                l.Direction = d[0];
                _lightDir.SetSelected(l.Direction);
            }
        }
    }
}
