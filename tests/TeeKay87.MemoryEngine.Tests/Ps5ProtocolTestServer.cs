using System;
using System.Buffers.Binary;
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
    private const uint CommandDebugProcessStop = 0xBDBB0500;
    private const uint WireStatusSuccess = 0x80000000;
    private const uint WireStatusError = 0xF0000001;
    private const int ProcessNameLength = 32;
    private const int ProcessEntrySize = 36;
    private const int ProcessMapNameLength = 32;
    private const int ProcessMapEntrySize = 58;

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
    private readonly int _memoryReadDelayMilliseconds;
    private readonly int _nativeScanDelayMilliseconds;
    private readonly byte _nativeValueType;
    private readonly byte _nativeAlignment;
    private readonly byte[] _nativeValueData;
    private readonly TaskCompletionSource<bool> _memoryReadRequestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _nativeScanRequestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Ps5ProtocolTestServer(
        bool serveProcessList = false,
        bool serveMemoryMap = false,
        bool serveMemoryRead = false,
        bool serveMemoryWrite = false,
        bool serveNativeScan = false,
        bool serveNativeRefinement = false,
        bool serveProcessControl = false,
        bool serveFollowUpProcessList = false,
        int memoryReadDelayMilliseconds = 0,
        int nativeScanDelayMilliseconds = 0,
        byte nativeValueType = 5,
        byte nativeAlignment = 4,
        byte[]? nativeValueData = null)
    {
        _serveProcessList = serveProcessList;
        _serveMemoryMap = serveMemoryMap;
        _serveMemoryRead = serveMemoryRead;
        _serveMemoryWrite = serveMemoryWrite;
        _serveNativeScan = serveNativeScan;
        _serveNativeRefinement = serveNativeRefinement;
        _serveProcessControl = serveProcessControl;
        _serveFollowUpProcessList = serveFollowUpProcessList;
        _memoryReadDelayMilliseconds = memoryReadDelayMilliseconds;
        _nativeScanDelayMilliseconds = nativeScanDelayMilliseconds;
        _nativeValueType = nativeValueType;
        _nativeAlignment = nativeAlignment;
        _nativeValueData = nativeValueData?.ToArray() ?? BitConverter.GetBytes(10002);
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverTask = RunAsync(_cancellation.Token);
    }

    public int Port { get; }

    public Task Completion => _serverTask;

    public Task MemoryReadRequestReceived => _memoryReadRequestReceived.Task;

    public Task NativeScanRequestReceived => _nativeScanRequestReceived.Task;

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
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
            BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(4, sizeof(uint)), 0x00000004u | 0x00000010u);
            BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(8, sizeof(uint)), 4);
            await stream.WriteAsync(caps, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteUInt32Async(stream, WireStatusError, cancellationToken).ConfigureAwait(false);
        }

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

            if (processId != 2222 || valueType != _nativeValueType || compareType != 0 || alignment != _nativeAlignment ||
                dataLength != _nativeValueData.Length || flags != (0x00000002u | 0x00000010u))
            {
                throw new InvalidDataException(
                    $"Unexpected TurboScan START shape: pid={processId}, valueType={valueType}, compareType={compareType}, alignment={alignment}, dataLength={dataLength}, flags=0x{flags:X8}.");
            }

            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
            byte[] comparisonValue = new byte[_nativeValueData.Length];
            await stream.ReadExactlyAsync(comparisonValue, cancellationToken).ConfigureAwait(false);
            if (!comparisonValue.SequenceEqual(_nativeValueData))
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

            byte[] summary = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(summary.AsSpan(0, sizeof(uint)), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(summary.AsSpan(4, sizeof(ulong)), 2);
            await stream.WriteAsync(summary, cancellationToken).ConfigureAwait(false);
            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);

            if (_nativeScanDelayMilliseconds == 0)
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
                    if (countPid != 2222 || baseAddress != 0 || countValueType != 5 || countCompareType != 0 ||
                        countDataLength != sizeof(int) || countFlags != 0x00000002u)
                    {
                        throw new InvalidDataException(
                            $"Unexpected TurboScan COUNT shape: pid={countPid}, base=0x{baseAddress:X}, valueType={countValueType}, compareType={countCompareType}, dataLength={countDataLength}, flags=0x{countFlags:X8}.");
                    }

                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                    byte[] nextValueBytes = new byte[sizeof(int)];
                    await stream.ReadExactlyAsync(nextValueBytes, cancellationToken).ConfigureAwait(false);
                    int nextValue = BinaryPrimitives.ReadInt32LittleEndian(nextValueBytes);
                    if (nextValue != 10001)
                    {
                        throw new InvalidDataException($"Unexpected TurboScan refinement value {nextValue}; expected 10001.");
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
                    await WriteTurboGetRecordAsync(stream, 0x0000000200000080, BitConverter.GetBytes(10001), BitConverter.GetBytes(10002), cancellationToken).ConfigureAwait(false);
                    await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
                }
            }

            await ExpectCommandAsync(stream, CommandTurboScanEnd, cancellationToken).ConfigureAwait(false);
            await WriteUInt32Async(stream, WireStatusSuccess, cancellationToken).ConfigureAwait(false);
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
