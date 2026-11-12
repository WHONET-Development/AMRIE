using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using AMRIE.WinUI.ViewModels;
using AMRIE.WinUI.Models;

namespace AMRIE.WinUI.Pages;

public sealed partial class SingleInterpretationPage : Page
{
    public SingleInterpretationPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.OrganismSearchText))
            {
                if (OrganismSuggestBox.Text != ViewModel.OrganismSearchText)
                {
                    OrganismSuggestBox.Text = ViewModel.OrganismSearchText ?? string.Empty;
                }
            }
            else if (e.PropertyName == nameof(ViewModel.AntibioticSearchText))
            {
                if (AntibioticSuggestBox.Text != ViewModel.AntibioticSearchText)
                {
                    AntibioticSuggestBox.Text = ViewModel.AntibioticSearchText ?? string.Empty;
                }
            }
        };
    }

    private async void OrganismSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string rawText = sender.Text;
            string query = rawText.Trim();
            ViewModel.OrganismSearchText = rawText;
            ViewModel.SelectedOrganism = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                return;
            }

            if (ViewModel.AllOrganisms.Count == 0)
            {
                await ViewModel.EnsureDataLoadedAsync();
                if (!string.Equals(sender.Text, rawText, StringComparison.Ordinal))
                    return;
            }

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = ViewModel.AllOrganisms
                .Where(o => terms.All(t => o.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           o.Code.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(50)
                .ToList();

            sender.ItemsSource = matches;
        }
    }

    private void OrganismSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is OrganismItem chosen)
        {
            ViewModel.SelectedOrganism = chosen;
            ViewModel.OrganismSearchText = chosen.DisplayName;
            sender.Text = chosen.DisplayName;
        }
    }

    private void OrganismSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is OrganismItem chosen)
        {
            ViewModel.SelectedOrganism = chosen;
            ViewModel.OrganismSearchText = chosen.DisplayName;
            sender.Text = chosen.DisplayName;
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var match = ViewModel.ResolveOrganism(args.QueryText);
            if (match != null)
            {
                ViewModel.SelectedOrganism = match;
                ViewModel.OrganismSearchText = match.DisplayName;
                sender.Text = match.DisplayName;
            }
        }
    }

    private async void AntibioticSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string rawText = sender.Text;
            string query = rawText.Trim();
            ViewModel.AntibioticSearchText = rawText;
            ViewModel.SelectedAntibiotic = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                return;
            }

            if (ViewModel.AllAntibiotics.Count == 0)
            {
                await ViewModel.EnsureDataLoadedAsync();
                if (!string.Equals(sender.Text, rawText, StringComparison.Ordinal))
                    return;
            }

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = ViewModel.AllAntibiotics
                .Where(a => terms.All(t => a.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           a.Code.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(50)
                .ToList();

            sender.ItemsSource = matches;
        }
    }

    private void AntibioticSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is AntibioticItem chosen)
        {
            ViewModel.SelectedAntibiotic = chosen;
            ViewModel.AntibioticSearchText = chosen.DisplayName;
            sender.Text = chosen.DisplayName;
        }
    }

    private void AntibioticSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is AntibioticItem chosen)
        {
            ViewModel.SelectedAntibiotic = chosen;
            ViewModel.AntibioticSearchText = chosen.DisplayName;
            sender.Text = chosen.DisplayName;
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var match = ViewModel.ResolveAntibiotic(args.QueryText);
            if (match != null)
            {
                ViewModel.SelectedAntibiotic = match;
                ViewModel.AntibioticSearchText = match.DisplayName;
                sender.Text = match.DisplayName;
            }
        }
    }

    private void MeasurementTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (ViewModel.InterpretCommand.CanExecute(null))
            {
                ViewModel.InterpretCommand.Execute(null);
            }
        }
    }

    private static bool _isDialogOpen = false;

    private void MoveTypeUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => SingleInterpretationViewModel.MovePriorityItem(ViewModel.BreakpointTypes, (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as SelectableItem, -1);

    private void MoveTypeDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => SingleInterpretationViewModel.MovePriorityItem(ViewModel.BreakpointTypes, (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as SelectableItem, 1);

    private void MoveSiteUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => SingleInterpretationViewModel.MovePriorityItem(ViewModel.SitesOfInfection, (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as SelectableItem, -1);

    private void MoveSiteDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => SingleInterpretationViewModel.MovePriorityItem(ViewModel.SitesOfInfection, (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as SelectableItem, 1);

    private async System.Threading.Tasks.Task ShowLookupDialogAsync<TDialog>(Func<TDialog> createDialog, string logName)
        where TDialog : ContentDialog
    {
        if (_isDialogOpen) return;
        _isDialogOpen = true;
        try
        {
            await ViewModel.EnsureDataLoadedAsync();
            ViewModel.SyncInputs(OrganismSuggestBox.Text, AntibioticSuggestBox.Text);
            var dialog = createDialog();
            Common.WindowHelper.ApplyDialogTheme(dialog, this);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AMRIE] Error displaying {logName}: {ex}");
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private async void GetApplicableBreakpoints_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ShowLookupDialogAsync(() =>
        {
            var (rows, summary) = ViewModel.GetApplicableBreakpoints();
            var dialog = new Dialogs.ApplicableBreakpointsDialog();
            dialog.SetData(rows, summary);
            return dialog;
        }, nameof(Dialogs.ApplicableBreakpointsDialog));
    }

    private async void GetApplicableExpertRules_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ShowLookupDialogAsync(() =>
        {
            var (rows, summary) = ViewModel.GetApplicableExpertRules();
            var dialog = new Dialogs.ApplicableExpertRulesDialog();
            dialog.SetData(rows, summary);
            return dialog;
        }, nameof(Dialogs.ApplicableExpertRulesDialog));
    }

    private async void GetApplicableIntrinsicResistance_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ShowLookupDialogAsync(() =>
        {
            var (rows, summary) = ViewModel.GetApplicableIntrinsicRules();
            var dialog = new Dialogs.ApplicableIntrinsicRulesDialog();
            dialog.SetData(rows, summary);
            return dialog;
        }, nameof(Dialogs.ApplicableIntrinsicRulesDialog));
    }
}
