using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public static class MemoryScanTypeCatalog
{
    public static IReadOnlyList<IMemoryScanType> All { get; } = Array.AsReadOnly(new IMemoryScanType[]
    {
        new ExactValueScanType(),
        new FuzzyValueScanType(),
        new BiggerThanScanType(),
        new SmallerThanScanType(),
        new BetweenScanType(),
        new UnknownInitialValueScanType(),
        new UnknownInitialLowValueScanType(),
        new IncreasedValueScanType(),
        new DecreasedValueScanType(),
        new ChangedValueScanType(),
        new UnchangedValueScanType(),
        new IncreasedByScanType(),
        new DecreasedByScanType()
    });

    public const string DefaultScanTypeId = StandardMemoryScanTypeIds.ExactValue;

    private abstract class ScanTypeBase : IMemoryScanType
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract bool AvailableForFirstScan { get; }
        public abstract bool AvailableForNextScan { get; }
        public abstract int? InputValueCount { get; }
        public abstract bool SupportsValueType(IMemoryValueType valueType);

        public virtual bool TryValidateInputValues(
            IMemoryValueType valueType,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(valueType);
            ArgumentNullException.ThrowIfNull(inputValues);
            ArgumentNullException.ThrowIfNull(architecture);

            if ((stage == MemoryScanStage.FirstScan && !AvailableForFirstScan) ||
                (stage == MemoryScanStage.NextScan && !AvailableForNextScan))
            {
                error = $"{DisplayName} is not available for {stage}.";
                return false;
            }

            if (!SupportsValueType(valueType))
            {
                error = $"{DisplayName} does not support {valueType.DisplayName}.";
                return false;
            }

            if (InputValueCount is int expected && inputValues.Count != expected)
            {
                error = $"{DisplayName} requires {expected} input value(s).";
                return false;
            }

            if (inputValues.Any(value => !string.Equals(value.ValueTypeId, valueType.Id, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"{DisplayName} received an input value that does not match {valueType.DisplayName}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public abstract bool IsMatch(
            IMemoryValueType valueType,
            ReadOnlySpan<byte> currentValue,
            MemoryScanValue? previousValue,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage);

        public virtual bool IsMatch(
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

        protected static bool TryCompare(
            IMemoryValueType valueType,
            ReadOnlySpan<byte> left,
            ReadOnlySpan<byte> right,
            TargetArchitecture architecture,
            out int comparison)
        {
            if (valueType is IMemoryValueComparer comparer)
            {
                return comparer.TryCompare(left, right, architecture, out comparison);
            }

            comparison = 0;
            return false;
        }

        protected static bool HasComparablePrevious(IMemoryValueType valueType, MemoryScanValue? previousValue) =>
            valueType is IMemoryValueComparer && previousValue is not null;

        protected static float ReadFloat32(ReadOnlySpan<byte> bytes, Endianness endianness)
        {
            int bits = endianness == Endianness.Big
                ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes)
                : System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
            return BitConverter.Int32BitsToSingle(bits);
        }

        protected static double ReadFloat64(ReadOnlySpan<byte> bytes, Endianness endianness)
        {
            long bits = endianness == Endianness.Big
                ? System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes)
                : System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes);
            return BitConverter.Int64BitsToDouble(bits);
        }
    }

    private sealed class ExactValueScanType : ScanTypeBase
    {
        private const float FloatMinimumNormal = 1.17549435E-38f;
        private const double DoubleMinimumNormal = 2.2250738585072014E-308;
        private const float Ps5DebugFloatTolerance = 1e-6f;
        private const double Ps5DebugDoubleTolerance = 1e-6;

        public override string Id => StandardMemoryScanTypeIds.ExactValue;
        public override string DisplayName => "Exact Value";
        public override string Description => "Matches memory values exactly against the supplied value.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 1;
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType is not null;

        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage)
        {
            return inputValues.Count == 1 && valueType.ValuesEqual(currentValue, inputValues[0].Bytes.Span, architecture);
        }

        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage, MemoryScanOptions scanOptions)
        {
            if (inputValues.Count != 1)
            {
                return false;
            }

            if (!scanOptions.TryGetValue(StandardMemoryScanOptionIds.FloatingPointRounding, out string choiceId) ||
                !string.Equals(choiceId, StandardMemoryScanOptionChoiceIds.FloatingPointRelativeTolerance1E6, StringComparison.OrdinalIgnoreCase))
            {
                return valueType.ValuesEqual(currentValue, inputValues[0].Bytes.Span, architecture);
            }

            return valueType.Id switch
            {
                StandardMemoryValueTypeIds.Float32 when currentValue.Length == sizeof(float) =>
                    FuzzyFloatCompare(ReadFloat32(currentValue, architecture.Endianness), ReadFloat32(inputValues[0].Bytes.Span, architecture.Endianness), Ps5DebugFloatTolerance),
                StandardMemoryValueTypeIds.Float64 when currentValue.Length == sizeof(double) =>
                    FuzzyDoubleCompare(ReadFloat64(currentValue, architecture.Endianness), ReadFloat64(inputValues[0].Bytes.Span, architecture.Endianness), Ps5DebugDoubleTolerance),
                _ => valueType.ValuesEqual(currentValue, inputValues[0].Bytes.Span, architecture)
            };
        }

        private static bool FuzzyFloatCompare(float scanValue, float memoryValue, float tolerance)
        {
            if (scanValue == memoryValue) return true;
            float difference = MathF.Abs(scanValue - memoryValue);
            float absoluteScan = MathF.Abs(scanValue);
            float absoluteMemory = MathF.Abs(memoryValue);
            if (scanValue == 0.0f || memoryValue == 0.0f) return FloatMinimumNormal * tolerance > difference;
            float sum = absoluteScan + absoluteMemory;
            if (FloatMinimumNormal > sum) return FloatMinimumNormal * tolerance > difference;
            return tolerance > difference / sum;
        }

        private static bool FuzzyDoubleCompare(double scanValue, double memoryValue, double tolerance)
        {
            if (scanValue == memoryValue) return true;
            double difference = Math.Abs(scanValue - memoryValue);
            double absoluteScan = Math.Abs(scanValue);
            double absoluteMemory = Math.Abs(memoryValue);
            if (scanValue == 0.0 || memoryValue == 0.0) return DoubleMinimumNormal * tolerance > difference;
            double sum = absoluteScan + absoluteMemory;
            if (DoubleMinimumNormal > sum) return DoubleMinimumNormal * tolerance > difference;
            return tolerance > difference / sum;
        }
    }

    private sealed class FuzzyValueScanType : ScanTypeBase
    {
        public override string Id => StandardMemoryScanTypeIds.FuzzyValue;
        public override string DisplayName => "Fuzzy Value";
        public override string Description => "Matches Float and Double values whose absolute difference from the supplied value is less than 1.0.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 1;
        public override bool SupportsValueType(IMemoryValueType valueType) =>
            valueType?.Id is StandardMemoryValueTypeIds.Float32 or StandardMemoryValueTypeIds.Float64;

        public override bool IsMatch(
            IMemoryValueType valueType,
            ReadOnlySpan<byte> currentValue,
            MemoryScanValue? previousValue,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage)
        {
            if (inputValues.Count != 1)
            {
                return false;
            }

            return valueType.Id switch
            {
                StandardMemoryValueTypeIds.Float32 when currentValue.Length == sizeof(float) =>
                    IsFuzzyMatch(
                        ReadFloat32(currentValue, architecture.Endianness),
                        ReadFloat32(inputValues[0].Bytes.Span, architecture.Endianness)),
                StandardMemoryValueTypeIds.Float64 when currentValue.Length == sizeof(double) =>
                    IsFuzzyMatch(
                        ReadFloat64(currentValue, architecture.Endianness),
                        ReadFloat64(inputValues[0].Bytes.Span, architecture.Endianness)),
                _ => false
            };
        }

        private static bool IsFuzzyMatch(float memoryValue, float scanValue) =>
            memoryValue == scanValue || MathF.Abs(memoryValue - scanValue) < 1.0f;

        private static bool IsFuzzyMatch(double memoryValue, double scanValue) =>
            memoryValue == scanValue || Math.Abs(memoryValue - scanValue) < 1.0;
    }

    private abstract class OrderedInputScanType : ScanTypeBase
    {
        public override bool SupportsValueType(IMemoryValueType valueType) =>
            valueType is IMemoryValueComparer && valueType.FixedSize is > 0;
    }

    private sealed class BiggerThanScanType : OrderedInputScanType
    {
        public override string Id => StandardMemoryScanTypeIds.BiggerThan;
        public override string DisplayName => "Bigger Than";
        public override string Description => "Matches values strictly greater than the supplied value.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 1;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            inputValues.Count == 1 && TryCompare(valueType, currentValue, inputValues[0].Bytes.Span, architecture, out int comparison) && comparison > 0;
    }

    private sealed class SmallerThanScanType : OrderedInputScanType
    {
        public override string Id => StandardMemoryScanTypeIds.SmallerThan;
        public override string DisplayName => "Smaller Than";
        public override string Description => "Matches values strictly smaller than the supplied value.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 1;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            inputValues.Count == 1 && TryCompare(valueType, currentValue, inputValues[0].Bytes.Span, architecture, out int comparison) && comparison < 0;
    }

    private sealed class BetweenScanType : OrderedInputScanType
    {
        public override string Id => StandardMemoryScanTypeIds.Between;
        public override string DisplayName => "Between";
        public override string Description => "Matches values inclusively between the two supplied bounds.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 2;

        public override bool TryValidateInputValues(IMemoryValueType valueType, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage, out string error)
        {
            if (!base.TryValidateInputValues(valueType, inputValues, architecture, stage, out error)) return false;
            if (!TryCompare(valueType, inputValues[0].Bytes.Span, inputValues[1].Bytes.Span, architecture, out int comparison))
            {
                error = $"{valueType.DisplayName} does not support ordered comparisons.";
                return false;
            }
            if (comparison > 0)
            {
                error = "The first Between value must be less than or equal to the second value.";
                return false;
            }
            return true;
        }

        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage)
        {
            return inputValues.Count == 2 &&
                   TryCompare(valueType, currentValue, inputValues[0].Bytes.Span, architecture, out int lower) && lower >= 0 &&
                   TryCompare(valueType, currentValue, inputValues[1].Bytes.Span, architecture, out int upper) && upper <= 0;
        }
    }

    private sealed class UnknownInitialValueScanType : ScanTypeBase
    {
        public override string Id => StandardMemoryScanTypeIds.UnknownInitialValue;
        public override string DisplayName => "Unknown Initial Value";
        public override string Description => "Stores every candidate's initial value so later scans can compare against the previous snapshot.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => false;
        public override int? InputValueCount => 0;
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType?.FixedSize is > 0;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) => stage == MemoryScanStage.FirstScan;
    }

    private sealed class UnknownInitialLowValueScanType : ScanTypeBase
    {
        public override string Id => StandardMemoryScanTypeIds.UnknownInitialLowValue;
        public override string DisplayName => "Unknown Initial Low Value";
        public override string Description => "Stores positive, non-zero initial numeric values up to and including the supplied upper limit.";
        public override bool AvailableForFirstScan => true;
        public override bool AvailableForNextScan => false;
        public override int? InputValueCount => 1;

        public override bool SupportsValueType(IMemoryValueType valueType) =>
            valueType?.Id is StandardMemoryValueTypeIds.UInt8 or StandardMemoryValueTypeIds.Int8 or
                StandardMemoryValueTypeIds.UInt16 or StandardMemoryValueTypeIds.Int16 or
                StandardMemoryValueTypeIds.UInt32 or StandardMemoryValueTypeIds.Int32 or
                StandardMemoryValueTypeIds.UInt64 or StandardMemoryValueTypeIds.Int64 or
                StandardMemoryValueTypeIds.Float32 or StandardMemoryValueTypeIds.Float64;

        public override bool TryValidateInputValues(
            IMemoryValueType valueType,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage,
            out string error)
        {
            if (!base.TryValidateInputValues(valueType, inputValues, architecture, stage, out error))
            {
                return false;
            }

            Span<byte> zero = stackalloc byte[8];
            ReadOnlySpan<byte> upperLimit = inputValues[0].Bytes.Span;
            if (upperLimit.Length > zero.Length ||
                !TryCompare(valueType, upperLimit, zero[..upperLimit.Length], architecture, out int comparison) ||
                comparison <= 0)
            {
                error = "Unknown Initial Low Value requires an upper limit greater than zero.";
                return false;
            }

            return true;
        }

        public override bool IsMatch(
            IMemoryValueType valueType,
            ReadOnlySpan<byte> currentValue,
            MemoryScanValue? previousValue,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage)
        {
            if (stage != MemoryScanStage.FirstScan || inputValues.Count != 1 || currentValue.Length > 8)
            {
                return false;
            }

            Span<byte> zero = stackalloc byte[8];
            return TryCompare(valueType, currentValue, zero[..currentValue.Length], architecture, out int positive) && positive > 0 &&
                   TryCompare(valueType, currentValue, inputValues[0].Bytes.Span, architecture, out int upper) && upper <= 0;
        }
    }

    private abstract class PreviousComparisonScanType : ScanTypeBase
    {
        public override bool AvailableForFirstScan => false;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 0;
    }

    private sealed class IncreasedValueScanType : PreviousComparisonScanType
    {
        public override string Id => StandardMemoryScanTypeIds.IncreasedValue;
        public override string DisplayName => "Increased Value";
        public override string Description => "Matches values that are greater than their value in the previous scan.";
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType is IMemoryValueComparer && valueType.FixedSize is > 0;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            HasComparablePrevious(valueType, previousValue) && TryCompare(valueType, currentValue, previousValue!.Bytes.Span, architecture, out int comparison) && comparison > 0;
    }

    private sealed class DecreasedValueScanType : PreviousComparisonScanType
    {
        public override string Id => StandardMemoryScanTypeIds.DecreasedValue;
        public override string DisplayName => "Decreased Value";
        public override string Description => "Matches values that are smaller than their value in the previous scan.";
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType is IMemoryValueComparer && valueType.FixedSize is > 0;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            HasComparablePrevious(valueType, previousValue) && TryCompare(valueType, currentValue, previousValue!.Bytes.Span, architecture, out int comparison) && comparison < 0;
    }

    private sealed class ChangedValueScanType : PreviousComparisonScanType
    {
        public override string Id => StandardMemoryScanTypeIds.ChangedValue;
        public override string DisplayName => "Changed Value";
        public override string Description => "Matches values whose stored bytes differ from the previous scan.";
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType?.FixedSize is > 0;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            previousValue is not null && !currentValue.SequenceEqual(previousValue.Bytes.Span);
    }

    private sealed class UnchangedValueScanType : PreviousComparisonScanType
    {
        public override string Id => StandardMemoryScanTypeIds.UnchangedValue;
        public override string DisplayName => "Unchanged Value";
        public override string Description => "Matches values whose stored bytes are identical to the previous scan.";
        public override bool SupportsValueType(IMemoryValueType valueType) => valueType?.FixedSize is > 0;
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            previousValue is not null && currentValue.SequenceEqual(previousValue.Bytes.Span);
    }

    private abstract class DeltaScanType : ScanTypeBase
    {
        public override bool AvailableForFirstScan => false;
        public override bool AvailableForNextScan => true;
        public override int? InputValueCount => 1;
        public override bool SupportsValueType(IMemoryValueType valueType) =>
            valueType is IMemoryValueComparer && valueType.FixedSize is > 0;
    }

    private sealed class IncreasedByScanType : DeltaScanType
    {
        public override string Id => StandardMemoryScanTypeIds.IncreasedBy;
        public override string DisplayName => "Increased By";
        public override string Description => "Matches values that increased by exactly the supplied amount since the previous scan.";
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            previousValue is not null && inputValues.Count == 1 && valueType is IMemoryValueComparer comparer && comparer.IsIncreasedBy(currentValue, previousValue.Bytes.Span, inputValues[0].Bytes.Span, architecture);
    }

    private sealed class DecreasedByScanType : DeltaScanType
    {
        public override string Id => StandardMemoryScanTypeIds.DecreasedBy;
        public override string DisplayName => "Decreased By";
        public override string Description => "Matches values that decreased by exactly the supplied amount since the previous scan.";
        public override bool IsMatch(IMemoryValueType valueType, ReadOnlySpan<byte> currentValue, MemoryScanValue? previousValue, IReadOnlyList<MemoryScanValue> inputValues, TargetArchitecture architecture, MemoryScanStage stage) =>
            previousValue is not null && inputValues.Count == 1 && valueType is IMemoryValueComparer comparer && comparer.IsDecreasedBy(currentValue, previousValue.Bytes.Span, inputValues[0].Bytes.Span, architecture);
    }
}
