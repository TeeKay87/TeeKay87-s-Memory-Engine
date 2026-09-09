using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class NativeValueScanResultBatch
{
    private readonly ReadOnlyMemory<ulong> _addresses;
    private readonly ReadOnlyMemory<byte> _currentValueData;
    private readonly ReadOnlyMemory<byte> _previousValueData;

    public NativeValueScanResultBatch(
        ReadOnlyMemory<ulong> addresses,
        ReadOnlyMemory<byte> currentValueData,
        int valueSize,
        ulong sourceRecordsProcessed)
        : this(
            addresses,
            currentValueData,
            ReadOnlyMemory<byte>.Empty,
            valueSize,
            sourceRecordsProcessed)
    {
    }

    public NativeValueScanResultBatch(
        ReadOnlyMemory<ulong> addresses,
        ReadOnlyMemory<byte> currentValueData,
        ReadOnlyMemory<byte> previousValueData,
        int valueSize,
        ulong sourceRecordsProcessed)
    {
        if (valueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueSize), valueSize, "Value size must be positive.");
        }

        int expectedValueBytes = checked(addresses.Length * valueSize);
        if (currentValueData.Length != expectedValueBytes)
        {
            throw new ArgumentException(
                "Current-value data length must equal address count multiplied by value size.",
                nameof(currentValueData));
        }

        if (!previousValueData.IsEmpty && previousValueData.Length != expectedValueBytes)
        {
            throw new ArgumentException(
                "Previous-value data length must be empty or equal address count multiplied by value size.",
                nameof(previousValueData));
        }

        _addresses = addresses;
        _currentValueData = currentValueData;
        _previousValueData = previousValueData;
        ValueSize = valueSize;
        SourceRecordsProcessed = sourceRecordsProcessed;
    }

    public int Count => _addresses.Length;

    public int ValueSize { get; }

    public ulong SourceRecordsProcessed { get; }

    public ReadOnlyMemory<ulong> Addresses => _addresses;

    public ReadOnlyMemory<byte> CurrentValueData => _currentValueData;

    public bool HasPreviousValueData => !_previousValueData.IsEmpty;

    public ReadOnlyMemory<byte> PreviousValueData => _previousValueData;

    public ReadOnlyMemory<byte> GetCurrentValueData(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _currentValueData.Slice(checked(index * ValueSize), ValueSize);
    }

    public ReadOnlyMemory<byte> GetPreviousValueData(int index)
    {
        if (!HasPreviousValueData)
        {
            throw new InvalidOperationException("This native result batch does not contain previous-value data.");
        }

        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _previousValueData.Slice(checked(index * ValueSize), ValueSize);
    }
}
