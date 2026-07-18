using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ScreenRecorder.Services
{
    public class AudioCaptureService : IDisposable
    {
        private WasapiLoopbackCapture? _systemCapture;
        private WasapiCapture? _microphoneCapture;
        private WaveFileWriter? _systemWriter;
        private WaveFileWriter? _microphoneWriter;
        private volatile bool _isRecording;

        public event EventHandler<byte[]>? AudioDataAvailable;
        public event EventHandler<string>? ErrorOccurred;

        /// <summary>
        /// 检查系统音频设备是否可用
        /// </summary>
        public static bool IsSystemAudioAvailable()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                return device != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查麦克风设备是否可用
        /// </summary>
        public static bool IsMicrophoneAvailable()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                return device != null;
            }
            catch
            {
                return false;
            }
        }

        public void StartRecording(string systemAudioPath, string microphonePath, bool recordSystem, bool recordMicrophone)
        {
            if (_isRecording) return;

            _isRecording = true;

            if (recordSystem)
            {
                if (IsSystemAudioAvailable())
                {
                    StartSystemCapture(systemAudioPath);
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "未检测到系统音频设备，将仅录制视频");
                }
            }

            if (recordMicrophone)
            {
                if (IsMicrophoneAvailable())
                {
                    StartMicrophoneCapture(microphonePath);
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "未检测到麦克风设备，将仅录制系统声音");
                }
            }
        }

        private void StartSystemCapture(string filePath)
        {
            try
            {
                _systemCapture = new WasapiLoopbackCapture();
                _systemWriter = new WaveFileWriter(filePath, _systemCapture.WaveFormat);

                _systemCapture.DataAvailable += (s, e) =>
                {
                    var writer = _systemWriter;
                    if (_isRecording && e.BytesRecorded > 0 && writer != null)
                    {
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                        AudioDataAvailable?.Invoke(this, e.Buffer);
                    }
                };

                _systemCapture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"System audio capture error: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"系统音频捕获失败: {ex.Message}");
            }
        }

        private void StartMicrophoneCapture(string filePath)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);

                _microphoneCapture = new WasapiCapture(device);
                _microphoneWriter = new WaveFileWriter(filePath, _microphoneCapture.WaveFormat);

                _microphoneCapture.DataAvailable += (s, e) =>
                {
                    var writer = _microphoneWriter;
                    if (_isRecording && e.BytesRecorded > 0 && writer != null)
                    {
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                _microphoneCapture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Microphone capture error: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"麦克风捕获失败: {ex.Message}");
            }
        }

        public void StopRecording()
        {
            _isRecording = false;

            try
            {
                _systemCapture?.StopRecording();
                _systemCapture?.Dispose();
                _systemCapture = null;

                _systemWriter?.Flush();
                _systemWriter?.Dispose();
                _systemWriter = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"System audio stop error: {ex.Message}");
            }

            try
            {
                _microphoneCapture?.StopRecording();
                _microphoneCapture?.Dispose();
                _microphoneCapture = null;

                _microphoneWriter?.Flush();
                _microphoneWriter?.Dispose();
                _microphoneWriter = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Microphone stop error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopRecording();
            GC.SuppressFinalize(this);
        }
    }
}
