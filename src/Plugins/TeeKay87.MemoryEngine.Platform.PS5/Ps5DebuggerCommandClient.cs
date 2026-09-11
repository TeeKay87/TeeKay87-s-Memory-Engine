using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed class Ps5DebuggerCommandClient : IAsyncDisposable
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OptionalRegisterProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private bool _disposed;

    private Ps5DebuggerCommandClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
    }

    public static async Task<Ps5DebuggerCommandClient> ConnectAsync(
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
            return new Ps5DebuggerCommandClient(tcpClient);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            tcpClient.Dispose();
            throw new TimeoutException(
                $"Timed out while opening the dedicated ps5debug-NG debugger command connection to {host}:{port} after {ConnectionTimeout.TotalSeconds:0} seconds.");
        }
        catch (SocketException exception)
        {
            tcpClient.Dispose();
            throw new InvalidOperationException(
                $"Could not open the dedicated ps5debug-NG debugger command connection to {host}:{port}. {exception.Message}",
                exception);
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }

    public Task AttachAsync(int processId, CancellationToken cancellationToken)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        byte[] body = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(body, processId);
        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugAttach,
            body,
            "debugger attach",
            cancellationToken);
    }

    public Task DetachAsync(CancellationToken cancellationToken)
    {
        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugDetach,
            ReadOnlyMemory<byte>.Empty,
            "debugger detach",
            cancellationToken);
    }

    public Task SetExecutionActionAsync(byte action, CancellationToken cancellationToken)
    {
        if (action > Ps5DebugProtocol.DebuggerActionKill)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        byte[] body = new byte[sizeof(uint)];
        body[0] = action;
        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugContinue,
            body,
            action == Ps5DebugProtocol.DebuggerActionResume
                ? "debugger continue"
                : action == Ps5DebugProtocol.DebuggerActionPause
                    ? "debugger pause"
                    : "debugger kill",
            cancellationToken);
    }


    public async Task<IReadOnlyList<Ps5DebuggerThreadInfo>> GetThreadsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendCommandAsync(
                    Ps5DebugProtocol.CommandDebugGetThreadList,
                    ReadOnlyMemory<byte>.Empty,
                    CancellationToken.None)
                .ConfigureAwait(false);

            uint status = await ReadStatusAsync(CancellationToken.None).ConfigureAwait(false);
            ValidateStatus(status, "debugger thread enumeration");

            uint count = await ReadUInt32Async(CancellationToken.None).ConfigureAwait(false);
            if (count > Ps5DebugProtocol.MaximumDebuggerThreadCount)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG returned an unreasonable debugger thread count ({count}).");
            }

            byte[] ids = new byte[checked((int)count * sizeof(uint))];
            if (ids.Length > 0)
            {
                await _stream.ReadExactlyAsync(ids, CancellationToken.None).ConfigureAwait(false);
            }

            List<Ps5DebuggerThreadInfo> threads = new(checked((int)count));
            for (int index = 0; index < count; index++)
            {
                uint threadId = BinaryPrimitives.ReadUInt32LittleEndian(
                    ids.AsSpan(index * sizeof(uint), sizeof(uint)));
                threads.Add(await ReadThreadInfoAsync(threadId).ConfigureAwait(false));
            }

            return threads;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    public async Task<byte[]> GetGeneralRegistersAsync(
        uint threadId,
        CancellationToken cancellationToken)
    {
        if (threadId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threadId));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            byte[] body = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(body, threadId);

            cancellationToken.ThrowIfCancellationRequested();
            await SendCommandAsync(
                    Ps5DebugProtocol.CommandDebugGetRegisters,
                    body,
                    CancellationToken.None)
                .ConfigureAwait(false);

            uint status = await ReadStatusAsync(CancellationToken.None).ConfigureAwait(false);
            ValidateStatus(status, "debugger register read");

            byte[] registers = new byte[Ps5DebugProtocol.DebuggerGeneralRegisterSize];
            await _stream.ReadExactlyAsync(registers, CancellationToken.None).ConfigureAwait(false);
            return registers;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    public async Task<IReadOnlyList<Ps5DebuggerStackFrameInfo>> ReadCallStackAsync(
        int processId,
        ulong framePointer,
        ulong stackPointer,
        int depth,
        CancellationToken cancellationToken)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (depth is < 1 or > Ps5DebugProtocol.DebuggerStackMaximumDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            byte[] body = new byte[Ps5DebugProtocol.DebuggerStackRequestSize];
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, sizeof(uint)), checked((uint)processId));
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(4, sizeof(ulong)), framePointer);
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(12, sizeof(ulong)), stackPointer);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(20, sizeof(uint)), checked((uint)depth));

            cancellationToken.ThrowIfCancellationRequested();
            await SendCommandAsync(
                    Ps5DebugProtocol.CommandProcessReadStack,
                    body,
                    CancellationToken.None)
                .ConfigureAwait(false);

            uint status = await ReadStatusAsync(CancellationToken.None).ConfigureAwait(false);
            ValidateStatus(status, "debugger call-stack read");

            uint payloadLength = await ReadUInt32Async(CancellationToken.None).ConfigureAwait(false);
            if (payloadLength < sizeof(uint) || payloadLength > Ps5DebugProtocol.DebuggerStackMaximumPayloadSize)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG returned an invalid call-stack payload length ({payloadLength}).");
            }

            byte[] payload = new byte[checked((int)payloadLength)];
            await _stream.ReadExactlyAsync(payload, CancellationToken.None).ConfigureAwait(false);

            uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint)));
            if (frameCount > Ps5DebugProtocol.DebuggerStackMaximumDepth || frameCount > depth)
            {
                throw new InvalidDataException(
                    $"ps5debug-NG returned an invalid call-stack frame count ({frameCount}).");
            }

            List<Ps5DebuggerStackFrameInfo> frames = new(checked((int)frameCount));
            int offset = sizeof(uint);
            for (int index = 0; index < frameCount; index++)
            {
                if (payload.Length - offset < Ps5DebugProtocol.DebuggerStackFrameHeaderSize)
                {
                    throw new InvalidDataException("ps5debug-NG call-stack payload ended inside a frame header.");
                }

                ReadOnlySpan<byte> header = payload.AsSpan(offset, Ps5DebugProtocol.DebuggerStackFrameHeaderSize);
                ulong currentFramePointer = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(0, 8));
                ulong currentStackPointer = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8));
                ulong savedFramePointer = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(16, 8));
                ulong returnAddress = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(24, 8));
                uint localsLength = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(36, 4));
                uint codeLength = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(40, 4));

                if (localsLength > Ps5DebugProtocol.DebuggerStackMaximumLocalsLength ||
                    codeLength > Ps5DebugProtocol.DebuggerStackMaximumCodeLength)
                {
                    throw new InvalidDataException("ps5debug-NG call-stack payload contains an invalid variable frame length.");
                }

                offset += Ps5DebugProtocol.DebuggerStackFrameHeaderSize;
                int variableLength = checked((int)(localsLength + codeLength));
                if (payload.Length - offset < variableLength)
                {
                    throw new InvalidDataException("ps5debug-NG call-stack payload ended inside frame data.");
                }

                offset += variableLength;
                frames.Add(new Ps5DebuggerStackFrameInfo(
                    currentFramePointer,
                    currentStackPointer,
                    savedFramePointer,
                    returnAddress));
            }

            if (offset != payload.Length)
            {
                throw new InvalidDataException("ps5debug-NG call-stack payload contains unexpected trailing bytes.");
            }

            return frames;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    public Task StepAsync(uint? threadId, CancellationToken cancellationToken)
    {
        if (threadId.HasValue)
        {
            if (threadId.Value == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(threadId));
            }

            byte[] body = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(body, threadId.Value);
            return ExecuteStatusCommandAsync(
                Ps5DebugProtocol.CommandDebugStepThread,
                body,
                "debugger thread step",
                cancellationToken);
        }

        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugStep,
            ReadOnlyMemory<byte>.Empty,
            "debugger process step",
            cancellationToken);
    }

    public static Task<byte[]?> ProbeFloatingPointRegistersAsync(
        string host,
        int port,
        uint threadId,
        CancellationToken cancellationToken)
    {
        return ProbeOptionalRegisterBlockAsync(
            host,
            port,
            Ps5DebugProtocol.CommandDebugGetFloatingPointRegisters,
            threadId,
            Ps5DebugProtocol.DebuggerFloatingPointRegisterSize,
            "debugger floating-point register read",
            cancellationToken);
    }

    public static Task<byte[]?> ProbeFsGsBaseAsync(
        string host,
        int port,
        uint threadId,
        CancellationToken cancellationToken)
    {
        return ProbeOptionalRegisterBlockAsync(
            host,
            port,
            Ps5DebugProtocol.CommandDebugGetFsGsBase,
            threadId,
            Ps5DebugProtocol.DebuggerFsGsBaseSize,
            "debugger FS/GS base read",
            cancellationToken);
    }

    public Task SetSoftwareBreakpointAsync(
        int slotIndex,
        bool isEnabled,
        ulong address,
        CancellationToken cancellationToken)
    {
        if (slotIndex is < 0 or >= Ps5DebugProtocol.MaximumSoftwareBreakpointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        byte[] body = new byte[Ps5DebugProtocol.DebuggerBreakpointRequestSize];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, sizeof(uint)), checked((uint)slotIndex));
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(sizeof(uint), sizeof(uint)), isEnabled ? 1u : 0u);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(sizeof(uint) * 2, sizeof(ulong)), address);
        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugSetBreakpoint,
            body,
            isEnabled ? "software breakpoint enable" : "software breakpoint disable",
            cancellationToken);
    }

    public Task SetHardwareWatchpointAsync(
        int slotIndex,
        bool isEnabled,
        uint lengthEncoding,
        uint breakType,
        ulong address,
        CancellationToken cancellationToken)
    {
        if (slotIndex is < 0 or >= Ps5DebugProtocol.MaximumHardwareWatchpointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        if (lengthEncoding > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthEncoding));
        }

        if (breakType is not (1u or 3u))
        {
            throw new ArgumentOutOfRangeException(nameof(breakType));
        }

        byte[] body = new byte[Ps5DebugProtocol.DebuggerWatchpointRequestSize];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0, sizeof(uint)), checked((uint)slotIndex));
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4, sizeof(uint)), isEnabled ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8, sizeof(uint)), lengthEncoding);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(12, sizeof(uint)), breakType);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(16, sizeof(ulong)), address);
        return ExecuteStatusCommandAsync(
            Ps5DebugProtocol.CommandDebugSetWatchpoint,
            body,
            isEnabled ? "hardware watchpoint enable" : "hardware watchpoint disable",
            cancellationToken);
    }

    public Task SuspendThreadAsync(uint threadId, CancellationToken cancellationToken)
    {
        return ExecuteThreadControlAsync(
            Ps5DebugProtocol.CommandDebugSuspendThread,
            threadId,
            "debugger thread suspend",
            cancellationToken);
    }

    public Task ResumeThreadAsync(uint threadId, CancellationToken cancellationToken)
    {
        return ExecuteThreadControlAsync(
            Ps5DebugProtocol.CommandDebugResumeThread,
            threadId,
            "debugger thread resume",
            cancellationToken);
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
        _commandGate.Dispose();
        return ValueTask.CompletedTask;
    }



    private static async Task<byte[]?> ProbeOptionalRegisterBlockAsync(
        string host,
        int port,
        uint command,
        uint threadId,
        int responseLength,
        string operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(OptionalRegisterProbeTimeout);

        try
        {
            await using Ps5DebuggerCommandClient probeClient = await ConnectAsync(
                    host,
                    port,
                    timeoutSource.Token)
                .ConfigureAwait(false);

            return await probeClient
                .GetOptionalRegisterBlockAsync(
                    command,
                    threadId,
                    responseLength,
                    operation,
                    timeoutSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (InvalidOperationException exception)
            when (!cancellationToken.IsCancellationRequested && exception.InnerException is SocketException)
        {
            return null;
        }
        catch (IOException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (InvalidDataException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<byte[]?> GetOptionalRegisterBlockAsync(
        uint command,
        uint threadId,
        int responseLength,
        string operation,
        CancellationToken cancellationToken)
    {
        if (threadId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threadId));
        }

        if (responseLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responseLength));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            byte[] body = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(body, threadId);

            cancellationToken.ThrowIfCancellationRequested();
            await SendCommandAsync(command, body, cancellationToken).ConfigureAwait(false);

            uint status = await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status is Ps5DebugProtocol.WireStatusError or Ps5DebugProtocol.WireStatusDataNull)
            {
                return null;
            }

            ValidateStatus(status, operation);
            byte[] response = new byte[responseLength];
            await _stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
            return response;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private Task ExecuteThreadControlAsync(
        uint command,
        uint threadId,
        string operation,
        CancellationToken cancellationToken)
    {
        if (threadId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threadId));
        }

        byte[] body = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(body, threadId);
        return ExecuteStatusCommandAsync(command, body, operation, cancellationToken);
    }

    private async Task<Ps5DebuggerThreadInfo> ReadThreadInfoAsync(uint threadId)
    {
        byte[] body = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(body, threadId);
        await SendCommandAsync(
                Ps5DebugProtocol.CommandDebugThreadInfo,
                body,
                CancellationToken.None)
            .ConfigureAwait(false);

        uint status = await ReadStatusAsync(CancellationToken.None).ConfigureAwait(false);
        if (status is Ps5DebugProtocol.WireStatusError or Ps5DebugProtocol.WireStatusDataNull)
        {
            return new Ps5DebuggerThreadInfo(threadId, null, 0);
        }

        ValidateStatus(status, "debugger thread info");
        byte[] response = new byte[Ps5DebugProtocol.DebuggerThreadInfoResponseSize];
        await _stream.ReadExactlyAsync(response, CancellationToken.None).ConfigureAwait(false);

        uint responseThreadId = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(0, sizeof(uint)));
        uint priority = BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(sizeof(uint), sizeof(uint)));
        if (responseThreadId != threadId)
        {
            throw new InvalidDataException(
                $"ps5debug-NG returned thread info for 0x{responseThreadId:X} while 0x{threadId:X} was requested.");
        }

        string name = ReadNullTerminatedUtf8(response.AsSpan(
            Ps5DebugProtocol.DebuggerThreadInfoNameOffset,
            Ps5DebugProtocol.DebuggerThreadInfoNameLength));
        return new Ps5DebuggerThreadInfo(threadId, string.IsNullOrWhiteSpace(name) ? null : name, priority);
    }

    private async Task<uint> ReadUInt32Async(CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0)
        {
            bytes = bytes[..terminator];
        }

        return bytes.IsEmpty ? string.Empty : Encoding.UTF8.GetString(bytes).Trim();
    }

    private async Task ExecuteStatusCommandAsync(
        uint command,
        ReadOnlyMemory<byte> body,
        string operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendCommandAsync(command, body, CancellationToken.None).ConfigureAwait(false);
            uint status = await ReadStatusAsync(CancellationToken.None).ConfigureAwait(false);
            ValidateStatus(status, operation);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private async Task SendCommandAsync(
        uint command,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
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

    private async Task<uint> ReadStatusAsync(CancellationToken cancellationToken)
    {
        byte[] statusBytes = new byte[sizeof(uint)];
        await _stream.ReadExactlyAsync(statusBytes, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(statusBytes);
    }

    private static void ValidateStatus(uint status, string operation)
    {
        if (status == Ps5DebugProtocol.WireStatusSuccess)
        {
            return;
        }

        if (status == Ps5DebugProtocol.WireStatusAlreadyDebug)
        {
            throw new InvalidOperationException(
                "ps5debug-NG reports that a debugger is already attached to a target.");
        }

        throw new InvalidDataException(
            $"ps5debug-NG {operation} returned unexpected status 0x{status:X8}.");
    }
}
