using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Scanning;

public static class StandardMemoryValueTypeIds
{
    public const string UInt8 = "standard.uint8";
    public const string Int8 = "standard.int8";
    public const string UInt16 = "standard.uint16";
    public const string Int16 = "standard.int16";
    public const string UInt32 = "standard.uint32";
    public const string Int32 = "standard.int32";
    public const string UInt64 = "standard.uint64";
    public const string Int64 = "standard.int64";
    public const string Float32 = "standard.float32";
    public const string Float64 = "standard.float64";
    public const string ByteArray = "standard.byte-array";
}

public static class StandardMemoryValueTypes
{
    public const int MaximumByteArrayLength = 4096;

    public static IMemoryValueType UInt8 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.UInt8,
        "1 Byte (Unsigned)",
        "Integer",
        "Unsigned 8-bit integer.",
        "Enter an unsigned integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.UInt8,
        fixedSize: 1,
        defaultAlignment: 1);

    public static IMemoryValueType Int8 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Int8,
        "1 Byte",
        "Integer",
        "Signed 8-bit integer.",
        "Enter a signed integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.Int8,
        fixedSize: 1,
        defaultAlignment: 1);

    public static IMemoryValueType UInt16 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.UInt16,
        "2 Bytes (Unsigned)",
        "Integer",
        "Unsigned 16-bit integer.",
        "Enter an unsigned integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.UInt16,
        fixedSize: 2,
        defaultAlignment: 2);

    public static IMemoryValueType Int16 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Int16,
        "2 Bytes",
        "Integer",
        "Signed 16-bit integer.",
        "Enter a signed integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.Int16,
        fixedSize: 2,
        defaultAlignment: 2);

    public static IMemoryValueType UInt32 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.UInt32,
        "4 Bytes (Unsigned)",
        "Integer",
        "Unsigned 32-bit integer.",
        "Enter an unsigned integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.UInt32,
        fixedSize: 4,
        defaultAlignment: 4);

    public static IMemoryValueType Int32 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Int32,
        "4 Bytes",
        "Integer",
        "Signed 32-bit integer.",
        "Enter a signed integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.Int32,
        fixedSize: 4,
        defaultAlignment: 4);

    public static IMemoryValueType UInt64 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.UInt64,
        "8 Bytes (Unsigned)",
        "Integer",
        "Unsigned 64-bit integer.",
        "Enter an unsigned integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.UInt64,
        fixedSize: 8,
        defaultAlignment: 8);

    public static IMemoryValueType Int64 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Int64,
        "8 Bytes",
        "Integer",
        "Signed 64-bit integer.",
        "Enter a signed integer in decimal or 0x-prefixed hexadecimal form.",
        StandardValueKind.Int64,
        fixedSize: 8,
        defaultAlignment: 8);

    public static IMemoryValueType Float32 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Float32,
        "Float",
        "Floating Point",
        "IEEE 754 32-bit floating-point value.",
        "Enter a 32-bit floating-point value using invariant decimal notation.",
        StandardValueKind.Float32,
        fixedSize: 4,
        defaultAlignment: 4);

    public static IMemoryValueType Float64 { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.Float64,
        "Double",
        "Floating Point",
        "IEEE 754 64-bit floating-point value.",
        "Enter a 64-bit floating-point value using invariant decimal notation.",
        StandardValueKind.Float64,
        fixedSize: 8,
        defaultAlignment: 8);

    public static IMemoryValueType ByteArray { get; } = new StandardMemoryValueType(
        StandardMemoryValueTypeIds.ByteArray,
        "Array of Bytes",
        "Bytes",
        "Raw byte-sequence scanning.",
        $"Enter hexadecimal bytes such as DE AD BE EF or DEADBEEF. Maximum length: {MaximumByteArrayLength:N0} bytes.",
        StandardValueKind.ByteArray,
        fixedSize: null,
        defaultAlignment: 1);

    public static IReadOnlyList<IMemoryValueType> NumericAndByteArray { get; } = Array.AsReadOnly(new[]
    {
        UInt8,
        Int8,
        UInt16,
        Int16,
        UInt32,
        Int32,
        UInt64,
        Int64,
        Float32,
        Float64,
        ByteArray
    });

    private enum StandardValueKind
    {
        UInt8,
        Int8,
        UInt16,
        Int16,
        UInt32,
        Int32,
        UInt64,
        Int64,
        Float32,
        Float64,
        ByteArray
    }

    private sealed class StandardMemoryValueType : IMemoryValueType, IMemoryValueInputPolicy, IMemoryValueComparer
    {
        private readonly StandardValueKind _kind;

        public StandardMemoryValueType(
            string id,
            string displayName,
            string category,
            string description,
            string inputDescription,
            StandardValueKind kind,
            int? fixedSize,
            int defaultAlignment)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            InputDescription = inputDescription;
            _kind = kind;
            FixedSize = fixedSize;
            DefaultAlignment = defaultAlignment;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public string InputDescription { get; }

        public int? FixedSize { get; }

        public int DefaultAlignment { get; }

        public bool IsPotentiallyValidInput(string text)
        {
            string candidate = text ?? string.Empty;

            return _kind switch
            {
                StandardValueKind.UInt8 or
                StandardValueKind.UInt16 or
                StandardValueKind.UInt32 or
                StandardValueKind.UInt64 => IsPotentialUnsignedIntegerInput(candidate),

                StandardValueKind.Int8 or
                StandardValueKind.Int16 or
                StandardValueKind.Int32 or
                StandardValueKind.Int64 => IsPotentialSignedIntegerInput(candidate),

                StandardValueKind.Float32 or
                StandardValueKind.Float64 => IsPotentialFloatingPointInput(candidate),

                StandardValueKind.ByteArray => IsPotentialByteArrayInput(candidate),
                _ => true
            };
        }

        public bool TryParse(
            string text,
            TargetArchitecture architecture,
            out MemoryScanValue? value,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(architecture);

            string candidate = text?.Trim() ?? string.Empty;
            if (candidate.Length == 0)
            {
                value = null;
                error = $"Enter a value for {DisplayName}.";
                return false;
            }

            try
            {
                switch (_kind)
                {
                    case StandardValueKind.UInt8:
                        if (!TryParseUnsigned(candidate, byte.MaxValue, out ulong uint8))
                        {
                            return Fail("Enter an unsigned 8-bit value from 0 to 255.", out value, out error);
                        }
                        return Success(new[] { (byte)uint8 }, uint8.ToString(CultureInfo.InvariantCulture), 1, out value, out error);

                    case StandardValueKind.Int8:
                        if (!TryParseSigned(candidate, sbyte.MinValue, sbyte.MaxValue, byte.MaxValue, 8, out long int8))
                        {
                            return Fail("Enter a signed 8-bit value from -128 to 127, or an 8-bit 0x-prefixed hexadecimal value.", out value, out error);
                        }
                        sbyte signed8 = (sbyte)int8;
                        return Success(new[] { unchecked((byte)signed8) }, signed8.ToString(CultureInfo.InvariantCulture), 1, out value, out error);

                    case StandardValueKind.UInt16:
                        if (!TryParseUnsigned(candidate, ushort.MaxValue, out ulong uint16))
                        {
                            return Fail("Enter an unsigned 16-bit value from 0 to 65535.", out value, out error);
                        }
                        return Success(EncodeUInt16((ushort)uint16, architecture.Endianness), uint16.ToString(CultureInfo.InvariantCulture), 2, out value, out error);

                    case StandardValueKind.Int16:
                        if (!TryParseSigned(candidate, short.MinValue, short.MaxValue, ushort.MaxValue, 16, out long int16))
                        {
                            return Fail("Enter a signed 16-bit value from -32768 to 32767, or a 16-bit 0x-prefixed hexadecimal value.", out value, out error);
                        }
                        return Success(EncodeInt16((short)int16, architecture.Endianness), ((short)int16).ToString(CultureInfo.InvariantCulture), 2, out value, out error);

                    case StandardValueKind.UInt32:
                        if (!TryParseUnsigned(candidate, uint.MaxValue, out ulong uint32))
                        {
                            return Fail("Enter an unsigned 32-bit value from 0 to 4294967295.", out value, out error);
                        }
                        return Success(EncodeUInt32((uint)uint32, architecture.Endianness), uint32.ToString(CultureInfo.InvariantCulture), 4, out value, out error);

                    case StandardValueKind.Int32:
                        if (!TryParseSigned(candidate, int.MinValue, int.MaxValue, uint.MaxValue, 32, out long int32))
                        {
                            return Fail("Enter a signed 32-bit value, or a 32-bit 0x-prefixed hexadecimal value.", out value, out error);
                        }
                        return Success(EncodeInt32((int)int32, architecture.Endianness), ((int)int32).ToString(CultureInfo.InvariantCulture), 4, out value, out error);

                    case StandardValueKind.UInt64:
                        if (!TryParseUnsigned(candidate, ulong.MaxValue, out ulong uint64))
                        {
                            return Fail("Enter an unsigned 64-bit value.", out value, out error);
                        }
                        return Success(EncodeUInt64(uint64, architecture.Endianness), uint64.ToString(CultureInfo.InvariantCulture), 8, out value, out error);

                    case StandardValueKind.Int64:
                        if (!TryParseSigned64(candidate, out long int64))
                        {
                            return Fail("Enter a signed 64-bit value, or a 64-bit 0x-prefixed hexadecimal value.", out value, out error);
                        }
                        return Success(EncodeInt64(int64, architecture.Endianness), int64.ToString(CultureInfo.InvariantCulture), 8, out value, out error);

                    case StandardValueKind.Float32:
                        if (!float.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out float float32))
                        {
                            return Fail("Enter a valid 32-bit floating-point value using '.' as the decimal separator.", out value, out error);
                        }
                        return Success(EncodeFloat32(float32, architecture.Endianness), float32.ToString("R", CultureInfo.InvariantCulture), 4, out value, out error);

                    case StandardValueKind.Float64:
                        if (!double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double float64))
                        {
                            return Fail("Enter a valid 64-bit floating-point value using '.' as the decimal separator.", out value, out error);
                        }
                        return Success(EncodeFloat64(float64, architecture.Endianness), float64.ToString("R", CultureInfo.InvariantCulture), 8, out value, out error);

                    case StandardValueKind.ByteArray:
                        if (!TryParseByteArray(candidate, out byte[]? bytes, out string byteArrayError) || bytes is null)
                        {
                            return Fail(byteArrayError, out value, out error);
                        }
                        return Success(bytes, FormatByteArray(bytes), 1, out value, out error);

                    default:
                        return Fail($"{DisplayName} does not provide a standard parser.", out value, out error);
                }
            }
            catch (OverflowException)
            {
                return Fail($"The entered value is outside the range supported by {DisplayName}.", out value, out error);
            }
        }

        public bool TryResolveScanShape(
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            out int valueSize,
            out int alignment,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(inputValues);
            ArgumentNullException.ThrowIfNull(architecture);

            if (FixedSize is int fixedSize)
            {
                valueSize = fixedSize;
                alignment = DefaultAlignment;
                error = string.Empty;
                return true;
            }

            MemoryScanValue? input = inputValues.FirstOrDefault();
            if (input is null || !string.Equals(input.ValueTypeId, Id, StringComparison.OrdinalIgnoreCase))
            {
                valueSize = 0;
                alignment = 0;
                error = $"{DisplayName} requires a parsed input value to determine its scan width.";
                return false;
            }

            valueSize = input.Size;
            alignment = input.Alignment;
            error = string.Empty;
            return true;
        }

        public MemoryScanValue CreateValue(
            ReadOnlySpan<byte> bytes,
            int alignment,
            TargetArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(architecture);

            if (bytes.IsEmpty)
            {
                throw new ArgumentException("A scan value must contain at least one byte.", nameof(bytes));
            }

            if (FixedSize is int fixedSize && bytes.Length != fixedSize)
            {
                throw new ArgumentException(
                    $"{DisplayName} requires exactly {fixedSize} byte(s), but {bytes.Length} were supplied.",
                    nameof(bytes));
            }

            string displayText = _kind switch
            {
                StandardValueKind.UInt8 => bytes[0].ToString(CultureInfo.InvariantCulture),
                StandardValueKind.Int8 => unchecked((sbyte)bytes[0]).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.UInt16 => ReadUInt16(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.Int16 => ReadInt16(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.UInt32 => ReadUInt32(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.Int32 => ReadInt32(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.UInt64 => ReadUInt64(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.Int64 => ReadInt64(bytes, architecture.Endianness).ToString(CultureInfo.InvariantCulture),
                StandardValueKind.Float32 => ReadFloat32(bytes, architecture.Endianness).ToString("R", CultureInfo.InvariantCulture),
                StandardValueKind.Float64 => ReadFloat64(bytes, architecture.Endianness).ToString("R", CultureInfo.InvariantCulture),
                StandardValueKind.ByteArray => FormatByteArray(bytes),
                _ => throw new InvalidOperationException($"{DisplayName} does not provide a standard formatter.")
            };

            return new MemoryScanValue(Id, DisplayName, bytes, displayText, alignment);
        }

        public bool ValuesEqual(
            ReadOnlySpan<byte> left,
            ReadOnlySpan<byte> right,
            TargetArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(architecture);

            if (left.Length != right.Length)
            {
                return false;
            }

            return _kind switch
            {
                StandardValueKind.Float32 when left.Length == sizeof(float) =>
                    ReadFloat32(left, architecture.Endianness) == ReadFloat32(right, architecture.Endianness),
                StandardValueKind.Float64 when left.Length == sizeof(double) =>
                    ReadFloat64(left, architecture.Endianness) == ReadFloat64(right, architecture.Endianness),
                _ => left.SequenceEqual(right)
            };
        }

        public bool TryCompare(
            ReadOnlySpan<byte> left,
            ReadOnlySpan<byte> right,
            TargetArchitecture architecture,
            out int comparison)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            if (_kind == StandardValueKind.ByteArray || left.Length != right.Length)
            {
                comparison = 0;
                return false;
            }

            if (_kind == StandardValueKind.Float32)
            {
                float leftValue = ReadFloat32(left, architecture.Endianness);
                float rightValue = ReadFloat32(right, architecture.Endianness);
                if (float.IsNaN(leftValue) || float.IsNaN(rightValue))
                {
                    comparison = 0;
                    return false;
                }

                comparison = leftValue.CompareTo(rightValue);
                return true;
            }

            if (_kind == StandardValueKind.Float64)
            {
                double leftValue = ReadFloat64(left, architecture.Endianness);
                double rightValue = ReadFloat64(right, architecture.Endianness);
                if (double.IsNaN(leftValue) || double.IsNaN(rightValue))
                {
                    comparison = 0;
                    return false;
                }

                comparison = leftValue.CompareTo(rightValue);
                return true;
            }

            comparison = CompareNumeric(left, right, architecture.Endianness);
            return true;
        }

        public bool IsIncreasedBy(
            ReadOnlySpan<byte> current,
            ReadOnlySpan<byte> previous,
            ReadOnlySpan<byte> amount,
            TargetArchitecture architecture)
        {
            return IsDeltaMatch(current, previous, amount, architecture, increase: true);
        }

        public bool IsDecreasedBy(
            ReadOnlySpan<byte> current,
            ReadOnlySpan<byte> previous,
            ReadOnlySpan<byte> amount,
            TargetArchitecture architecture)
        {
            return IsDeltaMatch(current, previous, amount, architecture, increase: false);
        }

        private int CompareNumeric(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, Endianness endianness)
        {
            return _kind switch
            {
                StandardValueKind.UInt8 => left[0].CompareTo(right[0]),
                StandardValueKind.Int8 => unchecked((sbyte)left[0]).CompareTo(unchecked((sbyte)right[0])),
                StandardValueKind.UInt16 => ReadUInt16(left, endianness).CompareTo(ReadUInt16(right, endianness)),
                StandardValueKind.Int16 => ReadInt16(left, endianness).CompareTo(ReadInt16(right, endianness)),
                StandardValueKind.UInt32 => ReadUInt32(left, endianness).CompareTo(ReadUInt32(right, endianness)),
                StandardValueKind.Int32 => ReadInt32(left, endianness).CompareTo(ReadInt32(right, endianness)),
                StandardValueKind.UInt64 => ReadUInt64(left, endianness).CompareTo(ReadUInt64(right, endianness)),
                StandardValueKind.Int64 => ReadInt64(left, endianness).CompareTo(ReadInt64(right, endianness)),
                StandardValueKind.Float32 => ReadFloat32(left, endianness).CompareTo(ReadFloat32(right, endianness)),
                StandardValueKind.Float64 => ReadFloat64(left, endianness).CompareTo(ReadFloat64(right, endianness)),
                _ => 0
            };
        }

        private bool IsDeltaMatch(
            ReadOnlySpan<byte> current,
            ReadOnlySpan<byte> previous,
            ReadOnlySpan<byte> amount,
            TargetArchitecture architecture,
            bool increase)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            if (_kind == StandardValueKind.ByteArray ||
                current.Length != previous.Length ||
                current.Length != amount.Length)
            {
                return false;
            }

            Endianness endianness = architecture.Endianness;
            if (_kind is StandardValueKind.Float32)
            {
                float currentValue = ReadFloat32(current, endianness);
                float previousValue = ReadFloat32(previous, endianness);
                float amountValue = ReadFloat32(amount, endianness);
                return amountValue >= 0 && (increase
                    ? currentValue > previousValue && currentValue - previousValue == amountValue
                    : currentValue < previousValue && previousValue - currentValue == amountValue);
            }

            if (_kind is StandardValueKind.Float64)
            {
                double currentValue = ReadFloat64(current, endianness);
                double previousValue = ReadFloat64(previous, endianness);
                double amountValue = ReadFloat64(amount, endianness);
                return amountValue >= 0 && (increase
                    ? currentValue > previousValue && currentValue - previousValue == amountValue
                    : currentValue < previousValue && previousValue - currentValue == amountValue);
            }

            decimal currentValueDecimal = ReadIntegerAsDecimal(current, endianness);
            decimal previousValueDecimal = ReadIntegerAsDecimal(previous, endianness);
            decimal amountValueDecimal = ReadIntegerAsDecimal(amount, endianness);
            return amountValueDecimal >= 0 && (increase
                ? currentValueDecimal > previousValueDecimal && currentValueDecimal - previousValueDecimal == amountValueDecimal
                : currentValueDecimal < previousValueDecimal && previousValueDecimal - currentValueDecimal == amountValueDecimal);
        }

        private decimal ReadIntegerAsDecimal(ReadOnlySpan<byte> bytes, Endianness endianness)
        {
            return _kind switch
            {
                StandardValueKind.UInt8 => bytes[0],
                StandardValueKind.Int8 => unchecked((sbyte)bytes[0]),
                StandardValueKind.UInt16 => ReadUInt16(bytes, endianness),
                StandardValueKind.Int16 => ReadInt16(bytes, endianness),
                StandardValueKind.UInt32 => ReadUInt32(bytes, endianness),
                StandardValueKind.Int32 => ReadInt32(bytes, endianness),
                StandardValueKind.UInt64 => ReadUInt64(bytes, endianness),
                StandardValueKind.Int64 => ReadInt64(bytes, endianness),
                _ => throw new InvalidOperationException($"{DisplayName} does not support integer delta comparison.")
            };
        }

        private bool Success(
            ReadOnlySpan<byte> bytes,
            string displayText,
            int alignment,
            out MemoryScanValue? value,
            out string error)
        {
            value = new MemoryScanValue(Id, DisplayName, bytes, displayText, alignment);
            error = string.Empty;
            return true;
        }
    }

    private static bool IsPotentialUnsignedIntegerInput(string text)
    {
        if (text.Length == 0 || text == "+")
        {
            return true;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return text.Length == 2 || text[2..].All(IsHexDigit);
        }

        int startIndex = text[0] == '+' ? 1 : 0;
        return startIndex < text.Length && text[startIndex..].All(char.IsAsciiDigit);
    }

    private static bool IsPotentialSignedIntegerInput(string text)
    {
        if (text.Length == 0 || text is "+" or "-")
        {
            return true;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return text.Length == 2 || text[2..].All(IsHexDigit);
        }

        int startIndex = text[0] is '+' or '-' ? 1 : 0;
        return startIndex < text.Length && text[startIndex..].All(char.IsAsciiDigit);
    }

    private static bool IsPotentialFloatingPointInput(string text)
    {
        if (text.Length == 0 || text is "+" or "-" or "." or "+." or "-.")
        {
            return true;
        }

        string[] specialValues = ["NaN", "Infinity", "+Infinity", "-Infinity"];
        if (specialValues.Any(value => value.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        bool exponentSeen = false;
        bool decimalPointSeen = false;
        bool mantissaDigitSeen = false;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (char.IsAsciiDigit(character))
            {
                if (!exponentSeen)
                {
                    mantissaDigitSeen = true;
                }

                continue;
            }

            if (character is '+' or '-')
            {
                if (index == 0 || (exponentSeen && index > 0 && (text[index - 1] == 'e' || text[index - 1] == 'E')))
                {
                    continue;
                }

                return false;
            }

            if (character == '.')
            {
                if (exponentSeen || decimalPointSeen)
                {
                    return false;
                }

                decimalPointSeen = true;
                continue;
            }

            if (character is 'e' or 'E')
            {
                if (exponentSeen || !mantissaDigitSeen)
                {
                    return false;
                }

                exponentSeen = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsPotentialByteArrayInput(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (IsHexDigit(character) || char.IsWhiteSpace(character) || character is ',' or '-')
            {
                continue;
            }

            if (character is 'x' or 'X')
            {
                if (index > 0 && text[index - 1] == '0' && (index == 1 || IsByteArraySeparator(text[index - 2])))
                {
                    continue;
                }
            }

            return false;
        }

        return true;
    }

    private static bool IsByteArraySeparator(char character)
    {
        return char.IsWhiteSpace(character) || character is ',' or '-';
    }

    private static bool IsHexDigit(char character)
    {
        return char.IsAsciiDigit(character) ||
               character is >= 'a' and <= 'f' ||
               character is >= 'A' and <= 'F';
    }

    private static bool Fail(string message, out MemoryScanValue? value, out string error)
    {
        value = null;
        error = message;
        return false;
    }

    private static bool TryParseUnsigned(string text, ulong maximum, out ulong value)
    {
        if (TryGetHexDigits(text, out string? hex))
        {
            return ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value) &&
                   value <= maximum;
        }

        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
               value <= maximum;
    }

    private static bool TryParseSigned(
        string text,
        long minimum,
        long maximum,
        ulong hexadecimalMaximum,
        int bitWidth,
        out long value)
    {
        if (TryGetHexDigits(text, out string? hex))
        {
            if (!ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong raw) ||
                raw > hexadecimalMaximum)
            {
                value = 0;
                return false;
            }

            ulong signBit = 1UL << (bitWidth - 1);
            ulong fullRange = 1UL << bitWidth;
            value = (raw & signBit) == 0
                ? checked((long)raw)
                : unchecked((long)(raw - fullRange));
            return value >= minimum && value <= maximum;
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
               value >= minimum && value <= maximum;
    }

    private static bool TryParseSigned64(string text, out long value)
    {
        if (TryGetHexDigits(text, out string? hex))
        {
            if (ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong raw))
            {
                value = unchecked((long)raw);
                return true;
            }

            value = 0;
            return false;
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetHexDigits(string text, out string? hex)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = text[2..];
            return hex.Length > 0;
        }

        hex = null;
        return false;
    }

    private static bool TryParseByteArray(string text, out byte[]? bytes, out string error)
    {
        string normalized = text
            .Replace(',', ' ')
            .Replace('-', ' ');
        string[] tokens = normalized.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 1)
        {
            string compact = tokens[0];
            if (compact.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compact = compact[2..];
            }

            if (compact.Length > 2)
            {
                if ((compact.Length & 1) != 0)
                {
                    bytes = null;
                    error = "A compact byte array must contain an even number of hexadecimal digits.";
                    return false;
                }

                tokens = Enumerable.Range(0, compact.Length / 2)
                    .Select(index => compact.Substring(index * 2, 2))
                    .ToArray();
            }
        }

        if (tokens.Length == 0)
        {
            bytes = null;
            error = "Enter at least one hexadecimal byte.";
            return false;
        }

        if (tokens.Length > MaximumByteArrayLength)
        {
            bytes = null;
            error = $"Array of Bytes is currently limited to {MaximumByteArrayLength:N0} bytes per scan value.";
            return false;
        }

        byte[] result = new byte[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                token = token[2..];
            }

            if (token.Length is < 1 or > 2 ||
                !byte.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result[index]))
            {
                bytes = null;
                error = $"'{tokens[index]}' is not a valid hexadecimal byte.";
                return false;
            }
        }

        bytes = result;
        error = string.Empty;
        return true;
    }

    private static string FormatByteArray(ReadOnlySpan<byte> bytes)
    {
        return string.Join(" ", bytes.ToArray().Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static byte[] EncodeUInt16(ushort value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(ushort)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeInt16(short value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(short)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeUInt32(uint value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(uint)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeInt32(int value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(int)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeUInt64(ulong value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeInt64(long value, Endianness endianness)
    {
        byte[] bytes = new byte[sizeof(long)];
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        }
        return bytes;
    }

    private static byte[] EncodeFloat32(float value, Endianness endianness)
    {
        return EncodeInt32(BitConverter.SingleToInt32Bits(value), endianness);
    }

    private static byte[] EncodeFloat64(double value, Endianness endianness)
    {
        return EncodeInt64(BitConverter.DoubleToInt64Bits(value), endianness);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static short ReadInt16(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt16BigEndian(bytes)
            : BinaryPrimitives.ReadInt16LittleEndian(bytes);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(bytes)
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(bytes)
            : BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt64BigEndian(bytes)
            : BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static long ReadInt64(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt64BigEndian(bytes)
            : BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    private static float ReadFloat32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return BitConverter.Int32BitsToSingle(ReadInt32(bytes, endianness));
    }

    private static double ReadFloat64(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        return BitConverter.Int64BitsToDouble(ReadInt64(bytes, endianness));
    }
}
