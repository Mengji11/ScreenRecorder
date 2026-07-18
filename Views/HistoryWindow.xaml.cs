using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScreenRecorder.Models;
using ScreenRecorder.Services;

namespace ScreenRecorder.Views
{
    public partial class HistoryWindow : Window
    {
        private readonly HistoryService _historyService;
        private List<RecordingHistoryItem> _history;

        public HistoryWindow()
        {
            InitializeComponent();

            _historyService = new HistoryService();
            _history = _historyService.LoadHistory();

            LoadHistory();
        }

        private void LoadHistory()
        {
            // Filter out entries whose files no longer exist
            var stalePaths = _history.Where(item => !File.Exists(item.FilePath)).Select(item => item.FilePath).ToList();
            if (stalePaths.Count > 0)
            {
                _historyService.RemoveEntries(stalePaths);
            }
            _history = _history.Where(item => File.Exists(item.FilePath)).ToList();

            dgHistory.ItemsSource = null;
            dgHistory.ItemsSource = _history;
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = _history.Count(i => i.IsSelected);
            txtSelected.Text = count > 0 ? $"已选 {count}/{_history.Count} 项" : "";
        }

        private void DgHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _history) item.IsSelected = true;
            dgHistory.Items.Refresh();
            UpdateSelectedCount();
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _history) item.IsSelected = false;
            dgHistory.Items.Refresh();
            UpdateSelectedCount();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _history = _historyService.LoadHistory();
            LoadHistory();
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _history.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请先勾选要删除的记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定删除 {selected.Count} 条记录？\n（同时删除对应视频文件）",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            int deletedCount = 0;
            int failedCount = 0;

            foreach (var item in selected)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                        deletedCount++;
                    }
                    else
                    {
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    System.Diagnostics.Debug.WriteLine($"Failed to delete {item.FilePath}: {ex.Message}");
                }
            }

            // Batch remove from history (single save)
            _historyService.RemoveEntries(selected.Select(i => i.FilePath));

            _history = _historyService.LoadHistory();
            LoadHistory();

            if (failedCount > 0)
            {
                MessageBox.Show($"成功删除 {deletedCount} 个文件，{failedCount} 个文件删除失败（可能被其他程序占用）",
                    "删除结果", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgHistory.SelectedItem as RecordingHistoryItem;
            string? directory = selected != null
                ? Path.GetDirectoryName(selected.FilePath)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Process.Start("explorer.exe", directory);
            }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is RecordingHistoryItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.FilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("文件不存在或已被删除", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("请先选择一条记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new VideoEditorWindow();
            editorWindow.Owner = this;
            editorWindow.ShowDialog();
        }

        private void BtnExportGif_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is RecordingHistoryItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    string gifPath = Path.ChangeExtension(item.FilePath, ".gif");

                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "导出GIF",
                        Filter = "GIF文件|*.gif",
                        FileName = Path.GetFileNameWithoutExtension(item.FilePath) + ".gif"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        gifPath = saveDialog.FileName;

                        var recorder = new RecorderService();
                        if (recorder.ExportGif(item.FilePath, gifPath))
                        {
                            MessageBox.Show($"GIF已导出到: {gifPath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("GIF导出失败，请确保FFmpeg已安装", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("文件不存在或已被删除", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("请先选择一条记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
