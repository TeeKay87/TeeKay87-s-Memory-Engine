using System;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5ConcurrentMemoryWriter : IConcurrentMemoryWriter, IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Ps5DebugClient? _client;
    private bool _disposed;

    public Ps5ConcurrentMemoryWriter(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _host = host.Trim();
        _port = port;
    }

    public async Task WriteAsync(
        TargetProcess process,
        ulong address,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        if (process.Id > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG signed 32-bit PID range.");
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_client is null || !_client.IsConnected)
            {
                if (_client is not null)
                {
                    await _client.DisposeAsync().ConfigureAwait(false);
                }

                _client = await Ps5DebugClient
                    .ConnectAsync(_host, _port, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _client
                .WriteMemoryAsync(
                    checked((int)process.Id),
                    address,
                    source,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _writeGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }
        }
        finally
        {
            _writeGate.Release();
            _writeGate.Dispose();
        }
    }
}
