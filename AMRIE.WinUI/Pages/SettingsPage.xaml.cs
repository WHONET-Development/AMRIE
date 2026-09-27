using System;
using System.Diagnostics;
using System.Reflection;
using AMR_Engine;
using AMRIE.WinUI.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = false;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
        VersionTextBlock.Text = $"Version: {GetAppVersion()} (WinUI 3 Edition)";
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        SyncThemeSelection();
        MainWindow.ThemeChanged += OnThemeChanged;
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        MainWindow.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(ElementTheme _) => SyncThemeSelection();

    private void SyncThemeSelection()
    {
        _isInitializing = true;
        try
        {
            var currentTheme = (WindowHelper.MainWindow as MainWindow)?.CurrentTheme ?? ElementTheme.Default;
            ThemeRadioButtons.SelectedIndex = currentTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            if (!string.IsNullOrEmpty(fvi.ProductVersion))
            {
                var clean = fvi.ProductVersion.Split('+')[0];
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    return clean;
                }
            }

            var v = asm.GetName().Version;
            if (v != null)
            {
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }
        catch
        {
        }

        return "26.9.27";
    }

    private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (ThemeRadioButtons.SelectedItem is RadioButton selected && selected.Tag is string tag)
        {
            if (Enum.TryParse<ElementTheme>(tag, out var theme))
            {
                if (WindowHelper.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.CurrentTheme = theme;
                }
                else if (WindowHelper.MainWindow?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                }
            }
        }
    }
}
