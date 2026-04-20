using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MonitAndRestart
{
    public partial class MainForm : Form
    {
        // 改用 DataGridView
        private DataGridView gridApps;

        private Timer monitorTimer;
        private NotifyIcon trayIcon;
        private ConfigManager configManager;
        private MonitorEngine monitorEngine;
        private List<AppConfig> appList;
        private GlobalSettings globalSettings;

        // Grace Period State
        private DateTime appStartTime;
        private bool isGracePeriodOver = false;

        public MainForm()
        {
            InitializeCustomUI();

            configManager = new ConfigManager();
            monitorEngine = new MonitorEngine();
            appStartTime = DateTime.Now;

            LoadSettings();
            ApplyLoggerSettings();

            Logger.Log("Application Started");
            if (globalSettings.StartMinimized)
            {
                this.WindowState = FormWindowState.Minimized;
                if (globalSettings.MinimizeToTray) this.ShowInTaskbar = false;
            }

            if (globalSettings.CheckUpdates)
            {
                Task.Run(() => CheckForUpdates());
            }
        }

        private void InitializeCustomUI()
        {
            this.Text = "Restart on Crash - Monitor Tool";
            this.Size = new Size(1000, 500); //稍微加寬以容納按鈕

            var asm = Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream("MonitAndRestart.faviconx.ico");
            Icon favIcon = new Icon(stream);

            this.Icon = favIcon;

            // --- 1. Toolbar ---
            ToolStrip ts = new ToolStrip();
            ts.ImageScalingSize = new Size(24, 24);

            // 只需要 Add，Remove 跟 Edit 已經移到 Grid 裡面了
            ts.Items.Add(new ToolStripButton("新增監控程式", null, (s, e) => AddApp()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            ts.Items.Add(new ToolStripSeparator());

            ToolStripButton btnSettings = new ToolStripButton("Settings", SystemIcons.Shield.ToBitmap(), (s, e) => OpenSettings());
            btnSettings.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            ts.Items.Add(btnSettings);
            ts.Items.Add(new ToolStripSeparator());

            ToolStripButton btnToggle = new ToolStripButton("停止監控");
            btnToggle.Click += (s, e) => ToggleMonitor(btnToggle);
            ts.Items.Add(btnToggle);

            this.Controls.Add(ts);

            // --- 2. DataGridView (取代 ListView) ---
            gridApps = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,       // 禁止使用者手動新增空行
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,        // 隱藏最左邊的箭頭區
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // 定義欄位 (順序：Modify, Remove, Info..., Checkbox)

            // Col 0: Modify Button
            var btnModify = new DataGridViewButtonColumn
            {
                HeaderText = "Edit",
                Text = "Modify",
                UseColumnTextForButtonValue = true, // 按鈕上顯示 "Modify" 字樣
                Name = "colModify",
                Width = 70,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            gridApps.Columns.Add(btnModify);

            // Col 1: Remove Button
            var btnRemove = new DataGridViewButtonColumn
            {
                HeaderText = "Del",
                Text = "Remove",
                UseColumnTextForButtonValue = true,
                Name = "colRemove",
                Width = 70,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            gridApps.Columns.Add(btnRemove);

            // Col 2: Process Name
            gridApps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Process Name", DataPropertyName = "ProcessName", ReadOnly = true });

            // Col 3: Status
            gridApps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Name = "colStatus", ReadOnly = true });

            // Col 4: PID
            gridApps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", Name = "colPid", Width = 60, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });

            // Col 5: Restarts
            gridApps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Restarts", Name = "colRestarts", Width = 70, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });

            // Col 6: Path
            gridApps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Path", DataPropertyName = "Path", ReadOnly = true });

            // Col 7: Monitor Checkbox (放在最後面)
            var chkMonitor = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Monitor",
                Name = "colMonitor",
                Width = 60,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            gridApps.Columns.Add(chkMonitor);

            // 綁定點擊事件
            gridApps.CellContentClick += GridApps_CellContentClick;

            this.Controls.Add(gridApps);
            this.Controls.SetChildIndex(gridApps, 0);

            // --- 3. Timer ---
            monitorTimer = new Timer { Interval = 1000 };
            monitorTimer.Tick += MonitorTimer_Tick;
            monitorTimer.Start();

            // --- 4. Tray Icon ---
            trayIcon = new NotifyIcon
            {
                Icon = favIcon,
                Text = "Restart On Crash",
                Visible = true
            };
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("Show", (s, e) => ShowForm());
            cm.MenuItems.Add("Exit", (s, e) => {
                trayIcon.Visible = false;
                Application.Exit();
            });
            trayIcon.ContextMenu = cm;
            trayIcon.DoubleClick += (s, e) => ShowForm();

            // Form Events
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
        }

        private void LoadSettings()
        {
            globalSettings = configManager.LoadGlobalSettings();
            appList = configManager.LoadApps();
            StartupManager.SetStartup(globalSettings.RunOnStartup);

            // 初始載入 Grid 資料
            RefreshGrid(true);
        }

        private void ApplyLoggerSettings()
        {
            Logger.Enabled = globalSettings.LogToFile;
            Logger.CustomLogPath = globalSettings.LogPath;
        }

        private void OpenSettings()
        {
            SettingsForm frm = new SettingsForm(globalSettings);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                globalSettings = frm.Settings;
                configManager.SaveGlobalSettings(globalSettings);
                StartupManager.SetStartup(globalSettings.RunOnStartup);
                ApplyLoggerSettings();
            }
        }

        // --- Grid Event Handling (按鈕與 Checkbox 邏輯) ---
        private void GridApps_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return; // 點擊標題列忽略

            // 取得對應的 AppConfig 物件 (我們將物件存在 Row 的 Tag 屬性中，或者透過 index 對應)
            // 這裡直接使用 Index 對應，因為我們每次都重建 Row 或同步 List
            if (e.RowIndex >= appList.Count) return;
            var app = appList[e.RowIndex];

            // 1. 處理 Modify 按鈕 (Col Index 0)
            if (e.ColumnIndex == 0)
            {
                // 暫停監控避免衝突
                monitorTimer.Stop();
                AddEditForm frm = new AddEditForm(app);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    appList[e.RowIndex] = frm.Config;
                    SaveAll();
                    RefreshGrid(true); // 強制重繪
                }
                monitorTimer.Start();
            }
            // 2. 處理 Remove 按鈕 (Col Index 1)
            else if (e.ColumnIndex == 1)
            {
                if (MessageBox.Show($"Are you sure you want to remove monitor for:\n{app.ProcessName}?",
                    "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    monitorTimer.Stop(); // 暫停監控，避免 List 在監控中被修改
                    appList.RemoveAt(e.RowIndex);
                    SaveAll();
                    RefreshGrid(true); // 移除後需重建 Grid Row
                    monitorTimer.Start();
                }
            }
            // 3. 處理 Monitor Checkbox (Col Index 7 - 最後一欄)
            else if (gridApps.Columns[e.ColumnIndex].Name == "colMonitor")
            {
                // DataGridView 的 Checkbox 需要先 Commit 編輯狀態才能取到最新值
                gridApps.CommitEdit(DataGridViewDataErrorContexts.Commit);

                bool isChecked = (bool)gridApps.Rows[e.RowIndex].Cells["colMonitor"].Value;

                // 更新邏輯
                if (app.Enable != isChecked)
                {
                    app.Enable = isChecked;
                    // 如果被 Uncheck，重置狀態文字
                    if (!app.Enable)
                    {
                        app.Status = "Disabled";
                        app.Pid = 0;
                        app.HangStartTime = null;
                    }
                    else
                    {
                        app.Status = "Initializing...";
                        // 重新啟用時，重置重啟次數計數器 ---
                        app.RestartCount = 0;
                    }

                    SaveAll(); // 立即存檔
                    Logger.Log($"Monitor status changed for {app.ProcessName}: {(app.Enable ? "Enabled" : "Disabled")}");
                    RefreshGrid(false); // 僅更新 UI 顏色文字
                }
            }
        }

        // --- Core Logic ---

        private async void MonitorTimer_Tick(object sender, EventArgs e)
        {
            monitorTimer.Stop();

            string version = System.Windows.Forms.Application.ProductVersion;

            if (!isGracePeriodOver)
            {
                double secondsPassed = (DateTime.Now - appStartTime).TotalSeconds;
                if (secondsPassed < globalSettings.GracePeriod)
                {
                    this.Text = $"MonitAndRestart v{version} - 監控倒數: {(int)(globalSettings.GracePeriod - secondsPassed)}s";
                    // Grace Period 期間也更新 Grid 顯示狀態
                    RefreshGrid(false);
                    monitorTimer.Start();
                    return;
                }
                else
                {
                    isGracePeriodOver = true;
                    this.Text = $"MonitAndRestart v{version} - 持續監控中";
                }
            }

            await Task.Run(() => monitorEngine.CheckApps(appList));

            RefreshGrid(false); // 一般更新，不重建 Rows
            monitorTimer.Start();
        }

        private void CheckForUpdates()
        {
            System.Threading.Thread.Sleep(2000);
        }

        /// <summary>
        /// 更新 Grid 資料
        /// </summary>
        /// <param name="rebuildRows">是否需要刪除所有行重新建立 (例如新增/刪除/排序變更時)</param>
        private void RefreshGrid(bool rebuildRows)
        {
            // 避免在背景執行緒存取 UI
            if (gridApps.InvokeRequired)
            {
                gridApps.Invoke(new Action(() => RefreshGrid(rebuildRows)));
                return;
            }

            // 若數量不一致，強制重建
            if (gridApps.Rows.Count != appList.Count) rebuildRows = true;

            if (rebuildRows)
            {
                gridApps.Rows.Clear();
                foreach (var app in appList)
                {
                    int idx = gridApps.Rows.Add();
                    UpdateRowData(gridApps.Rows[idx], app);
                }
            }
            else
            {
                // 僅更新現有行數值的資料 (避免閃爍)
                for (int i = 0; i < appList.Count; i++)
                {
                    UpdateRowData(gridApps.Rows[i], appList[i]);
                }
            }
        }

        private void UpdateRowData(DataGridViewRow row, AppConfig app)
        {
            // 按鈕欄位不需要更新資料
            // Text 欄位更新
            row.Cells[2].Value = app.ProcessName;
            row.Cells["colStatus"].Value = app.Status;
            row.Cells["colPid"].Value = app.Pid == 0 ? "" : app.Pid.ToString();
            row.Cells["colRestarts"].Value = app.RestartCount;
            row.Cells[6].Value = app.Path;

            // Checkbox 欄位更新
            row.Cells["colMonitor"].Value = app.Enable;

            // 顏色處理
            var statusCell = row.Cells["colStatus"];
            if (!app.Enable)
            {
                statusCell.Style.ForeColor = Color.Gray;
                statusCell.Style.SelectionForeColor = Color.Gray;
            }
            else if (app.Status.Contains("Running"))
            {
                statusCell.Style.ForeColor = Color.Green;
                statusCell.Style.SelectionForeColor = Color.LightGreen;
            }
            else if (app.Status.Contains("Hung") || app.Status.Contains("Crashed") || app.Status.Contains("Killing"))
            {
                statusCell.Style.ForeColor = Color.Red;
                statusCell.Style.SelectionForeColor = Color.Salmon;
            }
            else
            {
                statusCell.Style.ForeColor = Color.Black;
                statusCell.Style.SelectionForeColor = Color.White;
            }
        }

        private void AddApp()
        {
            AddEditForm frm = new AddEditForm();
            if (frm.ShowDialog() == DialogResult.OK)
            {
                appList.Add(frm.Config);
                SaveAll();
                RefreshGrid(true); // 新增需重建 Row
            }
        }

        private void SaveAll()
        {
            configManager.SaveGlobalSettings(globalSettings);
            configManager.SaveApps(appList);
        }

        private void ToggleMonitor(ToolStripButton btn)
        {
            if (monitorTimer.Enabled)
            {
                monitorTimer.Stop();
                btn.Text = "開始監控";
                this.Text = "崩潰後重啟 - 暫停監控中";
                Logger.Log("Monitor Paused by User");
            }
            else
            {
                monitorTimer.Start();
                btn.Text = "停止監控";
                this.Text = "崩潰後重啟 - 監控活動中";
                Logger.Log("Monitor Resumed by User");
            }
        }

        // --- Window Behavior ---
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && globalSettings.MinimizeToTray)
            {
                Hide();
                trayIcon.Visible = true;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && globalSettings.MinimizeToTray)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                Hide();
            }
            else
            {
                Logger.Log("Application Exiting");
                trayIcon.Visible = false;
            }
        }

        private void ShowForm()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }
    }
}