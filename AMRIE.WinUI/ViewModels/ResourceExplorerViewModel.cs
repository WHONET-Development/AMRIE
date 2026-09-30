using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AMR_Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AMRIE.WinUI.Models;

namespace AMRIE.WinUI.ViewModels;

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
        AvailableYears.Add("All Years");
        AvailableYears.Add(Constants.BreakpointTableRevisionYear.ToString());
    }

    /// <summary>
    /// Loads all breakpoint and rule resources once per application session.
    /// Data is static for the session lifetime, so subsequent page navigations return the completed task instantly.
    /// </summary>
    public Task LoadAsync()
    {
        return _loadTask ??= LoadAllResourcesAsync();
    }

    private async Task LoadAllResourcesAsync()
    {
        var years = await Task.Run(LoadAllResources);

        string targetYear = Constants.BreakpointTableRevisionYear.ToString();
        var yearList = new List<string> { "All Years" };
        foreach (var year in years)
        {
            string yStr = year.ToString();
            if (!yearList.Contains(yStr))
            {
                yearList.Add(yStr);
            }
        }

        AvailableYears.Clear();
        foreach (var yStr in yearList)
        {
            AvailableYears.Add(yStr);
        }

        if (AvailableYears.Contains(targetYear))
        {
            SelectedYear = targetYear;
        }
        else if (AvailableYears.Count > 1)
        {
            SelectedYear = AvailableYears[1];
        }
        else
        {
            SelectedYear = "All Years";
        }
        OnPropertyChanged(nameof(SelectedYear));

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

            if (!string.IsNullOrWhiteSpace(q))
            {
                // SearchKey is pre-lowercased at construction time — one Contains() per row per keystroke.
                queryable = queryable.Where(b => b.SearchKey.Contains(q));
            }

            var matches = queryable.Take(500).ToList();
            ResetCollection(FilteredBreakpoints, matches);
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
                queryable = queryable.Where(r => r.SearchKey.Contains(q));
            }

            var matches = queryable.ToList();
            ResetCollection(FilteredExpertRules, matches);
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
                queryable = queryable.Where(r => r.SearchKey.Contains(q));
            }

            var matches = queryable.ToList();
            ResetCollection(FilteredIntrinsicRules, matches);
            ResultCountMessage = $"Showing {FilteredIntrinsicRules.Count} intrinsic rule(s)";
        }
    }

    /// <summary>
    /// Replaces an ObservableCollection's contents in bulk. Fires Clear (Reset notification),
    /// then adds new items. This is significantly faster than Clear + per-item Add when the
    /// previous collection was large, since Clear already fires a single Reset that collapses
    /// all prior item notifications.
    /// </summary>
    private static void ResetCollection<T>(ObservableCollection<T> collection, List<T> newItems)
    {
        collection.Clear();
        foreach (var item in newItems)
            collection.Add(item);
    }
}
