using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryScanType
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    bool AvailableForFirstScan { get; }

    bool AvailableForNextScan { get; }

    int? InputValueCount { get; }

    bool SupportsValueType(IMemoryValueType valueType);


    bool TryValidateInputValues(
        IMemoryValueType valueType,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanStage stage,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(inputValues);
        ArgumentNullException.ThrowIfNull(architecture);
        error = string.Empty;
        return true;
    }

    bool IsMatch(
        IMemoryValueType valueType,
        ReadOnlySpan<byte> currentValue,
        MemoryScanValue? previousValue,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanStage stage);

    bool IsMatch(
        IMemoryValueType valueType,
        ReadOnlySpan<byte> currentValue,
        MemoryScanValue? previousValue,
        IReadOnlyList<MemoryScanValue> inputValues,
        TargetArchitecture architecture,
        MemoryScanStage stage,
        MemoryScanOptions scanOptions)
    {
        ArgumentNullException.ThrowIfNull(scanOptions);
        return IsMatch(valueType, currentValue, previousValue, inputValues, architecture, stage);
    }
}
