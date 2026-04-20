using System;
using System.IO;

namespace MonitAndRestart
{
    public static class Logger
    {
        private static readonly object _lock = new object();

        // 這些屬性現在由 SettingsForm 更新
        public static bool Enabled { get; set; } = true;
        public static string CustomLogPath { get; set; } = "";

        public static void Log(string message, string type = "INFO")
        {
            if (!Enabled) return;

            try
            {
                lock (_lock)
                {
                    // 決定 log 檔案位置
                    string folder = string.IsNullOrWhiteSpace(CustomLogPath)
                        ? AppDomain.CurrentDomain.BaseDirectory
                        : CustomLogPath;

                    // 確保目錄存在
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, "log.txt");
                    string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {message}";
                    File.AppendAllText(filePath, logLine + Environment.NewLine);
                }
            }
            catch { /* 忽略錯誤 */ }
        }
    }
}