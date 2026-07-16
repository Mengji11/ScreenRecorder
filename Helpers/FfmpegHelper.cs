using System.Diagnostics;
using System.IO;

namespace ScreenRecorder.Helpers
{
    public static class FfmpegHelper
    {
        private static string? _ffmpegPath;

        /// <summary>
        /// 获取 FFmpeg 可执行文件路径
        /// 优先使用嵌入的版本，其次使用系统 PATH 中的版本
        /// </summary>
        public static string GetFfmpegPath()
        {
            if (_ffmpegPath != null)
                return _ffmpegPath;

            // 1. 检查应用目录下的 ffmpeg/ 文件夹
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string embeddedPath = Path.Combine(appDir, "ffmpeg", "ffmpeg.exe");
            if (File.Exists(embeddedPath))
            {
                _ffmpegPath = embeddedPath;
                return _ffmpegPath;
            }

            // 2. 检查项目目录（开发时）
            string projectDir = Path.GetDirectoryName(appDir) ?? appDir;
            string devPath = Path.Combine(projectDir, "ffmpeg", "ffmpeg.exe");
            if (File.Exists(devPath))
            {
                _ffmpegPath = devPath;
                return _ffmpegPath;
            }

            // 3. 使用系统 PATH 中的 ffmpeg
            _ffmpegPath = "ffmpeg";
            return _ffmpegPath;
        }

        /// <summary>
        /// 检查 FFmpeg 是否可用
        /// </summary>
        public static bool IsFfmpegAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = GetFfmpegPath(),
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                process.WaitForExit(5000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取 FFmpeg 版本信息
        /// </summary>
        public static string GetFfmpegVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = GetFfmpegPath(),
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "未知";

                string output = process.StandardOutput.ReadLine() ?? "";
                process.WaitForExit(5000);

                return output;
            }
            catch
            {
                return "未安装";
            }
        }
    }
}
