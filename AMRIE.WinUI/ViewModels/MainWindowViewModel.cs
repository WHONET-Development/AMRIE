using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AMRIE.WinUI.Common;
using AMRIE.WinUI.Models;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace AMRIE.WinUI.ViewModels;

public class ComboBoxItemModel
{
    public string DisplayText { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public ComboBoxItemModel() { }
    public ComboBoxItemModel(string displayText, string value)
    {
        DisplayText = displayText;
        Value = value;
    }

    public override bool Equals(object? obj) =>
        obj is ComboBoxItemModel other && Value == other.Value;

    public override int GetHashCode() =>
        Value.GetHashCode();
}

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly Regex MatchCombinationAgents = new(@"/.+$", RegexOptions.Compiled);
    private static readonly string MIC_TestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.MIC;
    private static readonly string DiskTestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.Disk + @"{2}";

    public static readonly ComboBoxItemModel DefaultSelection = new("[Select a value]", string.Empty);

    private BackgroundWorker? _batchWorker;

    #region Top Bar Properties

    [ObservableProperty]
    private bool _restrictGuidelineYear = true;

    [ObservableProperty]
    private double _guidelineYear = Constants.BreakpointTableRevisionYear;

    [ObservableProperty]
    private bool _isProcessing = false;

    #endregion

    #region Whole-Database Tab Properties

    [ObservableProperty]
    private string _inputFilePath = @"Resources\SampleInputFile.txt";

    [ObservableProperty]
    private string _selectedDelimiter = "|";

    [ObservableProperty]
    private string _configFilePath = @"Resources\SampleConfig.json";

    [ObservableProperty]
    private string _outputFilePath = "Results.txt";

    [ObservableProperty]
    private int _progressPercentage = 0;

    [ObservableProperty]
    private string _batchStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasBatchStatus = false;

    [ObservableProperty]
    private InfoBarSeverity _batchSeverity = InfoBarSeverity.Informational;

    public string[] Delimiters { get; } = new[] { "|", "TAB", ",", ";" };

    #endregion

    #region Single Interpretation Tab Properties

    [ObservableProperty]
    private bool _restrictGuidelines = true;

    [ObservableProperty]
    private bool _restrictBreakpointTypes = false;

    [ObservableProperty]
    private bool _restrictSitesOfInfection = false;

    public ObservableCollection<SelectableItem> Guidelines { get; } = new();
    public ObservableCollection<SelectableItem> BreakpointTypes { get; } = new();
    public ObservableCollection<SelectableItem> SitesOfInfection { get; } = new();

    // Organisms
    private List<ComboBoxItemModel> _allOrganisms = new();
    public ObservableCollection<ComboBoxItemModel> FilteredOrganisms { get; } = new();

    [ObservableProperty]
    private string _organismSearchText = string.Empty;

    [ObservableProperty]
    private ComboBoxItemModel? _selectedOrganism;

    // Antibiotics
    private List<ComboBoxItemModel> _allAntibiotics = new();
    public ObservableCollection<ComboBoxItemModel> FilteredAntibiotics { get; } = new();

    [ObservableProperty]
    private string _antibioticSearchText = string.Empty;

    [ObservableProperty]
    private ComboBoxItemModel? _selectedAntibiotic;

    // Test method
    [ObservableProperty]
    private bool _isDiskMethod = true;

    [ObservableProperty]
    private bool _isMicMethod = false;

    public ObservableCollection<string> DiskPotencies { get; } = new();

    [ObservableProperty]
    private string? _selectedDiskPotency;

    // Measurement & Comments
    [ObservableProperty]
    private string _testMeasurement = string.Empty;

    [ObservableProperty]
    private bool _includeComments = false;

    #endregion

    #region Dialog Data

    public event Action<string, string>? ShowMessageRequested;
    public event Action<string, object>? ShowDataGridDialogRequested;

    #endregion

    public MainWindowViewModel()
    {
        InitializeData();
    }

    private void InitializeData()
    {
        // Guidelines (CLSI checked by default)
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.CLSI), true));
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.EUCAST), false));
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.SFM), false));
        foreach (var g in Guidelines)
        {
            g.PropertyChanged += (s, e) => UpdateDiskPotencies();
        }

        // Breakpoint Types
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.Human, false));
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.Animal, false));
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.ECOFF, false));

        // Sites of infection
        foreach (var site in Constants.SitesOfInfection.DefaultOrder)
        {
            SitesOfInfection.Add(new SelectableItem(site, false));
        }

        // Organisms
        try
        {
            _allOrganisms = new List<ComboBoxItemModel> { DefaultSelection };
            _allOrganisms.AddRange(
                Organism.AllOrganisms.OrderBy(o => o.ORGANISM)
                    .Select(o => new ComboBoxItemModel($"{o.ORGANISM} - ({o.WHONET_ORG_CODE})", o.WHONET_ORG_CODE))
                    .Distinct()
            );
        }
        catch
        {
            _allOrganisms = new List<ComboBoxItemModel> { DefaultSelection };
        }
        FilterOrganisms(string.Empty);
        SelectedOrganism = DefaultSelection;

        // Antibiotics
        try
        {
            _allAntibiotics = new List<ComboBoxItemModel> { DefaultSelection };
            _allAntibiotics.AddRange(
                Antibiotic.AllAntibiotics.OrderBy(a => a.ANTIBIOTIC)
                    .Select(a => new ComboBoxItemModel($"{a.ANTIBIOTIC} - ({a.WHONET_ABX_CODE})", a.WHONET_ABX_CODE))
                    .Distinct()
            );
        }
        catch
        {
            _allAntibiotics = new List<ComboBoxItemModel> { DefaultSelection };
        }
        FilterAntibiotics(string.Empty);
        SelectedAntibiotic = DefaultSelection;
    }

    #region Search Filters

    partial void OnOrganismSearchTextChanged(string value)
    {
        // Filtering is now driven by AutoSuggestBox_TextChanged in code-behind
    }

    public void FilterOrganisms(string search)
    {
        FilteredOrganisms.Clear();
        if (string.IsNullOrWhiteSpace(search))
        {
            foreach (var item in _allOrganisms.Skip(1).Take(50)) FilteredOrganisms.Add(item);
        }
        else
        {
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = _allOrganisms.Skip(1)
                .Where(o => terms.All(t => o.DisplayText.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           o.Value.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(50)
                .ToList();
            foreach (var m in matches) FilteredOrganisms.Add(m);
        }
    }

    partial void OnAntibioticSearchTextChanged(string value)
    {
        // Filtering is now driven by AutoSuggestBox_TextChanged in code-behind
    }

    public void FilterAntibiotics(string search)
    {
        FilteredAntibiotics.Clear();
        if (string.IsNullOrWhiteSpace(search))
        {
            foreach (var item in _allAntibiotics.Skip(1).Take(50)) FilteredAntibiotics.Add(item);
        }
        else
        {
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = _allAntibiotics.Skip(1)
                .Where(a => terms.All(t => a.DisplayText.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           a.Value.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(50)
                .ToList();
            foreach (var m in matches) FilteredAntibiotics.Add(m);
        }
    }

    partial void OnSelectedAntibioticChanged(ComboBoxItemModel? value)
    {
        UpdateDiskPotencies();
    }

    partial void OnIsDiskMethodChanged(bool value)
    {
        _isMicMethod = !value;
        OnPropertyChanged(nameof(IsMicMethod));
    }

    partial void OnIsMicMethodChanged(bool value)
    {
        _isDiskMethod = !value;
        OnPropertyChanged(nameof(IsDiskMethod));
    }

    private void UpdateDiskPotencies()
    {
        DiskPotencies.Clear();
        if (SelectedAntibiotic == null || SelectedAntibiotic == DefaultSelection || string.IsNullOrWhiteSpace(SelectedAntibiotic.Value))
            return;

        string abxCode = SelectedAntibiotic.Value;
        var activeGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();

        var potencies = Antibiotic.AllAntibiotics
            .Where(abx => abx.WHONET_ABX_CODE == abxCode &&
                          activeGuidelines.Any(g =>
                              (g == nameof(abx.CLSI) && abx.CLSI) ||
                              (g == nameof(abx.EUCAST) && abx.EUCAST) ||
                              (g == nameof(abx.SFM) && abx.SFM)))
            .Select(abx => abx.POTENCY)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        foreach (var p in potencies)
            DiskPotencies.Add(p);

        if (DiskPotencies.Count > 0)
            SelectedDiskPotency = DiskPotencies[0];
        else
            SelectedDiskPotency = null;
    }

    #endregion

    #region Whole-Database Execution

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
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null) InputFilePath = file.Path;
        }
        catch { }
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
            if (file != null) ConfigFilePath = file.Path;
        }
        catch { }
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
            if (file != null) OutputFilePath = file.Path;
        }
        catch { }
    }

    [RelayCommand]
    public async Task InterpretFileAsync()
    {
        HasBatchStatus = false;

        string inputFile = InputFilePath.Trim();
        if (!Path.IsPathFullyQualified(inputFile))
            inputFile = Path.GetFullPath(Path.Combine(Constants.SystemRootPath ?? AppContext.BaseDirectory, inputFile));

        string configFile = ConfigFilePath.Trim();
        if (!Path.IsPathFullyQualified(configFile))
            configFile = Path.GetFullPath(Path.Combine(Constants.SystemRootPath ?? AppContext.BaseDirectory, configFile));

        string outputFile = OutputFilePath.Trim();
        if (!Path.IsPathFullyQualified(outputFile))
            outputFile = Path.GetFullPath(outputFile);

        // Validate individual fields with specific error messages
        if (string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
        {
            ShowMessageRequested?.Invoke("Information", $"Input file not found: {inputFile}");
            return;
        }

        if (!RestrictGuidelineYear)
        {
            ShowMessageRequested?.Invoke("Information", "Please enable 'Restrict guideline year' and select a year.");
            return;
        }

        if (string.IsNullOrWhiteSpace(configFile) || !File.Exists(configFile))
        {
            ShowMessageRequested?.Invoke("Information", $"Configuration file not found: {configFile}");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFile))
        {
            ShowMessageRequested?.Invoke("Information", "Please specify an output file path.");
            return;
        }

        char delimiter = SelectedDelimiter == "TAB" ? Constants.Delimiters.TabChar : SelectedDelimiter[0];
        int guidelineYear = Convert.ToInt32(GuidelineYear);

        // Capture the DispatcherQueue before entering the background thread
        var dispatcherQueue = WindowHelper.MainWindow?.DispatcherQueue;

        IsProcessing = true;
        ProgressPercentage = 0;

        await Task.Run(() =>
        {
            _batchWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            _batchWorker.ProgressChanged += (s, e) =>
            {
                dispatcherQueue?.TryEnqueue(() =>
                    ProgressPercentage = e.ProgressPercentage);
            };

            var fileArgs = new FileInterpretationParameters(
                inputFile, delimiter, guidelineYear, configFile, outputFile, _batchWorker);
            var doWorkArgs = new DoWorkEventArgs(fileArgs);

            try
            {
                IO_Library.InterpretDataFile(_batchWorker, doWorkArgs);

                dispatcherQueue?.TryEnqueue(() =>
                {
                    if (doWorkArgs.Cancel)
                    {
                        BatchStatusMessage = "Processing cancelled by user.";
                        BatchSeverity = InfoBarSeverity.Warning;
                    }
                    else
                    {
                        BatchStatusMessage = "Interpretation completed.";
                        BatchSeverity = InfoBarSeverity.Success;
                        ShowMessageRequested?.Invoke("Interpretation Completed", $"Interpretation successfully completed!\r\nOutput saved to: {outputFile}");
                    }
                });
            }
            catch (Exception ex)
            {
                dispatcherQueue?.TryEnqueue(() =>
                {
                    BatchStatusMessage = $"Error: {ex.Message}";
                    BatchSeverity = InfoBarSeverity.Error;
                    ShowMessageRequested?.Invoke("Error", ex.Message);
                });
            }
            finally
            {
                dispatcherQueue?.TryEnqueue(() =>
                {
                    HasBatchStatus = true;
                    IsProcessing = false;
                });
            }
        });
    }

    [RelayCommand]
    public void CancelOperation()
    {
        if (_batchWorker != null && _batchWorker.IsBusy)
        {
            _batchWorker.CancelAsync();
        }
    }

    #endregion

    #region Single Interpretation Execution

    [RelayCommand]
    public void GetInterpretations()
    {
        if (!ValidateSelectionsForInterpretation(true))
            return;

        AntibioticSpecificInterpretationRules.ClearBreakpoints();

        var config = new InterpretationConfiguration
        {
            IncludeInterpretationComments = IncludeComments,
            GuidelineYear = Convert.ToInt64(GuidelineYear),
            PrioritizedBreakpointTypes = new List<string>()
        };

        if (RestrictBreakpointTypes)
        {
            foreach (var item in BreakpointTypes.Where(b => b.IsSelected))
                config.PrioritizedBreakpointTypes.Add(item.Name);
        }
        else
        {
            config.PrioritizedBreakpointTypes.Add(Breakpoint.BreakpointTypes.Human);
        }

        if (RestrictSitesOfInfection)
        {
            config.PrioritizedSitesOfInfection = SitesOfInfection.Where(s => s.IsSelected).Select(s => s.Name).ToList();
        }

        string orgCode = SelectedOrganism!.Value;
        List<string> antibioticTestCodes = GetFullTestCodes();

        if (!antibioticTestCodes.Any())
        {
            ShowMessageRequested?.Invoke("Notice", "There was a problem creating the antibiotic code. Please ensure that you have either CLSI or EUCAST checked above.");
            return;
        }

        string measurement = TestMeasurement.Trim();
        if (string.IsNullOrWhiteSpace(measurement))
        {
            ShowMessageRequested?.Invoke("Notice", "Please enter a test measurement.");
            return;
        }

        List<string> interpretations = antibioticTestCodes.Select(fullTestCode =>
        {
            string rawInterpretation = IsolateInterpretation.GetSingleInterpretation(config, orgCode, fullTestCode, measurement);
            if (!config.IncludeInterpretationComments)
                rawInterpretation = IsolateInterpretation.RemoveComments(rawInterpretation);

            return $"{fullTestCode}: {rawInterpretation}";
        }).ToList();

        string resultText = string.Join(Environment.NewLine, interpretations);
        ShowMessageRequested?.Invoke("Interpretation Results", resultText);
    }

    [RelayCommand]
    public void GetApplicableBreakpoints()
    {
        if (!ValidateSelectionsForBreakpoints(true))
            return;

        List<string>? prioritizedGuidelines = null;
        List<int>? prioritizedGuidelineYears = null;
        List<string>? prioritizedBreakpointTypes = null;
        List<string>? prioritizedSitesOfInfection = null;
        string orgCode = SelectedOrganism!.Value;
        List<string>? prioritizedWhonetAbxFullDrugCodes = null;

        if (RestrictGuidelines)
        {
            prioritizedGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        }

        if (RestrictBreakpointTypes)
        {
            prioritizedBreakpointTypes = BreakpointTypes.Where(b => b.IsSelected).Select(b => b.Name).ToList();
        }

        if (RestrictSitesOfInfection)
        {
            prioritizedSitesOfInfection = SitesOfInfection.Where(s => s.IsSelected).Select(s => s.Name).ToList();
        }

        if (RestrictGuidelineYear)
        {
            prioritizedGuidelineYears = new List<int> { Convert.ToInt32(GuidelineYear) };
        }

        if (ValidateAntibioticSelection())
        {
            prioritizedWhonetAbxFullDrugCodes = GetFullTestCodes();

            var applicableBreakpoints = Breakpoint.GetApplicableBreakpoints(
                orgCode,
                new List<Breakpoint>(),
                prioritizedGuidelines: prioritizedGuidelines,
                prioritizedGuidelineYears: prioritizedGuidelineYears,
                prioritizedBreakpointTypes: prioritizedBreakpointTypes,
                prioritizedSitesOfInfection: prioritizedSitesOfInfection,
                prioritizedWhonetAbxFullDrugCodes: prioritizedWhonetAbxFullDrugCodes
            );

            var rows = applicableBreakpoints.Select(b => new BreakpointRow(
                b.GUIDELINES, b.YEAR, b.TEST_METHOD, b.POTENCY, b.ORGANISM_CODE,
                b.BREAKPOINT_TYPE, b.HOST, b.SITE_OF_INFECTION, b.WHONET_ABX_CODE,
                b.WHONET_TEST, b.R, b.I, b.SDD, b.S, b.ECV_ECOFF, b.COMMENTS
            )).ToList();

            ShowDataGridDialogRequested?.Invoke($"Matching breakpoints: {applicableBreakpoints.Count}", rows);
        }
    }

    [RelayCommand]
    public void GetApplicableExpertRules()
    {
        if (!ValidateSelectionsForExpertRules())
        {
            ShowMessageRequested?.Invoke("Notice", "One or more selections is invalid.");
            return;
        }

        string orgCode = SelectedOrganism!.Value;
        string[] abxAndTests = GetFullTestCodes().ToArray();
        List<string> antibioticCodes = abxAndTests.Where(abx => IsolateInterpretation.ValidAntibioticCode.IsMatch(abx)).ToList();
        List<string> otherTestsCodes = abxAndTests.Except(antibioticCodes).ToList();

        var expertRules = ExpertInterpretationRule.GetApplicableExpertRules(
            orgCode, antibioticCodes, otherTestsCodes, ExpertInterpretationRule.RuleCodes.All);

        var rows = expertRules.Select(r => new ExpertRuleRow(
            r.RULE_CODE, r.DESCRIPTION, r.ORGANISM_CODE,
            string.Join(", ", r.AFFECTED_ANTIBIOTICS ?? new List<string>()),
            string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? new List<string>())
        )).ToList();

        ShowDataGridDialogRequested?.Invoke($"Matching expert rules: {expertRules.Count}", rows);
    }

    [RelayCommand]
    public void GetApplicableIntrinsicResistance()
    {
        if (!ValidateCommonSelections(true))
            return;

        string orgCode = SelectedOrganism!.Value;
        List<string>? prioritizedGuidelines = null;

        if (RestrictGuidelines)
        {
            prioritizedGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        }

        var applicableRules = ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(
            orgCode, prioritizedGuidelines: prioritizedGuidelines);

        var rows = applicableRules.Select(r => new IntrinsicRuleRow(
            r.GUIDELINE, r.ORGANISM_CODE, r.ABX_CODE,
            string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? new List<string>()),
            r.COMMENTS
        )).ToList();

        ShowDataGridDialogRequested?.Invoke($"Matching intrinsic resistance rules: {rows.Count}", rows);
    }

    #endregion

    #region Validations & Helpers

    public List<string> GetFullTestCodes()
    {
        var guidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        string selectedAbxCode = SelectedAntibiotic?.Value ?? string.Empty;
        string diskContent = IsDiskMethod && !string.IsNullOrWhiteSpace(SelectedDiskPotency) ? SelectedDiskPotency : string.Empty;

        return guidelines
            .Select(g => CreateFullTestCode(g, selectedAbxCode, IsDiskMethod, diskContent))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
    }

    private string CreateFullTestCode(string guidelineName, string abxCode, bool isDisk, string diskContent = "")
    {
        char guidelineCode = guidelineName switch
        {
            nameof(Antibiotic.GuidelineNames.CLSI) => Antibiotic.GuidelineCodes.CLSI,
            nameof(Antibiotic.GuidelineNames.EUCAST) => Antibiotic.GuidelineCodes.EUCAST,
            nameof(Antibiotic.GuidelineNames.SFM) => Antibiotic.GuidelineCodes.SFM,
            _ => ' '
        };

        if (guidelineCode == ' ') return string.Empty;

        if (isDisk)
        {
            if (!string.IsNullOrWhiteSpace(diskContent))
            {
                diskContent = diskContent.Replace("µg", string.Empty).Replace("units", string.Empty).Replace(".", "_");
                if (MatchCombinationAgents.IsMatch(diskContent))
                    diskContent = MatchCombinationAgents.Replace(diskContent, string.Empty);
                if (diskContent == "1_25") diskContent = "1_2";
            }

            return string.Format(DiskTestFormat, abxCode, guidelineCode, diskContent);
        }
        else
        {
            return string.Format(MIC_TestFormat, abxCode, guidelineCode);
        }
    }

    private bool ValidateCommonSelections(bool organismCodeRequired)
    {
        if (RestrictGuidelines && Guidelines.Count(g => g.IsSelected) == 0)
        {
            ShowMessageRequested?.Invoke("Notice", "You must enable at least one antibiotic guideline.");
            return false;
        }
        if (organismCodeRequired && !ValidateOrganismSelection())
            return false;

        return true;
    }

    private bool ValidateSelectionsForBreakpoints(bool organismCodeRequired)
    {
        if (!ValidateCommonSelections(organismCodeRequired))
            return false;

        if (RestrictBreakpointTypes && BreakpointTypes.Count(b => b.IsSelected) == 0)
        {
            ShowMessageRequested?.Invoke("Notice", "Please choose one or more breakpoint types.");
            return false;
        }
        if (RestrictSitesOfInfection && SitesOfInfection.Count(s => s.IsSelected) == 0)
        {
            ShowMessageRequested?.Invoke("Notice", "Please choose one or more sites of infection.");
            return false;
        }

        return true;
    }

    private bool ValidateSelectionsForInterpretation(bool organismCodeRequired)
    {
        if (!ValidateSelectionsForBreakpoints(organismCodeRequired))
            return false;

        if (!RestrictGuidelineYear)
        {
            ShowMessageRequested?.Invoke("Notice", "Please select a guideline year.");
            return false;
        }

        if (!ValidateAntibioticSelection())
            return false;

        if (!ValidateTestMeasurement())
            return false;

        return true;
    }

    private bool ValidateSelectionsForExpertRules()
    {
        return ValidateOrganismSelection() && ValidateAntibioticSelection();
    }

    private bool ValidateOrganismSelection()
    {
        if (SelectedOrganism != null && SelectedOrganism != DefaultSelection && !string.IsNullOrWhiteSpace(SelectedOrganism.Value))
            return true;

        ShowMessageRequested?.Invoke("Notice", "Please select an organism.");
        return false;
    }

    private bool ValidateAntibioticSelection()
    {
        if (SelectedAntibiotic != null && SelectedAntibiotic != DefaultSelection && !string.IsNullOrWhiteSpace(SelectedAntibiotic.Value))
            return true;

        ShowMessageRequested?.Invoke("Notice", "Please select an antibiotic.");
        return false;
    }

    private bool ValidateTestMeasurement()
    {
        string testMethod = IsDiskMethod ? Antibiotic.TestMethods.Disk : Antibiotic.TestMethods.MIC;
        decimal discardedNum = decimal.Zero;
        string discardedMod = string.Empty;

        if (InterpretationLibrary.ParseResult(testMethod, TestMeasurement, ref discardedNum, ref discardedMod))
            return true;

        ShowMessageRequested?.Invoke("Notice", "The test measurement could not be read. Please verify the input.");
        return false;
    }

    #endregion
}
