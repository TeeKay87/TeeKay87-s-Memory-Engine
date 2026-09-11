using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5DebuggerSession :
    IDebuggerSession,
    IDebuggerThreadService,
    IDebuggerThreadControlService,
    IDebuggerRegisterService,
    IDebuggerBreakpointService,
    IDebuggerBreakpointValidationService,
    IDebuggerBreakpointStateService,
    IDebuggerCallStackService,
    IDebuggerStepService
{
    private static readonly TimeSpan EventChannelTimeout = TimeSpan.FromSeconds(5);

    private readonly object _stateGate = new();
    private readonly Ps5DebuggerCommandClient _commandClient;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _supportsExtendedRegisterReads;
    private readonly Func<TargetProcess, IReadOnlyList<MemoryRegion>?> _getCachedMemoryRegions;
    private readonly TcpClient _eventClient;
    private readonly NetworkStream _eventStream;
    private readonly CancellationTokenSource _eventCancellation = new();
    private readonly Action<Ps5DebuggerSession> _onDisposed;
    private readonly HashSet<ulong> _suspendedThreadIds = new();
    private readonly HashSet<uint> _unavailableOptionalRegisterCommands = new();
    private readonly Dictionary<string, SoftwareBreakpointEntry> _breakpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ulong> _pendingBreakpointDisables = new();
    private readonly Dictionary<string, HardwareWatchpointEntry> _watchpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<int, DebuggerBreakpointRequest> _pendingWatchpointDisables = new();
    private readonly Task _eventLoopTask;
    private int _nextBreakpointSequence = 1;
    private DebuggerExecutionState _state = DebuggerExecutionState.Running;
    private bool _detaching;
    private bool _stepInProgress;
    private uint? _stepThreadId;
    private SoftwareBreakpointStopSnapshot? _softwareBreakpointStopSnapshot;
    private bool _disposed;

    private Ps5DebuggerSession(
        string host,
        int port,
        TargetProcess process,
        Ps5DebuggerCommandClient commandClient,
        TcpClient eventClient,
        bool supportsExtendedRegisterReads,
        Func<TargetProcess, IReadOnlyList<MemoryRegion>?> getCachedMemoryRegions,
        Action<Ps5DebuggerSession> onDisposed)
    {
        _host = host;
        _port = port;
        Process = process;
        _commandClient = commandClient;
        _supportsExtendedRegisterReads = supportsExtendedRegisterReads;
        _getCachedMemoryRegions = getCachedMemoryRegions;
        _eventClient = eventClient;
        _eventClient.NoDelay = true;
        _eventStream = eventClient.GetStream();
        _onDisposed = onDisposed;
        _eventLoopTask = RunEventLoopAsync(_eventCancellation.Token);
    }

    public TargetProcess Process { get; }

    public DebuggerExecutionState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<DebuggerEventEventArgs>? EventReceived;

    public static async Task<Ps5DebuggerSession> AttachAsync(
        string host,
        int port,
        TargetProcess process,
        bool supportsExtendedRegisterReads,
        Func<TargetProcess, IReadOnlyList<MemoryRegion>?> getCachedMemoryRegions,
        Action<Ps5DebuggerSession> onDisposed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(getCachedMemoryRegions);
        ArgumentNullException.ThrowIfNull(onDisposed);

        if (process.Id is 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG signed 32-bit debugger PID range.");
        }

        TcpListener listener = new(IPAddress.Any, Ps5DebugProtocol.DebuggerInterruptPort);
        Ps5DebuggerCommandClient? commandClient = null;
        TcpClient? eventClient = null;
        bool attached = false;

        try
        {
            try
            {
                listener.Start(1);
            }
            catch (SocketException exception)
            {
                throw new InvalidOperationException(
                    $"Could not listen for the ps5debug-NG debugger interrupt channel on TCP port {Ps5DebugProtocol.DebuggerInterruptPort}. Make sure no other debugger session is using that port.",
                    exception);
            }

            using CancellationTokenSource eventTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            eventTimeout.CancelAfter(EventChannelTimeout);

            Task<TcpClient> acceptTask = listener
                .AcceptTcpClientAsync(eventTimeout.Token)
                .AsTask();

            commandClient = await Ps5DebuggerCommandClient
                .ConnectAsync(host, port, cancellationToken)
                .ConfigureAwait(false);

            await commandClient
                .AttachAsync(checked((int)process.Id), cancellationToken)
                .ConfigureAwait(false);
            attached = true;

            try
            {
                eventClient = await acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"ps5debug-NG did not establish its debugger interrupt connection to TCP port {Ps5DebugProtocol.DebuggerInterruptPort} within {EventChannelTimeout.TotalSeconds:0} seconds.");
            }

            listener.Stop();
            return new Ps5DebuggerSession(
                host,
                port,
                process,
                commandClient,
                eventClient,
                supportsExtendedRegisterReads,
                getCachedMemoryRegions,
                onDisposed);
        }
        catch
        {
            listener.Stop();
            eventClient?.Dispose();

            if (commandClient is not null)
            {
                if (attached)
                {
                    try
                    {
                        await commandClient.DetachAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Preserve the original attach/event-channel failure.
                    }
                }

                await commandClient.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Running, "pause");
        ClearStepState();
        ClearSoftwareBreakpointStopSnapshot();

        await _commandClient
            .SetExecutionActionAsync(Ps5DebugProtocol.DebuggerActionPause, cancellationToken)
            .ConfigureAwait(false);

        SetState(DebuggerExecutionState.Paused);
        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Paused,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.PauseRequested,
            message: "PS5 target paused by debugger request."));
    }

    public async Task ContinueAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Paused, "continue");

        await FlushPendingBreakpointDisablesAsync(cancellationToken).ConfigureAwait(false);
        await FlushPendingWatchpointDisablesAsync(cancellationToken).ConfigureAwait(false);
        ClearStepState();
        ClearSoftwareBreakpointStopSnapshot();

        await _commandClient
            .SetExecutionActionAsync(Ps5DebugProtocol.DebuggerActionResume, cancellationToken)
            .ConfigureAwait(false);

        SetState(DebuggerExecutionState.Running);
        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running,
            DebuggerStopReason.None,
            message: "PS5 target resumed by debugger request."));
    }

    public async Task DetachAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateGate)
        {
            if (_state == DebuggerExecutionState.Detached)
            {
                return;
            }

            _detaching = true;
        }

        // Explicitly remove every client-owned debugger instrument before backend detach.
        // ps5debug-NG has its own teardown path, but software breakpoints and hardware
        // watchpoints must not depend on that path being able to rediscover every active slot.
        // Keep the event channel open while disable commands run because the backend can
        // briefly stop/resume the target as part of cleanup; teardown interrupts are drained
        // but ignored while _detaching is set, then the channel is closed in the finally block.

        List<Exception> cleanupFailures = new();
        Exception? detachFailure = null;
        try
        {
            try
            {
                await RestoreSoftwareBreakpointsBeforeDetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            try
            {
                await RestoreHardwareWatchpointsBeforeDetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            try
            {
                await _commandClient.DetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                detachFailure = exception;
            }

            if (detachFailure is null)
            {
                SetState(DebuggerExecutionState.Detached);
                lock (_stateGate)
                {
                    _suspendedThreadIds.Clear();
                    _unavailableOptionalRegisterCommands.Clear();
                    _breakpoints.Clear();
                    _pendingBreakpointDisables.Clear();
                    _watchpoints.Clear();
                    _pendingWatchpointDisables.Clear();
                    _stepInProgress = false;
                    _stepThreadId = null;
                    _softwareBreakpointStopSnapshot = null;
                }
            }

            if (cleanupFailures.Count > 0 && detachFailure is not null)
            {
                List<Exception> failures = new(cleanupFailures) { detachFailure };
                throw new AggregateException(
                    "PS5 debugger instrumentation cleanup and detach both reported errors.",
                    failures);
            }

            if (detachFailure is not null)
            {
                throw detachFailure;
            }

            if (cleanupFailures.Count > 0)
            {
                Exception cleanupFailure = cleanupFailures.Count == 1
                    ? cleanupFailures[0]
                    : new AggregateException(cleanupFailures);
                throw new InvalidOperationException(
                    "The PS5 debugger detached, but one or more client-owned breakpoints or watchpoints could not be explicitly cleared before teardown.",
                    cleanupFailure);
            }
        }
        finally
        {
            StopEventChannel();
        }
    }

    public TService? GetService<TService>() where TService : class
    {
        EnsureNotDisposed();
        return this as TService;
    }

    public async Task<IReadOnlyList<DebuggerThreadInfo>> GetThreadsAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureAttached();

        IReadOnlyList<Ps5DebuggerThreadInfo> backendThreads = await _commandClient
            .GetThreadsAsync(cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            HashSet<ulong> currentIds = backendThreads
                .Select(thread => (ulong)thread.Id)
                .ToHashSet();
            _suspendedThreadIds.RemoveWhere(threadId => !currentIds.Contains(threadId));

            return backendThreads
                .Select(thread => new DebuggerThreadInfo(
                    thread.Id,
                    thread.Name,
                    GetThreadStateNoLock(thread.Id)))
                .ToArray();
        }
    }

    public async Task<IReadOnlyList<DebuggerRegister>> GetRegistersAsync(
        ulong threadId,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Paused, "read registers");
        uint backendThreadId = ValidateThreadId(threadId);

        SoftwareBreakpointStopSnapshot? stopSnapshot = GetSoftwareBreakpointStopSnapshot(backendThreadId);
        byte[] rawRegisters = stopSnapshot?.GeneralRegisters.ToArray() ??
            await _commandClient
                .GetGeneralRegistersAsync(backendThreadId, cancellationToken)
                .ConfigureAwait(false);

        List<DebuggerRegister> registers = new(Ps5GeneralRegisterMapper.Map(rawRegisters));
        if (!_supportsExtendedRegisterReads)
        {
            return registers;
        }

        Task<byte[]?> floatingPointTask = stopSnapshot is not null
            ? Task.FromResult<byte[]?>(stopSnapshot.FloatingPointRegisters.ToArray())
            : ReadOptionalRegisterBlockAsync(
                Ps5DebugProtocol.CommandDebugGetFloatingPointRegisters,
                () => Ps5DebuggerCommandClient.ProbeFloatingPointRegistersAsync(
                    _host,
                    _port,
                    backendThreadId,
                    cancellationToken),
                cancellationToken);
        Task<byte[]?> fsGsBaseTask = ReadOptionalRegisterBlockAsync(
            Ps5DebugProtocol.CommandDebugGetFsGsBase,
            () => Ps5DebuggerCommandClient.ProbeFsGsBaseAsync(
                _host,
                _port,
                backendThreadId,
                cancellationToken),
            cancellationToken);

        await Task.WhenAll(floatingPointTask, fsGsBaseTask).ConfigureAwait(false);
        byte[]? floatingPointRegisters = await floatingPointTask.ConfigureAwait(false);
        byte[]? fsGsBaseRegisters = await fsGsBaseTask.ConfigureAwait(false);

        registers.AddRange(Ps5ExtendedRegisterMapper.Map(
            floatingPointRegisters,
            null,
            fsGsBaseRegisters));
        return registers;
    }

    public async Task<IReadOnlyList<DebuggerStackFrame>> GetCallStackAsync(
        ulong threadId,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Paused, "read the call stack");
        uint backendThreadId = ValidateThreadId(threadId);

        SoftwareBreakpointStopSnapshot? stopSnapshot = GetSoftwareBreakpointStopSnapshot(backendThreadId);
        byte[] registers = stopSnapshot?.GeneralRegisters.ToArray() ??
            await _commandClient
                .GetGeneralRegistersAsync(backendThreadId, cancellationToken)
                .ConfigureAwait(false);
        ulong instructionPointer = BinaryPrimitives.ReadUInt64LittleEndian(
            registers.AsSpan(Ps5DebugProtocol.DebuggerRegisterInstructionPointerOffset, sizeof(ulong)));
        ulong framePointer = BinaryPrimitives.ReadUInt64LittleEndian(
            registers.AsSpan(Ps5DebugProtocol.DebuggerRegisterFramePointerOffset, sizeof(ulong)));
        ulong stackPointer = BinaryPrimitives.ReadUInt64LittleEndian(
            registers.AsSpan(Ps5DebugProtocol.DebuggerRegisterStackPointerOffset, sizeof(ulong)));

        IReadOnlyList<Ps5DebuggerStackFrameInfo> backendFrames = await _commandClient
            .ReadCallStackAsync(
                checked((int)Process.Id),
                framePointer,
                stackPointer,
                Ps5DebugProtocol.DebuggerStackMaximumDepth,
                cancellationToken)
            .ConfigureAwait(false);

        if (backendFrames.Count == 0)
        {
            return new[]
            {
                new DebuggerStackFrame(
                    0,
                    instructionPointer,
                    stackPointer,
                    framePointer,
                    null,
                    ResolveModuleName(instructionPointer))
            };
        }

        DebuggerStackFrame[] frames = new DebuggerStackFrame[backendFrames.Count];
        for (int index = 0; index < backendFrames.Count; index++)
        {
            Ps5DebuggerStackFrameInfo frame = backendFrames[index];
            ulong frameInstructionAddress = index == 0
                ? instructionPointer
                : backendFrames[index - 1].ReturnAddress;
            frames[index] = new DebuggerStackFrame(
                index,
                frameInstructionAddress,
                frame.StackPointer,
                frame.FramePointer,
                frame.ReturnAddress == 0 ? null : frame.ReturnAddress,
                ResolveModuleName(frameInstructionAddress));
        }

        return frames;
    }

    public async Task StepAsync(
        DebuggerStepKind stepKind,
        ulong? threadId,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Paused, "step");
        if (stepKind != DebuggerStepKind.Into)
        {
            throw new NotSupportedException(
                "The PS5 backend exposes native Step Into. Step Over and Step Out are composed by the host from disassembly, call frames, temporary breakpoints, and Continue.");
        }

        uint? backendThreadId = threadId.HasValue ? ValidateThreadId(threadId.Value) : null;
        SoftwareBreakpointStopSnapshot? stopSnapshot = GetSoftwareBreakpointStopSnapshot(backendThreadId);
        if (stopSnapshot is not null)
        {
            await CompleteTransparentBreakpointStepAsync(stopSnapshot, cancellationToken).ConfigureAwait(false);
            return;
        }

        await FlushPendingBreakpointDisablesAsync(cancellationToken).ConfigureAwait(false);
        await FlushPendingWatchpointDisablesAsync(cancellationToken).ConfigureAwait(false);

        lock (_stateGate)
        {
            if (_state != DebuggerExecutionState.Paused)
            {
                throw new InvalidOperationException("The PS5 debugger target changed state before Step Into could start.");
            }

            _stepInProgress = true;
            _stepThreadId = backendThreadId;
        }

        try
        {
            await _commandClient.StepAsync(backendThreadId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ClearStepState();
            throw;
        }

        SetState(DebuggerExecutionState.Running);
        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running,
            DebuggerStopReason.None,
            backendThreadId,
            message: backendThreadId.HasValue
                ? $"PS5 target resumed for Step Into on thread 0x{backendThreadId.Value:X}."
                : "PS5 target resumed for Step Into."));
    }

    private async Task<byte[]?> ReadOptionalRegisterBlockAsync(
        uint command,
        Func<Task<byte[]?>> reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateGate)
        {
            if (_unavailableOptionalRegisterCommands.Contains(command))
            {
                return null;
            }
        }

        byte[]? result = await reader().ConfigureAwait(false);
        if (result is not null)
        {
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_stateGate)
        {
            _unavailableOptionalRegisterCommands.Add(command);
        }

        return null;
    }

    public Task WriteRegisterAsync(
        ulong threadId,
        DebuggerRegisterWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Paused, "write registers");
        _ = ValidateThreadId(threadId);

        throw new NotSupportedException(
            "Register editing is disabled for the current ps5debug-NG backend. PS5 register snapshots are read-only.");
    }

    public Task<IReadOnlyList<DebuggerBreakpoint>> GetBreakpointsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        EnsureAttached();

        lock (_stateGate)
        {
            IReadOnlyList<DebuggerBreakpoint> snapshot = _breakpoints.Values
                .Select(entry => entry.Breakpoint)
                .Concat(_watchpoints.Values.Select(entry => entry.Breakpoint))
                .OrderBy(breakpoint => breakpoint.Request.Address)
                .ThenBy(breakpoint => breakpoint.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(snapshot);
        }
    }

    public DebuggerBreakpointValidationResult ValidateBreakpointRequest(DebuggerBreakpointRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureNotDisposed();

        try
        {
            EnsureAttached();
            if (request.Kind == DebuggerBreakpointKind.Software)
            {
                ValidateSoftwareExecuteBreakpoint(request);
                ValidateSoftwareExecuteBreakpointAddress(request);

                lock (_stateGate)
                {
                    if (_breakpoints.Values.Any(entry => entry.Breakpoint.Request.Address == request.Address) ||
                        _pendingBreakpointDisables.Values.Contains(request.Address))
                    {
                        return DebuggerBreakpointValidationResult.Invalid(
                            $"A software execute breakpoint already exists or is awaiting cleanup at 0x{request.Address:X}.");
                    }

                    if (FindFreeBreakpointSlotNoLock() < 0)
                    {
                        return DebuggerBreakpointValidationResult.Invalid(
                            $"ps5debug-NG has no free software-breakpoint slots ({Ps5DebugProtocol.MaximumSoftwareBreakpointCount} maximum).");
                    }
                }

                return DebuggerBreakpointValidationResult.Valid();
            }

            if (request.Kind == DebuggerBreakpointKind.Hardware)
            {
                ValidateHardwareWatchpoint(request);
                ValidateHardwareWatchpointAddress(request);

                lock (_stateGate)
                {
                    if (_watchpoints.Values.Any(entry => IsEquivalentWatchpoint(entry.Breakpoint.Request, request)) ||
                        _pendingWatchpointDisables.Values.Any(pending => IsEquivalentWatchpoint(pending, request)))
                    {
                        return DebuggerBreakpointValidationResult.Invalid(
                            $"An equivalent hardware watchpoint already exists or is awaiting cleanup at 0x{request.Address:X}.");
                    }

                    if (FindFreeWatchpointSlotNoLock() < 0)
                    {
                        return DebuggerBreakpointValidationResult.Invalid(
                            $"ps5debug-NG has no free hardware-watchpoint slots ({Ps5DebugProtocol.MaximumHardwareWatchpointCount} maximum).");
                    }
                }

                return DebuggerBreakpointValidationResult.Valid();
            }

            return DebuggerBreakpointValidationResult.Invalid(
                $"Unsupported PS5 breakpoint kind '{request.Kind}'.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return DebuggerBreakpointValidationResult.Invalid(exception.Message);
        }
    }

    public Task<DebuggerBreakpoint> AddBreakpointAsync(
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        EnsureAttached();

        return request.Kind switch
        {
            DebuggerBreakpointKind.Software => AddSoftwareBreakpointAsync(request, cancellationToken),
            DebuggerBreakpointKind.Hardware => AddHardwareWatchpointAsync(request, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported PS5 breakpoint kind '{request.Kind}'.")
        };
    }

    private async Task<DebuggerBreakpoint> AddSoftwareBreakpointAsync(
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSoftwareExecuteBreakpoint(request);
        ValidateSoftwareExecuteBreakpointAddress(request);

        int slotIndex;
        string id;
        lock (_stateGate)
        {
            if (_breakpoints.Values.Any(entry => entry.Breakpoint.Request.Address == request.Address) ||
                _pendingBreakpointDisables.Values.Contains(request.Address))
            {
                throw new InvalidOperationException($"A software execute breakpoint already exists or is awaiting cleanup at 0x{request.Address:X}.");
            }

            slotIndex = FindFreeBreakpointSlotNoLock();
            if (slotIndex < 0)
            {
                throw new InvalidOperationException(
                    $"ps5debug-NG has no free software-breakpoint slots ({Ps5DebugProtocol.MaximumSoftwareBreakpointCount} maximum).");
            }

            id = $"ps5-sw-{slotIndex:D2}-{_nextBreakpointSequence++:D4}";
        }

        await _commandClient
            .SetSoftwareBreakpointAsync(slotIndex, isEnabled: true, request.Address, cancellationToken)
            .ConfigureAwait(false);

        DebuggerBreakpoint breakpoint = new(id, request, isEnabled: true);
        lock (_stateGate)
        {
            if (_disposed || _state == DebuggerExecutionState.Detached)
            {
                throw new InvalidOperationException("The PS5 debugger session became unavailable while the breakpoint was being added.");
            }

            _breakpoints.Add(id, new SoftwareBreakpointEntry(slotIndex, breakpoint, backendEnabled: true));
        }

        return breakpoint;
    }

    private async Task<DebuggerBreakpoint> AddHardwareWatchpointAsync(
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken)
    {
        ValidateHardwareWatchpoint(request);
        ValidateHardwareWatchpointAddress(request);

        int slotIndex;
        string id;
        lock (_stateGate)
        {
            if (_watchpoints.Values.Any(entry => IsEquivalentWatchpoint(entry.Breakpoint.Request, request)))
            {
                throw new InvalidOperationException(
                    $"An equivalent hardware watchpoint already exists at 0x{request.Address:X}.");
            }

            slotIndex = FindFreeWatchpointSlotNoLock();
            if (slotIndex < 0)
            {
                throw new InvalidOperationException(
                    $"ps5debug-NG has no free hardware-watchpoint slots ({Ps5DebugProtocol.MaximumHardwareWatchpointCount} maximum).");
            }

            id = $"ps5-hw-{slotIndex:D2}-{_nextBreakpointSequence++:D4}";
        }

        await SetHardwareWatchpointBackendStateAsync(slotIndex, isEnabled: true, request, cancellationToken)
            .ConfigureAwait(false);

        DebuggerBreakpoint watchpoint = new(id, request, isEnabled: true);
        lock (_stateGate)
        {
            if (_disposed || _state == DebuggerExecutionState.Detached)
            {
                throw new InvalidOperationException("The PS5 debugger session became unavailable while the watchpoint was being added.");
            }

            _watchpoints.Add(id, new HardwareWatchpointEntry(slotIndex, watchpoint, backendEnabled: true));
        }

        return watchpoint;
    }

    public async Task RemoveBreakpointAsync(string breakpointId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakpointId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        EnsureAttached();

        HardwareWatchpointEntry? hardwareEntry;
        lock (_stateGate)
        {
            _watchpoints.TryGetValue(breakpointId, out hardwareEntry);
        }
        if (hardwareEntry is not null)
        {
            await RemoveHardwareWatchpointAsync(breakpointId, hardwareEntry, cancellationToken).ConfigureAwait(false);
            return;
        }

        SoftwareBreakpointEntry entry;
        DebuggerExecutionState state;
        lock (_stateGate)
        {
            if (!_breakpoints.TryGetValue(breakpointId, out SoftwareBreakpointEntry? foundEntry) || foundEntry is null)
            {
                throw new KeyNotFoundException($"Unknown PS5 breakpoint '{breakpointId}'.");
            }

            entry = foundEntry;
            state = _state;
            if (entry.BackendEnabled && state == DebuggerExecutionState.Paused)
            {
                _pendingBreakpointDisables[entry.SlotIndex] = entry.Breakpoint.Request.Address;
                _breakpoints.Remove(breakpointId);
                return;
            }

            if (!entry.BackendEnabled)
            {
                _breakpoints.Remove(breakpointId);
                return;
            }
        }

        await _commandClient
            .SetSoftwareBreakpointAsync(
                entry.SlotIndex,
                isEnabled: false,
                entry.Breakpoint.Request.Address,
                cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            _breakpoints.Remove(breakpointId);
        }
    }

    public async Task SetBreakpointEnabledAsync(
        string breakpointId,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakpointId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();
        EnsureAttached();

        HardwareWatchpointEntry? hardwareEntry;
        lock (_stateGate)
        {
            _watchpoints.TryGetValue(breakpointId, out hardwareEntry);
        }
        if (hardwareEntry is not null)
        {
            await SetHardwareWatchpointEnabledAsync(breakpointId, hardwareEntry, isEnabled, cancellationToken).ConfigureAwait(false);
            return;
        }

        SoftwareBreakpointEntry entry;
        DebuggerExecutionState state;
        bool cancelPendingDisable = false;
        lock (_stateGate)
        {
            if (!_breakpoints.TryGetValue(breakpointId, out SoftwareBreakpointEntry? foundEntry) || foundEntry is null)
            {
                throw new KeyNotFoundException($"Unknown PS5 breakpoint '{breakpointId}'.");
            }

            entry = foundEntry;
            if (entry.Breakpoint.IsEnabled == isEnabled)
            {
                return;
            }

            state = _state;
            if (isEnabled && entry.BackendEnabled && _pendingBreakpointDisables.Remove(entry.SlotIndex))
            {
                cancelPendingDisable = true;
                entry.Breakpoint = new DebuggerBreakpoint(entry.Breakpoint.Id, entry.Breakpoint.Request, isEnabled: true);
            }
            else if (!isEnabled && entry.BackendEnabled && state == DebuggerExecutionState.Paused)
            {
                _pendingBreakpointDisables[entry.SlotIndex] = entry.Breakpoint.Request.Address;
                entry.Breakpoint = new DebuggerBreakpoint(entry.Breakpoint.Id, entry.Breakpoint.Request, isEnabled: false);
                return;
            }
        }

        if (cancelPendingDisable)
        {
            return;
        }

        await _commandClient
            .SetSoftwareBreakpointAsync(
                entry.SlotIndex,
                isEnabled,
                entry.Breakpoint.Request.Address,
                cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            if (_breakpoints.TryGetValue(breakpointId, out SoftwareBreakpointEntry? current))
            {
                current.BackendEnabled = isEnabled;
                current.Breakpoint = new DebuggerBreakpoint(current.Breakpoint.Id, current.Breakpoint.Request, isEnabled);
                _pendingBreakpointDisables.Remove(current.SlotIndex);
            }
        }
    }

    private async Task RemoveHardwareWatchpointAsync(
        string breakpointId,
        HardwareWatchpointEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.BackendEnabled)
        {
            await SetHardwareWatchpointBackendStateAsync(
                    entry.SlotIndex,
                    isEnabled: false,
                    entry.Breakpoint.Request,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        lock (_stateGate)
        {
            _watchpoints.Remove(breakpointId);
            _pendingWatchpointDisables.Remove(entry.SlotIndex);
        }
    }

    private async Task SetHardwareWatchpointEnabledAsync(
        string breakpointId,
        HardwareWatchpointEntry entry,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        if (entry.Breakpoint.IsEnabled == isEnabled)
        {
            return;
        }

        await SetHardwareWatchpointBackendStateAsync(
                entry.SlotIndex,
                isEnabled,
                entry.Breakpoint.Request,
                cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            if (_watchpoints.TryGetValue(breakpointId, out HardwareWatchpointEntry? current))
            {
                current.BackendEnabled = isEnabled;
                current.Breakpoint = new DebuggerBreakpoint(
                    current.Breakpoint.Id,
                    current.Breakpoint.Request,
                    isEnabled);
                _pendingWatchpointDisables.Remove(current.SlotIndex);
            }
        }
    }

    private Task SetHardwareWatchpointBackendStateAsync(
        int slotIndex,
        bool isEnabled,
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken)
    {
        uint lengthEncoding = request.Size switch
        {
            1 => 0u,
            2 => 1u,
            4 => 3u,
            8 => 2u,
            _ => throw new NotSupportedException($"Unsupported PS5 hardware-watchpoint size {request.Size}.")
        };
        uint breakType = request.Access switch
        {
            DebuggerBreakpointAccess.Write => 1u,
            DebuggerBreakpointAccess.ReadWrite => 3u,
            DebuggerBreakpointAccess.Read => throw new NotSupportedException(
                "ps5debug-NG/x86 debug registers do not provide a distinct read-only data-watchpoint mode. Use ReadWrite when reads must be observed."),
            _ => throw new NotSupportedException(
                "PS5 hardware watchpoints support Write or ReadWrite data access only.")
        };

        return _commandClient.SetHardwareWatchpointAsync(
            slotIndex,
            isEnabled,
            lengthEncoding,
            breakType,
            request.Address,
            cancellationToken);
    }

    public async Task SuspendThreadAsync(ulong threadId, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Running, "suspend an individual thread");
        uint backendThreadId = ValidateThreadId(threadId);

        lock (_stateGate)
        {
            if (_suspendedThreadIds.Contains(threadId))
            {
                throw new InvalidOperationException($"PS5 thread 0x{threadId:X} is already suspended.");
            }
        }

        await _commandClient
            .SuspendThreadAsync(backendThreadId, cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            _suspendedThreadIds.Add(threadId);
        }
    }

    public async Task ResumeThreadAsync(ulong threadId, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        EnsureState(DebuggerExecutionState.Running, "resume an individual thread");
        uint backendThreadId = ValidateThreadId(threadId);

        lock (_stateGate)
        {
            if (!_suspendedThreadIds.Contains(threadId))
            {
                throw new InvalidOperationException($"PS5 thread 0x{threadId:X} is not suspended.");
            }
        }

        await _commandClient
            .ResumeThreadAsync(backendThreadId, cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            _suspendedThreadIds.Remove(threadId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool notifyProvider;
        bool shouldDetach;

        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            notifyProvider = true;
            shouldDetach = _state != DebuggerExecutionState.Detached;
            _detaching = true;
        }

        if (shouldDetach)
        {
            try
            {
                await RestoreSoftwareBreakpointsBeforeDetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Disposal remains best-effort; hardware cleanup and backend detach still get a chance to run.
            }

            try
            {
                await RestoreHardwareWatchpointsBeforeDetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Disposal remains best-effort; backend detach still gets a chance to run its own teardown.
            }

            try
            {
                await _commandClient.DetachAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Disposal is best-effort after the owning host has already invalidated the session.
            }
        }

        SetState(DebuggerExecutionState.Detached);
        lock (_stateGate)
        {
            _suspendedThreadIds.Clear();
            _unavailableOptionalRegisterCommands.Clear();
            _breakpoints.Clear();
            _pendingBreakpointDisables.Clear();
            _watchpoints.Clear();
            _pendingWatchpointDisables.Clear();
        }
        StopEventChannel();

        try
        {
            await _eventLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }

        _eventStream.Dispose();
        _eventClient.Dispose();
        await _commandClient.DisposeAsync().ConfigureAwait(false);
        _eventCancellation.Dispose();

        if (notifyProvider)
        {
            _onDisposed(this);
        }
    }

    private async Task RunEventLoopAsync(CancellationToken cancellationToken)
    {
        byte[] packet = new byte[Ps5DebugProtocol.DebuggerInterruptPacketSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _eventStream.ReadExactlyAsync(packet, cancellationToken).ConfigureAwait(false);
                ProcessInterrupt(packet);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            bool reportFailure;
            lock (_stateGate)
            {
                reportFailure = !_disposed && !_detaching && _state != DebuggerExecutionState.Detached;
                if (reportFailure)
                {
                    _state = DebuggerExecutionState.Unknown;
                }
            }

            if (reportFailure)
            {
                RaiseEvent(new DebuggerEvent(
                    DebuggerEventKind.Other,
                    DebuggerExecutionState.Unknown,
                    DebuggerStopReason.Backend,
                    message: "The PS5 debugger interrupt channel disconnected. Detach the debugger before attaching again."));
            }
        }
    }

    private void ProcessInterrupt(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != Ps5DebugProtocol.DebuggerInterruptPacketSize)
        {
            return;
        }

        uint threadId = BinaryPrimitives.ReadUInt32LittleEndian(
            packet.Slice(Ps5DebugProtocol.DebuggerInterruptThreadIdOffset, sizeof(uint)));
        uint waitStatus = BinaryPrimitives.ReadUInt32LittleEndian(
            packet.Slice(Ps5DebugProtocol.DebuggerInterruptStatusOffset, sizeof(uint)));
        ulong instructionPointer = BinaryPrimitives.ReadUInt64LittleEndian(
            packet.Slice(Ps5DebugProtocol.DebuggerInterruptInstructionPointerOffset, sizeof(ulong)));
        ulong debugStatus = BinaryPrimitives.ReadUInt64LittleEndian(
            packet.Slice(Ps5DebugProtocol.DebuggerInterruptDebugRegisterStatusOffset, sizeof(ulong)));
        string threadName = ReadNullTerminatedUtf8(
            packet.Slice(
                Ps5DebugProtocol.DebuggerInterruptThreadNameOffset,
                Ps5DebugProtocol.DebuggerInterruptThreadNameLength));
        byte signal = checked((byte)((waitStatus >> 8) & 0xFF));

        DebuggerBreakpoint? hitBreakpoint = null;
        DebuggerBreakpoint[] hitWatchpoints = Array.Empty<DebuggerBreakpoint>();
        bool inferredSingleWatchpoint = false;
        bool stepCompleted = false;
        int ambiguousWatchpointCount = 0;
        lock (_stateGate)
        {
            if (_detaching || _disposed || _state == DebuggerExecutionState.Detached)
            {
                return;
            }

            _state = DebuggerExecutionState.Paused;
            _softwareBreakpointStopSnapshot = null;
            bool matchesPendingStep = _stepInProgress &&
                (!_stepThreadId.HasValue || _stepThreadId.Value == threadId);
            stepCompleted = signal == 5 && matchesPendingStep;
            if (matchesPendingStep)
            {
                _stepInProgress = false;
                _stepThreadId = null;
            }
            SoftwareBreakpointEntry? hitEntry = signal == 5
                ? _breakpoints.Values.FirstOrDefault(entry =>
                    entry.Breakpoint.IsEnabled &&
                    entry.BackendEnabled &&
                    entry.Breakpoint.Request.Address == instructionPointer)
                : null;
            if (hitEntry is not null)
            {
                hitBreakpoint = hitEntry.Breakpoint;
                _softwareBreakpointStopSnapshot = new SoftwareBreakpointStopSnapshot(
                    threadId,
                    hitBreakpoint.Request.Address,
                    packet.Slice(
                            Ps5DebugProtocol.DebuggerInterruptRegisterOffset,
                            Ps5DebugProtocol.DebuggerGeneralRegisterSize)
                        .ToArray(),
                    packet.Slice(
                            Ps5DebugProtocol.DebuggerInterruptFloatingPointRegisterOffset,
                            Ps5DebugProtocol.DebuggerFloatingPointRegisterSize)
                        .ToArray());
                if (hitBreakpoint.Request.IsTemporary)
                {
                    _breakpoints.Remove(hitBreakpoint.Id);
                    _pendingBreakpointDisables[hitEntry.SlotIndex] = hitBreakpoint.Request.Address;
                }
            }
            else if (signal == 5)
            {
                HardwareWatchpointEntry[] activeEntries = _watchpoints.Values
                    .Where(entry => entry.Breakpoint.IsEnabled && entry.BackendEnabled)
                    .OrderBy(entry => entry.SlotIndex)
                    .ToArray();
                HardwareWatchpointEntry[] matchingEntries = activeEntries
                    .Where(entry => (debugStatus & (1UL << entry.SlotIndex)) != 0)
                    .ToArray();

                if (matchingEntries.Length == 0 && (debugStatus & 0xFUL) == 0 && !stepCompleted)
                {
                    if (activeEntries.Length == 1)
                    {
                        matchingEntries = activeEntries;
                        inferredSingleWatchpoint = true;
                    }
                    else if (activeEntries.Length > 1)
                    {
                        ambiguousWatchpointCount = activeEntries.Length;
                    }
                }

                hitWatchpoints = matchingEntries
                    .Select(entry => entry.Breakpoint)
                    .ToArray();

                foreach (HardwareWatchpointEntry entry in matchingEntries)
                {
                    if (!entry.Breakpoint.Request.IsTemporary)
                    {
                        continue;
                    }

                    _watchpoints.Remove(entry.Breakpoint.Id);
                    _pendingWatchpointDisables[entry.SlotIndex] = entry.Breakpoint.Request;
                }
            }
        }

        string threadText = string.IsNullOrWhiteSpace(threadName)
            ? $"thread 0x{threadId:X}"
            : $"{threadName} (0x{threadId:X})";

        if (hitBreakpoint is not null)
        {
            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.Breakpoint,
                DebuggerExecutionState.Paused,
                DebuggerStopReason.Breakpoint,
                threadId,
                instructionPointer,
                hitBreakpoint,
                $"PS5 software breakpoint hit at 0x{instructionPointer:X} on {threadText}."));
            return;
        }

        if (hitWatchpoints.Length > 0)
        {
            foreach (DebuggerBreakpoint watchpoint in hitWatchpoints)
            {
                string attribution = inferredSingleWatchpoint
                    ? " The backend interrupt did not preserve DR6 trigger-slot status, so the hit was inferred because this is the only active hardware watchpoint."
                    : string.Empty;
                RaiseEvent(new DebuggerEvent(
                    DebuggerEventKind.Watchpoint,
                    DebuggerExecutionState.Paused,
                    DebuggerStopReason.Watchpoint,
                    threadId,
                    instructionPointer,
                    watchpoint,
                    $"PS5 {FormatWatchpointAccess(watchpoint.Request.Access)} watchpoint at 0x{watchpoint.Request.Address:X} hit from instruction 0x{instructionPointer:X} on {threadText}.{attribution}"));
            }

            return;
        }

        if (stepCompleted)
        {
            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.StepCompleted,
                DebuggerExecutionState.Paused,
                DebuggerStopReason.StepCompleted,
                threadId,
                instructionPointer,
                $"PS5 Step Into completed at 0x{instructionPointer:X} on {threadText}."));
            return;
        }

        if (signal == 5 && ambiguousWatchpointCount > 1)
        {
            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.Other,
                DebuggerExecutionState.Paused,
                DebuggerStopReason.Signal,
                threadId,
                instructionPointer,
                $"PS5 debugger stop: signal 5 on {threadText}. {ambiguousWatchpointCount} hardware watchpoints are active, but the backend interrupt did not preserve DR6 trigger-slot status, so the exact watchpoint cannot be attributed safely."));
            return;
        }

        string signalText = signal == 0 ? "backend stop" : $"signal {signal}";
        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Other,
            DebuggerExecutionState.Paused,
            signal == 0 ? DebuggerStopReason.Backend : DebuggerStopReason.Signal,
            threadId,
            instructionPointer,
            $"PS5 debugger stop: {signalText} on {threadText}."));
    }

    private async Task RestoreSoftwareBreakpointsBeforeDetachAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<int, ulong>[] breakpointsToRestore;
        lock (_stateGate)
        {
            Dictionary<int, ulong> bySlot = new(_pendingBreakpointDisables);
            foreach (SoftwareBreakpointEntry entry in _breakpoints.Values)
            {
                if (entry.BackendEnabled)
                {
                    bySlot[entry.SlotIndex] = entry.Breakpoint.Request.Address;
                }
            }

            breakpointsToRestore = bySlot
                .OrderBy(item => item.Key)
                .ToArray();
        }

        Exception? firstFailure = null;
        foreach ((int slotIndex, ulong address) in breakpointsToRestore)
        {
            try
            {
                await DisableSoftwareBreakpointBackendSlotAsync(slotIndex, address, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        if (firstFailure is not null)
        {
            throw new InvalidOperationException(
                "One or more PS5 software-breakpoint slots could not be restored before debugger detach.",
                firstFailure);
        }
    }

    private async Task RestoreHardwareWatchpointsBeforeDetachAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<int, DebuggerBreakpointRequest>[] watchpointsToDisable;
        lock (_stateGate)
        {
            Dictionary<int, DebuggerBreakpointRequest> bySlot = new(_pendingWatchpointDisables);
            foreach (HardwareWatchpointEntry entry in _watchpoints.Values)
            {
                if (entry.BackendEnabled)
                {
                    bySlot[entry.SlotIndex] = entry.Breakpoint.Request;
                }
            }

            watchpointsToDisable = bySlot
                .OrderBy(item => item.Key)
                .ToArray();
        }

        Exception? firstFailure = null;
        foreach ((int slotIndex, DebuggerBreakpointRequest request) in watchpointsToDisable)
        {
            try
            {
                await DisableHardwareWatchpointBackendSlotAsync(slotIndex, request, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
        }

        if (firstFailure is not null)
        {
            throw new InvalidOperationException(
                "One or more PS5 hardware-watchpoint slots could not be cleared before debugger detach.",
                firstFailure);
        }
    }

    private async Task FlushPendingBreakpointDisablesAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<int, ulong>[] pending;
        lock (_stateGate)
        {
            pending = _pendingBreakpointDisables.OrderBy(item => item.Key).ToArray();
        }

        foreach ((int slotIndex, ulong address) in pending)
        {
            await DisableSoftwareBreakpointBackendSlotAsync(slotIndex, address, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DisableSoftwareBreakpointBackendSlotAsync(
        int slotIndex,
        ulong address,
        CancellationToken cancellationToken)
    {
        await _commandClient
            .SetSoftwareBreakpointAsync(slotIndex, isEnabled: false, address, cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            _pendingBreakpointDisables.Remove(slotIndex);
            SoftwareBreakpointEntry? active = _breakpoints.Values
                .FirstOrDefault(entry => entry.SlotIndex == slotIndex);
            if (active is not null)
            {
                active.BackendEnabled = false;
                if (active.Breakpoint.IsEnabled)
                {
                    active.Breakpoint = new DebuggerBreakpoint(
                        active.Breakpoint.Id,
                        active.Breakpoint.Request,
                        isEnabled: false);
                }
            }
        }
    }

    private async Task FlushPendingWatchpointDisablesAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<int, DebuggerBreakpointRequest>[] pending;
        lock (_stateGate)
        {
            pending = _pendingWatchpointDisables.OrderBy(item => item.Key).ToArray();
        }

        foreach ((int slotIndex, DebuggerBreakpointRequest request) in pending)
        {
            await DisableHardwareWatchpointBackendSlotAsync(slotIndex, request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DisableHardwareWatchpointBackendSlotAsync(
        int slotIndex,
        DebuggerBreakpointRequest request,
        CancellationToken cancellationToken)
    {
        await SetHardwareWatchpointBackendStateAsync(
                slotIndex,
                isEnabled: false,
                request,
                cancellationToken)
            .ConfigureAwait(false);

        lock (_stateGate)
        {
            _pendingWatchpointDisables.Remove(slotIndex);
            HardwareWatchpointEntry? active = _watchpoints.Values
                .FirstOrDefault(entry => entry.SlotIndex == slotIndex);
            if (active is not null)
            {
                active.BackendEnabled = false;
                if (active.Breakpoint.IsEnabled)
                {
                    active.Breakpoint = new DebuggerBreakpoint(
                        active.Breakpoint.Id,
                        active.Breakpoint.Request,
                        isEnabled: false);
                }
            }
        }
    }

    private int FindFreeBreakpointSlotNoLock()
    {
        for (int slotIndex = 0; slotIndex < Ps5DebugProtocol.MaximumSoftwareBreakpointCount; slotIndex++)
        {
            if (_pendingBreakpointDisables.ContainsKey(slotIndex))
            {
                continue;
            }

            if (_breakpoints.Values.All(entry => entry.SlotIndex != slotIndex))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private int FindFreeWatchpointSlotNoLock()
    {
        for (int slotIndex = 0; slotIndex < Ps5DebugProtocol.MaximumHardwareWatchpointCount; slotIndex++)
        {
            if (_pendingWatchpointDisables.ContainsKey(slotIndex))
            {
                continue;
            }

            if (_watchpoints.Values.All(entry => entry.SlotIndex != slotIndex))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private static void ValidateSoftwareExecuteBreakpoint(DebuggerBreakpointRequest request)
    {
        if (request.Kind != DebuggerBreakpointKind.Software ||
            request.Access != DebuggerBreakpointAccess.Execute ||
            request.Size != 1)
        {
            throw new NotSupportedException(
                "The current PS5 debugger revision supports one-byte software execute breakpoints only.");
        }
    }

    private void ValidateSoftwareExecuteBreakpointAddress(DebuggerBreakpointRequest request)
    {
        IReadOnlyList<MemoryRegion>? regions = _getCachedMemoryRegions(Process);
        if (regions is null)
        {
            throw new InvalidOperationException(
                "The current PS5 memory map is unavailable. Refresh or reselect the Active Target before adding a software breakpoint.");
        }

        MemoryRegion? region = regions.FirstOrDefault(candidate =>
            candidate.Size > 0 &&
            request.Address >= candidate.BaseAddress &&
            request.Address < candidate.EndAddressExclusive);

        if (region is null)
        {
            throw new InvalidOperationException(
                $"Software execute breakpoint address 0x{request.Address:X} is outside the current PS5 target memory map.");
        }

        if (!region.Protection.HasFlag(MemoryProtection.Execute) ||
            region.Protection.HasFlag(MemoryProtection.Guard))
        {
            throw new InvalidOperationException(
                $"Software execute breakpoint address 0x{request.Address:X} is not inside an executable non-guarded PS5 memory region.");
        }
    }

    private static void ValidateHardwareWatchpoint(DebuggerBreakpointRequest request)
    {
        if (request.Kind != DebuggerBreakpointKind.Hardware)
        {
            throw new NotSupportedException("The PS5 watchpoint path requires a hardware breakpoint request.");
        }

        if (request.Access == DebuggerBreakpointAccess.Read)
        {
            throw new NotSupportedException(
                "ps5debug-NG/x86 debug registers do not provide a distinct read-only data-watchpoint mode. Use ReadWrite when reads must be observed.");
        }

        if (request.Access is not (DebuggerBreakpointAccess.Write or DebuggerBreakpointAccess.ReadWrite))
        {
            throw new NotSupportedException("PS5 hardware watchpoints support Write or ReadWrite data access only.");
        }

        if (request.Size is not (1 or 2 or 4 or 8))
        {
            throw new NotSupportedException("PS5 hardware watchpoints support sizes of 1, 2, 4, or 8 bytes.");
        }

        if (request.Address % (ulong)request.Size != 0)
        {
            throw new InvalidOperationException(
                $"Hardware watchpoint address 0x{request.Address:X} must be aligned to its {request.Size}-byte size.");
        }
    }

    private void ValidateHardwareWatchpointAddress(DebuggerBreakpointRequest request)
    {
        IReadOnlyList<MemoryRegion>? regions = _getCachedMemoryRegions(Process);
        if (regions is null)
        {
            throw new InvalidOperationException(
                "The current PS5 memory map is unavailable. Refresh or reselect the Active Target before adding a hardware watchpoint.");
        }

        ulong requestEnd;
        try
        {
            requestEnd = checked(request.Address + (ulong)request.Size);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Hardware watchpoint range overflows the address space.", exception);
        }

        MemoryRegion? region = regions.FirstOrDefault(candidate =>
            candidate.Size > 0 &&
            request.Address >= candidate.BaseAddress &&
            requestEnd <= candidate.EndAddressExclusive);

        if (region is null)
        {
            throw new InvalidOperationException(
                $"Hardware watchpoint range 0x{request.Address:X}-0x{requestEnd - 1:X} is outside a single mapped PS5 memory region.");
        }

        if (region.Protection.HasFlag(MemoryProtection.Guard))
        {
            throw new InvalidOperationException("Hardware watchpoints cannot target guarded PS5 memory regions.");
        }
    }

    private static bool IsEquivalentWatchpoint(
        DebuggerBreakpointRequest existing,
        DebuggerBreakpointRequest request)
    {
        return existing.Kind == DebuggerBreakpointKind.Hardware &&
               existing.Address == request.Address &&
               existing.Size == request.Size &&
               existing.Access == request.Access;
    }

    private static string FormatWatchpointAccess(DebuggerBreakpointAccess access)
    {
        return access switch
        {
            DebuggerBreakpointAccess.Write => "Write",
            DebuggerBreakpointAccess.ReadWrite => "Read/Write",
            DebuggerBreakpointAccess.Read => "Read",
            _ => access.ToString()
        };
    }

    private string? ResolveModuleName(ulong address)
    {
        IReadOnlyList<MemoryRegion>? regions = _getCachedMemoryRegions(Process);
        MemoryRegion? region = regions?.FirstOrDefault(candidate =>
            candidate.Size > 0 &&
            address >= candidate.BaseAddress &&
            address < candidate.EndAddressExclusive);
        return !string.IsNullOrWhiteSpace(region?.ModuleName)
            ? region.ModuleName
            : !string.IsNullOrWhiteSpace(region?.Name)
                ? region.Name
                : null;
    }

    private void ClearStepState()
    {
        lock (_stateGate)
        {
            _stepInProgress = false;
            _stepThreadId = null;
        }
    }

    private SoftwareBreakpointStopSnapshot? GetSoftwareBreakpointStopSnapshot(uint? threadId)
    {
        lock (_stateGate)
        {
            if (_softwareBreakpointStopSnapshot is not SoftwareBreakpointStopSnapshot snapshot)
            {
                return null;
            }

            return !threadId.HasValue || snapshot.ThreadId == threadId.Value
                ? snapshot
                : null;
        }
    }

    private void ClearSoftwareBreakpointStopSnapshot()
    {
        lock (_stateGate)
        {
            _softwareBreakpointStopSnapshot = null;
        }
    }

    private async Task CompleteTransparentBreakpointStepAsync(
        SoftwareBreakpointStopSnapshot stopSnapshot,
        CancellationToken cancellationToken)
    {
        byte[] liveRegisters = await _commandClient
            .GetGeneralRegistersAsync(stopSnapshot.ThreadId, cancellationToken)
            .ConfigureAwait(false);
        ulong liveInstructionPointer = BinaryPrimitives.ReadUInt64LittleEndian(
            liveRegisters.AsSpan(Ps5DebugProtocol.DebuggerRegisterInstructionPointerOffset, sizeof(ulong)));

        lock (_stateGate)
        {
            if (_state != DebuggerExecutionState.Paused ||
                _softwareBreakpointStopSnapshot is not SoftwareBreakpointStopSnapshot currentSnapshot ||
                currentSnapshot.ThreadId != stopSnapshot.ThreadId ||
                currentSnapshot.BreakpointAddress != stopSnapshot.BreakpointAddress)
            {
                throw new InvalidOperationException(
                    "The PS5 debugger stop context changed before the software-breakpoint step could complete.");
            }

            _softwareBreakpointStopSnapshot = null;
            _stepInProgress = false;
            _stepThreadId = null;
            _state = DebuggerExecutionState.Running;
        }

        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running,
            DebuggerStopReason.None,
            stopSnapshot.ThreadId,
            message: $"PS5 target resumed for Step Into from software breakpoint 0x{stopSnapshot.BreakpointAddress:X}."));

        SetState(DebuggerExecutionState.Paused);
        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.StepCompleted,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.StepCompleted,
            stopSnapshot.ThreadId,
            liveInstructionPointer,
            $"PS5 Step Into completed at 0x{liveInstructionPointer:X} using the backend's already-completed software-breakpoint step."));
    }

    private void EnsureAttached()
    {
        if (State == DebuggerExecutionState.Detached)
        {
            throw new InvalidOperationException("The PS5 debugger session is detached.");
        }
    }

    private DebuggerThreadState GetThreadStateNoLock(ulong threadId)
    {
        if (_suspendedThreadIds.Contains(threadId))
        {
            return DebuggerThreadState.Suspended;
        }

        return _state switch
        {
            DebuggerExecutionState.Running => DebuggerThreadState.Running,
            DebuggerExecutionState.Paused => DebuggerThreadState.Stopped,
            _ => DebuggerThreadState.Unknown
        };
    }

    private static uint ValidateThreadId(ulong threadId)
    {
        if (threadId is 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threadId),
                $"Thread id 0x{threadId:X} is outside the ps5debug-NG 32-bit LWP id range.");
        }

        return checked((uint)threadId);
    }

    private void EnsureState(DebuggerExecutionState expectedState, string operation)
    {
        DebuggerExecutionState state = State;
        if (state != expectedState)
        {
            throw new InvalidOperationException(
                $"The PS5 debugger can only {operation} while the target is {expectedState.ToString().ToLowerInvariant()}.");
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void SetState(DebuggerExecutionState state)
    {
        lock (_stateGate)
        {
            _state = state;
        }
    }

    private void StopEventChannel()
    {
        if (!_eventCancellation.IsCancellationRequested)
        {
            _eventCancellation.Cancel();
        }

        try
        {
            _eventClient.Client.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }
    }

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0)
        {
            bytes = bytes[..terminator];
        }

        return bytes.IsEmpty ? string.Empty : Encoding.UTF8.GetString(bytes).Trim();
    }

    private sealed class SoftwareBreakpointEntry
    {
        public SoftwareBreakpointEntry(int slotIndex, DebuggerBreakpoint breakpoint, bool backendEnabled)
        {
            SlotIndex = slotIndex;
            Breakpoint = breakpoint;
            BackendEnabled = backendEnabled;
        }

        public int SlotIndex { get; }

        public DebuggerBreakpoint Breakpoint { get; set; }

        public bool BackendEnabled { get; set; }
    }

    private sealed class HardwareWatchpointEntry
    {
        public HardwareWatchpointEntry(int slotIndex, DebuggerBreakpoint breakpoint, bool backendEnabled)
        {
            SlotIndex = slotIndex;
            Breakpoint = breakpoint;
            BackendEnabled = backendEnabled;
        }

        public int SlotIndex { get; }

        public DebuggerBreakpoint Breakpoint { get; set; }

        public bool BackendEnabled { get; set; }
    }

    private sealed record SoftwareBreakpointStopSnapshot(
        uint ThreadId,
        ulong BreakpointAddress,
        byte[] GeneralRegisters,
        byte[] FloatingPointRegisters);

    private void RaiseEvent(DebuggerEvent debugEvent)
    {
        EventReceived?.Invoke(this, new DebuggerEventEventArgs(debugEvent));
    }
}
