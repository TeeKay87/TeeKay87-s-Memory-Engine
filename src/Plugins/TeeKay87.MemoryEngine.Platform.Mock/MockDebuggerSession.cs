using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

internal sealed class MockDebuggerSession :
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
    private const ulong MainThreadId = 1;
    private const ulong WorkerThreadId = 2;
    private const ulong RenderThreadId = 3;
    private const int MaximumSoftwareBreakpointCount = 30;
    private const int MaximumHardwareWatchpointCount = 4;

    private static readonly IReadOnlyDictionary<ulong, string> ThreadNames =
        new Dictionary<ulong, string>
        {
            [MainThreadId] = "Main",
            [WorkerThreadId] = "Worker",
            [RenderThreadId] = "Render"
        };

    private readonly object _gate = new();
    private readonly Action<MockDebuggerSession> _onDisposed;
    private readonly Dictionary<ulong, Dictionary<string, ulong>> _registerValues = CreateRegisterValues();
    private readonly HashSet<ulong> _suspendedThreadIds = new();
    private readonly Dictionary<string, DebuggerBreakpoint> _breakpoints = new(StringComparer.Ordinal);
    private int _nextBreakpointId = 1;
    private DebuggerExecutionState _state = DebuggerExecutionState.Running;
    private bool _disposed;

    public MockDebuggerSession(
        TargetProcess process,
        Action<MockDebuggerSession> onDisposed)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        _onDisposed = onDisposed ?? throw new ArgumentNullException(nameof(onDisposed));
    }

    public TargetProcess Process { get; }

    public DebuggerExecutionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<DebuggerEventEventArgs>? EventReceived;

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            if (_state != DebuggerExecutionState.Running)
            {
                throw new InvalidOperationException("The mock debugger can only pause while the target is running.");
            }

            _state = DebuggerExecutionState.Paused;
        }

        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Paused,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.PauseRequested,
            MainThreadId,
            MockTargetLayout.CodeAddress,
            "Mock target paused."));
        return Task.CompletedTask;
    }

    public Task ContinueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            if (_state != DebuggerExecutionState.Paused)
            {
                throw new InvalidOperationException("The mock debugger can only continue while the target is paused.");
            }

            _state = DebuggerExecutionState.Running;
        }

        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running,
            DebuggerStopReason.None,
            MainThreadId,
            MockTargetLayout.CodeAddress,
            "Mock target resumed."));

        QueueDeterministicBreakpointHit();
        return Task.CompletedTask;
    }

    public Task DetachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            _state = DebuggerExecutionState.Detached;
            _suspendedThreadIds.Clear();
            _breakpoints.Clear();
        }

        return Task.CompletedTask;
    }

    public TService? GetService<TService>() where TService : class
    {
        EnsureNotDisposed();
        return this as TService;
    }

    public Task<IReadOnlyList<DebuggerThreadInfo>> GetThreadsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            IReadOnlyList<DebuggerThreadInfo> threads = ThreadNames
                .Select(thread => new DebuggerThreadInfo(
                    thread.Key,
                    thread.Value,
                    GetThreadState(thread.Key)))
                .ToArray();
            return Task.FromResult(threads);
        }
    }

    public Task SuspendThreadAsync(ulong threadId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            EnsureRunningForThreadControl();
            ValidateThreadId(threadId);
            if (!_suspendedThreadIds.Add(threadId))
            {
                throw new InvalidOperationException($"Mock thread 0x{threadId:X} is already suspended.");
            }
        }

        return Task.CompletedTask;
    }

    public Task ResumeThreadAsync(ulong threadId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            EnsureRunningForThreadControl();
            ValidateThreadId(threadId);
            if (!_suspendedThreadIds.Remove(threadId))
            {
                throw new InvalidOperationException($"Mock thread 0x{threadId:X} is not suspended.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DebuggerRegister>> GetRegistersAsync(
        ulong threadId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            EnsurePausedForRegisterAccess();
            ValidateThreadId(threadId);
            return Task.FromResult(CreateRegisterSnapshot(_registerValues[threadId]));
        }
    }

    public Task WriteRegisterAsync(
        ulong threadId,
        DebuggerRegisterWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            EnsurePausedForRegisterAccess();
            ValidateThreadId(threadId);

            Dictionary<string, ulong> values = _registerValues[threadId];
            if (!values.ContainsKey(request.RegisterId))
            {
                throw new ArgumentOutOfRangeException(nameof(request), $"Unknown mock register '{request.RegisterId}'.");
            }

            if (request.Value.Length != sizeof(ulong))
            {
                throw new ArgumentException(
                    "Mock general-purpose register writes must contain exactly 8 bytes.",
                    nameof(request));
            }

            values[request.RegisterId] = BinaryPrimitives.ReadUInt64LittleEndian(request.Value.Span);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DebuggerStackFrame>> GetCallStackAsync(
        ulong threadId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            EnsurePausedForRegisterAccess();
            ValidateThreadId(threadId);

            Dictionary<string, ulong> values = _registerValues[threadId];
            ulong instructionPointer = values["rip"];
            ulong stackPointer = values["rsp"];
            ulong framePointer = values["rbp"];
            string threadName = ThreadNames[threadId];

            IReadOnlyList<DebuggerStackFrame> frames = new[]
            {
                new DebuggerStackFrame(
                    0,
                    instructionPointer,
                    stackPointer,
                    framePointer,
                    MockTargetLayout.CodeAddress + 0x4,
                    Process.Name,
                    $"{threadName}.Current"),
                new DebuggerStackFrame(
                    1,
                    MockTargetLayout.CodeAddress + 0x4,
                    stackPointer + 0x40,
                    framePointer + 0x40,
                    MockTargetLayout.CodeAddress + 0x9,
                    Process.Name,
                    $"{threadName}.Caller"),
                new DebuggerStackFrame(
                    2,
                    MockTargetLayout.CodeAddress + 0x9,
                    stackPointer + 0x80,
                    framePointer + 0x80,
                    null,
                    Process.Name,
                    "MockEntry")
            };

            return Task.FromResult(frames);
        }
    }

    public Task StepAsync(
        DebuggerStepKind stepKind,
        ulong? threadId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        if (stepKind != DebuggerStepKind.Into)
        {
            throw new NotSupportedException(
                "The mock debugger exposes native Step Into. Step Over and Step Out are composed by the host from disassembly, call frames, temporary breakpoints, and Continue.");
        }

        ulong selectedThreadId = threadId ?? MainThreadId;
        ulong currentInstructionPointer;
        ulong nextInstructionPointer;
        lock (_gate)
        {
            EnsurePausedForRegisterAccess();
            ValidateThreadId(selectedThreadId);
            currentInstructionPointer = _registerValues[selectedThreadId]["rip"];
            nextInstructionPointer = ResolveStepIntoAddress(currentInstructionPointer);
            _state = DebuggerExecutionState.Running;
        }

        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running,
            DebuggerStopReason.None,
            selectedThreadId,
            currentInstructionPointer,
            "Mock target resumed for Step Into."));

        _ = Task.Run(async () =>
        {
            await Task.Delay(25).ConfigureAwait(false);

            lock (_gate)
            {
                if (_disposed || _state != DebuggerExecutionState.Running)
                {
                    return;
                }

                _registerValues[selectedThreadId]["rip"] = nextInstructionPointer;
                _state = DebuggerExecutionState.Paused;
            }

            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.StepCompleted,
                DebuggerExecutionState.Paused,
                DebuggerStopReason.StepCompleted,
                selectedThreadId,
                nextInstructionPointer,
                "Mock Step Into completed."));
        });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DebuggerBreakpoint>> GetBreakpointsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            IReadOnlyList<DebuggerBreakpoint> snapshot = _breakpoints.Values
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
            ValidateBreakpointRequestOrThrow(request);

            lock (_gate)
            {
                if (_state == DebuggerExecutionState.Detached)
                {
                    return DebuggerBreakpointValidationResult.Invalid(
                        "The mock debugger session is detached.");
                }

                int matchingKindCount = _breakpoints.Values.Count(existing => existing.Request.Kind == request.Kind);
                int maximumCount = request.Kind == DebuggerBreakpointKind.Software
                    ? MaximumSoftwareBreakpointCount
                    : MaximumHardwareWatchpointCount;
                if (matchingKindCount >= maximumCount)
                {
                    string resource = request.Kind == DebuggerBreakpointKind.Software
                        ? "software-breakpoint"
                        : "hardware-watchpoint";
                    return DebuggerBreakpointValidationResult.Invalid(
                        $"The mock debugger has no free {resource} slots ({maximumCount} maximum).");
                }

                if (_breakpoints.Values.Any(existing => IsDuplicateRequest(existing.Request, request)))
                {
                    return DebuggerBreakpointValidationResult.Invalid(
                        $"An equivalent {GetRequestDescription(request)} already exists at 0x{request.Address:X}.");
                }
            }

            return DebuggerBreakpointValidationResult.Valid();
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
        ValidateBreakpointRequestOrThrow(request);

        lock (_gate)
        {
            int matchingKindCount = _breakpoints.Values.Count(existing => existing.Request.Kind == request.Kind);
            int maximumCount = request.Kind == DebuggerBreakpointKind.Software
                ? MaximumSoftwareBreakpointCount
                : MaximumHardwareWatchpointCount;
            if (matchingKindCount >= maximumCount)
            {
                string resource = request.Kind == DebuggerBreakpointKind.Software
                    ? "software-breakpoint"
                    : "hardware-watchpoint";
                throw new InvalidOperationException($"The mock debugger has no free {resource} slots ({maximumCount} maximum).");
            }

            if (_breakpoints.Values.Any(existing => IsDuplicateRequest(existing.Request, request)))
            {
                throw new InvalidOperationException(
                    $"An equivalent {GetRequestDescription(request)} already exists at 0x{request.Address:X}.");
            }

            string prefix = request.Kind == DebuggerBreakpointKind.Software ? "mock-sw" : "mock-hw";
            string id = $"{prefix}-{_nextBreakpointId++:D2}";
            DebuggerBreakpoint breakpoint = new(id, request, isEnabled: true);
            _breakpoints.Add(id, breakpoint);
            return Task.FromResult(breakpoint);
        }
    }

    public Task RemoveBreakpointAsync(string breakpointId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakpointId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            if (!_breakpoints.Remove(breakpointId))
            {
                throw new KeyNotFoundException($"Unknown mock breakpoint '{breakpointId}'.");
            }
        }

        return Task.CompletedTask;
    }

    public Task SetBreakpointEnabledAsync(
        string breakpointId,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakpointId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            if (!_breakpoints.TryGetValue(breakpointId, out DebuggerBreakpoint? breakpoint))
            {
                throw new KeyNotFoundException($"Unknown mock breakpoint '{breakpointId}'.");
            }

            if (breakpoint.IsEnabled != isEnabled)
            {
                _breakpoints[breakpointId] = new DebuggerBreakpoint(
                    breakpoint.Id,
                    breakpoint.Request,
                    isEnabled);
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        bool notifyProvider;
        lock (_gate)
        {
            notifyProvider = !_disposed;
            _disposed = true;
            _state = DebuggerExecutionState.Detached;
            _suspendedThreadIds.Clear();
            _breakpoints.Clear();
        }

        if (notifyProvider)
        {
            _onDisposed(this);
        }

        return ValueTask.CompletedTask;
    }

    private void QueueDeterministicBreakpointHit()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(25).ConfigureAwait(false);

            DebuggerBreakpoint? hit = null;
            lock (_gate)
            {
                if (_disposed || _state != DebuggerExecutionState.Running)
                {
                    return;
                }

                hit = _breakpoints.Values
                    .Where(breakpoint => breakpoint.IsEnabled)
                    .OrderBy(breakpoint => breakpoint.Request.Address)
                    .ThenBy(breakpoint => breakpoint.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (hit is null)
                {
                    return;
                }

                _state = DebuggerExecutionState.Paused;
                ulong instructionPointer = hit.Request.Kind == DebuggerBreakpointKind.Software
                    ? hit.Request.Address
                    : MockTargetLayout.CodeAddress + 4;
                _registerValues[MainThreadId]["rip"] = instructionPointer;
                if (hit.Request.IsTemporary)
                {
                    _breakpoints.Remove(hit.Id);
                }
            }

            ulong eventInstructionPointer = hit.Request.Kind == DebuggerBreakpointKind.Software
                ? hit.Request.Address
                : MockTargetLayout.CodeAddress + 4;
            bool isWatchpoint = hit.Request.Kind == DebuggerBreakpointKind.Hardware;
            string message = isWatchpoint
                ? $"Mock {FormatAccess(hit.Request.Access)} watchpoint hit at 0x{hit.Request.Address:X} from instruction 0x{eventInstructionPointer:X}."
                : $"Mock software breakpoint hit at 0x{hit.Request.Address:X}.";
            RaiseEvent(new DebuggerEvent(
                isWatchpoint ? DebuggerEventKind.Watchpoint : DebuggerEventKind.Breakpoint,
                DebuggerExecutionState.Paused,
                isWatchpoint ? DebuggerStopReason.Watchpoint : DebuggerStopReason.Breakpoint,
                MainThreadId,
                eventInstructionPointer,
                hit,
                message));
        });
    }

    private static void ValidateBreakpointRequestOrThrow(DebuggerBreakpointRequest request)
    {
        if (request.Kind == DebuggerBreakpointKind.Software)
        {
            ValidateSoftwareExecuteBreakpoint(request);
            return;
        }

        if (request.Kind == DebuggerBreakpointKind.Hardware)
        {
            ValidateHardwareWatchpoint(request);
            return;
        }

        throw new NotSupportedException($"Unsupported mock breakpoint kind '{request.Kind}'.");
    }

    private static void ValidateSoftwareExecuteBreakpoint(DebuggerBreakpointRequest request)
    {
        if (request.Access != DebuggerBreakpointAccess.Execute || request.Size != 1)
        {
            throw new NotSupportedException(
                "Mock software breakpoints are one-byte execute breakpoints.");
        }

        ulong mappedEnd = checked(MockTargetLayout.BaseAddress + (ulong)MockTargetLayout.MemorySize);
        if (request.Address < MockTargetLayout.BaseAddress || request.Address >= mappedEnd)
        {
            throw new InvalidOperationException(
                $"Software execute breakpoint address 0x{request.Address:X} is outside the mock target memory map.");
        }

        ulong executableEnd = checked(MockTargetLayout.CodeAddress + (ulong)MockTargetLayout.CodeBytes.Length);
        if (request.Address < MockTargetLayout.CodeAddress || request.Address >= executableEnd)
        {
            throw new InvalidOperationException(
                $"Software execute breakpoint address 0x{request.Address:X} is not inside the mock target's executable code range.");
        }
    }

    private static void ValidateHardwareWatchpoint(DebuggerBreakpointRequest request)
    {
        if (request.Access is DebuggerBreakpointAccess.Execute)
        {
            throw new NotSupportedException("Mock hardware watchpoints require Read, Write, or ReadWrite access.");
        }

        if (request.Size is not (1 or 2 or 4 or 8))
        {
            throw new NotSupportedException("Mock hardware watchpoints support sizes of 1, 2, 4, or 8 bytes.");
        }

        if (request.Address % (ulong)request.Size != 0)
        {
            throw new InvalidOperationException(
                $"Hardware watchpoint address 0x{request.Address:X} must be aligned to its {request.Size}-byte size.");
        }

        ulong mappedEnd = checked(MockTargetLayout.BaseAddress + (ulong)MockTargetLayout.MemorySize);
        ulong requestEnd;
        try
        {
            requestEnd = checked(request.Address + (ulong)request.Size);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Hardware watchpoint range overflows the address space.", exception);
        }

        if (request.Address < MockTargetLayout.BaseAddress || requestEnd > mappedEnd)
        {
            throw new InvalidOperationException(
                $"Hardware watchpoint range 0x{request.Address:X}-0x{requestEnd - 1:X} is outside the mock target memory map.");
        }
    }

    private static bool IsDuplicateRequest(DebuggerBreakpointRequest existing, DebuggerBreakpointRequest request)
    {
        return existing.Kind == request.Kind &&
               existing.Address == request.Address &&
               existing.Size == request.Size &&
               existing.Access == request.Access;
    }

    private static string GetRequestDescription(DebuggerBreakpointRequest request)
    {
        return request.Kind == DebuggerBreakpointKind.Software
            ? "software execute breakpoint"
            : $"{FormatAccess(request.Access).ToLowerInvariant()} hardware watchpoint";
    }

    private static string FormatAccess(DebuggerBreakpointAccess access)
    {
        return access switch
        {
            DebuggerBreakpointAccess.Read => "Read",
            DebuggerBreakpointAccess.Write => "Write",
            DebuggerBreakpointAccess.ReadWrite => "Read/Write",
            _ => access.ToString()
        };
    }

    private DebuggerThreadState GetThreadState(ulong threadId)
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

    private void EnsureRunningForThreadControl()
    {
        if (_state != DebuggerExecutionState.Running)
        {
            throw new InvalidOperationException("Mock per-thread control is available while the debugger target is running.");
        }
    }

    private void EnsurePausedForRegisterAccess()
    {
        if (_state != DebuggerExecutionState.Paused)
        {
            throw new InvalidOperationException("Mock register access is available while the debugger target is paused.");
        }
    }

    private static void ValidateThreadId(ulong threadId)
    {
        if (!ThreadNames.ContainsKey(threadId))
        {
            throw new ArgumentOutOfRangeException(nameof(threadId), $"Unknown mock thread 0x{threadId:X}.");
        }
    }

    private static ulong ResolveStepIntoAddress(ulong instructionPointer)
    {
        ulong code = MockTargetLayout.CodeAddress;
        return instructionPointer switch
        {
            var address when address == code => code + 0x2,
            var address when address == code + 0x2 => code + 0x8,
            var address when address == code + 0x4 => code + 0x6,
            var address when address == code + 0x6 => code + 0x8,
            var address when address == code + 0x8 => code + 0x4,
            var address when address == code + 0x9 => code + 0xA,
            _ => code + 0xA
        };
    }

    private static Dictionary<ulong, Dictionary<string, ulong>> CreateRegisterValues()
    {
        return new Dictionary<ulong, Dictionary<string, ulong>>
        {
            [MainThreadId] = CreateThreadRegisterValues(
                MockTargetLayout.CodeAddress,
                MockTargetLayout.BaseAddress + 0xF000,
                MockTargetLayout.BaseAddress + 0xEF80,
                0x1000),
            [WorkerThreadId] = CreateThreadRegisterValues(
                MockTargetLayout.CodeAddress + 0x20,
                MockTargetLayout.BaseAddress + 0xE000,
                MockTargetLayout.BaseAddress + 0xDF80,
                0x2000),
            [RenderThreadId] = CreateThreadRegisterValues(
                MockTargetLayout.CodeAddress + 0x40,
                MockTargetLayout.BaseAddress + 0xD000,
                MockTargetLayout.BaseAddress + 0xCF80,
                0x3000)
        };
    }

    private static Dictionary<string, ulong> CreateThreadRegisterValues(
        ulong instructionPointer,
        ulong stackPointer,
        ulong framePointer,
        ulong seed)
    {
        return new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase)
        {
            ["rax"] = seed + 0x01,
            ["rbx"] = seed + 0x02,
            ["rcx"] = seed + 0x03,
            ["rdx"] = seed + 0x04,
            ["rsi"] = seed + 0x05,
            ["rdi"] = seed + 0x06,
            ["rbp"] = framePointer,
            ["rsp"] = stackPointer,
            ["r8"] = seed + 0x08,
            ["r9"] = seed + 0x09,
            ["r10"] = seed + 0x0A,
            ["r11"] = seed + 0x0B,
            ["r12"] = seed + 0x0C,
            ["r13"] = seed + 0x0D,
            ["r14"] = seed + 0x0E,
            ["r15"] = seed + 0x0F,
            ["rip"] = instructionPointer,
            ["rflags"] = 0x202
        };
    }

    private static IReadOnlyList<DebuggerRegister> CreateRegisterSnapshot(
        IReadOnlyDictionary<string, ulong> values)
    {
        List<DebuggerRegister> registers = new()
        {
            CreateRegister("rax", "RAX", values["rax"]),
            CreateRegister("rbx", "RBX", values["rbx"]),
            CreateRegister("rcx", "RCX", values["rcx"]),
            CreateRegister("rdx", "RDX", values["rdx"]),
            CreateRegister("rsi", "RSI", values["rsi"]),
            CreateRegister("rdi", "RDI", values["rdi"]),
            CreateRegister("rbp", "RBP", values["rbp"], DebuggerRegisterRole.FramePointer),
            CreateRegister("rsp", "RSP", values["rsp"], DebuggerRegisterRole.StackPointer),
            CreateRegister("r8", "R8", values["r8"]),
            CreateRegister("r9", "R9", values["r9"]),
            CreateRegister("r10", "R10", values["r10"]),
            CreateRegister("r11", "R11", values["r11"]),
            CreateRegister("r12", "R12", values["r12"]),
            CreateRegister("r13", "R13", values["r13"]),
            CreateRegister("r14", "R14", values["r14"]),
            CreateRegister("r15", "R15", values["r15"]),
            CreateRegister("rip", "RIP", values["rip"], DebuggerRegisterRole.InstructionPointer, "Control"),
            CreateRegister("rflags", "RFLAGS", values["rflags"], group: "Control")
        };

        ulong extendedSeed = values["rax"] ^ values["rsp"];
        registers.Add(CreateWideRegister("fp0", "FP0", 80, CreatePatternBytes(extendedSeed, 10), "Floating Point"));
        registers.Add(CreateWideRegister("vector0", "V0", 128, CreatePatternBytes(extendedSeed + 0x10, 16), "SIMD"));
        registers.Add(CreateWideRegister("vector1", "V1", 256, CreatePatternBytes(extendedSeed + 0x20, 32), "SIMD"));
        registers.Add(CreateWideRegister("debug0", "D0", 64, CreatePatternBytes(extendedSeed + 0x30, 8), "Debug"));
        return registers;
    }

    private static DebuggerRegister CreateRegister(
        string id,
        string displayName,
        ulong value,
        DebuggerRegisterRole role = DebuggerRegisterRole.None,
        string group = "General")
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return new DebuggerRegister(
            id,
            displayName,
            bitWidth: 64,
            bytes,
            group,
            role,
            canWrite: true,
            valueEncoding: DebuggerRegisterValueEncoding.UnsignedLittleEndian);
    }

    private static DebuggerRegister CreateWideRegister(
        string id,
        string displayName,
        int bitWidth,
        ReadOnlyMemory<byte> value,
        string group)
    {
        return new DebuggerRegister(
            id,
            displayName,
            bitWidth,
            value,
            group,
            DebuggerRegisterRole.None,
            canWrite: false,
            valueEncoding: DebuggerRegisterValueEncoding.UnsignedLittleEndian);
    }

    private static byte[] CreatePatternBytes(ulong seed, int byteCount)
    {
        byte[] value = new byte[byteCount];
        for (int index = 0; index < value.Length; index++)
        {
            value[index] = (byte)((seed + checked((ulong)(index * 0x11))) & 0xFF);
        }

        return value;
    }

    private void RaiseEvent(DebuggerEvent debugEvent)
    {
        EventReceived?.Invoke(this, new DebuggerEventEventArgs(debugEvent));
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
