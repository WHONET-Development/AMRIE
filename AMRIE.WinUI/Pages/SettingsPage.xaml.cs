using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AMRIE.Library;
using AMRIE.WinUI.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = false;
    private bool _isLicenseDialogOpen = false;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
        VersionTextBlock.Text = $"Version: {GetAppVersion()} (WinUI 3 Edition)";
        ResourceSummaryTextBlock.Text = BuildResourceSummary();
    }

    private static string BuildResourceSummary()
    {
        try
        {
            return $"Guideline Table Revision: {Constants.BreakpointTableRevisionYear}.{Constants.BreakpointTableRevisionMinorChangeNumber}  •  " +
                   $"{Breakpoint.Breakpoints.Count:N0} breakpoints  •  " +
                   $"{Organism.AllOrganisms.Count:N0} organisms  •  " +
                   $"{Antibiotic.AllAntibiotics.Count:N0} antibiotics  •  " +
                   $"{ExpertInterpretationRule.ExpertInterpretationRules.Count:N0} expert rules  •  " +
                   $"{ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules.Count:N0} intrinsic rules";
        }
        catch
        {
            return $"Guideline Table Revision: {Constants.BreakpointTableRevisionYear}.{Constants.BreakpointTableRevisionMinorChangeNumber}";
        }
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

    private void ViewClinicalAttribution_Click(object sender, RoutedEventArgs e)
        => ShowBundledLicenseDialog("Clinical Data Attribution", "CLINICAL-DATA-ATTRIBUTION.md");

    private void ViewThirdPartyNotices_Click(object sender, RoutedEventArgs e)
        => ShowBundledLicenseDialog("Third-Party Software Notices", "THIRD-PARTY-NOTICES.md");

    private void ViewMgbLicense_Click(object sender, RoutedEventArgs e)
        => ShowBundledLicenseDialog("Mass General Brigham Open Access License 1.0", "LICENSE-MGB.txt");

    private async void ShowBundledLicenseDialog(string title, string fileName)
    {
        if (_isLicenseDialogOpen) return;
        _isLicenseDialogOpen = true;
        try
        {
            string licensePath = Path.Combine(AppContext.BaseDirectory, "licenses", fileName);
            bool exists = File.Exists(licensePath);
            string body = exists
                ? await File.ReadAllTextAsync(licensePath)
                : $"License file not found at:\n{licensePath}";

            UIElement contentElement = exists && fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? MarkdownViewerHelper.BuildMarkdownView(body, Path.GetDirectoryName(licensePath) ?? AppContext.BaseDirectory)
                : new TextBlock
                {
                    Text = body,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                };

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 460,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = contentElement,
                Padding = new Thickness(0, 0, 12, 0)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Right,
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetRow(closeButton, 1);

            var layout = new Grid
            {
                Width = 680,
                RowSpacing = 12
            };
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(scrollViewer);
            layout.Children.Add(closeButton);

            var dialog = new ContentDialog
            {
                Title = title,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Content = layout
            };
            dialog.Resources["ContentDialogMaxWidth"] = 760d;
            WindowHelper.ApplyDialogTheme(dialog, this);
            WindowHelper.ConfigureInlineCloseDialog(dialog, closeButton);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AMRIE] Error displaying license dialog ({fileName}): {ex}");
        }
        finally
        {
            _isLicenseDialogOpen = false;
        }
    }

    private async void OpenThirdPartyNotices(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, @"licenses\THIRD-PARTY-NOTICES.md");
        try
        {
            if (!System.IO.File.Exists(path))
                throw new System.IO.FileNotFoundException("Third-party notices file not found.", path);

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (System.IO.FileNotFoundException)
        {
            var dialog = new ContentDialog
            {
                Title = "File Not Found",
                Content = "Third-party notices file not found. Please reinstall the application.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
