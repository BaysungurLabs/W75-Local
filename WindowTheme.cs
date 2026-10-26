using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Pencere başlık çubuğunu koyu yapar (DWM). Tüm Form'lar aynı yerden kullanır: WindowTheme.Apply(this).
    // Her çağrı try/catch içindedir: Windows sürümü desteklemiyorsa sessizce geçilir, program normal çalışır.
    //   Windows 10 1809+  : koyu başlık çubuğu (DWMWA_USE_IMMERSIVE_DARK_MODE 20; eski 10 sürümlerinde 19).
    //   Windows 11 (22000+): ayrıca başlık rengi, ince kenar rengi, yazı rengi ve yuvarlak köşe.
    public static class WindowTheme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX { public int Size, Major, Minor, Build, Platform; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string CSD; }

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX v);

        private static int _build = -1;

        // .NET Framework, manifest yokken sürümü eski gösterir; gerçek build numarası RtlGetVersion'dan alınır.
        public static int WindowsBuild
        {
            get
            {
                if (_build >= 0) return _build;
                try
                {
                    OSVERSIONINFOEX v = new OSVERSIONINFOEX();
                    v.Size = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                    _build = RtlGetVersion(ref v) == 0 ? v.Build : 0;
                }
                catch { _build = 0; }
                return _build;
            }
        }

        private static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        // Form oluşturucusundan çağrılır: koyu başlık çubuğu + form açılırken denetim ağacının temaya uydurulması.
        public static void Setup(Form f)
        {
            Apply(f);
            try { f.Load += delegate { try { Theme.StyleTree(f); } catch { } }; } catch { }
        }

        public static void Apply(Form f)
        {
            try
            {
                if (f.IsHandleCreated) ApplyHandle(f);
                f.HandleCreated += delegate { ApplyHandle(f); };
            }
            catch { }
        }

        private static void Set(IntPtr h, int attr, int value)
        {
            try { DwmSetWindowAttribute(h, attr, ref value, sizeof(int)); } catch { }
        }

        private static void ApplyHandle(Form f)
        {
            try
            {
                IntPtr h = f.Handle;
                int dark = 1;
                int hr = 1;
                try { hr = DwmSetWindowAttribute(h, 20, ref dark, sizeof(int)); } catch { }
                if (hr != 0) Set(h, 19, 1); // eski Windows 10 sürümleri
                if (WindowsBuild >= 22000)
                {
                    Set(h, 35, Bgr(Theme.Bg));     // DWMWA_CAPTION_COLOR: pencere zemini
                    Set(h, 34, Bgr(Theme.Border)); // DWMWA_BORDER_COLOR: ince, düşük kontrastlı kenar
                    Set(h, 36, Bgr(Theme.Text2));  // DWMWA_TEXT_COLOR: yumuşak açık gri
                    Set(h, 33, 2);                 // DWMWA_WINDOW_CORNER_PREFERENCE: DWMWCP_ROUND
                }
            }
            catch { }
        }
    }
}
