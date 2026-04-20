using System;
using System.Windows.Forms;

namespace MonitAndRestart
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 處理未捕捉的例外，防止工具本身崩潰
            Application.ThreadException += (s, e) => MonitAndRestart.Logger.Log($"Critical Error: {e.Exception.Message}", "FATAL");

            Application.Run(new MainForm());
        }
    }
}