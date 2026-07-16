using System.Diagnostics;
using System.IO;
using System.Windows;
using ScreenRecorder.Helpers;

namespace ScreenRecorder.Views
{
    public partial class VideoEditorWindow : Window
    {
        public VideoEditorWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowseTrimFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择视频文件",
                Filter = "视频文件|*.mp4;*.avi;*.mkv|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                txtTrimFile.Text = dialog.FileName;
            }
        }

        private void BtnTrim_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtTrimFile.Text) || !File.Exists(txtTrimFile.Text))
            {
                MessageBox.Show("请选择有效的视频文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!TimeSpan.TryParse(txtStartTime.Text, out var startTime))
            {
                MessageBox.Show("开始时间格式无效（应为 HH:mm:ss）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!TimeSpan.TryParse(txtEndTime.Text, out var endTime))
            {
                MessageBox.Show("结束时间格式无效（应为 HH:mm:ss）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (endTime <= startTime)
            {
                MessageBox.Show("结束时间必须大于开始时间", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存裁剪后的视频",
                Filter = "MP4文件|*.mp4",
                FileName = Path.GetFileNameWithoutExtension(txtTrimFile.Text) + "_裁剪.mp4"
            };

            if (saveDialog.ShowDialog() == true)
            {
                TrimVideo(txtTrimFile.Text, saveDialog.FileName, startTime, endTime);
            }
        }

        private void TrimVideo(string inputPath, string outputPath, TimeSpan start, TimeSpan end)
        {
            try
            {
                string startStr = start.ToString(@"hh\:mm\:ss");
                string endStr = end.ToString(@"hh\:mm\:ss");
                string duration = (end - start).ToString(@"hh\:mm\:ss");

                var psi = new ProcessStartInfo
                {
                    FileName = FfmpegHelper.GetFfmpegPath(),
                    Arguments = $"-y -i \"{inputPath}\" -ss {startStr} -t {duration} -c copy \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(120000);

                if (process?.ExitCode == 0 && File.Exists(outputPath))
                {
                    MessageBox.Show($"裁剪完成！\n保存到: {outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("裁剪失败，请检查FFmpeg是否安装", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"裁剪失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddMergeFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择视频文件",
                Filter = "视频文件|*.mp4;*.avi;*.mkv|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!lstMergeFiles.Items.Contains(file))
                    {
                        lstMergeFiles.Items.Add(file);
                    }
                }
            }
        }

        private void BtnRemoveMergeFile_Click(object sender, RoutedEventArgs e)
        {
            if (lstMergeFiles.SelectedItem != null)
            {
                lstMergeFiles.Items.Remove(lstMergeFiles.SelectedItem);
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = lstMergeFiles.SelectedIndex;
            if (index > 0)
            {
                var item = lstMergeFiles.Items[index];
                lstMergeFiles.Items.RemoveAt(index);
                lstMergeFiles.Items.Insert(index - 1, item);
                lstMergeFiles.SelectedIndex = index - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = lstMergeFiles.SelectedIndex;
            if (index < lstMergeFiles.Items.Count - 1)
            {
                var item = lstMergeFiles.Items[index];
                lstMergeFiles.Items.RemoveAt(index);
                lstMergeFiles.Items.Insert(index + 1, item);
                lstMergeFiles.SelectedIndex = index + 1;
            }
        }

        private void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            if (lstMergeFiles.Items.Count < 2)
            {
                MessageBox.Show("请至少添加2个视频文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存合并后的视频",
                Filter = "MP4文件|*.mp4",
                FileName = "合并视频.mp4"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var files = new List<string>();
                foreach (var item in lstMergeFiles.Items)
                {
                    files.Add(item.ToString()!);
                }
                MergeVideos(files, saveDialog.FileName);
            }
        }

        private void MergeVideos(List<string> inputPaths, string outputPath)
        {
            try
            {
                // 创建临时文件列表
                string tempDir = Path.GetTempPath();
                string listFile = Path.Combine(tempDir, "merge_list.txt");

                using (var writer = new StreamWriter(listFile))
                {
                    foreach (var path in inputPaths)
                    {
                        writer.WriteLine($"file '{path}'");
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = FfmpegHelper.GetFfmpegPath(),
                    Arguments = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(300000); // 5分钟超时

                // 清理临时文件
                try { File.Delete(listFile); } catch { }

                if (process?.ExitCode == 0 && File.Exists(outputPath))
                {
                    MessageBox.Show($"合并完成！\n保存到: {outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("合并失败，请检查FFmpeg是否安装", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"合并失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
