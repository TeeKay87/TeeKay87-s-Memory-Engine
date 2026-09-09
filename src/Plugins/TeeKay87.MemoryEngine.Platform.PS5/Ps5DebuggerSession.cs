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

internal sealed class Ps5DebuggerSession : IDebuggerSession, IDebuggerThreadService, IDebuggerThreadControlService, IDebuggerRegisterService
{
    private static readonly TimeSpan EventChannelTimeout = TimeSpan.FromSeconds(5);

    private readonly object _stateGate = new();
    private readonly Ps5DebuggerCommandClient _commandClient;
    private readonly TcpClient _eventClient;
    private readonly NetworkStream _eventStream;
    private readonly CancellationTokenSource _eventCancellation = new();
    private readonly Action<Ps5DebuggerSession> _onDisposed;
    private readonly HashSet<ulong> _suspendedThreadIds = new();
    private readonly Task _eventLoopTask;
    private DebuggerExecutionState _state = DebuggerExecutionState.Running;
    private bool _detaching;
    private bool _disposed;

    private Ps5DebuggerSession(
        TargetProcess process,
        Ps5DebuggerCommandClient commandClient,
        TcpClient eventClient,
        Action<Ps5DebuggerSession> onDisposed)
    {
        Process = process;
        _commandClient = commandClient;
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
        Action<Ps5DebuggerSession> onDisposed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
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
            return new Ps5DebuggerSession(process, commandClient, eventClient, onDisposed);
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

        lock (_stateGate)
        {
            if (_state == DebuggerExecutionState.Detached)
            {
                return;
            }

            _detaching = true;
        }

        try
        {
            await _commandClient.DetachAsync(cancellationToken).ConfigureAwait(false);
            SetState(DebuggerExecutionState.Detached);
            lock (_stateGate)
            {
                _suspendedThreadIds.Clear();
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

        byte[] rawRegisters = await _commandClient
            .GetGeneralRegistersAsync(backendThreadId, cancellationToken)
            .ConfigureAwait(false);

        return Ps5GeneralRegisterMapper.Map(rawRegisters);
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
            "Register editing is disabled for the current ps5debug-NG backend. General-purpose register snapshots are read-only.");
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
        string threadName = ReadNullTerminatedUtf8(
            packet.Slice(
                Ps5DebugProtocol.DebuggerInterruptThreadNameOffset,
                Ps5DebugProtocol.DebuggerInterruptThreadNameLength));
        byte signal = checked((byte)((waitStatus >> 8) & 0xFF));

        SetState(DebuggerExecutionState.Paused);

        string threadText = string.IsNullOrWhiteSpace(threadName)
            ? $"thread 0x{threadId:X}"
            : $"{threadName} (0x{threadId:X})";
        string signalText = signal == 0 ? "backend stop" : $"signal {signal}";

        RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Other,
            DebuggerExecutionState.Paused,
            signal == 0 ? DebuggerStopReason.Backend : DebuggerStopReason.Signal,
            threadId,
            instructionPointer,
            $"PS5 debugger stop: {signalText} on {threadText}."));
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

    private void RaiseEvent(DebuggerEvent debugEvent)
    {
        EventReceived?.Invoke(this, new DebuggerEventEventArgs(debugEvent));
    }
}
