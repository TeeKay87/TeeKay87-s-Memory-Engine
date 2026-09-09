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
