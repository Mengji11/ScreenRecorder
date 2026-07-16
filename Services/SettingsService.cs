using System.IO;
using System.Text.Json;
using ScreenRecorder.Models;

namespace ScreenRecorder.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenRecorder",
            "settings.json");

        public RecordingSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<RecordingSettings>(json) ?? new RecordingSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load settings error: {ex.Message}");
            }

            return new RecordingSettings();
        }

        public void SaveSettings(RecordingSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath) ?? string.Empty;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save settings error: {ex.Message}");
            }
        }
    }
}
