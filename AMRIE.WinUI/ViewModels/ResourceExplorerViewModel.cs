using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AMRIE.WinUI.ViewModels;

public class BreakpointRow
{
    public string Guidelines { get; set; } = string.Empty;
    public int Year { get; set; }
    public string TestMethod { get; set; } = string.Empty;
    public string Potency { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string BreakpointType { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string SiteOfInfection { get; set; } = string.Empty;
    public string AbxCode { get; set; } = string.Empty;
    public string WhonetTest { get; set; } = string.Empty;
    public decimal R { get; set; }
    public string I { get; set; } = string.Empty;
    public string Sdd { get; set; } = string.Empty;
    public decimal S { get; set; }
    public decimal EcvEcoff { get; set; }
    public string Comments { get; set; } = string.Empty;

    public string RDisplay => R > 0 ? R.ToString("G29") : "-";
    public string SDisplay => S > 0 ? S.ToString("G29") : "-";
    public string EcvEcoffDisplay => EcvEcoff > 0 ? EcvEcoff.ToString("G29") : "-";

    public BreakpointRow() { }

    public BreakpointRow(string guidelines, int year, string testMethod, string potency, string organismCode,
        string breakpointType, string host, string siteOfInfection, string abxCode, string whonetTest,
        decimal r, string i, string sdd, decimal s, decimal ecvEcoff, string comments)
        : this(guidelines, year, testMethod, potency, organismCode, string.Empty, breakpointType, host, siteOfInfection, abxCode, whonetTest, r, i, sdd, s, ecvEcoff, comments)
    {
    }

    public BreakpointRow(string guidelines, int year, string testMethod, string potency, string organismCode,
        string organismName, string breakpointType, string host, string siteOfInfection, string abxCode, string whonetTest,
        decimal r, string i, string sdd, decimal s, decimal ecvEcoff, string comments)
    {
        Guidelines = guidelines;
        Year = year;
        TestMethod = testMethod;
        Potency = potency;
        OrganismCode = organismCode;
        OrganismName = organismName;
        BreakpointType = breakpointType;
        Host = host;
        SiteOfInfection = siteOfInfection;
        AbxCode = abxCode;
        WhonetTest = whonetTest;
        R = r;
        I = i;
        Sdd = sdd;
        S = s;
        EcvEcoff = ecvEcoff;
        Comments = comments;
    }
}

public class ExpertRuleRow
{
    public string RuleCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string AffectedAntibiotics { get; set; } = string.Empty;
    public string Exceptions { get; set; } = string.Empty;

    public ExpertRuleRow() { }

    public ExpertRuleRow(string ruleCode, string description, string organismCode, string affectedAntibiotics, string exceptions)
        : this(ruleCode, description, organismCode, string.Empty, affectedAntibiotics, exceptions)
    {
    }

    public ExpertRuleRow(string ruleCode, string description, string organismCode, string organismName, string affectedAntibiotics, string exceptions)
    {
        RuleCode = ruleCode;
        Description = description;
        OrganismCode = organismCode;
        OrganismName = organismName;
        AffectedAntibiotics = affectedAntibiotics;
        Exceptions = exceptions;
    }
}

public class IntrinsicRuleRow
{
    public string Guideline { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string AbxCode { get; set; } = string.Empty;
    public string Exceptions { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    public IntrinsicRuleRow() { }

    public IntrinsicRuleRow(string guideline, string organismCode, string abxCode, string exceptions, string comments)
        : this(guideline, organismCode, string.Empty, abxCode, exceptions, comments)
    {
    }

    public IntrinsicRuleRow(string guideline, string organismCode, string organismName, string abxCode, string exceptions, string comments)
    {
        Guideline = guideline;
        OrganismCode = organismCode;
        OrganismName = organismName;
        AbxCode = abxCode;
        Exceptions = exceptions;
        Comments = comments;
    }
}

public partial class ResourceExplorerViewModel : ObservableObject
{
    // Selected Section: 0 = Breakpoints, 1 = Expert Rules, 2 = Intrinsic Resistance
    [ObservableProperty]
    private int _selectedSectionIndex = 0;

    [ObservableProperty]
    private string _selectedSectionName = "Breakpoints";

    public ObservableCollection<string> AvailableSections { get; } = new()
    {
        "Breakpoints",
        "Expert Rules",
        "Intrinsic Resistance"
    };

    [ObservableProperty]
    private string _filterQuery = string.Empty;

    // Guideline Filter (CLSI, EUCAST, SFM, All)
    [ObservableProperty]
    private string _selectedGuideline = "All Guidelines";

    public ObservableCollection<string> AvailableGuidelines { get; } = new()
    {
        "All Guidelines",
        nameof(Antibiotic.GuidelineNames.CLSI),
        nameof(Antibiotic.GuidelineNames.EUCAST),
        nameof(Antibiotic.GuidelineNames.SFM)
    };

    // Test Method Filter (All Methods, Disk, MIC)
    [ObservableProperty]
    private string _selectedTestMethod = "All Methods";

    public ObservableCollection<string> AvailableTestMethods { get; } = new()
    {
        "All Methods",
        "Disk",
        "MIC"
    };

    // Year Filter ("All Years" or specific year)
    [ObservableProperty]
    private string _selectedYear = Constants.BreakpointTableRevisionYear.ToString();

    public ObservableCollection<string> AvailableYears { get; } = new();

    [ObservableProperty]
    private string _resultCountMessage = string.Empty;

    public ObservableCollection<BreakpointRow> FilteredBreakpoints { get; } = new();
    public ObservableCollection<ExpertRuleRow> FilteredExpertRules { get; } = new();
    public ObservableCollection<IntrinsicRuleRow> FilteredIntrinsicRules { get; } = new();

    private List<BreakpointRow> _allBreakpoints = new();
    private List<ExpertRuleRow> _allExpertRules = new();
    private List<IntrinsicRuleRow> _allIntrinsicRules = new();

    private Task? _loadTask;
    private static Dictionary<string, string> BuildOrganismNameLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var org in Organism.AllOrganisms)
            {
                if (!string.IsNullOrWhiteSpace(org.WHONET_ORG_CODE) && !string.IsNullOrWhiteSpace(org.ORGANISM))
                {
                    lookup.TryAdd(org.WHONET_ORG_CODE, org.ORGANISM);
                }
            }
        }
        catch { }
        return lookup;
    }

    private static string ResolveOrganismName(string? code, Dictionary<string, string> organismNameCache)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return organismNameCache.TryGetValue(code, out var name) ? name : string.Empty;
    }

    public ResourceExplorerViewModel()
    {
    }

    public Task LoadAsync()
    {
        return _loadTask ??= LoadAllResourcesAsync();
    }

    private async Task LoadAllResourcesAsync()
    {
        var years = await Task.Run(LoadAllResources);

        AvailableYears.Clear();
        AvailableYears.Add("All Years");
        foreach (var year in years)
        {
            AvailableYears.Add(year.ToString());
        }

        string currentYear = Constants.BreakpointTableRevisionYear.ToString();
        if (years.Contains((int)Constants.BreakpointTableRevisionYear))
        {
            SelectedYear = currentYear;
        }
        else if (years.Count > 0)
        {
            SelectedYear = years[0].ToString();
        }

        ApplyFilter();
    }

    private List<int> LoadAllResources()
    {
        var organismNameCache = BuildOrganismNameLookup();
        var years = new List<int>();

        try
        {
            // Breakpoints
            _allBreakpoints = Breakpoint.Breakpoints.Select(b => new BreakpointRow(
                b.GUIDELINES,
                b.YEAR,
                b.TEST_METHOD,
                b.POTENCY,
                b.ORGANISM_CODE,
                ResolveOrganismName(b.ORGANISM_CODE, organismNameCache),
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
            )).ToList();

            years = _allBreakpoints.Select(b => b.Year).Distinct().OrderByDescending(y => y).ToList();
        }
        catch { }

        try
        {
            // Expert Rules
            _allExpertRules = ExpertInterpretationRule.ExpertInterpretationRules.Select(r => new ExpertRuleRow(
                r.RULE_CODE,
                r.DESCRIPTION,
                r.ORGANISM_CODE,
                ResolveOrganismName(r.ORGANISM_CODE, organismNameCache),
                string.Join(", ", r.AFFECTED_ANTIBIOTICS ?? new List<string>()),
                string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? new List<string>())
            )).ToList();
        }
        catch { }

        try
        {
            // Intrinsic Rules: Load all rules directly
            _allIntrinsicRules = ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules
                .Select(r => new IntrinsicRuleRow(
                    r.GUIDELINE,
                    r.ORGANISM_CODE,
                    ResolveOrganismName(r.ORGANISM_CODE, organismNameCache),
                    r.ABX_CODE,
                    string.Join(", ", r.ANTIBIOTIC_EXCEPTIONS ?? new List<string>()),
                    r.COMMENTS
                )).ToList();
        }
        catch { }

        return years;
    }

    partial void OnSelectedSectionIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableSections.Count && SelectedSectionName != AvailableSections[value])
        {
            _selectedSectionName = AvailableSections[value];
            OnPropertyChanged(nameof(SelectedSectionName));
        }
        ApplyFilter();
    }

    partial void OnSelectedSectionNameChanged(string value)
    {
        int index = AvailableSections.IndexOf(value);
        if (index >= 0 && index != SelectedSectionIndex)
        {
            _selectedSectionIndex = index;
            OnPropertyChanged(nameof(SelectedSectionIndex));
            ApplyFilter();
        }
    }

    partial void OnFilterQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedGuidelineChanged(string value) => ApplyFilter();
    partial void OnSelectedTestMethodChanged(string value) => ApplyFilter();
    partial void OnSelectedYearChanged(string value) => ApplyFilter();

    [RelayCommand]
    public void ApplyFilter()
    {
        string q = FilterQuery.Trim().ToLowerInvariant();

        if (SelectedSectionIndex == 0)
        {
            FilteredBreakpoints.Clear();
            var queryable = _allBreakpoints.AsEnumerable();

            // Guideline Filter
            if (!string.IsNullOrWhiteSpace(SelectedGuideline) && SelectedGuideline != "All Guidelines")
            {
                queryable = queryable.Where(b => string.Equals(b.Guidelines, SelectedGuideline, StringComparison.OrdinalIgnoreCase));
            }

            // Test Method Filter (All Methods, Disk, MIC)
            if (!string.IsNullOrWhiteSpace(SelectedTestMethod) && SelectedTestMethod != "All Methods")
            {
                queryable = queryable.Where(b => string.Equals(b.TestMethod, SelectedTestMethod, StringComparison.OrdinalIgnoreCase));
            }

            // Year Filter (direct dropdown, no checkbox needed)
            if (!string.IsNullOrWhiteSpace(SelectedYear) && SelectedYear != "All Years" && int.TryParse(SelectedYear, out int targetYear))
            {
                queryable = queryable.Where(b => b.Year == targetYear);
            }

            // Text search query: matches organism code OR organism name, abx, guideline, test, etc.
            if (!string.IsNullOrWhiteSpace(q))
            {
                queryable = queryable.Where(b =>
                    b.OrganismCode.ToLowerInvariant().Contains(q) ||
                    b.OrganismName.ToLowerInvariant().Contains(q) ||
                    b.AbxCode.ToLowerInvariant().Contains(q) ||
                    b.Guidelines.ToLowerInvariant().Contains(q) ||
                    b.WhonetTest.ToLowerInvariant().Contains(q) ||
                    b.BreakpointType.ToLowerInvariant().Contains(q) ||
                    b.SiteOfInfection.ToLowerInvariant().Contains(q) ||
                    b.Comments.ToLowerInvariant().Contains(q));
            }

            var matches = queryable.Take(500).ToList();

            foreach (var m in matches) FilteredBreakpoints.Add(m);
            ResultCountMessage = SelectedYear != "All Years"
                ? $"Showing {FilteredBreakpoints.Count} breakpoints for year {SelectedYear} (capped at 500)"
                : $"Showing {FilteredBreakpoints.Count} of {_allBreakpoints.Count} breakpoints (capped at 500)";
        }
        else if (SelectedSectionIndex == 1)
        {
            FilteredExpertRules.Clear();
            var queryable = _allExpertRules.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SelectedGuideline) && SelectedGuideline != "All Guidelines")
            {
                queryable = queryable.Where(r => r.RuleCode.Contains(SelectedGuideline, StringComparison.OrdinalIgnoreCase) ||
                                                 r.Description.Contains(SelectedGuideline, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                queryable = queryable.Where(r =>
                    r.RuleCode.ToLowerInvariant().Contains(q) ||
                    r.Description.ToLowerInvariant().Contains(q) ||
                    r.OrganismCode.ToLowerInvariant().Contains(q) ||
                    r.OrganismName.ToLowerInvariant().Contains(q) ||
                    r.AffectedAntibiotics.ToLowerInvariant().Contains(q));
            }

            foreach (var m in queryable) FilteredExpertRules.Add(m);
            ResultCountMessage = $"Showing {FilteredExpertRules.Count} expert rule(s)";
        }
        else if (SelectedSectionIndex == 2)
        {
            FilteredIntrinsicRules.Clear();
            var queryable = _allIntrinsicRules.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SelectedGuideline) && SelectedGuideline != "All Guidelines")
            {
                queryable = queryable.Where(r => string.Equals(r.Guideline, SelectedGuideline, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                queryable = queryable.Where(r =>
                    r.Guideline.ToLowerInvariant().Contains(q) ||
                    r.OrganismCode.ToLowerInvariant().Contains(q) ||
                    r.OrganismName.ToLowerInvariant().Contains(q) ||
                    r.AbxCode.ToLowerInvariant().Contains(q) ||
                    r.Comments.ToLowerInvariant().Contains(q));
            }

            foreach (var m in queryable) FilteredIntrinsicRules.Add(m);
            ResultCountMessage = $"Showing {FilteredIntrinsicRules.Count} intrinsic rule(s)";
        }
    }
}
