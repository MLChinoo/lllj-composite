using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace atri_composite
{
    public partial class FolderSelectWindow : Window
    {
        public ObservableCollection<string> FolderPaths { get; } = new ObservableCollection<string>();

        public List<string> SelectedFolders => FolderPaths.ToList();

        public Encoding SelectedStandEncoding { get; private set; } = Encoding.Unicode;
        public Encoding SelectedSinfoEncoding { get; private set; } = Encoding.Unicode;
        public Encoding SelectedPbdEncoding { get; private set; } = Encoding.Unicode;

        public FolderSelectWindow()
        {
            InitializeComponent();
            lstFolders.ItemsSource = FolderPaths;
            cmbPreset.ItemsSource = Utils.AvailablePresets;
            // Presets may reference specific Encoding instances; find them in AvailableEncodings
            cmbStandEncoding.ItemsSource = Utils.AvailableEncodings;
            cmbSinfoEncoding.ItemsSource = Utils.AvailableEncodings;
            cmbPbdEncoding.ItemsSource = Utils.AvailableEncodings;

            // Select first preset (International Chinese)
            if (cmbPreset.Items.Count > 0)
                cmbPreset.SelectedIndex = 0;
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
        private void CmbPreset_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbPreset.SelectedItem is Utils.EncodingPreset preset)
            {
                SelectEncoding(cmbStandEncoding, preset.StandEncoding);
                SelectEncoding(cmbSinfoEncoding, preset.SinfoEncoding);
                SelectEncoding(cmbPbdEncoding, preset.PbdEncoding);
            }
        }

        private void CmbEncoding_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbStandEncoding.SelectedItem is Utils.EncodingInfo info)
                SelectedStandEncoding = info.Encoding;
            if (cmbSinfoEncoding.SelectedItem is Utils.EncodingInfo info2)
                SelectedSinfoEncoding = info2.Encoding;
            if (cmbPbdEncoding.SelectedItem is Utils.EncodingInfo info3)
                SelectedPbdEncoding = info3.Encoding;
        }

        private static void SelectEncoding(System.Windows.Controls.ComboBox combo, Encoding target)
        {
            foreach (Utils.EncodingInfo item in combo.Items)
            {
                if (item.Encoding.CodePage == target.CodePage
                    && item.Encoding.GetPreamble().Length == target.GetPreamble().Length)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            combo.SelectedIndex = 0;
        }
    }
}
