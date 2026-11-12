using CommunityToolkit.Mvvm.ComponentModel;

namespace AMRIE.WinUI.Models;

public partial class SelectableItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public SelectableItem() { }

    public SelectableItem(string name, bool isSelected = false)
    {
        Name = name;
        IsSelected = isSelected;
    }
}
