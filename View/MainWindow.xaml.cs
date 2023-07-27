﻿using System;
using System.Windows;
using ArkPlotWpf.ViewModel;

namespace ArkPlotWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        void ChooseJsonPath_OnClick(object sender,  EventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "请选择Tags.json",
                Filter = "tags.json|*.json"
            };
            if (dialog.ShowDialog() == true)
            {
            
                (DataContext as MainWindowViewModel)?.SelectJsonFile(dialog.FileName);
            }
        }
        void ChooseSpawnFolder_OnClick(object sender,  RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new ();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = folderBrowserDialog.SelectedPath;
                (this.DataContext as MainWindowViewModel)?.SelectOutputFolder(path);
            }
        }

        private void JsonPathBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                (this.DataContext as MainWindowViewModel)?.DropJsonFile(files[0]);
            }
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}