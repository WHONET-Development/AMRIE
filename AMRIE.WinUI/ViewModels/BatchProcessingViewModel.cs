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
                await SetInputFileAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            SetError($"Error browsing for input file: {ex.Message}");
        }
    }

    public async Task SetInputFileAsync(string filePath)
    {
        InputFilePath = filePath;

        if (string.IsNullOrWhiteSpace(OutputFilePath))
        {
            string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";
            OutputFilePath = Path.Combine(dir, $"{name}_interpreted{ext}");
        }

        string extension = Path.GetExtension(filePath);
        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            SelectedDelimiter = ",";
        }
        else if (extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            SelectedDelimiter = "TAB";
        }
        else
        {
            // Inspect first line of the file to auto-select delimiter (|, TAB, ,, ;)
            try
            {
                using var reader = new StreamReader(filePath);
                string? firstLine = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    int pipeCount = firstLine.Count(c => c == '|');
                    int tabCount = firstLine.Count(c => c == '\t');
                    int commaCount = firstLine.Count(c => c == ',');
                    int semiCount = firstLine.Count(c => c == ';');

                    int max = Math.Max(pipeCount, Math.Max(tabCount, Math.Max(commaCount, semiCount)));
                    if (max > 0)
                    {
                        if (max == pipeCount) SelectedDelimiter = "|";
                        else if (max == tabCount) SelectedDelimiter = "TAB";
                        else if (max == commaCount) SelectedDelimiter = ",";
                        else if (max == semiCount) SelectedDelimiter = ";";
                    }
                }
            }
            catch { }
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
            SetError("Please select or enter an input data file.");
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
            {
                string localPath = Path.GetFullPath(inputFile);
                inputFile = File.Exists(localPath) ? localPath : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, inputFile));
            }

            if (!Path.IsPathFullyQualified(configFile))
            {
                string localConfig = Path.GetFullPath(configFile);
                configFile = File.Exists(localConfig) ? localConfig : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configFile));
            }

            if (!Path.IsPathFullyQualified(outputFile))
                outputFile = Path.GetFullPath(outputFile);

            string? outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
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

        if (string.IsNullOrEmpty(SelectedDelimiter))
        {
            SetError("Please select a column delimiter.");
            return;
        }
        char delimiter = SelectedDelimiter == "TAB" ? Constants.Delimiters.TabChar : SelectedDelimiter[0];
        int guidelineYear = Convert.ToInt32(GuidelineYear);

        var dispatcherQueue = WindowHelper.MainWindow?.DispatcherQueue;

        _worker = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };
        _worker.ProgressChanged += (s, e) =>
        {
            int p = e.ProgressPercentage;
            if (dispatcherQueue != null)
            {
                dispatcherQueue.TryEnqueue(() => ProgressPercentage = p);
            }
            else
            {
                ProgressPercentage = p;
            }
        };

        IsProcessing = true;
        ProgressPercentage = 0;
        StartProcessingCommand.NotifyCanExecuteChanged();
        CancelProcessingCommand.NotifyCanExecuteChanged();

        // Snapshot fields so the closure is safe across threads.
        var worker = _worker;
        var fileArgs = new FileInterpretationParameters(
            inputFile, delimiter, guidelineYear, configFile, outputFile, worker);
        var doWorkArgs = new DoWorkEventArgs(fileArgs);

        await Task.Run(() =>
        {
            try
            {
                IO_Library.InterpretDataFile(worker, doWorkArgs);

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
                string message;
                if (ex is AggregateException agg && agg.InnerExceptions.Count > 0)
                {
                    var details = agg.InnerExceptions.Select(ie =>
                    {
                        string typeName = ie.GetType().Name;
                        string desc = string.IsNullOrEmpty(ie.Message)
                            ? (ie is System.Runtime.InteropServices.COMException comEx ? $"0x{comEx.HResult:X8}" : string.Empty)
                            : ie.Message;
                        return string.IsNullOrEmpty(desc) ? typeName : $"{typeName}: {desc}";
                    });
                    message = string.Join("; ", details);
                }
                else
                {
                    string inner = ex.InnerException != null ? $" ({ex.InnerException.GetType().Name}: {ex.InnerException.Message})" : string.Empty;
                    string baseMsg = string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message;
                    message = $"{baseMsg}{inner}";
                }

                dispatcherQueue?.TryEnqueue(() =>
                {
                    StatusMessage = $"Error processing file: {message}";
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
                    _worker = null;  // Clear so CancelProcessing can't target a stale worker.
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
