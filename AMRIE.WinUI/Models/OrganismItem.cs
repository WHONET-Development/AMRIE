namespace AMRIE.WinUI.Models;

public class OrganismItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public OrganismItem() { }

    public OrganismItem(string displayName, string code)
    {
        DisplayName = displayName;
        Code = code;
    }

    public override string ToString() => DisplayName;
}
