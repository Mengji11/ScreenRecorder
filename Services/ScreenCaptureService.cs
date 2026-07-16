using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenRecorder.Helpers;

using Rectangle = System.Drawing.Rectangle;

namespace ScreenRecorder.Services
{
    public class ScreenCaptureService
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        private const int HORZRES = 8;
        private const int VERTRES = 10;
        private const int CURSOR_SHOWING = 0x00000001;
        private const uint DI_NORMAL = 0x0003;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public Bitmap CaptureFullScreen()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int screenWidth = GetDeviceCaps(hdc, HORZRES);
            int screenHeight = GetDeviceCaps(hdc, VERTRES);
            ReleaseDC(IntPtr.Zero, hdc);

            var bitmap = new Bitmap(screenWidth, screenHeight);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));

            // Draw cursor on the captured frame
            DrawCursor(graphics);

            return bitmap;
        }

        private void DrawCursor(Graphics graphics)
        {
            CURSORINFO ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

            if (GetCursorInfo(out ci) && ci.flags == CURSOR_SHOWING)
            {
                IntPtr hdc = graphics.GetHdc();
                DrawIconEx(hdc, ci.ptScreenPos.X, ci.ptScreenPos.Y, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                graphics.ReleaseHdc(hdc);
            }
        }

        public Bitmap CaptureMonitor(int monitorIndex)
        {
            var monitors = MonitorHelper.GetAllMonitors();
            if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            {
                return CaptureFullScreen();
            }

            var monitor = monitors[monitorIndex];
            var bitmap = new Bitmap(monitor.Bounds.Width, monitor.Bounds.Height);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(monitor.Bounds.X, monitor.Bounds.Y, 0, 0, bitmap.Size);

            // Draw cursor adjusted to monitor offset
            CURSORINFO ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (GetCursorInfo(out ci) && ci.flags == CURSOR_SHOWING)
            {
                int x = ci.ptScreenPos.X - monitor.Bounds.X;
                int y = ci.ptScreenPos.Y - monitor.Bounds.Y;
                IntPtr hdc = graphics.GetHdc();
                DrawIconEx(hdc, x, y, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                graphics.ReleaseHdc(hdc);
            }

            return bitmap;
        }

        public Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            var bitmap = new Bitmap(width, height);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));

            // Draw cursor adjusted to region offset
            CURSORINFO ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (GetCursorInfo(out ci) && ci.flags == CURSOR_SHOWING)
            {
                int cx = ci.ptScreenPos.X - x;
                int cy = ci.ptScreenPos.Y - y;
                IntPtr hdc = graphics.GetHdc();
                DrawIconEx(hdc, cx, cy, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                graphics.ReleaseHdc(hdc);
            }

            return bitmap;
        }

        public Bitmap CaptureWindow(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
            return bitmap;
        }

        public IntPtr GetWindowHandleAtPoint(System.Drawing.Point point)
        {
            return WindowFromPoint(new POINT { X = point.X, Y = point.Y });
        }

        public Rect GetWindowRectangle(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out RECT rect);
            return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        public List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    GetWindowRect(hWnd, out RECT rect);
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 100 && height > 100)
                    {
                        int titleLength = GetWindowTextLength(hWnd);
                        if (titleLength > 0)
                        {
                            var sb = new System.Text.StringBuilder(titleLength + 1);
                            GetWindowText(hWnd, sb, sb.Capacity);
                            windows.Add(new WindowInfo
                            {
                                Handle = hWnd,
                                Title = sb.ToString(),
                                Rectangle = new Rect(rect.Left, rect.Top, width, height)
                            });
                        }
                    }
                }
                return true;
            }, 0);
            return windows;
        }

        public static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, int lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);
    }

    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public Rect Rectangle { get; set; }
        public double Width => Rectangle.Width;
        public double Height => Rectangle.Height;
    }
}
