using System;
using System.Text.RegularExpressions;

namespace AMRIE.WinUI.Models;

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
    public string AbxName { get; set; } = string.Empty;
    public string WhonetTest { get; set; } = string.Empty;
    public decimal R { get; set; }
    public string I { get; set; } = string.Empty;
    public string Sdd { get; set; } = string.Empty;
    public decimal S { get; set; }
    public decimal EcvEcoff { get; set; }
    public string Comments { get; set; } = string.Empty;

    public string OrganismCodeType { get; set; } = string.Empty;
    public bool IsPanel => !string.Equals(OrganismCodeType, "WHONET_ORG_CODE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(OrganismCodeType);
    public string ReferenceTable { get; set; } = string.Empty;

    public string Scope
    {
        get
        {
            if (string.Equals(OrganismCodeType, "FAMILY_CODE", StringComparison.OrdinalIgnoreCase))
                return "Family Panel";
            if (string.Equals(OrganismCodeType, "GENUS_CODE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(OrganismCodeType, "GENUS_GROUP", StringComparison.OrdinalIgnoreCase))
                return "Genus Panel";
            if (string.Equals(OrganismCodeType, "SPECIES_GROUP", StringComparison.OrdinalIgnoreCase))
                return "Species Group";
            if (string.Equals(OrganismCodeType, "SEROVAR_GROUP", StringComparison.OrdinalIgnoreCase))
                return "Serovar Group";
            if (string.Equals(OrganismCodeType, "ANAEROBE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(OrganismCodeType, "ANAEROBE+SUBKINGDOM_CODE", StringComparison.OrdinalIgnoreCase))
                return "Anaerobe Panel";
            if (string.Equals(OrganismCodeType, "ALL", StringComparison.OrdinalIgnoreCase))
                return "Global Panel";
            if (!IsPanel)
                return "Species-Specific";
            return $"{OrganismCodeType} Panel";
        }
    }

    /// <summary>
    /// Taxonomic hierarchy specificity rank matching AMRIE Breakpoint.GetApplicableBreakpoints:
    /// 1 = Serovar Group, 2 = Species-Specific, 3 = Species Group, 4 = Genus Panel, 5 = Family Panel, 6 = Anaerobe Panel.
    /// </summary>
    public int SpecificityRank => OrganismCodeType switch
    {
        "SEROVAR_GROUP" => 1,
        "WHONET_ORG_CODE" => 2,
        "SPECIES_GROUP" => 3,
        "GENUS_CODE" => 4,
        "GENUS_GROUP" => 4,
        "FAMILY_CODE" => 5,
        "ANAEROBE+SUBKINGDOM_CODE" => 6,
        "ANAEROBE" => 6,
        _ => IsPanel ? 7 : 2
    };

    public string PanelAppliesNote { get; set; } = string.Empty;
    public bool HasPanelAppliesNote => !string.IsNullOrEmpty(PanelAppliesNote);

    public string RDisplay
    {
        get
        {
            if (R <= 0) return "-";
            bool isDisk = string.Equals(TestMethod, "DISK", StringComparison.OrdinalIgnoreCase);
            return isDisk ? $"≤ {R:G29}" : $"≥ {R:G29}";
        }
    }

    private static readonly Regex RangeDashRegex = new(@"(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)", RegexOptions.Compiled);

    public string IDisplay => !string.IsNullOrWhiteSpace(I) && I != "0" ? RangeDashRegex.Replace(I, "$1\u2013$2") : "-";
    public string SddDisplay => !string.IsNullOrWhiteSpace(Sdd) && Sdd != "0" ? RangeDashRegex.Replace(Sdd, "$1\u2013$2") : "-";

    public string SDisplay
    {
        get
        {
            if (S <= 0) return "-";
            bool isDisk = string.Equals(TestMethod, "DISK", StringComparison.OrdinalIgnoreCase);
            return isDisk ? $"≥ {S:G29}" : $"≤ {S:G29}";
        }
    }

    public string EcvEcoffDisplay => EcvEcoff > 0 ? $"≤ {EcvEcoff:G29}" : "-";

    /// <summary>
    /// Returns true if at least one of the clinical breakpoints (S, I, R, or SDD) is defined (not '-').
    /// </summary>
    public bool HasAnyBreakpoint =>
        R > 0 ||
        S > 0 ||
        (!string.IsNullOrWhiteSpace(I) && I != "-" && I != "0") ||
        (!string.IsNullOrWhiteSpace(Sdd) && Sdd != "-" && Sdd != "0");

    /// <summary>
    /// Pre-computed, lowercased concatenation of all searchable fields.
    /// Used by ApplyFilter to avoid repeated ToLowerInvariant() calls per keystroke.
    /// </summary>
    public string SearchKey { get; }

    public BreakpointRow() { SearchKey = string.Empty; }

    public BreakpointRow(string guidelines, int year, string testMethod, string potency, string organismCode,
        string breakpointType, string host, string siteOfInfection, string abxCode, string whonetTest,
        decimal r, string i, string sdd, decimal s, decimal ecvEcoff, string comments)
        : this(guidelines, year, testMethod, potency, organismCode, string.Empty, breakpointType, host, siteOfInfection, abxCode, string.Empty, whonetTest, r, i, sdd, s, ecvEcoff, comments, string.Empty, string.Empty)
    {
    }

    public BreakpointRow(string guidelines, int year, string testMethod, string potency, string organismCode,
        string organismName, string breakpointType, string host, string siteOfInfection, string abxCode, string whonetTest,
        decimal r, string i, string sdd, decimal s, decimal ecvEcoff, string comments)
        : this(guidelines, year, testMethod, potency, organismCode, organismName, breakpointType, host, siteOfInfection, abxCode, string.Empty, whonetTest, r, i, sdd, s, ecvEcoff, comments, string.Empty, string.Empty)
    {
    }

    public BreakpointRow(string guidelines, int year, string testMethod, string potency, string organismCode,
        string organismName, string breakpointType, string host, string siteOfInfection, string abxCode, string abxName, string whonetTest,
        decimal r, string i, string sdd, decimal s, decimal ecvEcoff, string comments,
        string organismCodeType = "", string referenceTable = "")
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
        AbxName = abxName;
        WhonetTest = whonetTest;
        R = r;
        I = i;
        Sdd = sdd;
        S = s;
        EcvEcoff = ecvEcoff;
        Comments = comments;
        OrganismCodeType = organismCodeType;
        ReferenceTable = referenceTable;

        // Pre-compute once to avoid repeated ToLowerInvariant() per keystroke in filter.
        SearchKey = $"{organismCode} {organismName} {abxCode} {abxName} {guidelines} {whonetTest} {breakpointType} {siteOfInfection} {comments} {organismCodeType} {referenceTable}"
            .ToLowerInvariant();
    }
}
