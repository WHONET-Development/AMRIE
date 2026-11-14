using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.WinUI.UI.Controls;
using AMRIE.WinUI.Models;
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

    private void DataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        string? sortPath = (e.Column.Tag as string)
            ?? (e.Column as DataGridTextColumn)?.Binding?.Path?.Path
            ?? e.Column.Header?.ToString();
        if (sender is not DataGrid grid || string.IsNullOrEmpty(sortPath))
            return;

        var newDirection = e.Column.SortDirection == DataGridSortDirection.Ascending
            ? DataGridSortDirection.Descending
            : DataGridSortDirection.Ascending;

        foreach (var col in grid.Columns)
        {
            if (col != e.Column) col.SortDirection = null;
        }
        e.Column.SortDirection = newDirection;

        if (grid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<BreakpointRow> bpCollection)
        {
            SortCollection(bpCollection, sortPath, newDirection == DataGridSortDirection.Ascending);
        }
        else if (grid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ExpertRuleRow> erCollection)
        {
            SortCollection(erCollection, sortPath, newDirection == DataGridSortDirection.Ascending);
        }
        else if (grid.ItemsSource is System.Collections.ObjectModel.ObservableCollection<IntrinsicRuleRow> irCollection)
        {
            SortCollection(irCollection, sortPath, newDirection == DataGridSortDirection.Ascending);
        }
    }

    private static void SortCollection<T>(System.Collections.ObjectModel.ObservableCollection<T> collection, string propertyName, bool ascending)
    {
        var propertyInfo = typeof(T).GetProperty(propertyName);
        if (propertyInfo == null) return;

        var sorted = ascending
            ? collection.OrderBy(x => propertyInfo.GetValue(x, null)).ToList()
            : collection.OrderByDescending(x => propertyInfo.GetValue(x, null)).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int oldIndex = collection.IndexOf(sorted[i]);
            if (oldIndex != i)
            {
                collection.Move(oldIndex, i);
            }
        }
    }
}
