using System.IO;
using System.Text.Json;
using ScreenRecorder.Models;

namespace ScreenRecorder.Services
{
    public class HistoryService
    {
        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenRecorder",
            "history.json");

        private List<RecordingHistoryItem> _history = new();

        public List<RecordingHistoryItem> LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    string json = File.ReadAllText(HistoryPath);
                    _history = JsonSerializer.Deserialize<List<RecordingHistoryItem>>(json) ?? new List<RecordingHistoryItem>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load history error: {ex.Message}");
                _history = new List<RecordingHistoryItem>();
            }

            return _history;
        }

        public void AddEntry(RecordingHistoryItem item)
        {
            _history.Insert(0, item);
            SaveHistory();
        }

        public void RemoveEntry(string filePath, bool save = true)
        {
            _history.RemoveAll(h => h.FilePath == filePath);
            if (save) SaveHistory();
        }

        public void RemoveEntries(IEnumerable<string> filePaths)
        {
            var set = new HashSet<string>(filePaths);
            _history.RemoveAll(h => set.Contains(h.FilePath));
            SaveHistory();
        }

        public void ClearHistory()
        {
            _history.Clear();
            SaveHistory();
        }

        public void SaveHistory()
        {
            try
            {
                string directory = Path.GetDirectoryName(HistoryPath) ?? string.Empty;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HistoryPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save history error: {ex.Message}");
            }
        }
    }
}
