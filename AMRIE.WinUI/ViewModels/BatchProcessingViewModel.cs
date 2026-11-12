using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using AMRIE.Library;
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
    public partial string InputFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfigFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedDelimiter { get; set; } = "TAB";

    [ObservableProperty]
    public partial double GuidelineYear { get; set; } = Constants.BreakpointTableRevisionYear;

    [ObservableProperty]
    public partial bool RestrictGuidelineYear { get; set; } = true;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    [ObservableProperty]
    public partial int ProgressPercentage { get; set; } = 0;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasStatusMessage { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSuccess { get; set; } = false;

    [ObservableProperty]
    public partial bool IsError { get; set; } = false;

    [ObservableProperty]
    public partial InfoBarSeverity StatusInfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial string BatchSummaryText { get; set; } = string.Empty;

    public bool IsProgressIndeterminate => IsProcessing && ProgressPercentage == 0;

    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(IsProgressIndeterminate));
    partial void OnProgressPercentageChanged(int value) => OnPropertyChanged(nameof(IsProgressIndeterminate));

    public string ResolveConfigFilePath()
    {
        string configFile = ConfigFilePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configFile))
            return string.Empty;
        if (Path.IsPathFullyQualified(configFile))
            return configFile;

        string localConfig = Path.GetFullPath(configFile);
        return File.Exists(localConfig)
            ? localConfig
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configFile));
    }

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
        BatchSummaryText = string.Empty;

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
                var sw = System.Diagnostics.Stopwatch.StartNew();
                IO_Library.InterpretDataFileBounded(worker, doWorkArgs);
                sw.Stop();

                string summary = string.Empty;
                if (!doWorkArgs.Cancel && File.Exists(outputFile))
                {
                    summary = BuildCompletionSummary(outputFile, sw.Elapsed);
                }

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
                        BatchSummaryText = summary;
                        StatusMessage = string.IsNullOrEmpty(summary)
                            ? $"Interpretation complete! Output saved to: {outputFile}"
                            : $"Interpretation complete ({summary}). Saved to: {outputFile}";
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
        if (_worker != null && IsProcessing)
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
    public async Task LoadSampleAsync()
    {
        string sampleInput = Path.Combine(AppContext.BaseDirectory, "Resources", "SampleInputFile.txt");
        if (File.Exists(sampleInput))
        {
            await SetInputFileAsync(sampleInput);

            // Ensure the sample output destination is in the user's Documents folder to avoid permissions issues
            string docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OutputFilePath = Path.Combine(docsFolder, "SampleInputFile_interpreted.txt");
        }

        string sampleConfig = Path.Combine(AppContext.BaseDirectory, "Resources", "SampleConfig.json");
        if (File.Exists(sampleConfig))
        {
            ConfigFilePath = sampleConfig;
        }

        StatusMessage = "Loaded sample input data and configuration parameters.";
        HasStatusMessage = true;
        IsSuccess = false;
        IsError = false;
        StatusInfoBarSeverity = InfoBarSeverity.Informational;
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
        BatchSummaryText = string.Empty;
        StatusInfoBarSeverity = InfoBarSeverity.Error;
        HasStatusMessage = true;
    }

    private static string BuildCompletionSummary(string outputFile, TimeSpan elapsed)
    {
        try
        {
            long bytes = new FileInfo(outputFile).Length;
            long lines = 0;
            using (var reader = new StreamReader(outputFile))
            {
                while (reader.ReadLine() != null) lines++;
            }
            long records = Math.Max(0, lines - 1);
            string sizeLabel = bytes >= 1024 * 1024
                ? $"{bytes / (1024.0 * 1024.0):F1} MB"
                : $"{Math.Max(1, bytes / 1024.0):F1} KB";
            string timeLabel = elapsed.TotalSeconds >= 1.0
                ? $"{elapsed.TotalSeconds:F2} s"
                : $"{elapsed.TotalMilliseconds:F0} ms";
            return $"{records:N0} isolates in {timeLabel} • {sizeLabel}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
