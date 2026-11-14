using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.WinUI.UI.Controls;
using AMRIE.WinUI.Models;

namespace AMRIE.WinUI.Dialogs;

public sealed partial class ApplicableIntrinsicRulesDialog : ContentDialog
{
    private List<IntrinsicRuleRow> _allRows = [];
    private List<IntrinsicRuleRow> _filteredRows = [];

    public ApplicableIntrinsicRulesDialog()
    {
        InitializeComponent();
        Common.WindowHelper.ConfigureInlineCloseDialog(this, CloseModalButton);
    }

    public void SetData(List<IntrinsicRuleRow> rows, string filterSummary)
    {
        _allRows = rows ?? [];
        SummaryTextBlock.Text = filterSummary;
        ApplyFilter(string.Empty);
    }

    private void ApplyFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _filteredRows = [.. _allRows];
        }
        else
        {
            var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _filteredRows = [.. _allRows.Where(r => terms.All(t => r.SearchKey.Contains(t)))];
        }

        RulesTable.ItemsSource = _filteredRows;
        CountTextBlock.Text = $"Found {_filteredRows.Count} rule{(_filteredRows.Count == 1 ? "" : "s")}" +
                              (_filteredRows.Count != _allRows.Count ? $" (filtered from {_allRows.Count})" : string.Empty);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void RulesTable_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        string? sortPath = (e.Column.Tag as string)
            ?? (e.Column as DataGridTextColumn)?.Binding?.Path?.Path
            ?? e.Column.Header?.ToString();
        if (string.IsNullOrEmpty(sortPath)) return;

        var newDirection = e.Column.SortDirection == DataGridSortDirection.Ascending
            ? DataGridSortDirection.Descending
            : DataGridSortDirection.Ascending;

        foreach (var col in RulesTable.Columns)
        {
            if (col != e.Column) col.SortDirection = null;
        }
        e.Column.SortDirection = newDirection;

        var prop = typeof(IntrinsicRuleRow).GetProperty(sortPath);
        if (prop != null)
        {
            _filteredRows = newDirection == DataGridSortDirection.Ascending
                ? _filteredRows.OrderBy(x => prop.GetValue(x, null)).ToList()
                : _filteredRows.OrderByDescending(x => prop.GetValue(x, null)).ToList();
            RulesTable.ItemsSource = null;
            RulesTable.ItemsSource = _filteredRows;
        }
    }
}

