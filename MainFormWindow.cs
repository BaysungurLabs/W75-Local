using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    // Pencere boyutu davranışı: ilk açılışta büyütülmüş; Normal duruma dönünce varsayılan ya da son kaydedilen boyut.
    public class WindowBoundsData
    {
        public int Left;
        public int Top;
        public int Width;
        public int Height;
        public string KeyLabelMode; // "TR" veya "US": klavye çizimindeki tuş yazıları (pencere ayarlarıyla birlikte saklanır)
        public bool ClickActivateOff; // true: "Profile tıklayınca klavyede etkinleştir" kapalı (varsayılan açık)
    }

    public partial class MainForm
    {
        private static readonly Size MinWindowSize = new Size(1080, 600);
        private const int DefaultWindowMinWidth = 1240;
        private const int DefaultWindowMaxWidth = 1700;
        private const int DefaultWindowMinHeight = 700;
        private const int DefaultWindowMaxHeight = 1200;

        private static string WindowFilePath { get { return Path.Combine(Store.Dir, "pencere.json"); } }

        // Normal durumdaki boyutu belirler (kaydedilmiş ve ekrana sığıyorsa onu, yoksa varsayılanı), sonra büyütür.
        private void ApplyInitialWindowBounds()
        {
            MinimumSize = MinWindowSize;
            StartPosition = FormStartPosition.Manual;

            Rectangle bounds;
            bool saved = TryLoadSavedBounds(out bounds);
            if (!saved) bounds = DefaultBounds(Screen.PrimaryScreen.WorkingArea);
            Bounds = bounds;
            WindowState = FormWindowState.Maximized; // ilk açılışta büyütülmüş

            // Büyütülmüş başlayan pencere Normal'e dönerken Windows eski/tasarım boyutuna dönebiliyor.
            // Bu yüzden ilk Normal geçişte hedef boyut (kayıtlı ya da varsayılan) açıkça uygulanır.
            _pendingNormalBounds = bounds;
            _pendingIsDefault = !saved;
            _sawMaximized = WindowState == FormWindowState.Maximized;
            SizeChanged += ApplyPendingNormalBounds;

            ResizeEnd += delegate { SaveWindowBounds(); };
            FormClosing += delegate { SaveWindowBounds(); };
        }

        private Rectangle? _pendingNormalBounds;
        private bool _pendingIsDefault;
        private bool _applyingBounds;
        private bool _sawMaximized;

        private void ApplyPendingNormalBounds(object sender, EventArgs e)
        {
            if (_applyingBounds || _pendingNormalBounds == null) return;
            if (WindowState == FormWindowState.Maximized) { _sawMaximized = true; return; }
            if (WindowState != FormWindowState.Normal || !_sawMaximized) return; // yalnızca büyütülmüşten Normal'e dönüşte
            Rectangle b = _pendingNormalBounds.Value;
            if (_pendingIsDefault) b = DefaultBounds(Screen.FromControl(this).WorkingArea); // pencerenin olduğu ekrana göre
            _pendingNormalBounds = null;
            SizeChanged -= ApplyPendingNormalBounds;
            _applyingBounds = true;
            try { Bounds = b; }
            finally { _applyingBounds = false; }
        }

        // Varsayılan Normal boyut çalışma alanına oranlıdır (aşağıya bakın); ekrana sığmıyorsa çalışma alanına sığar, ortalanır.
        private static Rectangle DefaultBounds(Rectangle wa)
        {
            // Çalışma alanına oranlı: genişlik %65 (1240–1700), yükseklik %92 (700–1200); ekrana sığmıyorsa küçülür.
            int w = Math.Max(DefaultWindowMinWidth, Math.Min(DefaultWindowMaxWidth, (int)(wa.Width * 0.65)));
            int h = Math.Max(DefaultWindowMinHeight, Math.Min(DefaultWindowMaxHeight, (int)(wa.Height * 0.92)));
            w = Math.Min(w, wa.Width);
            h = Math.Min(h, wa.Height);
            int x = wa.Left + (wa.Width - w) / 2;
            int y = wa.Top + (wa.Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private static bool TryLoadSavedBounds(out Rectangle result)
        {
            result = Rectangle.Empty;
            try
            {
                if (!File.Exists(WindowFilePath)) return false;
                WindowBoundsData d = new JavaScriptSerializer().Deserialize<WindowBoundsData>(File.ReadAllText(WindowFilePath, Encoding.UTF8));
                if (d == null) return false;
                if (d.Width < MinWindowSize.Width || d.Height < MinWindowSize.Height || d.Width > 20000 || d.Height > 20000) return false;
                Rectangle r = new Rectangle(d.Left, d.Top, d.Width, d.Height);
                Rectangle wa = Screen.FromRectangle(r).WorkingArea;
                if (r.Width > wa.Width || r.Height > wa.Height) return false; // ekrana sığmıyor: varsayılana dön
                // Ekran dışında kalmasın.
                r.X = Math.Max(wa.Left, Math.Min(r.X, wa.Right - r.Width));
                r.Y = Math.Max(wa.Top, Math.Min(r.Y, wa.Bottom - r.Height));
                result = r;
                return true;
            }
            catch
            {
                return false; // bozuk dosya: varsayılan boyuta dön
            }
        }

        // Yalnızca Normal durumdayken kaydedilir (büyütülmüş/küçültülmüş boyut kaydedilmez).
        private void SaveWindowBounds()
        {
            try
            {
                if (WindowState != FormWindowState.Normal) return;
                Rectangle b = Bounds;
                if (b.Width < MinWindowSize.Width || b.Height < MinWindowSize.Height) return;
                WindowBoundsData d = new WindowBoundsData();
                d.Left = b.Left; d.Top = b.Top; d.Width = b.Width; d.Height = b.Height; d.KeyLabelMode = KeyLabels.ModeName;
                d.ClickActivateOff = !_clickActivate;
                File.WriteAllText(WindowFilePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
