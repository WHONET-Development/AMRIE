using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AMRIE.WinUI.Common;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace AMRIE.WinUI.ViewModels;

public partial class BatchProcessingViewModel : ObservableObject
{
    private BackgroundWorker? _worker;

    [ObservableProperty]
    private string _inputFilePath = string.Empty;

    [ObservableProperty]
    private string _configFilePath = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private string _selectedDelimiter = "TAB";

    [ObservableProperty]
    private double _guidelineYear = Constants.BreakpointTableRevisionYear;

    [ObservableProperty]
    private bool _restrictGuidelineYear = true;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private int _progressPercentage = 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage = false;

    [ObservableProperty]
    private bool _isSuccess = false;

    [ObservableProperty]
    private bool _isError = false;

    [ObservableProperty]
    private InfoBarSeverity _statusInfoBarSeverity = InfoBarSeverity.Informational;

    public string[] Delimiters { get; } = new[] { "TAB", "|", ",", ";" };

    public BatchProcessingViewModel()
    {
        try
        {
            string defaultConfig = Path.Combine(AppContext.BaseDirectory, "Resources", "SampleConfig.json");
            if (File.Exists(defaultConfig))
            {
                ConfigFilePath = defaultConfig;
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task BrowseInputFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            WindowHelper.InitializeWithMainWindow(picker);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".tsv");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                InputFilePath = file.Path;

                if (string.IsNullOrWhiteSpace(OutputFilePath))
                {
                    string dir = Path.GetDirectoryName(file.Path) ?? string.Empty;
                    string name = Path.GetFileNameWithoutExtension(file.Path);
                    string ext = Path.GetExtension(file.Path);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";
                    OutputFilePath = Path.Combine(dir, $"{name}_interpreted{ext}");
                }

                if (file.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedDelimiter = ",";
                }
                else if (file.FileType.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
                {
                    SelectedDelimiter = "TAB";
                }
            }
        }
        catch (Exception ex)
        {
            SetError($"Error browsing for input file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task BrowseConfigFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            WindowHelper.InitializeWithMainWindow(picker);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ConfigFilePath = file.Path;
            }
        }
        catch (Exception ex)
        {
            SetError($"Error browsing for config file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task BrowseOutputFileAsync()
    {
        try
        {
            var picker = new FileSavePicker();
            WindowHelper.InitializeWithMainWindow(picker);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Text File", new[] { ".txt" });
            picker.SuggestedFileName = "Results.txt";

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                OutputFilePath = file.Path;
            }
        }
        catch (Exception ex)
        {
            SetError($"Error selecting output destination: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartProcessing))]
    public async Task StartProcessingAsync()
    {
        HasStatusMessage = false;
        IsSuccess = false;
        IsError = false;

        string inputFile = InputFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(inputFile))
        {
            SetError("Please select or enter an input surveillance data file.");
            return;
        }

        string configFile = ConfigFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configFile))
        {
            SetError("Please select or enter an interpretation configuration file (.json).");
            return;
        }

        string outputFile = OutputFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outputFile))
        {
            SetError("Please specify an output results file destination.");
            return;
        }

        try
        {
            if (!Path.IsPathFullyQualified(inputFile))
                inputFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, inputFile));

            if (!Path.IsPathFullyQualified(configFile))
                configFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configFile));

            if (!Path.IsPathFullyQualified(outputFile))
                outputFile = Path.GetFullPath(outputFile);
        }
        catch (Exception ex)
        {
            SetError($"Invalid file path: {ex.Message}");
            return;
        }

        if (!File.Exists(inputFile))
        {
            SetError($"Input file not found: {inputFile}");
            return;
        }

        if (!File.Exists(configFile))
        {
            SetError($"Configuration file not found: {configFile}");
            return;
        }

        char delimiter = SelectedDelimiter == "TAB" ? Constants.Delimiters.TabChar : SelectedDelimiter[0];
        int guidelineYear = Convert.ToInt32(GuidelineYear);

        // Capture the DispatcherQueue before entering the background thread
        var dispatcherQueue = WindowHelper.MainWindow?.DispatcherQueue;

        IsProcessing = true;
        ProgressPercentage = 0;
        StartProcessingCommand.NotifyCanExecuteChanged();
        CancelProcessingCommand.NotifyCanExecuteChanged();

        await Task.Run(() =>
        {
            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            _worker.ProgressChanged += (s, e) =>
            {
                dispatcherQueue?.TryEnqueue(() =>
                    ProgressPercentage = e.ProgressPercentage);
            };

            var fileArgs = new FileInterpretationParameters(
                inputFile, delimiter, guidelineYear, configFile, outputFile, _worker);
            var doWorkArgs = new DoWorkEventArgs(fileArgs);

            try
            {
                IO_Library.InterpretDataFile(_worker, doWorkArgs);

                dispatcherQueue?.TryEnqueue(() =>
                {
                    if (doWorkArgs.Cancel)
                    {
                        StatusMessage = "Processing was cancelled by user.";
                        IsError = true;
                        StatusInfoBarSeverity = InfoBarSeverity.Warning;
                    }
                    else
                    {
                        StatusMessage = $"Interpretation complete! Output saved to: {outputFile}";
                        IsSuccess = true;
                        StatusInfoBarSeverity = InfoBarSeverity.Success;
                    }
                });
            }
            catch (Exception ex)
            {
                dispatcherQueue?.TryEnqueue(() =>
                {
                    StatusMessage = $"Error processing file: {ex.Message}";
                    IsError = true;
                    StatusInfoBarSeverity = InfoBarSeverity.Error;
                });
            }
            finally
            {
                dispatcherQueue?.TryEnqueue(() =>
                {
                    HasStatusMessage = true;
                    IsProcessing = false;
                    StartProcessingCommand.NotifyCanExecuteChanged();
                    CancelProcessingCommand.NotifyCanExecuteChanged();
                });
            }
        });
    }

    private bool CanStartProcessing() => !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanCancelProcessing))]
    public void CancelProcessing()
    {
        if (_worker != null && _worker.IsBusy)
        {
            _worker.CancelAsync();
            StatusMessage = "Cancelling processing...";
            StatusInfoBarSeverity = InfoBarSeverity.Warning;
            HasStatusMessage = true;
        }
    }

    private bool CanCancelProcessing() => IsProcessing;

    [RelayCommand]
    public void OpenOutputFile()
    {
        try
        {
            if (File.Exists(OutputFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OutputFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    public void OpenOutputFolder()
    {
        try
        {
            string? dir = Path.GetDirectoryName(OutputFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        IsError = true;
        IsSuccess = false;
        StatusInfoBarSeverity = InfoBarSeverity.Error;
        HasStatusMessage = true;
    }
}
