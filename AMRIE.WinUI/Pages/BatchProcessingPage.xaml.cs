using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AMRIE.WinUI.Pages;

public sealed partial class BatchProcessingPage : Page
{
    private bool _isConfigDialogOpen;

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
                if (file.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.ConfigFilePath = file.Path;
                }
                else
                {
                    await ViewModel.SetInputFileAsync(file.Path);
                }
            }
        }
    }

    private async void EditConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_isConfigDialogOpen) return;
        _isConfigDialogOpen = true;
        try
        {
            var dialog = new Dialogs.BatchConfigDialog();
            Common.WindowHelper.ApplyDialogTheme(dialog, this);
            dialog.LoadFromFile(ViewModel.ResolveConfigFilePath());
            var result = await dialog.ShowAsync();
            if ((result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary) &&
                !string.IsNullOrWhiteSpace(dialog.SavedConfigPath))
            {
                ViewModel.ConfigFilePath = dialog.SavedConfigPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AMRIE] Error displaying BatchConfigDialog: {ex}");
        }
        finally
        {
            _isConfigDialogOpen = false;
        }
    }
}
