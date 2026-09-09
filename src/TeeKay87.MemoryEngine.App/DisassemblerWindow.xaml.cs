using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Controls;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.Core.Exporting;

namespace TeeKay87.MemoryEngine.App;

public partial class DisassemblerWindow : Window
{
    private readonly DataExportDialogService _dataExportDialogService = new();
    private readonly OperationProgressDialogService _operationProgressDialogService = new();
    private readonly TabularExportService _tabularExportService = new();

    public DisassemblerWindow(DisassemblerViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }

    private async void DisassemblerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DisassemblerViewModel viewModel)
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
        }
    }

    private void DisassemblyDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is not null)
        {
            dataGrid.ScrollIntoView(dataGrid.SelectedItem);
        }
    }

    private void DisassemblyDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            e.OriginalSource is not DependencyObject originalSource ||
            ItemsControl.ContainerFromElement(dataGrid, originalSource) is not DataGridRow row)
        {
            return;
        }

        if (row.IsSelected && dataGrid.SelectedItems.Count > 1)
        {
            e.Handled = true;
        }
    }

    private void DisassemblyDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            dataGrid.ContextMenu is not ContextMenu contextMenu)
        {
            e.Handled = true;
            return;
        }

        DisassemblyInstructionViewModel? contextInstruction = null;
        if (Mouse.DirectlyOver is DependencyObject source &&
            ItemsControl.ContainerFromElement(dataGrid, source) is DataGridRow
            { DataContext: DisassemblyInstructionViewModel pointerInstruction })
        {
            contextInstruction = pointerInstruction;
        }
        else if (dataGrid.SelectedItem is DisassemblyInstructionViewModel selectedInstruction)
        {
            contextInstruction = selectedInstruction;
        }

        if (contextInstruction is null)
        {
            contextMenu.DataContext = null;
            e.Handled = true;
            return;
        }

        if (!dataGrid.SelectedItems.Contains(contextInstruction))
        {
            dataGrid.SelectedItems.Clear();
            dataGrid.SelectedItem = contextInstruction;
        }

        contextMenu.DataContext = contextInstruction;

        bool canFollow = false;
        bool canExport = false;
        if (DataContext is DisassemblerViewModel viewModel)
        {
            canFollow = viewModel.CanFollowTarget(contextInstruction);
            canExport = viewModel.CanExport;
        }

        ContextMenuUtilities.SetItemEnabled(contextMenu, "FollowTarget", canFollow);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "CopyAddress", true);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "CopyBytes", true);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "CopyInstruction", true);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "CopyAddressInstruction", true);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "CopySelected", dataGrid.SelectedItems.Count > 0);
        ContextMenuUtilities.SetItemEnabled(contextMenu, "ExportDisassembly", canExport);
    }

    private void FollowTargetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DisassemblerViewModel viewModel ||
            sender is not MenuItem { DataContext: DisassemblyInstructionViewModel instruction })
        {
            return;
        }

        viewModel.SelectedInstruction = instruction;
        e.Handled = TryExecuteFollowTarget(viewModel);
    }

    private void CopyAddressMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextInstruction(sender, out _))
        {
            e.Handled = CopySelectedRows(
                row => row.Address,
                "address",
                "addresses");
        }
    }

    private void CopyBytesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextInstruction(sender, out _))
        {
            e.Handled = CopySelectedRows(
                row => row.Bytes,
                "instruction bytes",
                "instruction byte rows");
        }
    }

    private void CopyInstructionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextInstruction(sender, out _))
        {
            e.Handled = CopySelectedRows(
                row => row.Instruction,
                "instruction",
                "instructions");
        }
    }

    private void CopyAddressInstructionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextInstruction(sender, out _))
        {
            e.Handled = CopySelectedRows(
                row => $"{row.Address}: {row.Instruction}",
                "address and instruction",
                "address and instruction rows");
        }
    }

    private void CopySelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = CopySelectedRows();
    }

    private async void ExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await ExportDisassemblyAsync().ConfigureAwait(true);
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        await ExportDisassemblyAsync().ConfigureAwait(true);
    }

    private void DisassemblyDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            e.OriginalSource is not DependencyObject originalSource ||
            ItemsControl.ContainerFromElement(dataGrid, originalSource) is not DataGridRow
            { DataContext: DisassemblyInstructionViewModel instruction } ||
            DataContext is not DisassemblerViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedInstruction = instruction;
        e.Handled = TryExecuteFollowTarget(viewModel);
    }

    private void DisassemblyDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = CopySelectedRows();
            return;
        }

        if (e.Key != Key.Enter || DataContext is not DisassemblerViewModel viewModel)
        {
            return;
        }

        e.Handled = TryExecuteFollowTarget(viewModel);
    }

    private async Task ExportDisassemblyAsync()
    {
        if (DataContext is not DisassemblerViewModel viewModel || !viewModel.CanExport)
        {
            return;
        }

        DisassemblyInstructionViewModel[] selectedRows = GetSelectedRowsInDisplayOrder();
        IReadOnlyList<ExportScopeOption> scopes = viewModel.CreateExportScopes(selectedRows);
        if (scopes.Count == 0)
        {
            return;
        }

        DataExportDialogResult? selection = _dataExportDialogService.Show(
            this,
            "Export Disassembly",
            scopes);
        if (selection is null ||
            !ExportDestinationPicker.TryChoose(this, "disassembly", selection.Format, out string? path))
        {
            return;
        }

        IExportDataSource source = selection.Scope.Source;
        viewModel.ReportStatus($"Exporting {source.Count:N0} disassembly instruction(s)...");

        try
        {
            TabularExportResult result = await _operationProgressDialogService
                .RunAsync(
                    this,
                    "Exporting Disassembly",
                    "Writing Disassembler export...",
                    allowCancellation: true,
                    (progress, cancellationToken) => _tabularExportService.ExportAsync(
                        path,
                        selection.Format,
                        source,
                        selection.ColumnIds,
                        progress,
                        cancellationToken))
                .ConfigureAwait(true);

            viewModel.ReportStatus(
                $"Export complete: {result.RowsWritten:N0} instruction(s) written to {System.IO.Path.GetFileName(path)}.");
        }
        catch (OperationCanceledException)
        {
            viewModel.ReportStatus("Disassembly export cancelled. No partial export was published.");
        }
        catch (Exception exception)
        {
            viewModel.ReportStatus(
                "Disassembly export failed. No partial export was published.",
                exception.Message);
        }
    }

    private bool CopySelectedRows()
    {
        return CopySelectedRows(
            row => $"{row.Address}: {row.Bytes}\t{row.Instruction}",
            "selected instruction",
            "selected instructions");
    }

    private bool CopySelectedRows(
        Func<DisassemblyInstructionViewModel, string> formatter,
        string singularDescription,
        string pluralDescription)
    {
        DisassemblyInstructionViewModel[] rows = GetSelectedRowsInDisplayOrder();
        if (rows.Length == 0)
        {
            return false;
        }

        string text = string.Join(
            Environment.NewLine,
            rows.Select(formatter));
        string description = rows.Length == 1
            ? singularDescription
            : $"{rows.Length:N0} {pluralDescription}";
        return CopyText(text, description);
    }

    private DisassemblyInstructionViewModel[] GetSelectedRowsInDisplayOrder()
    {
        if (DataContext is not DisassemblerViewModel viewModel)
        {
            return Array.Empty<DisassemblyInstructionViewModel>();
        }

        HashSet<DisassemblyInstructionViewModel> selected = DisassemblyDataGrid.SelectedItems
            .OfType<DisassemblyInstructionViewModel>()
            .ToHashSet();
        return viewModel.Instructions
            .Where(selected.Contains)
            .ToArray();
    }

    private bool CopyText(string text, string description)
    {
        try
        {
            Clipboard.SetText(text ?? string.Empty);
            if (DataContext is DisassemblerViewModel viewModel)
            {
                viewModel.ReportStatus($"Copied {description} to the clipboard.");
            }

            return true;
        }
        catch (ExternalException exception)
        {
            if (DataContext is DisassemblerViewModel viewModel)
            {
                viewModel.ReportStatus("Clipboard copy failed.", exception.Message);
            }

            return false;
        }
    }

    private static bool TryGetContextInstruction(
        object sender,
        [NotNullWhen(true)] out DisassemblyInstructionViewModel? instruction)
    {
        instruction = (sender as MenuItem)?.DataContext as DisassemblyInstructionViewModel;
        return instruction is not null;
    }

    private static bool TryExecuteFollowTarget(DisassemblerViewModel viewModel)
    {
        if (!viewModel.FollowTargetCommand.CanExecute(null))
        {
            return false;
        }

        viewModel.FollowTargetCommand.Execute(null);
        return true;
    }

    private void DisassemblerWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
