using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class MemoryScanValue
{
    private readonly byte[] _bytes;

    public MemoryScanValue(
        string valueTypeId,
        string valueTypeDisplayName,
        ReadOnlySpan<byte> bytes,
        string displayText,
        int alignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayText);

        if (bytes.IsEmpty)
        {
            throw new ArgumentException("A scan value must contain at least one byte.", nameof(bytes));
        }

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        ValueTypeId = valueTypeId;
        ValueTypeDisplayName = valueTypeDisplayName;
        DisplayText = displayText;
        Alignment = alignment;
        _bytes = bytes.ToArray();
    }

    public string ValueTypeId { get; }

    public string ValueTypeDisplayName { get; }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public string DisplayText { get; }

    public int Alignment { get; }

    public int Size => _bytes.Length;
}
