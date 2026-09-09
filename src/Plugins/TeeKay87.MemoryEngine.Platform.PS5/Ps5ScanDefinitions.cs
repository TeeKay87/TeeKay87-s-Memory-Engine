using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Platform.PS5;

internal static class Ps5ScanDefinitions
{
    public static IReadOnlyList<IMemoryValueType> ValueTypes { get; } = Array.AsReadOnly(new[]
    {
        StandardMemoryValueTypes.UInt8,
        StandardMemoryValueTypes.Int8,
        StandardMemoryValueTypes.UInt16,
        StandardMemoryValueTypes.Int16,
        StandardMemoryValueTypes.UInt32,
        StandardMemoryValueTypes.Int32,
        StandardMemoryValueTypes.UInt64,
        StandardMemoryValueTypes.Int64,
        StandardMemoryValueTypes.Float32,
        StandardMemoryValueTypes.Float64,
        StandardMemoryValueTypes.ByteArray
    });

    public static IReadOnlyList<IMemoryScanOption> ScanOptions { get; } = Array.AsReadOnly(new IMemoryScanOption[]
    {
        new Ps5ScanOption(
            StandardMemoryScanOptionIds.Endianness,
            "Endianness",
            "Controls how multi-byte scan values are encoded and displayed. Checked uses the native PlayStation 5 little-endian byte order; unchecked uses Big Endian for values stored most-significant byte first.",
            new[]
            {
                new MemoryScanOptionChoice(StandardMemoryScanOptionChoiceIds.LittleEndian, "Little Endian", "Native PlayStation 5 / x86-64 byte order."),
                new MemoryScanOptionChoice(StandardMemoryScanOptionChoiceIds.BigEndian, "Big Endian", "Searches for multi-byte values stored most-significant byte first.")
            },
            StandardMemoryScanOptionChoiceIds.LittleEndian,
            lockAfterFirstScan: true,
            valueType =>
                !string.Equals(valueType.Id, StandardMemoryValueTypeIds.UInt8, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(valueType.Id, StandardMemoryValueTypeIds.Int8, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(valueType.Id, StandardMemoryValueTypeIds.ByteArray, StringComparison.OrdinalIgnoreCase),
            presentationKind: MemoryScanOptionPresentationKind.Toggle,
            toggleLabel: "Little-endian byte order",
            checkedChoiceId: StandardMemoryScanOptionChoiceIds.LittleEndian,
            uncheckedChoiceId: StandardMemoryScanOptionChoiceIds.BigEndian),
        new Ps5ScanOption(
            StandardMemoryScanOptionIds.Alignment,
            "Alignment",
            "Controls the byte step between First Scan candidates. Default uses the selected Value Type's natural alignment. ps5debug-NG carries alignment directly in SCAN_START/TURBOSCAN_START.",
            new[]
            {
                new MemoryScanOptionChoice(StandardMemoryScanOptionChoiceIds.DefaultAlignment, "Default", "Use the selected Value Type's natural alignment."),
                new MemoryScanOptionChoice("1", "1 Byte"),
                new MemoryScanOptionChoice("2", "2 Bytes"),
                new MemoryScanOptionChoice("4", "4 Bytes"),
                new MemoryScanOptionChoice("8", "8 Bytes"),
                new MemoryScanOptionChoice("16", "16 Bytes"),
                new MemoryScanOptionChoice("32", "32 Bytes"),
                new MemoryScanOptionChoice("64", "64 Bytes"),
                new MemoryScanOptionChoice("128", "128 Bytes")
            },
            StandardMemoryScanOptionChoiceIds.DefaultAlignment,
            lockAfterFirstScan: true,
            _ => true),
        new Ps5ScanOption(
            StandardMemoryScanOptionIds.FloatingPointRounding,
            "Floating-point rounding",
            "Controls Exact Value matching for Float and Double. Strict preserves Memory Engine exact semantics; ps5debug-NG tolerance matches the payload's native relative 1e-6 comparison.",
            new[]
            {
                new MemoryScanOptionChoice(StandardMemoryScanOptionChoiceIds.FloatingPointStrict, "Strict", "Use strict numeric equality."),
                new MemoryScanOptionChoice(StandardMemoryScanOptionChoiceIds.FloatingPointRelativeTolerance1E6, "ps5debug-NG tolerance (1e-6)", "Use ps5debug-NG's native relative floating-point tolerance.")
            },
            StandardMemoryScanOptionChoiceIds.FloatingPointStrict,
            lockAfterFirstScan: false,
            valueType =>
                string.Equals(valueType.Id, StandardMemoryValueTypeIds.Float32, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valueType.Id, StandardMemoryValueTypeIds.Float64, StringComparison.OrdinalIgnoreCase),
            supportsScanType: (scanType, _) =>
                string.Equals(scanType.Id, StandardMemoryScanTypeIds.ExactValue, StringComparison.OrdinalIgnoreCase))
    });

    public static string DefaultValueTypeId => StandardMemoryValueTypeIds.Int32;

    private sealed class Ps5ScanOption :
        IMemoryScanOption,
        IMemoryScanOptionPresentation,
        IMemoryScanOptionApplicability
    {
        private readonly Func<IMemoryValueType, bool> _supportsValueType;
        private readonly Func<IMemoryScanType, MemoryScanStage, bool> _supportsScanType;

        public Ps5ScanOption(
            string id,
            string displayName,
            string description,
            IReadOnlyList<MemoryScanOptionChoice> choices,
            string defaultChoiceId,
            bool lockAfterFirstScan,
            Func<IMemoryValueType, bool> supportsValueType,
            MemoryScanOptionPresentationKind presentationKind = MemoryScanOptionPresentationKind.ChoiceList,
            string toggleLabel = "",
            string? checkedChoiceId = null,
            string? uncheckedChoiceId = null,
            Func<IMemoryScanType, MemoryScanStage, bool>? supportsScanType = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultChoiceId);
            ArgumentNullException.ThrowIfNull(supportsValueType);

            Id = id;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            Choices = choices;
            DefaultChoiceId = defaultChoiceId;
            LockAfterFirstScan = lockAfterFirstScan;
            _supportsValueType = supportsValueType;
            PresentationKind = presentationKind;
            ToggleLabel = toggleLabel ?? string.Empty;
            CheckedChoiceId = checkedChoiceId;
            UncheckedChoiceId = uncheckedChoiceId;
            _supportsScanType = supportsScanType ?? ((_, _) => true);
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public IReadOnlyList<MemoryScanOptionChoice> Choices { get; }

        public string DefaultChoiceId { get; }

        public bool LockAfterFirstScan { get; }

        public MemoryScanOptionPresentationKind PresentationKind { get; }

        public string ToggleLabel { get; }

        public string? CheckedChoiceId { get; }

        public string? UncheckedChoiceId { get; }

        public bool SupportsValueType(IMemoryValueType valueType)
        {
            ArgumentNullException.ThrowIfNull(valueType);
            return _supportsValueType(valueType);
        }

        public bool SupportsScanType(IMemoryScanType scanType, MemoryScanStage stage)
        {
            ArgumentNullException.ThrowIfNull(scanType);
            return _supportsScanType(scanType, stage);
        }
    }
}
