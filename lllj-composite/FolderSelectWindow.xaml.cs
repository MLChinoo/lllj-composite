using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace atri_composite
{
    public partial class FolderSelectWindow : Window
    {
        public ObservableCollection<string> FolderPaths { get; } = new ObservableCollection<string>();

        public List<string> SelectedFolders => FolderPaths.ToList();

        public FolderSelectWindow()
        {
            InitializeComponent();
            lstFolders.ItemsSource = FolderPaths;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog()
            {
                Title = "选择立绘文件夹",
                DefaultDirectory = Environment.CurrentDirectory,
                IsFolderPicker = true,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureValidNames = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                AddFolderIfNotExists(dialog.FileName);
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstFolders.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
            {
                FolderPaths.Remove(item);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            FolderPaths.Clear();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (FolderPaths.Count == 0)
            {
                MessageBox.Show("请至少添加一个文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LstFolders_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(Directory.Exists))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void LstFolders_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    foreach (var path in files)
                    {
                        if (Directory.Exists(path))
                        {
                            AddFolderIfNotExists(path);
                        }
                    }
                }
            }
        }

        private void AddFolderIfNotExists(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!FolderPaths.Any(p => string.Equals(Path.GetFullPath(p), fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                FolderPaths.Add(fullPath);
            }
        }
    }
}
