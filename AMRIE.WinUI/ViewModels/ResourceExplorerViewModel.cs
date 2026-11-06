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

    // Organism / Clinical Panel Filter
    [ObservableProperty]
    private OrganismFilterItem _selectedOrganism;

    public ObservableCollection<OrganismFilterItem> AvailableOrganisms { get; } = new();

    // Taxonomic mappings: species code -> list of parent panel codes (e.g. "pmi" -> ["EBC", "PR-"])
    private readonly Dictionary<string, List<string>> _memberToPanels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _panelToMembers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _organismNameToCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _organismNameLookup = new(StringComparer.OrdinalIgnoreCase);

    private record ResolvedOrganismContext(
        string Code,
        string Name,
        bool IsPanel,
        List<string> ParentPanels,
        List<string> MemberSpecies);

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

    // Breakpoint Type Filter (All Types, Human, Animal, ECOFF)
    [ObservableProperty]
    private string _selectedBreakpointType = "All Types";

    public ObservableCollection<string> AvailableBreakpointTypes { get; } = new()
    {
        "All Types",
        Breakpoint.BreakpointTypes.Human,
        Breakpoint.BreakpointTypes.Animal,
        Breakpoint.BreakpointTypes.ECOFF
    };

    // Year Filter ("All Years" or specific year)
    [ObservableProperty]
    private string _selectedYear = Constants.BreakpointTableRevisionYear.ToString();

    public ObservableCollection<string> AvailableYears { get; } = new();

    // Option to hide rows where all breakpoints (S/I/R/SDD) are not available (shown as -)
    [ObservableProperty]
    private bool _hideUndefinedBreakpoints;

    [ObservableProperty]
    private string _resultCountMessage = string.Empty;

    public ObservableCollection<BreakpointRow> FilteredBreakpoints { get; } = new();
    public ObservableCollection<ExpertRuleRow> FilteredExpertRules { get; } = new();
    public ObservableCollection<IntrinsicRuleRow> FilteredIntrinsicRules { get; } = new();

    private List<BreakpointRow> _allBreakpoints = new();
    private List<ExpertRuleRow> _allExpertRules = new();
    private List<IntrinsicRuleRow> _allIntrinsicRules = new();

    private Task? _loadTask;

    public ResourceExplorerViewModel()
    {
        _selectedOrganism = InitializeAvailableOrganisms();
        AvailableYears.Add("All Years");
        AvailableYears.Add(Constants.BreakpointTableRevisionYear.ToString());
    }

    private OrganismFilterItem InitializeAvailableOrganisms()
    {
        BuildTaxonomicLookups();
        AvailableOrganisms.Clear();

        var allItem = new OrganismFilterItem { Code = "ALL", Name = "All Organisms & Panels", IsPanel = false, Group = "All" };
        AvailableOrganisms.Add(allItem);

        // 13 Curated Clinical Panels from WHONET
        var panels = new List<OrganismFilterItem>
        {
            new() { Code = "EBC", Name = "Enterobacterales (Family Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "STA", Name = "Staphylococcus sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "ENT", Name = "Enterococcus sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "STR", Name = "Streptococcus sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "PSE", Name = "Pseudomonas sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "AC-", Name = "Acinetobacter sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "HA-", Name = "Haemophilus sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "NE-", Name = "Neisseria sp. (Genus Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "NFR", Name = "Other non-Enterobacterales (Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "BCS", Name = "Burkholderia cepacia complex (Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "SCN", Name = "Coagulase-negative staphylococci (Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "SVI", Name = "Viridans group streptococci (Panel)", IsPanel = true, Group = "Clinical Panels" },
            new() { Code = "ANA", Name = "Anaerobic bacteria (Panel)", IsPanel = true, Group = "Clinical Panels" },
        };
        foreach (var p in panels) AvailableOrganisms.Add(p);

        // Extract all clinical organisms (common pathogens or organisms having published breakpoints)
        var bpOrgsSet = new HashSet<string>(
            Breakpoint.Breakpoints.Select(b => b.ORGANISM_CODE),
            StringComparer.OrdinalIgnoreCase);

        var panelCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EBC", "STA", "ENT", "STR", "PSE", "AC-", "HA-", "NE-", "NFR", "BCS", "SCN", "SVI", "ANA",
            "ACI", "PS-", "AN+", "AN-", "SC+", "HIN", "ALL", "Gen", "LAC", "LEU", "PED", "PAS",
            "CAR", "EIK", "KIN", "CAM", "COF", "AER", "CAN", "BFX", "PFX", "MTX", "MAX", "RGM",
            "SAL", "BR-", "BS-", "AGT", "PLE", "GCT"
        };

        var clinicalOrganismsMap = new Dictionary<string, OrganismFilterItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var org in Organism.AllOrganisms)
        {
            if (string.IsNullOrWhiteSpace(org.WHONET_ORG_CODE)) continue;
            string code = org.WHONET_ORG_CODE.Trim();
            if (panelCodes.Contains(code)) continue;

            bool isCommon = org.COMMON;
            bool hasBreakpoints = bpOrgsSet.Contains(code);

            if (isCommon || hasBreakpoints)
            {
                if (!clinicalOrganismsMap.ContainsKey(code))
                {
                    string name = !string.IsNullOrWhiteSpace(org.ORGANISM) ? org.ORGANISM.Trim() : code;
                    clinicalOrganismsMap[code] = new OrganismFilterItem
                    {
                        Code = code,
                        Name = name,
                        IsPanel = false,
                        Group = "Common Pathogens (Panel + Species Breakpoints)"
                    };
                }
            }
        }

        var sortedClinicalOrgs = clinicalOrganismsMap.Values
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var org in sortedClinicalOrgs)
        {
            AvailableOrganisms.Add(org);
        }

        return allItem;
    }

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
            foreach (var b in Breakpoint.Breakpoints)
            {
                if (!string.IsNullOrWhiteSpace(b.ORGANISM_CODE) && !string.IsNullOrWhiteSpace(b.REFERENCE_TABLE))
                {
                    lookup.TryAdd(b.ORGANISM_CODE, b.REFERENCE_TABLE);
                }
            }
            lookup.TryAdd("EBC", "Enterobacterales");
            lookup.TryAdd("STA", "Staphylococcus");
            lookup.TryAdd("ENT", "Enterococcus");
            lookup.TryAdd("STR", "Streptococcus");
            lookup.TryAdd("PSE", "Pseudomonas");
            lookup.TryAdd("AC-", "Acinetobacter");
            lookup.TryAdd("NFR", "Non-fermenters");
            lookup.TryAdd("BCS", "Burkholderia cepacia complex");
            lookup.TryAdd("ANA", "Anaerobes");
            lookup.TryAdd("PR-", "Proteus");
        }
        catch { }
        return lookup;
    }

    private static Dictionary<string, string> BuildAntibioticNameLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var abx in Antibiotic.AllAntibiotics)
            {
                if (!string.IsNullOrWhiteSpace(abx.WHONET_ABX_CODE) && !string.IsNullOrWhiteSpace(abx.ANTIBIOTIC))
                {
                    lookup.TryAdd(abx.WHONET_ABX_CODE, abx.ANTIBIOTIC);
                }
            }
        }
        catch { }
        return lookup;
    }

    private static string ResolveAntibioticName(string? code, Dictionary<string, string> antibioticNameCache)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return antibioticNameCache.TryGetValue(code, out var name) ? name : string.Empty;
    }

    private static string ResolveOrganismName(string? code, Dictionary<string, string> organismNameCache)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return organismNameCache.TryGetValue(code, out var name) ? name : string.Empty;
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
        var antibioticNameCache = BuildAntibioticNameLookup();
        var years = new List<int>();

        BuildTaxonomicLookups();

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
                ResolveAntibioticName(b.WHONET_ABX_CODE, antibioticNameCache),
                b.WHONET_TEST,
                b.R,
                b.I,
                b.SDD,
                b.S,
                b.ECV_ECOFF,
                b.COMMENTS,
                b.ORGANISM_CODE_TYPE,
                b.REFERENCE_TABLE
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
                    ResolveAntibioticName(r.ABX_CODE, antibioticNameCache),
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

    partial void OnSelectedOrganismChanged(OrganismFilterItem value) => ApplyFilter();
    partial void OnFilterQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedGuidelineChanged(string value) => ApplyFilter();
    partial void OnSelectedTestMethodChanged(string value) => ApplyFilter();
    partial void OnSelectedBreakpointTypeChanged(string value) => ApplyFilter();
    partial void OnSelectedYearChanged(string value) => ApplyFilter();
    partial void OnHideUndefinedBreakpointsChanged(bool value) => ApplyFilter();

    private void BuildTaxonomicLookups()
    {
        if (_memberToPanels.Count > 0) return;

        var nameLookup = BuildOrganismNameLookup();
        foreach (var kvp in nameLookup)
        {
            _organismNameLookup.TryAdd(kvp.Key, kvp.Value);
        }

        try
        {
            foreach (var org in Organism.AllOrganisms)
            {
                if (string.IsNullOrWhiteSpace(org.WHONET_ORG_CODE)) continue;
                string code = org.WHONET_ORG_CODE.Trim();

                if (!string.IsNullOrWhiteSpace(org.ORGANISM))
                {
                    _organismNameToCode.TryAdd(org.ORGANISM.Trim(), code);
                }

                var panels = new List<string>();
                if (!string.IsNullOrWhiteSpace(org.FAMILY_CODE)) panels.Add(org.FAMILY_CODE.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(org.GENUS_CODE)) panels.Add(org.GENUS_CODE.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(org.GENUS_GROUP)) panels.Add(org.GENUS_GROUP.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(org.SPECIES_GROUP)) panels.Add(org.SPECIES_GROUP.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(org.SEROVAR_GROUP)) panels.Add(org.SEROVAR_GROUP.Trim().ToUpperInvariant());
                if (org.ANAEROBE && !panels.Contains("ANA")) panels.Add("ANA");

                if (panels.Count > 0)
                {
                    _memberToPanels[code] = panels.Distinct().ToList();
                    foreach (var p in panels)
                    {
                        if (!_panelToMembers.TryGetValue(p, out var members))
                        {
                            members = new List<string>();
                            _panelToMembers[p] = members;
                        }
                        if (!members.Contains(code, StringComparer.OrdinalIgnoreCase))
                            members.Add(code);
                    }
                }
            }

            foreach (var kvp in Organism.MergedOrganisms)
            {
                if (!_memberToPanels.ContainsKey(kvp.Key) && _memberToPanels.TryGetValue(kvp.Value, out var targetPanels))
                {
                    _memberToPanels[kvp.Key] = new List<string>(targetPanels);
                }
            }
        }
        catch { }
    }

    private ResolvedOrganismContext? ResolveOrganismOrPanel(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        BuildTaxonomicLookups();

        string q = query.Trim();
        string qUpper = q.ToUpperInvariant();

        // 1. Direct match on organism code (e.g. "pmi", "eco", "sau")
        if (_memberToPanels.TryGetValue(q, out var parentPanels) || _organismNameLookup.ContainsKey(q))
        {
            string? name = _organismNameToCode.FirstOrDefault(kvp => string.Equals(kvp.Value, q, StringComparison.OrdinalIgnoreCase)).Key;
            if (string.IsNullOrEmpty(name))
            {
                _organismNameLookup.TryGetValue(q, out name);
            }
            var panels = parentPanels ?? (_memberToPanels.TryGetValue(q, out var p) ? p : new List<string>());
            return new ResolvedOrganismContext(q.ToLowerInvariant(), name ?? q, false, panels, new List<string>());
        }

        // 2. Direct match on panel code (e.g. "EBC", "PR-", "STA")
        if (_panelToMembers.TryGetValue(qUpper, out var memberSpecies) || _organismNameLookup.ContainsKey(qUpper))
        {
            _organismNameLookup.TryGetValue(qUpper, out string? panelName);
            var members = memberSpecies ?? (_panelToMembers.TryGetValue(qUpper, out var m) ? m : new List<string>());
            return new ResolvedOrganismContext(qUpper, panelName ?? qUpper, true, new List<string>(), members);
        }

        // 3. Exact match on organism name
        if (_organismNameToCode.TryGetValue(q, out var matchedCode))
        {
            var panels = _memberToPanels.TryGetValue(matchedCode, out var p) ? p : new List<string>();
            return new ResolvedOrganismContext(matchedCode.ToLowerInvariant(), q, false, panels, new List<string>());
        }

        var nameMatch = _organismNameToCode.FirstOrDefault(kvp => string.Equals(kvp.Key, q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(nameMatch.Value))
        {
            var panels = _memberToPanels.TryGetValue(nameMatch.Value, out var p) ? p : new List<string>();
            return new ResolvedOrganismContext(nameMatch.Value.ToLowerInvariant(), nameMatch.Key, false, panels, new List<string>());
        }

        // 4. Panel description match (e.g. "Enterobacterales")
        var panelMatch = _organismNameLookup.FirstOrDefault(kvp => string.Equals(kvp.Value, q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(panelMatch.Key))
        {
            var members = _panelToMembers.TryGetValue(panelMatch.Key, out var pMembers) ? pMembers : new List<string>();
            return new ResolvedOrganismContext(panelMatch.Key.ToUpperInvariant(), panelMatch.Value, true, new List<string>(), members);
        }

        return null;
    }

    [RelayCommand]
    public void ApplyFilter()
    {
        string q = FilterQuery.Trim().ToLowerInvariant();

        ResolvedOrganismContext? selectedOrgCtx = (SelectedOrganism != null && SelectedOrganism.Code != "ALL")
            ? ResolveOrganismOrPanel(SelectedOrganism.Code)
            : null;
        ResolvedOrganismContext? searchOrgCtx = !string.IsNullOrWhiteSpace(q)
            ? ResolveOrganismOrPanel(q)
            : null;
        ResolvedOrganismContext? effectiveOrg = selectedOrgCtx ?? searchOrgCtx;

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

            // Breakpoint Type Filter (All Types, Human, Animal, ECOFF)
            if (!string.IsNullOrWhiteSpace(SelectedBreakpointType) && SelectedBreakpointType != "All Types")
            {
                queryable = queryable.Where(b => string.Equals(b.BreakpointType, SelectedBreakpointType, StringComparison.OrdinalIgnoreCase));
            }

            // Year Filter
            if (!string.IsNullOrWhiteSpace(SelectedYear) && SelectedYear != "All Years" && int.TryParse(SelectedYear, out int targetYear))
            {
                queryable = queryable.Where(b => b.Year == targetYear);
            }

            // Hide rows where all breakpoints (S/I/R/SDD) are not available (shown as -)
            if (HideUndefinedBreakpoints)
            {
                queryable = queryable.Where(b => b.HasAnyBreakpoint);
            }

            // Organism / Panel Filter (Taxonomic Resolution)
            if (effectiveOrg != null)
            {
                queryable = queryable.Where(b =>
                {
                    if (effectiveOrg.IsPanel)
                    {
                        if (string.Equals(b.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.MemberSpecies.Any(m => string.Equals(b.OrganismCode, m, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    else
                    {
                        if (string.Equals(b.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.ParentPanels.Any(p => string.Equals(b.OrganismCode, p, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    return false;
                });
            }

            // Search Query Filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                bool isSearchExactOrg = searchOrgCtx != null && effectiveOrg != null &&
                    string.Equals(searchOrgCtx.Code, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase);

                if (!isSearchExactOrg)
                {
                    queryable = queryable.Where(b => b.SearchKey.Contains(q));
                }
            }

            var matches = queryable.ToList();

            if (effectiveOrg != null)
            {
                string targetCode = effectiveOrg.Code;
                bool isSpeciesTarget = !effectiveOrg.IsPanel;

                foreach (var b in matches)
                {
                    if (isSpeciesTarget && b.IsPanel)
                    {
                        b.PanelAppliesNote = $"↳ Applies to {targetCode} via {b.OrganismCode} panel";
                    }
                    else
                    {
                        b.PanelAppliesNote = string.Empty;
                    }
                }

                int panelsCount = matches.Count(b => b.IsPanel);
                int speciesCount = matches.Count - panelsCount;
                ResultCountMessage = $"Showing {matches.Count:N0} breakpoints ({panelsCount} panel, {speciesCount} species-specific) for {effectiveOrg.Name} ({effectiveOrg.Code})";
            }
            else
            {
                foreach (var b in matches)
                {
                    b.PanelAppliesNote = string.Empty;
                }

                ResultCountMessage = SelectedYear != "All Years"
                    ? $"Showing {matches.Count:N0} breakpoints for year {SelectedYear}"
                    : $"Showing {matches.Count:N0} of {_allBreakpoints.Count:N0} breakpoints";
            }

            ResetCollection(FilteredBreakpoints, matches);
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

            if (effectiveOrg != null)
            {
                queryable = queryable.Where(r =>
                {
                    if (effectiveOrg.IsPanel)
                    {
                        if (string.Equals(r.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.MemberSpecies.Any(m => string.Equals(r.OrganismCode, m, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    else
                    {
                        if (string.Equals(r.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.ParentPanels.Any(p => string.Equals(r.OrganismCode, p, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    return false;
                });
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                bool isSearchExactOrg = searchOrgCtx != null && effectiveOrg != null &&
                    string.Equals(searchOrgCtx.Code, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase);

                if (!isSearchExactOrg)
                {
                    queryable = queryable.Where(r => r.SearchKey.Contains(q));
                }
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

            if (effectiveOrg != null)
            {
                queryable = queryable.Where(r =>
                {
                    if (effectiveOrg.IsPanel)
                    {
                        if (string.Equals(r.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.MemberSpecies.Any(m => string.Equals(r.OrganismCode, m, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    else
                    {
                        if (string.Equals(r.OrganismCode, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (effectiveOrg.ParentPanels.Any(p => string.Equals(r.OrganismCode, p, StringComparison.OrdinalIgnoreCase)))
                            return true;
                    }
                    return false;
                });
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                bool isSearchExactOrg = searchOrgCtx != null && effectiveOrg != null &&
                    string.Equals(searchOrgCtx.Code, effectiveOrg.Code, StringComparison.OrdinalIgnoreCase);

                if (!isSearchExactOrg)
                {
                    queryable = queryable.Where(r => r.SearchKey.Contains(q));
                }
            }

            var matches = queryable.ToList();
            ResetCollection(FilteredIntrinsicRules, matches);
            ResultCountMessage = $"Showing {FilteredIntrinsicRules.Count} intrinsic rule(s)";
        }
    }

    [RelayCommand]
    public void CopyTable()
    {
        var sb = new System.Text.StringBuilder();

        switch (SelectedSectionIndex)
        {
            case 0: // Breakpoints
                if (FilteredBreakpoints.Count == 0) return;
                sb.AppendLine("Guidelines\tYear\tMethod\tPotency\tOrganismCode\tOrganismName\tType\tAbxCode\tAbxName\tR\tI\tSDD\tS\tECV\tScope\tSiteOfInfection\tComments");
                foreach (var r in FilteredBreakpoints)
                {
                    sb.AppendLine($"{r.Guidelines}\t{r.Year}\t{r.TestMethod}\t{r.Potency}\t{r.OrganismCode}\t{r.OrganismName}\t{r.BreakpointType}\t{r.AbxCode}\t{r.AbxName}\t{r.RDisplay}\t{r.IDisplay}\t{r.SddDisplay}\t{r.SDisplay}\t{r.EcvEcoffDisplay}\t{r.Scope}\t{r.SiteOfInfection}\t{r.Comments}");
                }
                break;

            case 1: // Expert Rules
                if (FilteredExpertRules.Count == 0) return;
                sb.AppendLine("Guideline\tOrganismCode\tOrganismName\tAffectedDrugs\tExceptions\tDescription");
                foreach (var r in FilteredExpertRules)
                {
                    sb.AppendLine($"{r.Guideline}\t{r.OrganismCode}\t{r.OrganismName}\t{r.AffectedAntibiotics}\t{r.Exceptions}\t{r.Description}");
                }
                break;

            case 2: // Intrinsic Resistance
                if (FilteredIntrinsicRules.Count == 0) return;
                sb.AppendLine("Guideline\tOrganismCode\tOrganismName\tAntibioticCode\tAntibioticName\tExceptions\tComments");
                foreach (var r in FilteredIntrinsicRules)
                {
                    sb.AppendLine($"{r.Guideline}\t{r.OrganismCode}\t{r.OrganismName}\t{r.AbxCode}\t{r.AbxName}\t{r.Exceptions}\t{r.Comments}");
                }
                break;
        }

        if (sb.Length > 0)
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(sb.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

            int count = SelectedSectionIndex switch
            {
                0 => FilteredBreakpoints.Count,
                1 => FilteredExpertRules.Count,
                2 => FilteredIntrinsicRules.Count,
                _ => 0
            };
            ResultCountMessage = $"Copied {count:N0} rows to clipboard (ready to paste in Excel/Sheets).";
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
