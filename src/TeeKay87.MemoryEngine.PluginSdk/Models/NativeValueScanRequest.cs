using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public enum ValueScanComparison
{
    ExactValue
}

public sealed class NativeValueScanRequest
{
    private readonly byte[] _valueData;

    public NativeValueScanRequest(
        MemoryValueType valueType,
        ValueScanComparison comparison,
        ReadOnlySpan<byte> valueData,
        int alignment)
    {
        if (valueData.IsEmpty)
        {
            throw new ArgumentException("Native value scans require comparison data.", nameof(valueData));
        }

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        ValueType = valueType;
        Comparison = comparison;
        Alignment = alignment;
        _valueData = valueData.ToArray();
    }

    public MemoryValueType ValueType { get; }

    public ValueScanComparison Comparison { get; }

    public int Alignment { get; }

    public ReadOnlyMemory<byte> ValueData => _valueData;
}
