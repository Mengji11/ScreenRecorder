using System.Drawing;
using System.Runtime.InteropServices;

namespace ScreenRecorder.Helpers
{
    public class MonitorInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public bool IsPrimary { get; set; }
        public string Resolution => $"{Bounds.Width}x{Bounds.Height}";
        public string DisplayName => IsPrimary ? $"{Name} (主显示器) - {Resolution}" : $"{Name} - {Resolution}";
    }

    public static class MonitorHelper
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int MONITORINFOF_PRIMARY = 0x00000001;

        public static List<MonitorInfo> GetAllMonitors()
        {
            var monitors = new List<MonitorInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    var mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(mi);
                    GetMonitorInfo(hMonitor, ref mi);

                    var bounds = new Rectangle(
                        lprcMonitor.Left,
                        lprcMonitor.Top,
                        lprcMonitor.Right - lprcMonitor.Left,
                        lprcMonitor.Bottom - lprcMonitor.Top);

                    bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

                    monitors.Add(new MonitorInfo
                    {
                        Index = index,
                        Name = $"显示器 {index + 1}",
                        Bounds = bounds,
                        IsPrimary = isPrimary
                    });

                    index++;
                    return true;
                },
                IntPtr.Zero);

            return monitors;
        }
    }
}
