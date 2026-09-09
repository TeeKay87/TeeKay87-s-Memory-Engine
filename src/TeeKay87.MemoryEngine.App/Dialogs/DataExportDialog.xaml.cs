using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class DataExportDialog : Window
{
    private readonly IReadOnlyList<ExportScopeOption> _scopes;
    private ColumnChoice[] _columnChoices = Array.Empty<ColumnChoice>();

    public DataExportDialog(string title, IReadOnlyList<ExportScopeOption> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            throw new ArgumentException("At least one export scope is required.", nameof(scopes));
        }

        _scopes = scopes;
        InitializeComponent();
        Title = title;

        ScopeComboBox.ItemsSource = _scopes;
        ScopeComboBox.SelectedIndex = 0;
        FormatComboBox.ItemsSource = ExportFormatOption.All;
        FormatComboBox.SelectedIndex = 0;
        RefreshScope();
    }

    public DataExportDialogResult? Result { get; private set; }

    private void ScopeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshScope();
    }

    private void SelectAllColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (ColumnChoice choice in _columnChoices)
        {
            choice.IsSelected = true;
        }

        ColumnsItemsControl.Items.Refresh();
        ValidationTextBlock.Text = string.Empty;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScopeComboBox.SelectedItem is not ExportScopeOption scope ||
            FormatComboBox.SelectedItem is not ExportFormatOption format)
        {
            ValidationTextBlock.Text = "Choose an export scope and format.";
            return;
        }

        string[] selectedColumns = _columnChoices
            .Where(choice => choice.IsSelected)
            .Select(choice => choice.Id)
            .ToArray();
        if (selectedColumns.Length == 0)
        {
            ValidationTextBlock.Text = "Select at least one column.";
            return;
        }

        Result = new DataExportDialogResult(scope, format.Format, selectedColumns);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RefreshScope()
    {
        if (ScopeComboBox.SelectedItem is not ExportScopeOption scope || ColumnsItemsControl is null)
        {
            return;
        }

        _columnChoices = scope.Source.Columns
            .Select(column => new ColumnChoice(column.Id, column.Header))
            .ToArray();
        ColumnsItemsControl.ItemsSource = _columnChoices;
        ScopeDescriptionTextBlock.Text = scope.Description;
        ValidationTextBlock.Text = string.Empty;
    }

    private sealed class ColumnChoice
    {
        public ColumnChoice(string id, string header)
        {
            Id = id;
            Header = header;
        }

        public string Id { get; }

        public string Header { get; }

        public bool IsSelected { get; set; } = true;
    }

    internal sealed record ExportFormatOption(TabularExportFormat Format, string DisplayName)
    {
        public static IReadOnlyList<ExportFormatOption> All { get; } = new[]
        {
            new ExportFormatOption(TabularExportFormat.Json, "JSON"),
            new ExportFormatOption(TabularExportFormat.Csv, "CSV"),
            new ExportFormatOption(TabularExportFormat.Tsv, "TSV"),
            new ExportFormatOption(TabularExportFormat.MarkdownTable, "Markdown table")
        };
    }
}
