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

namespace TeeKay87.MemoryEngine.Tests;

internal sealed class Ps5ProtocolTestServer : IAsyncDisposable
{
    private const uint PacketMagic = 0xFFAABBCC;
    private const uint CommandVersion = 0xBD000001;
    private const uint CommandFirmwareVersion = 0xBD000500;
    private const uint CommandBranding = 0xBD000501;
    private const uint CommandPlatformId = 0xBD000502;
    private const uint CommandProcessList = 0xBDAA0001;
    private const uint CommandProcessRead = 0xBDAA0002;
    private const uint CommandProcessWrite = 0xBDAA0003;
    private const uint CommandProcessMaps = 0xBDAA0004;
    private const uint CommandProcessAuth = 0xBDAACCFF;
    private const uint CommandTurboScanCaps = 0xBDAACC10;
    private const uint CommandTurboScanStart = 0xBDAACC11;
    private const uint CommandTurboScanCount = 0xBDAACC12;
    private const uint CommandTurboScanGet = 0xBDAACC13;
    private const uint CommandTurboScanEnd = 0xBDAACC14;
    private const uint CommandProcessNop = 0xBDAACC06;
    private const uint CommandDebugAttach = 0xBDBB0001;
    private const uint CommandDebugDetach = 0xBDBB0002;
    private const uint CommandDebugGetThreadList = 0xBDBB0005;
    private const uint CommandDebugSuspendThread = 0xBDBB0006;
    private const uint CommandDebugResumeThread = 0xBDBB0007;
    private const uint CommandDebugGetRegisters = 0xBDBB0008;
    private const uint CommandDebugContinue = 0xBDBB0010;
    private const uint CommandDebugThreadInfo = 0xBDBB0011;
    private const uint CommandDebugProcessStop = 0xBDBB0500;
    private const uint WireStatusSuccess = 0x80000000;
    private const uint WireStatusError = 0xF0000001;
    private const int ProcessNameLength = 32;
    private const int ProcessEntrySize = 36;
    private const int ProcessMapNameLength = 32;
    private const int ProcessMapEntrySize = 58;
    private const int DebuggerInterruptPort = 755;
    private const int DebuggerInterruptPacketSize = 1184;
    private const int DebuggerGeneralRegisterSize = 0xB0;
    private const int DebuggerInterruptInstructionPointerOffset = 0xB8;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly TcpListener _listener;
    private readonly Task _serverTask;
    private readonly bool _serveProcessList;
    private readonly bool _serveMemoryMap;
    private readonly bool _serveMemoryRead;
    private readonly bool _serveMemoryWrite;
    private readonly bool _serveNativeScan;
    private readonly bool _serveNativeRefinement;
    private readonly bool _serveProcessControl;
    private readonly bool _serveFollowUpProcessList;
    private readonly bool _serveDebugger;
    private readonly int _debuggerSessionCount;
    private readonly int _memoryReadDelayMilliseconds;
    private readonly int _nativeScanDelayMilliseconds;
    private readonly byte _nativeValueType;
    private readonly byte _nativeAlignment;
    private readonly byte _nativeStartCompareType;
    private readonly byte _nativeRefinementValueType;
    private readonly byte _nativeRefinementCompareType;
    private readonly bool _nativeStartSnapshot;
    private readonly int _nativeSnapshotProgressRecordCount;
    private readonly int _nativeRefinementProgressRecordCount;
    private readonly bool _nativeSnapshotStored;
    private readonly byte[] _nativeValueData;
    private readonly byte[] _nativeStartComparisonData;
    private readonly byte[] _nativeRefinementValueData;
    private readonly byte[] _nativeRefinementReturnedCurrentValueData;
    private readonly TaskCompletionSource<bool> _memoryReadRequestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _nativeScanRequestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _debuggerAttached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _debuggerEventGate = new();
    private readonly SemaphoreSlim _debuggerEventWriteGate = new(1, 1);
    private TcpClient? _debuggerEventClient;
    private NetworkStream? _debuggerEventStream;
    private readonly System.Collections.Generic.List<byte> _debuggerActions = new();
    private readonly System.Collections.Generic.List<string> _debuggerThreadActions = new();
    private readonly System.Collections.Generic.List<uint> _debuggerRegisterReadThreadIds = new();

    public Ps5ProtocolTestServer(
        bool serveProcessList = false,
        bool serveMemoryMap = false,
        bool serveMemoryRead = false,
        bool serveMemoryWrite = false,
        bool serveNativeScan = false,
        bool serveNativeRefinement = false,
        bool serveProcessControl = false,
        bool serveFollowUpProcessList = false,
        bool serveDebugger = false,
        int debuggerSessionCount = 1,
        int memoryReadDelayMilliseconds = 0,
        int nativeScanDelayMilliseconds = 0,
        byte nativeValueType = 5,
        byte nativeAlignment = 4,
        byte nativeStartCompareType = 0,
        byte? nativeRefinementValueType = null,
        byte nativeRefinementCompareType = 0,
        bool nativeStartSnapshot = false,
        int nativeSnapshotProgressRecordCount = 0,
        int nativeRefinementProgressRecordCount = 0,
        bool nativeSnapshotStored = true,
        byte[]? nativeValueData = null,
        byte[]? nativeStartComparisonData = null,
        byte[]? nativeRefinementValueData = null,
        byte[]? nativeRefinementReturnedCurrentValueData = null)
    {
        _serveProcessList = serveProcessList;
        _serveMemoryMap = serveMemoryMap;
        _serveMemoryRead = serveMemoryRead;
        _serveMemoryWrite = serveMemoryWrite;
        _serveNativeScan = serveNativeScan;
        _serveNativeRefinement = serveNativeRefinement;
        _serveProcessControl = serveProcessControl;
        _serveFollowUpProcessList = serveFollowUpProcessList;
        _serveDebugger = serveDebugger;
        if (debuggerSessionCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(debuggerSessionCount));
        }
        _debuggerSessionCount = debuggerSessionCount;
        _memoryReadDelayMilliseconds = memoryReadDelayMilliseconds;
        _nativeScanDelayMilliseconds = nativeScanDelayMilliseconds;
        _nativeValueType = nativeValueType;
        _nativeAlignment = nativeAlignment;
        _nativeStartCompareType = nativeStartCompareType;
        _nativeRefinementValueType = nativeRefinementValueType ?? nativeValueType;
        _nativeRefinementCompareType = nativeRefinementCompareType;
        _nativeStartSnapshot = nativeStartSnapshot;
        if (nativeSnapshotProgressRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeSnapshotProgressRecordCount));
        }

        _nativeSnapshotProgressRecordCount = nativeSnapshotProgressRecordCount;
        if (nativeRefinementProgressRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeRefinementProgressRecordCount));
        }

        _nativeRefinementProgressRecordCount = nativeRefinementProgressRecordCount;
        _nativeSnapshotStored = nativeSnapshotStored;
        _nativeValueData = nativeValueData?.ToArray() ?? BitConverter.GetBytes(10002);
        _nativeStartComparisonData = nativeStartComparisonData?.ToArray() ?? _nativeValueData.ToArray();
        _nativeRefinementValueData = nativeRefinementValueData?.ToArray() ?? BitConverter.GetBytes(10001);
        _nativeRefinementReturnedCurrentValueData =
            nativeRefinementReturnedCurrentValueData?.ToArray() ??
            _nativeRefinementValueData.ToArray();
        if (_serveNativeRefinement &&
            _nativeRefinementReturnedCurrentValueData.Length != _nativeValueData.Length)
        {
            throw new ArgumentException(
                "Native refinement GET test data must use the same byte width as the First Scan value.",
                nameof(nativeRefinementReturnedCurrentValueData));
        }

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverTask = RunAsync(_cancellation.Token);
    }

    public int Port { get; }

    public Task Completion => _serverTask;

    public Task MemoryReadRequestReceived => _memoryReadRequestReceived.Task;

    public Task NativeScanRequestReceived => _nativeScanRequestReceived.Task;

    public Task DebuggerAttached => _debuggerAttached.Task;

    public IReadOnlyList<byte> DebuggerActions
    {
        get
        {
            lock (_debuggerEventGate)
            {
                return _debuggerActions.ToArray();
            }
        }
    }

    public IReadOnlyList<string> DebuggerThreadActions
    {
        get
        {
            lock (_debuggerEventGate)
            {
                return _debuggerThreadActions.ToArray();
            }
        }
    }

    public IReadOnlyList<uint> DebuggerRegisterReadThreadIds
    {
        get
        {
            lock (_debuggerEventGate)
            {
                return _debuggerRegisterReadThreadIds.ToArray();
            }
        }
    }

    public async Task SendDebuggerInterruptAsync(
        uint threadId,
        uint waitStatus,
        ulong instructionPointer,
        string threadName = "main")
    {
        await _debuggerAttached.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        byte[] packet = new byte[DebuggerInterruptPacketSize];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0x000, sizeof(uint)), threadId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0x004, sizeof(uint)), waitStatus);
        byte[] nameBytes = Encoding.UTF8.GetBytes(threadName ?? string.Empty);
        nameBytes.AsSpan(0, Math.Min(nameBytes.Length, 39)).CopyTo(packet.AsSpan(0x008, 40));
        BinaryPrimitives.WriteUInt64LittleEndian(
            packet.AsSpan(DebuggerInterruptInstructionPointerOffset, sizeof(ulong)),
            instructionPointer);

        await _debuggerEventWriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
            NetworkStream stream;
            lock (_debuggerEventGate)
            {
                stream = _debuggerEventStream ??
                    throw new InvalidOperationException("The test debugger event channel is not connected.");
            }

            await stream.WriteAsync(packet).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _debuggerEventWriteGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        lock (_debuggerEventGate)
        {
            _debuggerEventStream?.Dispose();
            _debuggerEventClient?.Dispose();
            _debuggerEventStream = null;
            _debuggerEventClient = null;
        }
        _listener.Stop();

        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_cancellation.IsCancellationRequested)
        {
        }

        _debuggerEventWriteGate.Dispose();
        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        using NetworkStream stream = client.GetStream();

        await ExpectCommandAsync(stream, CommandVersion, cancellationToken).ConfigureAwait(false);
        await WriteLengthPrefixedAsync(stream, Encoding.UTF8.GetBytes("1.3"), cancellationToken).ConfigureAwait(false);

        await ExpectCommandAsync(stream, CommandPlatformId, cancellationToken).ConfigureAwait(false);
        await WriteUInt16Async(stream, 5, cancellationToken).ConfigureAwait(false);

        await ExpectCommandAsync(stream, CommandBranding, cancellationToken).ConfigureAwait(false);
        byte[] brand = Encoding.UTF8.GetBytes("ps5debug-NG by OSR v1.3.0\0" + "1.0");
        await WriteLengthPrefixedAsync(stream, brand, cancellationToken).ConfigureAwait(false);

        await ExpectCommandAsync(stream, CommandFirmwareVersion, cancellationToken).ConfigureAwait(false);
        await WriteUInt16Async(stream, 1240, cancellationToken).ConfigureAwait(false);

        await ExpectCommandAsync(stream, CommandProcessNop, cancellationToken).ConfigureAwait(false);
        await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

        await ExpectCommandAsync(stream, CommandTurboScanCaps, cancellationToken).ConfigureAwait(false);
        if (_serveNativeScan)
        {
            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            byte[] caps = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(0, sizeof(uint)), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(4, sizeof(uint)), 0x00000004u | 0x00000008u | 0x00000010u);
            BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(8, sizeof(uint)), 4);
            await stream.WriteAsync(caps, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteUInt32Async(stream, WireStatusError, cancellationToken).ConfigureAwait(false);
        }

        Task? debuggerTask = _serveDebugger
            ? RunDebuggerAsync(cancellationToken)
            : null;

        if (_serveProcessList)
        {
            await ExpectCommandAsync(stream, CommandProcessList, cancellationToken).ConfigureAwait(false);
            await WriteProcessListAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        if (_serveMemoryMap)
        {
            byte[] request = await ExpectCommandBodyAsync(
                    stream,
                    CommandProcessMaps,
                    sizeof(int),
                    cancellationToken)
                .ConfigureAwait(false);
            int processId = BinaryPrimitives.ReadInt32LittleEndian(request);
            if (processId != 2222)
            {
                throw new InvalidDataException($"Unexpected memory-map PID {processId}; expected 2222.");
            }

            await WriteMemoryMapAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        ulong? writtenAddress = null;
        byte[]? writtenPayload = null;

        if (_serveMemoryWrite)
        {
            byte[] request = await ExpectCommandBodyAsync(
                    stream,
                    CommandProcessWrite,
                    16,
                    cancellationToken)
                .ConfigureAwait(false);

            int processId = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan(0, sizeof(int)));
            ulong address = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(sizeof(int), sizeof(ulong)));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(
                request.AsSpan(sizeof(int) + sizeof(ulong), sizeof(uint)));

            if (processId != 2222)
            {
                throw new InvalidDataException($"Unexpected memory-write PID {processId}; expected 2222.");
            }

            if (length is 0 or > 4096)
            {
                throw new InvalidDataException($"Test memory-write length {length} is outside the fixture range.");
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

            writtenAddress = address;
            writtenPayload = new byte[checked((int)length)];
            await stream.ReadExactlyAsync(writtenPayload, cancellationToken).ConfigureAwait(false);

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
        }

        if (_serveMemoryRead)
        {
            byte[] request = await ExpectCommandBodyAsync(
                    stream,
                    CommandProcessRead,
                    16,
                    cancellationToken)
                .ConfigureAwait(false);

            int processId = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan(0, sizeof(int)));
            ulong address = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(sizeof(int), sizeof(ulong)));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(
                request.AsSpan(sizeof(int) + sizeof(ulong), sizeof(uint)));

            if (processId != 2222)
            {
                throw new InvalidDataException($"Unexpected memory-read PID {processId}; expected 2222.");
            }

            if (length > 1024 * 1024)
            {
                throw new InvalidDataException($"Test memory-read length {length} exceeds the fixture limit.");
            }

            _memoryReadRequestReceived.TrySetResult(true);

            if (_memoryReadDelayMilliseconds > 0)
            {
                await Task.Delay(_memoryReadDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

            byte[] payload;
            if (writtenAddress == address &&
                writtenPayload is not null &&
                writtenPayload.Length == checked((int)length))
            {
                payload = writtenPayload;
            }
            else
            {
                payload = new byte[checked((int)length)];
                for (int index = 0; index < payload.Length; index++)
                {
                    payload[index] = checked((byte)((address + (ulong)index) & 0xFF));
                }
            }

            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        if (_serveNativeScan)
        {
            byte[] auth = await ExpectCommandBodyAsync(
                    stream,
                    CommandProcessAuth,
                    8,
                    cancellationToken)
                .ConfigureAwait(false);
            uint authMagic = BinaryPrimitives.ReadUInt32LittleEndian(auth.AsSpan(0, sizeof(uint)));
            uint authFlags = BinaryPrimitives.ReadUInt32LittleEndian(auth.AsSpan(4, sizeof(uint)));
            if (authMagic != 0xBB40E64Du || (authFlags & 2u) == 0)
            {
                throw new InvalidDataException("Unexpected ps5debug-NG scan authorization request.");
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            const ushort challengeLength = 64;
            await WriteUInt16Async(stream, challengeLength, cancellationToken).ConfigureAwait(false);
            byte[] challenge = new byte[challengeLength];
            for (int index = 0; index < challenge.Length; index++)
            {
                challenge[index] = checked((byte)(index * 3 + 7));
            }
            await stream.WriteAsync(challenge, cancellationToken).ConfigureAwait(false);

            byte[] authResponse = new byte[challengeLength];
            await stream.ReadExactlyAsync(authResponse, cancellationToken).ConfigureAwait(false);
            byte[] keystream = BuildAuthKeystream(challengeLength);
            for (int index = 0; index < challenge.Length; index++)
            {
                if (authResponse[index] != (byte)(challenge[index] ^ keystream[index]))
                {
                    throw new InvalidDataException("Unexpected ps5debug-NG scan authorization response.");
                }
            }
            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

            byte[] request = await ExpectCommandBodyAsync(
                    stream,
                    CommandTurboScanStart,
                    27,
                    cancellationToken)
                .ConfigureAwait(false);

            int processId = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan(0, sizeof(int)));
            byte valueType = request[16];
            byte compareType = request[17];
            byte alignment = request[18];
            uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(19, sizeof(uint)));
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(23, sizeof(uint)));

            uint expectedStartFlags = _nativeStartSnapshot
                ? 0x00000004u | 0x00000008u | 0x00000010u
                : 0x00000002u | 0x00000010u;
            if (processId != 2222 || valueType != _nativeValueType || compareType != _nativeStartCompareType || alignment != _nativeAlignment ||
                dataLength != _nativeStartComparisonData.Length || flags != expectedStartFlags)
            {
                throw new InvalidDataException(
                    $"Unexpected TurboScan START shape: pid={processId}, valueType={valueType}, compareType={compareType}, alignment={alignment}, dataLength={dataLength}, flags=0x{flags:X8}.");
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            byte[] comparisonValue = new byte[_nativeStartComparisonData.Length];
            await stream.ReadExactlyAsync(comparisonValue, cancellationToken).ConfigureAwait(false);
            if (!comparisonValue.SequenceEqual(_nativeStartComparisonData))
            {
                throw new InvalidDataException("Unexpected TurboScan comparison value bytes.");
            }

            if (_nativeValueType == 10)
            {
                byte[] mask = new byte[_nativeValueData.Length];
                await stream.ReadExactlyAsync(mask, cancellationToken).ConfigureAwait(false);
                if (mask.Any(item => item != 1))
                {
                    throw new InvalidDataException("Unexpected TurboScan Array of Bytes mask.");
                }
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

            byte[] segmentCountBytes = new byte[sizeof(uint)];
            await stream.ReadExactlyAsync(segmentCountBytes, cancellationToken).ConfigureAwait(false);
            uint segmentCount = BinaryPrimitives.ReadUInt32LittleEndian(segmentCountBytes);
            if (segmentCount != 3)
            {
                throw new InvalidDataException($"Unexpected TurboScan segment count {segmentCount}; expected 3.");
            }

            byte[] segments = new byte[checked((int)segmentCount * 12)];
            await stream.ReadExactlyAsync(segments, cancellationToken).ConfigureAwait(false);
            ValidateTurboSegment(segments.AsSpan(0, 12), 0x0000000100000000, 0x00010000);
            ValidateTurboSegment(segments.AsSpan(12, 12), 0x0000000200000000, 0x00020000);
            ValidateTurboSegment(segments.AsSpan(24, 12), 0x0000000300000000, 0x00001000);

            _nativeScanRequestReceived.TrySetResult(true);
            if (_nativeScanDelayMilliseconds > 0)
            {
                await Task.Delay(_nativeScanDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            if (_nativeStartSnapshot)
            {
                byte[] plan = new byte[16];
                ulong slotCount = checked((ulong)Math.Max(2, _nativeSnapshotProgressRecordCount));
                BinaryPrimitives.WriteUInt64LittleEndian(plan.AsSpan(0, sizeof(ulong)), slotCount);
                BinaryPrimitives.WriteUInt64LittleEndian(plan.AsSpan(sizeof(ulong), sizeof(ulong)), 0x00031000);
                await stream.WriteAsync(plan, cancellationToken).ConfigureAwait(false);

                for (int index = 0; index < _nativeSnapshotProgressRecordCount; index++)
                {
                    await WriteUInt64Async(stream, checked((ulong)index + 1), cancellationToken).ConfigureAwait(false);
                }

                await WriteUInt64Async(stream, ulong.MaxValue, cancellationToken).ConfigureAwait(false);

                byte[] summary = new byte[12];
                BinaryPrimitives.WriteUInt32LittleEndian(
                    summary.AsSpan(0, sizeof(uint)),
                    _nativeSnapshotStored ? 1u : 0u);
                BinaryPrimitives.WriteUInt64LittleEndian(
                    summary.AsSpan(4, sizeof(ulong)),
                    _nativeSnapshotStored ? 2ul : 0ul);
                await stream.WriteAsync(summary, cancellationToken).ConfigureAwait(false);
                await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                byte[] summary = new byte[12];
                BinaryPrimitives.WriteUInt32LittleEndian(summary.AsSpan(0, sizeof(uint)), 1);
                BinaryPrimitives.WriteUInt64LittleEndian(summary.AsSpan(4, sizeof(ulong)), 2);
                await stream.WriteAsync(summary, cancellationToken).ConfigureAwait(false);
                await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            }

            bool nativeSessionCreated = !_nativeStartSnapshot || _nativeSnapshotStored;
            if (_nativeScanDelayMilliseconds == 0 && nativeSessionCreated)
            {
                byte[] get = await ExpectCommandBodyAsync(
                        stream,
                        CommandTurboScanGet,
                        12,
                        cancellationToken)
                    .ConfigureAwait(false);
                uint startIndex = BinaryPrimitives.ReadUInt32LittleEndian(get.AsSpan(0, sizeof(uint)));
                uint requestedCount = BinaryPrimitives.ReadUInt32LittleEndian(get.AsSpan(4, sizeof(uint)));
                if (startIndex != 0 || requestedCount != 2)
                {
                    throw new InvalidDataException(
                        $"Unexpected TurboScan GET range: start={startIndex}, count={requestedCount}.");
                }

                await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                await WriteUInt32Async(stream, 2, cancellationToken).ConfigureAwait(false);
                await WriteTurboGetRecordAsync(stream, 0x0000000200000040, _nativeValueData, _nativeValueData, cancellationToken).ConfigureAwait(false);
                await WriteTurboGetRecordAsync(stream, 0x0000000200000080, _nativeValueData, _nativeValueData, cancellationToken).ConfigureAwait(false);
                await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

                if (_serveNativeRefinement)
                {
                    byte[] countRequest = await ExpectCommandBodyAsync(
                            stream,
                            CommandTurboScanCount,
                            22,
                            cancellationToken)
                        .ConfigureAwait(false);

                    int countPid = BinaryPrimitives.ReadInt32LittleEndian(countRequest.AsSpan(0, sizeof(int)));
                    ulong baseAddress = BinaryPrimitives.ReadUInt64LittleEndian(countRequest.AsSpan(4, sizeof(ulong)));
                    byte countValueType = countRequest[12];
                    byte countCompareType = countRequest[13];
                    uint countDataLength = BinaryPrimitives.ReadUInt32LittleEndian(countRequest.AsSpan(14, sizeof(uint)));
                    uint countFlags = BinaryPrimitives.ReadUInt32LittleEndian(countRequest.AsSpan(18, sizeof(uint)));
                    if (countPid != 2222 || baseAddress != 0 || countValueType != _nativeRefinementValueType || countCompareType != _nativeRefinementCompareType ||
                        countDataLength != _nativeRefinementValueData.Length || countFlags != 0x00000002u)
                    {
                        throw new InvalidDataException(
                            $"Unexpected TurboScan COUNT shape: pid={countPid}, base=0x{baseAddress:X}, valueType={countValueType}, compareType={countCompareType}, dataLength={countDataLength}, flags=0x{countFlags:X8}.");
                    }

                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    byte[] nextValueBytes = new byte[_nativeRefinementValueData.Length];
                    await stream.ReadExactlyAsync(nextValueBytes, cancellationToken).ConfigureAwait(false);
                    if (!nextValueBytes.SequenceEqual(_nativeRefinementValueData))
                    {
                        throw new InvalidDataException("Unexpected TurboScan refinement value bytes.");
                    }

                    for (int index = 0; index < _nativeRefinementProgressRecordCount; index++)
                    {
                        await WriteUInt64Async(stream, checked((ulong)index + 1), cancellationToken).ConfigureAwait(false);
                    }

                    await WriteUInt64Async(stream, ulong.MaxValue, cancellationToken).ConfigureAwait(false);
                    await WriteUInt64Async(stream, 1, cancellationToken).ConfigureAwait(false);
                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

                    byte[] refinedGet = await ExpectCommandBodyAsync(
                            stream,
                            CommandTurboScanGet,
                            12,
                            cancellationToken)
                        .ConfigureAwait(false);
                    uint refinedStartIndex = BinaryPrimitives.ReadUInt32LittleEndian(refinedGet.AsSpan(0, sizeof(uint)));
                    uint refinedRequestedCount = BinaryPrimitives.ReadUInt32LittleEndian(refinedGet.AsSpan(4, sizeof(uint)));
                    if (refinedStartIndex != 0 || refinedRequestedCount != 1)
                    {
                        throw new InvalidDataException(
                            $"Unexpected refined TurboScan GET range: start={refinedStartIndex}, count={refinedRequestedCount}.");
                    }

                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    await WriteUInt32Async(stream, 1, cancellationToken).ConfigureAwait(false);
                    await WriteTurboGetRecordAsync(
                            stream,
                            0x0000000200000080,
                            _nativeRefinementReturnedCurrentValueData,
                            _nativeValueData,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                }
            }

            if (nativeSessionCreated)
            {
                await ExpectCommandAsync(stream, CommandTurboScanEnd, cancellationToken).ConfigureAwait(false);
                await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            }
        }

        if (_serveProcessControl)
        {
            await ExpectProcessStateAsync(stream, expectedState: 1, cancellationToken).ConfigureAwait(false);
            await ExpectProcessStateAsync(stream, expectedState: 0, cancellationToken).ConfigureAwait(false);
        }

        if (_serveFollowUpProcessList)
        {
            await ExpectCommandAsync(stream, CommandProcessList, cancellationToken).ConfigureAwait(false);
            await WriteProcessListAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        if (debuggerTask is not null)
        {
            await debuggerTask.ConfigureAwait(false);
        }
    }

    private async Task RunDebuggerAsync(CancellationToken cancellationToken)
    {
        for (int sessionIndex = 0; sessionIndex < _debuggerSessionCount; sessionIndex++)
        {
            using TcpClient commandClient = await _listener
                .AcceptTcpClientAsync(cancellationToken)
                .ConfigureAwait(false);
            using NetworkStream commandStream = commandClient.GetStream();

            byte[] attach = await ExpectCommandBodyAsync(
                    commandStream,
                    CommandDebugAttach,
                    sizeof(int),
                    cancellationToken)
                .ConfigureAwait(false);
            int processId = BinaryPrimitives.ReadInt32LittleEndian(attach);
            if (processId != 2222)
            {
                throw new InvalidDataException($"Unexpected debugger attach PID {processId}; expected 2222.");
            }

            TcpClient eventClient = new();
            eventClient.NoDelay = true;
            await eventClient.ConnectAsync(IPAddress.Loopback, DebuggerInterruptPort, cancellationToken).ConfigureAwait(false);
            NetworkStream eventStream = eventClient.GetStream();
            lock (_debuggerEventGate)
            {
                _debuggerEventClient = eventClient;
                _debuggerEventStream = eventStream;
            }

            await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            _debuggerAttached.TrySetResult(true);

            bool detached = false;
            while (!detached && !cancellationToken.IsCancellationRequested)
            {
                byte[] header = new byte[12];
                await commandStream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, sizeof(uint)));
                uint command = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, sizeof(uint)));
                uint bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, sizeof(uint)));
                if (magic != PacketMagic)
                {
                    throw new InvalidDataException($"Unexpected debugger packet magic 0x{magic:X8}.");
                }


                if (command == CommandDebugGetThreadList)
                {
                    if (bodyLength != 0)
                    {
                        throw new InvalidDataException($"Debugger thread-list sent {bodyLength} bytes; expected 0.");
                    }

                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    uint[] threadIds = { 0x101, 0x102, 0x103 };
                    await WriteUInt32Async(commandStream, checked((uint)threadIds.Length), cancellationToken).ConfigureAwait(false);
                    byte[] payload = new byte[threadIds.Length * sizeof(uint)];
                    for (int index = 0; index < threadIds.Length; index++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(
                            payload.AsSpan(index * sizeof(uint), sizeof(uint)),
                            threadIds[index]);
                    }
                    await commandStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (command == CommandDebugThreadInfo)
                {
                    if (bodyLength != sizeof(uint))
                    {
                        throw new InvalidDataException($"Debugger thread-info sent {bodyLength} bytes; expected 4.");
                    }

                    byte[] body = new byte[sizeof(uint)];
                    await commandStream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                    uint threadId = BinaryPrimitives.ReadUInt32LittleEndian(body);
                    string name = threadId switch
                    {
                        0x101 => "MainThread",
                        0x102 => "Worker",
                        0x103 => "Render",
                        _ => string.Empty
                    };

                    if (name.Length == 0)
                    {
                        await WriteUInt32Async(commandStream, WireStatusError, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    byte[] response = new byte[40];
                    BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(0, sizeof(uint)), threadId);
                    BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(sizeof(uint), sizeof(uint)), 10 + threadId);
                    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                    nameBytes.AsSpan(0, Math.Min(nameBytes.Length, 31)).CopyTo(response.AsSpan(8, 32));
                    await commandStream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (command == CommandDebugGetRegisters)
                {
                    if (bodyLength != sizeof(uint))
                    {
                        throw new InvalidDataException($"Debugger register read sent {bodyLength} bytes; expected 4.");
                    }

                    byte[] body = new byte[sizeof(uint)];
                    await commandStream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                    uint threadId = BinaryPrimitives.ReadUInt32LittleEndian(body);
                    if (threadId is not (0x101 or 0x102 or 0x103))
                    {
                        await WriteUInt32Async(commandStream, WireStatusError, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    lock (_debuggerEventGate)
                    {
                        _debuggerRegisterReadThreadIds.Add(threadId);
                    }

                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    await commandStream.WriteAsync(BuildDebuggerRegisterBlob(threadId), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (command == CommandDebugSuspendThread || command == CommandDebugResumeThread)
                {
                    if (bodyLength != sizeof(uint))
                    {
                        throw new InvalidDataException($"Debugger thread-control sent {bodyLength} bytes; expected 4.");
                    }

                    byte[] body = new byte[sizeof(uint)];
                    await commandStream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                    uint threadId = BinaryPrimitives.ReadUInt32LittleEndian(body);
                    if (threadId is not (0x101 or 0x102 or 0x103))
                    {
                        throw new InvalidDataException($"Unexpected debugger thread id 0x{threadId:X}.");
                    }

                    lock (_debuggerEventGate)
                    {
                        _debuggerThreadActions.Add(
                            $"{(command == CommandDebugSuspendThread ? "suspend" : "resume")}:0x{threadId:X}");
                    }
                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (command == CommandDebugContinue)
                {
                    if (bodyLength != 4)
                    {
                        throw new InvalidDataException($"Debugger continue sent {bodyLength} bytes; expected 4.");
                    }

                    byte[] body = new byte[4];
                    await commandStream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                    lock (_debuggerEventGate)
                    {
                        _debuggerActions.Add(body[0]);
                    }
                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (command == CommandDebugDetach)
                {
                    if (bodyLength != 0)
                    {
                        throw new InvalidDataException($"Debugger detach sent {bodyLength} bytes; expected 0.");
                    }

                    await WriteUInt32Async(commandStream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    detached = true;
                    continue;
                }

                throw new InvalidDataException($"Unexpected debugger command 0x{command:X8}.");
            }

            lock (_debuggerEventGate)
            {
                _debuggerEventStream?.Dispose();
                _debuggerEventClient?.Dispose();
                _debuggerEventStream = null;
                _debuggerEventClient = null;
            }
        }
    }

    private static byte[] BuildDebuggerRegisterBlob(uint threadId)
    {
        byte[] registers = new byte[DebuggerGeneralRegisterSize];
        ulong seed = 0x1000000000000000UL + threadId;

        static void Write64(byte[] target, int offset, ulong value) =>
            BinaryPrimitives.WriteUInt64LittleEndian(target.AsSpan(offset, sizeof(ulong)), value);
        static void Write32(byte[] target, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset, sizeof(uint)), value);
        static void Write16(byte[] target, int offset, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset, sizeof(ushort)), value);

        Write64(registers, 0x00, seed + 0x0F); // r15
        Write64(registers, 0x08, seed + 0x0E); // r14
        Write64(registers, 0x10, seed + 0x0D); // r13
        Write64(registers, 0x18, seed + 0x0C); // r12
        Write64(registers, 0x20, seed + 0x0B); // r11
        Write64(registers, 0x28, seed + 0x0A); // r10
        Write64(registers, 0x30, seed + 0x09); // r9
        Write64(registers, 0x38, seed + 0x08); // r8
        Write64(registers, 0x40, seed + 0x06); // rdi
        Write64(registers, 0x48, seed + 0x05); // rsi
        Write64(registers, 0x50, 0x7000000000000100UL + threadId); // rbp
        Write64(registers, 0x58, seed + 0x02); // rbx
        Write64(registers, 0x60, seed + 0x04); // rdx
        Write64(registers, 0x68, seed + 0x03); // rcx
        Write64(registers, 0x70, seed + 0x01); // rax
        Write32(registers, 0x78, 0x12);
        Write16(registers, 0x7C, 0x20);
        Write16(registers, 0x7E, 0x30);
        Write32(registers, 0x80, 0x34);
        Write16(registers, 0x84, 0x40);
        Write16(registers, 0x86, 0x50);
        Write64(registers, 0x88, 0x0000000000420000UL + threadId);
        Write64(registers, 0x90, 0x33);
        Write64(registers, 0x98, 0x202);
        Write64(registers, 0xA0, 0x7000000000000000UL + threadId);
        Write64(registers, 0xA8, 0x2B);
        return registers;
    }

    private static void ValidateTurboSegment(ReadOnlySpan<byte> segment, ulong expectedAddress, uint expectedLength)
    {
        ulong address = BinaryPrimitives.ReadUInt64LittleEndian(segment[..sizeof(ulong)]);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(segment.Slice(sizeof(ulong), sizeof(uint)));
        if (address != expectedAddress || length != expectedLength)
        {
            throw new InvalidDataException(
                $"Unexpected TurboScan segment 0x{address:X}+0x{length:X}; expected 0x{expectedAddress:X}+0x{expectedLength:X}.");
        }
    }

    private static async Task WriteTurboGetRecordAsync(
        NetworkStream stream,
        ulong address,
        ReadOnlyMemory<byte> currentValue,
        ReadOnlyMemory<byte> previousValue,
        CancellationToken cancellationToken)
    {
        if (currentValue.Length != previousValue.Length)
        {
            throw new ArgumentException("TurboScan current/previous test values must have the same width.");
        }

        byte[] record = new byte[checked(sizeof(ulong) + currentValue.Length + previousValue.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(0, sizeof(ulong)), address);
        currentValue.Span.CopyTo(record.AsSpan(sizeof(ulong), currentValue.Length));
        previousValue.Span.CopyTo(record.AsSpan(sizeof(ulong) + currentValue.Length, previousValue.Length));
        await stream.WriteAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildAuthKeystream(int length)
    {
        uint s1 = 200, s2 = 300, s3 = 400, s4 = 500;
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

    private static async Task ExpectProcessStateAsync(
        NetworkStream stream,
        byte expectedState,
        CancellationToken cancellationToken)
    {
        byte[] request = await ExpectCommandBodyAsync(
                stream,
                CommandDebugProcessStop,
                5,
                cancellationToken)
            .ConfigureAwait(false);

        int processId = BinaryPrimitives.ReadInt32LittleEndian(request.AsSpan(0, sizeof(int)));
        byte state = request[4];

        if (processId != 2222 || state != expectedState)
        {
            throw new InvalidDataException(
                $"Unexpected process-control request: pid={processId}, state={state}; expected pid=2222, state={expectedState}.");
        }

        await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExpectCommandAsync(
        NetworkStream stream,
        uint expectedCommand,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[12];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, sizeof(uint)));
        uint command = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(uint), sizeof(uint)));
        uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(uint) * 2, sizeof(uint)));

        if (magic != PacketMagic)
        {
            throw new InvalidDataException($"Unexpected packet magic 0x{magic:X8}.");
        }

        if (command != expectedCommand)
        {
            throw new InvalidDataException(
                $"Unexpected command 0x{command:X8}; expected 0x{expectedCommand:X8}.");
        }

        if (dataLength != 0)
        {
            throw new InvalidDataException($"Command 0x{command:X8} unexpectedly sent {dataLength} data bytes.");
        }
    }

    private static async Task<byte[]> ExpectCommandBodyAsync(
        NetworkStream stream,
        uint expectedCommand,
        int expectedBodyLength,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[12];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, sizeof(uint)));
        uint command = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(uint), sizeof(uint)));
        uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(uint) * 2, sizeof(uint)));

        if (magic != PacketMagic)
        {
            throw new InvalidDataException($"Unexpected packet magic 0x{magic:X8}.");
        }

        if (command != expectedCommand)
        {
            throw new InvalidDataException(
                $"Unexpected command 0x{command:X8}; expected 0x{expectedCommand:X8}.");
        }

        if (expectedBodyLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedBodyLength));
        }

        if (dataLength != checked((uint)expectedBodyLength))
        {
            throw new InvalidDataException(
                $"Command 0x{command:X8} sent {dataLength} data bytes; expected {expectedBodyLength}.");
        }

        byte[] body = new byte[expectedBodyLength];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        return body;
    }

    private static async Task WriteProcessListAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
        await WriteUInt32Async(stream, 3, cancellationToken).ConfigureAwait(false);
        await WriteProcessEntryAsync(stream, "SceShellCore", 101, cancellationToken).ConfigureAwait(false);
        await WriteProcessEntryAsync(stream, "eboot.bin", 2222, cancellationToken).ConfigureAwait(false);
        await WriteProcessEntryAsync(stream, "WebProcess", 3333, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteMemoryMapAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
        await WriteUInt32Async(stream, 3, cancellationToken).ConfigureAwait(false);
        await WriteMemoryMapEntryAsync(
                stream,
                "eboot.bin",
                0x0000000100000000,
                0x0000000100010000,
                0,
                0x0005,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteMemoryMapEntryAsync(
                stream,
                "data",
                0x0000000200000000,
                0x0000000200020000,
                0x10000,
                0x0003,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteMemoryMapEntryAsync(
                stream,
                string.Empty,
                0x0000000300000000,
                0x0000000300001000,
                0,
                0x0001,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteMemoryMapEntryAsync(
        NetworkStream stream,
        string name,
        ulong start,
        ulong end,
        ulong offset,
        ushort protection,
        CancellationToken cancellationToken)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length > ProcessMapNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Test memory-map names must fit the ps5debug-NG map field.");
        }

        byte[] entry = new byte[ProcessMapEntrySize];
        nameBytes.CopyTo(entry, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.AsSpan(32, sizeof(ulong)), start);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.AsSpan(40, sizeof(ulong)), end);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.AsSpan(48, sizeof(ulong)), offset);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(56, sizeof(ushort)), protection);
        await stream.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteProcessEntryAsync(
        NetworkStream stream,
        string name,
        int processId,
        CancellationToken cancellationToken)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Length > ProcessNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Test process names must fit the ps5debug-NG process-list field.");
        }

        byte[] entry = new byte[ProcessEntrySize];
        nameBytes.CopyTo(entry, 0);
        BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(ProcessNameLength, sizeof(int)), processId);
        await stream.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteLengthPrefixedAsync(
        NetworkStream stream,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        await WriteUInt32Async(stream, checked((uint)payload.Length), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteUInt16Async(
        NetworkStream stream,
        ushort value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteUInt64Async(
        NetworkStream stream,
        ulong value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteUInt32Async(
        NetworkStream stream,
        uint value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
