namespace AMRIE.WinUI.Models;

public class IntrinsicRuleRow
{
    public string Guideline { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string AbxCode { get; set; } = string.Empty;
    public string Exceptions { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    public string SearchKey { get; }

    public IntrinsicRuleRow() { SearchKey = string.Empty; }

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

        SearchKey = $"{guideline} {organismCode} {organismName} {abxCode} {comments}".ToLowerInvariant();
    }
}
