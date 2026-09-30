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
        // Configure default restore-state dimensions before maximizing so restoring the window returns to a sensible size
        WindowHelper.SetWindowSize(this, 1150, 780);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        try
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }
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

    /// <summary>Raised whenever the app theme changes. Subscribers receive the new <see cref="ElementTheme"/>.</summary>
    public static event Action<ElementTheme>? ThemeChanged;

    public ElementTheme CurrentTheme
    {
        get => RootGrid.RequestedTheme;
        set
        {
            RootGrid.RequestedTheme = value;
            UpdateThemeButtonVisuals();
            ThemeChanged?.Invoke(value);
        }
    }

    private DateTime _lastThemeToggle = DateTime.MinValue;

    private void ThemeToggleItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        ToggleTheme();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer == ThemeToggleItem || (args.InvokedItem as string) == ThemeToggleItem.Content as string)
        {
            ToggleTheme();
        }
    }

    public void ToggleTheme()
    {
        if ((DateTime.UtcNow - _lastThemeToggle).TotalMilliseconds < 250)
        {
            return;
        }
        _lastThemeToggle = DateTime.UtcNow;

        if (RootGrid.ActualTheme == ElementTheme.Dark)
        {
            CurrentTheme = ElementTheme.Light;
        }
        else
        {
            CurrentTheme = ElementTheme.Dark;
        }
    }

    public void UpdateThemeButtonVisuals()
    {
        bool isDark = RootGrid.RequestedTheme == ElementTheme.Dark ||
                     (RootGrid.RequestedTheme == ElementTheme.Default && RootGrid.ActualTheme == ElementTheme.Dark);

        if (isDark)
        {
            ThemeIcon.Glyph = "\uE706"; // Light/Sun icon
            ThemeToggleItem.Content = "Light mode";
            ToolTipService.SetToolTip(ThemeToggleItem, "Switch to Light mode");
        }
        else
        {
            ThemeIcon.Glyph = "\uE708"; // Moon icon
            ThemeToggleItem.Content = "Dark mode";
            ToolTipService.SetToolTip(ThemeToggleItem, "Switch to Dark mode");
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
