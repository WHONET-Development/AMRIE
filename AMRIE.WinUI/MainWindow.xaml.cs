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
        var savedSettings = SettingsManager.LoadSettings();
        WindowHelper.RestoreWindowState(this, savedSettings);
        Closed += (_, _) => WindowHelper.SaveWindowState(this);

        // Ctrl+, (VK_OEM_COMMA = 188) is not a named VirtualKey enum member in XAML
        var settingsAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = (Windows.System.VirtualKey)188,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        settingsAccelerator.Invoked += NavShortcut_Invoked;
        RootGrid.KeyboardAccelerators.Add(settingsAccelerator);

        ExtendsContentIntoTitleBar = false;

        try
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }
        }
        catch { }

        RootGrid.ActualThemeChanged += (s, e) =>
        {
            if (RootGrid.RequestedTheme == ElementTheme.Default)
            {
                bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;
                UpdateTitleBarTheme(isDark);
            }
        };

        // Apply persisted theme from already-loaded settings (avoids a duplicate settings.json read)
        ElementTheme savedTheme = ElementTheme.Default;
        if (!string.IsNullOrEmpty(savedSettings.Theme) && Enum.TryParse<ElementTheme>(savedSettings.Theme, out var parsedTheme))
        {
            savedTheme = parsedTheme;
        }
        RootGrid.RequestedTheme = savedTheme;

        // Initialize theme toggle visuals
        UpdateThemeButtonVisuals();

        // Defer initial page inflation until the window is activated so the first frame paints immediately
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            if (NavView.SelectedItem == null && NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        });
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
            SettingsManager.SaveTheme(value);
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

        CurrentTheme = RootGrid.RequestedTheme switch
        {
            ElementTheme.Default => ElementTheme.Light,
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Default,
            _ => ElementTheme.Default
        };
    }

    private void ThemeMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.RadioMenuFlyoutItem item && item.Tag is string tag)
        {
            if (Enum.TryParse<ElementTheme>(tag, out var theme))
            {
                CurrentTheme = theme;
            }
        }
    }

    public void UpdateThemeButtonVisuals()
    {
        var requested = RootGrid.RequestedTheme;

        switch (requested)
        {
            case ElementTheme.Light:
                ThemeIcon.Glyph = "\uE706"; // Light/Sun icon
                ThemeToggleItem.Content = "Light mode";
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ThemeToggleItem, "Theme: Light mode (click to switch to Dark)");
                if (LightThemeMenuItem != null) LightThemeMenuItem.IsChecked = true;
                break;

            case ElementTheme.Dark:
                ThemeIcon.Glyph = "\uE708"; // Moon icon
                ThemeToggleItem.Content = "Dark mode";
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ThemeToggleItem, "Theme: Dark mode (click to switch to System)");
                if (DarkThemeMenuItem != null) DarkThemeMenuItem.IsChecked = true;
                break;

            case ElementTheme.Default:
            default:
                ThemeIcon.Glyph = "\uE790"; // Personalize / Theme icon
                ThemeToggleItem.Content = "System theme";
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ThemeToggleItem, "Theme: Use system setting (click to switch to Light)");
                if (SystemThemeMenuItem != null) SystemThemeMenuItem.IsChecked = true;
                break;
        }

        bool isDark = RootGrid.RequestedTheme == ElementTheme.Dark ||
                     (RootGrid.RequestedTheme == ElementTheme.Default && RootGrid.ActualTheme == ElementTheme.Dark);

        UpdateTitleBarTheme(isDark);
    }

    private void UpdateTitleBarTheme(bool isDark)
    {
        WindowHelper.SetNativeTitleBarTheme(this, isDark);
    }

    private void NavShortcut_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        switch (sender.Key)
        {
            case Windows.System.VirtualKey.Number1:
                NavView.SelectedItem = NavView.MenuItems[0];
                args.Handled = true;
                break;
            case Windows.System.VirtualKey.Number2:
                NavView.SelectedItem = NavView.MenuItems[1];
                args.Handled = true;
                break;
            case Windows.System.VirtualKey.Number3:
                NavView.SelectedItem = NavView.MenuItems[2];
                args.Handled = true;
                break;
            case (Windows.System.VirtualKey)188:
                NavView.SelectedItem = NavView.SettingsItem;
                args.Handled = true;
                break;
        }
    }
}
