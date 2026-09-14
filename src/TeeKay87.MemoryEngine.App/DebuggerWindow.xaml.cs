using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TeeKay87.MemoryEngine.App.Dialogs;
using TeeKay87.MemoryEngine.App.Exporting;
using TeeKay87.MemoryEngine.App.ViewModels;
using TeeKay87.MemoryEngine.Core.Exporting;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App;

public partial class DebuggerWindow : Window
{
    private readonly Func<bool>? _canOpenDisassembler;
    private readonly Action<ulong>? _openDisassembler;
    private readonly Func<bool>? _canOpenMemoryViewer;
    private readonly Action<ulong>? _openMemoryViewer;
    private readonly Func<Task>? _openComparer;
    private readonly DataExportDialogService _dataExportDialogService = new();
    private readonly OperationProgressDialogService _operationProgressDialogService = new();
    private readonly TabularExportService _tabularExportService = new();

    public DebuggerWindow(
        DebuggerViewModel viewModel,
        Func<bool>? canOpenDisassembler = null,
        Action<ulong>? openDisassembler = null,
        Func<bool>? canOpenMemoryViewer = null,
        Action<ulong>? openMemoryViewer = null,
        Func<Task>? openComparer = null)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canOpenDisassembler = canOpenDisassembler;
        _openDisassembler = openDisassembler;
        _canOpenMemoryViewer = canOpenMemoryViewer;
        _openMemoryViewer = openMemoryViewer;
        _openComparer = openComparer;
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



    private async void ExportDebuggerButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebuggerViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<ExportScopeOption> scopes = CreateDebuggerExportScopes(viewModel);
        if (scopes.Count == 0)
        {
            viewModel.ReportExternalStatus("No debugger list data is currently available to export.");
            return;
        }

        DataExportDialogResult? selection = _dataExportDialogService.Show(this, "Export Debugger Data", scopes);
        if (selection is null || !ExportDestinationPicker.TryChoose(this, "debugger-data", selection.Format, out string? path))
        {
            return;
        }

        try
        {
            TabularExportResult result = await _operationProgressDialogService.RunAsync(
                this, "Exporting Debugger Data", "Writing debugger export...", true,
                (progress, cancellationToken) => _tabularExportService.ExportAsync(
                    path, selection.Format, selection.Scope.Source, selection.ColumnIds, progress, cancellationToken)).ConfigureAwait(true);
            viewModel.ReportExternalStatus($"Debugger export complete: {result.RowsWritten:N0} row(s) written.");
        }
        catch (OperationCanceledException)
        {
            viewModel.ReportExternalStatus("Debugger export cancelled. No partial export was published.");
        }
        catch (Exception exception)
        {
            viewModel.ReportExternalStatus("Debugger export failed.", exception.Message);
        }
        e.Handled = true;
    }

    private static IReadOnlyList<ExportScopeOption> CreateDebuggerExportScopes(DebuggerViewModel viewModel)
    {
        List<ExportScopeOption> scopes = new();
        if (viewModel.Threads.Count > 0)
        {
            scopes.Add(new ExportScopeOption(
                "threads",
                "Threads",
                "Exports the current debugger thread list.",
                Source(
                    "debugger-threads",
                    [new("id", "Thread"), new("name", "Name"), new("state", "State")],
                    viewModel.Threads.Select(item =>
                    {
                        DebuggerThreadInfo thread = item.Thread;
                        return Row(A(thread.Id), S(thread.Name), S(thread.State.ToString()));
                    }))));
        }

        if (viewModel.Registers.Count > 0)
        {
            scopes.Add(new ExportScopeOption(
                "registers",
                "Registers",
                "Exports the selected thread's current register snapshot.",
                Source(
                    "debugger-registers",
                    [
                        new("id", "ID"), new("name", "Register"), new("bitWidth", "Bit Width"),
                        new("encoding", "Encoding"), new("rawBytes", "Raw Bytes"), new("formattedValue", "Formatted Value"),
                        new("group", "Group"), new("role", "Role"), new("writable", "Writable")
                    ],
                    viewModel.Registers.Select(item =>
                    {
                        DebuggerRegister register = item.Register;
                        return Row(
                            S(register.Id), S(register.DisplayName), I(register.BitWidth), S(register.ValueEncoding.ToString()),
                            S(Convert.ToHexString(register.Value.Span)), S(item.ValueText), S(register.Group),
                            S(register.Role.ToString()), B(register.CanWrite));
                    }))));
        }

        if (viewModel.Breakpoints.Count > 0)
        {
            scopes.Add(new ExportScopeOption(
                "breakpoints",
                "Breakpoints / Watchpoints",
                "Exports current managed breakpoint/watchpoint state.",
                Source(
                    "debugger-breakpoints",
                    [
                        new("id", "ID"), new("address", "Address"), new("enabled", "Enabled"),
                        new("type", "Type"), new("kind", "Mechanism"), new("access", "Access"), new("size", "Size"),
                        new("lifetime", "Lifetime")
                    ],
                    viewModel.Breakpoints.Select(item =>
                    {
                        DebuggerBreakpoint breakpoint = item.Breakpoint;
                        DebuggerBreakpointRequest request = breakpoint.Request;
                        string type = request.Access == DebuggerBreakpointAccess.Execute ? "Breakpoint" : "Watchpoint";
                        string lifetime = request.IsTemporary ? "Temporary" : "Persistent";
                        return Row(
                            S(breakpoint.Id), A(request.Address), B(breakpoint.IsEnabled), S(type), S(request.Kind.ToString()),
                            S(request.Access.ToString()), I(request.Size), S(lifetime));
                    }))));
        }

        if (viewModel.CallFrames.Count > 0)
        {
            scopes.Add(new ExportScopeOption(
                "callstack",
                "Call Stack",
                "Exports the current call-stack frames.",
                Source(
                    "debugger-call-stack",
                    [
                        new("index", "#"), new("instruction", "Instruction Address"), new("stack", "Stack Pointer"),
                        new("frame", "Frame Pointer"), new("return", "Return Address"), new("module", "Module"),
                        new("symbol", "Symbol")
                    ],
                    viewModel.CallFrames.Select(item =>
                    {
                        DebuggerStackFrame frame = item.Frame;
                        return Row(
                            I(frame.Index), A(frame.InstructionAddress), A(frame.StackPointer), A(frame.FramePointer),
                            A(frame.ReturnAddress), S(frame.ModuleName), S(frame.SymbolName));
                    }))));
        }

        if (viewModel.Events.Count > 0)
        {
            scopes.Add(new ExportScopeOption(
                "events",
                "Events",
                "Exports debugger events including separate stop and trigger addresses.",
                Source(
                    "debugger-events",
                    [
                        new("sequence", "#"), new("timestampUtc", "Timestamp UTC"), new("event", "Event"),
                        new("state", "State"), new("stopReason", "Stop Reason"), new("thread", "Thread"),
                        new("instructionPointer", "Instruction Pointer"), new("triggerInstruction", "Trigger Instruction"),
                        new("watchedAddress", "Watched Address"), new("watchpointAccess", "Watchpoint Access"),
                        new("watchpointSize", "Watchpoint Size"), new("triggerResolution", "Trigger Resolution"),
                        new("message", "Message")
                    ],
                    viewModel.Events.Select(item =>
                    {
                        DebuggerEvent debuggerEvent = item.Context.Event;
                        return Row(
                            I(item.Context.Sequence), S(debuggerEvent.Timestamp.UtcDateTime.ToString("O")), S(debuggerEvent.Kind.ToString()),
                            S(debuggerEvent.ExecutionState.ToString()), S(debuggerEvent.StopReason.ToString()), A(debuggerEvent.ThreadId),
                            A(debuggerEvent.InstructionPointer), A(debuggerEvent.TriggerInstructionAddress), A(debuggerEvent.WatchedAddress),
                            S(debuggerEvent.WatchpointAccess?.ToString()), debuggerEvent.WatchpointSize.HasValue ? I(debuggerEvent.WatchpointSize.Value) : ExportCellValue.Null,
                            S(debuggerEvent.TriggerResolution.ToString()), S(debuggerEvent.Message));
                    }))));
        }

        return scopes;
    }

    private static InMemoryExportDataSource Source(
        string type,
        IReadOnlyList<ExportColumn> columns,
        IEnumerable<IReadOnlyList<ExportCellValue>> rows) => new(type, columns, rows);

    private static IReadOnlyList<ExportCellValue> Row(params ExportCellValue[] cells) => cells;
    private static ExportCellValue S(string? value) => ExportCellValue.FromString(value);
    private static ExportCellValue I(long value) => ExportCellValue.FromInt64(value);
    private static ExportCellValue B(bool value) => ExportCellValue.FromBoolean(value);
    private static ExportCellValue A(ulong value) => ExportCellValue.FromString($"0x{value:X}");
    private static ExportCellValue A(ulong? value) => value.HasValue ? A(value.Value) : ExportCellValue.Null;

    private async void OpenComparerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openComparer is not null)
        {
            await _openComparer().ConfigureAwait(true);
        }
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
