using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AMRIE.WinUI.ViewModels;

namespace AMRIE.WinUI.Pages;

public sealed partial class ResourceExplorerPage : Page
{
    public ResourceExplorerPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        SyncSelectorBarWithViewModel();
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        if (DataContext is ResourceExplorerViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResourceExplorerViewModel.SelectedSectionIndex))
        {
            SyncSelectorBarWithViewModel();
        }
    }

    private void SectionSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is SelectorBarItem item)
        {
            int index = sender.Items.IndexOf(item);
            if (index >= 0 && ViewModel.SelectedSectionIndex != index)
            {
                ViewModel.SelectedSectionIndex = index;
            }
        }
    }

    private void SyncSelectorBarWithViewModel()
    {
        if (SectionSelector != null && ViewModel != null &&
            ViewModel.SelectedSectionIndex >= 0 && ViewModel.SelectedSectionIndex < SectionSelector.Items.Count)
        {
            var targetItem = SectionSelector.Items[ViewModel.SelectedSectionIndex];
            if (SectionSelector.SelectedItem != targetItem)
            {
                SectionSelector.SelectedItem = targetItem;
            }
        }
    }

    private void FocusSearch_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        FilterSearchBox.Focus(FocusState.Keyboard);
        FilterSearchBox.SelectAll();
        args.Handled = true;
    }
}
