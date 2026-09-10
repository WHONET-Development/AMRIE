using System;
using System.Collections.Generic;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AMRIE.WinUI.Common;
using AMRIE.WinUI.Pages;
using AMRIE.WinUI.ViewModels;

namespace AMRIE.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        WindowHelper.MainWindow = this;
        WindowHelper.SetWindowSize(this, 1150, 780);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            AppWindow.SetIcon("Assets/microscope.ico");
        }
        catch { }

        // Initialize theme toggle visuals
        UpdateThemeButtonVisuals();

        // Default navigation to Single Interpretation
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            Type? pageType = tag switch
            {
                "SingleInterpretation" => typeof(SingleInterpretationPage),
                "BatchProcessing" => typeof(BatchProcessingPage),
                "ResourceExplorer" => typeof(ResourceExplorerPage),
                _ => null
            };

            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootGrid.ActualTheme == ElementTheme.Dark)
        {
            RootGrid.RequestedTheme = ElementTheme.Light;
        }
        else
        {
            RootGrid.RequestedTheme = ElementTheme.Dark;
        }

        UpdateThemeButtonVisuals();
    }

    public void UpdateThemeButtonVisuals()
    {
        bool isDark = RootGrid.RequestedTheme == ElementTheme.Dark ||
                     (RootGrid.RequestedTheme == ElementTheme.Default && RootGrid.ActualTheme == ElementTheme.Dark);

        if (isDark)
        {
            ThemeIcon.Glyph = "\uE706"; // Light/Sun icon
            ThemeText.Text = "Light mode";
            ToolTipService.SetToolTip(ThemeToggleButton, "Switch to Light mode");
        }
        else
        {
            ThemeIcon.Glyph = "\uE708"; // Moon icon
            ThemeText.Text = "Dark mode";
            ToolTipService.SetToolTip(ThemeToggleButton, "Switch to Dark mode");
        }

        UpdateTitleBarColors(isDark);
    }

    private void UpdateTitleBarColors(bool isDark)
    {
        try
        {
            var titleBar = AppWindow.TitleBar;
            if (titleBar != null)
            {
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

                if (isDark)
                {
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(120, 255, 255, 255);
                }
                else
                {
                    titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(230, 20, 20, 20);
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 0, 0, 0);
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(120, 0, 0, 0);
                }
            }
        }
        catch { }
    }
}
