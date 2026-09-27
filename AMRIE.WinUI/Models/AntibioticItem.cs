namespace AMRIE.WinUI.Models;

public class AntibioticItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public AntibioticItem() { }

    public AntibioticItem(string displayName, string code)
    {
        DisplayName = displayName;
        Code = code;
    }

    public override string ToString() => DisplayName;
}
