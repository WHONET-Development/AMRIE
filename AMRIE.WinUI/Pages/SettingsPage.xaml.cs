using System;
using AMR_Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeRadioButtons.SelectedItem is RadioButton selected && selected.Tag is string tag)
        {
            if (Enum.TryParse<ElementTheme>(tag, out var theme))
            {
                if (XamlRoot?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                }
            }
        }
    }
}
