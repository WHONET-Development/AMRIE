namespace AMRIE.WinUI.Models;

public class InterpretationResultItem
{
    public string TestCode { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
    public string ResultBadge { get; set; } = string.Empty;
    public string StatusSeverity { get; set; } = "Informational"; // Success, Warning, Error, Informational
    public string Comments { get; set; } = string.Empty;

    public string BadgeBackground => ResultBadge.Trim() switch
    {
        "S" => "#107C41",  // Green (Susceptible)
        "I" or "SDD" or "S-DD" => "#D83B01",  // Orange (Intermediate / SDD)
        "R" => "#A80000",  // Red (Resistant)
        _ => "#0067C0"     // Fluent Accent Blue
    };

    public string BadgeForeground => "#FFFFFF";
}
