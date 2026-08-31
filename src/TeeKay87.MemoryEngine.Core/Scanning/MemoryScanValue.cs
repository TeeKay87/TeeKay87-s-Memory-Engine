using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed class MemoryScanValue
{
    private readonly byte[] _bytes;

    public MemoryScanValue(
        MemoryValueType valueType,
        ReadOnlySpan<byte> bytes,
        string displayText,
        int alignment)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("A scan value must contain at least one byte.", nameof(bytes));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayText);

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        ValueType = valueType;
        DisplayText = displayText;
        Alignment = alignment;
        _bytes = bytes.ToArray();
    }

    public MemoryValueType ValueType { get; }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public string DisplayText { get; }

    public int Alignment { get; }

    public int Size => _bytes.Length;
}
