using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScreenRecorder.Services;

namespace ScreenRecorder.Views
{
    public partial class RecordingControlBar : Window
    {
        private readonly RecorderService _recorder;
        private System.Windows.Threading.DispatcherTimer? _blinkTimer;
        private bool _blinkState;

        public event EventHandler? StopClicked;
        public event EventHandler? PauseClicked;
        public event EventHandler? ScreenshotClicked;

        public RecordingControlBar(RecorderService recorder)
        {
            InitializeComponent();
            _recorder = recorder;

            _recorder.DurationChanged += Recorder_DurationChanged;
            _recorder.StateChanged += Recorder_StateChanged;

            StartBlinking();
        }

        private void StartBlinking()
        {
            _blinkTimer = new System.Windows.Threading.DispatcherTimer();
            _blinkTimer.Interval = TimeSpan.FromMilliseconds(500);
            _blinkTimer.Tick += (s, e) =>
            {
                _blinkState = !_blinkState;
                recIndicator.Fill = _blinkState
                    ? new SolidColorBrush(Color.FromRgb(255, 77, 79))
                    : new SolidColorBrush(Color.FromRgb(82, 196, 26));
            };
            _blinkTimer.Start();
        }

        private void StopBlinking()
        {
            _blinkTimer?.Stop();
            _blinkTimer = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopBlinking();
            _recorder.DurationChanged -= Recorder_DurationChanged;
            _recorder.StateChanged -= Recorder_StateChanged;
            base.OnClosed(e);
        }

        private void Recorder_DurationChanged(object? sender, TimeSpan duration)
        {
            Dispatcher.Invoke(() =>
            {
                txtTime.Text = duration.ToString(@"hh\:mm\:ss");
            });
        }

        private void Recorder_StateChanged(object? sender, Models.RecordingState state)
        {
            Dispatcher.Invoke(() =>
            {
                switch (state)
                {
                    case Models.RecordingState.Recording:
                        btnPause.Content = "⏸";
                        recIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        break;
                    case Models.RecordingState.Paused:
                        btnPause.Content = "▶";
                        recIndicator.Fill = new SolidColorBrush(Color.FromRgb(250, 173, 20));
                        break;
                    case Models.RecordingState.Idle:
                        recIndicator.Fill = new SolidColorBrush(Color.FromRgb(82, 196, 26));
                        break;
                }
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            PauseClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            ScreenshotClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
