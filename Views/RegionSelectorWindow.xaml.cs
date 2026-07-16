using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenRecorder.Views
{
    public partial class RegionSelectorWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging;

        public Rect SelectedRegion { get; private set; }
        public bool IsConfirmed { get; private set; }

        public RegionSelectorWindow()
        {
            InitializeComponent();

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsConfirmed = false;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isDragging = true;

            selectionRect.Visibility = Visibility.Visible;
            sizeLabel.Visibility = Visibility.Visible;

            Canvas.SetLeft(selectionRect, _startPoint.X);
            Canvas.SetTop(selectionRect, _startPoint.Y);
            selectionRect.Width = 0;
            selectionRect.Height = 0;

            CaptureMouse();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(this);

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(selectionRect, x);
            Canvas.SetTop(selectionRect, y);
            selectionRect.Width = width;
            selectionRect.Height = height;

            txtSize.Text = $"{(int)width} x {(int)height}";

            double labelX = x;
            double labelY = y - 30;
            if (labelY < 0) labelY = y + height + 5;

            Canvas.SetLeft(sizeLabel, labelX);
            Canvas.SetTop(sizeLabel, labelY);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();

            var endPoint = e.GetPosition(this);

            double x = Math.Min(_startPoint.X, endPoint.X);
            double y = Math.Min(_startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - _startPoint.X);
            double height = Math.Abs(endPoint.Y - _startPoint.Y);

            if (width < 10 || height < 10)
            {
                IsConfirmed = false;
                Close();
                return;
            }

            SelectedRegion = new Rect(x, y, width, height);
            IsConfirmed = true;
            Close();
        }
    }
}
