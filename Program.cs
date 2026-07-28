using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CTL472_OsuDriver
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogException(e.ExceptionObject as Exception);
            };

            Application.ThreadException += (s, e) =>
            {
                LogException(e.Exception);
            };

            try
            {
                SetProcessDPIAware();
            }
            catch { }

            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private static void LogException(Exception ex)
        {
            if (ex == null) return;
            string msg = string.Format("[{0}] Unhandled Exception: {1}\nStackTrace: {2}\n", DateTime.Now, ex.Message, ex.StackTrace);
            try
            {
                File.AppendAllText("error.log", msg);
            }
            catch { }
            MessageBox.Show(ex.Message, "CTL-472 Driver Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
