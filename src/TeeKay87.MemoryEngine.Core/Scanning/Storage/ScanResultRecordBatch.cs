using System;

namespace TeeKay87.MemoryEngine.Core.Scanning.Storage;

public sealed class ScanResultRecordBatch
{
    private readonly ReadOnlyMemory<ulong> _addresses;
    private readonly ReadOnlyMemory<byte> _currentValueData;

    public ScanResultRecordBatch(
        ReadOnlyMemory<ulong> addresses,
        ReadOnlyMemory<byte> currentValueData,
        int valueSize)
    {
        if (valueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueSize), valueSize, "Value size must be positive.");
        }

        if (currentValueData.Length != checked(addresses.Length * valueSize))
        {
            throw new ArgumentException(
                "Current-value data length must equal address count multiplied by value size.",
                nameof(currentValueData));
        }

        _addresses = addresses;
        _currentValueData = currentValueData;
        ValueSize = valueSize;
    }

    public int Count => _addresses.Length;

    public int ValueSize { get; }

    public ReadOnlyMemory<ulong> Addresses => _addresses;

    public ReadOnlyMemory<byte> CurrentValueData => _currentValueData;

    public ReadOnlyMemory<byte> GetCurrentValueData(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _currentValueData.Slice(checked(index * ValueSize), ValueSize);
    }
}
