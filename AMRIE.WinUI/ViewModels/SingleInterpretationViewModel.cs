using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AMRIE.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AMRIE.WinUI.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.ViewModels;

public partial class SingleInterpretationViewModel : ObservableObject
{
    private static readonly Regex MatchCombinationAgents = new(@"/.+$", RegexOptions.Compiled);
    private static readonly string MIC_TestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.MIC;
    private static readonly string DiskTestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.Disk + @"{2}";

    // Guideline Year
    [ObservableProperty]
    public partial bool RestrictGuidelineYear { get; set; } = true;

    [ObservableProperty]
    public partial int GuidelineYear { get; set; } = Constants.BreakpointTableRevisionYear;

    public ObservableCollection<int> AvailableYears { get; } = [];

    // Filters
    [ObservableProperty]
    public partial bool RestrictGuidelines { get; set; } = true;

    [ObservableProperty]
    public partial bool RestrictBreakpointTypes { get; set; } = true;

    [ObservableProperty]
    public partial bool RestrictSitesOfInfection { get; set; } = false;

    public ObservableCollection<SelectableItem> Guidelines { get; } = [];
    public ObservableCollection<SelectableItem> BreakpointTypes { get; } = [];
    public ObservableCollection<SelectableItem> SitesOfInfection { get; } = [];

    // Organisms & Antibiotics
    public List<OrganismItem> AllOrganisms { get; } = [];
    public List<AntibioticItem> AllAntibiotics { get; } = [];

    [ObservableProperty]
    public partial OrganismItem? SelectedOrganism { get; set; }

    [ObservableProperty]
    public partial string OrganismSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial AntibioticItem? SelectedAntibiotic { get; set; }

    [ObservableProperty]
    public partial string AntibioticSearchText { get; set; } = string.Empty;

    // Test Method (Disk vs MIC)
    // Single source of truth: one private field drives both public booleans.
    // This avoids the fragile pattern of each partial void hook writing the other's backing field.
    private string _testMethod = Antibiotic.TestMethods.Disk;

    public bool IsDiskMethod
    {
        get => _testMethod == Antibiotic.TestMethods.Disk;
        set
        {
            if (value && _testMethod != Antibiotic.TestMethods.Disk)
            {
                _testMethod = Antibiotic.TestMethods.Disk;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMicMethod));
            }
        }
    }

    public bool IsMicMethod
    {
        get => _testMethod == Antibiotic.TestMethods.MIC;
        set
        {
            if (value && _testMethod != Antibiotic.TestMethods.MIC)
            {
                _testMethod = Antibiotic.TestMethods.MIC;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDiskMethod));
            }
        }
    }

    public ObservableCollection<string> DiskPotencies { get; } = [];

    [ObservableProperty]
    public partial string? SelectedDiskPotency { get; set; }

    // Measurement & Engine Options
    [ObservableProperty]
    public partial string Measurement { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeComments { get; set; } = true;

    [ObservableProperty]
    public partial bool RoundHalfDilutions { get; set; } = true;

    [ObservableProperty]
    public partial bool UseIntrinsicResistanceRules { get; set; } = true;

    // Status & Results
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasStatusMessage { get; set; } = false;

    [ObservableProperty]
    public partial bool IsStatusError { get; set; } = false;

    [ObservableProperty]
    public partial InfoBarSeverity StatusInfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    public ObservableCollection<InterpretationResultItem> Results { get; } = [];

    private Task? _initializationTask;

    public SingleInterpretationViewModel()
    {
        InitializeData();
    }

    /// <summary>
    /// Ensures reference tables (breakpoints, organisms, antibiotics) have finished loading asynchronously.
    /// </summary>
    public Task EnsureDataLoadedAsync() => _initializationTask ?? Task.CompletedTask;

    private void InitializeData()
    {
        // Seed initial default year synchronously so the UI renders immediately without waiting for file I/O.
        AvailableYears.Add(Constants.BreakpointTableRevisionYear);
        GuidelineYear = Constants.BreakpointTableRevisionYear;

        // Guidelines
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.CLSI), true));
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.EUCAST), false));
        Guidelines.Add(new SelectableItem(nameof(Antibiotic.GuidelineNames.SFM), false));
        foreach (var g in Guidelines)
        {
            g.PropertyChanged += (s, e) => UpdateDiskPotencies();
        }

        // Breakpoint types
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.Human, true));
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.Animal, false));
        BreakpointTypes.Add(new SelectableItem(Breakpoint.BreakpointTypes.ECOFF, false));

        // Sites of infection
        foreach (var site in Constants.SitesOfInfection.DefaultOrder)
        {
            SitesOfInfection.Add(new SelectableItem(site, false));
        }

        _initializationTask = LoadReferenceDataAsync();
    }

    private async Task LoadReferenceDataAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        var (years, orgs, abxs) = await Task.Run(() =>
        {
            List<int> loadedYears = [];
            try
            {
                loadedYears = [.. Breakpoint.Breakpoints
                    .Select(b => b.YEAR)
                    .Distinct()
                    .OrderByDescending(y => y)];
            }
            catch { }

            List<OrganismItem> loadedOrgs = [];
            try
            {
                // Anonymous type projection relies on compiler-generated Equals/GetHashCode value equality for Distinct().
                loadedOrgs = [.. Organism.AllOrganisms
                    .OrderBy(o => o.ORGANISM)
                    .Select(o => new { Display = $"{o.ORGANISM} - ({o.WHONET_ORG_CODE})", Code = o.WHONET_ORG_CODE })
                    .Distinct()
                    .Select(o => new OrganismItem(o.Display, o.Code))];
            }
            catch { }

            List<AntibioticItem> loadedAbxs = [];
            try
            {
                loadedAbxs = [.. Antibiotic.AllAntibiotics
                    .OrderBy(a => a.ANTIBIOTIC)
                    .Select(a => new { Display = $"{a.ANTIBIOTIC} - ({a.WHONET_ABX_CODE})", Code = a.WHONET_ABX_CODE })
                    .Distinct()
                    .Select(a => new AntibioticItem(a.Display, a.Code))];
            }
            catch { }

            return (loadedYears, loadedOrgs, loadedAbxs);
        }).ConfigureAwait(false);

        void ApplyLoadedData()
        {
            if (years.Count > 0)
            {
                for (int i = 0; i < years.Count; i++)
                {
                    int y = years[i];
                    if (!AvailableYears.Contains(y))
                    {
                        AvailableYears.Insert(Math.Min(i, AvailableYears.Count), y);
                    }
                }

                if (!AvailableYears.Contains(GuidelineYear))
                {
                    GuidelineYear = AvailableYears.Contains(Constants.BreakpointTableRevisionYear)
                        ? Constants.BreakpointTableRevisionYear
                        : AvailableYears[0];
                }
            }

            AllOrganisms.Clear();
            AllOrganisms.AddRange(orgs);

            AllAntibiotics.Clear();
            AllAntibiotics.AddRange(abxs);

            if (SelectedAntibiotic != null && DiskPotencies.Count == 0)
            {
                UpdateDiskPotencies();
            }
        }

        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource();
            if (!dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        ApplyLoadedData();
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }))
            {
                tcs.TrySetResult();
            }
            await tcs.Task.ConfigureAwait(false);
        }
        else
        {
            ApplyLoadedData();
        }
    }

    private int _statusVersion = 0;

    public void ClearResults()
    {
        _statusVersion++;
        Results.Clear();
        HasStatusMessage = false;
        StatusMessage = string.Empty;
        IsStatusError = false;
    }

    private async void AutoDismissSuccessMessageAsync()
    {
        int version = ++_statusVersion;
        await Task.Delay(4000);
        if (_statusVersion == version && StatusInfoBarSeverity == InfoBarSeverity.Success)
        {
            HasStatusMessage = false;
        }
    }

    partial void OnMeasurementChanged(string value)
    {
        ClearResults();
    }

    partial void OnOrganismSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ClearResults();
        }
    }

    partial void OnAntibioticSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ClearResults();
        }
    }

    partial void OnSelectedOrganismChanged(OrganismItem? value)
    {
        if (value == null && string.IsNullOrWhiteSpace(OrganismSearchText))
        {
            ClearResults();
        }
    }

    partial void OnSelectedAntibioticChanged(AntibioticItem? value)
    {
        UpdateDiskPotencies();
        if (value == null && string.IsNullOrWhiteSpace(AntibioticSearchText))
        {
            ClearResults();
        }
    }


    private void UpdateDiskPotencies()
    {
        DiskPotencies.Clear();
        if (SelectedAntibiotic == null)
        {
            SelectedDiskPotency = null;
            return;
        }

        var activeGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        var potencies = Antibiotic.AllAntibiotics
            .Where(abx => abx.WHONET_ABX_CODE == SelectedAntibiotic.Code &&
                          activeGuidelines.Any(g =>
                              (g == nameof(abx.CLSI) && abx.CLSI) ||
                              (g == nameof(abx.EUCAST) && abx.EUCAST) ||
                              (g == nameof(abx.SFM) && abx.SFM)))
            .Select(abx => abx.POTENCY)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        foreach (var p in potencies)
        {
            DiskPotencies.Add(p);
        }

        if (DiskPotencies.Count > 0)
        {
            SelectedDiskPotency = DiskPotencies[0];
        }
        else
        {
            SelectedDiskPotency = null;
        }
    }

    public OrganismItem? ResolveOrganism(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        // If format is "Name - (CODE)", extract CODE
        int pOpen = text.LastIndexOf('(');
        int pClose = text.LastIndexOf(')');
        string codeCandidate = text;
        if (pOpen >= 0 && pClose > pOpen)
        {
            codeCandidate = text.Substring(pOpen + 1, pClose - pOpen - 1).Trim();
        }

        // 1. Direct match on code candidate or text in AllOrganisms
        var match = AllOrganisms.FirstOrDefault(o => o.Code.Equals(codeCandidate, StringComparison.OrdinalIgnoreCase))
            ?? AllOrganisms.FirstOrDefault(o => o.Code.Equals(text, StringComparison.OrdinalIgnoreCase))
            ?? AllOrganisms.FirstOrDefault(o => o.DisplayName.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        // 2. StartsWith match in AllOrganisms
        match = AllOrganisms.FirstOrDefault(o => o.DisplayName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                                                 o.Code.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 3. Contains match in AllOrganisms
        match = AllOrganisms.FirstOrDefault(o => o.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 4. Fallback against engine's Organism.CurrentOrganisms / Organism.AllOrganisms
        var engineOrg = Organism.CurrentOrganisms.Values.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(codeCandidate, StringComparison.OrdinalIgnoreCase))
            ?? Organism.AllOrganisms.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(codeCandidate, StringComparison.OrdinalIgnoreCase) ||
                                                         o.ORGANISM.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (engineOrg != null)
        {
            return new OrganismItem($"{engineOrg.ORGANISM} - ({engineOrg.WHONET_ORG_CODE})", engineOrg.WHONET_ORG_CODE);
        }

        // 5. If text is non-empty code, return raw item
        return new OrganismItem(text, text);
    }

    public AntibioticItem? ResolveAntibiotic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        int pOpen = text.LastIndexOf('(');
        int pClose = text.LastIndexOf(')');
        string codeCandidate = text;
        if (pOpen >= 0 && pClose > pOpen)
        {
            codeCandidate = text.Substring(pOpen + 1, pClose - pOpen - 1).Trim();
        }

        var match = AllAntibiotics.FirstOrDefault(a => a.Code.Equals(codeCandidate, StringComparison.OrdinalIgnoreCase))
            ?? AllAntibiotics.FirstOrDefault(a => a.Code.Equals(text, StringComparison.OrdinalIgnoreCase))
            ?? AllAntibiotics.FirstOrDefault(a => a.DisplayName.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (match != null) return match;

        match = AllAntibiotics.FirstOrDefault(a => a.DisplayName.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                                                  a.Code.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = AllAntibiotics.FirstOrDefault(a => a.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        var engineAbx = Antibiotic.AllAntibiotics.FirstOrDefault(a => a.WHONET_ABX_CODE.Equals(codeCandidate, StringComparison.OrdinalIgnoreCase) ||
                                                                      a.ANTIBIOTIC.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (engineAbx != null)
        {
            return new AntibioticItem($"{engineAbx.ANTIBIOTIC} - ({engineAbx.WHONET_ABX_CODE})", engineAbx.WHONET_ABX_CODE);
        }

        return new AntibioticItem(text, text);
    }

    public void SyncInputs(string? organismText, string? antibioticText)
    {
        if (!string.IsNullOrWhiteSpace(organismText))
        {
            string trimmed = organismText.Trim();
            if (SelectedOrganism == null ||
                (!trimmed.Equals(SelectedOrganism.Code, StringComparison.OrdinalIgnoreCase) &&
                 !trimmed.Equals(SelectedOrganism.DisplayName, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedOrganism = ResolveOrganism(trimmed);
            }
        }

        if (!string.IsNullOrWhiteSpace(antibioticText))
        {
            string trimmed = antibioticText.Trim();
            if (SelectedAntibiotic == null ||
                (!trimmed.Equals(SelectedAntibiotic.Code, StringComparison.OrdinalIgnoreCase) &&
                 !trimmed.Equals(SelectedAntibiotic.DisplayName, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedAntibiotic = ResolveAntibiotic(trimmed);
            }
        }
    }

    [RelayCommand]
    public async Task InterpretAsync()
    {
        HasStatusMessage = false;
        Results.Clear();

        await EnsureDataLoadedAsync();

        if (SelectedOrganism == null && !string.IsNullOrWhiteSpace(OrganismSearchText))
            SelectedOrganism = ResolveOrganism(OrganismSearchText);
        if (SelectedAntibiotic == null && !string.IsNullOrWhiteSpace(AntibioticSearchText))
            SelectedAntibiotic = ResolveAntibiotic(AntibioticSearchText);

        if (SelectedOrganism == null)
        {
            SetError("Please select a microorganism.");
            return;
        }

        if (SelectedAntibiotic == null)
        {
            SetError("Please select an antibiotic.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Measurement))
        {
            SetError("Please enter a test measurement (e.g. 22, <=0.5, or >16).");
            return;
        }

        string testMethod = _testMethod;
        decimal discardedNum = decimal.Zero;
        string discardedMod = string.Empty;

        if (!InterpretationLibrary.ParseResult(testMethod, Measurement.Trim(), ref discardedNum, ref discardedMod))
        {
            if (IsDiskMethod)
            {
                SetError("The disk measurement could not be parsed. Disk zone diameters must be an integer between 6 and 80 mm (e.g. 18, 22).");
            }
            else
            {
                SetError("The MIC measurement could not be parsed. Please enter a valid MIC (e.g. 0.5, <=1, >16).");
            }
            return;
        }

        List<string> fullTestCodes = GetFullTestCodes();
        if (fullTestCodes.Count == 0)
        {
            SetError("Please ensure at least one guideline (CLSI, EUCAST, SFM) is checked.");
            return;
        }

        // Build config
        var config = new InterpretationConfiguration
        {
            RoundHalfDilutions = RoundHalfDilutions,
            IncludeInterpretationComments = IncludeComments,
            UseIntrinsicResistanceRules = UseIntrinsicResistanceRules,
            GuidelineYear = RestrictGuidelineYear ? Convert.ToInt64(GuidelineYear) : 0,
            PrioritizedBreakpointTypes = []
        };

        if (RestrictBreakpointTypes)
        {
            foreach (var bt in BreakpointTypes.Where(b => b.IsSelected))
                config.PrioritizedBreakpointTypes.Add(bt.Name);
        }
        else
        {
            config.PrioritizedBreakpointTypes.Add(Breakpoint.BreakpointTypes.Human);
        }

        if (RestrictSitesOfInfection)
        {
            config.PrioritizedSitesOfInfection = [.. SitesOfInfection.Where(s => s.IsSelected).Select(s => s.Name)];
        }

        // Capture immutable locals needed in Task.Run before leaving the UI thread.
        var organism = SelectedOrganism;
        var measurement = Measurement.Trim();
        var testCodes = fullTestCodes;
        var includeComments = IncludeComments;
        var capturedConfig = config;

        var results = await Task.Run(() =>
        {
            var items = new List<InterpretationResultItem>();
            foreach (var testCode in testCodes)
            {
                try
                {
                    string rawInterpretation = IsolateInterpretation.GetSingleInterpretation(
                        capturedConfig, organism.Code, testCode, measurement);

                    // The badge is the first whitespace-delimited token (e.g. "S", "R!", "SDD").
                    string badge = ExtractBadge(rawInterpretation);

                    // cleanInterpretation strips comment markers (*!/?) when the user has opted out.
                    string cleanInterpretation = includeComments
                        ? rawInterpretation
                        : IsolateInterpretation.RemoveComments(rawInterpretation);

                    string severity = badge switch
                    {
                        "S" => "Success",
                        "I" or "SDD" => "Warning",
                        "R" => "Error",
                        _ => "Informational"
                    };

                    items.Add(new InterpretationResultItem
                    {
                        TestCode = testCode,
                        Interpretation = cleanInterpretation,
                        ResultBadge = badge,
                        StatusSeverity = severity
                    });
                }
                catch (Exception ex)
                {
                    items.Add(new InterpretationResultItem
                    {
                        TestCode = testCode,
                        Interpretation = $"Error: {ex.Message}",
                        ResultBadge = "ERR",
                        StatusSeverity = "Error"
                    });
                }
            }
            return items;
        });

        foreach (var item in results)
            Results.Add(item);

        if (Results.Count == 0)
        {
            SetError("No interpretation could be determined with the selected criteria.");
        }
        else
        {
            HasStatusMessage = true;
            IsStatusError = false;
            StatusInfoBarSeverity = InfoBarSeverity.Success;
            StatusMessage = $"Calculated {Results.Count} interpretation(s).";
            AutoDismissSuccessMessageAsync();
        }
    }

    private static string ExtractBadge(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "?";
        var parts = raw.Split(' ', ';', '(', '\r', '\n');
        return parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? raw;
    }

    public List<string> GetFullTestCodes()
    {
        if (SelectedAntibiotic == null) return [];

        var selectedGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        return [.. selectedGuidelines
            .Select(g => CreateFullTestCode(g, SelectedAntibiotic.Code, IsDiskMethod, SelectedDiskPotency ?? string.Empty))
            .Where(code => !string.IsNullOrWhiteSpace(code))];
    }

    private string CreateFullTestCode(string guidelineName, string abxCode, bool isDisk, string diskContent)
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
                diskContent = diskContent
                    .Replace("µg", string.Empty)
                    .Replace("units", string.Empty)
                    .Replace(".", "_");

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

    [RelayCommand]
    public async Task LoadExampleAsync()
    {
        await EnsureDataLoadedAsync();

        var clsi = Guidelines.FirstOrDefault(g => g.Name.Equals(nameof(Antibiotic.GuidelineNames.CLSI), StringComparison.OrdinalIgnoreCase));
        if (clsi != null)
            clsi.IsSelected = true;

        var spn = AllOrganisms.FirstOrDefault(o => o.Code.Equals("spn", StringComparison.OrdinalIgnoreCase))
                  ?? AllOrganisms.FirstOrDefault(o => o.DisplayName.Contains("pneumoniae", StringComparison.OrdinalIgnoreCase));
        if (spn != null)
        {
            SelectedOrganism = spn;
            OrganismSearchText = spn.DisplayName;
        }

        var amc = AllAntibiotics.FirstOrDefault(a => a.Code.Equals("AMC", StringComparison.OrdinalIgnoreCase))
                  ?? AllAntibiotics.FirstOrDefault(a => a.DisplayName.Contains("Amoxicillin/Clavulanic", StringComparison.OrdinalIgnoreCase));
        if (amc != null)
        {
            SelectedAntibiotic = amc;
            AntibioticSearchText = amc.DisplayName;
        }

        IsMicMethod = true;
        Measurement = "2";
        ClearResults();
    }

    [RelayCommand]
    public void ClearInputs()
    {
        SelectedOrganism = null;
        OrganismSearchText = string.Empty;
        SelectedAntibiotic = null;
        AntibioticSearchText = string.Empty;
        Measurement = string.Empty;
        DiskPotencies.Clear();
        SelectedDiskPotency = null;
        ClearResults();
    }

    [RelayCommand]
    public void CopyResults()
    {
        if (Results.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Organism: {SelectedOrganism?.DisplayName ?? OrganismSearchText}");
        sb.AppendLine($"Antibiotic: {SelectedAntibiotic?.DisplayName ?? AntibioticSearchText}");
        sb.AppendLine($"Method: {(IsDiskMethod ? $"Disk ({SelectedDiskPotency})" : "MIC")}");
        sb.AppendLine($"Measurement: {Measurement}");
        sb.AppendLine();
        sb.AppendLine("Test Code\tResult\tDetails");
        foreach (var r in Results)
        {
            sb.AppendLine($"{r.TestCode}\t{r.ResultBadge}\t{r.Interpretation}");
        }

        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(sb.ToString());
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

        StatusMessage = "Interpretation results copied to clipboard.";
        HasStatusMessage = true;
        IsStatusError = false;
        StatusInfoBarSeverity = InfoBarSeverity.Success;
        AutoDismissSuccessMessageAsync();
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        IsStatusError = true;
        StatusInfoBarSeverity = InfoBarSeverity.Error;
        HasStatusMessage = true;
    }

    private static Dictionary<string, string>? _cachedOrganismNames;
    private static Dictionary<string, string> GetOrganismNameLookup()
    {
        if (_cachedOrganismNames != null) return _cachedOrganismNames;
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var org in Organism.AllOrganisms)
            {
                if (!string.IsNullOrWhiteSpace(org.WHONET_ORG_CODE) && !string.IsNullOrWhiteSpace(org.ORGANISM))
                {
                    lookup.TryAdd(org.WHONET_ORG_CODE, org.ORGANISM);
                }
                if (!string.IsNullOrWhiteSpace(org.FAMILY_CODE) && !string.IsNullOrWhiteSpace(org.FAMILY))
                {
                    lookup.TryAdd(org.FAMILY_CODE, $"{org.FAMILY} (Family)");
                }
                else if (!string.IsNullOrWhiteSpace(org.FAMILY_CODE) && !string.IsNullOrWhiteSpace(org.ORDER))
                {
                    lookup.TryAdd(org.FAMILY_CODE, $"{org.ORDER} (Order)");
                }
                if (!string.IsNullOrWhiteSpace(org.GENUS_CODE) && !string.IsNullOrWhiteSpace(org.GENUS))
                {
                    lookup.TryAdd(org.GENUS_CODE, $"{org.GENUS} (Genus)");
                }
            }
            lookup.TryAdd("ANA", "Anaerobes");
            lookup.TryAdd("ALL", "All Microorganisms");
        }
        catch { }
        _cachedOrganismNames = lookup;
        return lookup;
    }

    private static Dictionary<string, string>? _cachedAntibioticNames;

    private static Dictionary<string, string> GetAntibioticNameLookup()
    {
        if (_cachedAntibioticNames != null) return _cachedAntibioticNames;
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var abx in Antibiotic.AllAntibiotics)
            {
                if (!string.IsNullOrWhiteSpace(abx.WHONET_ABX_CODE) && !string.IsNullOrWhiteSpace(abx.ANTIBIOTIC))
                    lookup.TryAdd(abx.WHONET_ABX_CODE, abx.ANTIBIOTIC);
            }
        }
        catch { }
        _cachedAntibioticNames = lookup;
        return lookup;
    }

    private static string ResolveAbxName(string code, Dictionary<string, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return lookup.TryGetValue(code, out var name) ? name : code;
    }
    private static string ResolveOrgName(string code, Dictionary<string, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return lookup.TryGetValue(code, out var name) ? name : code;
    }

    public (List<BreakpointRow> Rows, string Summary) GetApplicableBreakpoints()
    {
        if (SelectedOrganism == null && !string.IsNullOrWhiteSpace(OrganismSearchText))
            SelectedOrganism = ResolveOrganism(OrganismSearchText);
        if (SelectedAntibiotic == null && !string.IsNullOrWhiteSpace(AntibioticSearchText))
            SelectedAntibiotic = ResolveAntibiotic(AntibioticSearchText);

        var lookup = GetOrganismNameLookup();
        var abxLookup = GetAntibioticNameLookup();
        string? orgCode = SelectedOrganism?.Code;
        string? abxCode = SelectedAntibiotic?.Code;

        var activeGuidelines = RestrictGuidelines ? Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        int? activeYear = RestrictGuidelineYear ? Convert.ToInt32(GuidelineYear) : null;
        var activeTypes = RestrictBreakpointTypes ? BreakpointTypes.Where(b => b.IsSelected).Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var activeSites = RestrictSitesOfInfection ? SitesOfInfection.Where(s => s.IsSelected).Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        string activeMethod = IsDiskMethod ? "DISK" : "MIC";

        Organism? organismEntity = null;
        if (!string.IsNullOrWhiteSpace(orgCode))
        {
            if (Organism.CurrentOrganisms.TryGetValue(orgCode, out var foundOrg))
            {
                organismEntity = foundOrg;
            }
            else if (Organism.MergedOrganisms.TryGetValue(orgCode, out var mergedCode) && Organism.CurrentOrganisms.TryGetValue(mergedCode, out var mergedOrg))
            {
                organismEntity = mergedOrg;
            }
            else
            {
                organismEntity = Organism.CurrentOrganisms.Values.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase))
                    ?? Organism.AllOrganisms.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        // InterpretAsync uses Human breakpoints only when type restriction is disabled.
        var typeFilter = activeTypes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Breakpoint.BreakpointTypes.Human };

        IEnumerable<Breakpoint> query;

        if (organismEntity != null)
        {
            // Reuse the engine so superseded parent-group breakpoints are dropped as in interpretation.
            query = Breakpoint.GetApplicableBreakpoints(
                organismEntity.WHONET_ORG_CODE,
                [],
                prioritizedGuidelines: activeGuidelines?.ToList(),
                prioritizedGuidelineYears: activeYear.HasValue ? [activeYear.Value] : null,
                prioritizedBreakpointTypes: [.. typeFilter],
                prioritizedSitesOfInfection: activeSites?.ToList());
        }
        else
        {
            query = Breakpoint.Breakpoints.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(orgCode))
            {
                query = query.Where(b => b.ORGANISM_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(abxCode))
        {
            query = query.Where(b =>
                b.WHONET_ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase) ||
                b.WHONET_TEST.StartsWith(abxCode + "_", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(activeMethod))
        {
            query = query.Where(b => b.TEST_METHOD.Equals(activeMethod, StringComparison.OrdinalIgnoreCase));
        }

        if (IsDiskMethod && !string.IsNullOrWhiteSpace(SelectedDiskPotency) && !string.IsNullOrWhiteSpace(abxCode))
        {
            string cleanPotency = SelectedDiskPotency.Replace(" ", "").Replace("µg", "ug");
            query = query.Where(b => string.IsNullOrWhiteSpace(b.POTENCY) ||
                                     b.POTENCY.Replace(" ", "").Replace("µg", "ug").Equals(cleanPotency, StringComparison.OrdinalIgnoreCase));
        }

        if (activeGuidelines != null)
        {
            query = query.Where(b => activeGuidelines.Contains(b.GUIDELINES));
        }

        if (activeYear.HasValue)
        {
            query = query.Where(b => b.YEAR == activeYear.Value);
        }

        query = query.Where(b => typeFilter.Contains(b.BREAKPOINT_TYPE));

        if (activeSites != null)
        {
            // Breakpoints store blank sites as an empty string, while the UI uses the "(Blank)" constant.
            query = query.Where(b => b.SITES_OF_INFECTION.Any(s =>
                activeSites.Contains(string.IsNullOrWhiteSpace(s) ? Constants.SitesOfInfection.Blank : s)));
        }

        var list = query
            .Select(b => new BreakpointRow(
                b.GUIDELINES,
                b.YEAR,
                b.TEST_METHOD,
                b.POTENCY,
                b.ORGANISM_CODE,
                ResolveOrgName(b.ORGANISM_CODE, lookup),
                b.BREAKPOINT_TYPE,
                b.HOST,
                b.SITE_OF_INFECTION,
                b.WHONET_ABX_CODE,
                ResolveAbxName(b.WHONET_ABX_CODE, abxLookup),
                b.WHONET_TEST,
                b.R,
                b.I,
                b.SDD,
                b.S,
                b.ECV_ECOFF,
                b.COMMENTS,
                b.ORGANISM_CODE_TYPE,
                b.REFERENCE_TABLE
            ))
            .OrderByDescending(r => r.Year)
            .ThenBy(r => r.Guidelines)
            .ThenBy(r => r.AbxCode)
            .ToList();

        var summaryParts = new List<string>();
        if (SelectedOrganism != null)
            summaryParts.Add($"Organism: {SelectedOrganism.DisplayName}");
        if (SelectedAntibiotic != null)
            summaryParts.Add($"Antibiotic: {SelectedAntibiotic.DisplayName}");
        summaryParts.Add($"Method: {activeMethod}" + (IsDiskMethod && !string.IsNullOrWhiteSpace(SelectedDiskPotency) && SelectedAntibiotic != null ? $" ({SelectedDiskPotency})" : ""));
        if (activeGuidelines != null && activeGuidelines.Count > 0)
            summaryParts.Add($"Guidelines: {string.Join(", ", activeGuidelines)}");
        if (activeYear.HasValue)
            summaryParts.Add($"Year: {activeYear.Value}");
        summaryParts.Add($"Type: {string.Join(", ", typeFilter)}");
        if (activeSites != null && activeSites.Count > 0)
            summaryParts.Add($"Site: {string.Join(", ", activeSites)}");

        string summary = string.Join("  •  ", summaryParts);
        if (string.IsNullOrWhiteSpace(summary))
            summary = "All Breakpoints";

        summary += $"  •  {list.Count} records";

        return (list, summary);
    }

    public (List<IntrinsicRuleRow> Rows, string Summary) GetApplicableIntrinsicRules()
    {
        if (SelectedOrganism == null && !string.IsNullOrWhiteSpace(OrganismSearchText))
            SelectedOrganism = ResolveOrganism(OrganismSearchText);
        if (SelectedAntibiotic == null && !string.IsNullOrWhiteSpace(AntibioticSearchText))
            SelectedAntibiotic = ResolveAntibiotic(AntibioticSearchText);

        var lookup = GetOrganismNameLookup();
        var abxLookup = GetAntibioticNameLookup();
        string? orgCode = SelectedOrganism?.Code;
        string? abxCode = SelectedAntibiotic?.Code;
        var activeGuidelines = RestrictGuidelines ? Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

        Organism? organismEntity = null;
        if (!string.IsNullOrWhiteSpace(orgCode))
        {
            if (Organism.CurrentOrganisms.TryGetValue(orgCode, out var foundOrg))
            {
                organismEntity = foundOrg;
            }
            else if (Organism.MergedOrganisms.TryGetValue(orgCode, out var mergedCode) && Organism.CurrentOrganisms.TryGetValue(mergedCode, out var mergedOrg))
            {
                organismEntity = mergedOrg;
            }
            else
            {
                organismEntity = Organism.CurrentOrganisms.Values.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase))
                    ?? Organism.AllOrganisms.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        IEnumerable<ExpectedResistancePhenotypeRule> query;
        var guidelineList = activeGuidelines?.ToList();
        var abxList = !string.IsNullOrWhiteSpace(abxCode) ? new List<string> { abxCode } : null;

        if (organismEntity != null)
        {
            // Reuse the engine so subkingdom, anaerobe, ATC-class and antibiotic-exception rules match interpretation.
            query = ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(
                organismEntity.WHONET_ORG_CODE, guidelineList, abxList);
        }
        else if (!string.IsNullOrWhiteSpace(orgCode))
        {
            query = ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules
                .Where(r => r.ORGANISM_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
            if (abxList != null)
                query = query.Where(r => r.ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(abxCode))
        {
            var antibioticEntity = Antibiotic.AllAntibiotics.FirstOrDefault(a => a.WHONET_ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase));
            string? atcCode = antibioticEntity?.ATC_CODE;

            query = ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules
                .Where(r =>
                    (r.ABX_CODE_TYPE == nameof(Antibiotic.WHONET_ABX_CODE) && r.ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase)) ||
                    (r.ABX_CODE_TYPE == nameof(Antibiotic.ATC_CODE) && !string.IsNullOrWhiteSpace(atcCode) && atcCode.StartsWith(r.ABX_CODE, StringComparison.OrdinalIgnoreCase)) ||
                    (string.IsNullOrWhiteSpace(r.ABX_CODE_TYPE) && r.ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase))
                )
                .Where(r => r.ANTIBIOTIC_EXCEPTIONS == null || !r.ANTIBIOTIC_EXCEPTIONS.Contains(abxCode, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            query = ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules;
        }

        if (activeGuidelines != null)
        {
            query = query.Where(r => activeGuidelines.Contains(r.GUIDELINE));
        }

        var list = query
            .Select(r => new IntrinsicRuleRow(
                r.GUIDELINE,
                r.ORGANISM_CODE,
                ResolveOrgName(r.ORGANISM_CODE, lookup),
                r.ABX_CODE,
                ResolveAbxName(r.ABX_CODE, abxLookup),
                string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? []),
                r.COMMENTS
            ))
            .OrderBy(r => r.Guideline)
            .ThenBy(r => r.OrganismCode)
            .ThenBy(r => r.AbxCode)
            .ToList();

        var summaryParts = new List<string>();
        if (SelectedOrganism != null)
            summaryParts.Add($"Organism: {SelectedOrganism.DisplayName}");
        if (SelectedAntibiotic != null)
            summaryParts.Add($"Antibiotic: {SelectedAntibiotic.DisplayName}");
        if (activeGuidelines != null && activeGuidelines.Count > 0)
            summaryParts.Add($"Guidelines: {string.Join(", ", activeGuidelines)}");

        string summary = string.Join("  •  ", summaryParts);
        if (string.IsNullOrWhiteSpace(summary))
            summary = "All Intrinsic Resistance Rules";

        summary += $"  •  {list.Count} records";

        return (list, summary);
    }

    public static void MovePriorityItem(ObservableCollection<SelectableItem> collection, SelectableItem? item, int delta)
    {
        if (item == null) return;
        int oldIndex = collection.IndexOf(item);
        int newIndex = oldIndex + delta;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= collection.Count) return;
        collection.Move(oldIndex, newIndex);
    }

    public (List<ExpertRuleRow> Rows, string Summary) GetApplicableExpertRules()
    {
        if (SelectedOrganism == null && !string.IsNullOrWhiteSpace(OrganismSearchText))
            SelectedOrganism = ResolveOrganism(OrganismSearchText);
        if (SelectedAntibiotic == null && !string.IsNullOrWhiteSpace(AntibioticSearchText))
            SelectedAntibiotic = ResolveAntibiotic(AntibioticSearchText);

        var lookup = GetOrganismNameLookup();
        string? orgCode = SelectedOrganism?.Code;
        string? abxCode = SelectedAntibiotic?.Code;
        var activeGuidelines = RestrictGuidelines ? Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

        Organism? organismEntity = null;
        if (!string.IsNullOrWhiteSpace(orgCode))
        {
            if (Organism.CurrentOrganisms.TryGetValue(orgCode, out var foundOrg))
                organismEntity = foundOrg;
            else if (Organism.MergedOrganisms.TryGetValue(orgCode, out var mergedCode) && Organism.CurrentOrganisms.TryGetValue(mergedCode, out var mergedOrg))
                organismEntity = mergedOrg;
            else
                organismEntity = Organism.CurrentOrganisms.Values.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase))
                    ?? Organism.AllOrganisms.FirstOrDefault(o => o.WHONET_ORG_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
        }

        IEnumerable<ExpertInterpretationRule> query;
        if (organismEntity != null && !string.IsNullOrWhiteSpace(abxCode))
        {
            var fullTestCodes = GetFullTestCodes();
            if (fullTestCodes.Count == 0)
            {
                string gCode = Antibiotic.GuidelineCodes.CLSI.ToString();
                fullTestCodes.Add(IsDiskMethod
                    ? $"{abxCode}_{gCode}D"
                    : $"{abxCode}_{gCode}M");
            }

            query = ExpertInterpretationRule.GetApplicableExpertRules(
                organismEntity.WHONET_ORG_CODE,
                fullTestCodes,
                [],
                ExpertInterpretationRule.RuleCodes.All);
        }
        else
        {
            query = ExpertInterpretationRule.ExpertInterpretationRules;
            if (!string.IsNullOrWhiteSpace(orgCode))
            {
                query = query.Where(r => r.ORGANISM_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(abxCode))
            {
                query = query.Where(r =>
                    (r.AFFECTED_ANTIBIOTICS != null && r.AFFECTED_ANTIBIOTICS.Contains(abxCode, StringComparer.OrdinalIgnoreCase)) ||
                    (r.RULE_CRITERIA != null && r.RULE_CRITERIA.Any(c => c.TestName.Equals(abxCode, StringComparison.OrdinalIgnoreCase))));
            }
        }

        if (activeGuidelines != null && activeGuidelines.Count > 0)
        {
            query = query.Where(r =>
                activeGuidelines.Any(g =>
                    r.RULE_CODE.Contains(g, StringComparison.OrdinalIgnoreCase) ||
                    r.DESCRIPTION.Contains(g, StringComparison.OrdinalIgnoreCase) ||
                    (!r.RULE_CODE.Contains("EUCAST", StringComparison.OrdinalIgnoreCase) &&
                     !r.RULE_CODE.Contains("CLSI", StringComparison.OrdinalIgnoreCase) &&
                     !r.RULE_CODE.Contains("SFM", StringComparison.OrdinalIgnoreCase))));
        }

        var list = query
            .Select(r => new ExpertRuleRow(
                r.RULE_CODE,
                r.DESCRIPTION,
                r.ORGANISM_CODE,
                ResolveOrgName(r.ORGANISM_CODE, lookup),
                string.Join(", ", r.AFFECTED_ANTIBIOTICS ?? []),
                string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? [])
            ))
            .OrderBy(r => r.RuleCode)
            .ThenBy(r => r.OrganismCode)
            .ToList();

        var summaryParts = new List<string>();
        if (SelectedOrganism != null)
            summaryParts.Add($"Organism: {SelectedOrganism.DisplayName}");
        if (SelectedAntibiotic != null)
            summaryParts.Add($"Antibiotic: {SelectedAntibiotic.DisplayName}");
        if (activeGuidelines != null && activeGuidelines.Count > 0)
            summaryParts.Add($"Guidelines: {string.Join(", ", activeGuidelines)}");

        string summary = string.Join("  •  ", summaryParts);
        if (string.IsNullOrWhiteSpace(summary))
            summary = "All Expert Interpretation Rules";

        summary += $"  •  {list.Count} records";

        return (list, summary);
    }
}
