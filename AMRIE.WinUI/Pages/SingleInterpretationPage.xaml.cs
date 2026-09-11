using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using AMRIE.WinUI.ViewModels;

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
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                return;
            }

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = ViewModel.AllOrganisms
                .Where(o => terms.All(t => o.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           o.Code.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(25)
                .ToList();

            sender.ItemsSource = matches;
        }
    }

    private void OrganismSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is OrganismItem chosen)
        {
            ViewModel.SelectedOrganism = chosen;
            sender.Text = chosen.DisplayName;
        }
    }

    private void AntibioticSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string query = sender.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                return;
            }

            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = ViewModel.AllAntibiotics
                .Where(a => terms.All(t => a.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                           a.Code.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(25)
                .ToList();

            sender.ItemsSource = matches;
        }
    }

    private void AntibioticSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is AntibioticItem chosen)
        {
            ViewModel.SelectedAntibiotic = chosen;
            sender.Text = chosen.DisplayName;
        }
    }
}
