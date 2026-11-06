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
    }

    private void OrganismSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string query = sender.Text.Trim();
            ViewModel.OrganismSearchText = sender.Text;
            ViewModel.SelectedOrganism = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
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

    private void AntibioticSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string query = sender.Text.Trim();
            ViewModel.AntibioticSearchText = sender.Text;
            ViewModel.SelectedAntibiotic = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
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

    private async void GetApplicableBreakpoints_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isDialogOpen) return;
        _isDialogOpen = true;
        try
        {
            ViewModel.SyncInputs(OrganismSuggestBox.Text, AntibioticSuggestBox.Text);
            var (rows, summary) = ViewModel.GetApplicableBreakpoints();
            var dialog = new Dialogs.ApplicableBreakpointsDialog
            {
                XamlRoot = this.XamlRoot ?? (Content as Microsoft.UI.Xaml.UIElement)?.XamlRoot
            };
            dialog.SetData(rows, summary);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AMRIE] Error displaying ApplicableBreakpointsDialog: {ex}");
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private async void GetApplicableIntrinsicResistance_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isDialogOpen) return;
        _isDialogOpen = true;
        try
        {
            ViewModel.SyncInputs(OrganismSuggestBox.Text, AntibioticSuggestBox.Text);
            var (rows, summary) = ViewModel.GetApplicableIntrinsicRules();
            var dialog = new Dialogs.ApplicableIntrinsicRulesDialog
            {
                XamlRoot = this.XamlRoot ?? (Content as Microsoft.UI.Xaml.UIElement)?.XamlRoot
            };
            dialog.SetData(rows, summary);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AMRIE] Error displaying ApplicableIntrinsicRulesDialog: {ex}");
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
}
