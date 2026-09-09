using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Controls;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.ViewModels;

namespace TeeKay87.MemoryEngine.App;

public partial class MemoryViewerWindow : Window
{
    private readonly Func<bool>? _canOpenDisassembler;
    private readonly Action<ulong>? _openDisassembler;

    public MemoryViewerWindow(MemoryViewerViewModel viewModel)
        : this(viewModel, canOpenDisassembler: null, openDisassembler: null)
    {
    }

    internal MemoryViewerWindow(
        MemoryViewerViewModel viewModel,
        Func<bool>? canOpenDisassembler,
        Action<ulong>? openDisassembler)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canOpenDisassembler = canOpenDisassembler;
        _openDisassembler = openDisassembler;
        InitializeComponent();
    }

    private async void MemoryViewerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MemoryViewerViewModel viewModel)
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
        }
    }

    private void MemoryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is not null)
        {
            dataGrid.ScrollIntoView(dataGrid.SelectedItem);
        }
    }

    private void MemoryDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            dataGrid.ContextMenu is not ContextMenu contextMenu)
        {
            e.Handled = true;
            return;
        }

        MemoryViewerRowViewModel? contextRow = null;
        if (Mouse.DirectlyOver is DependencyObject source &&
            ItemsControl.ContainerFromElement(dataGrid, source) is DataGridRow
            { DataContext: MemoryViewerRowViewModel pointerRow })
        {
            contextRow = pointerRow;
        }
        else if (dataGrid.SelectedItem is MemoryViewerRowViewModel selectedRow)
        {
            contextRow = selectedRow;
        }

        if (contextRow is null)
        {
            contextMenu.DataContext = null;
            e.Handled = true;
            return;
        }

        if (!dataGrid.SelectedItems.Contains(contextRow))
        {
            dataGrid.SelectedItems.Clear();
            dataGrid.SelectedItem = contextRow;
        }

        contextMenu.DataContext = contextRow;
        ContextMenuUtilities.SetItemEnabled(
            contextMenu,
            "OpenDisassembler",
            _openDisassembler is not null &&
            (_canOpenDisassembler?.Invoke() ?? true));
    }

    private void OpenDisassemblerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_openDisassembler is null ||
            (_canOpenDisassembler is not null && !_canOpenDisassembler()) ||
            sender is not MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            return;
        }

        _openDisassembler(row.AddressValue);
        e.Handled = true;
    }

    private void MemoryDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C ||
            (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        CopySelectedRows();
        e.Handled = true;
    }

    private async void EditSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MemoryViewerViewModel viewModel &&
            MemoryDataGrid.SelectedItem is MemoryViewerRowViewModel row)
        {
            await EditRowAsync(viewModel, row).ConfigureAwait(true);
            e.Handled = true;
        }
    }

    private async void EditHexBytesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MemoryViewerViewModel viewModel &&
            sender is MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            await EditRowAsync(viewModel, row).ConfigureAwait(true);
            e.Handled = true;
        }
    }

    private async Task EditRowAsync(
        MemoryViewerViewModel viewModel,
        MemoryViewerRowViewModel row)
    {
        if (viewModel.IsBusy || !viewModel.CanEditMemory || !row.CanEdit)
        {
            return;
        }

        MemoryEditDialog dialog = new(row.AddressValue, row.Bytes)
        {
            Owner = this
        };

        bool? accepted = dialog.ShowDialog();
        if (accepted == true && dialog.ReplacementBytes is byte[] replacementBytes)
        {
            await viewModel.WriteRowBytesAsync(row, replacementBytes).ConfigureAwait(true);
            ScrollOriginIntoView();
        }
    }

    private void ScrollOriginIntoView()
    {
        MemoryViewerRowViewModel? originRow = MemoryDataGrid.Items
            .OfType<MemoryViewerRowViewModel>()
            .FirstOrDefault(row => row.IsOriginRow);
        if (originRow is not null)
        {
            MemoryDataGrid.ScrollIntoView(originRow);
        }
    }

    private void CopyAddressMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            CopyTextToClipboard(row.Address, "address");
            e.Handled = true;
        }
    }

    private void CopyHexBytesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            CopyTextToClipboard(row.HexBytes, "hex bytes");
            e.Handled = true;
        }
    }

    private void CopyAsciiMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            CopyTextToClipboard(row.Ascii, "ASCII text");
            e.Handled = true;
        }
    }

    private void CopyRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: MemoryViewerRowViewModel row })
        {
            CopyTextToClipboard(row.ClipboardRow, "Memory Viewer row");
            e.Handled = true;
        }
    }

    private void CopySelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedRows();
        e.Handled = true;
    }

    private void CopySelectedRows()
    {
        MemoryViewerRowViewModel[] selectedRows = MemoryDataGrid.Items
            .OfType<MemoryViewerRowViewModel>()
            .Where(row => MemoryDataGrid.SelectedItems.Contains(row))
            .ToArray();
        if (selectedRows.Length == 0)
        {
            return;
        }

        string text = string.Join(
            Environment.NewLine,
            selectedRows.Select(row => row.ClipboardRow));

        try
        {
            Clipboard.SetText(text);
            if (DataContext is MemoryViewerViewModel viewModel)
            {
                viewModel.ReportClipboardCopy(selectedRows.Length);
            }
        }
        catch (Exception exception)
        {
            ReportClipboardFailure(exception);
        }
    }

    private void CopyTextToClipboard(string text, string itemName)
    {
        try
        {
            Clipboard.SetText(text);
            if (DataContext is MemoryViewerViewModel viewModel)
            {
                viewModel.ReportClipboardCopy(itemName);
            }
        }
        catch (Exception exception)
        {
            ReportClipboardFailure(exception);
        }
    }

    private void ReportClipboardFailure(Exception exception)
    {
        if (DataContext is MemoryViewerViewModel viewModel)
        {
            viewModel.ReportClipboardFailure($"Could not copy to the clipboard: {exception.Message}");
        }
    }

    private void MemoryViewerWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
