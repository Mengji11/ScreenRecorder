using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ScreenRecorder.Helpers;
using ScreenRecorder.Models;

namespace ScreenRecorder.Services
{
    public class RecorderService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;

        private readonly ScreenCaptureService _screenCapture;
        private readonly AudioCaptureService _audioCapture;
        private RecordingSettings _settings;
        private volatile RecordingState _state = RecordingState.Idle;
        private RecordingMode _mode = RecordingMode.FullScreen;
        private System.Windows.Threading.DispatcherTimer? _durationTimer;
        private DateTime _startTime;
        private string _currentOutputPath = string.Empty;
        private TimeSpan _currentDuration;
        private Rect _captureRegion;
        private IntPtr _windowHandle;
        private int _captureOffsetX;
        private int _captureOffsetY;
        private readonly object _mouseClicksLock = new();
        private List<MouseClickInfo> _mouseClicks = new();
        private bool _wasMouseDown;
        private volatile bool _isSaving;

        // Streaming: write frames to disk during recording to keep memory constant
        private FileStream? _frameStream;
        private string _tempFramePath = string.Empty;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameCount;
        private CancellationTokenSource? _captureCts;
        private System.Diagnostics.Stopwatch? _captureStopwatch;

        public event EventHandler<RecordingState>? StateChanged;
        public event EventHandler<TimeSpan>? DurationChanged;
        public event EventHandler<string>? RecordingCompleted;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<string>? SavingProgress;

        public RecordingState State => _state;
        public TimeSpan CurrentDuration => _currentDuration;
        public bool IsSaving => _isSaving;

        /// <summary>
        /// Actual capture width/height (set after first frame).
        /// Used by MainWindow to record correct resolution in history.
        /// </summary>
        public int CaptureWidth => _frameWidth;
        public int CaptureHeight => _frameHeight;

        public RecorderService()
        {
            _screenCapture = new ScreenCaptureService();
            _audioCapture = new AudioCaptureService();
            _audioCapture.ErrorOccurred += (s, msg) => ErrorOccurred?.Invoke(this, msg);
            _settings = new RecordingSettings();
        }

        public void UpdateSettings(RecordingSettings settings)
        {
            _settings = settings;
        }

        public void StartRecording(RecordingMode mode, string outputPath, Rect? region = null, IntPtr windowHandle = default)
        {
            if (_state == RecordingState.Recording || _isSaving) return;

            try
            {
                _mode = mode;
                _currentOutputPath = outputPath;
                _captureRegion = region ?? new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                _windowHandle = windowHandle;
                _currentDuration = TimeSpan.Zero;
                _startTime = DateTime.Now;
                _frameCount = 0;
                _captureStopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Compute capture offset for mouse click coordinate adjustment
                _captureOffsetX = 0;
                _captureOffsetY = 0;
                if (mode == RecordingMode.Region)
                {
                    _captureOffsetX = (int)_captureRegion.X;
                    _captureOffsetY = (int)_captureRegion.Y;
                }
                else if (mode == RecordingMode.Window && windowHandle != default)
                {
                    var windowRect = _screenCapture.GetWindowRectangle(windowHandle);
                    _captureOffsetX = (int)windowRect.X;
                    _captureOffsetY = (int)windowRect.Y;
                }
                else if (mode == RecordingMode.FullScreen && _settings.MonitorIndex > 0)
                {
                    var monitors = MonitorHelper.GetAllMonitors();
                    if (_settings.MonitorIndex < monitors.Count)
                    {
                        _captureOffsetX = monitors[_settings.MonitorIndex].Bounds.X;
                        _captureOffsetY = monitors[_settings.MonitorIndex].Bounds.Y;
                    }
                }

                // Create temp file for streaming frames to disk
                _tempFramePath = Path.Combine(Path.GetTempPath(), $"sr_frames_{Guid.NewGuid():N}.bin");
                _frameStream = new FileStream(_tempFramePath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);

                string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(outputPath);
                string audioSystemPath = Path.Combine(directory, $"{baseName}_system.wav");
                string audioMicPath = Path.Combine(directory, $"{baseName}_mic.wav");

                _audioCapture.StartRecording(
                    audioSystemPath,
                    audioMicPath,
                    _settings.RecordSystemAudio,
                    _settings.RecordMicrophone);

                // Frame capture runs on background thread to keep UI responsive
                _captureCts?.Dispose();
                _captureCts = new CancellationTokenSource();
                Task.Run(() => CaptureLoop(_captureCts.Token));

                // Duration timer stays on UI thread for status bar updates
                _durationTimer = new System.Windows.Threading.DispatcherTimer();
                _durationTimer.Interval = TimeSpan.FromSeconds(1);
                _durationTimer.Tick += UpdateDuration;
                _durationTimer.Start();

                _state = RecordingState.Recording;
                StateChanged?.Invoke(this, _state);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"录制启动失败: {ex.Message}");
                StopRecording();
            }
        }

        private void CaptureLoop(CancellationToken ct)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            double intervalMs = 1000.0 / _settings.FrameRate;
            double nextFrameTime = intervalMs;

            while (!ct.IsCancellationRequested)
            {
                double elapsed = stopwatch.Elapsed.TotalMilliseconds;
                if (elapsed >= nextFrameTime)
                {
                    try
                    {
                        CaptureFrame();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Frame capture error: {ex.Message}");
                    }
                    nextFrameTime += intervalMs;
                    // If we fell far behind (e.g. first frame was slow), reset to avoid burst
                    if (nextFrameTime < elapsed - intervalMs * 2)
                        nextFrameTime = elapsed + intervalMs;
                }
                else
                {
                    double remaining = nextFrameTime - elapsed;
                    if (remaining > 5)
                    {
                        Thread.Sleep(1);
                    }
                    else if (remaining > 1)
                    {
                        Thread.Sleep(0);
                    }
                    else
                    {
                        Thread.SpinWait(100);
                    }
                }
            }
        }

        private void CaptureFrame()
        {
            if (_state != RecordingState.Recording || _frameStream == null) return;

            try
            {
                Bitmap frame;

                switch (_mode)
                {
                    case RecordingMode.FullScreen:
                        if (_settings.MonitorIndex > 0)
                        {
                            frame = _screenCapture.CaptureMonitor(_settings.MonitorIndex);
                        }
                        else
                        {
                            frame = _screenCapture.CaptureFullScreen();
                        }
                        break;

                    case RecordingMode.Region:
                        frame = _screenCapture.CaptureRegion(
                            (int)_captureRegion.X,
                            (int)_captureRegion.Y,
                            (int)_captureRegion.Width,
                            (int)_captureRegion.Height);
                        break;

                    case RecordingMode.Window:
                        frame = _screenCapture.CaptureWindow(_windowHandle);
                        break;

                    default:
                        frame = _screenCapture.CaptureFullScreen();
                        break;
                }

                if (_frameCount == 0)
                {
                    _frameWidth = frame.Width;
                    _frameHeight = frame.Height;
                }

                if (_settings.EnableWatermark && !string.IsNullOrEmpty(_settings.WatermarkText))
                {
                    ApplyWatermark(frame, _settings.WatermarkText, _settings.WatermarkPosition, _settings.WatermarkOpacity);
                }

                if (_settings.ShowMouseClickHighlight)
                {
                    DetectAndDrawMouseClick(frame);
                }

                // Convert to BGR24 and write directly to disk (memory stays constant)
                byte[] bgrData = BitmapToBgr24(frame);
                try
                {
                    int bgrLen = _frameWidth * _frameHeight * 3;
                    lock (_frameStream)
                    {
                        _frameStream.Write(bgrData, 0, bgrLen);
                    }
                    frame.Dispose();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bgrData);
                }
                Interlocked.Increment(ref _frameCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Frame capture error: {ex.Message}");
            }
        }

        private void DetectAndDrawMouseClick(Bitmap frame)
        {
            bool isMouseDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ||
                               (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            if (isMouseDown && !_wasMouseDown)
            {
                GetCursorPos(out POINT cursorPos);

                int x = cursorPos.X - _captureOffsetX;
                int y = cursorPos.Y - _captureOffsetY;

                lock (_mouseClicksLock)
                {
                    _mouseClicks.Add(new MouseClickInfo
                    {
                        X = x,
                        Y = y,
                        Time = DateTime.Now,
                        IsLeftButton = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
                    });
                }
            }

            _wasMouseDown = isMouseDown;

            List<MouseClickInfo> snapshot;
            lock (_mouseClicksLock)
            {
                var now = DateTime.Now;
                _mouseClicks.RemoveAll(c => (now - c.Time).TotalMilliseconds > 500);
                snapshot = _mouseClicks.ToList();
            }

            using var graphics = Graphics.FromImage(frame);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var now2 = DateTime.Now;
            foreach (var click in snapshot)
            {
                int elapsed = (int)(now2 - click.Time).TotalMilliseconds;
                int radius = 20 + elapsed / 10;
                int alpha = Math.Max(0, 200 - elapsed / 2);

                var color = click.IsLeftButton
                    ? System.Drawing.Color.FromArgb(alpha, 0, 120, 215)
                    : System.Drawing.Color.FromArgb(alpha, 215, 0, 0);

                using var brush = new SolidBrush(color);
                graphics.FillEllipse(brush, click.X - radius, click.Y - radius, radius * 2, radius * 2);

                using var pen = new Pen(System.Drawing.Color.FromArgb(alpha, 255, 255, 255), 2);
                graphics.DrawEllipse(pen, click.X - radius, click.Y - radius, radius * 2, radius * 2);
            }
        }

        private void ApplyWatermark(Bitmap frame, string text, string position, int opacity)
        {
            using var graphics = Graphics.FromImage(frame);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Create font - use generic sans-serif fallback
            using var font = new System.Drawing.Font(
                System.Drawing.FontFamily.GenericSansSerif, 16, System.Drawing.FontStyle.Bold);

            var textSize = graphics.MeasureString(text, font);
            float margin = 20;

            float x, y;
            switch (position)
            {
                case "左上角":
                    x = margin;
                    y = margin;
                    break;
                case "右上角":
                    x = frame.Width - textSize.Width - margin;
                    y = margin;
                    break;
                case "左下角":
                    x = margin;
                    y = frame.Height - textSize.Height - margin;
                    break;
                case "居中":
                    x = (frame.Width - textSize.Width) / 2;
                    y = (frame.Height - textSize.Height) / 2;
                    break;
                default: // 右下角
                    x = frame.Width - textSize.Width - margin;
                    y = frame.Height - textSize.Height - margin;
                    break;
            }

            int alphaValue = opacity * 255 / 100;
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(alphaValue, 255, 255, 255));
            using var outlineBrush = new SolidBrush(System.Drawing.Color.FromArgb(alphaValue, 0, 0, 0));

            // Draw outline via 3x3 grid offset
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    graphics.DrawString(text, font, outlineBrush, x + offsetX, y + offsetY);
                }
            }

            graphics.DrawString(text, font, brush, x, y);
        }

        private void UpdateDuration(object? sender, EventArgs e)
        {
            _currentDuration = DateTime.Now - _startTime;
            DurationChanged?.Invoke(this, _currentDuration);

            if (_settings.MaxDurationMinutes > 0 &&
                _currentDuration.TotalMinutes >= _settings.MaxDurationMinutes)
            {
                StopRecording();
            }
        }

        public void PauseRecording()
        {
            if (_state != RecordingState.Recording) return;

            _captureCts?.Cancel();
            _captureCts?.Dispose();
            _captureCts = null;
            _audioCapture.StopRecording();

            _state = RecordingState.Paused;
            StateChanged?.Invoke(this, _state);
        }

        public void ResumeRecording()
        {
            if (_state != RecordingState.Paused) return;

            // Restart background capture
            _captureCts?.Dispose();
            _captureCts = new CancellationTokenSource();
            Task.Run(() => CaptureLoop(_captureCts.Token));

            string directory = Path.GetDirectoryName(_currentOutputPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(_currentOutputPath);

            string audioSystemPath = Path.Combine(directory, $"{baseName}_system.wav");
            string audioMicPath = Path.Combine(directory, $"{baseName}_mic.wav");

            _audioCapture.StartRecording(
                audioSystemPath,
                audioMicPath,
                _settings.RecordSystemAudio,
                _settings.RecordMicrophone);

            _state = RecordingState.Recording;
            StateChanged?.Invoke(this, _state);
        }

        public void StopRecording()
        {
            if (_state == RecordingState.Idle) return;

            // Stop background capture thread
            _captureCts?.Cancel();
            _captureCts?.Dispose();
            _captureCts = null;

            _durationTimer?.Stop();
            _audioCapture.StopRecording();

            // Close stream and capture state for background processing
            _frameStream?.Flush();
            _frameStream?.Dispose();
            _frameStream = null;

            string framePath = _tempFramePath;
            int width = _frameWidth;
            int height = _frameHeight;
            int count = _frameCount;
            string outputPath = _currentOutputPath;
            var settings = _settings;
            var actualDuration = _captureStopwatch?.Elapsed ?? (DateTime.Now - _startTime);
            _captureStopwatch?.Stop();

            _state = RecordingState.Idle;
            StateChanged?.Invoke(this, _state);

            if (count > 0 && File.Exists(framePath))
            {
                _isSaving = true;
                Task.Run(() => SaveRecordingBackground(framePath, width, height, count, actualDuration, outputPath, settings));
            }
            else
            {
                CleanupTempFilesBackground();
            }
        }

        /// <summary>
        /// Convert a Bitmap to raw BGR24 byte array using pooled buffer.
        /// Caller MUST return the buffer via ArrayPool&lt;byte&gt;.Shared.Return().
        /// </summary>
        private static byte[] BitmapToBgr24(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int stride = w * 3;

            var result = ArrayPool<byte>.Shared.Rent(stride * h);

            // Use a temporary 24bpp bitmap to ensure correct format
            using var temp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(temp))
            {
                g.DrawImage(bitmap, 0, 0, w, h);
            }

            var bitmapData = temp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(
                        bitmapData.Scan0 + y * bitmapData.Stride,
                        result,
                        y * stride,
                        stride);
                }
            }
            finally
            {
                temp.UnlockBits(bitmapData);
            }

            return result;
        }

        private void SaveRecordingBackground(string framePath, int width, int height, int frameCount, TimeSpan actualDuration, string outputPath, RecordingSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(outputPath);
                string extension = Path.GetExtension(outputPath);

                string audioSystemPath = Path.Combine(directory, $"{baseName}_system.wav");
                string audioMicPath = Path.Combine(directory, $"{baseName}_mic.wav");

                SavingProgress?.Invoke(this, $"正在编码视频 (0/{frameCount})...");

                double actualFps = actualDuration.TotalSeconds > 0
                    ? Math.Max(1, frameCount / actualDuration.TotalSeconds)
                    : settings.FrameRate;

                // Always encode video to temp file first
                string tempVideoPath = Path.Combine(directory, $"{baseName}_temp{extension}");
                SaveFramesAsVideo(tempVideoPath, framePath, width, height, frameCount, actualFps, settings);
                try { File.Delete(framePath); } catch { }

                // Check available audio
                bool hasSystemAudio = File.Exists(audioSystemPath) && new FileInfo(audioSystemPath).Length > 1000;
                bool hasMicAudio = File.Exists(audioMicPath) && new FileInfo(audioMicPath).Length > 1000;

                SavingProgress?.Invoke(this, "正在合并音视频...");

                string ffmpegArgs;

                // Always merge with audio track (silent or real) for maximum player compatibility
                if (hasSystemAudio && hasMicAudio)
                {
                    ffmpegArgs = $"-y -i \"{tempVideoPath}\" -i \"{audioSystemPath}\" -i \"{audioMicPath}\" -filter_complex \"[1:a][2:a]amerge=inputs=2[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"";
                }
                else if (hasSystemAudio)
                {
                    ffmpegArgs = $"-y -i \"{tempVideoPath}\" -i \"{audioSystemPath}\" -map 0:v -map 1:a -c:v copy -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"";
                }
                else if (hasMicAudio)
                {
                    ffmpegArgs = $"-y -i \"{tempVideoPath}\" -i \"{audioMicPath}\" -map 0:v -map 1:a -c:v copy -c:a aac -b:a 128k -movflags +faststart \"{outputPath}\"";
                }
                else
                {
                    // No audio: add silent audio track for player compatibility
                    ffmpegArgs = $"-y -i \"{tempVideoPath}\" -f lavfi -i anullsrc=channel_layout=stereo:sample_rate=44100 -map 0:v -map 1:a -c:v copy -c:a aac -b:a 128k -shortest -movflags +faststart \"{outputPath}\"";
                }

                var psi2 = new ProcessStartInfo
                {
                    FileName = FfmpegHelper.GetFfmpegPath(),
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process2 = new System.Diagnostics.Process();
                process2.StartInfo = psi2;
                process2.Start();
                string mergeStderr = process2.StandardError.ReadToEnd();
                process2.WaitForExit(120000);

                System.Diagnostics.Debug.WriteLine($"[FFmpeg merge] exit={process2.ExitCode} output={new FileInfo(outputPath).Length} bytes");
                if (process2.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[FFmpeg merge ERROR] {mergeStderr}");
                }

                CleanupWithRetry(tempVideoPath);
                CleanupWithRetry(audioSystemPath);
                CleanupWithRetry(audioMicPath);

                if (File.Exists(outputPath))
                {
                    RecordingCompleted?.Invoke(this, outputPath);
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "视频保存失败");
                }
            }
            catch (Exception ex)
            {
                try { File.Delete(framePath); } catch { }
                ErrorOccurred?.Invoke(this, $"保存录制失败: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void SaveFramesAsVideo(string outputPath, string framePath, int width, int height, int frameCount, double actualFps, RecordingSettings settings)
        {
            if (frameCount == 0) return;

            var psi = new ProcessStartInfo
            {
                FileName = FfmpegHelper.GetFfmpegPath(),
                Arguments = $"-y -f rawvideo -pix_fmt bgr24 -s {width}x{height} -r {actualFps:F2} -i pipe:0 -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process();
            process.StartInfo = psi;
            process.Start();

            // Write all frame data to FFmpeg stdin
            using (var stdin = process.StandardInput.BaseStream)
            {
                int bytesPerFrame = width * height * 3;
                var buffer = new byte[bytesPerFrame];

                using var frameStream = new FileStream(framePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                for (int i = 0; i < frameCount; i++)
                {
                    int read = frameStream.Read(buffer, 0, bytesPerFrame);
                    if (read < bytesPerFrame) break;
                    stdin.Write(buffer, 0, bytesPerFrame);
                }
                stdin.Flush();
            }

            // Read stderr to avoid deadlock, then wait for exit
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(120000);

            System.Diagnostics.Debug.WriteLine($"[FFmpeg] exit={process.ExitCode} file={new FileInfo(outputPath).Length} bytes frames={frameCount} size={width}x{height} fps={actualFps:F1}");
            if (process.ExitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[FFmpeg ERROR] {stderr}");
            }
        }

        private void CleanupTempFilesBackground()
        {
            Task.Run(() =>
            {
                try
                {
                    string directory = Path.GetDirectoryName(_currentOutputPath) ?? string.Empty;
                    string baseName = Path.GetFileNameWithoutExtension(_currentOutputPath);

                    var tempFiles = Directory.GetFiles(directory, $"{baseName}_*")
                        .Where(f => f != _currentOutputPath)
                        .ToList();

                    foreach (var file in tempFiles)
                    {
                        CleanupWithRetry(file);
                    }
                }
                catch { }
            });
        }

        private static void CleanupWithRetry(string filePath, int maxRetries = 5)
        {
            if (!File.Exists(filePath)) return;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(filePath);
                    return; // success
                }
                catch (IOException)
                {
                    Thread.Sleep(300); // file handle may still be held
                }
                catch { return; }
            }
        }

        public void TakeScreenshot(string outputPath)
        {
            try
            {
                using var screenshot = _screenCapture.CaptureFullScreen();
                screenshot.Save(outputPath, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"截图失败: {ex.Message}");
            }
        }

        public bool ExportGif(string videoPath, string gifPath, int fps = 10, int width = 480)
        {
            try
            {
                if (!File.Exists(videoPath))
                {
                    ErrorOccurred?.Invoke(this, "视频文件不存在");
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = FfmpegHelper.GetFfmpegPath(),
                    Arguments = $"-y -i \"{videoPath}\" -filter_complex \"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{gifPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                process.WaitForExit(60000); // 60秒超时

                if (process.ExitCode == 0 && File.Exists(gifPath))
                {
                    return true;
                }

                ErrorOccurred?.Invoke(this, "GIF导出失败");
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"GIF导出失败: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _captureCts?.Cancel();
            _captureCts?.Dispose();
            _durationTimer?.Stop();
            _audioCapture?.Dispose();

            _frameStream?.Dispose();
            try { if (File.Exists(_tempFramePath)) File.Delete(_tempFramePath); } catch { }

            lock (_mouseClicksLock)
            {
                _mouseClicks.Clear();
            }

            GC.SuppressFinalize(this);
        }
    }

    public class MouseClickInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public DateTime Time { get; set; }
        public bool IsLeftButton { get; set; }
    }
}
