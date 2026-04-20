using System;

namespace MonitAndRestart
{
    public class AppConfig
    {
        public string Path { get; set; }
        public string ProcessName { get; set; }

        // 啟動參數 ---
        public string Arguments { get; set; } = "";

        public int RestartDelay { get; set; } = 5;
        public int RestartInterval { get; set; } = 10;
        public int HangTimeout { get; set; } = 10;

        // 最大重啟次數 (0代表無限) ---
        public int MaxRestarts { get; set; } = 0;
        // 雙重確認設定 ---
        public bool DoubleCheckEnabled { get; set; } = false;
        public int DoubleCheckSeconds { get; set; } = 5;

        public bool ForceToTop { get; set; }
        public bool Enable { get; set; } = true;

        // 排程設定 (保持不變)
        public bool[] ScheduleDays { get; set; } = new bool[] { true, true, true, true, true, true, true };
        // Window 1
        public TimeSpan? ScheduleStart1 { get; set; } = null;
        public TimeSpan? ScheduleStop1 { get; set; } = null;

        // Window 2
        public TimeSpan? ScheduleStart2 { get; set; } = null;
        public TimeSpan? ScheduleStop2 { get; set; } = null;

        // Runtime State
        public string Status { get; set; } = "Stopped";
        public int Pid { get; set; } = 0;
        public int RestartCount { get; set; } = 0;
        public DateTime LastRestartTime { get; set; } = DateTime.MinValue;
        public DateTime? HangStartTime { get; set; } = null;

        // --- 新增：雙重確認計時器 ---
        public DateTime? VerificationStartTime { get; set; } = null;
    }

    public class GlobalSettings
    {
        public bool RunOnStartup { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;

        // --- 新增欄位 (對應截圖) ---
        public bool CheckUpdates { get; set; } = true;
        public bool LogToFile { get; set; } = true;
        public string LogPath { get; set; } = ""; // 預設為空，代表程式根目錄
        public int GracePeriod { get; set; } = 30; // 預設 60 秒
    }
}