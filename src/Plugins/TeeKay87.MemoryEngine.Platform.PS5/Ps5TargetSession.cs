using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5TargetSession :
    ITargetSession,
    IProcessProvider,
    IForegroundProcessProvider,
    IMemoryMapProvider,
    IMemoryReader,
    IMemoryWriter,
    INativeValueScanner,
    INativeValueScanRefiner,
    IProcessControl
{
    private readonly Ps5DebugClient _client;
    private IReadOnlyList<TargetProcess> _lastProcesses = Array.Empty<TargetProcess>();
    private bool _disposed;

    public Ps5TargetSession(PluginMetadata plugin, Ps5DebugClient client)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public PluginMetadata Plugin { get; }

    public TargetArchitecture Architecture => Plugin.Architecture;

    public bool IsConnected => !_disposed && _client.IsConnected;

    public TService? GetService<TService>() where TService : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((typeof(TService) == typeof(INativeValueScanner) ||
             typeof(TService) == typeof(INativeValueScanRefiner)) &&
            !_client.SupportsTurboValueScan)
        {
            return null;
        }

        return this as TService;
    }

    public async Task<IReadOnlyList<TargetProcess>> GetProcessesAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IReadOnlyList<Ps5DebugProcessInfo> processes = await _client
            .GetProcessesAsync(cancellationToken)
            .ConfigureAwait(false);

        _lastProcesses = processes
            .Select(process => new TargetProcess(
                checked((ulong)process.ProcessId),
                process.Name))
            .ToArray();

        return _lastProcesses;
    }


    public async Task<TargetProcess?> GetForegroundProcessAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<TargetProcess> processes = _lastProcesses.Count > 0
            ? _lastProcesses
            : await GetProcessesAsync(cancellationToken).ConfigureAwait(false);

        return processes.FirstOrDefault(process =>
            string.Equals(process.Name, "eboot.bin", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<MemoryRegion>> GetMemoryRegionsAsync(
        TargetProcess process,
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

        IReadOnlyList<Ps5DebugMemoryRegionInfo> regions = await _client
            .GetMemoryRegionsAsync(checked((int)process.Id), cancellationToken)
            .ConfigureAwait(false);

        return regions
            .Select(region => new MemoryRegion(
                region.Start,
                checked(region.End - region.Start),
                ConvertProtection(region.Protection),
                region.Name))
            .ToArray();
    }


    public async Task<int> ReadAsync(
        TargetProcess process,
        ulong address,
        Memory<byte> destination,
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

        return await _client
            .ReadMemoryAsync(
                checked((int)process.Id),
                address,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
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

        await _client
            .WriteMemoryAsync(
                checked((int)process.Id),
                address,
                source,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ulong>> ScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(request);

        if (process.Id > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG signed 32-bit PID range.");
        }

        return await _client
            .ScanValuesAsync(
                checked((int)process.Id),
                memoryRegions,
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ulong>> RefineAsync(
        TargetProcess process,
        IReadOnlyList<ulong> previousAddresses,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(previousAddresses);
        ArgumentNullException.ThrowIfNull(request);

        if (process.Id > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG signed 32-bit PID range.");
        }

        return await _client
            .RefineValuesAsync(
                checked((int)process.Id),
                previousAddresses,
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client.ResetNativeScanAsync(cancellationToken);
    }

    public async Task SuspendAsync(TargetProcess process, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        if (process.Id is 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG process-control PID range.");
        }

        await _client
            .SuspendProcessAsync(checked((int)process.Id), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ResumeAsync(TargetProcess process, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        if (process.Id is 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG process-control PID range.");
        }

        await _client
            .ResumeProcessAsync(checked((int)process.Id), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private static MemoryProtection ConvertProtection(ushort protection)
    {
        MemoryProtection result = MemoryProtection.None;

        if ((protection & Ps5DebugProtocol.VmProtectionRead) != 0)
        {
            result |= MemoryProtection.Read;
        }

        if ((protection & Ps5DebugProtocol.VmProtectionWrite) != 0)
        {
            result |= MemoryProtection.Write;
        }

        if ((protection & Ps5DebugProtocol.VmProtectionExecute) != 0)
        {
            result |= MemoryProtection.Execute;
        }

        return result;
    }
}
