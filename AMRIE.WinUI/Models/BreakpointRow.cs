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

    /// <summary>
    /// Pre-computed, lowercased concatenation of all searchable fields.
    /// Used by ApplyFilter to avoid repeated ToLowerInvariant() calls per keystroke.
    /// </summary>
    public string SearchKey { get; }

    public BreakpointRow() { SearchKey = string.Empty; }

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

        // Pre-compute once to avoid repeated ToLowerInvariant() per keystroke in filter.
        SearchKey = $"{organismCode} {organismName} {abxCode} {guidelines} {whonetTest} {breakpointType} {siteOfInfection} {comments}"
            .ToLowerInvariant();
    }
}
