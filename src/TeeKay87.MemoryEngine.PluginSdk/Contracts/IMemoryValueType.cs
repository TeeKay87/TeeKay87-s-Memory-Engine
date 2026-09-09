using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryValueType
{
    string Id { get; }

    string DisplayName { get; }

    string Category { get; }

    string Description { get; }

    string InputDescription { get; }

    int? FixedSize { get; }

    int DefaultAlignment { get; }

    bool TryParse(
        string text,
        TargetArchitecture architecture,
        out MemoryScanValue? value,
        out string error);

    bool TryResolveScanShape(
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        out int valueSize,
        out int alignment,
        out string error);

    MemoryScanValue CreateValue(
        ReadOnlySpan<byte> bytes,
        int alignment,
        TargetArchitecture architecture);

    bool ValuesEqual(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        TargetArchitecture architecture);
}
