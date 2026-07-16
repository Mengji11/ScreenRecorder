namespace ScreenRecorder.Models
{
    public class RecordingHistoryItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime RecordingTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long FileSizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public string DurationString => Duration.ToString(@"hh\:mm\:ss");
        public string FileSizeString => FormatFileSize(FileSizeBytes);
        public string ResolutionString => $"{Width}x{Height}";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
