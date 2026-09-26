using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AMRIE.WinUI.Pages;

public sealed partial class BatchProcessingPage : Page
{
    public BatchProcessingPage()
    {
        InitializeComponent();
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                ViewModel.InputFilePath = file.Path;

                // Auto-suggest output path if not yet configured
                if (string.IsNullOrWhiteSpace(ViewModel.OutputFilePath))
                {
                    string dir = Path.GetDirectoryName(file.Path) ?? string.Empty;
                    string name = Path.GetFileNameWithoutExtension(file.Path);
                    string ext = Path.GetExtension(file.Path);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";
                    ViewModel.OutputFilePath = Path.Combine(dir, $"{name}_interpreted{ext}");
                }

                // If delimiter can be inferred from extension
                if (file.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.SelectedDelimiter = ",";
                }
                else if (file.FileType.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.SelectedDelimiter = "TAB";
                }
            }
        }
    }
}
