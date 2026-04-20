using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MonitAndRestart
{
    public class MonitorEngine
    {
        private readonly RestartEngine _restarter;

        public MonitorEngine()
        {
            _restarter = new RestartEngine();
        }

        /// <summary>
        /// 主監控迴圈，由外部 Timer 定期呼叫
        /// </summary>
        public void CheckApps(List<AppConfig> apps)
        {
            DateTime now = DateTime.Now;

            foreach (var app in apps)
            {
                if (!app.Enable) { UpdateStatus(app, "Disabled", 0); continue; }

                if (!IsWithinSchedule(app, now, out string scheduleStatus))
                {
                    EnsureClosed(app);
                    UpdateStatus(app, scheduleStatus, 0);
                    continue;
                }

                Process[] processes = Process.GetProcessesByName(app.ProcessName);
                Process target = processes.FirstOrDefault();
                bool isRunning = (target != null && !target.HasExited);
                bool isHung = false;

                if (isRunning)
                {
                    app.Pid = target.Id;
                    if (IsHung(target))
                    {
                        if (app.HangStartTime == null) app.HangStartTime = DateTime.Now;
                        double hungSeconds = (now - app.HangStartTime.Value).TotalSeconds;

                        if (hungSeconds > app.HangTimeout) isHung = true;
                        else
                        {
                            app.Status = $"Not Responding ({hungSeconds:F0}s / {app.HangTimeout}s)";
                            app.VerificationStartTime = null;
                            continue;
                        }
                    }
                    else
                    {
                        app.HangStartTime = null;
                        app.VerificationStartTime = null;
                        app.Status = "Running";
                        continue;
                    }
                }
                else
                {
                    app.Pid = 0;
                    app.HangStartTime = null;
                }

                // --- 故障處理 ---
                string failureReason = isHung ? "Hung Timeout" : "Crashed/Missing";

                // 【新增：檢查最大重啟次數限制】
                if (app.MaxRestarts > 0 && app.RestartCount >= app.MaxRestarts)
                {
                    app.Status = $"Max Restarts Reached ({app.MaxRestarts})";
                    // 為了安全，如果是 Hang 狀態，仍然把它殺掉，但不再重啟
                    if (isHung && target != null) _restarter.KillProcess(target);

                    app.VerificationStartTime = null;
                    continue;
                }

                if (app.DoubleCheckEnabled)
                {
                    if (app.VerificationStartTime == null)
                    {
                        app.VerificationStartTime = DateTime.Now;
                        Logger.Log($"Detected {failureReason} for {app.ProcessName}. Starting verification wait...", "WARN");
                    }

                    double verifySeconds = (now - app.VerificationStartTime.Value).TotalSeconds;
                    if (verifySeconds < app.DoubleCheckSeconds)
                    {
                        app.Status = $"Verifying Failure... ({verifySeconds:F0}s / {app.DoubleCheckSeconds}s)";
                        continue;
                    }
                    else
                    {
                        Logger.Log($"Verification confirmed: {app.ProcessName} is definitely {failureReason}.", "ERROR");
                        if (isHung && target != null) _restarter.KillProcess(target);
                        PerformRestart(app);
                    }
                }
                else
                {
                    Logger.Log($"Immediate restart triggered: {app.ProcessName} ({failureReason})", "ERROR");
                    if (isHung && target != null) _restarter.KillProcess(target);
                    PerformRestart(app);
                }
            }
        }
        // ---------------------------------------------------------
        // 輔助方法
        // ---------------------------------------------------------
        private void PerformRestart(AppConfig app)
        {
            _restarter.RestartApp(app);
            // 重啟指令發出後，重置所有錯誤狀態
            app.HangStartTime = null;
            app.VerificationStartTime = null;
        }

        private void UpdateStatus(AppConfig app, string status, int pid) { app.Status = status; app.Pid = pid; app.HangStartTime = null; app.VerificationStartTime = null; }

        private void EnsureClosed(AppConfig app)
        {
            try
            {
                Process[] running = Process.GetProcessesByName(app.ProcessName);

                foreach (var p in running)
                {
                    Logger.Log($"Closing process outside schedule: {app.ProcessName}", "INFO");
                    _restarter.KillProcess(p);
                }
            }
            catch { }

            app.Pid = 0;
            app.HangStartTime = null;
            app.VerificationStartTime = null;
        }

        private bool IsWithinSchedule(AppConfig app, DateTime now, out string status)
        {
            // 1. 檢查星期幾 (Day of Week)
            // C# DayOfWeek: Sunday=0, Monday=1... 
            // Config Checkbox: [0]=Mon ... [6]=Sun
            int dayIndex = (int)now.DayOfWeek - 1;
            if (dayIndex < 0) dayIndex = 6;

            if (app.ScheduleDays != null && dayIndex < app.ScheduleDays.Length && !app.ScheduleDays[dayIndex])
            {
                status = $"Scheduled Off ({now.DayOfWeek})";
                return false;
            }

            // --- 全新的時間區間邏輯 (完美支援雙區間與跨日) ---
            bool allEmpty = !app.ScheduleStart1.HasValue && !app.ScheduleStop1.HasValue &&
                            !app.ScheduleStart2.HasValue && !app.ScheduleStop2.HasValue;

            if (allEmpty) { status = "Running"; return true; }

            TimeSpan nowTs = now.TimeOfDay;
            bool inWin1 = IsInWindow(nowTs, app.ScheduleStart1, app.ScheduleStop1);
            bool inWin2 = IsInWindow(nowTs, app.ScheduleStart2, app.ScheduleStop2);

            if (inWin1 || inWin2)
            {
                status = "Running";
                return true;
            }

            status = "Scheduled Stop (Time)";
            return false;
        }

        /// <summary>
        /// 判斷當下時間是否落在指定的 [Start, Stop) 區間內
        /// </summary>
        private bool IsInWindow(TimeSpan now, TimeSpan? start, TimeSpan? stop)
        {
            // 如果該區間完全沒設定，視為無效區間
            if (!start.HasValue && !stop.HasValue) return false;

            // 只有 Start：Start 之後一路執行
            if (start.HasValue && !stop.HasValue) return now >= start.Value;

            // 只有 Stop：00:00 一路執行到 Stop
            if (!start.HasValue && stop.HasValue) return now < stop.Value;

            TimeSpan s = start.Value;
            TimeSpan e = stop.Value;

            // 正常區間 (例如 08:00 ~ 18:00)
            if (s < e) return now >= s && now < e;
            // 跨日區間 (例如 23:00 ~ 02:00)
            else return now >= s || now < e;
        }

        private bool IsHung(Process p)
        {
            if (p.MainWindowHandle == IntPtr.Zero) return false;

            IntPtr result;
            // 使用 SendMessageTimeout 測試視窗回應
            // Timeout 設為 1000ms (快速檢測)，真正的容忍時間由外部 HangTimeout 控制
            IntPtr ret = NativeMethods.SendMessageTimeout(
                p.MainWindowHandle,
                NativeMethods.WM_NULL,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.SMTO_ABORTIFHUNG,
                1000,
                out result);

            if (ret == IntPtr.Zero) return true; // Win32 API 說它掛了

            p.Refresh();
            return !p.Responding; // .NET 屬性說它掛了
        }
    }
}