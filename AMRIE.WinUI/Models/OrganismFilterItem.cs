namespace AMRIE.WinUI.Models;

public class OrganismFilterItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPanel { get; set; }
    public string Group { get; set; } = string.Empty;

    public string DisplayText => Code == "ALL" ? "All Organisms & Panels" : $"{Code} — {Name}";

    public override string ToString() => DisplayText;
}

