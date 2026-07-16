using System.Windows.Input;

namespace ScreenRecorder.Models
{
    public class RecordingSettings
    {
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int FrameRate { get; set; } = 30;
        public bool RecordSystemAudio { get; set; } = true;
        public bool RecordMicrophone { get; set; } = true;
        public string OutputFormat { get; set; } = "mp4";
        public string SavePath { get; set; } = string.Empty;
        public int MaxDurationMinutes { get; set; } = 0;

        public Key HotkeyStartStop { get; set; } = Key.F5;
        public Key HotkeyPauseResume { get; set; } = Key.F7;
        public Key HotkeyFullScreen { get; set; } = Key.F1;
        public Key HotkeyWindow { get; set; } = Key.F2;
        public Key HotkeyRegion { get; set; } = Key.F3;
        public Key HotkeyScreenshot { get; set; } = Key.F8;

        public bool UseCtrlModifier { get; set; } = true;
        public bool UseAltModifier { get; set; } = false;
        public bool UseShiftModifier { get; set; } = false;

        public bool ShowMouseClickHighlight { get; set; } = true;
        public bool EnableWatermark { get; set; } = false;
        public string WatermarkText { get; set; } = string.Empty;
        public string WatermarkPosition { get; set; } = "右下角";
        public int WatermarkOpacity { get; set; } = 50;
        public int MonitorIndex { get; set; } = 0;
    }
}
