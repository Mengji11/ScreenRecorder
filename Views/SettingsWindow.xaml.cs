using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using ScreenRecorder.Models;
using ScreenRecorder.Services;

namespace ScreenRecorder.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        public RecordingSettings Settings { get; private set; }

        public SettingsWindow(RecordingSettings currentSettings)
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            Settings = currentSettings;

            LoadSettings();
            LoadAudioDevices();
        }

        private void LoadSettings()
        {
            txtHotkeyStartStop.Text = FormatHotkey(Settings.HotkeyStartStop, Settings.UseCtrlModifier, Settings.UseAltModifier, Settings.UseShiftModifier);
            txtHotkeyPauseResume.Text = FormatHotkey(Settings.HotkeyPauseResume, Settings.UseCtrlModifier, Settings.UseAltModifier, Settings.UseShiftModifier);
            txtHotkeyScreenshot.Text = FormatHotkey(Settings.HotkeyScreenshot, Settings.UseCtrlModifier, Settings.UseAltModifier, Settings.UseShiftModifier);

            chkCtrl.IsChecked = Settings.UseCtrlModifier;
            chkAlt.IsChecked = Settings.UseAltModifier;
            chkShift.IsChecked = Settings.UseShiftModifier;

            chkRecordSystem.IsChecked = Settings.RecordSystemAudio;
            chkRecordMic.IsChecked = Settings.RecordMicrophone;
            chkEnableWatermark.IsChecked = Settings.EnableWatermark;
            txtWatermarkText.Text = Settings.WatermarkText;
            txtSavePath.Text = Settings.SavePath;

            // 加载水印位置
            foreach (System.Windows.Controls.ComboBoxItem item in cmbWatermarkPosition.Items)
            {
                if (item.Content.ToString() == Settings.WatermarkPosition)
                {
                    cmbWatermarkPosition.SelectedItem = item;
                    break;
                }
            }

            sliderOpacity.Value = Settings.WatermarkOpacity;

            if (Settings.MaxDurationMinutes > 0)
            {
                txtMaxDuration.Text = Settings.MaxDurationMinutes.ToString();
            }
        }

        private void LoadAudioDevices()
        {
            // 系统音频设备（播放设备 / Loopback）
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                cmbSystemDevice.Items.Clear();
                foreach (var device in devices)
                {
                    cmbSystemDevice.Items.Add(device.FriendlyName);
                }
                if (cmbSystemDevice.Items.Count > 0)
                    cmbSystemDevice.SelectedIndex = 0;
            }
            catch
            {
                cmbSystemDevice.Items.Add("默认系统音频");
                cmbSystemDevice.SelectedIndex = 0;
            }

            // 麦克风设备（录音设备）
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                cmbMicDevice.Items.Clear();
                foreach (var device in devices)
                {
                    cmbMicDevice.Items.Add(device.FriendlyName);
                }
                if (cmbMicDevice.Items.Count > 0)
                    cmbMicDevice.SelectedIndex = 0;
            }
            catch
            {
                cmbMicDevice.Items.Add("默认麦克风");
                cmbMicDevice.SelectedIndex = 0;
            }
        }

        private string FormatHotkey(Key key, bool ctrl, bool alt, bool shift)
        {
            var parts = new List<string>();
            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");
            parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            pnlHotkeys.Visibility = Visibility.Collapsed;
            pnlRecording.Visibility = Visibility.Collapsed;
            pnlAudio.Visibility = Visibility.Collapsed;
            pnlWatermark.Visibility = Visibility.Collapsed;
            pnlSavePath.Visibility = Visibility.Collapsed;

            if (navHotkeys.IsChecked == true) pnlHotkeys.Visibility = Visibility.Visible;
            else if (navRecording.IsChecked == true) pnlRecording.Visibility = Visibility.Visible;
            else if (navAudio.IsChecked == true) pnlAudio.Visibility = Visibility.Visible;
            else if (navWatermark.IsChecked == true) pnlWatermark.Visibility = Visibility.Visible;
            else if (navSavePath.IsChecked == true) pnlSavePath.Visibility = Visibility.Visible;
        }

        private void BtnResetHotkeys_Click(object sender, RoutedEventArgs e)
        {
            Settings.HotkeyStartStop = Key.F5;
            Settings.HotkeyPauseResume = Key.F7;
            Settings.HotkeyScreenshot = Key.F8;
            Settings.UseCtrlModifier = true;
            Settings.UseAltModifier = false;
            Settings.UseShiftModifier = false;

            txtHotkeyStartStop.Text = FormatHotkey(Key.F5, true, false, false);
            txtHotkeyPauseResume.Text = FormatHotkey(Key.F7, true, false, false);
            txtHotkeyScreenshot.Text = FormatHotkey(Key.F8, true, false, false);

            chkCtrl.IsChecked = true;
            chkAlt.IsChecked = false;
            chkShift.IsChecked = false;
        }

        private void BtnBrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择录制文件保存位置",
                SelectedPath = Settings.SavePath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtSavePath.Text = dialog.SelectedPath;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.UseCtrlModifier = chkCtrl.IsChecked == true;
            Settings.UseAltModifier = chkAlt.IsChecked == true;
            Settings.UseShiftModifier = chkShift.IsChecked == true;

            Settings.RecordSystemAudio = chkRecordSystem.IsChecked == true;
            Settings.RecordMicrophone = chkRecordMic.IsChecked == true;
            Settings.EnableWatermark = chkEnableWatermark.IsChecked == true;
            Settings.WatermarkText = txtWatermarkText.Text;
            Settings.SavePath = txtSavePath.Text;

            // 保存水印位置和透明度
            if (cmbWatermarkPosition.SelectedItem is System.Windows.Controls.ComboBoxItem positionItem)
            {
                Settings.WatermarkPosition = positionItem.Content.ToString() ?? "右下角";
            }
            Settings.WatermarkOpacity = (int)sliderOpacity.Value;

            if (int.TryParse(txtMaxDuration.Text, out int maxDuration))
            {
                Settings.MaxDurationMinutes = maxDuration;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
