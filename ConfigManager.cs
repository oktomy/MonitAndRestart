using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonitAndRestart
{
    public class ConfigManager
    {
        private readonly string _iniPath;

        public ConfigManager()
        {
            _iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        }

        public GlobalSettings LoadGlobalSettings()
        {
            var settings = new GlobalSettings();
            // 既有設定
            settings.RunOnStartup = bool.Parse(NativeMethods.ReadIni("General", "RunOnStartup", _iniPath, "false"));
            settings.StartMinimized = bool.Parse(NativeMethods.ReadIni("General", "StartMinimized", _iniPath, "false"));
            settings.MinimizeToTray = bool.Parse(NativeMethods.ReadIni("General", "MinimizeToTray", _iniPath, "true"));

            // 新增設定 (對應截圖)
            settings.CheckUpdates = bool.Parse(NativeMethods.ReadIni("General", "CheckUpdates", _iniPath, "true"));
            settings.LogToFile = bool.Parse(NativeMethods.ReadIni("General", "LogToFile", _iniPath, "true"));
            settings.LogPath = NativeMethods.ReadIni("General", "LogPath", _iniPath, "");
            settings.GracePeriod = int.Parse(NativeMethods.ReadIni("General", "GracePeriod", _iniPath, "60"));

            return settings;
        }

        public void SaveGlobalSettings(GlobalSettings settings)
        {
            NativeMethods.WriteIni("General", "RunOnStartup", settings.RunOnStartup.ToString(), _iniPath);
            NativeMethods.WriteIni("General", "StartMinimized", settings.StartMinimized.ToString(), _iniPath);
            NativeMethods.WriteIni("General", "MinimizeToTray", settings.MinimizeToTray.ToString(), _iniPath);

            // 新增設定儲存
            NativeMethods.WriteIni("General", "CheckUpdates", settings.CheckUpdates.ToString(), _iniPath);
            NativeMethods.WriteIni("General", "LogToFile", settings.LogToFile.ToString(), _iniPath);
            NativeMethods.WriteIni("General", "LogPath", settings.LogPath, _iniPath);
            NativeMethods.WriteIni("General", "GracePeriod", settings.GracePeriod.ToString(), _iniPath);
        }

        public List<AppConfig> LoadApps()
        {
            var list = new List<AppConfig>();
            int count = int.Parse(NativeMethods.ReadIni("Apps", "Count", _iniPath, "0"));

            for (int i = 1; i <= count; i++)
            {
                string section = $"App{i}";
                var app = new AppConfig
                {
                    Path = NativeMethods.ReadIni(section, "Path", _iniPath),
                    ProcessName = NativeMethods.ReadIni(section, "ProcessName", _iniPath),
                    Arguments = NativeMethods.ReadIni(section, "Arguments", _iniPath, ""),

                    RestartDelay = int.Parse(NativeMethods.ReadIni(section, "RestartDelay", _iniPath, "5")),
                    RestartInterval = int.Parse(NativeMethods.ReadIni(section, "RestartInterval", _iniPath, "10")),
                    HangTimeout = int.Parse(NativeMethods.ReadIni(section, "HangTimeout", _iniPath, "10")),

                    // 讀取最大重啟次數
                    MaxRestarts = int.Parse(NativeMethods.ReadIni(section, "MaxRestarts", _iniPath, "0")),

                    DoubleCheckEnabled = bool.Parse(NativeMethods.ReadIni(section, "DoubleCheckEnabled", _iniPath, "false")),
                    DoubleCheckSeconds = int.Parse(NativeMethods.ReadIni(section, "DoubleCheckSeconds", _iniPath, "5")),
                    ForceToTop = bool.Parse(NativeMethods.ReadIni(section, "ForceToTop", _iniPath, "false")),
                    Enable = bool.Parse(NativeMethods.ReadIni(section, "Enable", _iniPath, "true"))
                };

                // 排程讀取
                // Read Days (Format: 1,1,1,1,1,0,0)
                string daysStr = NativeMethods.ReadIni(section, "ScheduleDays", _iniPath, "1,1,1,1,1,1,1");
                var daysArr = daysStr.Split(',').Select(x => x == "1").ToArray();
                if (daysArr.Length == 7) app.ScheduleDays = daysArr;

                app.ScheduleStart1 = ParseTime(NativeMethods.ReadIni(section, "ScheduleStart1", _iniPath, ""));

                // 向下相容：先嘗試讀取 Stop1，若無則讀取舊版 Stop
                string oldStop = NativeMethods.ReadIni(section, "ScheduleStop", _iniPath, "");
                app.ScheduleStop1 = ParseTime(NativeMethods.ReadIni(section, "ScheduleStop1", _iniPath, oldStop));

                app.ScheduleStart2 = ParseTime(NativeMethods.ReadIni(section, "ScheduleStart2", _iniPath, ""));
                app.ScheduleStop2 = ParseTime(NativeMethods.ReadIni(section, "ScheduleStop2", _iniPath, "")); // 新增 Stop2

                list.Add(app);
            }
            return list;
        }

        public void SaveApps(List<AppConfig> apps)
        {
            NativeMethods.WriteIni("Apps", "Count", apps.Count.ToString(), _iniPath);
            for (int i = 0; i < apps.Count; i++)
            {
                string section = $"App{i + 1}";
                var app = apps[i];

                NativeMethods.WriteIni(section, "Path", app.Path, _iniPath);
                NativeMethods.WriteIni(section, "ProcessName", app.ProcessName, _iniPath);
                // 新增：儲存參數
                NativeMethods.WriteIni(section, "Arguments", app.Arguments, _iniPath);

                NativeMethods.WriteIni(section, "RestartDelay", app.RestartDelay.ToString(), _iniPath);
                NativeMethods.WriteIni(section, "RestartInterval", app.RestartInterval.ToString(), _iniPath);
                NativeMethods.WriteIni(section, "HangTimeout", app.HangTimeout.ToString(), _iniPath);

                // 新增：儲存雙重確認
                NativeMethods.WriteIni(section, "DoubleCheckEnabled", app.DoubleCheckEnabled.ToString(), _iniPath);
                NativeMethods.WriteIni(section, "DoubleCheckSeconds", app.DoubleCheckSeconds.ToString(), _iniPath);

                NativeMethods.WriteIni(section, "ForceToTop", app.ForceToTop.ToString(), _iniPath);
                NativeMethods.WriteIni(section, "Enable", app.Enable.ToString(), _iniPath);
                NativeMethods.WriteIni(section, "MaxRestarts", app.MaxRestarts.ToString(), _iniPath); // 儲存最大重啟次數

                // --- Save Schedule ---
                string daysStr = string.Join(",", app.ScheduleDays.Select(b => b ? "1" : "0"));
                NativeMethods.WriteIni(section, "ScheduleDays", daysStr, _iniPath);

                NativeMethods.WriteIni(section, "ScheduleStart1", FormatTime(app.ScheduleStart1), _iniPath);
                NativeMethods.WriteIni(section, "ScheduleStop1", FormatTime(app.ScheduleStop1), _iniPath);
                NativeMethods.WriteIni(section, "ScheduleStart2", FormatTime(app.ScheduleStart2), _iniPath);
                NativeMethods.WriteIni(section, "ScheduleStop2", FormatTime(app.ScheduleStop2), _iniPath);
            }
        }

        // Helper Methods for Time
        private TimeSpan? ParseTime(string s)
        {
            if (TimeSpan.TryParse(s, out TimeSpan ts)) return ts;
            return null;
        }

        private string FormatTime(TimeSpan? ts)
        {
            return ts.HasValue ? ts.Value.ToString(@"hh\:mm") : "";
        }
    }
}