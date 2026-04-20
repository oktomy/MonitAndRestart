using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitAndRestart
{
    public partial class SettingsForm : Form
    {
        public GlobalSettings Settings { get; private set; }

        private CheckBox chkStartMinimized;
        private CheckBox chkMinimizeToTray;
        private CheckBox chkLogToFile;
        private TextBox txtLogPath;
        private Button btnBrowseLog;
        private NumericUpDown numGracePeriod;
        private Button btnOk, btnCancel;

        public SettingsForm(GlobalSettings currentSettings)
        {
            // 複製一份設定，避免直接修改原始參考 (直到按下 OK)
            Settings = new GlobalSettings
            {
                RunOnStartup = currentSettings.RunOnStartup,
                StartMinimized = currentSettings.StartMinimized,
                MinimizeToTray = currentSettings.MinimizeToTray,
                CheckUpdates = currentSettings.CheckUpdates,
                LogToFile = currentSettings.LogToFile,
                LogPath = currentSettings.LogPath,
                GracePeriod = currentSettings.GracePeriod
            };

            InitializeCustomComponent();
            LoadValues();
        }

        private void InitializeCustomComponent()
        {
            this.Text = "Settings";
            this.Size = new Size(450, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            int x = 20;
            int y = 20;
            int spacing = 30;

            // 1. Checkboxes
            chkStartMinimized = CreateCheck("最小化啟動", x, y);
            y += spacing;

            chkMinimizeToTray = CreateCheck("關閉時最小化到系統托盤", x, y);
            y += spacing;

            //chkCheckUpdates = CreateCheck("Check for updates on startup", x, y);
            //y += spacing;

            chkLogToFile = CreateCheck("日誌記錄到文件", x, y);
            chkLogToFile.CheckedChanged += (s, e) => ToggleLogControls();
            y += spacing;

            // 2. Log Path Selection
            txtLogPath = new TextBox { Left = x + 20, Top = y, Width = 320, ReadOnly = true, BackColor = Color.WhiteSmoke };
            btnBrowseLog = new Button { Text = "...", Left = x + 350, Top = y - 2, Width = 40, Height = 25 };
            btnBrowseLog.Click += BtnBrowseLog_Click;

            this.Controls.Add(txtLogPath);
            this.Controls.Add(btnBrowseLog);
            y += 40;

            // 3. Grace Period
            Label lblGrace = new Label { Text = "啟動監控倒數 ", Left = x, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblGrace);
            y += 25;

            numGracePeriod = new NumericUpDown { Left = x, Top = y, Width = 60, Minimum = 0, Maximum = 999 };
            Label lblSeconds = new Label { Text = "seconds", Left = x + 70, Top = y + 2, AutoSize = true };
            this.Controls.Add(numGracePeriod);
            this.Controls.Add(lblSeconds);
            y += 60;

            // 4. Buttons (Bottom)
            btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, Height = 30, Left = 240, Top = y };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Height = 30, Left = 330, Top = y };

            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            // 初始 UI 狀態
            ToggleLogControls();
        }

        private CheckBox CreateCheck(string text, int x, int y)
        {
            var chk = new CheckBox { Text = text, Left = x, Top = y, AutoSize = true };
            this.Controls.Add(chk);
            return chk;
        }

        private void LoadValues()
        {
            chkStartMinimized.Checked = Settings.StartMinimized;
            chkMinimizeToTray.Checked = Settings.MinimizeToTray;
            chkLogToFile.Checked = Settings.LogToFile;
            txtLogPath.Text = Settings.LogPath;
            numGracePeriod.Value = Settings.GracePeriod;
        }

        private void ToggleLogControls()
        {
            bool active = chkLogToFile.Checked;
            txtLogPath.Enabled = active;
            btnBrowseLog.Enabled = active;
        }

        private void BtnBrowseLog_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Log Folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtLogPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // 將 UI 值寫回 Settings 物件
            Settings.StartMinimized = chkStartMinimized.Checked;
            Settings.MinimizeToTray = chkMinimizeToTray.Checked;
            Settings.LogToFile = chkLogToFile.Checked;
            Settings.LogPath = txtLogPath.Text;
            Settings.GracePeriod = (int)numGracePeriod.Value;
        }
    }
}