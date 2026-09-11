using System;
using System.Windows;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App;

public partial class DebuggerWindow : Window
{
    private readonly Func<bool>? _canOpenDisassembler;
    private readonly Action<ulong>? _openDisassembler;
    private readonly Func<bool>? _canOpenMemoryViewer;
    private readonly Action<ulong>? _openMemoryViewer;

    public DebuggerWindow(
        DebuggerViewModel viewModel,
        Func<bool>? canOpenDisassembler = null,
        Action<ulong>? openDisassembler = null,
        Func<bool>? canOpenMemoryViewer = null,
        Action<ulong>? openMemoryViewer = null)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canOpenDisassembler = canOpenDisassembler;
        _openDisassembler = openDisassembler;
        _canOpenMemoryViewer = canOpenMemoryViewer;
        _openMemoryViewer = openMemoryViewer;
        InitializeComponent();
    }

    private async void EditRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            !viewModel.CanEditSelectedRegister ||
            viewModel.SelectedRegister?.Register is not DebuggerRegister { CanWrite: true } register)
        {
            return;
        }

        RegisterEditDialog dialog = new(register)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.ReplacementValue is byte[] replacementValue)
        {
            await viewModel.WriteSelectedRegisterAsync(replacementValue).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private void OpenCurrentInstructionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            viewModel.CurrentInstructionPointer is not ulong address ||
            _canOpenDisassembler?.Invoke() != true ||
            _openDisassembler is null)
        {
            return;
        }

        _openDisassembler(address);
        e.Handled = true;
    }

    private async void AddBreakpointButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel || !viewModel.CanAddBreakpoint)
        {
            return;
        }

        BreakpointDialog dialog = new(
            viewModel.HasBreakpointCapability,
            viewModel.HasWatchpointCapability,
            viewModel.CurrentInstructionPointer)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Request is DebuggerBreakpointRequest request)
        {
            await viewModel.AddBreakpointAsync(request).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private async void RemoveAllBreakpointsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel || !viewModel.CanRemoveAllBreakpoints)
        {
            return;
        }

        ConfirmationDialogService confirmation = new();
        bool confirmed = confirmation.Show(
            this,
            new ConfirmationDialogOptions(
                "Remove All Breakpoints / Watchpoints",
                "Remove every breakpoint and watchpoint from the current debugger session?",
                confirmButtonText: "Remove All",
                tone: ConfirmationDialogTone.Danger));
        if (confirmed)
        {
            await viewModel.RemoveAllBreakpointsAsync().ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private void OpenSelectedBreakpointButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            viewModel.SelectedBreakpoint is not DebuggerBreakpointViewModel breakpoint ||
            _canOpenDisassembler?.Invoke() != true ||
            _openDisassembler is null)
        {
            return;
        }

        _openDisassembler(breakpoint.Address);
        e.Handled = true;
    }

    private void CallStackDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSelectedCallFrameDisassembler();
        e.Handled = true;
    }

    private void OpenSelectedCallFrameDisassemblerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSelectedCallFrameDisassembler();
        e.Handled = true;
    }

    private void OpenSelectedCallFrameDisassembler()
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            !viewModel.CanNavigateToSelectedFrameDisassembler ||
            viewModel.SelectedStackFrame is not DebuggerStackFrameViewModel frame ||
            _canOpenDisassembler?.Invoke() != true ||
            _openDisassembler is null)
        {
            return;
        }

        _openDisassembler(frame.InstructionAddress);
    }

    private void OpenSelectedCallFrameMemoryViewerButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel ||
            !viewModel.CanNavigateToSelectedFrameMemoryViewer ||
            viewModel.SelectedStackFrame is not DebuggerStackFrameViewModel frame ||
            _canOpenMemoryViewer?.Invoke() != true ||
            _openMemoryViewer is null)
        {
            return;
        }

        _openMemoryViewer(frame.InstructionAddress);
        e.Handled = true;
    }

    private async void RunToAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel || !viewModel.CanRunToAddress)
        {
            return;
        }

        RunToAddressDialog dialog = new(viewModel.CurrentInstructionPointer)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.Address is ulong address)
        {
            await viewModel.RunToAddressAsync(address).ConfigureAwait(true);
        }

        e.Handled = true;
    }

    private async void DebuggerWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is DebuggerViewModel viewModel)
        {
            await viewModel.DisposeAsync().ConfigureAwait(true);
        }
    }
}
