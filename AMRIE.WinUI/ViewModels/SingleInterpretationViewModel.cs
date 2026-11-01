using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AMRIE.WinUI.Models;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.ViewModels;

public partial class SingleInterpretationViewModel : ObservableObject
{
    private static readonly Regex MatchCombinationAgents = new(@"/.+$", RegexOptions.Compiled);
    private static readonly string MIC_TestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.MIC;
    private static readonly string DiskTestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.Disk + @"{2}";

    // Guideline Year
    [ObservableProperty]
    private bool _restrictGuidelineYear = true;

    [ObservableProperty]
    private int _guidelineYear = Constants.BreakpointTableRevisionYear;

    public ObservableCollection<int> AvailableYears { get; } = new();

    // Filters
    [ObservableProperty]
    private bool _restrictGuidelines = true;

    [ObservableProperty]
    private bool _restrictBreakpointTypes = false;

    [ObservableProperty]
    private bool _restrictSitesOfInfection = false;

    public ObservableCollection<SelectableItem> Guidelines { get; } = new();
    public ObservableCollection<SelectableItem> BreakpointTypes { get; } = new();
    public ObservableCollection<SelectableItem> SitesOfInfection { get; } = new();

    // Organisms & Antibiotics
    public List<OrganismItem> AllOrganisms { get; } = new();
    public List<AntibioticItem> AllAntibiotics { get; } = new();

    [ObservableProperty]
    private OrganismItem? _selectedOrganism;

    [ObservableProperty]
    private string _organismSearchText = string.Empty;

    [ObservableProperty]
    private AntibioticItem? _selectedAntibiotic;

    [ObservableProperty]
    private string _antibioticSearchText = string.Empty;

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

    public ObservableCollection<string> DiskPotencies { get; } = new();

    [ObservableProperty]
    private string? _selectedDiskPotency;

    // Measurement & Comments
    [ObservableProperty]
    private string _measurement = string.Empty;

    [ObservableProperty]
    private bool _includeComments = true;

    // Status & Results
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage = false;

    [ObservableProperty]
    private bool _isStatusError = false;

    [ObservableProperty]
    private InfoBarSeverity _statusInfoBarSeverity = InfoBarSeverity.Informational;

    public ObservableCollection<InterpretationResultItem> Results { get; } = new();

    public SingleInterpretationViewModel()
    {
        InitializeData();
    }

    private void InitializeData()
    {
        // Available Years
        try
        {
            var years = Breakpoint.Breakpoints
                .Select(b => b.YEAR)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            foreach (var y in years)
                AvailableYears.Add(y);

            if (AvailableYears.Contains(Constants.BreakpointTableRevisionYear))
                GuidelineYear = Constants.BreakpointTableRevisionYear;
            else if (AvailableYears.Count > 0)
                GuidelineYear = AvailableYears[0];
        }
        catch { }

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

        // Cache Organisms (identical to classic WinForms app: string.Format("{0} - ({1})", o.ORGANISM, o.WHONET_ORG_CODE).Distinct())
        try
        {
            // Anonymous type projection relies on compiler-generated Equals/GetHashCode value equality for Distinct().
            var orgs = Organism.AllOrganisms
                .OrderBy(o => o.ORGANISM)
                .Select(o => new { Display = $"{o.ORGANISM} - ({o.WHONET_ORG_CODE})", Code = o.WHONET_ORG_CODE })
                .Distinct()
                .Select(o => new OrganismItem(o.Display, o.Code))
                .ToList();
            AllOrganisms.AddRange(orgs);
        }
        catch { }

        // Cache Antibiotics (identical to classic WinForms app: string.Format("{0} - ({1})", a.ANTIBIOTIC, a.WHONET_ABX_CODE).Distinct())
        try
        {
            var abxs = Antibiotic.AllAntibiotics
                .OrderBy(a => a.ANTIBIOTIC)
                .Select(a => new { Display = $"{a.ANTIBIOTIC} - ({a.WHONET_ABX_CODE})", Code = a.WHONET_ABX_CODE })
                .Distinct()
                .Select(a => new AntibioticItem(a.Display, a.Code))
                .ToList();
            AllAntibiotics.AddRange(abxs);
        }
        catch { }
    }

    partial void OnSelectedAntibioticChanged(AntibioticItem? value)
    {
        UpdateDiskPotencies();
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
            IncludeInterpretationComments = IncludeComments,
            GuidelineYear = RestrictGuidelineYear ? Convert.ToInt64(GuidelineYear) : 0,
            PrioritizedBreakpointTypes = new List<string>()
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
            config.PrioritizedSitesOfInfection = SitesOfInfection.Where(s => s.IsSelected).Select(s => s.Name).ToList();
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
        if (SelectedAntibiotic == null) return new List<string>();

        var selectedGuidelines = Guidelines.Where(g => g.IsSelected).Select(g => g.Name).ToList();
        return selectedGuidelines
            .Select(g => CreateFullTestCode(g, SelectedAntibiotic.Code, IsDiskMethod, SelectedDiskPotency ?? string.Empty))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
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

        var query = Breakpoint.Breakpoints.AsEnumerable();

        if (organismEntity != null)
        {
            var o = organismEntity;
            query = query.Where(b =>
                ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.WHONET_ORG_CODE), StringComparison.OrdinalIgnoreCase) && o.WHONET_ORG_CODE.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                 b.ORGANISM_CODE.Equals(o.WHONET_ORG_CODE, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(o.GENUS_CODE) &&
                 ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.GENUS_CODE), StringComparison.OrdinalIgnoreCase) && o.GENUS_CODE.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                  b.ORGANISM_CODE.Equals(o.GENUS_CODE, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) &&
                 ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.FAMILY_CODE), StringComparison.OrdinalIgnoreCase) && o.FAMILY_CODE.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                  b.ORGANISM_CODE.Equals(o.FAMILY_CODE, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(o.SPECIES_GROUP) &&
                 ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.SPECIES_GROUP), StringComparison.OrdinalIgnoreCase) && o.SPECIES_GROUP.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                  b.ORGANISM_CODE.Equals(o.SPECIES_GROUP, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(o.GENUS_GROUP) &&
                 ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.GENUS_GROUP), StringComparison.OrdinalIgnoreCase) && o.GENUS_GROUP.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                  b.ORGANISM_CODE.Equals(o.GENUS_GROUP, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(o.SEROVAR_GROUP) &&
                 ((b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.SEROVAR_GROUP), StringComparison.OrdinalIgnoreCase) && o.SEROVAR_GROUP.Equals(b.ORGANISM_CODE, StringComparison.OrdinalIgnoreCase)) ||
                  b.ORGANISM_CODE.Equals(o.SEROVAR_GROUP, StringComparison.OrdinalIgnoreCase))) ||
                (o.ANAEROBE && (
                    (b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(nameof(o.ANAEROBE), StringComparison.OrdinalIgnoreCase) && (b.ORGANISM_CODE.Equals(Constants.OrganismGroups.Anaerobes, StringComparison.OrdinalIgnoreCase) || b.ORGANISM_CODE.Equals("ANA", StringComparison.OrdinalIgnoreCase))) ||
                    (b.ORGANISM_CODE_TYPE != null && b.ORGANISM_CODE_TYPE.Equals(Constants.OrganismGroups.AnaerobePlusSubkingdomCode, StringComparison.OrdinalIgnoreCase) && (
                        (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive && b.ORGANISM_CODE.Equals(Constants.OrganismGroups.GramPositiveAnaerobes, StringComparison.OrdinalIgnoreCase)) ||
                        (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Negative && b.ORGANISM_CODE.Equals(Constants.OrganismGroups.GramNegativeAnaerobes, StringComparison.OrdinalIgnoreCase))
                    )) ||
                    b.ORGANISM_CODE.Equals("ANA", StringComparison.OrdinalIgnoreCase)
                ))
            );
        }
        else if (!string.IsNullOrWhiteSpace(orgCode))
        {
            query = query.Where(b => b.ORGANISM_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
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

        if (activeGuidelines != null && activeGuidelines.Count > 0)
        {
            query = query.Where(b => activeGuidelines.Contains(b.GUIDELINES));
        }

        if (activeYear.HasValue)
        {
            query = query.Where(b => b.YEAR == activeYear.Value);
        }

        if (activeTypes != null && activeTypes.Count > 0)
        {
            query = query.Where(b => activeTypes.Contains(b.BREAKPOINT_TYPE));
        }

        if (activeSites != null && activeSites.Count > 0)
        {
            query = query.Where(b => b.SITES_OF_INFECTION.Any(s => activeSites.Contains(s)));
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
                b.WHONET_TEST,
                b.R,
                b.I,
                b.SDD,
                b.S,
                b.ECV_ECOFF,
                b.COMMENTS
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
        if (activeTypes != null && activeTypes.Count > 0)
            summaryParts.Add($"Type: {string.Join(", ", activeTypes)}");
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

        var query = ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules.AsEnumerable();

        if (organismEntity != null)
        {
            var o = organismEntity;
            query = query.Where(r =>
            {
                if (!string.IsNullOrWhiteSpace(r.EXCEPTION_ORGANISM_CODE))
                {
                    var exceptions = r.EXCEPTION_ORGANISM_CODE.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (exceptions.Any(ex =>
                        ex.Equals(o.WHONET_ORG_CODE, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(o.GENUS_CODE) && ex.Equals(o.GENUS_CODE, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) && ex.Equals(o.FAMILY_CODE, StringComparison.OrdinalIgnoreCase))))
                    {
                        return false;
                    }
                }

                return r.ORGANISM_CODE.Equals(o.WHONET_ORG_CODE, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(o.GENUS_CODE) && r.ORGANISM_CODE.Equals(o.GENUS_CODE, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(o.GENUS_GROUP) && r.ORGANISM_CODE.Equals(o.GENUS_GROUP, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) && r.ORGANISM_CODE.Equals(o.FAMILY_CODE, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(o.SPECIES_GROUP) && r.ORGANISM_CODE.Equals(o.SPECIES_GROUP, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(o.SEROVAR_GROUP) && r.ORGANISM_CODE.Equals(o.SEROVAR_GROUP, StringComparison.OrdinalIgnoreCase)) ||
                    r.ORGANISM_CODE.Equals(ExpectedResistancePhenotypeRule.AllOrganisms, StringComparison.OrdinalIgnoreCase);
            });
        }
        else if (!string.IsNullOrWhiteSpace(orgCode))
        {
            query = query.Where(r => r.ORGANISM_CODE.Equals(orgCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(abxCode))
        {
            query = query.Where(r => r.ABX_CODE.Equals(abxCode, StringComparison.OrdinalIgnoreCase));
        }

        if (activeGuidelines != null && activeGuidelines.Count > 0)
        {
            query = query.Where(r => activeGuidelines.Contains(r.GUIDELINE));
        }

        var list = query
            .Select(r => new IntrinsicRuleRow(
                r.GUIDELINE,
                r.ORGANISM_CODE,
                ResolveOrgName(r.ORGANISM_CODE, lookup),
                r.ABX_CODE,
                string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? new List<string>()),
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
}
