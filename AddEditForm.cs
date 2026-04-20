using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MonitAndRestart
{
    public partial class AddEditForm : Form
    {
        public AppConfig Config { get; private set; }

        private TextBox txtPath, txtName, txtArgs;
        private TextBox txtDelay, txtInterval, txtHangTimeout, txtMaxRestarts;
        private CheckBox chkForceTop, chkEnable, chkDoubleCheck;
        private TextBox txtDoubleCheckSec;

        private CheckBox[] chkDays = new CheckBox[7];
        private DateTimePicker dtpStart1, dtpStop1, dtpStart2, dtpStop2;
        private Button btnBrowse, btnSave, btnCancel;

        public AddEditForm(AppConfig existing = null)
        {
            InitializeComponent();
            InitializeCustomControls();

            if (existing != null) { Config = existing; LoadConfigToUI(); }
            else { Config = new AppConfig(); }
        }

        private void InitializeCustomControls()
        {
            this.Size = new Size(480, 650);
            this.Text = "Application Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;

            // 1. Target Application Group
            GroupBox grpTarget = new GroupBox { Text = "應用軟體", Left = 20, Top = y, Width = 410, Height = 180 };

            Label lblPath = new Label { Text = "程式路徑:", Left = 15, Top = 25, Width = 380 };
            txtPath = new TextBox { Left = 15, Top = 50, Width = 330 };
            btnBrowse = new Button { Text = "...", Left = 350, Top = 48, Width = 40 };
            btnBrowse.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "Executables|*.exe|All Files|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    txtName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                }
            };

            // Arguments 可監控 Java 程式（需執行 java -jar app.jar）、Python 腳本、或帶有啟動參數的伺服器程式。
            Label lblArgs = new Label { Text = "Arguments (Optional):", Left = 15, Top = 80, Width = 380 };
            txtArgs = new TextBox { Left = 15, Top = 100, Width = 375 }; // e.g. -server -config config.ini

            Label lblName = new Label { Text = "Process Name (no .exe):", Left = 15, Top = 130, Width = 380 };
            txtName = new TextBox { Left = 15, Top = 150, Width = 375 };

            grpTarget.Controls.AddRange(new Control[] { lblPath, txtPath, btnBrowse, lblArgs, txtArgs, lblName, txtName });
            this.Controls.Add(grpTarget);

            y += 190;
            // 2. Monitoring & Crash Logic Group
            GroupBox grpLogic = new GroupBox { Text = "異常檢測監控", Left = 20, Top = y, Width = 410, Height = 140 };

            // Row 1: Hang Detection
            Label lblHang = new Label { Text = "Hang Timeout (s):", Left = 15, Top = 30, Width = 110 };
            txtHangTimeout = new TextBox { Text = "10", Left = 130, Top = 27, Width = 50 };
            Label lblHangHint = new Label { Text = "('Hung'狀態暫且等待時間)", Left = 190, Top = 30, AutoSize = true, ForeColor = Color.Gray };

            // --- 新增 Max Restarts ---
            Label lblMaxRes = new Label { Text = "Max Restarts:", Left = 200, Top = 30, Width = 80 };
            txtMaxRestarts = new TextBox { Text = "0", Left = 285, Top = 27, Width = 40 };
            Label lblMaxHint = new Label { Text = "(0 = Infinite)", Left = 330, Top = 30, AutoSize = true, ForeColor = Color.Gray };

            // Row 2: Double Check (New Feature)
            chkDoubleCheck = new CheckBox { Text = "Double Check before restart", Left = 15, Top = 65, Width = 200, AutoSize = true };
            chkDoubleCheck.CheckedChanged += (s, e) => txtDoubleCheckSec.Enabled = chkDoubleCheck.Checked;

            Label lblWait = new Label { Text = "Wait", Left = 220, Top = 69, AutoSize = true };
            txtDoubleCheckSec = new TextBox { Text = "5", Left = 255, Top = 66, Width = 40, Enabled = false };
            Label lblSecCheck = new Label { Text = "sec and verify again", Left = 300, Top = 69, AutoSize = true };

            // Row 3: Restart settings
            Label lblDelay = new Label { Text = "重啟延遲 (s):", Left = 15, Top = 105, Width = 110 };
            txtDelay = new TextBox { Text = "5", Left = 130, Top = 102, Width = 50 };

            Label lblInterval = new Label { Text = "最短間隔 (s):", Left = 220, Top = 105, Width = 100 };
            txtInterval = new TextBox { Text = "10", Left = 320, Top = 102, Width = 40 };

            grpLogic.Controls.AddRange(new Control[] { lblHang, txtHangTimeout, lblMaxRes, txtMaxRestarts, lblMaxHint, chkDoubleCheck, lblWait, txtDoubleCheckSec, lblSecCheck, lblDelay, txtDelay, lblInterval, txtInterval });
            this.Controls.Add(grpLogic);

            y += 150;
            // 3. Schedule Group (Same as before, simplified for brevity in this response)
            GroupBox grpSchedule = new GroupBox { Text = "Schedule", Left = 20, Top = y, Width = 410, Height = 130 };
            // ... (Schedule controls code from previous step goes here) ...
            // Re-implementing Schedule controls briefly to keep it compilable:
            string[] dayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            int dayX = 15;
            for (int i = 0; i < 7; i++)
            {
                chkDays[i] = new CheckBox { Text = dayNames[i], Left = dayX, Top = 25, Width = 50, Checked = true };
                grpSchedule.Controls.Add(chkDays[i]); dayX += 52;
            }

            // --- 修改排程版面為兩段區間 ---
            Label lblWin1 = new Label { Text = "Window 1:", Left = 15, Top = 60, AutoSize = true };
            dtpStart1 = CreateTimePicker(80, 57);
            Label lblTo1 = new Label { Text = "to", Left = 175, Top = 60, AutoSize = true };
            dtpStop1 = CreateTimePicker(200, 57);

            Label lblWin2 = new Label { Text = "Window 2:", Left = 15, Top = 90, AutoSize = true };
            dtpStart2 = CreateTimePicker(80, 87);
            Label lblTo2 = new Label { Text = "to", Left = 175, Top = 90, AutoSize = true };
            dtpStop2 = CreateTimePicker(200, 87);

            grpSchedule.Controls.AddRange(new Control[] { lblWin1, dtpStart1, lblTo1, dtpStop1, lblWin2, dtpStart2, lblTo2, dtpStop2 });
            this.Controls.Add(grpSchedule);

            y += 140;
            // Bottom Controls
            chkForceTop = new CheckBox { Text = "Force To Top", Left = 20, Top = y, Checked = false, Width = 150 };
            chkEnable = new CheckBox { Text = "Enable Monitor", Left = 200, Top = y, Checked = true, Width = 150 };

            y += 40;
            btnSave = new Button { Text = "Save", Left = 100, Top = y, DialogResult = DialogResult.OK, Height = 40, Width = 100 };
            btnSave.Click += BtnSave_Click;
            btnCancel = new Button { Text = "Cancel", Left = 220, Top = y, DialogResult = DialogResult.Cancel, Height = 40, Width = 100 };

            this.Controls.AddRange(new Control[] { chkForceTop, chkEnable, btnSave, btnCancel });
        }

        // Helper for TimePicker
        private DateTimePicker CreateTimePicker(int x, int y) { return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, ShowCheckBox = true, Checked = false, Left = x, Top = y, Width = 90 }; }
        private void SetTimePicker(DateTimePicker dtp, TimeSpan? ts) { if (ts.HasValue) { dtp.Checked = true; dtp.Value = DateTime.Today.Add(ts.Value); } else dtp.Checked = false; }
        private TimeSpan? GetTimeFromPicker(DateTimePicker dtp) { if (dtp.Checked) return dtp.Value.TimeOfDay; return null; }

        private void LoadConfigToUI()
        {
            txtPath.Text = Config.Path;
            txtName.Text = Config.ProcessName;
            txtArgs.Text = Config.Arguments; // Load Args

            txtDelay.Text = Config.RestartDelay.ToString();
            txtInterval.Text = Config.RestartInterval.ToString();
            txtHangTimeout.Text = Config.HangTimeout.ToString();
            txtMaxRestarts.Text = Config.MaxRestarts.ToString(); // 載入 Max Restarts

            chkDoubleCheck.Checked = Config.DoubleCheckEnabled;
            txtDoubleCheckSec.Text = Config.DoubleCheckSeconds.ToString();
            txtDoubleCheckSec.Enabled = Config.DoubleCheckEnabled;

            chkForceTop.Checked = Config.ForceToTop;
            chkEnable.Checked = Config.Enable;

            // Schedule Load
            for (int i = 0; i < 7; i++) if (i < Config.ScheduleDays.Length) chkDays[i].Checked = Config.ScheduleDays[i];

            // 載入區間
            SetTimePicker(dtpStart1, Config.ScheduleStart1);
            SetTimePicker(dtpStop1, Config.ScheduleStop1);
            SetTimePicker(dtpStart2, Config.ScheduleStart2);
            SetTimePicker(dtpStop2, Config.ScheduleStop2);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPath.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Path and Process Name are required.");
                this.DialogResult = DialogResult.None;
                return;
            }

            Config.Path = txtPath.Text;
            Config.ProcessName = txtName.Text;
            Config.Arguments = txtArgs.Text; // Save Args

            int.TryParse(txtDelay.Text, out int delay); Config.RestartDelay = delay;
            int.TryParse(txtInterval.Text, out int interval); Config.RestartInterval = interval;
            int.TryParse(txtHangTimeout.Text, out int hang); Config.HangTimeout = hang;
            int.TryParse(txtMaxRestarts.Text, out int maxRes); Config.MaxRestarts = maxRes; // 儲存 Max Restarts

            Config.DoubleCheckEnabled = chkDoubleCheck.Checked;
            int.TryParse(txtDoubleCheckSec.Text, out int dCheck); Config.DoubleCheckSeconds = dCheck;

            Config.ForceToTop = chkForceTop.Checked;
            Config.Enable = chkEnable.Checked;

            // Schedule Save
            Config.ScheduleDays = new bool[7];
            for (int i = 0; i < 7; i++) Config.ScheduleDays[i] = chkDays[i].Checked;

            // 儲存區間
            Config.ScheduleStart1 = GetTimeFromPicker(dtpStart1);
            Config.ScheduleStop1 = GetTimeFromPicker(dtpStop1);
            Config.ScheduleStart2 = GetTimeFromPicker(dtpStart2);
            Config.ScheduleStop2 = GetTimeFromPicker(dtpStop2);
        }
    }
}