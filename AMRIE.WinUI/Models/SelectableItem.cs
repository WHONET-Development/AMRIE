using CommunityToolkit.Mvvm.ComponentModel;

namespace AMRIE.WinUI.Models;

public partial class SelectableItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public SelectableItem() { }

    public SelectableItem(string name, bool isSelected = false)
    {
        Name = name;
        IsSelected = isSelected;
    }
}
