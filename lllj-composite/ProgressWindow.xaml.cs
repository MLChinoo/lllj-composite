using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace atri_composite
{
    public partial class ProgressWindow : Window
    {
        private readonly List<string> _folders;

        public ProgressWindow(List<string> folders)
        {
            InitializeComponent();
            _folders = folders;
            Loaded += ProgressWindow_Loaded;
        }

        private void ProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    Utils.InitializeFileCache(_folders, (currentFolder, processed, total, fileCount, memorySize) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (total > 0)
                            {
                                progressBar.Value = (double)processed / total * 100;
                            }
                            txtTitle.Text = $"正在扫描: {System.IO.Path.GetFileName(currentFolder)}";
                            txtProgress.Text = $"已扫描: {processed}/{total} 文件夹";
                            txtIndexed.Text = $"已索引: {fileCount} 个匹配后缀的文件";
                            txtMemory.Text = FormatBytes(memorySize);
                        }));
                    });

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DialogResult = true;
                        Close();
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show($"扫描文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        DialogResult = false;
                        Close();
                    }));
                }
            });
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F2} KB";
            double mb = kb / 1024.0;
            return $"{mb:F2} MB";
        }
    }
}
