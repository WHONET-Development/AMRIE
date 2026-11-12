using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using AMRIE.WinUI.Models;

namespace AMRIE.WinUI.Dialogs;

public sealed partial class ApplicableBreakpointsDialog : ContentDialog
{
    private List<BreakpointRow> _allRows = [];
    private List<BreakpointRow> _filteredRows = [];

    public ApplicableBreakpointsDialog()
    {
        InitializeComponent();
        Common.WindowHelper.ConfigureInlineCloseDialog(this, CloseModalButton);
    }

    public void SetData(List<BreakpointRow> rows, string filterSummary)
    {
        _allRows = rows ?? [];
        SummaryTextBlock.Text = filterSummary;
        ApplyFilter(string.Empty);
    }

    private void ApplyFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _filteredRows = new List<BreakpointRow>(_allRows);
        }
        else
        {
            var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _filteredRows = [.. _allRows.Where(r => terms.All(t => r.SearchKey.Contains(t)))];
        }

        if (HideUndefinedCheck?.IsChecked == true)
        {
            _filteredRows = [.. _filteredRows.Where(r => r.HasAnyBreakpoint)];
        }

        BreakpointsTable.ItemsSource = _filteredRows;
        CountTextBlock.Text = $"Found {_filteredRows.Count} breakpoint{(_filteredRows.Count == 1 ? "" : "s")}" +
                              (_filteredRows.Count != _allRows.Count ? $" (filtered from {_allRows.Count})" : string.Empty);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void HideUndefinedCheck_Changed(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }
}

