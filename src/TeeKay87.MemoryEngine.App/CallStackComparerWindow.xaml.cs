using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.Core.Debugging.Snapshots;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App;

public partial class CallStackComparerWindow : Window
{
    private readonly Func<Task<DebuggerSnapshot?>>? _captureCurrent;
    private readonly DebuggerSnapshotJsonSerializer _serializer = new();
    private readonly DataExportDialogService _dataExportDialogService = new();
    private readonly ConfirmationDialogService _confirmationDialogService = new();
    private readonly OperationProgressDialogService _operationProgressDialogService = new();
    private readonly TabularExportService _tabularExportService = new();

    public CallStackComparerWindow(CallStackComparerViewModel viewModel, Func<Task<DebuggerSnapshot?>>? captureCurrent = null)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _captureCurrent = captureCurrent;
        InitializeComponent();
    }

    private CallStackComparerViewModel ViewModel => (CallStackComparerViewModel)DataContext;

    private async void CaptureCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_captureCurrent is null) { ViewModel.ReportStatus("No live Debugger is available for capture. Imported snapshots remain available offline."); return; }
        DebuggerSnapshot? snapshot = await _captureCurrent().ConfigureAwait(true);
        if (snapshot is not null) ViewModel.AddSnapshot(snapshot);
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Import Debugger Snapshot", Filter = "Debugger snapshot (*.json)|*.json|JSON files (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { ViewModel.AddSnapshot(await _serializer.ImportAsync(dialog.FileName, CancellationToken.None).ConfigureAwait(true), isImported: true); }
        catch (Exception ex) { ViewModel.ReportStatus($"Snapshot import failed: {ex.Message}"); }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotGrid.SelectedItem is not DebuggerSnapshotItemViewModel item) { ViewModel.ReportStatus("Select one snapshot to export."); return; }
        SaveFileDialog dialog = new() { Title = "Export Debugger Snapshot", Filter = "Debugger snapshot (*.json)|*.json", FileName = Sanitize(item.Label) + ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try { await _serializer.ExportAsync(dialog.FileName, item.Snapshot, CancellationToken.None).ConfigureAwait(true); ViewModel.ReportStatus($"Exported '{item.Label}' to {Path.GetFileName(dialog.FileName)}."); }
        catch (Exception ex) { ViewModel.ReportStatus($"Snapshot export failed: {ex.Message}"); }
    }


    private async void ExportResultsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Results.Count == 0)
        {
            ViewModel.ReportStatus("Run a comparison before exporting comparison results.");
            return;
        }

        ExportColumn[] columns =
        [
            new("category", "Category"),
            new("item", "Item"),
            new("groupA", "Group A"),
            new("groupB", "Group B"),
            new("classification", "Classification"),
            new("evidence", "Evidence")
        ];
        InMemoryExportDataSource source = new(
            "debugger-snapshot-comparison",
            columns,
            ViewModel.Results.Select(item => (IReadOnlyList<ExportCellValue>)new ExportCellValue[]
            {
                ExportCellValue.FromString(item.Category),
                ExportCellValue.FromString(item.Name),
                ExportCellValue.FromString(item.GroupA),
                ExportCellValue.FromString(item.GroupB),
                ExportCellValue.FromString(item.Classification.ToString()),
                ExportCellValue.FromString(item.Evidence)
            }));
        ExportScopeOption scope = new("comparison", "Comparison Results", "Exports the current derived comparison result table.", source);
        DataExportDialogResult? selection = _dataExportDialogService.Show(this, "Export Comparison Results", [scope]);
        if (selection is null || !ExportDestinationPicker.TryChoose(this, "debugger-comparison", selection.Format, out string? path))
        {
            return;
        }

        try
        {
            TabularExportResult result = await _operationProgressDialogService.RunAsync(
                this,
                "Exporting Comparison Results",
                "Writing comparison export...",
                true,
                (progress, cancellationToken) => _tabularExportService.ExportAsync(
                    path, selection.Format, source, selection.ColumnIds, progress, cancellationToken)).ConfigureAwait(true);
            ViewModel.ReportStatus($"Comparison export complete: {result.RowsWritten:N0} row(s) written.");
        }
        catch (OperationCanceledException)
        {
            ViewModel.ReportStatus("Comparison export cancelled. No partial export was published.");
        }
        catch (Exception ex)
        {
            ViewModel.ReportStatus($"Comparison export failed: {ex.Message}");
        }
    }

    private void CompareSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        DebuggerSnapshotItemViewModel[] selected = SnapshotGrid.SelectedItems.OfType<DebuggerSnapshotItemViewModel>().Take(3).ToArray();
        if (selected.Length != 2 || !ViewModel.ComparePair(selected[0], selected[1])) ViewModel.ReportStatus("Select exactly two snapshots for pairwise comparison.");
    }

    private void CompareGroupsButton_Click(object sender, RoutedEventArgs e)
    {
        string groupA = ViewModel.SelectedGroupA ?? string.Empty;
        string groupB = ViewModel.SelectedGroupB ?? string.Empty;
        if (!ViewModel.CompareGroups(groupA, groupB))
        {
            ViewModel.ReportStatus("Select two different groups that both contain at least one snapshot.");
        }
    }

    private void SnapshotGroupComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        comboBox.ApplyTemplate();
        if (comboBox.Template.FindName("NewSnapshotGroupTextBox", comboBox) is TextBox textBox)
        {
            textBox.Focus();
            Keyboard.Focus(textBox);
        }
    }

    private void SnapshotNewGroupTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: DebuggerSnapshotItemViewModel item } textBox)
        {
            return;
        }

        e.Handled = true;
        if (!item.TryCreateAndAssignGroup(textBox.Text, out string assignedGroup))
        {
            ViewModel.ReportStatus("Enter a group name before creating a snapshot group.");
            return;
        }

        textBox.Clear();
        if (textBox.Tag is ComboBox comboBox)
        {
            comboBox.IsDropDownOpen = false;
        }

        ViewModel.ReportStatus($"Assigned '{item.Label}' to group '{assignedGroup}'.");
    }

    private void SnapshotGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedSnapshotCount = SnapshotGrid.SelectedItems.Count;
    }

    private void SnapshotNoGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DebuggerSnapshotItemViewModel item } button)
        {
            return;
        }

        item.ClearGroup();
        if (button.Tag is ComboBox comboBox)
        {
            // The ungrouped value is intentionally not part of AvailableGroups.
            // Clear the live selection itself so the selection box cannot retain the old item.
            // SelectedIndex is not data-bound, so this preserves the SelectedItem binding.
            comboBox.SelectedIndex = -1;
            comboBox.IsDropDownOpen = false;
        }

        ViewModel.ReportStatus($"Removed '{item.Label}' from its snapshot group.");
    }

    private void RemoveSnapshotRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DebuggerSnapshotItemViewModel item })
        {
            return;
        }

        bool confirmed = _confirmationDialogService.Show(
            this,
            new ConfirmationDialogOptions(
                title: "Remove Snapshot",
                message: $"Remove snapshot '{item.Label}' from the comparer?",
                confirmButtonText: "Remove",
                cancelButtonText: "Cancel",
                tone: ConfirmationDialogTone.Danger,
                confirmIsDefault: false));

        if (confirmed)
        {
            ViewModel.Remove(item);
        }
    }

    private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasSnapshots)
        {
            return;
        }

        bool confirmed = _confirmationDialogService.Show(
            this,
            new ConfirmationDialogOptions(
                title: "Remove All Snapshots",
                message: $"Remove all {ViewModel.Snapshots.Count:N0} snapshot(s) from the comparer?",
                confirmButtonText: "Remove All",
                cancelButtonText: "Cancel",
                tone: ConfirmationDialogTone.Danger,
                confirmIsDefault: false));

        if (confirmed)
        {
            ViewModel.Clear();
        }
    }

    private static string Sanitize(string value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "debugger-snapshot" : value.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) text = text.Replace(c, '_');
        return text;
    }
}
