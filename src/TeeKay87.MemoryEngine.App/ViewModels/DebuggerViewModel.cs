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
using TeeKay87.MemoryEngine.App.Application;
using TeeKay87.MemoryEngine.App.Debugging;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Debugging;
using TeeKay87.MemoryEngine.Core.Debugging.Snapshots;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.Core.MemoryViewer;
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
    private readonly DebuggerWatchpointTriggerResolver _watchpointTriggerResolver = new();
    private readonly AsyncRelayCommand _attachCommand;
    private readonly AsyncRelayCommand _pauseCommand;
    private readonly AsyncRelayCommand _continueCommand;
    private readonly AsyncRelayCommand _detachCommand;
    private readonly RelayCommand _clearEventsCommand;
    private readonly AsyncRelayCommand _refreshThreadsCommand;
    private readonly AsyncRelayCommand _suspendThreadCommand;
    private readonly AsyncRelayCommand _resumeThreadCommand;
    private readonly AsyncRelayCommand _refreshRegistersCommand;
    private readonly AsyncRelayCommand _refreshBreakpointsCommand;
    private readonly AsyncRelayCommand _refreshCallStackCommand;
    private readonly AsyncRelayCommand _stepIntoCommand;
    private readonly AsyncRelayCommand _stepOverCommand;
    private readonly AsyncRelayCommand _stepOutCommand;
    private readonly AsyncRelayCommand _removeBreakpointCommand;
    private readonly AsyncRelayCommand _enableBreakpointCommand;
    private readonly AsyncRelayCommand _disableBreakpointCommand;
    private readonly RelayCommand _showBreakpointsWorkspaceCommand;
    private readonly RelayCommand _showCallStackWorkspaceCommand;
    private readonly Dictionary<ulong, DisassembledInstruction> _softwareBreakpointInstructionCache = new();
    private DebuggerSessionCoordinator? _coordinator;
    private DebuggerSessionState _sessionState = DebuggerSessionState.Detached;
    private string _stateText = "Detached";
    private string _statusText = "Ready to attach to the Active Target.";
    private string _errorText = string.Empty;
    private bool _isBusy;
    private DebuggerThreadViewModel? _selectedThread;
    private DebuggerRegisterViewModel? _selectedRegister;
    private DebuggerBreakpointViewModel? _selectedBreakpoint;
    private DebuggerStackFrameViewModel? _selectedStackFrame;
    private ulong? _currentInstructionPointer;
    private DebuggerEvent? _deferredStopContextEvent;
    private DebuggerEventContext? _latestStopContext;
    private readonly DebuggerSnapshotCaptureService _snapshotCaptureService = new();
    private ComposedExecutionOperation? _activeComposedExecutionOperation;
    private ComposedExecutionOperation? _pendingInterruptedComposedExecutionOperation;
    private ulong? _logicalSoftwareBreakpointStopAddress;
    private ulong? _logicalSoftwareBreakpointStopThreadId;
    private bool _suppressAutomaticRegisterRefresh;
    private bool _continueInProgress;
    private bool _isCallStackWorkspaceSelected;
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
        _refreshBreakpointsCommand = new AsyncRelayCommand(RefreshBreakpointsAsync, CanRefreshBreakpoints);
        _refreshCallStackCommand = new AsyncRelayCommand(RefreshCallStackAsync, CanRefreshCallStack);
        _stepIntoCommand = new AsyncRelayCommand(StepIntoAsync, CanStepInto);
        _stepOverCommand = new AsyncRelayCommand(StepOverAsync, CanStepOver);
        _stepOutCommand = new AsyncRelayCommand(StepOutAsync, CanStepOut);
        _removeBreakpointCommand = new AsyncRelayCommand(RemoveSelectedBreakpointAsync, CanRemoveSelectedBreakpoint);
        _enableBreakpointCommand = new AsyncRelayCommand(EnableSelectedBreakpointAsync, CanEnableSelectedBreakpoint);
        _disableBreakpointCommand = new AsyncRelayCommand(DisableSelectedBreakpointAsync, CanDisableSelectedBreakpoint);
        _showBreakpointsWorkspaceCommand = new RelayCommand(ShowBreakpointsWorkspace, () => HasBreakpointManagementCapability);
        _showCallStackWorkspaceCommand = new RelayCommand(ShowCallStackWorkspace, () => HasCallStackCapability);
        _isCallStackWorkspaceSelected = !HasBreakpointManagementCapability && HasCallStackCapability;

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
                OnPropertyChanged(nameof(CanCaptureSnapshot));
            }
        }
    }

    public ObservableCollection<DebuggerEventViewModel> Events { get; } = new();

    public ObservableCollection<DebuggerThreadViewModel> Threads { get; } = new();

    public ObservableCollection<DebuggerRegisterViewModel> Registers { get; } = new();

    public ObservableCollection<DebuggerBreakpointViewModel> Breakpoints { get; } = new();

    public ObservableCollection<DebuggerStackFrameViewModel> CallFrames { get; } = new();

    public DebuggerThreadViewModel? SelectedThread
    {
        get => _selectedThread;
        set
        {
            if (SetProperty(ref _selectedThread, value))
            {
                ClearRegisters();
                ClearCallStack();
                RaiseThreadCommandStates();
                RaiseRegisterCommandStates();
                RaiseCallStackCommandStates();
                RaiseStepCommandStates();

                if (!_suppressAutomaticRegisterRefresh &&
                    !IsBusy &&
                    value is not null &&
                    _sessionState == DebuggerSessionState.Paused &&
                    IsTargetCurrent())
                {
                    _ = RefreshStopContextAfterThreadSelectionAsync(value.Id);
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

    public DebuggerBreakpointViewModel? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set
        {
            if (SetProperty(ref _selectedBreakpoint, value))
            {
                RaiseBreakpointCommandStates();
                OnPropertyChanged(nameof(CanNavigateToSelectedBreakpoint));
            }
        }
    }

    public DebuggerStackFrameViewModel? SelectedStackFrame
    {
        get => _selectedStackFrame;
        set
        {
            if (SetProperty(ref _selectedStackFrame, value))
            {
                RaiseCallStackCommandStates();
                RaiseStepCommandStates();
            }
        }
    }

    public bool HasBreakpointCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.Breakpoints);

    public bool HasWatchpointCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.Watchpoints);

    public bool HasBreakpointManagementCapability =>
        HasBreakpointCapability || HasWatchpointCapability;

    public bool HasCallStackCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.CallStack);

    public bool HasStepExecutionCapability =>
        _plugin.Instance.Capabilities.HasFlag(TargetCapabilities.StepExecution);

    public bool HasUpperDebuggerWorkspaceCapability =>
        HasBreakpointManagementCapability || HasCallStackCapability;

    public bool IsBreakpointsWorkspaceSelected =>
        HasBreakpointManagementCapability && !_isCallStackWorkspaceSelected;

    public bool IsCallStackWorkspaceSelected =>
        HasCallStackCapability && _isCallStackWorkspaceSelected;

    public bool CanNavigateToSelectedFrameDisassembler =>
        SelectedStackFrame is not null &&
        _sessionState == DebuggerSessionState.Paused &&
        IsTargetCurrent() &&
        _plugin.CanOpenDisassemblerForTarget(_targetProcess);

    public bool CanNavigateToSelectedFrameMemoryViewer =>
        SelectedStackFrame is not null &&
        _sessionState == DebuggerSessionState.Paused &&
        IsTargetCurrent() &&
        _plugin.CanOpenMemoryViewerForTarget(_targetProcess);

    public bool CanRunToAddress =>
        !_disposed &&
        !IsBusy &&
        HasStepExecutionCapability &&
        HasBreakpointCapability &&
        _sessionState == DebuggerSessionState.Paused &&
        IsTargetCurrent();

    public bool CanAddBreakpoint =>
        !_disposed &&
        !IsBusy &&
        HasBreakpointManagementCapability &&
        IsAttachedState() &&
        IsTargetCurrent();

    public bool CanRemoveAllBreakpoints => CanAddBreakpoint && Breakpoints.Count > 0;

    public bool CanNavigateToSelectedBreakpoint =>
        SelectedBreakpoint?.Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute &&
        IsTargetCurrent() &&
        _plugin.CanOpenDisassemblerForTarget(_targetProcess);

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

    internal bool CanResolveDisassemblyWatchpoint()
    {
        return !_disposed &&
               !IsBusy &&
               _sessionState == DebuggerSessionState.Paused &&
               Registers.Count > 0 &&
               IsTargetCurrent();
    }

    public bool CanExportDebuggerData => IsAttachedState();

    public bool CanCaptureSnapshot =>
        !_disposed &&
        !IsBusy &&
        _sessionState == DebuggerSessionState.Paused &&
        _latestStopContext is not null &&
        IsTargetCurrent();

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

    public ICommand RefreshBreakpointsCommand => _refreshBreakpointsCommand;

    public ICommand RefreshCallStackCommand => _refreshCallStackCommand;

    public ICommand StepIntoCommand => _stepIntoCommand;

    public ICommand StepOverCommand => _stepOverCommand;

    public ICommand StepOutCommand => _stepOutCommand;

    public ICommand RemoveBreakpointCommand => _removeBreakpointCommand;

    public ICommand EnableBreakpointCommand => _enableBreakpointCommand;

    public ICommand DisableBreakpointCommand => _disableBreakpointCommand;

    public ICommand ShowBreakpointsWorkspaceCommand => _showBreakpointsWorkspaceCommand;

    public ICommand ShowCallStackWorkspaceCommand => _showCallStackWorkspaceCommand;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OnPropertyChanged(nameof(CanCaptureSnapshot));
        _plugin.PropertyChanged -= OnPluginPropertyChanged;
        _lifetimeCancellation.Cancel();
        ClearSoftwareBreakpointInstructionState();
        ClearComposedExecutionState();

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

    internal bool IsAttachedTo(
        PluginViewModel plugin,
        TargetProcess targetProcess,
        long connectionGeneration)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(targetProcess);

        DebuggerSessionCoordinator? coordinator = _coordinator;
        return !_disposed &&
               ReferenceEquals(_plugin, plugin) &&
               coordinator is { IsAttached: true } &&
               (_sessionState is DebuggerSessionState.Attached or
                   DebuggerSessionState.Running or
                   DebuggerSessionState.Paused) &&
               coordinator.Identity.Matches(plugin.Metadata.Id, targetProcess, connectionGeneration) &&
               IsTargetCurrent();
    }

    internal DebuggerBreakpointValidationResult ValidateAddressActionRequest(
        DebuggerBreakpointRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool capabilityAvailable = request.Kind switch
        {
            DebuggerBreakpointKind.Software => HasBreakpointCapability,
            DebuggerBreakpointKind.Hardware => HasWatchpointCapability,
            _ => false
        };
        if (!CanAddBreakpoint || !capabilityAvailable ||
            _coordinator is not { IsAttached: true } coordinator ||
            !IsTargetCurrent())
        {
            return DebuggerBreakpointValidationResult.Invalid(
                "The Debugger must be attached to the current Active Target before this action can be used.");
        }

        IDebuggerBreakpointValidationService? validationService =
            coordinator.GetService<IDebuggerBreakpointValidationService>();
        if (validationService is null)
        {
            return DebuggerBreakpointValidationResult.Invalid(
                "The attached debugger backend does not expose request validation for address shortcuts.");
        }

        return validationService.ValidateBreakpointRequest(request);
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

    private bool CanRefreshBreakpoints()
    {
        return !_disposed &&
               !IsBusy &&
               HasBreakpointManagementCapability &&
               IsAttachedState() &&
               IsTargetCurrent();
    }

    private bool CanRefreshCallStack()
    {
        return !_disposed &&
               !IsBusy &&
               HasCallStackCapability &&
               SelectedThread is not null &&
               _sessionState == DebuggerSessionState.Paused &&
               IsTargetCurrent();
    }

    private bool CanStepInto()
    {
        return !_disposed &&
               !IsBusy &&
               HasStepExecutionCapability &&
               SelectedThread is not null &&
               _sessionState == DebuggerSessionState.Paused &&
               IsTargetCurrent();
    }

    private bool CanStepOver()
    {
        return CanStepInto() && CurrentInstructionPointer.HasValue;
    }

    private bool CanStepOut()
    {
        return CanStepInto() && SelectedStackFrame?.Frame.ReturnAddress.HasValue == true;
    }

    private void ShowBreakpointsWorkspace()
    {
        if (!HasBreakpointManagementCapability || !_isCallStackWorkspaceSelected)
        {
            return;
        }

        _isCallStackWorkspaceSelected = false;
        OnPropertyChanged(nameof(IsBreakpointsWorkspaceSelected));
        OnPropertyChanged(nameof(IsCallStackWorkspaceSelected));
    }

    private void ShowCallStackWorkspace()
    {
        if (!HasCallStackCapability || _isCallStackWorkspaceSelected)
        {
            return;
        }

        _isCallStackWorkspaceSelected = true;
        OnPropertyChanged(nameof(IsBreakpointsWorkspaceSelected));
        OnPropertyChanged(nameof(IsCallStackWorkspaceSelected));
    }

    private bool CanRemoveSelectedBreakpoint()
    {
        return CanRefreshBreakpoints() && SelectedBreakpoint is not null;
    }

    private bool CanEnableSelectedBreakpoint()
    {
        return CanRefreshBreakpoints() && SelectedBreakpoint?.IsEnabled == false;
    }

    private bool CanDisableSelectedBreakpoint()
    {
        return CanRefreshBreakpoints() && SelectedBreakpoint?.IsEnabled == true;
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
        ClearSoftwareBreakpointInstructionState();
        ClearComposedExecutionState();

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
            await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            if (coordinator.State == DebuggerSessionState.Paused)
            {
                await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
                await RefreshCallStackCoreAsync(showStatus: false).ConfigureAwait(true);
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
            await RefreshCallStackCoreAsync(showStatus: false).ConfigureAwait(true);
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
            RefreshDeferredStopContextIfNeeded();
        }
    }

    private async Task ContinueAsync()
    {
        if (!EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        _continueInProgress = true;
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Continuing target...";

        try
        {
            await coordinator
                .ContinueAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            coordinator.DisassemblyOverlayState.CompleteStagedSoftwareBreakpointRetirements();
            ApplyCoordinatorState(coordinator.State);
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            ApplyPostExecutionCommandState(
                coordinator,
                "Target running.",
                "Target paused.");
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
            _continueInProgress = false;
            IsBusy = false;
            RefreshDeferredStopContextIfNeeded();
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
            ClearCallStack();
            ClearBreakpoints();
            ClearSoftwareBreakpointInstructionState();
            ClearComposedExecutionState();
            ApplyCoordinatorState(DebuggerSessionState.Detached);
            IsBusy = false;
        }
    }


    private async Task RefreshCallStackAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await RefreshCallStackCoreAsync(showStatus: true).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RefreshCallStackCoreAsync(
        bool showStatus,
        int? preferredFrameIndex = null,
        ulong? expectedThreadId = null)
    {
        if (!HasCallStackCapability ||
            _sessionState != DebuggerSessionState.Paused ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator) ||
            SelectedThread is not DebuggerThreadViewModel selectedThread)
        {
            ClearCallStack();
            return false;
        }

        if (expectedThreadId.HasValue && selectedThread.Id != expectedThreadId.Value)
        {
            return false;
        }

        IDebuggerCallStackService? service = coordinator.GetService<IDebuggerCallStackService>();
        if (service is null)
        {
            ClearCallStack();
            ErrorText = "The plugin advertises call-stack support but the attached debugger session does not provide IDebuggerCallStackService.";
            if (showStatus)
            {
                StatusText = "Call-stack access is unavailable.";
            }
            return false;
        }

        int? selectedFrameIndex = preferredFrameIndex ?? SelectedStackFrame?.Index;
        ulong threadId = selectedThread.Id;
        try
        {
            IReadOnlyList<DebuggerStackFrame> frames = await service
                .GetCallStackAsync(threadId, _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (_sessionState != DebuggerSessionState.Paused ||
                SelectedThread?.Id != threadId ||
                !IsTargetCurrent())
            {
                return false;
            }

            CallFrames.Clear();
            foreach (DebuggerStackFrame frame in frames.OrderBy(frame => frame.Index))
            {
                CallFrames.Add(new DebuggerStackFrameViewModel(frame));
            }

            SelectedStackFrame = selectedFrameIndex.HasValue
                ? CallFrames.FirstOrDefault(frame => frame.Index == selectedFrameIndex.Value) ?? CallFrames.FirstOrDefault()
                : CallFrames.FirstOrDefault();

            if (showStatus)
            {
                StatusText = CallFrames.Count == 1
                    ? $"Loaded 1 call frame for thread {selectedThread.IdText}."
                    : $"Loaded {CallFrames.Count} call frames for thread {selectedThread.IdText}.";
            }

            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (showStatus)
            {
                StatusText = "Call-stack refresh cancelled.";
            }
            return false;
        }
        catch (Exception exception)
        {
            ClearCallStack();
            ErrorText = exception.Message;
            if (showStatus)
            {
                StatusText = "Call-stack refresh failed.";
            }
            return false;
        }
        finally
        {
            RaiseCallStackCommandStates();
        }
    }

    private Task StepIntoAsync()
    {
        return ExecuteNativeStepIntoAsync("Step Into");
    }

    private async Task StepOverAsync()
    {
        if (!CanStepOver() || CurrentInstructionPointer is not ulong instructionPointer)
        {
            return;
        }

        DisassembledInstruction? instruction = GetLogicalSoftwareBreakpointInstruction(instructionPointer);
        if (instruction is null)
        {
            IsBusy = true;
            ErrorText = string.Empty;
            StatusText = $"Resolving instruction at 0x{instructionPointer:X} for Step Over...";
            try
            {
                DisassemblySnapshot snapshot = await _plugin
                    .ReadDisassemblyContextAsync(
                        _targetProcess,
                        _connectionGeneration,
                        instructionPointer,
                        beforeByteCount: 0,
                        afterByteCount: 32,
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);
                instruction = snapshot.Instructions.FirstOrDefault(item => item.Address == instructionPointer);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
                StatusText = "Step Over cancelled.";
                return;
            }
            catch (Exception exception)
            {
                ErrorText = exception.Message;
                StatusText = "Step Over could not inspect the current instruction.";
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        if (instruction is null)
        {
            ErrorText = "The Disassembler did not return the current instruction.";
            StatusText = "Step Over could not resolve the current instruction.";
            return;
        }

        if (instruction.FlowControl != DisassemblyFlowControl.Call)
        {
            await ExecuteNativeStepIntoAsync("Step Over").ConfigureAwait(true);
            return;
        }

        ulong returnAddress;
        try
        {
            returnAddress = checked(instruction.Address + (ulong)instruction.Length);
        }
        catch (OverflowException exception)
        {
            ErrorText = exception.Message;
            StatusText = "Step Over could not calculate the instruction fall-through address.";
            return;
        }

        await RunToAddressAsync(returnAddress, "Step Over").ConfigureAwait(true);
    }

    private async Task StepOutAsync()
    {
        if (!CanStepOut() || SelectedStackFrame?.Frame.ReturnAddress is not ulong returnAddress)
        {
            return;
        }

        await RunToAddressAsync(returnAddress, "Step Out").ConfigureAwait(true);
    }

    private async Task ExecuteNativeStepIntoAsync(string operationName)
    {
        if (!CanStepInto() ||
            SelectedThread is not DebuggerThreadViewModel thread ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IDebuggerStepService? service = coordinator.GetService<IDebuggerStepService>();
        if (service is null)
        {
            ErrorText = "The plugin advertises stepping but the attached debugger session does not provide IDebuggerStepService.";
            StatusText = $"{operationName} is unavailable.";
            return;
        }

        _continueInProgress = true;
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"{operationName} on thread {thread.IdText}...";
        try
        {
            await service
                .StepAsync(DebuggerStepKind.Into, thread.Id, _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            ApplyPostExecutionCommandState(
                coordinator,
                $"{operationName} started.",
                $"{operationName} completed.");
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = $"{operationName} cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = $"{operationName} failed.";
        }
        finally
        {
            _continueInProgress = false;
            IsBusy = false;
            RefreshDeferredStopContextIfNeeded();
        }
    }

    public Task RunToAddressAsync(ulong address)
    {
        return RunToAddressAsync(address, "Run to Address");
    }

    private async Task RunToAddressAsync(ulong address, string operationName)
    {
        if (!CanRunToAddress || !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        DebuggerBreakpointViewModel? existing = Breakpoints.FirstOrDefault(item =>
            item.Breakpoint.Request.Kind == DebuggerBreakpointKind.Software &&
            item.Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute &&
            item.Breakpoint.Request.Address == address);
        if (existing is { IsEnabled: false })
        {
            ErrorText = $"A disabled software breakpoint already exists at 0x{address:X}. Enable or remove it before using {operationName}.";
            StatusText = $"{operationName} was not started.";
            return;
        }

        bool ownsTemporaryBreakpoint = existing is null;
        if (existing is null)
        {
            bool added = await AddBreakpointAsync(new DebuggerBreakpointRequest(
                    address,
                    1,
                    DebuggerBreakpointKind.Software,
                    DebuggerBreakpointAccess.Execute,
                    isTemporary: true))
                .ConfigureAwait(true);
            if (!added)
            {
                return;
            }

            existing = Breakpoints.FirstOrDefault(item =>
                item.Breakpoint.Request.Kind == DebuggerBreakpointKind.Software &&
                item.Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute &&
                item.Breakpoint.Request.Address == address &&
                item.Breakpoint.Request.IsTemporary);
            if (existing is null)
            {
                ErrorText = $"The temporary {operationName} breakpoint could not be resolved after creation.";
                StatusText = $"{operationName} was not started.";
                return;
            }
        }

        ComposedExecutionOperation operation = new(
            operationName,
            address,
            existing.Id,
            ownsTemporaryBreakpoint);
        _activeComposedExecutionOperation = operation;

        if (_sessionState != DebuggerSessionState.Paused || !IsTargetCurrent())
        {
            _activeComposedExecutionOperation = null;
            await CleanupComposedExecutionOperationAsync(operation, debugEvent: null).ConfigureAwait(true);
            return;
        }

        _continueInProgress = true;
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"{operationName}: continuing to 0x{address:X}...";
        try
        {
            await coordinator.ContinueAsync(_lifetimeCancellation.Token).ConfigureAwait(true);
            coordinator.DisassemblyOverlayState.CompleteStagedSoftwareBreakpointRetirements();
            ApplyCoordinatorState(coordinator.State);
            await RefreshThreadsCoreAsync(showStatus: false).ConfigureAwait(true);
            ApplyPostExecutionCommandState(
                coordinator,
                $"{operationName} is running toward 0x{address:X}.",
                $"{operationName} stopped.");
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = $"{operationName} cancelled.";
            if (ReferenceEquals(_activeComposedExecutionOperation, operation))
            {
                _activeComposedExecutionOperation = null;
                _pendingInterruptedComposedExecutionOperation = operation;
            }
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = $"{operationName} failed.";
            if (ReferenceEquals(_activeComposedExecutionOperation, operation))
            {
                _activeComposedExecutionOperation = null;
                _pendingInterruptedComposedExecutionOperation = operation;
            }
        }
        finally
        {
            _continueInProgress = false;
            IsBusy = false;

            if (_deferredStopContextEvent is null &&
                ReferenceEquals(_pendingInterruptedComposedExecutionOperation, operation))
            {
                _pendingInterruptedComposedExecutionOperation = null;
                await CleanupComposedExecutionOperationAsync(operation, debugEvent: null).ConfigureAwait(true);
            }

            RefreshDeferredStopContextIfNeeded();
        }
    }

    public Task<bool> AddSoftwareBreakpointAsync(ulong address, bool isTemporary)
    {
        return AddBreakpointAsync(new DebuggerBreakpointRequest(
            address,
            1,
            DebuggerBreakpointKind.Software,
            DebuggerBreakpointAccess.Execute,
            isTemporary));
    }

    public async Task<bool> AddBreakpointAsync(DebuggerBreakpointRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool capabilityAvailable = request.Kind switch
        {
            DebuggerBreakpointKind.Software => HasBreakpointCapability,
            DebuggerBreakpointKind.Hardware => HasWatchpointCapability,
            _ => false
        };
        if (!CanAddBreakpoint || !capabilityAvailable ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return false;
        }

        IDebuggerBreakpointService? service = coordinator.GetService<IDebuggerBreakpointService>();
        if (service is null)
        {
            ErrorText = "The plugin advertises breakpoint/watchpoint management but the attached debugger session does not provide IDebuggerBreakpointService.";
            StatusText = "Breakpoint/watchpoint management is unavailable.";
            return false;
        }

        string description = GetBreakpointDescription(request);
        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = $"Adding {description} at 0x{request.Address:X}...";

        try
        {
            DisassembledInstruction? originalInstruction = await CaptureSoftwareBreakpointInstructionAsync(request)
                .ConfigureAwait(true);
            DebuggerBreakpoint added = await service
                .AddBreakpointAsync(request, _lifetimeCancellation.Token)
                .ConfigureAwait(true);

            if (request.Kind == DebuggerBreakpointKind.Software &&
                request.Access == DebuggerBreakpointAccess.Execute)
            {
                if (originalInstruction is not null)
                {
                    _softwareBreakpointInstructionCache[request.Address] = originalInstruction;
                    coordinator.DisassemblyOverlayState.RememberSoftwareBreakpointInstruction(originalInstruction);
                }
                else
                {
                    _softwareBreakpointInstructionCache.Remove(request.Address);
                }
            }

            await RefreshBreakpointsCoreAsync(showStatus: false, preferredBreakpointId: added.Id).ConfigureAwait(true);
            StatusText = $"{(request.IsTemporary ? "Temporary " : string.Empty)}{description} added at 0x{request.Address:X}.";
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Breakpoint/watchpoint add cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Breakpoint/watchpoint add failed.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveAllBreakpointsAsync()
    {
        if (!CanRemoveAllBreakpoints || !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IDebuggerBreakpointService? service = coordinator.GetService<IDebuggerBreakpointService>();
        if (service is null)
        {
            ErrorText = "The attached debugger session does not provide breakpoint/watchpoint management.";
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        StatusText = "Removing all breakpoints/watchpoints...";
        try
        {
            DebuggerBreakpointViewModel[] currentBreakpoints = Breakpoints.ToArray();
            foreach (DebuggerBreakpointViewModel breakpoint in currentBreakpoints)
            {
                await service.RemoveBreakpointAsync(breakpoint.Id, _lifetimeCancellation.Token).ConfigureAwait(true);
                if (coordinator.State == DebuggerSessionState.Paused && IsSoftwareExecuteBreakpoint(breakpoint.Breakpoint))
                {
                    coordinator.DisassemblyOverlayState.StageSoftwareBreakpointRetirement(
                        breakpoint.Breakpoint.Request.Address);
                }
            }

            await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
            StatusText = "All breakpoints/watchpoints removed.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Remove All breakpoints/watchpoints cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Remove All breakpoints/watchpoints failed.";
            await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshBreakpointsAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await RefreshBreakpointsCoreAsync(showStatus: true).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveSelectedBreakpointAsync()
    {
        if (SelectedBreakpoint is not DebuggerBreakpointViewModel selected ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IDebuggerBreakpointService? service = coordinator.GetService<IDebuggerBreakpointService>();
        if (service is null)
        {
            ErrorText = "The attached debugger session does not provide breakpoint/watchpoint management.";
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        string description = GetBreakpointDescription(selected.Breakpoint.Request);
        StatusText = $"Removing {description} at {selected.AddressText}...";
        try
        {
            await service.RemoveBreakpointAsync(selected.Id, _lifetimeCancellation.Token).ConfigureAwait(true);
            if (coordinator.State == DebuggerSessionState.Paused && IsSoftwareExecuteBreakpoint(selected.Breakpoint))
            {
                coordinator.DisassemblyOverlayState.StageSoftwareBreakpointRetirement(
                    selected.Breakpoint.Request.Address);
            }
            await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
            StatusText = $"{description} at {selected.AddressText} removed.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Breakpoint/watchpoint removal cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Breakpoint/watchpoint removal failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task EnableSelectedBreakpointAsync() => SetSelectedBreakpointEnabledAsync(isEnabled: true);

    private Task DisableSelectedBreakpointAsync() => SetSelectedBreakpointEnabledAsync(isEnabled: false);

    private async Task SetSelectedBreakpointEnabledAsync(bool isEnabled)
    {
        if (SelectedBreakpoint is not DebuggerBreakpointViewModel selected ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            return;
        }

        IDebuggerBreakpointStateService? service = coordinator.GetService<IDebuggerBreakpointStateService>();
        if (service is null)
        {
            ErrorText = "The attached debugger session does not support enabling or disabling existing breakpoints/watchpoints.";
            StatusText = "Breakpoint/watchpoint state change is unavailable.";
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        string description = GetBreakpointDescription(selected.Breakpoint.Request);
        StatusText = $"{(isEnabled ? "Enabling" : "Disabling")} {description} at {selected.AddressText}...";
        try
        {
            await service
                .SetBreakpointEnabledAsync(selected.Id, isEnabled, _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            if (IsSoftwareExecuteBreakpoint(selected.Breakpoint))
            {
                if (isEnabled)
                {
                    coordinator.DisassemblyOverlayState.CancelSoftwareBreakpointRetirement(
                        selected.Breakpoint.Request.Address);
                }
                else if (coordinator.State == DebuggerSessionState.Paused)
                {
                    coordinator.DisassemblyOverlayState.StageSoftwareBreakpointRetirement(
                        selected.Breakpoint.Request.Address);
                }
            }
            await RefreshBreakpointsCoreAsync(showStatus: false, preferredBreakpointId: selected.Id).ConfigureAwait(true);
            StatusText = $"{description} at {selected.AddressText} {(isEnabled ? "enabled" : "disabled")}.";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            StatusText = "Breakpoint/watchpoint state change cancelled.";
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Breakpoint/watchpoint state change failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RefreshBreakpointsCoreAsync(bool showStatus, string? preferredBreakpointId = null)
    {
        if (!HasBreakpointManagementCapability || !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            ClearBreakpoints();
            return false;
        }

        IDebuggerBreakpointService? service = coordinator.GetService<IDebuggerBreakpointService>();
        if (service is null)
        {
            ClearBreakpoints();
            ErrorText = "The plugin advertises breakpoint/watchpoint management but the attached debugger session does not provide IDebuggerBreakpointService.";
            if (showStatus)
            {
                StatusText = "Breakpoint/watchpoint management is unavailable.";
            }
            return false;
        }

        string? selectedId = preferredBreakpointId ?? SelectedBreakpoint?.Id;
        try
        {
            IReadOnlyList<DebuggerBreakpoint> breakpoints = await service
                .GetBreakpointsAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            coordinator.DisassemblyOverlayState.SynchronizeBreakpoints(breakpoints, coordinator.State);

            Breakpoints.Clear();
            foreach (DebuggerBreakpoint breakpoint in breakpoints
                         .OrderBy(item => item.Request.Address)
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                Breakpoints.Add(new DebuggerBreakpointViewModel(breakpoint));
            }

            SelectedBreakpoint = selectedId is not null
                ? Breakpoints.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal))
                    ?? Breakpoints.FirstOrDefault()
                : Breakpoints.FirstOrDefault();

            if (showStatus)
            {
                StatusText = Breakpoints.Count == 1
                    ? "Loaded 1 breakpoint/watchpoint."
                    : $"Loaded {Breakpoints.Count} breakpoints/watchpoints.";
            }

            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (showStatus)
            {
                StatusText = "Breakpoint/watchpoint refresh cancelled.";
            }
            return false;
        }
        catch (Exception exception)
        {
            ClearBreakpoints();
            ErrorText = exception.Message;
            if (showStatus)
            {
                StatusText = "Breakpoint/watchpoint refresh failed.";
            }
            return false;
        }
        finally
        {
            RaiseBreakpointCommandStates();
        }
    }

    private static bool IsSoftwareExecuteBreakpoint(DebuggerBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        return breakpoint.Request.Kind == DebuggerBreakpointKind.Software &&
               breakpoint.Request.Access == DebuggerBreakpointAccess.Execute;
    }

    private async Task<DisassembledInstruction?> CaptureSoftwareBreakpointInstructionAsync(
        DebuggerBreakpointRequest request)
    {
        if (request.Kind != DebuggerBreakpointKind.Software ||
            request.Access != DebuggerBreakpointAccess.Execute ||
            !_plugin.CanOpenDisassemblerForTarget(_targetProcess))
        {
            return null;
        }

        try
        {
            DisassemblySnapshot snapshot = await _plugin
                .ReadDisassemblyContextAsync(
                    _targetProcess,
                    _connectionGeneration,
                    request.Address,
                    beforeByteCount: 0,
                    afterByteCount: 32,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            return snapshot.Instructions.FirstOrDefault(item =>
                item.Address == request.Address && item.IsValid);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Breakpoint creation must remain available even when optional instruction capture fails.
            return null;
        }
    }

    private DisassembledInstruction? GetLogicalSoftwareBreakpointInstruction(ulong instructionPointer)
    {
        if (_logicalSoftwareBreakpointStopAddress != instructionPointer ||
            SelectedThread is not DebuggerThreadViewModel selectedThread ||
            (_logicalSoftwareBreakpointStopThreadId.HasValue &&
             _logicalSoftwareBreakpointStopThreadId.Value != selectedThread.Id))
        {
            return null;
        }

        return _softwareBreakpointInstructionCache.TryGetValue(instructionPointer, out DisassembledInstruction? instruction)
            ? instruction
            : null;
    }

    private void SetLogicalSoftwareBreakpointStop(DebuggerEvent debugEvent)
    {
        DebuggerBreakpoint? breakpoint = debugEvent.TriggeredBreakpoint;
        if (debugEvent.Kind == DebuggerEventKind.Breakpoint &&
            breakpoint is not null &&
            breakpoint.Request.Kind == DebuggerBreakpointKind.Software &&
            breakpoint.Request.Access == DebuggerBreakpointAccess.Execute &&
            debugEvent.InstructionPointer.HasValue)
        {
            _logicalSoftwareBreakpointStopAddress = debugEvent.InstructionPointer.Value;
            _logicalSoftwareBreakpointStopThreadId = debugEvent.ThreadId;
            return;
        }

        ClearLogicalSoftwareBreakpointStop();
    }

    private void ClearLogicalSoftwareBreakpointStop()
    {
        _logicalSoftwareBreakpointStopAddress = null;
        _logicalSoftwareBreakpointStopThreadId = null;
    }

    private void ClearSoftwareBreakpointInstructionState()
    {
        _softwareBreakpointInstructionCache.Clear();
        ClearLogicalSoftwareBreakpointStop();
    }

    private void ClearComposedExecutionState()
    {
        _activeComposedExecutionOperation = null;
        _pendingInterruptedComposedExecutionOperation = null;
        _deferredStopContextEvent = null;
    }

    private static bool IsComposedExecutionTargetEvent(
        ComposedExecutionOperation operation,
        DebuggerEvent debugEvent)
    {
        if (debugEvent.Kind != DebuggerEventKind.Breakpoint ||
            debugEvent.TriggeredBreakpoint is not DebuggerBreakpoint breakpoint ||
            !string.Equals(breakpoint.Id, operation.BreakpointId, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private async Task CleanupComposedExecutionOperationAsync(
        ComposedExecutionOperation operation,
        DebuggerEvent? debugEvent)
    {
        if (!operation.OwnsTemporaryBreakpoint ||
            !EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            if (debugEvent is not null)
            {
                await RefreshStopContextFromEventAsync(debugEvent).ConfigureAwait(true);
            }
            return;
        }

        IDebuggerBreakpointService? service = coordinator.GetService<IDebuggerBreakpointService>();
        if (service is null)
        {
            ErrorText = "The attached debugger session cannot clean up the interrupted temporary breakpoint.";
            if (debugEvent is not null)
            {
                await RefreshStopContextFromEventAsync(debugEvent).ConfigureAwait(true);
            }
            return;
        }

        bool wasBusy = IsBusy;
        IsBusy = true;
        try
        {
            IReadOnlyList<DebuggerBreakpoint> current = await service
                .GetBreakpointsAsync(_lifetimeCancellation.Token)
                .ConfigureAwait(true);
            if (current.Any(item => string.Equals(item.Id, operation.BreakpointId, StringComparison.Ordinal)))
            {
                await service
                    .RemoveBreakpointAsync(operation.BreakpointId, _lifetimeCancellation.Token)
                    .ConfigureAwait(true);
                if (coordinator.State == DebuggerSessionState.Paused)
                {
                    coordinator.DisassemblyOverlayState.StageSoftwareBreakpointRetirement(operation.Address);
                }
            }

            if (debugEvent is not null)
            {
                await RefreshStopContextFromEventAsync(debugEvent).ConfigureAwait(true);
                StatusText = $"{operation.Name} was interrupted before 0x{operation.Address:X}; the operation-owned temporary breakpoint was retired. {debugEvent.Message}".Trim();
            }
            else
            {
                await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            if (debugEvent is not null)
            {
                StatusText = $"{operation.Name} was interrupted before 0x{operation.Address:X}; temporary breakpoint cleanup will complete during session cleanup.";
            }
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            if (debugEvent is not null)
            {
                StatusText = $"{operation.Name} was interrupted before 0x{operation.Address:X}, and temporary breakpoint cleanup reported an error.";
                await RefreshStopContextFromEventAsync(debugEvent).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = wasBusy;
        }
    }

    internal void ReportExternalStatus(string status, string? error = null)
    {
        StatusText = status ?? string.Empty;
        ErrorText = error ?? string.Empty;
    }

    public async Task<DebuggerSnapshot?> CaptureSnapshotAsync(
        string? label = null,
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanCaptureSnapshot || _latestStopContext is not DebuggerEventContext eventContext)
        {
            ErrorText = "Cannot capture snapshot because the debugger is not paused on the current Active Target.";
            return null;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        CancellationToken captureToken = linkedCancellation.Token;

        try
        {
            long stopSequence = eventContext.Sequence;
            DebuggerEvent debugEvent = eventContext.Event;
            ulong? selectedThreadId = SelectedThread?.Id;
            if (debugEvent.ThreadId.HasValue && selectedThreadId.HasValue && debugEvent.ThreadId != selectedThreadId)
            {
                throw new InvalidOperationException(
                    "The selected debugger thread no longer matches the current stop context. Select the stopped thread before capturing a snapshot.");
            }

            DebuggerSnapshotRegister[] registers = Registers.Select(register => new DebuggerSnapshotRegister(
                register.Register.Id,
                register.Register.DisplayName,
                register.Register.Group,
                register.Register.Role,
                register.Register.BitWidth,
                register.Register.ValueEncoding,
                Convert.ToHexString(register.Register.Value.Span),
                register.ValueText,
                register.Register.CanWrite)).ToArray();

            DebuggerSnapshotCallFrame[] frames = CallFrames.Select(frame =>
            {
                MemoryRegion? region = _plugin.ActiveMemoryRegions.FirstOrDefault(candidate =>
                    frame.Frame.InstructionAddress >= candidate.BaseAddress &&
                    frame.Frame.InstructionAddress < candidate.EndAddressExclusive);
                string? module = frame.Frame.ModuleName ?? region?.ModuleName ?? region?.Name;
                ulong? moduleBase = FindModuleBase(module, region);
                ulong? moduleOffset = moduleBase.HasValue && frame.Frame.InstructionAddress >= moduleBase.Value
                    ? frame.Frame.InstructionAddress - moduleBase.Value
                    : null;
                return new DebuggerSnapshotCallFrame(
                    frame.Frame.Index,
                    frame.Frame.InstructionAddress,
                    frame.Frame.ReturnAddress,
                    frame.Frame.StackPointer,
                    frame.Frame.FramePointer,
                    module,
                    moduleBase,
                    moduleOffset,
                    frame.Frame.SymbolName);
            }).ToArray();

            DebuggerSnapshotBreakpoint[] breakpoints = Breakpoints.Select(item => new DebuggerSnapshotBreakpoint(
                item.Breakpoint.Id,
                item.Breakpoint.Request.Address,
                item.Breakpoint.IsEnabled,
                item.Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute
                    ? DebuggerSnapshotBreakpointType.Breakpoint
                    : DebuggerSnapshotBreakpointType.Watchpoint,
                item.Breakpoint.Request.Kind,
                item.Breakpoint.Request.Access,
                item.Breakpoint.Request.Size,
                item.Breakpoint.Request.IsTemporary
                    ? DebuggerSnapshotBreakpointLifetime.Temporary
                    : DebuggerSnapshotBreakpointLifetime.Persistent,
                string.Equals(item.Breakpoint.Id, debugEvent.TriggeredBreakpoint?.Id, StringComparison.Ordinal))).ToArray();

            List<DebuggerSnapshotInstruction> instructions = new();
            DebuggerSnapshotSectionStatus disassemblyStatus =
                DebuggerSnapshotSectionStatus.Unavailable("No instruction pointer was available.");
            if (debugEvent.InstructionPointer.HasValue && _plugin.CanOpenDisassemblerForTarget(_targetProcess))
            {
                try
                {
                    List<ulong> origins = new() { debugEvent.InstructionPointer.Value };
                    if (debugEvent.TriggerInstructionAddress is ulong triggerAddress && !origins.Contains(triggerAddress))
                    {
                        origins.Add(triggerAddress);
                    }

                    Dictionary<ulong, DebuggerSnapshotInstruction> uniqueInstructions = new();
                    foreach (ulong origin in origins)
                    {
                        captureToken.ThrowIfCancellationRequested();
                        DisassemblySnapshot disassembly = await _plugin.ReadDisassemblyContextAsync(
                            _targetProcess,
                            _connectionGeneration,
                            origin,
                            beforeByteCount: 64,
                            afterByteCount: 64,
                            captureToken).ConfigureAwait(true);

                        string? module = disassembly.Region.ModuleName ?? disassembly.Region.Name;
                        ulong? moduleBase = FindModuleBase(module, disassembly.Region);
                        foreach (DisassembledInstruction instruction in disassembly.Instructions)
                        {
                            string instructionText = string.IsNullOrWhiteSpace(instruction.Operands)
                                ? instruction.Mnemonic
                                : $"{instruction.Mnemonic} {instruction.Operands}";
                            string[] markers = disassembly.Markers
                                .Where(marker => marker.Address == instruction.Address)
                                .Select(marker => marker.Text)
                                .Distinct(StringComparer.Ordinal)
                                .ToArray();
                            ulong? moduleOffset = moduleBase.HasValue && instruction.Address >= moduleBase.Value
                                ? instruction.Address - moduleBase.Value
                                : null;

                            uniqueInstructions[instruction.Address] = new DebuggerSnapshotInstruction(
                                instruction.Address,
                                module,
                                moduleOffset,
                                Convert.ToHexString(instruction.RawBytes.Span),
                                instructionText,
                                instruction.FlowControl,
                                instruction.BranchTarget,
                                Array.AsReadOnly(markers));
                        }
                    }

                    instructions.AddRange(uniqueInstructions.Values.OrderBy(instruction => instruction.Address));
                    disassemblyStatus = DebuggerSnapshotSectionStatus.Complete;
                }
                catch (OperationCanceledException) when (captureToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    disassemblyStatus = DebuggerSnapshotSectionStatus.Failed(exception.Message);
                }
            }

            List<DebuggerSnapshotMemoryBlock> memory = new();
            DebuggerSnapshotSectionStatus memoryStatus =
                DebuggerSnapshotSectionStatus.Skipped("No stack pointer was available for bounded standard memory capture.");
            ulong? stackPointer = Registers
                .FirstOrDefault(register => register.Register.Role == DebuggerRegisterRole.StackPointer) is DebuggerRegisterViewModel stackRegister
                ? DecodeRegisterUnsigned(stackRegister.Register)
                : SelectedStackFrame?.Frame.StackPointer;

            if (stackPointer.HasValue && _plugin.SupportsMemoryRead && _plugin.ActiveMemoryRegions.Count > 0)
            {
                try
                {
                    MemoryViewSnapshot stackSnapshot = await _plugin.ReadMemoryViewerWindowAsync(
                        _targetProcess,
                        _connectionGeneration,
                        stackPointer.Value,
                        256,
                        captureToken).ConfigureAwait(true);
                    memory.Add(new DebuggerSnapshotMemoryBlock(
                        "Stack",
                        stackSnapshot.StartAddress,
                        Convert.ToHexString(stackSnapshot.Bytes.Span)));
                    memoryStatus = DebuggerSnapshotSectionStatus.Complete;
                }
                catch (OperationCanceledException) when (captureToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    memoryStatus = DebuggerSnapshotSectionStatus.Failed(exception.Message);
                }
            }

            if (_sessionState != DebuggerSessionState.Paused ||
                _latestStopContext?.Sequence != stopSequence ||
                !IsTargetCurrent())
            {
                throw new InvalidOperationException(
                    "Debugger stop context changed while the snapshot was being captured.");
            }

            PluginMetadata metadata = _plugin.Metadata;
            TargetArchitecture architecture = metadata.Architecture;
            DebuggerSnapshotSource source = new(
                AppInfo.Version,
                AppInfo.Revision,
                metadata.Id,
                metadata.Name,
                metadata.Version.ToString(),
                metadata.Revision,
                metadata.ApiVersion.ToString(),
                metadata.Platform,
                _targetProcess.Id,
                _targetProcess.Name,
                architecture.Cpu,
                architecture.AddressWidthBits,
                architecture.PointerWidthBits,
                architecture.Endianness,
                _connectionGeneration,
                DebuggerSnapshotCaptureSource.Live);

            DebuggerSnapshotEventContext snapshotEvent = new(
                _latestStopContext.Sequence,
                debugEvent.Timestamp,
                debugEvent.Kind,
                debugEvent.ExecutionState,
                debugEvent.StopReason,
                debugEvent.ThreadId,
                SelectedThread?.Name,
                debugEvent.InstructionPointer,
                debugEvent.TriggerInstructionAddress,
                debugEvent.TriggerResolution,
                debugEvent.WatchedAddress,
                debugEvent.WatchpointAccess,
                debugEvent.WatchpointSize,
                debugEvent.TriggeredBreakpoint?.Id,
                debugEvent.TriggeredBreakpoint is DebuggerBreakpoint triggeredBreakpoint
                    ? triggeredBreakpoint.Request.Access == DebuggerBreakpointAccess.Execute
                        ? DebuggerSnapshotBreakpointType.Breakpoint
                        : DebuggerSnapshotBreakpointType.Watchpoint
                    : null,
                debugEvent.TriggeredBreakpoint?.Request.Kind,
                debugEvent.TriggeredBreakpoint is DebuggerBreakpoint lifetimeBreakpoint
                    ? lifetimeBreakpoint.Request.IsTemporary
                        ? DebuggerSnapshotBreakpointLifetime.Temporary
                        : DebuggerSnapshotBreakpointLifetime.Persistent
                    : null,
                debugEvent.Message);

            DebuggerSnapshotSections sections = new(
                !HasRegisterAccessCapability
                    ? DebuggerSnapshotSectionStatus.Unavailable("Register access is not supported.")
                    : registers.Length > 0
                        ? DebuggerSnapshotSectionStatus.Complete
                        : DebuggerSnapshotSectionStatus.Unavailable("No register snapshot was available for the stopped thread."),
                !HasCallStackCapability
                    ? DebuggerSnapshotSectionStatus.Unavailable("Call stack is not supported.")
                    : frames.Length > 0
                        ? DebuggerSnapshotSectionStatus.Complete
                        : DebuggerSnapshotSectionStatus.Unavailable("No call-stack frames were available for the stopped thread."),
                disassemblyStatus,
                HasBreakpointManagementCapability
                    ? DebuggerSnapshotSectionStatus.Complete
                    : DebuggerSnapshotSectionStatus.Unavailable("Breakpoint management is not supported."),
                memoryStatus);

            string snapshotLabel = string.IsNullOrWhiteSpace(label)
                ? $"{debugEvent.Kind} {DateTime.Now:HH:mm:ss}"
                : label.Trim();
            DebuggerSnapshot snapshot = _snapshotCaptureService.Capture(new DebuggerSnapshotCaptureRequest(
                source,
                snapshotEvent,
                registers,
                frames,
                instructions,
                breakpoints,
                memory,
                sections,
                snapshotLabel,
                group?.Trim() ?? string.Empty));
            StatusText = $"Captured debugger snapshot '{snapshot.Label}'.";
            return snapshot;
        }
        catch (OperationCanceledException) when (captureToken.IsCancellationRequested)
        {
            StatusText = "Debugger snapshot capture cancelled. No partial snapshot was published.";
            return null;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            StatusText = "Debugger snapshot capture failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ulong? FindModuleBase(string? moduleName, MemoryRegion? fallbackRegion)
    {
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            MemoryRegion[] regions = _plugin.ActiveMemoryRegions
                .Where(region => string.Equals(
                    region.ModuleName ?? region.Name,
                    moduleName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (regions.Length > 0)
            {
                return regions.Min(region => region.BaseAddress);
            }
        }

        return fallbackRegion?.BaseAddress;
    }

    private ulong? DecodeRegisterUnsigned(DebuggerRegister register)
    {
        ReadOnlySpan<byte> bytes = register.Value.Span;
        if (bytes.Length == 0 || bytes.Length > sizeof(ulong))
        {
            return null;
        }

        ulong value = 0;
        if (_plugin.Metadata.Architecture.Endianness == Endianness.Big)
        {
            foreach (byte current in bytes)
            {
                value = (value << 8) | current;
            }
            return value;
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            value |= (ulong)bytes[index] << (index * 8);
        }
        return value;
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
                await RefreshCallStackCoreAsync(showStatus: false).ConfigureAwait(true);
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

    private async Task RefreshStopContextAfterThreadSelectionAsync(ulong threadId)
    {
        try
        {
            if (HasRegisterAccessCapability)
            {
                await RefreshRegistersCoreAsync(showStatus: false, expectedThreadId: threadId).ConfigureAwait(true);
            }

            if (HasCallStackCapability)
            {
                await RefreshCallStackCoreAsync(showStatus: false, expectedThreadId: threadId).ConfigureAwait(true);
            }
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

    private static string GetBreakpointDescription(DebuggerBreakpointRequest request)
    {
        if (request.Kind == DebuggerBreakpointKind.Software)
        {
            return "software breakpoint";
        }

        string access = request.Access switch
        {
            DebuggerBreakpointAccess.Read => "read",
            DebuggerBreakpointAccess.Write => "write",
            DebuggerBreakpointAccess.ReadWrite => "read/write",
            _ => request.Access.ToString().ToLowerInvariant()
        };
        return $"{access} hardware watchpoint";
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

    private void ClearCallStack()
    {
        CallFrames.Clear();
        SelectedStackFrame = null;
        RaiseCallStackCommandStates();
        RaiseStepCommandStates();
    }

    private void ClearBreakpoints()
    {
        Breakpoints.Clear();
        SelectedBreakpoint = null;
        RaiseBreakpointCommandStates();
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
        ClearCallStack();
        ClearBreakpoints();
        ClearSoftwareBreakpointInstructionState();
        ClearComposedExecutionState();
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

        if (sender is DebuggerSessionCoordinator coordinator)
        {
            coordinator.DisassemblyOverlayState.ObserveEvent(context.Event);
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
            SetLogicalSoftwareBreakpointStop(context.Event);

            if (context.Event.ExecutionState == DebuggerExecutionState.Running)
            {
                _latestStopContext = null;
                _deferredStopContextEvent = null;
                ClearRegisters();
                ClearCallStack();
                OnPropertyChanged(nameof(CanCaptureSnapshot));
            }
            else if (context.Event.ExecutionState == DebuggerExecutionState.Paused)
            {
                _latestStopContext = context;
                OnPropertyChanged(nameof(CanCaptureSnapshot));
                if (context.Event.InstructionPointer.HasValue)
                {
                    CurrentInstructionPointer = context.Event.InstructionPointer.Value;
                }

                if (_activeComposedExecutionOperation is ComposedExecutionOperation operation)
                {
                    _activeComposedExecutionOperation = null;
                    if (!IsComposedExecutionTargetEvent(operation, context.Event) &&
                        operation.OwnsTemporaryBreakpoint)
                    {
                        _pendingInterruptedComposedExecutionOperation = operation;
                    }
                }

                if (!IsBusy)
                {
                    _deferredStopContextEvent = context.Event;
                    RefreshDeferredStopContextIfNeeded();
                }
                else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)
                {
                    _deferredStopContextEvent = context.Event;
                }
            }
        });
    }

    private void RefreshDeferredStopContextIfNeeded()
    {
        if (_deferredStopContextEvent is not DebuggerEvent debugEvent ||
            _disposed ||
            IsBusy ||
            _sessionState != DebuggerSessionState.Paused ||
            !IsTargetCurrent())
        {
            return;
        }

        _deferredStopContextEvent = null;
        if (_pendingInterruptedComposedExecutionOperation is ComposedExecutionOperation interruptedOperation)
        {
            _pendingInterruptedComposedExecutionOperation = null;
            _ = CleanupComposedExecutionOperationAsync(interruptedOperation, debugEvent);
            return;
        }

        _ = RefreshStopContextFromEventAsync(debugEvent);
    }

    private void ApplyPostExecutionCommandState(
        DebuggerSessionCoordinator coordinator,
        string runningStatus,
        string pausedFallbackStatus)
    {
        DebuggerSessionState finalState = coordinator.State;
        ApplyCoordinatorState(finalState);

        if (finalState == DebuggerSessionState.Running)
        {
            StatusText = runningStatus;
        }
        else if (finalState == DebuggerSessionState.Paused && _deferredStopContextEvent is null)
        {
            StatusText = pausedFallbackStatus;
        }
    }

    private async Task RefreshStopContextFromEventAsync(DebuggerEvent debugEvent)
    {
        try
        {
            debugEvent = await ResolveWatchpointTriggerAsync(debugEvent).ConfigureAwait(true);
            await RefreshBreakpointsCoreAsync(showStatus: false).ConfigureAwait(true);
            await RefreshThreadsCoreAsync(
                    showStatus: false,
                    preferredThreadId: debugEvent.ThreadId ?? SelectedThread?.Id)
                .ConfigureAwait(true);

            if (_sessionState == DebuggerSessionState.Paused)
            {
                await RefreshRegistersCoreAsync(showStatus: false).ConfigureAwait(true);
                await RefreshCallStackCoreAsync(showStatus: false).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task<DebuggerEvent> ResolveWatchpointTriggerAsync(DebuggerEvent debugEvent)
    {
        if (debugEvent.Kind != DebuggerEventKind.Watchpoint ||
            debugEvent.ExecutionState != DebuggerExecutionState.Paused ||
            !debugEvent.InstructionPointer.HasValue ||
            debugEvent.TriggerInstructionAddress.HasValue ||
            !_plugin.CanOpenDisassemblerForTarget(_targetProcess))
        {
            return debugEvent;
        }

        DebuggerEvent resolvedEvent = debugEvent;
        try
        {
            DisassemblySnapshot snapshot = await _plugin
                .ReadDisassemblyContextAsync(
                    _targetProcess,
                    _connectionGeneration,
                    debugEvent.InstructionPointer.Value,
                    beforeByteCount: 64,
                    afterByteCount: 16,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);
            resolvedEvent = _watchpointTriggerResolver.Resolve(debugEvent, snapshot);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Trigger resolution is best-effort. The real stop instruction remains authoritative.
        }

        if (EnsureCurrentAttachedCoordinator(out DebuggerSessionCoordinator? coordinator))
        {
            coordinator.DisassemblyOverlayState.ObserveEvent(resolvedEvent);
        }

        ReplaceDisplayedEvent(debugEvent, resolvedEvent);

        if (resolvedEvent.TriggerInstructionAddress is ulong triggerAddress &&
            resolvedEvent.InstructionPointer is ulong stopAddress &&
            resolvedEvent.WatchedAddress is ulong watchedAddress)
        {
            StatusText = $"Watchpoint at 0x{watchedAddress:X} triggered by instruction 0x{triggerAddress:X}; execution stopped at 0x{stopAddress:X}.";
        }
        else if (resolvedEvent.InstructionPointer is ulong unresolvedStopAddress)
        {
            StatusText = $"Watchpoint stopped execution at 0x{unresolvedStopAddress:X}; the triggering instruction could not be resolved safely.";
        }

        return resolvedEvent;
    }

    private void ReplaceDisplayedEvent(DebuggerEvent originalEvent, DebuggerEvent resolvedEvent)
    {
        if (ReferenceEquals(originalEvent, resolvedEvent))
        {
            return;
        }

        for (int index = Events.Count - 1; index >= 0; index--)
        {
            DebuggerEventViewModel viewModel = Events[index];
            if (!ReferenceEquals(viewModel.Context.Event, originalEvent))
            {
                continue;
            }

            DebuggerEventContext resolvedContext = new(
                viewModel.Context.Identity,
                viewModel.Context.Sequence,
                resolvedEvent);
            Events[index] = new DebuggerEventViewModel(resolvedContext);
            if (_latestStopContext?.Sequence == viewModel.Context.Sequence)
            {
                _latestStopContext = resolvedContext;
            }
            break;
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
            _latestStopContext = null;
            _deferredStopContextEvent = null;
            ClearLogicalSoftwareBreakpointStop();
            ClearRegisters();
            ClearCallStack();
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
        OnPropertyChanged(nameof(CanCaptureSnapshot));
        OnPropertyChanged(nameof(CanExportDebuggerData));
    }

    private void RaiseCommandStates()
    {
        _attachCommand.RaiseCanExecuteChanged();
        _pauseCommand.RaiseCanExecuteChanged();
        _continueCommand.RaiseCanExecuteChanged();
        _detachCommand.RaiseCanExecuteChanged();
        _clearEventsCommand.RaiseCanExecuteChanged();
        _showBreakpointsWorkspaceCommand.RaiseCanExecuteChanged();
        _showCallStackWorkspaceCommand.RaiseCanExecuteChanged();
        RaiseThreadCommandStates();
        RaiseRegisterCommandStates();
        RaiseBreakpointCommandStates();
        RaiseCallStackCommandStates();
        RaiseStepCommandStates();
        OnPropertyChanged(nameof(HasBreakpointManagementCapability));
        OnPropertyChanged(nameof(HasCallStackCapability));
        OnPropertyChanged(nameof(HasStepExecutionCapability));
        OnPropertyChanged(nameof(HasUpperDebuggerWorkspaceCapability));
        OnPropertyChanged(nameof(IsBreakpointsWorkspaceSelected));
        OnPropertyChanged(nameof(IsCallStackWorkspaceSelected));
        OnPropertyChanged(nameof(CanAddBreakpoint));
        OnPropertyChanged(nameof(CanRemoveAllBreakpoints));
        OnPropertyChanged(nameof(CanNavigateToSelectedBreakpoint));
        OnPropertyChanged(nameof(CanNavigateToSelectedFrameDisassembler));
        OnPropertyChanged(nameof(CanNavigateToSelectedFrameMemoryViewer));
        OnPropertyChanged(nameof(CanRunToAddress));
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
        RaiseStepCommandStates();
    }

    private void RaiseBreakpointCommandStates()
    {
        _refreshBreakpointsCommand.RaiseCanExecuteChanged();
        _removeBreakpointCommand.RaiseCanExecuteChanged();
        _enableBreakpointCommand.RaiseCanExecuteChanged();
        _disableBreakpointCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddBreakpoint));
        OnPropertyChanged(nameof(CanRemoveAllBreakpoints));
        OnPropertyChanged(nameof(CanNavigateToSelectedBreakpoint));
    }

    private void RaiseCallStackCommandStates()
    {
        _refreshCallStackCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanNavigateToSelectedFrameDisassembler));
        OnPropertyChanged(nameof(CanNavigateToSelectedFrameMemoryViewer));
    }

    private void RaiseStepCommandStates()
    {
        _stepIntoCommand.RaiseCanExecuteChanged();
        _stepOverCommand.RaiseCanExecuteChanged();
        _stepOutCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunToAddress));
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

    private sealed record ComposedExecutionOperation(
        string Name,
        ulong Address,
        string BreakpointId,
        bool OwnsTemporaryBreakpoint);
}
