using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AMRIE.WinUI.Models;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.ViewModels;

public class OrganismItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public OrganismItem() { }

    public OrganismItem(string displayName, string code)
    {
        DisplayName = displayName;
        Code = code;
    }

    public override string ToString() => DisplayName;
}

public class AntibioticItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public AntibioticItem() { }

    public AntibioticItem(string displayName, string code)
    {
        DisplayName = displayName;
        Code = code;
    }

    public override string ToString() => DisplayName;
}

public partial class SingleInterpretationViewModel : ObservableObject
{
    private static readonly Regex MatchCombinationAgents = new(@"/.+$", RegexOptions.Compiled);
    private static readonly string MIC_TestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.MIC;
    private static readonly string DiskTestFormat = @"{0}_{1}" + Antibiotic.TestMethodCodes.Disk + @"{2}";

    // Guideline Year
    [ObservableProperty]
    private bool _restrictGuidelineYear = true;

    [ObservableProperty]
    private double _guidelineYear = Constants.BreakpointTableRevisionYear;

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
    [ObservableProperty]
    private bool _isDiskMethod = true;

    [ObservableProperty]
    private bool _isMicMethod = false;

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

    [RelayCommand]
    public void Interpret()
    {
        HasStatusMessage = false;
        Results.Clear();

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

        string testMethod = IsDiskMethod ? Antibiotic.TestMethods.Disk : Antibiotic.TestMethods.MIC;
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
        AntibioticSpecificInterpretationRules.ClearBreakpoints();

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

        foreach (var testCode in fullTestCodes)
        {
            try
            {
                string rawInterpretation = IsolateInterpretation.GetSingleInterpretation(
                    config, SelectedOrganism.Code, testCode, Measurement.Trim());

                string badge = ExtractBadge(rawInterpretation);
                string cleanInterpretation = IncludeComments ? rawInterpretation : IsolateInterpretation.RemoveComments(rawInterpretation);

                string severity = badge switch
                {
                    "S" => "Success",
                    "I" or "SDD" => "Warning",
                    "R" => "Error",
                    _ => "Informational"
                };

                Results.Add(new InterpretationResultItem
                {
                    TestCode = testCode,
                    Interpretation = cleanInterpretation,
                    ResultBadge = badge,
                    StatusSeverity = severity,
                    Comments = rawInterpretation.Length > badge.Length ? rawInterpretation : string.Empty
                });
            }
            catch (Exception ex)
            {
                Results.Add(new InterpretationResultItem
                {
                    TestCode = testCode,
                    Interpretation = $"Error: {ex.Message}",
                    ResultBadge = "ERR",
                    StatusSeverity = "Error"
                });
            }
        }

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
}
