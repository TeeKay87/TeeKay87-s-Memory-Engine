using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5DebuggerProvider : IDebuggerProvider, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _host;
    private readonly int _port;
    private Ps5DebuggerSession? _activeSession;
    private bool _attachInProgress;
    private bool _disposed;

    public Ps5DebuggerProvider(string host, int port)
    {
        _host = string.IsNullOrWhiteSpace(host)
            ? throw new ArgumentException("A debugger host is required.", nameof(host))
            : host;
        _port = port is >= 1 and <= 65535
            ? port
            : throw new ArgumentOutOfRangeException(nameof(port));
    }

    public async Task<IDebuggerSession> AttachAsync(
        TargetProcess process,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_activeSession is not null || _attachInProgress)
            {
                throw new InvalidOperationException(
                    "The PS5 target already has an attached debugger session.");
            }

            _attachInProgress = true;
        }

        try
        {
            Ps5DebuggerSession session = await Ps5DebuggerSession
                .AttachAsync(_host, _port, process, ReleaseSession, cancellationToken)
                .ConfigureAwait(false);

            bool providerDisposed;
            lock (_gate)
            {
                providerDisposed = _disposed;
                if (!providerDisposed)
                {
                    _activeSession = session;
                }

                _attachInProgress = false;
            }

            if (providerDisposed)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw new ObjectDisposedException(nameof(Ps5DebuggerProvider));
            }

            return session;
        }
        catch
        {
            lock (_gate)
            {
                _attachInProgress = false;
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Ps5DebuggerSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _attachInProgress = false;
            session = _activeSession;
            _activeSession = null;
        }

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ReleaseSession(Ps5DebuggerSession session)
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
