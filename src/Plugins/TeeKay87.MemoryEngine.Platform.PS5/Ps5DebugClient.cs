using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

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
    private MemoryValueType? _turboScanValueType;
    private int _turboScanValueLength;
    private int _turboScanAlignment;

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

    public async Task<IReadOnlyList<ulong>> ScanValuesAsync(
        int processId,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(request);

        ValidateNativeScanRequest(processId, request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_turboScanSessionActive)
        {
            await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
        }

        List<TurboScanSegment> segments = BuildTurboScanSegments(memoryRegions, request.ValueData.Length, request.Alignment);
        if (segments.Count == 0)
        {
            return Array.Empty<ulong>();
        }

        await EnsureScanAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] commandBody = new byte[Ps5DebugProtocol.TurboScanStartBodySize];
        BinaryPrimitives.WriteInt32LittleEndian(commandBody.AsSpan(0, sizeof(int)), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(commandBody.AsSpan(4, sizeof(ulong)), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(commandBody.AsSpan(12, sizeof(uint)), 0);
        commandBody[16] = GetScanValueType(request.ValueType);
        commandBody[17] = Ps5DebugProtocol.ScanCompareTypeExactValue;
        commandBody[18] = checked((byte)request.Alignment);
        BinaryPrimitives.WriteUInt32LittleEndian(
            commandBody.AsSpan(19, sizeof(uint)),
            checked((uint)request.ValueData.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(
            commandBody.AsSpan(23, sizeof(uint)),
            Ps5DebugProtocol.TurboScanFlagServerResident | Ps5DebugProtocol.TurboScanFlagSegments);

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

            byte[] summaryBytes = new byte[Ps5DebugProtocol.TurboScanResidentSummarySize];
            await _stream.ReadExactlyAsync(summaryBytes, CancellationToken.None).ConfigureAwait(false);
            uint residentStored = BinaryPrimitives.ReadUInt32LittleEndian(summaryBytes.AsSpan(0, sizeof(uint)));
            ulong resultCount = BinaryPrimitives.ReadUInt64LittleEndian(summaryBytes.AsSpan(sizeof(uint), sizeof(ulong)));

            if (residentStored == 0)
            {
                byte[] sentinelBytes = new byte[sizeof(ulong)];
                await _stream.ReadExactlyAsync(sentinelBytes, CancellationToken.None).ConfigureAwait(false);
                ulong sentinel = BinaryPrimitives.ReadUInt64LittleEndian(sentinelBytes);
                if (sentinel != ulong.MaxValue)
                {
                    throw new InvalidDataException(
                        $"ps5debug-NG TurboScan fallback returned unexpected sentinel 0x{sentinel:X16}.");
                }

                await ReadSuccessStatusAsync("TurboScan completion", CancellationToken.None).ConfigureAwait(false);
                throw new NotSupportedException(
                    "ps5debug-NG could not keep the accelerated scan result set server-side; use the shared scanner fallback for this scan.");
            }

            if (residentStored != 1)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG TurboScan returned invalid resident state {residentStored}.");
            }

            residentSessionCreated = true;
            await ReadSuccessStatusAsync("TurboScan completion", CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            ValidateNativeResultCount(resultCount);
            IReadOnlyList<ulong> addresses = await FetchTurboScanAddressesAsync(
                    resultCount,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            _turboScanSessionActive = true;
            _turboScanProcessId = processId;
            _turboScanResultCount = resultCount;
            _turboScanValueType = request.ValueType;
            _turboScanValueLength = request.ValueData.Length;
            _turboScanAlignment = request.Alignment;
            keepResidentSession = true;
            return addresses;
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

    public async Task<IReadOnlyList<ulong>> RefineValuesAsync(
        int processId,
        IReadOnlyList<ulong> previousAddresses,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(previousAddresses);
        ArgumentNullException.ThrowIfNull(request);

        ValidateNativeScanRequest(processId, request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ValueType is MemoryValueType.Float32 or MemoryValueType.Float64)
        {
            await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException(
                "ps5debug-NG resident TurboScan refinement uses fuzzy floating-point comparison for Float and Double. " +
                "The shared scanner is required to preserve strict Exact Value semantics for these types.");
        }

        if (!_turboScanSessionActive || _turboScanProcessId != processId)
        {
            throw new NotSupportedException(
                "No compatible resident TurboScan session is available for native refinement.");
        }

        if (_turboScanValueType != request.ValueType ||
            _turboScanValueLength != request.ValueData.Length ||
            _turboScanAlignment != request.Alignment)
        {
            await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
            throw new NotSupportedException(
                "The requested Value Type or width does not match the resident TurboScan session. Start a New Scan before changing Value Type.");
        }

        if ((ulong)previousAddresses.Count != _turboScanResultCount)
        {
            await EndActiveTurboScanSessionAsync(CancellationToken.None).ConfigureAwait(false);
            throw new NotSupportedException(
                "The resident TurboScan survivor count no longer matches the host scan session; use the shared refinement fallback.");
        }

        if (_turboScanResultCount == 0)
        {
            return Array.Empty<ulong>();
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
        body[12] = GetScanValueType(request.ValueType);
        body[13] = Ps5DebugProtocol.ScanCompareTypeExactValue;
        BinaryPrimitives.WriteUInt32LittleEndian(
            body.AsSpan(14, sizeof(uint)),
            checked((uint)request.ValueData.Length));
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

            // Resident list sessions currently emit an immediate sentinel. The parser also
            // accepts progress records so it remains correct if the server materializes or
            // otherwise changes the resident representation in a future narrowing pass.
            int progressRecordCount = 0;
            while (true)
            {
                byte[] progressBytes = new byte[sizeof(ulong)];
                await _stream.ReadExactlyAsync(progressBytes, CancellationToken.None).ConfigureAwait(false);
                ulong progressValue = BinaryPrimitives.ReadUInt64LittleEndian(progressBytes);
                if (progressValue == ulong.MaxValue)
                {
                    break;
                }

                progressRecordCount++;
                if (progressRecordCount > 1024)
                {
                    throw new InvalidDataException(
                        "ps5debug-NG TurboScan refinement returned an unreasonable number of progress records.");
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

            ValidateNativeResultCount(resultCount);
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ulong> addresses = await FetchTurboScanAddressesAsync(
                    resultCount,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            _turboScanResultCount = resultCount;
            return addresses;
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

    private void ValidateNativeScanRequest(int processId, NativeValueScanRequest request)
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

        if (request.Comparison != ValueScanComparison.ExactValue)
        {
            throw new NotSupportedException(
                "The current ps5debug-NG accelerated scan path supports Exact Value scans only.");
        }

        int expectedSize = request.ValueType switch
        {
            MemoryValueType.UInt8 or MemoryValueType.Int8 => 1,
            MemoryValueType.UInt16 or MemoryValueType.Int16 => 2,
            MemoryValueType.UInt32 or MemoryValueType.Int32 or MemoryValueType.Float32 => 4,
            MemoryValueType.UInt64 or MemoryValueType.Int64 or MemoryValueType.Float64 => 8,
            MemoryValueType.ByteArray => request.ValueData.Length,
            _ => 0
        };

        if (expectedSize == 0 ||
            request.ValueData.Length != expectedSize ||
            request.ValueData.Length > Ps5DebugProtocol.TurboScanMaximumResidentValueLength)
        {
            throw new NotSupportedException(
                "The selected Value Type or value width is not supported by the ps5debug-NG resident TurboScan path.");
        }

        int expectedAlignment = request.ValueType == MemoryValueType.ByteArray
            ? 1
            : expectedSize;
        if (request.Alignment != expectedAlignment)
        {
            throw new NotSupportedException(
                $"The selected Value Type requires an alignment of {expectedAlignment} byte(s) for the ps5debug-NG TurboScan path.");
        }
    }

    private static byte GetScanValueType(MemoryValueType valueType)
    {
        return valueType switch
        {
            MemoryValueType.UInt8 => Ps5DebugProtocol.ScanValueTypeUInt8,
            MemoryValueType.Int8 => Ps5DebugProtocol.ScanValueTypeInt8,
            MemoryValueType.UInt16 => Ps5DebugProtocol.ScanValueTypeUInt16,
            MemoryValueType.Int16 => Ps5DebugProtocol.ScanValueTypeInt16,
            MemoryValueType.UInt32 => Ps5DebugProtocol.ScanValueTypeUInt32,
            MemoryValueType.Int32 => Ps5DebugProtocol.ScanValueTypeInt32,
            MemoryValueType.UInt64 => Ps5DebugProtocol.ScanValueTypeUInt64,
            MemoryValueType.Int64 => Ps5DebugProtocol.ScanValueTypeInt64,
            MemoryValueType.Float32 => Ps5DebugProtocol.ScanValueTypeFloat32,
            MemoryValueType.Float64 => Ps5DebugProtocol.ScanValueTypeFloat64,
            MemoryValueType.ByteArray => Ps5DebugProtocol.ScanValueTypeByteArray,
            _ => throw new NotSupportedException(
                $"{valueType} is not a ps5debug-NG scan value type.")
        };
    }

    private async Task WriteScanComparisonDataAsync(
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        await _stream.WriteAsync(request.ValueData, cancellationToken).ConfigureAwait(false);

        if (request.ValueType == MemoryValueType.ByteArray)
        {
            byte[] mask = new byte[request.ValueData.Length];
            Array.Fill(mask, (byte)1);
            await _stream.WriteAsync(mask, cancellationToken).ConfigureAwait(false);
        }

        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateNativeResultCount(ulong resultCount)
    {
        if (resultCount > Ps5DebugProtocol.MaximumNativeScanResultCount)
        {
            throw new InvalidDataException(
                $"ps5debug-NG TurboScan returned {resultCount:N0} matches, exceeding the {Ps5DebugProtocol.MaximumNativeScanResultCount:N0} result safety limit.");
        }
    }

    private async Task<IReadOnlyList<ulong>> FetchTurboScanAddressesAsync(
        ulong resultCount,
        NativeValueScanRequest request,
        CancellationToken cancellationToken)
    {
        int valueLength = request.ValueData.Length;
        int maximumRecordSize = checked(sizeof(ulong) + (valueLength * 3));
        uint payloadLimitedBatchSize = checked((uint)Math.Max(
            1,
            Ps5DebugProtocol.TurboScanGetMaximumPayloadSize / maximumRecordSize));
        uint maximumBatchSize = Math.Min(
            checked((uint)Ps5DebugProtocol.TurboScanGetBatchSize),
            payloadLimitedBatchSize);

        List<ulong> addresses = new(checked((int)resultCount));
        ulong fetched = 0;

        while (fetched < resultCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint requestedCount = checked((uint)Math.Min(
                (ulong)maximumBatchSize,
                resultCount - fetched));

            byte[] getBody = new byte[Ps5DebugProtocol.TurboScanGetBodySize];
            BinaryPrimitives.WriteUInt32LittleEndian(getBody.AsSpan(0, sizeof(uint)), checked((uint)fetched));
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

            for (int index = 0; index < actualCount; index++)
            {
                ReadOnlySpan<byte> record = records.AsSpan(index * recordSize, recordSize);
                ulong address = BinaryPrimitives.ReadUInt64LittleEndian(record[..sizeof(ulong)]);
                ReadOnlySpan<byte> currentValue = record.Slice(sizeof(ulong), valueLength);
                if (!TurboScanValueMatches(currentValue, request))
                {
                    throw new InvalidDataException(
                        $"ps5debug-NG TurboScan returned a mismatched value for address 0x{address:X}.");
                }

                addresses.Add(address);
            }

            if (actualCount == 0)
            {
                throw new InvalidDataException(
                    "ps5debug-NG TurboScan GET returned zero records before the advertised result count was exhausted.");
            }

            fetched += actualCount;
        }

        return addresses;
    }

    private static bool TurboScanValueMatches(
        ReadOnlySpan<byte> currentValue,
        NativeValueScanRequest request)
    {
        if (currentValue.Length != request.ValueData.Length)
        {
            return false;
        }

        return request.ValueType switch
        {
            MemoryValueType.Float32 =>
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(currentValue)) ==
                BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(request.ValueData.Span)),
            MemoryValueType.Float64 =>
                BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(currentValue)) ==
                BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(request.ValueData.Span)),
            _ => currentValue.SequenceEqual(request.ValueData.Span)
        };
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
        _turboScanValueType = null;
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

    private readonly record struct TurboScanSegment(ulong Address, uint Length);

}
