using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal enum Ps5NativeScanMode
{
    Compare,
    SnapshotIncludeZeros
}

internal static class Ps5NativeScanTypeMappings
{
    private static readonly string[] IntegerValueTypeIds =
    {
        StandardMemoryValueTypeIds.UInt8,
        StandardMemoryValueTypeIds.Int8,
        StandardMemoryValueTypeIds.UInt16,
        StandardMemoryValueTypeIds.Int16,
        StandardMemoryValueTypeIds.UInt32,
        StandardMemoryValueTypeIds.Int32,
        StandardMemoryValueTypeIds.UInt64,
        StandardMemoryValueTypeIds.Int64
    };

    private static readonly string[] FloatingPointValueTypeIds =
    {
        StandardMemoryValueTypeIds.Float32,
        StandardMemoryValueTypeIds.Float64
    };

    private static readonly string[] NumericValueTypeIds = IntegerValueTypeIds
        .Concat(FloatingPointValueTypeIds)
        .ToArray();

    private static readonly string[] ExactValueTypeIds = NumericValueTypeIds
        .Append(StandardMemoryValueTypeIds.ByteArray)
        .ToArray();

    private static readonly Definition[] Definitions =
    {
        DefineCompare(StandardMemoryScanTypeIds.ExactValue, Ps5DebugProtocol.ScanCompareTypeExactValue, 1, true, true, ExactValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.FuzzyValue, Ps5DebugProtocol.ScanCompareTypeFuzzyValue, 1, true, true, FloatingPointValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.BiggerThan, Ps5DebugProtocol.ScanCompareTypeBiggerThan, 1, true, true, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.SmallerThan, Ps5DebugProtocol.ScanCompareTypeSmallerThan, 1, true, true, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.Between, Ps5DebugProtocol.ScanCompareTypeBetween, 2, true, true, NumericValueTypeIds),
        DefineSnapshot(StandardMemoryScanTypeIds.UnknownInitialValue, Ps5DebugProtocol.ScanCompareTypeUnknownInitialValue, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.UnknownInitialLowValue, Ps5DebugProtocol.ScanCompareTypeUnknownInitialLowValue, 1, true, false, IntegerValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.IncreasedValue, Ps5DebugProtocol.ScanCompareTypeIncreasedValue, 0, false, true, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.DecreasedValue, Ps5DebugProtocol.ScanCompareTypeDecreasedValue, 0, false, true, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.ChangedValue, Ps5DebugProtocol.ScanCompareTypeChangedValue, 0, false, true, NumericValueTypeIds),
        DefineCompare(StandardMemoryScanTypeIds.UnchangedValue, Ps5DebugProtocol.ScanCompareTypeUnchangedValue, 0, false, true, NumericValueTypeIds)
    };

    public static IReadOnlyList<NativeScanTypeMapping> All { get; } = Array.AsReadOnly(
        Definitions.Select(definition => definition.Mapping).ToArray());

    public static bool TryResolve(
        string coreScanTypeId,
        string valueTypeId,
        MemoryScanStage stage,
        out byte compareType,
        out int inputValueCount,
        out Ps5NativeScanMode mode)
    {
        Definition? definition = Definitions.FirstOrDefault(candidate =>
            candidate.Mapping.Supports(coreScanTypeId, valueTypeId, stage));

        if (definition is null)
        {
            compareType = 0;
            inputValueCount = 0;
            mode = Ps5NativeScanMode.Compare;
            return false;
        }

        compareType = definition.CompareType;
        inputValueCount = definition.InputValueCount;
        mode = definition.Mode;
        return true;
    }

    private static Definition DefineCompare(
        string coreScanTypeId,
        byte compareType,
        int inputValueCount,
        bool availableForFirstScan,
        bool availableForNextScan,
        IEnumerable<string> supportedValueTypeIds)
    {
        return new Definition(
            new NativeScanTypeMapping(
                coreScanTypeId,
                $"ps5debug-ng.compare.{compareType}",
                availableForFirstScan,
                availableForNextScan,
                supportedValueTypeIds),
            compareType,
            inputValueCount,
            Ps5NativeScanMode.Compare);
    }

    private static Definition DefineSnapshot(
        string coreScanTypeId,
        byte compareType,
        IEnumerable<string> supportedValueTypeIds)
    {
        return new Definition(
            new NativeScanTypeMapping(
                coreScanTypeId,
                "ps5debug-ng.snapshot.include-zeros",
                availableForFirstScan: true,
                availableForNextScan: false,
                supportedValueTypeIds),
            compareType,
            inputValueCount: 0,
            Ps5NativeScanMode.SnapshotIncludeZeros);
    }

    private sealed class Definition
    {
        public Definition(
            NativeScanTypeMapping mapping,
            byte compareType,
            int inputValueCount,
            Ps5NativeScanMode mode)
        {
            Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            CompareType = compareType;
            InputValueCount = inputValueCount;
            Mode = mode;
        }

        public NativeScanTypeMapping Mapping { get; }

        public byte CompareType { get; }

        public int InputValueCount { get; }

        public Ps5NativeScanMode Mode { get; }
    }
}
