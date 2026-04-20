using System;
using System.Diagnostics;
using System.Threading;

namespace MonitAndRestart
{
    public class RestartEngine
    {
        public void KillProcess(Process p)
        {
            try
            {
                if (p != null && !p.HasExited)
                {
                    Logger.Log($"Killing process {p.ProcessName} (PID: {p.Id})", "WARN");
                    p.Kill();
                    p.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to kill process: {ex.Message}", "ERROR");
            }
        }

        public void RestartApp(AppConfig app)
        {
            // Interval Check
            if ((DateTime.Now - app.LastRestartTime).TotalSeconds < app.RestartInterval)
                return;

            Logger.Log($"Initiating restart sequence for: {app.ProcessName}");

            if (app.RestartDelay > 0)
                Thread.Sleep(app.RestartDelay * 1000);

            try
            {
                Process p = new Process();
                p.StartInfo.FileName = app.Path;

                // --- 新增：設定啟動參數 ---
                if (!string.IsNullOrWhiteSpace(app.Arguments))
                {
                    p.StartInfo.Arguments = app.Arguments;
                    Logger.Log($"Command args: {app.Arguments}", "DEBUG");
                }

                p.StartInfo.UseShellExecute = true;
                p.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(app.Path);

                p.Start();
                app.LastRestartTime = DateTime.Now;
                app.RestartCount++;

                // 重置所有錯誤狀態
                app.HangStartTime = null;
                app.VerificationStartTime = null; // 重啟成功後，重置雙重確認狀態

                Logger.Log($"Application restarted: {app.ProcessName}", "SUCCESS");

                if (app.ForceToTop)
                {
                    Thread.Sleep(2000);
                    p.Refresh();
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        NativeMethods.SetWindowPos(p.MainWindowHandle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to restart {app.ProcessName}: {ex.Message}", "ERROR");
            }
        }
    }
}