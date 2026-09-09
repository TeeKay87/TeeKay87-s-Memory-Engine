using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class NativeValueScanResult
{
    private readonly byte[] _currentValueData;

    public NativeValueScanResult(ulong address, ReadOnlySpan<byte> currentValueData)
    {
        if (currentValueData.IsEmpty)
        {
            throw new ArgumentException("A native scan result must include the current value bytes.", nameof(currentValueData));
        }

        Address = address;
        _currentValueData = currentValueData.ToArray();
    }

    public ulong Address { get; }

    public ReadOnlyMemory<byte> CurrentValueData => _currentValueData;
}
