using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Platform.Mock;

internal sealed class MockTargetSession :
    ITargetSession,
    IProcessProvider,
    IForegroundProcessProvider,
    IMemoryReader,
    IMemoryWriter,
    IMemoryMapProvider
{
    private static readonly TargetProcess Process = new(
        MockTargetLayout.ProcessId,
        MockTargetLayout.ProcessName,
        "Mock Game");

    private static readonly IReadOnlyList<TargetProcess> Processes = new[] { Process };

    private static readonly IReadOnlyList<MemoryRegion> MemoryRegions = new[]
    {
        new MemoryRegion(
            MockTargetLayout.BaseAddress,
            MockTargetLayout.MemorySize,
            MemoryProtection.Read | MemoryProtection.Write | MemoryProtection.Private,
            "Mock Main Memory",
            MockTargetLayout.ProcessName)
    };

    private readonly byte[] _memory = new byte[MockTargetLayout.MemorySize];
    private readonly MockDisassemblerProvider _disassembler = new();
    private readonly MockDebuggerProvider _debugger = new(Process);
    private readonly object _memoryGate = new();
    private bool _isConnected = true;

    public MockTargetSession(PluginMetadata plugin)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        InitializeMemory();
    }

    public PluginMetadata Plugin { get; }

    public TargetArchitecture Architecture => Plugin.Architecture;

    public bool IsConnected => _isConnected;

    public TService? GetService<TService>() where TService : class
    {
        EnsureConnected();

        if (_disassembler is TService disassemblerService)
        {
            return disassemblerService;
        }

        if (_debugger is TService debuggerService)
        {
            return debuggerService;
        }

        return this as TService;
    }

    public Task<IReadOnlyList<TargetProcess>> GetProcessesAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Processes);
    }

    public Task<TargetProcess?> GetForegroundProcessAsync(CancellationToken cancellationToken)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<TargetProcess?>(Process);
    }

    public Task<IReadOnlyList<MemoryRegion>> GetMemoryRegionsAsync(
        TargetProcess process,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        ValidateProcess(process);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MemoryRegions);
    }

    public Task<int> ReadAsync(
        TargetProcess process,
        ulong address,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        ValidateProcess(process);
        cancellationToken.ThrowIfCancellationRequested();

        int offset = GetOffset(address, destination.Length);

        lock (_memoryGate)
        {
            _memory.AsSpan(offset, destination.Length).CopyTo(destination.Span);
        }

        return Task.FromResult(destination.Length);
    }

    public Task WriteAsync(
        TargetProcess process,
        ulong address,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        ValidateProcess(process);
        cancellationToken.ThrowIfCancellationRequested();

        int offset = GetOffset(address, source.Length);

        lock (_memoryGate)
        {
            source.Span.CopyTo(_memory.AsSpan(offset, source.Length));
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isConnected)
        {
            return;
        }

        await _debugger.DisposeAsync().ConfigureAwait(false);
        _isConnected = false;
    }

    private void InitializeMemory()
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            _memory.AsSpan(GetOffset(MockTargetLayout.HealthAddress, sizeof(int)), sizeof(int)),
            BitConverter.SingleToInt32Bits(100.0f));

        BinaryPrimitives.WriteInt32LittleEndian(
            _memory.AsSpan(GetOffset(MockTargetLayout.AmmoAddress, sizeof(int)), sizeof(int)),
            30);

        BinaryPrimitives.WriteInt32LittleEndian(
            _memory.AsSpan(GetOffset(MockTargetLayout.MoneyAddress, sizeof(int)), sizeof(int)),
            5000);

        MockTargetLayout.CodeBytes.Span.CopyTo(
            _memory.AsSpan(
                GetOffset(MockTargetLayout.CodeAddress, MockTargetLayout.CodeBytes.Length),
                MockTargetLayout.CodeBytes.Length));
    }

    private static void ValidateProcess(TargetProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (process.Id != MockTargetLayout.ProcessId)
        {
            throw new ArgumentException("The selected process does not belong to this mock target.", nameof(process));
        }
    }

    private static int GetOffset(ulong address, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (address < MockTargetLayout.BaseAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        ulong offset = address - MockTargetLayout.BaseAddress;
        ulong end = checked(offset + (ulong)length);

        if (end > MockTargetLayout.MemorySize)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "The requested range is outside mock memory.");
        }

        return checked((int)offset);
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(!_isConnected, this);
    }
}
