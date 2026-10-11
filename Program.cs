using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace QuodePaletteCapture
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(true, "QuodePaletteCapture_SingleInstance_Mutex", out bool isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show(
                    "Quode Palette Capture is already running. Check your system tray.",
                    "Quode Palette Capture",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppTheme.TryEnableAppDarkMode();

            Application.ThreadException += (s, e) => ShowUnexpectedError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    ShowUnexpectedError(ex);
                }
            };

            Application.Run(new MainForm());
        }

        /// <summary>
        /// Shows the exception type, message and inner-exception chain in a dialog, and
        /// writes the same details plus the stack trace to a log file next to the exe,
        /// so intermittent issues can be diagnosed later.
        /// </summary>
        internal static void ShowUnexpectedError(Exception ex)
        {
            var sb = new StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (depth > 0)
                {
                    sb.Append("\n\nCaused by: ");
                }
                sb.Append(current.GetType().FullName).Append(": ");
                sb.Append(string.IsNullOrEmpty(current.Message) ? "(no message)" : current.Message);
                current = current.InnerException;
                depth++;
            }

            try
            {
                string logPath = Path.Combine(AppContext.BaseDirectory, "QuodePaletteCapture_error.log");
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
            }
            catch
            {
                // Logging is best effort. A failed write should never hide the original error.
            }

            MessageBox.Show(
                "An unexpected error occurred:\n\n" + sb +
                "\n\nDetails were also saved to QuodePaletteCapture_error.log next to the exe.",
                "Quode Palette Capture",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
