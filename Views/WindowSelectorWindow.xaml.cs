using System.Windows;
using System.Windows.Input;
using ScreenRecorder.Services;

namespace ScreenRecorder.Views
{
    public partial class WindowSelectorWindow : Window
    {
        private readonly ScreenCaptureService _screenCapture;

        public IntPtr SelectedWindowHandle { get; private set; }
        public bool IsConfirmed { get; private set; }

        public WindowSelectorWindow()
        {
            InitializeComponent();

            _screenCapture = new ScreenCaptureService();
            LoadWindows();
        }

        private void LoadWindows()
        {
            var windows = _screenCapture.GetOpenWindows();
            lstWindows.ItemsSource = windows;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsConfirmed = false;
                Close();
            }
        }

        private void LstWindows_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadWindows();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void ConfirmSelection()
        {
            if (lstWindows.SelectedItem is WindowInfo windowInfo)
            {
                SelectedWindowHandle = windowInfo.Handle;
                IsConfirmed = true;
                Close();
            }
            else
            {
                MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
