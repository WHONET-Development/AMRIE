using System;
using AMR_Engine;
using AMRIE.WinUI.Common;
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
                if (WindowHelper.MainWindow?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                    (WindowHelper.MainWindow as MainWindow)?.UpdateThemeButtonVisuals();
                }
                else if (XamlRoot?.Content is FrameworkElement fallbackRoot)
                {
                    fallbackRoot.RequestedTheme = theme;
                }
            }
        }
    }
}
