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
    INativeValueScanStreamProvider,
    INativeValueScanStreamRefiner,
    INativeScanTypeMappingProvider,
    IProcessControl
{
    private static readonly IDisassemblerProvider DisassemblerProvider = new Ps5X64DisassemblerProvider();
    private static readonly IDisassemblyWatchpointResolver DisassemblyWatchpointResolver = new Ps5DisassemblyWatchpointResolver();

    private readonly object _memoryRegionCacheGate = new();
    private readonly Ps5DebugClient _client;
    private readonly Ps5ConcurrentMemoryWriter _concurrentMemoryWriter;
    private readonly Ps5DebuggerProvider _debuggerProvider;
    private IReadOnlyList<TargetProcess> _lastProcesses = Array.Empty<TargetProcess>();
    private TargetProcess? _memoryRegionCacheProcess;
    private IReadOnlyList<MemoryRegion>? _memoryRegionCache;
    private bool _disposed;

    public Ps5TargetSession(PluginMetadata plugin, Ps5DebugClient client, string host, int port)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _concurrentMemoryWriter = new Ps5ConcurrentMemoryWriter(host, port);
        _debuggerProvider = new Ps5DebuggerProvider(host, port, client.ConnectionInfo, GetCachedMemoryRegions);
    }

    public PluginMetadata Plugin { get; }

    public TargetArchitecture Architecture => Plugin.Architecture;

    public bool IsConnected => !_disposed && _client.IsConnected;

    public IReadOnlyList<NativeScanTypeMapping> NativeScanTypeMappings => Ps5NativeScanTypeMappings.All;

    public TService? GetService<TService>() where TService : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (typeof(TService) == typeof(IConcurrentMemoryWriter))
        {
            return _concurrentMemoryWriter as TService;
        }

        if (typeof(TService) == typeof(IDisassemblerProvider))
        {
            return DisassemblerProvider as TService;
        }

        if (typeof(TService) == typeof(IDisassemblyWatchpointResolver))
        {
            return DisassemblyWatchpointResolver as TService;
        }

        if (typeof(TService) == typeof(IDebuggerProvider))
        {
            return _debuggerProvider as TService;
        }

        if ((typeof(TService) == typeof(INativeValueScanner) ||
             typeof(TService) == typeof(INativeValueScanRefiner) ||
             typeof(TService) == typeof(INativeValueScanStreamProvider) ||
             typeof(TService) == typeof(INativeValueScanStreamRefiner)) &&
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

        IReadOnlyList<MemoryRegion> mappedRegions = regions
            .Select(region => new MemoryRegion(
                region.Start,
                checked(region.End - region.Start),
                ConvertProtection(region.Protection),
                region.Name))
            .ToArray();

        lock (_memoryRegionCacheGate)
        {
            _memoryRegionCacheProcess = process;
            _memoryRegionCache = mappedRegions;
        }

        return mappedRegions;
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

    public async Task<IReadOnlyList<NativeValueScanResult>> ScanAsync(
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

    public async Task<INativeValueScanResultStream> StartScanAsync(
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
            .StartScanValuesStreamAsync(
                checked((int)process.Id),
                memoryRegions,
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NativeValueScanResult>> RefineAsync(
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

    public async Task<INativeValueScanResultStream> StartRefineAsync(
        TargetProcess process,
        INativeValueScanCandidateSource previousResults,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(previousResults);
        ArgumentNullException.ThrowIfNull(request);

        if (process.Id > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(process),
                $"Process id {process.Id} is outside the ps5debug-NG signed 32-bit PID range.");
        }

        return await _client
            .StartRefineValuesStreamAsync(
                checked((int)process.Id),
                previousResults.Count,
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
        await _debuggerProvider.DisposeAsync().ConfigureAwait(false);
        await _concurrentMemoryWriter.DisposeAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
    }


    private IReadOnlyList<MemoryRegion>? GetCachedMemoryRegions(TargetProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (_memoryRegionCacheGate)
        {
            if (_memoryRegionCacheProcess is null ||
                _memoryRegionCacheProcess.Id != process.Id ||
                !string.Equals(_memoryRegionCacheProcess.Name, process.Name, StringComparison.Ordinal))
            {
                return null;
            }

            return _memoryRegionCache;
        }
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
