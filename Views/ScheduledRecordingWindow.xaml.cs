using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ScreenRecorder.Views
{
    public partial class ScheduledRecordingWindow : Window
    {
        private DispatcherTimer? _countdownTimer;
        private DateTime? _scheduledStartTime;
        private DateTime? _scheduledEndTime;

        public event EventHandler<DateTime>? ScheduledStart;
        public event EventHandler<DateTime>? ScheduledEnd;

        public ScheduledRecordingWindow()
        {
            InitializeComponent();

            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;
            txtStartTime.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm:ss");
            txtEndTime.Text = DateTime.Now.AddMinutes(35).ToString("HH:mm:ss");
        }

        private void BtnStartTimer_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateTimes())
                return;

            _scheduledStartTime = GetDateTime(dpStartDate, txtStartTime);
            _scheduledEndTime = GetDateTime(dpEndDate, txtEndTime);

            if (_scheduledStartTime <= DateTime.Now)
            {
                MessageBox.Show("开始时间必须大于当前时间", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_scheduledEndTime <= _scheduledStartTime)
            {
                MessageBox.Show("结束时间必须大于开始时间", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StartCountdown();

            btnStartTimer.IsEnabled = false;
            btnCancelTimer.IsEnabled = true;

            txtStatus.Text = $"已设置 - {_scheduledStartTime:yyyy-MM-dd HH:mm:ss}";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 196, 26));
        }

        private void BtnCancelTimer_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();

            btnStartTimer.IsEnabled = true;
            btnCancelTimer.IsEnabled = false;

            txtStatus.Text = "已取消";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153));
            txtCountdown.Text = "";
        }

        private void StartCountdown()
        {
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
        }

        private void StopCountdown()
        {
            _countdownTimer?.Stop();
            _countdownTimer = null;
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (_scheduledStartTime == null)
            {
                StopCountdown();
                return;
            }

            var now = DateTime.Now;

            if (now >= _scheduledStartTime)
            {
                // 时间到，触发开始录制
                StopCountdown();
                ScheduledStart?.Invoke(this, _scheduledStartTime.Value);

                txtStatus.Text = "录制中...";
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 77, 79));
                txtCountdown.Text = "";

                // 如果设置了结束时间，启动结束倒计时
                if (_scheduledEndTime != null && _scheduledEndTime > now)
                {
                    _countdownTimer = new DispatcherTimer();
                    _countdownTimer.Interval = TimeSpan.FromSeconds(1);
                    _countdownTimer.Tick += EndCountdownTimer_Tick;
                    _countdownTimer.Start();
                }

                return;
            }

            var remaining = _scheduledStartTime.Value - now;
            txtCountdown.Text = $"距开始: {remaining:hh\\:mm\\:ss}";
        }

        private void EndCountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (_scheduledEndTime == null)
            {
                StopCountdown();
                return;
            }

            var now = DateTime.Now;

            if (now >= _scheduledEndTime)
            {
                // 结束时间到
                StopCountdown();
                ScheduledEnd?.Invoke(this, _scheduledEndTime.Value);

                txtStatus.Text = "已完成";
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(82, 196, 26));
                txtCountdown.Text = "";

                btnStartTimer.IsEnabled = true;
                btnCancelTimer.IsEnabled = false;

                return;
            }

            var remaining = _scheduledEndTime.Value - now;
            txtCountdown.Text = $"距结束: {remaining:hh\\:mm\\:ss}";
        }

        private bool ValidateTimes()
        {
            if (dpStartDate.SelectedDate == null)
            {
                MessageBox.Show("请选择开始日期", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("请选择结束日期", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeSpan.TryParse(txtStartTime.Text, out _))
            {
                MessageBox.Show("开始时间格式无效（应为 HH:mm:ss）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeSpan.TryParse(txtEndTime.Text, out _))
            {
                MessageBox.Show("结束时间格式无效（应为 HH:mm:ss）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private DateTime GetDateTime(DatePicker datePicker, System.Windows.Controls.TextBox timeTextBox)
        {
            var date = datePicker.SelectedDate ?? DateTime.Today;
            var time = TimeSpan.Parse(timeTextBox.Text);
            return date + time;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCountdown();
            base.OnClosed(e);
        }
    }
}
