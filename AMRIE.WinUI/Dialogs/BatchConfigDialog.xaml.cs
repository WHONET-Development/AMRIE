using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AMRIE.Library;
using AMRIE.WinUI.Common;
using AMRIE.WinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace AMRIE.WinUI.Dialogs;

public sealed partial class BatchConfigDialog : ContentDialog
{
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed class BatchConfigFileModel
    {
        public bool RoundHalfDilutions { get; set; } = true;
        public bool IncludeInterpretationComments { get; set; } = false;
        public bool UseIntrinsicResistanceRules { get; set; } = true;
        public List<string>? EnabledExpertInterpretationRules { get; set; }
        public long GuidelineYear { get; set; } = Constants.BreakpointTableRevisionYear;
        public List<string>? PrioritizedBreakpointTypes { get; set; }
        public List<string>? PrioritizedSitesOfInfection { get; set; }
        public List<string>? DisabledSitesOfInfection { get; set; }
        public bool HorizontalAntibioticResults { get; set; } = true;
        public string? UserDefinedBreakpointsFile { get; set; }
    }

    private readonly ObservableCollection<SelectableItem> _breakpointTypes = [];
    private readonly ObservableCollection<SelectableItem> _expertRules = [];
    private readonly ObservableCollection<SelectableItem> _sites = [];
    private List<string>? _disabledSites;
    private string _currentPath = string.Empty;

    public string? SavedConfigPath { get; private set; }

    public BatchConfigDialog()
    {
        InitializeComponent();
        BreakpointTypesList.ItemsSource = _breakpointTypes;
        ExpertRulesList.ItemsSource = _expertRules;
        SitesList.ItemsSource = _sites;
    }

    public void LoadFromFile(string? configPath)
    {
        _currentPath = configPath?.Trim() ?? string.Empty;
        InterpretationConfiguration config;

        try
        {
            if (!string.IsNullOrWhiteSpace(_currentPath) && File.Exists(_currentPath))
            {
                config = InterpretationConfiguration.ReadConfiguration(_currentPath);
            }
            else
            {
                config = new InterpretationConfiguration
                {
                    GuidelineYear = Constants.BreakpointTableRevisionYear,
                    RoundHalfDilutions = true,
                    UseIntrinsicResistanceRules = true,
                    HorizontalAntibioticResults = true
                };
            }
        }
        catch (Exception ex)
        {
            config = new InterpretationConfiguration
            {
                GuidelineYear = Constants.BreakpointTableRevisionYear,
                RoundHalfDilutions = true,
                UseIntrinsicResistanceRules = true,
                HorizontalAntibioticResults = true
            };
            HintInfoBar.Severity = InfoBarSeverity.Warning;
            HintInfoBar.Message = $"Could not parse '{Path.GetFileName(_currentPath)}' ({ex.Message}). Loaded defaults.";
            HintInfoBar.IsOpen = true;
        }

        if (IsInsideAppBaseDirectory(_currentPath))
        {
            HintInfoBar.Severity = InfoBarSeverity.Informational;
            HintInfoBar.Message = "Bundled SampleConfig.json cannot be overwritten directly; clicking Save will prompt Save As...";
            HintInfoBar.IsOpen = true;
        }

        GuidelineYearBox.Value = config.GuidelineYear > 0 ? config.GuidelineYear : Constants.BreakpointTableRevisionYear;
        RoundHalfDilutionsCheck.IsChecked = config.RoundHalfDilutions;
        IncludeCommentsCheck.IsChecked = config.IncludeInterpretationComments;
        UseIntrinsicRulesCheck.IsChecked = config.UseIntrinsicResistanceRules;
        HorizontalResultsCheck.IsChecked = config.HorizontalAntibioticResults;
        UserDefinedFileBox.Text = config.UserDefinedBreakpointsFile ?? string.Empty;
        _disabledSites = config.DisabledSitesOfInfection?.ToList();

        // Populate Breakpoint Types in configured priority order, followed by remaining types
        _breakpointTypes.Clear();
        var allTypes = new[]
        {
            Breakpoint.BreakpointTypes.Human,
            Breakpoint.BreakpointTypes.Animal,
            Breakpoint.BreakpointTypes.ECOFF
        };
        var configuredTypes = config.PrioritizedBreakpointTypes ?? [];
        foreach (var t in configuredTypes)
        {
            if (allTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
                _breakpointTypes.Add(new SelectableItem(t, true));
        }
        foreach (var t in allTypes)
        {
            if (!configuredTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
                _breakpointTypes.Add(new SelectableItem(t, configuredTypes.Count == 0 && t == Breakpoint.BreakpointTypes.Human));
        }

        // Populate Expert Rules
        _expertRules.Clear();
        var enabledSet = config.EnabledExpertInterpretationRules?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        FilterExpertRulesCheck.IsChecked = enabledSet != null;
        var ruleCodes = ExpertInterpretationRule.ExpertInterpretationRules
            .Select(r => r.RULE_CODE)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        if (enabledSet != null)
        {
            foreach (var c in enabledSet.Where(c => !ruleCodes.Contains(c, StringComparer.OrdinalIgnoreCase)))
                ruleCodes.Add(c);
        }
        foreach (var code in ruleCodes)
        {
            bool selected = enabledSet == null || enabledSet.Contains(code);
            _expertRules.Add(new SelectableItem(code, selected));
        }
        ExpertRulesList.IsEnabled = FilterExpertRulesCheck.IsChecked == true;

        // Populate Sites of Infection in configured priority order, followed by remaining sites
        _sites.Clear();
        var configuredSites = config.PrioritizedSitesOfInfection ?? [];
        foreach (var s in configuredSites)
        {
            _sites.Add(new SelectableItem(s, true));
        }
        foreach (var s in Constants.SitesOfInfection.DefaultOrder)
        {
            if (!configuredSites.Contains(s, StringComparer.OrdinalIgnoreCase))
                _sites.Add(new SelectableItem(s, false));
        }
    }

    private void FilterExpertRulesCheck_Changed(object sender, RoutedEventArgs e)
    {
        ExpertRulesList.IsEnabled = FilterExpertRulesCheck.IsChecked == true;
    }

    private async void BrowseUserBreakpoints_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            WindowHelper.InitializeWithMainWindow(picker);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                UserDefinedFileBox.Text = file.Path;
            }
        }
        catch { }
    }

    private void ClearUserBreakpoints_Click(object sender, RoutedEventArgs e)
    {
        UserDefinedFileBox.Text = string.Empty;
    }

    private BatchConfigFileModel BuildConfigModel()
    {
        var selectedTypes = _breakpointTypes.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        var selectedSites = _sites.Where(s => s.IsSelected).Select(s => s.Name).ToList();
        List<string>? enabledRules = FilterExpertRulesCheck.IsChecked == true
            ? [.. _expertRules.Where(r => r.IsSelected).Select(r => r.Name)]
            : null;

        return new BatchConfigFileModel
        {
            RoundHalfDilutions = RoundHalfDilutionsCheck.IsChecked ?? true,
            IncludeInterpretationComments = IncludeCommentsCheck.IsChecked ?? false,
            UseIntrinsicResistanceRules = UseIntrinsicRulesCheck.IsChecked ?? true,
            EnabledExpertInterpretationRules = enabledRules,
            GuidelineYear = double.IsNaN(GuidelineYearBox.Value) ? Constants.BreakpointTableRevisionYear : Convert.ToInt64(GuidelineYearBox.Value),
            PrioritizedBreakpointTypes = selectedTypes,
            PrioritizedSitesOfInfection = selectedSites,
            DisabledSitesOfInfection = _disabledSites ?? [],
            HorizontalAntibioticResults = HorizontalResultsCheck.IsChecked ?? true,
            UserDefinedBreakpointsFile = string.IsNullOrWhiteSpace(UserDefinedFileBox.Text) ? null : UserDefinedFileBox.Text.Trim()
        };
    }

    private static bool IsInsideAppBaseDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            string full = Path.GetFullPath(path);
            string baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (string.IsNullOrWhiteSpace(_currentPath) || IsInsideAppBaseDirectory(_currentPath))
            {
                if (!await SaveAsAsync())
                    args.Cancel = true;
                return;
            }

            WriteConfigToPath(_currentPath);
            SavedConfigPath = _currentPath;
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            HintInfoBar.Severity = InfoBarSeverity.Error;
            HintInfoBar.Message = $"Failed to save configuration: {ex.Message}";
            HintInfoBar.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (!await SaveAsAsync())
                args.Cancel = true;
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            HintInfoBar.Severity = InfoBarSeverity.Error;
            HintInfoBar.Message = $"Failed to save configuration: {ex.Message}";
            HintInfoBar.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task<bool> SaveAsAsync()
    {
        var picker = new FileSavePicker();
        WindowHelper.InitializeWithMainWindow(picker);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("JSON Configuration", [".json"]);
        picker.SuggestedFileName = "amrie-config.json";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return false;

        WriteConfigToPath(file.Path);
        SavedConfigPath = file.Path;
        return true;
    }

    private void WriteConfigToPath(string path)
    {
        string json = JsonSerializer.Serialize(BuildConfigModel(), WriteJsonOptions);
        File.WriteAllText(path, json);
    }

    private static void MoveCollectionItem(ObservableCollection<SelectableItem> collection, object sender, int delta)
    {
        if (sender is FrameworkElement { Tag: SelectableItem item })
        {
            int idx = collection.IndexOf(item);
            int targetIdx = idx + delta;
            if (idx >= 0 && targetIdx >= 0 && targetIdx < collection.Count)
                collection.Move(idx, targetIdx);
        }
    }

    private void MoveTypeUp_Click(object sender, RoutedEventArgs e) => MoveCollectionItem(_breakpointTypes, sender, -1);
    private void MoveTypeDown_Click(object sender, RoutedEventArgs e) => MoveCollectionItem(_breakpointTypes, sender, 1);
    private void MoveSiteUp_Click(object sender, RoutedEventArgs e) => MoveCollectionItem(_sites, sender, -1);
    private void MoveSiteDown_Click(object sender, RoutedEventArgs e) => MoveCollectionItem(_sites, sender, 1);
}
