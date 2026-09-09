using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

internal sealed class MockDebuggerProvider : IDebuggerProvider, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly TargetProcess _process;
    private MockDebuggerSession? _activeSession;
    private bool _disposed;

    public MockDebuggerProvider(TargetProcess process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public Task<IDebuggerSession> AttachAsync(
        TargetProcess process,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsTargetProcess(process))
            {
                throw new ArgumentException(
                    "The selected process does not belong to this mock target.",
                    nameof(process));
            }

            if (_activeSession is not null)
            {
                throw new InvalidOperationException(
                    "The mock target already has an attached debugger session.");
            }

            MockDebuggerSession session = new(_process, ReleaseSession);
            _activeSession = session;
            return Task.FromResult<IDebuggerSession>(session);
        }
    }

    public async ValueTask DisposeAsync()
    {
        MockDebuggerSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _activeSession;
            _activeSession = null;
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private bool IsTargetProcess(TargetProcess process)
    {
        return process.Id == _process.Id &&
               string.Equals(process.Name, _process.Name, StringComparison.Ordinal);
    }

    private void ReleaseSession(MockDebuggerSession session)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
            }
        }
    }
}
