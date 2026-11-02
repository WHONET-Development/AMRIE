namespace AMRIE.WinUI.Models;

public class IntrinsicRuleRow
{
    public string Guideline { get; set; } = string.Empty;
    public string OrganismCode { get; set; } = string.Empty;
    public string OrganismName { get; set; } = string.Empty;
    public string AbxCode { get; set; } = string.Empty;
    public string AbxName { get; set; } = string.Empty;
    public string Exceptions { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    public string SearchKey { get; }

    public IntrinsicRuleRow() { SearchKey = string.Empty; }

    public IntrinsicRuleRow(string guideline, string organismCode, string abxCode, string exceptions, string comments)
        : this(guideline, organismCode, string.Empty, abxCode, string.Empty, exceptions, comments)
    {
    }

    public IntrinsicRuleRow(string guideline, string organismCode, string organismName, string abxCode, string exceptions, string comments)
        : this(guideline, organismCode, organismName, abxCode, string.Empty, exceptions, comments)
    {
    }

    public IntrinsicRuleRow(string guideline, string organismCode, string organismName, string abxCode, string abxName, string exceptions, string comments)
    {
        Guideline = guideline;
        OrganismCode = organismCode;
        OrganismName = organismName;
        AbxCode = abxCode;
        AbxName = abxName;
        Exceptions = exceptions;
        Comments = comments;

        SearchKey = $"{guideline} {organismCode} {organismName} {abxCode} {abxName} {comments}".ToLowerInvariant();
    }
}
