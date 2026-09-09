using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5DebugClient : IAsyncDisposable
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private bool _disposed;
    private bool _scanAuthorizationEstablished;
    private uint _turboScanEngines;
    private bool _turboScanSessionActive;
    private int _turboScanProcessId;
    private ulong _turboScanResultCount;
    private string? _turboScanValueTypeId;
    private int _turboScanValueLength;
    private int _turboScanAlignment;
    private long _turboScanSessionGeneration;

    private Ps5DebugClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
    }

    public Ps5DebugConnectionInfo ConnectionInfo { get; private set; } =
        new(string.Empty, string.Empty, null, 0);

    public bool IsConnected => !_disposed && _tcpClient.Connected;

    public bool SupportsTurboValueScan { get; private set; }

    public static async Task<Ps5DebugClient> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        TcpClient tcpClient = new()
        {
            NoDelay = true
        };

        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ConnectionTimeout);

        try
        {
            await tcpClient.ConnectAsync(host, port, timeoutSource.Token).ConfigureAwait(false);

            Ps5DebugClient client = new(tcpClient);
            try
            {
                await client.IdentifyTargetAsync(timeoutSource.Token).ConfigureAwait(false);
                return client;
            }
            catch
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            tcpClient.Dispose();
            throw new TimeoutException(
                $"Timed out while connecting to ps5debug-NG at {host}:{port} after {ConnectionTimeout.TotalSeconds:0} seconds.");
        }
        catch (SocketException exception)
        {
            tcpClient.Dispose();
            throw new InvalidOperationException(
                $"Could not connect to ps5debug-NG at {host}:{port}. {exception.Message}",
                exception);
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }


    public async Task<IReadOnlyList<Ps5DebugProcessInfo>> GetProcessesAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await SendHeaderAsync(Ps5DebugProtocol.CommandProcessList, cancellationToken).ConfigureAwait(false);
        await ReadSuccessStatusAsync("process-list request", cancellationToken).ConfigureAwait(false);

        byte[] countBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(countBytes, cancellationToken).ConfigureAwait(false);
        uint processCount = BinaryPrimitives.ReadUInt32LittleEndian(countBytes);

        if (processCount > Ps5DebugProtocol.MaximumProcessCount)
        {
            throw new InvalidDataException(
                $"ps5debug-NG returned an invalid process count of {processCount}.");
        }

        int count = checked((int)processCount);
        int payloadLength = checked(count * Ps5DebugProtocol.ProcessListEntrySize);
        byte[] payload = new byte[payloadLength];
        await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        List<Ps5DebugProcessInfo> processes = new(count);

        for (int index = 0; index < count; index++)
        {
            int entryOffset = checked(index * Ps5DebugProtocol.ProcessListEntrySize);
            string name = ReadNullTerminatedUtf8(
                payload.AsSpan(entryOffset, Ps5DebugProtocol.ProcessListNameLength));
            int processId = BinaryPrimitives.ReadInt32LittleEndian(
                payload.AsSpan(
                    entryOffset + Ps5DebugProtocol.ProcessListNameLength,
                    sizeof(int)));

            if (processId < 0)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG returned an invalid negative process id ({processId}).");
            }

            processes.Add(new Ps5DebugProcessInfo(processId, name));
        }

        return processes;
    }

    public async Task<IReadOnlyList<Ps5DebugMemoryRegionInfo>> GetMemoryRegionsAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (processId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        byte[] request = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(request, processId);

        await SendCommandAsync(
                Ps5DebugProtocol.CommandProcessMaps,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        await ReadSuccessStatusAsync("memory-map request", cancellationToken).ConfigureAwait(false);

        byte[] countBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(countBytes, cancellationToken).ConfigureAwait(false);
        uint regionCount = BinaryPrimitives.ReadUInt32LittleEndian(countBytes);

        if (regionCount > Ps5DebugProtocol.MaximumMemoryRegionCount)
        {
            throw new InvalidDataException(
                $"ps5debug-NG returned an invalid memory-region count of {regionCount}.");
        }

        int count = checked((int)regionCount);
        int payloadLength = checked(count * Ps5DebugProtocol.ProcessMapEntrySize);
        byte[] payload = new byte[payloadLength];
        await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        List<Ps5DebugMemoryRegionInfo> regions = new(count);

        for (int index = 0; index < count; index++)
        {
            int entryOffset = checked(index * Ps5DebugProtocol.ProcessMapEntrySize);
            ReadOnlySpan<byte> entry = payload.AsSpan(entryOffset, Ps5DebugProtocol.ProcessMapEntrySize);

            string name = ReadNullTerminatedUtf8(entry[..Ps5DebugProtocol.ProcessMapNameLength]);
            ulong start = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(32, sizeof(ulong)));
            ulong end = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(40, sizeof(ulong)));
            ulong offset = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(48, sizeof(ulong)));
            ushort protection = BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(56, sizeof(ushort)));

            if (end < start)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG returned a memory region whose end address 0x{end:X} precedes its start address 0x{start:X}.");
            }

            regions.Add(new Ps5DebugMemoryRegionInfo(
                string.IsNullOrWhiteSpace(name) ? null : name,
                start,
                end,
                offset,
                protection));
        }

        return regions;
    }

    public async Task<int> ReadMemoryAsync(
        int processId,
        ulong address,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (processId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (destination.Length == 0)
        {
            return 0;
        }

        byte[] request = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(0, sizeof(int)), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(sizeof(int), sizeof(ulong)), address);
        BinaryPrimitives.WriteUInt32LittleEndian(
            request.AsSpan(sizeof(int) + sizeof(ulong), sizeof(uint)),
            checked((uint)destination.Length));

        // A ps5debug-NG command is one framed TCP transaction. Once the command has
        // started, always consume its complete response before honoring cancellation;
        // otherwise unread payload bytes would remain in the shared stream and corrupt
        // the next command. Scanner cancellation is observed before the next read request.
        cancellationToken.ThrowIfCancellationRequested();

        await SendCommandAsync(
                Ps5DebugProtocol.CommandProcessRead,
                request,
                CancellationToken.None)
            .ConfigureAwait(false);
        await ReadSuccessStatusAsync("memory-read request", CancellationToken.None).ConfigureAwait(false);
        await _stream.ReadExactlyAsync(destination, CancellationToken.None).ConfigureAwait(false);

        return destination.Length;
    }

    public async Task WriteMemoryAsync(
        int processId,
        ulong address,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (processId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (source.IsEmpty)
        {
            return;
        }

        byte[] request = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(0, sizeof(int)), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(sizeof(int), sizeof(ulong)), address);
        BinaryPrimitives.WriteUInt32LittleEndian(
            request.AsSpan(sizeof(int) + sizeof(ulong), sizeof(uint)),
            checked((uint)source.Length));

        // Keep the two-phase write transaction protocol-synchronized for the same
        // reason as reads: cancellation is checked before the command begins, then both
        // acknowledgements and the complete payload are exchanged atomically.
        cancellationToken.ThrowIfCancellationRequested();

        await SendCommandAsync(
                Ps5DebugProtocol.CommandProcessWrite,
                request,
                CancellationToken.None)
            .ConfigureAwait(false);

        await ReadSuccessStatusAsync("memory-write data acknowledgement", CancellationToken.None)
            .ConfigureAwait(false);

        await _stream.WriteAsync(source, CancellationToken.None).ConfigureAwait(false);
        await _stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);

        await ReadSuccessStatusAsync("memory-write completion", CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NativeValueScanResult>> ScanValuesAsync(
        int processId,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        await using INativeValueScanResultStream resultStream = await StartScanValuesStreamAsync(
                processId,
                memoryRegions,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        return await MaterializeNativeResultStreamAsync(resultStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<INativeValueScanResultStream> StartScanValuesStreamAsync(
        int processId,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(request);

        NativeScanProtocolShape protocolShape = ValidateNativeScanRequest(
            processId,
            request,
            MemoryScanStage.FirstScan);
        cancellationToken.ThrowIfCancellationRequested();

        if (_turboScanSessionActive)
        {
            await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
        }

        List<TurboScanSegment> segments = BuildTurboScanSegments(memoryRegions, request.ValueSize, request.Alignment);
        if (segments.Count == 0)
        {
            return new EmptyNativeValueScanResultStream();
        }

        await EnsureScanAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] commandBody = new byte[Ps5DebugProtocol.TurboScanStartBodySize];
        BinaryPrimitives.WriteInt32LittleEndian(commandBody.AsSpan(0, sizeof(int)), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(commandBody.AsSpan(4, sizeof(ulong)), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(commandBody.AsSpan(12, sizeof(uint)), 0);
        commandBody[16] = GetScanValueType(request.ValueTypeId);
        commandBody[17] = protocolShape.CompareType;
        commandBody[18] = checked((byte)request.Alignment);
        BinaryPrimitives.WriteUInt32LittleEndian(
            commandBody.AsSpan(19, sizeof(uint)),
            checked((uint)protocolShape.ComparisonDataLength));
        uint startFlags = protocolShape.UseSnapshot
            ? Ps5DebugProtocol.TurboScanFlagSnapshot |
              Ps5DebugProtocol.TurboScanFlagSnapshotIncludeZeros |
              Ps5DebugProtocol.TurboScanFlagSegments
            : Ps5DebugProtocol.TurboScanFlagServerResident |
              Ps5DebugProtocol.TurboScanFlagSegments;
        BinaryPrimitives.WriteUInt32LittleEndian(
            commandBody.AsSpan(23, sizeof(uint)),
            startFlags);

        bool residentSessionCreated = false;
        bool keepResidentSession = false;

        try
        {
            // START is a framed streaming transaction. Once sent, consume the complete
            // response before observing cancellation so the shared command stream remains
            // synchronized and the resident session can be closed safely if needed.
            await SendCommandAsync(
                    Ps5DebugProtocol.CommandTurboScanStart,
                    commandBody,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await ReadSuccessStatusAsync("TurboScan value acknowledgement", CancellationToken.None)
                .ConfigureAwait(false);

            await WriteScanComparisonDataAsync(request, CancellationToken.None).ConfigureAwait(false);

            await ReadSuccessStatusAsync("TurboScan start acknowledgement", CancellationToken.None)
                .ConfigureAwait(false);

            byte[] segmentPayload = BuildTurboScanSegmentPayload(segments);
            await _stream.WriteAsync(segmentPayload, CancellationToken.None).ConfigureAwait(false);
            await _stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            ulong resultCount;
            if (protocolShape.UseSnapshot)
            {
                byte[] planBytes = new byte[Ps5DebugProtocol.TurboScanSnapshotPlanSize];
                await _stream.ReadExactlyAsync(planBytes, CancellationToken.None).ConfigureAwait(false);
                ulong slotCount = BinaryPrimitives.ReadUInt64LittleEndian(planBytes.AsSpan(0, sizeof(ulong)));

                // The payload emits one progress record per snapshot I/O window. The normal
                // 16 MiB path produces relatively few records, but its documented allocation
                // fallback uses smaller windows and can legitimately exceed 1,024 records on
                // large targets. Always consume the protocol-defined stream through its
                // sentinel before validating the summary so the shared command stream cannot
                // be left mid-response by a host-side progress-count assumption.
                byte[] progressBytes = new byte[sizeof(ulong)];
                while (true)
                {
                    await _stream.ReadExactlyAsync(progressBytes, CancellationToken.None).ConfigureAwait(false);
                    ulong progressValue = BinaryPrimitives.ReadUInt64LittleEndian(progressBytes);
                    if (progressValue == ulong.MaxValue)
                    {
                        break;
                    }
                }

                byte[] summaryBytes = new byte[Ps5DebugProtocol.TurboScanSnapshotSummarySize];
                await _stream.ReadExactlyAsync(summaryBytes, CancellationToken.None).ConfigureAwait(false);
                uint snapshotStored = BinaryPrimitives.ReadUInt32LittleEndian(summaryBytes.AsSpan(0, sizeof(uint)));
                resultCount = BinaryPrimitives.ReadUInt64LittleEndian(summaryBytes.AsSpan(sizeof(uint), sizeof(ulong)));
                await ReadSuccessStatusAsync("TurboScan snapshot completion", CancellationToken.None).ConfigureAwait(false);

                // Validation happens only after the complete START response has been consumed.
                // A rejected snapshot therefore remains safe for the host's shared-scanner
                // fallback, and an invalid stored-session summary can still be closed cleanly.
                residentSessionCreated = snapshotStored == 1;

                if (snapshotStored == 0)
                {
                    throw new NotSupportedException(
                        "ps5debug-NG could not create the accelerated unknown-value snapshot; use the shared scanner fallback for this scan.");
                }

                if (snapshotStored != 1 || resultCount > slotCount)
                {
                    throw new InvalidDataException(
                        $"ps5debug-NG TurboScan returned invalid snapshot state {snapshotStored} with {resultCount:N0} survivors from {slotCount:N0} slots.");
                }
            }
            else
            {
                byte[] summaryBytes = new byte[Ps5DebugProtocol.TurboScanResidentSummarySize];
                await _stream.ReadExactlyAsync(summaryBytes, CancellationToken.None).ConfigureAwait(false);
                uint residentStored = BinaryPrimitives.ReadUInt32LittleEndian(summaryBytes.AsSpan(0, sizeof(uint)));
                resultCount = BinaryPrimitives.ReadUInt64LittleEndian(summaryBytes.AsSpan(sizeof(uint), sizeof(ulong)));

                if (residentStored == 0)
                {
                    byte[] sentinelBytes = new byte[sizeof(ulong)];
                    await _stream.ReadExactlyAsync(sentinelBytes, CancellationToken.None).ConfigureAwait(false);
                    ulong sentinel = BinaryPrimitives.ReadUInt64LittleEndian(sentinelBytes);
                    await ReadSuccessStatusAsync("TurboScan completion", CancellationToken.None).ConfigureAwait(false);

                    if (sentinel != ulong.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"ps5debug-NG TurboScan fallback returned unexpected sentinel 0x{sentinel:X16}.");
                    }

                    throw new NotSupportedException(
                        "ps5debug-NG could not keep the accelerated scan result set server-side; use the shared scanner fallback for this scan.");
                }

                await ReadSuccessStatusAsync("TurboScan completion", CancellationToken.None).ConfigureAwait(false);
                residentSessionCreated = residentStored == 1;

                if (residentStored != 1)
                {
                    throw new InvalidDataException(
                        $"ps5debug-NG TurboScan returned invalid resident state {residentStored}.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            _turboScanSessionActive = true;
            _turboScanProcessId = processId;
            _turboScanResultCount = resultCount;
            _turboScanValueTypeId = request.ValueTypeId;
            _turboScanValueLength = request.ValueSize;
            _turboScanAlignment = request.Alignment;
            long sessionGeneration = checked(++_turboScanSessionGeneration);
            TurboScanResultStream resultStream = new(
                this,
                processId,
                resultCount,
                request,
                isRefinement: false,
                sessionGeneration: sessionGeneration);
            keepResidentSession = true;
            return resultStream;
        }
        finally
        {
            if (residentSessionCreated && !keepResidentSession)
            {
                try
                {
                    await EndTurboScanAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the primary scan/cancellation exception. A damaged connection
                    // will surface naturally on the next command instead of masking it here.
                }

                ResetTurboScanSessionState();
            }
        }
    }

    public async Task<IReadOnlyList<NativeValueScanResult>> RefineValuesAsync(
        int processId,
        IReadOnlyList<ulong> previousAddresses,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previousAddresses);
        await using INativeValueScanResultStream resultStream = await StartRefineValuesStreamAsync(
                processId,
                previousAddresses.Count,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        return await MaterializeNativeResultStreamAsync(resultStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<INativeValueScanResultStream> StartRefineValuesStreamAsync(
        int processId,
        long previousResultCount,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        if (previousResultCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previousResultCount));
        }

        NativeScanProtocolShape protocolShape = ValidateNativeScanRequest(
            processId,
            request,
            MemoryScanStage.NextScan);

        cancellationToken.ThrowIfCancellationRequested();

        if (IsExactValueRequest(request) &&
            IsFloatingPointRequest(request) &&
            !UsesPs5DebugFloatingTolerance(request))
        {
            throw new NotSupportedException(
                "Strict Float and Double refinement uses the shared scanner because ps5debug-NG resident TurboScan applies a relative 1e-6 tolerance.");
        }

        if (!_turboScanSessionActive || _turboScanProcessId != processId)
        {
            throw new NotSupportedException(
                "No compatible resident TurboScan session is available for native refinement.");
        }

        if (!string.Equals(_turboScanValueTypeId, request.ValueTypeId, StringComparison.OrdinalIgnoreCase) ||
            _turboScanValueLength != request.ValueSize ||
            _turboScanAlignment != request.Alignment)
        {
            throw new NotSupportedException(
                "The requested Value Type or width does not match the resident TurboScan session. Start a New Scan before changing Value Type.");
        }

        if (checked((ulong)previousResultCount) != _turboScanResultCount)
        {
            throw new NotSupportedException(
                "The resident TurboScan survivor count no longer matches the host scan session; use the shared refinement fallback.");
        }

        if (_turboScanResultCount == 0)
        {
            return new EmptyNativeValueScanResultStream();
        }

        cancellationToken.ThrowIfCancellationRequested();

        uint flags = Ps5DebugProtocol.TurboScanFlagServerResident;
        if ((_turboScanEngines & Ps5DebugProtocol.TurboScanEngineRescanAliasing) != 0)
        {
            flags |= Ps5DebugProtocol.TurboScanFlagRescanAliasing;
        }

        byte[] body = new byte[Ps5DebugProtocol.TurboScanCountBodySize];
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(0, sizeof(int)), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(4, sizeof(ulong)), 0);
        body[12] = GetRefinementScanValueType(request);
        body[13] = protocolShape.CompareType;
        BinaryPrimitives.WriteUInt32LittleEndian(
            body.AsSpan(14, sizeof(uint)),
            checked((uint)protocolShape.ComparisonDataLength));
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(18, sizeof(uint)), flags);

        bool transactionStarted = false;
        try
        {
            await SendCommandAsync(
                    Ps5DebugProtocol.CommandTurboScanCount,
                    body,
                    CancellationToken.None)
                .ConfigureAwait(false);
            transactionStarted = true;
            await ReadSuccessStatusAsync("TurboScan refinement acknowledgement", CancellationToken.None)
                .ConfigureAwait(false);

            await WriteScanComparisonDataAsync(request, CancellationToken.None).ConfigureAwait(false);

            byte[] progressBytes = new byte[sizeof(ulong)];
            while (true)
            {
                await _stream.ReadExactlyAsync(progressBytes, CancellationToken.None).ConfigureAwait(false);
                ulong progressValue = BinaryPrimitives.ReadUInt64LittleEndian(progressBytes);
                if (progressValue == ulong.MaxValue)
                {
                    break;
                }
            }

            byte[] countBytes = new byte[sizeof(ulong)];
            await _stream.ReadExactlyAsync(countBytes, CancellationToken.None).ConfigureAwait(false);
            ulong resultCount = BinaryPrimitives.ReadUInt64LittleEndian(countBytes);
            await ReadSuccessStatusAsync("TurboScan refinement completion", CancellationToken.None)
                .ConfigureAwait(false);

            if (resultCount > _turboScanResultCount)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG TurboScan refinement increased the survivor count from {_turboScanResultCount:N0} to {resultCount:N0}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            _turboScanResultCount = resultCount;
            long sessionGeneration = checked(++_turboScanSessionGeneration);
            return new TurboScanResultStream(
                this,
                processId,
                resultCount,
                request,
                isRefinement: true,
                sessionGeneration: sessionGeneration);
        }
        catch
        {
            if (transactionStarted && _turboScanSessionActive)
            {
                try
                {
                    await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    ResetTurboScanSessionState();
                }
            }

            throw;
        }
    }

    public Task ResetNativeScanAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return EndActiveTurboScanSessionAsync(CancellationToken.None);
    }

    public Task SuspendProcessAsync(int processId, CancellationToken cancellationToken)
    {
        return SetProcessStateAsync(
            processId,
            Ps5DebugProtocol.ProcessStateSuspend,
            "process suspend",
            cancellationToken);
    }

    public Task ResumeProcessAsync(int processId, CancellationToken cancellationToken)
    {
        return SetProcessStateAsync(
            processId,
            Ps5DebugProtocol.ProcessStateResume,
            "process resume",
            cancellationToken);
    }

    private async Task SetProcessStateAsync(
        int processId,
        byte state,
        string operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        byte[] request = new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(0, sizeof(int)), processId);
        request[4] = state;

        cancellationToken.ThrowIfCancellationRequested();

        await SendCommandAsync(
                Ps5DebugProtocol.CommandDebugProcessStop,
                request,
                CancellationToken.None)
            .ConfigureAwait(false);
        await ReadSuccessStatusAsync(operation, CancellationToken.None).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _stream.Dispose();
        _tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task IdentifyTargetAsync(CancellationToken cancellationToken)
    {
        string protocolVersion = await ReadLengthPrefixedStringAsync(
                Ps5DebugProtocol.CommandVersion,
                cancellationToken)
            .ConfigureAwait(false);

        ushort platformId = await ReadUInt16ResponseAsync(
                Ps5DebugProtocol.CommandPlatformId,
                cancellationToken)
            .ConfigureAwait(false);

        if (platformId != Ps5DebugProtocol.PlatformIdPlayStation5)
        {
            throw new InvalidDataException(
                $"The remote server reported platform id {platformId}; expected PlayStation 5 platform id {Ps5DebugProtocol.PlatformIdPlayStation5}.");
        }

        byte[] brandingPayload = await ReadLengthPrefixedPayloadAsync(
                Ps5DebugProtocol.CommandBranding,
                cancellationToken)
            .ConfigureAwait(false);
        (string branding, string? capabilityLevel) = ParseBranding(brandingPayload);

        if (!branding.StartsWith("ps5debug-NG", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The remote server did not identify itself as ps5debug-NG. Reported branding: '{branding}'.");
        }

        ushort firmwareVersion = await ReadUInt16ResponseAsync(
                Ps5DebugProtocol.CommandFirmwareVersion,
                cancellationToken)
            .ConfigureAwait(false);

        await PingAsync(cancellationToken).ConfigureAwait(false);
        SupportsTurboValueScan = await ProbeTurboScanAsync(cancellationToken).ConfigureAwait(false);

        ConnectionInfo = new Ps5DebugConnectionInfo(
            protocolVersion,
            branding,
            capabilityLevel,
            firmwareVersion);
    }

    private async Task PingAsync(CancellationToken cancellationToken)
    {
        await SendHeaderAsync(Ps5DebugProtocol.CommandProcessNop, cancellationToken).ConfigureAwait(false);

        byte[] statusBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(statusBytes, cancellationToken).ConfigureAwait(false);
        uint status = BinaryPrimitives.ReadUInt32LittleEndian(statusBytes);

        if (status != Ps5DebugProtocol.WireStatusSuccess)
        {
            throw new InvalidDataException(
                $"ps5debug-NG liveness probe returned unexpected status 0x{status:X8}.");
        }
    }

    private async Task<bool> ProbeTurboScanAsync(CancellationToken cancellationToken)
    {
        await SendHeaderAsync(Ps5DebugProtocol.CommandTurboScanCaps, cancellationToken).ConfigureAwait(false);
        uint status = await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status != Ps5DebugProtocol.WireStatusSuccess)
        {
            _turboScanEngines = 0;
            return false;
        }

        byte[] response = new byte[Ps5DebugProtocol.TurboScanCapabilitiesSize];
        await _stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(0, sizeof(uint)));
        uint engines = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(4, sizeof(uint)));
        uint requiredEngines =
            Ps5DebugProtocol.TurboScanEngineServerResident |
            Ps5DebugProtocol.TurboScanEngineSegments;

        bool supported = version >= 1 && (engines & requiredEngines) == requiredEngines;
        _turboScanEngines = supported ? engines : 0;
        return supported;
    }

    private async Task EnsureScanAuthorizationAsync(CancellationToken cancellationToken)
    {
        if (_scanAuthorizationEstablished)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] body = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, sizeof(uint)), Ps5DebugProtocol.ProcessAuthMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4, sizeof(uint)), Ps5DebugProtocol.ProcessAuthScanFlag);

        await SendCommandAsync(Ps5DebugProtocol.CommandProcessAuth, body, CancellationToken.None)
            .ConfigureAwait(false);
        await ReadSuccessStatusAsync("scan authorization acknowledgement", CancellationToken.None)
            .ConfigureAwait(false);

        byte[] lengthBytes = new byte[sizeof(ushort)];
        await _stream.ReadExactlyAsync(lengthBytes, CancellationToken.None).ConfigureAwait(false);
        ushort challengeLength = BinaryPrimitives.ReadUInt16LittleEndian(lengthBytes);
        if (challengeLength is 0 or > 256)
        {
            throw new InvalidDataException(
                $"ps5debug-NG scan authorization returned invalid challenge length {challengeLength}.");
        }

        byte[] challenge = new byte[challengeLength];
        await _stream.ReadExactlyAsync(challenge, CancellationToken.None).ConfigureAwait(false);
        byte[] keystream = BuildAuthKeystream(challengeLength);
        byte[] response = new byte[challengeLength];
        for (int index = 0; index < response.Length; index++)
        {
            response[index] = (byte)(challenge[index] ^ keystream[index]);
        }

        await _stream.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
        await _stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        await ReadSuccessStatusAsync("scan authorization completion", CancellationToken.None)
            .ConfigureAwait(false);

        _scanAuthorizationEstablished = true;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private NativeScanProtocolShape ValidateNativeScanRequest(
        int processId,
        NativeValueScanRequest request,
        MemoryScanStage stage)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (!SupportsTurboValueScan)
        {
            throw new NotSupportedException(
                "The connected ps5debug-NG server does not advertise the required TurboScan capabilities.");
        }

        if (!Ps5NativeScanTypeMappings.TryResolve(
                request.ScanTypeId,
                request.ValueTypeId,
                stage,
                out byte compareType,
                out int inputValueCount,
                out Ps5NativeScanMode nativeMode))
        {
            throw new NotSupportedException(
                $"The PS5 plugin does not provide a semantically equivalent native {stage} mapping for Scan Type '{request.ScanTypeId}' with Value Type '{request.ValueTypeId}'.");
        }

        if (nativeMode == Ps5NativeScanMode.SnapshotIncludeZeros &&
            (_turboScanEngines & Ps5DebugProtocol.TurboScanEngineSnapshot) == 0)
        {
            throw new NotSupportedException(
                "The connected ps5debug-NG server does not advertise the TurboScan snapshot engine required for native Unknown Initial Value scanning.");
        }

        if (request.InputValues.Count != inputValueCount)
        {
            throw new NotSupportedException(
                $"The PS5 native mapping for Scan Type '{request.ScanTypeId}' requires {inputValueCount} comparison value(s), but {request.InputValues.Count} were supplied.");
        }

        int expectedSize = request.ValueTypeId switch
        {
            StandardMemoryValueTypeIds.UInt8 or StandardMemoryValueTypeIds.Int8 => 1,
            StandardMemoryValueTypeIds.UInt16 or StandardMemoryValueTypeIds.Int16 => 2,
            StandardMemoryValueTypeIds.UInt32 or StandardMemoryValueTypeIds.Int32 or StandardMemoryValueTypeIds.Float32 => 4,
            StandardMemoryValueTypeIds.UInt64 or StandardMemoryValueTypeIds.Int64 or StandardMemoryValueTypeIds.Float64 => 8,
            StandardMemoryValueTypeIds.ByteArray => request.ValueSize,
            _ => 0
        };

        if (expectedSize == 0 ||
            request.ValueSize != expectedSize ||
            request.InputValues.Any(value => value.Length != expectedSize) ||
            request.ValueSize > Ps5DebugProtocol.TurboScanMaximumResidentValueLength)
        {
            throw new NotSupportedException(
                "The selected Value Type, comparison payload, or value width is not supported by the ps5debug-NG resident TurboScan path.");
        }

        if (request.Alignment is < 1 or > byte.MaxValue)
        {
            throw new NotSupportedException(
                $"ps5debug-NG TurboScan alignment must be between 1 and {byte.MaxValue} bytes.");
        }

        Endianness endianness = GetRequestedEndianness(request);
        if (nativeMode != Ps5NativeScanMode.SnapshotIncludeZeros &&
            endianness == Endianness.Big && request.ValueSize > 1)
        {
            if (IsFloatingPointRequest(request))
            {
                throw new NotSupportedException(
                    "ps5debug-NG evaluates native Float and Double values as little-endian. Big-endian floating-point scans use the shared scanner fallback.");
            }

            if (!IsEndianIndependentIntegerComparison(request.ScanTypeId))
            {
                throw new NotSupportedException(
                    "This ps5debug-NG native comparison interprets multi-byte integers as little-endian. The selected Big Endian scan uses the shared scanner fallback.");
            }
        }

        return new NativeScanProtocolShape(
            compareType,
            inputValueCount,
            checked(inputValueCount * expectedSize),
            nativeMode == Ps5NativeScanMode.SnapshotIncludeZeros);
    }

    private static bool IsEndianIndependentIntegerComparison(string scanTypeId)
    {
        return scanTypeId is StandardMemoryScanTypeIds.ExactValue or
            StandardMemoryScanTypeIds.ChangedValue or
            StandardMemoryScanTypeIds.UnchangedValue;
    }

    private static byte GetScanValueType(string valueTypeId)
    {
        return valueTypeId switch
        {
            StandardMemoryValueTypeIds.UInt8 => Ps5DebugProtocol.ScanValueTypeUInt8,
            StandardMemoryValueTypeIds.Int8 => Ps5DebugProtocol.ScanValueTypeInt8,
            StandardMemoryValueTypeIds.UInt16 => Ps5DebugProtocol.ScanValueTypeUInt16,
            StandardMemoryValueTypeIds.Int16 => Ps5DebugProtocol.ScanValueTypeInt16,
            StandardMemoryValueTypeIds.UInt32 => Ps5DebugProtocol.ScanValueTypeUInt32,
            StandardMemoryValueTypeIds.Int32 => Ps5DebugProtocol.ScanValueTypeInt32,
            StandardMemoryValueTypeIds.UInt64 => Ps5DebugProtocol.ScanValueTypeUInt64,
            StandardMemoryValueTypeIds.Int64 => Ps5DebugProtocol.ScanValueTypeInt64,
            StandardMemoryValueTypeIds.Float32 => Ps5DebugProtocol.ScanValueTypeFloat32,
            StandardMemoryValueTypeIds.Float64 => Ps5DebugProtocol.ScanValueTypeFloat64,
            StandardMemoryValueTypeIds.ByteArray => Ps5DebugProtocol.ScanValueTypeByteArray,
            _ => throw new NotSupportedException(
                $"Value Type id '{valueTypeId}' is not supported by ps5debug-NG TurboScan.")
        };
    }

    private static byte GetRefinementScanValueType(NativeValueScanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Core defines Changed/Unchanged as stored-byte comparisons. ps5debug-NG's
        // Float/Double comparator uses IEEE equality, where an unchanged NaN is not equal
        // to itself. Reusing the same-width unsigned type preserves the exact payload
        // while keeping the resident record width and GET decoding unchanged.
        if (request.ScanTypeId is StandardMemoryScanTypeIds.ChangedValue or
            StandardMemoryScanTypeIds.UnchangedValue)
        {
            return request.ValueTypeId switch
            {
                StandardMemoryValueTypeIds.Float32 => Ps5DebugProtocol.ScanValueTypeUInt32,
                StandardMemoryValueTypeIds.Float64 => Ps5DebugProtocol.ScanValueTypeUInt64,
                _ => GetScanValueType(request.ValueTypeId)
            };
        }

        return GetScanValueType(request.ValueTypeId);
    }

    private async Task WriteScanComparisonDataAsync(
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        foreach (ReadOnlyMemory<byte> comparisonValue in request.InputValues)
        {
            await _stream.WriteAsync(comparisonValue, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(
                request.ValueTypeId,
                StandardMemoryValueTypeIds.ByteArray,
                StringComparison.OrdinalIgnoreCase))
        {
            if (request.InputValues.Count != 1)
            {
                throw new NotSupportedException(
                    "ps5debug-NG Array of Bytes native scanning requires exactly one comparison value.");
            }

            byte[] mask = new byte[request.InputValues[0].Length];
            Array.Fill(mask, (byte)1);
            await _stream.WriteAsync(mask, cancellationToken).ConfigureAwait(false);
        }

        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<NativeValueScanResult>> MaterializeNativeResultStreamAsync(
        INativeValueScanResultStream resultStream,
        CancellationToken cancellationToken)
    {
        if (resultStream.SourceResultCount > Ps5DebugProtocol.MaximumLegacyMaterializedNativeScanResultCount)
        {
            throw new InvalidDataException(
                $"ps5debug-NG TurboScan returned {resultStream.SourceResultCount:N0} matches, exceeding the {Ps5DebugProtocol.MaximumLegacyMaterializedNativeScanResultCount:N0} legacy in-memory result safety limit. Use the disk-backed streaming scan path for massive result sets.");
        }

        List<NativeValueScanResult> results = new(checked((int)resultStream.SourceResultCount));
        await foreach (NativeValueScanResultBatch batch in resultStream
                           .ReadBatchesAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            for (int index = 0; index < batch.Count; index++)
            {
                results.Add(new NativeValueScanResult(
                    batch.Addresses.Span[index],
                    batch.GetCurrentValueData(index).Span));
            }
        }

        return results;
    }

    private IAsyncEnumerable<NativeValueScanResultBatch> FetchTurboScanResultBatchesAsync(
        ulong resultCount,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        return FetchTurboScanResultBatchesAsync(
            0,
            resultCount,
            Ps5DebugProtocol.TurboScanGetBatchSize,
            includePreviousValues: false,
            request: request,
            cancellationToken: cancellationToken);
    }

    private async IAsyncEnumerable<NativeValueScanResultBatch> FetchTurboScanResultBatchesAsync(
        ulong startIndex,
        ulong maximumRecords,
        int maximumRecordsPerBatch,
        bool includePreviousValues,
        NativeValueScanRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (maximumRecordsPerBatch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRecordsPerBatch));
        }

        if (startIndex > uint.MaxValue)
        {
            throw new NotSupportedException(
                "ps5debug-NG TurboScan GET cannot address a resident result window starting beyond the 32-bit result-index range.");
        }

        int valueLength = request.ValueSize;
        int maximumRecordSize = checked(sizeof(ulong) + (valueLength * 3));
        uint payloadLimitedBatchSize = checked((uint)Math.Max(
            1,
            Ps5DebugProtocol.TurboScanGetMaximumPayloadSize / maximumRecordSize));
        uint maximumBatchSize = Math.Min(
            checked((uint)Math.Min(Ps5DebugProtocol.TurboScanGetBatchSize, maximumRecordsPerBatch)),
            payloadLimitedBatchSize);

        ulong fetched = 0;
        while (fetched < maximumRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong absoluteIndex = checked(startIndex + fetched);
            if (absoluteIndex > uint.MaxValue)
            {
                throw new NotSupportedException(
                    "ps5debug-NG TurboScan GET cannot address resident results beyond the 32-bit result-index range.");
            }

            uint requestedCount = checked((uint)Math.Min(
                (ulong)maximumBatchSize,
                maximumRecords - fetched));
            ulong addressableRemaining = ((ulong)uint.MaxValue + 1UL) - absoluteIndex;
            requestedCount = checked((uint)Math.Min((ulong)requestedCount, addressableRemaining));

            byte[] getBody = new byte[Ps5DebugProtocol.TurboScanGetBodySize];
            BinaryPrimitives.WriteUInt32LittleEndian(getBody.AsSpan(0, sizeof(uint)), checked((uint)absoluteIndex));
            BinaryPrimitives.WriteUInt32LittleEndian(getBody.AsSpan(4, sizeof(uint)), requestedCount);
            BinaryPrimitives.WriteUInt32LittleEndian(getBody.AsSpan(8, sizeof(uint)), 0);

            await SendCommandAsync(
                    Ps5DebugProtocol.CommandTurboScanGet,
                    getBody,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await ReadSuccessStatusAsync("TurboScan GET acknowledgement", CancellationToken.None)
                .ConfigureAwait(false);

            byte[] countBytes = new byte[sizeof(uint)];
            await _stream.ReadExactlyAsync(countBytes, CancellationToken.None).ConfigureAwait(false);
            uint headerCount = BinaryPrimitives.ReadUInt32LittleEndian(countBytes);
            bool includesFirstValue = (headerCount & 0x80000000u) != 0;
            uint actualCount = headerCount & 0x7FFFFFFFu;

            if (actualCount > requestedCount)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG TurboScan GET returned {actualCount} records after requesting {requestedCount}.");
            }

            int storedValueCount = includesFirstValue ? 3 : 2;
            int recordSize = checked(sizeof(ulong) + (valueLength * storedValueCount));
            byte[] records = new byte[checked((int)actualCount * recordSize)];
            await _stream.ReadExactlyAsync(records, CancellationToken.None).ConfigureAwait(false);
            await ReadSuccessStatusAsync("TurboScan GET completion", CancellationToken.None)
                .ConfigureAwait(false);

            if (actualCount == 0)
            {
                throw new InvalidDataException(
                    "ps5debug-NG TurboScan GET returned zero records before the requested resident result window was exhausted.");
            }

            int actualCountInt = checked((int)actualCount);
            ulong[] addresses = new ulong[actualCountInt];
            byte[] currentValues = new byte[checked(actualCountInt * valueLength)];
            byte[] previousValues = includePreviousValues
                ? new byte[checked(actualCountInt * valueLength)]
                : Array.Empty<byte>();
            int acceptedCount = 0;
            bool requiresStrictFloatingPointFilter =
                IsExactValueRequest(request) &&
                IsFloatingPointRequest(request) &&
                !UsesPs5DebugFloatingTolerance(request);

            for (int index = 0; index < actualCountInt; index++)
            {
                ReadOnlySpan<byte> record = records.AsSpan(index * recordSize, recordSize);
                ulong address = BinaryPrimitives.ReadUInt64LittleEndian(record[..sizeof(ulong)]);
                ReadOnlySpan<byte> currentValue = record.Slice(sizeof(ulong), valueLength);

                if (requiresStrictFloatingPointFilter &&
                    !StrictFloatingPointValueMatches(currentValue, request))
                {
                    continue;
                }

                addresses[acceptedCount] = address;
                currentValue.CopyTo(currentValues.AsSpan(acceptedCount * valueLength, valueLength));
                if (includePreviousValues)
                {
                    ReadOnlySpan<byte> previousValue = record.Slice(sizeof(ulong) + valueLength, valueLength);
                    previousValue.CopyTo(previousValues.AsSpan(acceptedCount * valueLength, valueLength));
                }

                acceptedCount++;
            }

            fetched += actualCount;

            if (acceptedCount != addresses.Length)
            {
                Array.Resize(ref addresses, acceptedCount);
                Array.Resize(ref currentValues, checked(acceptedCount * valueLength));
                if (includePreviousValues)
                {
                    Array.Resize(ref previousValues, checked(acceptedCount * valueLength));
                }
            }

            yield return includePreviousValues
                ? new NativeValueScanResultBatch(
                    addresses,
                    currentValues,
                    previousValues,
                    valueLength,
                    fetched)
                : new NativeValueScanResultBatch(
                    addresses,
                    currentValues,
                    valueLength,
                    fetched);
        }
    }

    private static bool StrictFloatingPointValueMatches(
        ReadOnlySpan<byte> currentValue,
        NativeValueScanRequest request)
    {
        if (request.InputValues.Count != 1)
        {
            return false;
        }

        ReadOnlySpan<byte> comparisonValue = request.InputValues[0].Span;
        if (currentValue.Length != comparisonValue.Length)
        {
            return false;
        }

        Endianness endianness = GetRequestedEndianness(request);
        return request.ValueTypeId switch
        {
            StandardMemoryValueTypeIds.Float32 when currentValue.Length == sizeof(float) =>
                ReadFloat32(currentValue, endianness) == ReadFloat32(comparisonValue, endianness),
            StandardMemoryValueTypeIds.Float64 when currentValue.Length == sizeof(double) =>
                ReadFloat64(currentValue, endianness) == ReadFloat64(comparisonValue, endianness),
            _ => false
        };
    }

    private static bool IsExactValueRequest(NativeValueScanRequest request)
    {
        return string.Equals(
            request.ScanTypeId,
            StandardMemoryScanTypeIds.ExactValue,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFloatingPointRequest(NativeValueScanRequest request)
    {
        return request.ValueTypeId is StandardMemoryValueTypeIds.Float32 or StandardMemoryValueTypeIds.Float64;
    }

    private static bool UsesPs5DebugFloatingTolerance(NativeValueScanRequest request)
    {
        return request.ScanOptions.TryGetValue(
                   StandardMemoryScanOptionIds.FloatingPointRounding,
                   out string choiceId) &&
               string.Equals(
                   choiceId,
                   StandardMemoryScanOptionChoiceIds.FloatingPointRelativeTolerance1E6,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Endianness GetRequestedEndianness(NativeValueScanRequest request)
    {
        if (request.ScanOptions.TryGetValue(
                StandardMemoryScanOptionIds.Endianness,
                out string choiceId) &&
            string.Equals(
                choiceId,
                StandardMemoryScanOptionChoiceIds.BigEndian,
                StringComparison.OrdinalIgnoreCase))
        {
            return Endianness.Big;
        }

        return Endianness.Little;
    }

    private static float ReadFloat32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        int bits = endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(bytes)
            : BinaryPrimitives.ReadInt32LittleEndian(bytes);
        return BitConverter.Int32BitsToSingle(bits);
    }

    private static double ReadFloat64(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        long bits = endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt64BigEndian(bytes)
            : BinaryPrimitives.ReadInt64LittleEndian(bytes);
        return BitConverter.Int64BitsToDouble(bits);
    }

    private sealed class EmptyNativeValueScanResultStream : INativeValueScanResultStream
    {
        public ulong SourceResultCount => 0;

        public async IAsyncEnumerable<NativeValueScanResultBatch> ReadBatchesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TurboScanResultStream : INativeValueScanResultStream, INativeValueScanResidentResultSet
    {
        private readonly Ps5DebugClient _owner;
        private readonly int _processId;
        private readonly NativeValueScanRequest _request;
        private readonly bool _isRefinement;
        private readonly long _sessionGeneration;
        private bool _enumerationStarted;
        private bool _completed;
        private bool _disposed;

        public TurboScanResultStream(
            Ps5DebugClient owner,
            int processId,
            ulong sourceResultCount,
            NativeValueScanRequest request,
            bool isRefinement,
            long sessionGeneration)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _processId = processId;
            _isRefinement = isRefinement;
            _sessionGeneration = sessionGeneration;
            SourceResultCount = sourceResultCount;
            Count = checked((long)sourceResultCount);
        }

        public ulong SourceResultCount { get; }

        public long Count { get; }

        public int ValueSize => _request.ValueSize;

        public int Alignment => _request.Alignment;

        public bool IsAuthoritative => !(
            IsExactValueRequest(_request) &&
            IsFloatingPointRequest(_request) &&
            !UsesPs5DebugFloatingTolerance(_request));

        public bool CanRefine(NativeValueScanRequest request)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(request);
            return IsAuthoritative && _owner.CanRefineTurboScanResidentSession(
                _sessionGeneration,
                _processId,
                SourceResultCount,
                request);
        }

        public async IAsyncEnumerable<NativeValueScanResultBatch> ReadBatchesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_enumerationStarted)
            {
                throw new InvalidOperationException("A native TurboScan result stream can only be consumed once.");
            }

            _enumerationStarted = true;
            await foreach (NativeValueScanResultBatch batch in _owner
                               .FetchTurboScanResultBatchesAsync(
                                   0,
                                   SourceResultCount,
                                   Ps5DebugProtocol.TurboScanGetBatchSize,
                                   includePreviousValues: _isRefinement,
                                   request: _request,
                                   cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return batch;
            }

            _completed = true;
        }

        public async IAsyncEnumerable<NativeValueScanResultBatch> ReadResultBatchesAsync(
            long startIndex,
            long maximumRecords,
            int maximumRecordsPerBatch,
            bool includePreviousValues,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (startIndex < 0 || startIndex > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (maximumRecords < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRecords));
            }

            long boundedCount = Math.Min(maximumRecords, Count - startIndex);
            if (boundedCount == 0)
            {
                yield break;
            }

            EnsureCurrentResidentSession();
            await foreach (NativeValueScanResultBatch batch in _owner
                               .FetchTurboScanResultBatchesAsync(
                                   checked((ulong)startIndex),
                                   checked((ulong)boundedCount),
                                   maximumRecordsPerBatch,
                                   includePreviousValues,
                                   _request,
                                   cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return batch;
            }
        }

        public async IAsyncEnumerable<ReadOnlyMemory<ulong>> ReadAddressBatchesAsync(
            int maximumRecordsPerBatch,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (NativeValueScanResultBatch batch in ReadResultBatchesAsync(
                               0,
                               Count,
                               maximumRecordsPerBatch,
                               includePreviousValues: false,
                               cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return batch.Addresses;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_completed || !_owner.IsCurrentTurboScanSession(_sessionGeneration))
            {
                return;
            }

            try
            {
                await _owner.EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                _owner.ResetTurboScanSessionState();
            }
        }

        private void EnsureCurrentResidentSession()
        {
            if (!_owner.IsCurrentTurboScanSession(_sessionGeneration))
            {
                throw new InvalidOperationException(
                    "The native TurboScan resident result handle has been replaced by a newer scan generation.");
            }
        }
    }

    private bool IsCurrentTurboScanSession(long generation)
    {
        return _turboScanSessionActive && generation == _turboScanSessionGeneration;
    }

    private bool CanRefineTurboScanResidentSession(
        long generation,
        int processId,
        ulong resultCount,
        NativeValueScanRequest request)
    {
        if (_disposed ||
            !_turboScanSessionActive ||
            generation != _turboScanSessionGeneration ||
            _turboScanProcessId != processId ||
            _turboScanResultCount != resultCount ||
            !string.Equals(_turboScanValueTypeId, request.ValueTypeId, StringComparison.OrdinalIgnoreCase) ||
            _turboScanValueLength != request.ValueSize ||
            _turboScanAlignment != request.Alignment)
        {
            return false;
        }

        if (IsExactValueRequest(request) &&
            IsFloatingPointRequest(request) &&
            !UsesPs5DebugFloatingTolerance(request))
        {
            return false;
        }

        try
        {
            _ = ValidateNativeScanRequest(processId, request, MemoryScanStage.NextScan);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task EndActiveTurboScanSessionAsync(CancellationToken cancellationToken)
    {
        if (!_turboScanSessionActive)
        {
            return;
        }

        try
        {
            await EndTurboScanAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ResetTurboScanSessionState();
        }
    }

    private void ResetTurboScanSessionState()
    {
        _turboScanSessionActive = false;
        _turboScanProcessId = 0;
        _turboScanResultCount = 0;
        _turboScanValueTypeId = null;
        _turboScanValueLength = 0;
        _turboScanAlignment = 0;
    }

    private async Task EndTurboScanAsync(CancellationToken cancellationToken)
    {
        await SendHeaderAsync(Ps5DebugProtocol.CommandTurboScanEnd, cancellationToken).ConfigureAwait(false);
        await ReadSuccessStatusAsync("TurboScan END", cancellationToken).ConfigureAwait(false);
    }

    private static List<TurboScanSegment> BuildTurboScanSegments(
        IReadOnlyList<MemoryRegion> memoryRegions,
        int valueLength,
        int alignment)
    {
        List<TurboScanSegment> segments = new();
        ulong alignmentValue = checked((ulong)alignment);
        ulong valueLengthValue = checked((ulong)valueLength);
        ulong maximumSegmentLength = uint.MaxValue;
        ulong maximumCandidatesPerSegment =
            ((maximumSegmentLength - valueLengthValue) / alignmentValue) + 1;

        foreach (MemoryRegion region in memoryRegions)
        {
            if (!region.Protection.HasFlag(MemoryProtection.Read) ||
                region.Protection.HasFlag(MemoryProtection.Guard))
            {
                continue;
            }

            ulong remainder = region.BaseAddress % alignmentValue;
            ulong start = remainder == 0
                ? region.BaseAddress
                : checked(region.BaseAddress + (alignmentValue - remainder));
            ulong end = region.EndAddressExclusive;
            if (start >= end || end - start < valueLengthValue)
            {
                continue;
            }

            ulong candidateCount = ((end - valueLengthValue - start) / alignmentValue) + 1;
            ulong address = start;

            while (candidateCount > 0)
            {
                ulong segmentCandidateCount = Math.Min(candidateCount, maximumCandidatesPerSegment);
                ulong segmentLength = checked(
                    ((segmentCandidateCount - 1) * alignmentValue) + valueLengthValue);

                segments.Add(new TurboScanSegment(address, checked((uint)segmentLength)));
                if ((uint)segments.Count > Ps5DebugProtocol.MaximumTurboScanSegmentCount)
                {
                    throw new InvalidDataException(
                        $"The target requires more than {Ps5DebugProtocol.MaximumTurboScanSegmentCount:N0} TurboScan segments.");
                }

                address = checked(address + (segmentCandidateCount * alignmentValue));
                candidateCount -= segmentCandidateCount;
            }
        }

        return segments;
    }

    private static byte[] BuildTurboScanSegmentPayload(IReadOnlyList<TurboScanSegment> segments)
    {
        int payloadLength = checked(sizeof(uint) + (segments.Count * Ps5DebugProtocol.TurboScanSegmentSize));
        byte[] payload = new byte[payloadLength];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, sizeof(uint)), checked((uint)segments.Count));

        for (int index = 0; index < segments.Count; index++)
        {
            int offset = checked(sizeof(uint) + (index * Ps5DebugProtocol.TurboScanSegmentSize));
            BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(offset, sizeof(ulong)), segments[index].Address);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + sizeof(ulong), sizeof(uint)), segments[index].Length);
        }

        return payload;
    }

    private static byte[] BuildAuthKeystream(int length)
    {
        uint s1 = 200;
        uint s2 = 300;
        uint s3 = 400;
        uint s4 = 500;
        byte[] output = new byte[length];

        for (int index = 0; index < output.Length; index++)
        {
            s1 = ((s1 << 18) & 0xFFF80000u) ^ ((s1 ^ (s1 << 6)) >> 13);
            s2 = ((s2 << 2) & 0xFFFFFFE0u) ^ ((s2 ^ (s2 << 2)) >> 27);
            s3 = ((s3 << 7) & 0xFFFFF800u) ^ ((s3 ^ (s3 << 13)) >> 21);
            s4 = ((s4 << 13) & 0xFFF00000u) ^ ((s4 ^ (s4 << 3)) >> 12);
            output[index] = (byte)(s1 ^ s2 ^ s3 ^ s4);
        }

        return output;
    }

    private async Task<uint> ReadStatusAsync(CancellationToken cancellationToken)
    {
        byte[] statusBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(statusBytes, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(statusBytes);
    }

    private async Task ReadSuccessStatusAsync(
        string operation,
        CancellationToken cancellationToken)
    {
        uint status = await ReadStatusAsync(cancellationToken).ConfigureAwait(false);

        if (status != Ps5DebugProtocol.WireStatusSuccess)
        {
            throw new InvalidDataException(
                $"ps5debug-NG {operation} returned unexpected status 0x{status:X8}.");
        }
    }


    private async Task<string> ReadLengthPrefixedStringAsync(
        uint command,
        CancellationToken cancellationToken)
    {
        byte[] payload = await ReadLengthPrefixedPayloadAsync(command, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    private async Task<byte[]> ReadLengthPrefixedPayloadAsync(
        uint command,
        CancellationToken cancellationToken)
    {
        await SendHeaderAsync(command, cancellationToken).ConfigureAwait(false);

        byte[] lengthBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);

        if (length > Ps5DebugProtocol.MaximumMetadataLength)
        {
            throw new InvalidDataException(
                $"ps5debug-NG returned an invalid metadata payload length of {length} bytes.");
        }

        int payloadLength = checked((int)length);
        byte[] payload = new byte[payloadLength];
        await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private async Task<ushort> ReadUInt16ResponseAsync(
        uint command,
        CancellationToken cancellationToken)
    {
        await SendHeaderAsync(command, cancellationToken).ConfigureAwait(false);

        byte[] response = new byte[sizeof(ushort)];
        await _stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(response);
    }

    private Task SendHeaderAsync(uint command, CancellationToken cancellationToken)
    {
        return SendCommandAsync(command, ReadOnlyMemory<byte>.Empty, cancellationToken);
    }

    private async Task SendCommandAsync(
        uint command,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] header = new byte[Ps5DebugProtocol.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, sizeof(uint)), Ps5DebugProtocol.PacketMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(sizeof(uint), sizeof(uint)), command);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(sizeof(uint) * 2, sizeof(uint)),
            checked((uint)body.Length));

        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        if (!body.IsEmpty)
        {
            await _stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }

        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        ReadOnlySpan<byte> textBytes = terminator >= 0 ? bytes[..terminator] : bytes;
        return Encoding.UTF8.GetString(textBytes);
    }

    private static (string Branding, string? CapabilityLevel) ParseBranding(byte[] payload)
    {
        int separator = Array.IndexOf(payload, (byte)0);
        if (separator < 0)
        {
            return (Encoding.UTF8.GetString(payload), null);
        }

        string branding = Encoding.UTF8.GetString(payload, 0, separator);
        string? capabilityLevel = separator + 1 < payload.Length
            ? Encoding.UTF8.GetString(payload, separator + 1, payload.Length - separator - 1)
            : null;

        return (branding, string.IsNullOrWhiteSpace(capabilityLevel) ? null : capabilityLevel);
    }

    private readonly record struct NativeScanProtocolShape(
        byte CompareType,
        int InputValueCount,
        int ComparisonDataLength,
        bool UseSnapshot);

    private readonly record struct TurboScanSegment(ulong Address, uint Length);

}
