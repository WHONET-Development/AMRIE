namespace AMRIE.WinUI.Models;

public class ExpertRuleRow
{
    public string RuleCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string AffectedAntibiotics { get; set; } = string.Empty;
    public string Exceptions { get; set; } = string.Empty;

    public string SearchKey { get; }

    public ExpertRuleRow() { SearchKey = string.Empty; }

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

        SearchKey = $"{ruleCode} {description} {organismCode} {organismName} {affectedAntibiotics}".ToLowerInvariant();
    }
}
