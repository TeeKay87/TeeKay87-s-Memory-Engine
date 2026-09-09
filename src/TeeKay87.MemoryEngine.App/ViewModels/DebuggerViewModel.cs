using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TeeKay87.MemoryEngine.App.Debugging;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Debugging;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumDisplayedEvents = 2_000;

    private readonly PluginViewModel _plugin;
    private readonly TargetProcess _targetProcess;
    private readonly long _connectionGeneration;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly AsyncRelayCommand _attachCommand;
    private readonly AsyncRelayCommand _pauseCommand;
    private readonly AsyncRelayCommand _continueCommand;
    private readonly AsyncRelayCommand _detachCommand;
    private readonly RelayCommand _clearEventsCommand;
    private readonly AsyncRelayCommand _refreshThreadsCommand;
    private readonly AsyncRelayCommand _suspendThreadCommand;
    private readonly AsyncRelayCommand _resumeThreadCommand;
    private readonly AsyncRelayCommand _refreshRegistersCommand;
    private DebuggerSessionCoordinator? _coordinator;
    private DebuggerSessionState _sessionState = DebuggerSessionState.Detached;
    private string _stateText = "Detached";
    private string _statusText = "Ready to attach to the Active Target.";
    private string _errorText = string.Empty;
    private bool _isBusy;
    private DebuggerThreadViewModel? _selectedThread;
    private DebuggerRegisterViewModel? _selectedRegister;
    private ulong? _currentInstructionPointer;
    private bool _suppressAutomaticRegisterRefresh;
    private bool _disposed;

    internal DebuggerViewModel(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        long connectionGeneration)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _targetProcess = targetProcess ?? throw new ArgumentNullException(nameof(targetProcess));
        _connectionGeneration = connectionGeneration;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _attachCommand = new AsyncRelayCommand(AttachAsync, CanAttach);
        _pauseCommand = new AsyncRelayCommand(PauseAsync, CanPause);
        _continueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
        _detachCommand = new AsyncRelayCommand(DetachAsync, CanDetach);
        _clearEventsCommand = new RelayCommand(ClearEvents, CanClearEvents);
        _refreshThreadsCommand = new AsyncRelayCommand(RefreshThreadsAsync, CanRefreshThreads);
        _suspendThreadCommand = new AsyncRelayCommand(SuspendThreadAsync, CanSuspendThread);
        _resumeThreadCommand = new AsyncRelayCommand(ResumeThreadAsync, CanResumeThread);
        _refreshRegistersCommand = new AsyncRelayCommand(RefreshRegistersAsync, CanRefreshRegisters);

        _plugin.PropertyChanged += OnPluginPropertyChanged;
    }

    public string WindowTitle => $"Debugger - {TargetDisplayName}";

    public string TargetText => $"{_plugin.Name} · {TargetDisplayName} (0x{_targetProcess.Id:X})";

    public string TargetDisplayName => !string.IsNullOrWhiteSpace(_targetProcess.DisplayName)
        ? _targetProcess.DisplayName!
        : !string.IsNullOrWhiteSpace(_targetProcess.Name)
            ? _targetProcess.Name
            : "<unnamed process>";

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ObservableCollection<DebuggerEventViewModel> Events { get; } = new();

    public ObservableCollection<DebuggerThreadViewModel> Threads { get; } = new();

    public ObservableCollection<DebuggerRegisterViewModel> Registers { get; } = new();

    public DebuggerThreadViewModel? SelectedThread
    {
        get => _selectedThread;
        set
        {
            if (SetProperty(ref _selectedThread, value))
            {
                ClearRegisters();
                RaiseThreadCommandStates();
                RaiseRegisterCommandStates();

                if (!_suppressAutomaticRegisterRefresh &&
                    !IsBusy &&
                    value is not null &&
                    _sessionState == DebuggerSessionState.Paused &&
                    HasRegisterAccessCapability &&
                    IsTargetCurrent())
                {
                    _ = RefreshRegistersAfterThreadSelectionAsync(value.Id);
                }
            }
        }
    }

    public DebuggerRegisterViewModel? SelectedRegister
    {
        get => _selectedRegister;
        set
        {
            if (SetProperty(ref _selectedRegister, value))
            {
                OnPropertyChanged(nameof(CanEditSelectedRegister));
            }
        }
    }

    public bool HasRegisterAccessCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.RegisterAccess);

    public ulong? CurrentInstructionPointer
    {
        get => _currentInstructionPointer;
        private set
        {
            if (SetProperty(ref _currentInstructionPointer, value))
            {
                OnPropertyChanged(nameof(CurrentInstructionPointerText));
                OnPropertyChanged(nameof(CanNavigateToCurrentInstruction));
            }
        }
    }

    public string CurrentInstructionPointerText => CurrentInstructionPointer.HasValue
        ? $"0x{CurrentInstructionPointer.Value:X}"
        : "Unavailable";

    public bool CanNavigateToCurrentInstruction =>
        CurrentInstructionPointer.HasValue &&
        _sessionState == DebuggerSessionState.Paused &&
        IsTargetCurrent() &&
        _plugin.CanOpenDisassemblerForTarget(_targetProcess);

    public bool CanEditSelectedRegister =>
        !_disposed &&
        !IsBusy &&
        _sessionState == DebuggerSessionState.Paused &&
        SelectedThread is not null &&
        SelectedRegister?.CanWrite == true &&
        IsTargetCurrent();

    public bool HasThreadEnumerationCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.ThreadEnumeration);

    public bool HasThreadControlCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.ThreadControl);

    public ICommand AttachCommand => _attachCommand;

    public ICommand PauseCommand => _pauseCommand;

    public ICommand ContinueCommand => _continueCommand;

    public ICommand DetachCommand => _detachCommand;

    public ICommand ClearEventsCommand => _clearEventsCommand;

    public ICommand RefreshThreadsCommand => _refreshThreadsCommand;

    public ICommand SuspendThreadCommand => _suspendThreadCommand;

    public ICommand ResumeThreadCommand => _resumeThreadCommand;

    public ICommand RefreshRegistersCommand => _refreshRegistersCommand;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _plugin.PropertyChanged -= OnPluginPropertyChanged;
        _lifetimeCancellation.Cancel();

        DebuggerSessionCoordinator? coordinator = _coordinator;
        _coordinator = null;
        if (coordinator is not null)
        {
            coordinator.StateChanged -= OnCoordinatorStateChanged;
            coordinator.EventReceived -= OnCoordinatorEventReceived;
            await _plugin.ReleaseDebuggerSessionAsync(coordinator).ConfigureAwait(true);
        }

        _lifetimeCancellation.Dispose();
    }

    private bool CanAttach()
    {
        return !_disposed &&
               !IsBusy &&
               _coordinator is null &&
               _plugin.CanOpenDebugger &&
               IsTargetCurrent();
    }

    private bool CanPause()
    {
        return !_disposed &&
               !IsBusy &&
               _coordinator is not null &&
               _sessionState == DebuggerSessionState.Running &&
               IsTargetCurrent();
    }

    private bool CanContinue()
    {
        return !_disposed &&
               !IsBusy &&
               _coordinator is not null &&
               _sessionState == DebuggerSessionState.Paused &&
               IsTargetCurrent();
    }

    private bool CanDetach()
    {
        return !_disposed &&
               !IsBusy &&
               _coordinator is not null &&
               _sessionState is not DebuggerSessionState.Detached and
                   not DebuggerSessionState.Detaching;
    }

    private bool CanClearEvents()
    {
        return !_disposed && !IsBusy && Events.Count > 0;
    }

    private bool CanRefreshThreads()
    {
        return !_disposed &&
               !IsBusy &&
               HasThreadEnumerationCapability &&
               IsAttachedState() &&
               IsTargetCurrent();
    }

    private bool CanSuspendThread()
    {
        return !_disposed &&
               !IsBusy &&
               HasThreadControlCapability &&
               SelectedThread is not null &&
               SelectedThread.ThreadState is not DebuggerThreadState.Suspended and not DebuggerThreadState.Exited &&
               _sessionState == DebuggerSessionState.Running &&
               IsTargetCurrent();
    }

    private bool CanResumeThread()
    {
        return !_disposed &&
               !IsBusy &&
               HasThreadControlCapability &&
               SelectedThread?.ThreadState == DebuggerThreadState.Suspended &&
               _sessionState == DebuggerSessionState.Running &&
               IsTargetCurrent();
    }

    private bool CanRefreshRegisters()
    {
        return !_disposed &&
               !IsBusy &&
               HasRegisterAccessCapability &&
               SelectedThread is not null &&
               _sessionState == DebuggerSessionState.Paused &&
               IsTargetCurrent();
    }

    private async Task AttachAsync()
    {
        if (!IsTargetCurrent())
        {
            ReportStaleTarget();
            return;
        }

        if (!_plugin.CanOpenDebugger)
        {
            ErrorText = string.Empty;
            StatusText = "Wait for the current target operation to finish before attaching the debugger.";
            RaiseCommandStates();
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Attaching debugger...";

        try
        {
            DebuggerSessionCoordinator coordinator = _plugin.CreateDebuggerSessionCoordinator(
                _targetProcess,
                _connectionGeneration);
            _coordinator = coordinator;
            coordinator.StateChanged += OnCoordinatorStateChanged;
            coordinator.EventReceived += OnCoordinatorEventReceived;

            await coordinator
                .AttachAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);

            ApplyCoordinatorState(coordinator.State);
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            if (coordinator.State == DebuggerSessionState.Paused)
            {
                await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
            }
            StatusText = coordinator.State switch
            {
                DebuggerSessionState.Running => "Debugger attached. The target is running.",
                DebuggerSessionState.Paused => "Debugger attached. The target is paused.",
                _ => "Debugger attached."
            };
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Debugger attach cancelled.";
            await ReleaseCoordinatorAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Debugger attach failed.";
            await ReleaseCoordinatorAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PauseAsync()
    {
        if (!EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Pausing target...";

        try
        {
            await coordinator
                .PauseAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            ApplyCoordinatorState(coordinator.State);
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
            StatusText = "Target paused.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Pause cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Pause failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContinueAsync()
    {
        if (!EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Continuing target...";

        try
        {
            await coordinator
                .ContinueAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            ApplyCoordinatorState(coordinator.State);
            ClearRegisters();
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            StatusText = "Target running.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Continue cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Continue failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DetachAsync()
    {
        DebuggerSessionCoordinator? coordinator = _coordinator;
        if (coordinator is null)
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Detaching debugger...";

        try
        {
            await coordinator
                .DetachAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            StatusText = "Debugger detached.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Debugger detach cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Debugger detach reported an error; the session was still released.";
        }
        finally
        {
            await ReleaseCoordinatorAsync().ConfigureAwait(true);
            ClearThreads();
            ClearRegisters();
            ApplyCoordinatorState(DebuggerSessionState.Detached);
            IsBusy = false;
        }
    }


    private async Task RefreshThreadsAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await RefreshThreadsCoreAsync(showStatus: true).ConfigureAwait(true);
            if (_sessionState == DebuggerSessionState.Paused)
            {
                await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SuspendThreadAsync()
    {
        if (!TryGetThreadControlContext(out DebuggerSessionCoordinator? coordinator, out DebuggerThreadViewModel? thread))
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Suspending thread {thread.IdText}...";

        try
        {
            IDebuggerThreadControlService? service = coordinator.GetService<IDebuggerThreadControlService>();
            if (service is null)
            {
                throw new NotSupportedException("The attached debugger session does not provide thread control.");
            }

            await service.SuspendThreadAsync(thread.Id, _lifetimeCancellation.Token).ConfigureAwait(true);
            await RefreshThreadsCoreAsync(showStatus: false, preferredThreadId: thread.Id).ConfigureAwait(true);
            StatusText = $"Thread {thread.IdText} suspended.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Thread suspend cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Thread suspend failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResumeThreadAsync()
    {
        if (!TryGetThreadControlContext(out DebuggerSessionCoordinator? coordinator, out DebuggerThreadViewModel? thread))
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Resuming thread {thread.IdText}...";

        try
        {
            IDebuggerThreadControlService? service = coordinator.GetService<IDebuggerThreadControlService>();
            if (service is null)
            {
                throw new NotSupportedException("The attached debugger session does not provide thread control.");
            }

            await service.ResumeThreadAsync(thread.Id, _lifetimeCancellation.Token).ConfigureAwait(true);
            await RefreshThreadsCoreAsync(showStatus: false, preferredThreadId: thread.Id).ConfigureAwait(true);
            StatusText = $"Thread {thread.IdText} resumed.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Thread resume cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Thread resume failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RefreshThreadsCoreAsync(bool showStatus, ulong? preferredThreadId = null)
    {
        if (!HasThreadEnumerationCapability || !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            ClearThreads();
            return false;
        }

        IDebuggerThreadService? service = coordinator.GetService<IDebuggerThreadService>();
        if (service is null)
        {
            ClearThreads();
            ErrorText = "The plugin advertises thread enumeration but the attached debugger session does not provide IDebuggerThreadService.";
            if (showStatus)
            {
                StatusText = "Thread enumeration is unavailable.";
            }
            return false;
        }

        ulong? selectedId = preferredThreadId ?? SelectedThread?.Id;
        try
        {
            IReadOnlyList<DebuggerThreadInfo> threads = await service
                .GetThreadsAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);

            Threads.Clear();
            foreach (DebuggerThreadInfo thread in threads.OrderBy(thread => thread.Id))
            {
                Threads.Add(new DebuggerThreadViewModel(thread));
            }

            _suppressAutomaticRegisterRefresh = true;
            try
            {
                SelectedThread = selectedId.HasValue
                    ? Threads.FirstOrDefault(thread => thread.Id == selectedId.Value) ?? Threads.FirstOrDefault()
                    : Threads.FirstOrDefault();
            }
            finally
            {
                _suppressAutomaticRegisterRefresh = false;
            }

            if (showStatus)
            {
                StatusText = Threads.Count == 1
                    ? "Loaded 1 debugger thread."
                    : $"Loaded {Threads.Count} debugger threads.";
            }

            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (showStatus)
            {
                StatusText = "Thread enumeration cancelled.";
            }
            return false;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            if (showStatus)
            {
                StatusText = "Thread enumeration failed.";
            }
            return false;
        }
        finally
        {
            RaiseThreadCommandStates();
        }
    }

    private async Task RefreshRegistersAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await RefreshRegistersCoreAsync(showStatus: true).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshRegistersAfterThreadSelectionAsync(ulong threadId)
    {
        try
        {
            await RefreshRegistersCoreAsync(showStatus: false, expectedThreadId: threadId).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> RefreshRegistersCoreAsync(
        bool showStatus,
        string? preferredRegisterId = null,
        ulong? expectedThreadId = null)
    {
        if (!HasRegisterAccessCapability ||
            _sessionState != DebuggerSessionState.Paused ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator) ||
            SelectedThread is not DebuggerThreadViewModel selectedThread)
        {
            ClearRegisters();
            return false;
        }

        if (expectedThreadId.HasValue && selectedThread.Id != expectedThreadId.Value)
        {
            return false;
        }

        IDebuggerRegisterService? service = coordinator.GetService<IDebuggerRegisterService>();
        if (service is null)
        {
            ClearRegisters();
            ErrorText = "The plugin advertises register access but the attached debugger session does not provide IDebuggerRegisterService.";
            if (showStatus)
            {
                StatusText = "Register access is unavailable.";
            }
            return false;
        }

        ulong threadId = selectedThread.Id;
        string? selectedRegisterId = preferredRegisterId ?? SelectedRegister?.Id;
        try
        {
            IReadOnlyList<DebuggerRegister> registers = await service
                .GetRegistersAsync(threadId, _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_sessionState != DebuggerSessionState.Paused ||
                SelectedThread?.Id != threadId ||
                !IsTargetCurrent())
            {
                return false;
            }

            Registers.Clear();
            foreach (DebuggerRegister register in registers)
            {
                Registers.Add(new DebuggerRegisterViewModel(register));
            }

            SelectedRegister = selectedRegisterId is not null
                ? Registers.FirstOrDefault(register => string.Equals(register.Id, selectedRegisterId, StringComparison.OrdinalIgnoreCase))
                    ?? Registers.FirstOrDefault()
                : Registers.FirstOrDefault();

            DebuggerRegisterViewModel? instructionPointer = Registers
                .FirstOrDefault(register => register.Register.Role == DebuggerRegisterRole.InstructionPointer);
            CurrentInstructionPointer = instructionPointer is not null &&
                                        DebuggerRegisterValueCodec.TryGetUInt64(instructionPointer.Register, out ulong address)
                ? address
                : null;

            if (showStatus)
            {
                StatusText = Registers.Count == 1
                    ? $"Loaded 1 register for thread {selectedThread.IdText}."
                    : $"Loaded {Registers.Count} registers for thread {selectedThread.IdText}.";
            }

            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (showStatus)
            {
                StatusText = "Register refresh cancelled.";
            }
            return false;
        }
        catch (Exception exception)
        {
            ClearRegisters();
            ErrorText = exception.Message;
            if (showStatus)
            {
                StatusText = "Register refresh failed.";
            }
            return false;
        }
        finally
        {
            RaiseRegisterCommandStates();
        }
    }

    public async Task<bool> WriteSelectedRegisterAsync(ReadOnlyMemory<byte> value)
    {
        if (!CanEditSelectedRegister ||
            SelectedThread is not DebuggerThreadViewModel thread ||
            SelectedRegister is not DebuggerRegisterViewModel register ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return false;
        }

        if (value.Length != register.Register.Value.Length)
        {
            ErrorText = "The replacement value does not match the selected register width.";
            return false;
        }

        IDebuggerRegisterService? service = coordinator.GetService<IDebuggerRegisterService>();
        if (service is null)
        {
            ErrorText = "The attached debugger session does not provide register access.";
            return false;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Writing {register.DisplayName}...";

        try
        {
            byte[] expectedValue = value.ToArray();
            await service
                .WriteRegisterAsync(
                    thread.Id,
                    new DebuggerRegisterWriteRequest(register.Id, expectedValue),
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            bool refreshed = await RefreshRegistersCoreAsync(
                    showStatus: false,
                    preferredRegisterId: register.Id,
                    expectedThreadId: thread.Id)
                .ConfigureAwait(true);

            DebuggerRegisterViewModel? verifiedRegister = Registers
                .FirstOrDefault(item => string.Equals(item.Id, register.Id, StringComparison.OrdinalIgnoreCase));
            if (!refreshed || verifiedRegister is null || !verifiedRegister.Register.Value.Span.SequenceEqual(expectedValue))
            {
                throw new InvalidOperationException(
                    $"The {register.DisplayName} write completed, but read-back verification did not match the requested value.");
            }

            StatusText = $"{register.DisplayName} written and verified.";
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Register write cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Register write failed.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryGetThreadControlContext(
        [NotNullWhen(true)] out DebuggerSessionCoordinator? coordinator,
        [NotNullWhen(true)] out DebuggerThreadViewModel? thread)
    {
        thread = SelectedThread;
        if (thread is null || !EnsureCurrentAttachedCoordinator(out coordinator))
        {
            coordinator = null;
            return false;
        }

        return true;
    }

    private bool IsAttachedState()
    {
        return _coordinator is not null && _sessionState is
            DebuggerSessionState.Attached or
            DebuggerSessionState.Running or
            DebuggerSessionState.Paused;
    }

    private void ClearThreads()
    {
        Threads.Clear();
        SelectedThread = null;
        RaiseThreadCommandStates();
    }

    private void ClearRegisters()
    {
        Registers.Clear();
        SelectedRegister = null;
        CurrentInstructionPointer = null;
        RaiseRegisterCommandStates();
    }

    private void ApplyThreadExecutionState(DebuggerSessionState state)
    {
        DebuggerThreadState? mappedState = state switch
        {
            DebuggerSessionState.Running => DebuggerThreadState.Running,
            DebuggerSessionState.Paused => DebuggerThreadState.Stopped,
            _ => null
        };

        if (!mappedState.HasValue || Threads.Count == 0)
        {
            return;
        }

        ulong? selectedId = SelectedThread?.Id;
        for (int index = 0; index < Threads.Count; index++)
        {
            DebuggerThreadViewModel current = Threads[index];
            if (current.ThreadState == DebuggerThreadState.Suspended)
            {
                continue;
            }

            Threads[index] = new DebuggerThreadViewModel(new DebuggerThreadInfo(
                current.Id,
                current.Name,
                mappedState.Value));
        }

        if (selectedId.HasValue)
        {
            SelectedThread = Threads.FirstOrDefault(thread => thread.Id == selectedId.Value);
        }
    }

    private void ClearEvents()
    {
        Events.Clear();
        StatusText = "Debugger event history cleared.";
        _clearEventsCommand.RaiseCanExecuteChanged();
    }

    private bool EnsureCurrentAttachedCoordinator(
        [NotNullWhen(true)] out DebuggerSessionCoordinator? coordinator)
    {
        coordinator = _coordinator;
        if (coordinator is null)
        {
            return false;
        }

        if (!IsTargetCurrent())
        {
            ReportStaleTarget();
            return false;
        }

        return true;
    }

    private bool IsTargetCurrent()
    {
        return _plugin.IsCurrentDebuggerTarget(_targetProcess, _connectionGeneration);
    }

    private void ReportStaleTarget()
    {
        ErrorText = "This Debugger window belongs to an earlier target or connection. Open a new Debugger window for the current Active Target.";
        StatusText = "Debugger target is no longer current.";
        ClearThreads();
        ClearRegisters();
        RaiseCommandStates();
    }

    private async Task ReleaseCoordinatorAsync()
    {
        DebuggerSessionCoordinator? coordinator = _coordinator;
        _coordinator = null;
        if (coordinator is null)
        {
            return;
        }

        coordinator.StateChanged -= OnCoordinatorStateChanged;
        coordinator.EventReceived -= OnCoordinatorEventReceived;
        await _plugin.ReleaseDebuggerSessionAsync(coordinator).ConfigureAwait(true);
    }

    private void OnCoordinatorStateChanged(object? sender, DebuggerSessionStateChangedEventArgs eventArgs)
    {
        DispatchToUi(() =>
        {
            ApplyCoordinatorState(eventArgs.CurrentState);
            if (eventArgs.CurrentState == DebuggerSessionState.Detached && !IsTargetCurrent())
            {
                StatusText = "Debugger detached because the target session changed.";
            }
        });
    }

    private void OnCoordinatorEventReceived(object? sender, DebuggerEventContextEventArgs eventArgs)
    {
        DebuggerEventContext context = eventArgs.Context;
        if (!context.Identity.Matches(_plugin.Metadata.Id, _targetProcess, _connectionGeneration) ||
            !IsTargetCurrent())
        {
            return;
        }

        DispatchToUi(() =>
        {
            if (!IsTargetCurrent())
            {
                return;
            }

            Events.Add(new DebuggerEventViewModel(context));
            while (Events.Count > MaximumDisplayedEvents)
            {
                Events.RemoveAt(0);
            }

            _clearEventsCommand.RaiseCanExecuteChanged();
            StatusText = context.Event.Message ?? $"Debugger event: {context.Event.Kind}.";

            if (context.Event.ExecutionState == DebuggerExecutionState.Running)
            {
                ClearRegisters();
            }
            else if (context.Event.ExecutionState == DebuggerExecutionState.Paused)
            {
                if (context.Event.InstructionPointer.HasValue)
                {
                    CurrentInstructionPointer = context.Event.InstructionPointer.Value;
                }

                if (!IsBusy)
                {
                    _ = RefreshStopContextFromEventAsync(context.Event);
                }
            }
        });
    }

    private async Task RefreshStopContextFromEventAsync(DebuggerEvent debugEvent)
    {
        try
        {
            await RefreshThreadsCoreAsync(
                    showStatus: false,
                    preferredThreadId: debugEvent.ThreadId ?? SelectedThread?.Id)
                .ConfigureAwait(true);

            if (_sessionState == DebuggerSessionState.Paused)
            {
                await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
    }

    private void OnPluginPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(PluginViewModel.ActiveProcess) or
            nameof(PluginViewModel.IsConnected) or
            nameof(PluginViewModel.CanOpenDebugger))
        {
            DispatchToUi(() =>
            {
                if (!IsTargetCurrent())
                {
                    ReportStaleTarget();
                    return;
                }

                RaiseCommandStates();
            });
        }
    }

    private void ApplyCoordinatorState(DebuggerSessionState state)
    {
        _sessionState = state;
        ApplyThreadExecutionState(state);
        if (state != DebuggerSessionState.Paused)
        {
            ClearRegisters();
        }
        StateText = state switch
        {
            DebuggerSessionState.Attaching => "Attaching",
            DebuggerSessionState.Attached => "Attached",
            DebuggerSessionState.Running => "Running",
            DebuggerSessionState.Paused => "Paused",
            DebuggerSessionState.Detaching => "Detaching",
            DebuggerSessionState.Faulted => "Faulted",
            _ => "Detached"
        };
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        _attachCommand.RaiseCanExecuteChanged();
        _pauseCommand.RaiseCanExecuteChanged();
        _continueCommand.RaiseCanExecuteChanged();
        _detachCommand.RaiseCanExecuteChanged();
        _clearEventsCommand.RaiseCanExecuteChanged();
        RaiseThreadCommandStates();
        RaiseRegisterCommandStates();
    }

    private void RaiseThreadCommandStates()
    {
        _refreshThreadsCommand.RaiseCanExecuteChanged();
        _suspendThreadCommand.RaiseCanExecuteChanged();
        _resumeThreadCommand.RaiseCanExecuteChanged();
    }

    private void RaiseRegisterCommandStates()
    {
        _refreshRegistersCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditSelectedRegister));
        OnPropertyChanged(nameof(CanNavigateToCurrentInstruction));
    }

    private void DispatchToUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }
}
