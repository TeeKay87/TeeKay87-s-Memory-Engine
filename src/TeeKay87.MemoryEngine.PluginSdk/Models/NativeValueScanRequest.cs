using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class NativeValueScanRequest
{
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> _inputValues;

    public NativeValueScanRequest(
        string valueTypeId,
        string scanTypeId,
        int valueSize,
        int alignment,
        IEnumerable<ReadOnlyMemory<byte>> inputValues)
        : this(
            valueTypeId,
            scanTypeId,
            valueSize,
            alignment,
            inputValues,
            MemoryScanOptions.Empty)
    {
    }

    public NativeValueScanRequest(
        string valueTypeId,
        string scanTypeId,
        int valueSize,
        int alignment,
        IEnumerable<ReadOnlyMemory<byte>> inputValues,
        MemoryScanOptions scanOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scanTypeId);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(scanOptions);

        if (valueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueSize));
        }

        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        ValueTypeId = valueTypeId;
        ScanTypeId = scanTypeId;
        ValueSize = valueSize;
        Alignment = alignment;
        ScanOptions = scanOptions;
        _inputValues = Array.AsReadOnly(inputValues
            .Select(value => (ReadOnlyMemory<byte>)value.ToArray())
            .ToArray());
    }

    public string ValueTypeId { get; }

    public string ScanTypeId { get; }

    public int ValueSize { get; }

    public int Alignment { get; }

    public MemoryScanOptions ScanOptions { get; }

    public IReadOnlyList<ReadOnlyMemory<byte>> InputValues => _inputValues;
}
