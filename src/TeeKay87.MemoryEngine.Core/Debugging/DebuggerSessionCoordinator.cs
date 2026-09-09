using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Debugging;

public sealed class DebuggerSessionCoordinator : IAsyncDisposable
{
    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly IDebuggerProvider _provider;
    private readonly TargetProcess _process;
    private IDebuggerSession? _session;
    private DebuggerSessionState _state = DebuggerSessionState.Detached;
    private long _eventSequence;
    private bool _disposed;

    public DebuggerSessionCoordinator(
        DebuggerSessionIdentity identity,
        TargetProcess process,
        IDebuggerProvider provider)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        if (Identity.ProcessId != process.Id ||
            !string.Equals(Identity.ProcessName, process.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The debugger identity does not describe the supplied target process.",
                nameof(process));
        }
    }

    public DebuggerSessionIdentity Identity { get; }

    public DebuggerSessionState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public bool IsAttached => State is
        DebuggerSessionState.Attached or
        DebuggerSessionState.Running or
        DebuggerSessionState.Paused;

    public event EventHandler<DebuggerSessionStateChangedEventArgs>? StateChanged;

    public event EventHandler<DebuggerEventContextEventArgs>? EventReceived;

    public async Task AttachAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (State != DebuggerSessionState.Detached)
            {
                throw new InvalidOperationException("The debugger session can only attach from the Detached state.");
            }

            ChangeState(DebuggerSessionState.Attaching);

            IDebuggerSession? attachedSession = null;
            try
            {
                attachedSession = await _provider
                    .AttachAsync(_process, cancellationToken)
                    .ConfigureAwait(false);

                if (attachedSession is null)
                {
                    throw new InvalidOperationException("The debugger provider returned a null debugger session.");
                }

                ValidateAttachedProcess(attachedSession.Process);
                if (attachedSession.State == DebuggerExecutionState.Detached)
                {
                    throw new InvalidOperationException(
                        "The debugger provider returned a detached debugger session after attach.");
                }

                _session = attachedSession;
                _session.EventReceived += OnDebuggerEventReceived;
                ChangeState(MapExecutionState(_session.State));
            }
            catch (OperationCanceledException)
            {
                if (attachedSession is not null)
                {
                    attachedSession.EventReceived -= OnDebuggerEventReceived;
                    await DisposeFailedAttachAsync(attachedSession).ConfigureAwait(false);
                }

                _session = null;
                ChangeState(DebuggerSessionState.Detached);
                throw;
            }
            catch
            {
                if (attachedSession is not null)
                {
                    attachedSession.EventReceived -= OnDebuggerEventReceived;
                    await DisposeFailedAttachAsync(attachedSession).ConfigureAwait(false);
                }

                _session = null;
                ChangeState(DebuggerSessionState.Faulted);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        await ExecuteAttachedOperationAsync(
                static (session, token) => session.PauseAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ContinueAsync(CancellationToken cancellationToken)
    {
        await ExecuteAttachedOperationAsync(
                static (session, token) => session.ContinueAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public TService? GetService<TService>() where TService : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IDebuggerSession session = GetAttachedSession();
        return session.GetService<TService>();
    }

    public async Task DetachAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await DetachCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await DetachCoreAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            _operationGate.Dispose();
        }
    }

    private async Task ExecuteAttachedOperationAsync(
        Func<IDebuggerSession, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            IDebuggerSession session = GetAttachedSession();
            await operation(session, cancellationToken).ConfigureAwait(false);
            ChangeState(MapExecutionState(session.State));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task DetachCoreAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            IDebuggerSession? session = _session;
            if (session is null)
            {
                if (State != DebuggerSessionState.Detached)
                {
                    ChangeState(DebuggerSessionState.Detached);
                }

                return;
            }

            ChangeState(DebuggerSessionState.Detaching);
            _session = null;
            session.EventReceived -= OnDebuggerEventReceived;

            Exception? detachFailure = null;
            try
            {
                await session.DetachAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                detachFailure = exception;
            }

            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                ChangeState(DebuggerSessionState.Detached);
            }

            if (detachFailure is not null)
            {
                throw detachFailure;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private IDebuggerSession GetAttachedSession()
    {
        IDebuggerSession? session = _session;
        if (session is null || !IsAttached)
        {
            throw new InvalidOperationException("The debugger session is not attached.");
        }

        return session;
    }

    private void OnDebuggerEventReceived(object? sender, DebuggerEventEventArgs eventArgs)
    {
        IDebuggerSession? session = _session;
        if (session is null || (sender is not null && !ReferenceEquals(sender, session)))
        {
            return;
        }

        DebuggerEvent debugEvent = eventArgs.Event;
        ChangeState(MapExecutionState(debugEvent.ExecutionState));

        long sequence = Interlocked.Increment(ref _eventSequence);
        DebuggerEventContext context = new(Identity, sequence, debugEvent);
        EventReceived?.Invoke(this, new DebuggerEventContextEventArgs(context));
    }

    private void ValidateAttachedProcess(TargetProcess attachedProcess)
    {
        ArgumentNullException.ThrowIfNull(attachedProcess);

        if (attachedProcess.Id != _process.Id ||
            !string.Equals(attachedProcess.Name, _process.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The debugger provider attached to a different target process than requested.");
        }
    }

    private void ChangeState(DebuggerSessionState newState)
    {
        DebuggerSessionState previousState;
        lock (_stateGate)
        {
            previousState = _state;
            if (previousState == newState)
            {
                return;
            }

            _state = newState;
        }

        StateChanged?.Invoke(
            this,
            new DebuggerSessionStateChangedEventArgs(previousState, newState));
    }

    private static DebuggerSessionState MapExecutionState(DebuggerExecutionState state)
    {
        return state switch
        {
            DebuggerExecutionState.Running => DebuggerSessionState.Running,
            DebuggerExecutionState.Paused => DebuggerSessionState.Paused,
            DebuggerExecutionState.Detached => DebuggerSessionState.Detached,
            _ => DebuggerSessionState.Attached
        };
    }

    private static async Task DisposeFailedAttachAsync(IDebuggerSession session)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // The original attach failure remains authoritative.
        }
    }
}
