using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace W75Ayarlayici
{
    internal static class Program
    {
        public static string CrashLog = Path.Combine(Path.GetTempPath(), "w75-crash.log");

        private static void LogCrash(Exception ex)
        {
            try
            {
                File.AppendAllText(CrashLog, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }

        // Dosya yazılamadığında (salt okunur klasör, başka program dosyayı kullanıyor) sade bir mesaj; diğer hatalarda genel mesaj.
        private static void ShowError(Exception ex)
        {
            LogCrash(ex);
            if (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("Bir dosya okunamadı ya da yazılamadı.\n\nVeri klasörü salt okunur olabilir ya da başka bir program dosyayı kullanıyor olabilir." +
                    "\n\nAyrıntı: " + ex.Message + "\n\nProgram çalışmaya devam eder; ayarlarınız bu işlemde kaydedilmemiş olabilir.",
                    "W75 Local", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show("Beklenmeyen bir hata oluştu:\n" + (ex != null ? ex.Message : "bilinmiyor") + "\n\nAyrıntılar: " + CrashLog,
                "W75 Local", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Aynı ayar klasörünü kullanan ikinci kopya açılmasın (ayarlar birbirinin üzerine yazılır). Farklı veri klasörü (test) serbest.
        private static string InstanceName()
        {
            string dir = Environment.GetEnvironmentVariable("W75_VERI_KLASORU");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "W75Ayarlayici");
            using (SHA1 sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(dir.TrimEnd('\\').ToLowerInvariant()));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
                return "Local\\W75Ayarlayici-" + sb;
            }
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { ShowError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                ShowError(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
            };
            bool first;
            Mutex instance = null;
            try { instance = new Mutex(true, InstanceName(), out first); }
            catch { first = true; } // mutex kurulamazsa programı engelleme
            if (!first)
            {
                MessageBox.Show("W75 Local programı zaten açık.\n\nAynı ayar klasörünü kullanan ikinci bir kopya açılamaz (ayarlar birbirinin üzerine yazılabilir). Lütfen açık olan pencereyi kullanın.",
                    "W75 Local", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Açılış sırasında ayar klasörü oluşturulamadı / okunamadı: program devam edemez.
                    LogCrash(ex);
                    MessageBox.Show("Program açılamadı.\n\nAyar klasörü oluşturulamadı ya da yazılamıyor. Klasörün var olduğundan ve yazma izniniz olduğundan emin olun." +
                        "\n\nAyrıntı: " + ex.Message, "W75 Local", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else ShowError(ex);
            }
            finally
            {
                if (instance != null) { try { instance.ReleaseMutex(); } catch { } instance.Dispose(); }
            }
        }
    }
}