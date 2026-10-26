using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Uygulama simgesi: exe'ye gömülü w75.ico kaynağından okunur (araclar\ikon-uret.ps1 üretir).
    // Okuma başarısız olursa null döner; form varsayılan simgeyle açılır, program çökmez.
    public static class AppIcon
    {
        private static Icon _icon;
        private static bool _tried;

        public static Icon Get()
        {
            if (_tried) return _icon;
            _tried = true;
            try
            {
                using (System.IO.Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("w75.ico"))
                {
                    if (st != null) _icon = new Icon(st);
                }
            }
            catch { _icon = null; }
            return _icon;
        }

        // Ana form ve özel pencereler için: başlık çubuğu, görev çubuğu ve Alt+Tab simgesi.
        public static void Apply(Form f)
        {
            try
            {
                Icon ic = Get();
                if (ic != null) { f.Icon = ic; f.ShowIcon = true; }
            }
            catch { }
        }
    }
}
