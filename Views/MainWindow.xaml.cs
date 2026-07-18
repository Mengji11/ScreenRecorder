using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScreenRecorder.Helpers;
using ScreenRecorder.Models;
using ScreenRecorder.Services;

namespace ScreenRecorder.Views
{
    public partial class MainWindow : Window
    {
        private readonly RecorderService _recorder;
        private readonly HotkeyService _hotkey;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private RecordingSettings _settings;
        private IntPtr _windowHandle;
        private bool _isRecording;
        private RecordingControlBar? _controlBar;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            _recorder = new RecorderService();
            _hotkey = new HotkeyService();
            _settingsService = new SettingsService();
            _historyService = new HistoryService();
            _settings = _settingsService.LoadSettings();

            _recorder.StateChanged += Recorder_StateChanged;
            _recorder.DurationChanged += Recorder_DurationChanged;
            _recorder.RecordingCompleted += Recorder_RecordingCompleted;
            _recorder.ErrorOccurred += Recorder_ErrorOccurred;
            _recorder.SavingProgress += Recorder_SavingProgress;

            Loaded += MainWindow_Loaded;
            Closing += Window_Closing;

            InitNotifyIcon();
        }

        private void InitNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "录屏助手";
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Visible = false;

            _notifyIcon.DoubleClick += (s, e) => ShowFromTray();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("显示主窗口", null, (s, e) => ShowFromTray());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("开始录制", null, (s, e) => Dispatcher.Invoke(StartRecording));
            contextMenu.Items.Add("停止录制", null, (s, e) => Dispatcher.Invoke(StopRecording));
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("退出", null, (s, e) => Dispatcher.Invoke(() => Close()));

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            _notifyIcon!.Visible = false;
            Activate();
        }

        private void MinimizeToTray()
        {
            Hide();
            ShowInTaskbar = false;
            _notifyIcon!.Visible = true;
            _notifyIcon.ShowBalloonTip(1000, "录屏助手", "程序已最小化到系统托盘", System.Windows.Forms.ToolTipIcon.Info);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            _hotkey.Register(_windowHandle);

            RegisterHotkeys();

            if (string.IsNullOrEmpty(_settings.SavePath))
            {
                _settings.SavePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "ScreenRecorder");
            }

            LoadMonitors();
            LoadSettingsToUI();
            CheckFfmpegAvailability();
        }

        private void CheckFfmpegAvailability()
        {
            if (!FfmpegHelper.IsFfmpegAvailable())
            {
                var result = MessageBox.Show(
                    "未检测到 FFmpeg，录制功能需要 FFmpeg 支持。\n\n" +
                    "安装方法（任选一种）：\n" +
                    "1. 运行 download-ffmpeg.ps1 脚本自动下载\n" +
                    "2. 运行 download-ffmpeg.bat 查看手动安装指引\n" +
                    "3. 从 https://github.com/BtbN/FFmpeg-Builds/releases 下载\n\n" +
                    "是否打开下载页面？",
                    "FFmpeg 未安装",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://github.com/BtbN/FFmpeg-Builds/releases",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            else
            {
                string version = FfmpegHelper.GetFfmpegVersion();
                System.Diagnostics.Debug.WriteLine($"FFmpeg: {version}");
            }

            // Check audio devices
            bool hasSystem = AudioCaptureService.IsSystemAudioAvailable();
            bool hasMic = AudioCaptureService.IsMicrophoneAvailable();

            if (!hasSystem && !hasMic)
            {
                chkSystemAudio.IsEnabled = false;
                chkSystemAudio.IsChecked = false;
                chkMicrophone.IsEnabled = false;
                chkMicrophone.IsChecked = false;
                _settings.RecordSystemAudio = false;
                _settings.RecordMicrophone = false;
                _settingsService.SaveSettings(_settings);
            }
            else if (!hasSystem)
            {
                chkSystemAudio.IsEnabled = false;
                chkSystemAudio.IsChecked = false;
                _settings.RecordSystemAudio = false;
                _settingsService.SaveSettings(_settings);
            }
            else if (!hasMic)
            {
                chkMicrophone.IsEnabled = false;
                chkMicrophone.IsChecked = false;
                _settings.RecordMicrophone = false;
                _settingsService.SaveSettings(_settings);
            }
        }

        private void LoadMonitors()
        {
            var monitors = MonitorHelper.GetAllMonitors();
            cmbMonitor.Items.Clear();

            foreach (var monitor in monitors)
            {
                cmbMonitor.Items.Add(monitor.DisplayName);
            }

            if (cmbMonitor.Items.Count > 0)
            {
                cmbMonitor.SelectedIndex = Math.Min(_settings.MonitorIndex, cmbMonitor.Items.Count - 1);
            }
        }

        private void RegisterHotkeys()
        {
            _hotkey.UnregisterAll();

            _hotkey.RegisterHotkey(_settings.HotkeyStartStop, _settings.UseCtrlModifier, _settings.UseAltModifier, _settings.UseShiftModifier, ToggleRecording);
            _hotkey.RegisterHotkey(_settings.HotkeyPauseResume, _settings.UseCtrlModifier, _settings.UseAltModifier, _settings.UseShiftModifier, TogglePause);
            _hotkey.RegisterHotkey(_settings.HotkeyScreenshot, _settings.UseCtrlModifier, _settings.UseAltModifier, _settings.UseShiftModifier, TakeScreenshot);
        }

        private void ToggleRecording()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isRecording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            });
        }

        private void TogglePause()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isRecording)
                {
                    if (_recorder.State == RecordingState.Recording)
                    {
                        _recorder.PauseRecording();
                    }
                    else if (_recorder.State == RecordingState.Paused)
                    {
                        _recorder.ResumeRecording();
                    }
                }
            });
        }

        private void TakeScreenshot()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    string fileName = $"截图_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string savePath = Path.Combine(_settings.SavePath, fileName);

                    if (!Directory.Exists(_settings.SavePath))
                    {
                        Directory.CreateDirectory(_settings.SavePath);
                    }

                    _recorder.TakeScreenshot(savePath);
                    MessageBox.Show($"截图已保存到: {savePath}", "截图成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void StartRecording()
        {
            if (_isRecording || _recorder.IsSaving) return;

            // Save current UI state to settings before recording
            SaveSettingsFromUI();

            var mode = GetCurrentMode();
            Rect? region = null;
            IntPtr windowHandle = default;

            if (mode == RecordingMode.Region)
            {
                var selector = new RegionSelectorWindow();
                selector.ShowDialog();
                if (!selector.IsConfirmed) return;
                region = selector.SelectedRegion;
            }
            else if (mode == RecordingMode.Window)
            {
                var selector = new WindowSelectorWindow();
                selector.Owner = this;
                selector.ShowDialog();
                if (!selector.IsConfirmed) return;
                windowHandle = selector.SelectedWindowHandle;
            }

            string ext = string.IsNullOrEmpty(_settings.OutputFormat) ? "mp4" : _settings.OutputFormat;
            string fileName = $"录屏_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
            string outputPath = Path.Combine(_settings.SavePath, fileName);

            if (!Directory.Exists(_settings.SavePath))
            {
                Directory.CreateDirectory(_settings.SavePath);
            }

            _recorder.UpdateSettings(_settings);
            _recorder.StartRecording(mode, outputPath, region, windowHandle);

            ShowControlBar();
        }

        private void ShowControlBar()
        {
            _controlBar?.Close();
            _controlBar = new RecordingControlBar(_recorder);
            _controlBar.StopClicked += (s, e) => Dispatcher.Invoke(StopRecording);
            _controlBar.PauseClicked += (s, e) => Dispatcher.Invoke(TogglePause);
            _controlBar.ScreenshotClicked += (s, e) => Dispatcher.Invoke(TakeScreenshot);
            _controlBar.Left = (SystemParameters.PrimaryScreenWidth - _controlBar.Width) / 2;
            _controlBar.Top = 50;
            _controlBar.Show();
        }

        private void HideControlBar()
        {
            if (_controlBar != null)
            {
                _controlBar.Close();
                _controlBar = null;
            }
        }

        private void StopRecording()
        {
            if (!_isRecording) return;
            HideControlBar();
            _recorder.StopRecording();
        }

        private RecordingMode GetCurrentMode()
        {
            if (rbWindow.IsChecked == true) return RecordingMode.Window;
            if (rbRegion.IsChecked == true) return RecordingMode.Region;
            return RecordingMode.FullScreen;
        }

        private void RecordingMode_Changed(object sender, RoutedEventArgs e)
        {
            // 录制模式切换时的处理逻辑
        }

        private void Recorder_StateChanged(object? sender, RecordingState state)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isRecording = state != RecordingState.Idle;

                switch (state)
                {
                    case RecordingState.Idle:
                        txtStatus.Text = _recorder.IsSaving ? "保存中..." : "就绪";
                        txtStatus.Foreground = _recorder.IsSaving
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 160, 20))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 196, 26));
                        btnStart.Content = "▶ 开始录制";
                        EnableControls(!_recorder.IsSaving);
                        break;

                    case RecordingState.Recording:
                        txtStatus.Text = "录制中...";
                        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 77, 79));
                        btnStart.Content = "■ 停止录制";
                        EnableControls(false);
                        break;

                    case RecordingState.Paused:
                        txtStatus.Text = "已暂停";
                        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 173, 20));
                        break;
                }
            }));
        }

        private void Recorder_DurationChanged(object? sender, TimeSpan duration)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtDuration.Text = duration.ToString(@"hh\:mm\:ss");
            }));
        }

        private void Recorder_RecordingCompleted(object? sender, string filePath)
        {
            // Use BeginInvoke to avoid blocking the save thread and prevent exceptions during shutdown
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var historyItem = new RecordingHistoryItem
                    {
                        FileName = Path.GetFileName(filePath),
                        FilePath = filePath,
                        RecordingTime = DateTime.Now,
                        Duration = _recorder.CurrentDuration,
                        FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                        Width = _recorder.CaptureWidth > 0 ? _recorder.CaptureWidth : (int)SystemParameters.PrimaryScreenWidth,
                        Height = _recorder.CaptureHeight > 0 ? _recorder.CaptureHeight : (int)SystemParameters.PrimaryScreenHeight
                    };

                    _historyService.AddEntry(historyItem);
                    HideControlBar();

                    txtStatus.Text = $"录制完成: {historyItem.FileSizeString}";
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 196, 26));
                }
                catch { }
            }));
        }

        private void OpenFileLocation(string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件位置: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportGif(string videoPath)
        {
            try
            {
                string gifPath = Path.ChangeExtension(videoPath, ".gif");

                if (_recorder.ExportGif(videoPath, gifPath))
                {
                    MessageBox.Show($"GIF已导出到: {gifPath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"GIF导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Recorder_ErrorOccurred(object? sender, string error)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(error, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }

        private void Recorder_SavingProgress(object? sender, string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtStatus.Text = message;
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 160, 20));
            }));
        }

        private void EnableControls(bool enabled)
        {
            rbFullScreen.IsEnabled = enabled;
            rbWindow.IsEnabled = enabled;
            rbRegion.IsEnabled = enabled;
            chkSystemAudio.IsEnabled = enabled;
            chkMicrophone.IsEnabled = enabled;
            cmbFrameRate.IsEnabled = enabled;
        }

        private void LoadSettingsToUI()
        {
            chkSystemAudio.IsChecked = _settings.RecordSystemAudio;
            chkMicrophone.IsChecked = _settings.RecordMicrophone;
            chkMouseHighlight.IsChecked = _settings.ShowMouseClickHighlight;
            chkWatermark.IsChecked = _settings.EnableWatermark;
        }

        private void SaveSettingsFromUI()
        {
            _settings.RecordSystemAudio = chkSystemAudio.IsChecked == true;
            _settings.RecordMicrophone = chkMicrophone.IsChecked == true;
            _settings.ShowMouseClickHighlight = chkMouseHighlight.IsChecked == true;
            _settings.EnableWatermark = chkWatermark.IsChecked == true;
            _settings.MonitorIndex = cmbMonitor.SelectedIndex;

            if (cmbFrameRate.SelectedItem is System.Windows.Controls.ComboBoxItem fpsItem &&
                fpsItem.Content is string fpsText)
            {
                if (int.TryParse(fpsText.Replace("fps", ""), out int fps))
                {
                    _settings.FrameRate = fps;
                }
            }

            _settingsService.SaveSettings(_settings);
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            var settingsWindow = new SettingsWindow(_settings);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                _settings = settingsWindow.Settings;
                _settingsService.SaveSettings(_settings);
                RegisterHotkeys();
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new HistoryWindow();
            historyWindow.Owner = this;
            historyWindow.ShowDialog();
        }

        private void BtnScheduled_Click(object sender, RoutedEventArgs e)
        {
            var scheduledWindow = new ScheduledRecordingWindow();
            scheduledWindow.Owner = this;

            scheduledWindow.ScheduledStart += (s, time) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!_isRecording)
                    {
                        StartRecording();
                    }
                });
            };

            scheduledWindow.ScheduledEnd += (s, time) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isRecording)
                    {
                        StopRecording();
                    }
                });
            };

            scheduledWindow.ShowDialog();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTray();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isRecording)
            {
                var result = MessageBox.Show("录制正在进行中，确定要退出吗？", "确认退出",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _recorder.StopRecording();
            }

            // Give background save a brief moment to finish, then force quit
            if (_recorder.IsSaving)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (_recorder.IsSaving && sw.ElapsedMilliseconds < 3000)
                {
                    System.Threading.Thread.Sleep(100);
                    System.Windows.Application.Current.Dispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => { }));
                }
            }

            _notifyIcon?.Dispose();
            _hotkey?.Dispose();
            _recorder?.Dispose();
        }
    }
}
