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
    IDebuggerRegisterService
{
    private const ulong MainThreadId = 1;
    private const ulong WorkerThreadId = 2;
    private const ulong RenderThreadId = 3;

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
        return Task.CompletedTask;
    }

    public Task DetachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDisposed();

        lock (_gate)
        {
            _state = DebuggerExecutionState.Detached;
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

    public ValueTask DisposeAsync()
    {
        bool notifyProvider;
        lock (_gate)
        {
            notifyProvider = !_disposed;
            _disposed = true;
            _state = DebuggerExecutionState.Detached;
            _suspendedThreadIds.Clear();
        }

        if (notifyProvider)
        {
            _onDisposed(this);
        }

        return ValueTask.CompletedTask;
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
        return new[]
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

    private void RaiseEvent(DebuggerEvent debugEvent)
    {
        EventReceived?.Invoke(this, new DebuggerEventEventArgs(debugEvent));
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
