using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public static class MemoryScanValueCodec
{
    public const int MaximumByteArrayLength = 4096;

    private static readonly IReadOnlyList<MemoryValueType> ExactValueTypes = Array.AsReadOnly(new[]
    {
        MemoryValueType.UInt8,
        MemoryValueType.Int8,
        MemoryValueType.UInt16,
        MemoryValueType.Int16,
        MemoryValueType.UInt32,
        MemoryValueType.Int32,
        MemoryValueType.UInt64,
        MemoryValueType.Int64,
        MemoryValueType.Float32,
        MemoryValueType.Float64,
        MemoryValueType.ByteArray
    });

    public static IReadOnlyList<MemoryValueType> SupportedExactValueTypes => ExactValueTypes;

    public static bool IsSupportedExactValueType(MemoryValueType valueType)
    {
        return ExactValueTypes.Contains(valueType);
    }

    public static string GetDisplayName(MemoryValueType valueType)
    {
        return valueType switch
        {
            MemoryValueType.UInt8 => "1 Byte (Unsigned)",
            MemoryValueType.Int8 => "1 Byte (Signed)",
            MemoryValueType.UInt16 => "2 Bytes (Unsigned)",
            MemoryValueType.Int16 => "2 Bytes (Signed)",
            MemoryValueType.UInt32 => "4 Bytes (Unsigned)",
            MemoryValueType.Int32 => "4 Bytes (Signed)",
            MemoryValueType.UInt64 => "8 Bytes (Unsigned)",
            MemoryValueType.Int64 => "8 Bytes (Signed)",
            MemoryValueType.Float32 => "Float",
            MemoryValueType.Float64 => "Double",
            MemoryValueType.ByteArray => "Array of Bytes",
            _ => valueType.ToString()
        };
    }

    public static string GetInputDescription(MemoryValueType valueType)
    {
        return valueType switch
        {
            MemoryValueType.Int8 or MemoryValueType.Int16 or MemoryValueType.Int32 or MemoryValueType.Int64 =>
                "Enter a signed integer in decimal or 0x-prefixed hexadecimal form.",
            MemoryValueType.UInt8 or MemoryValueType.UInt16 or MemoryValueType.UInt32 or MemoryValueType.UInt64 =>
                "Enter an unsigned integer in decimal or 0x-prefixed hexadecimal form.",
            MemoryValueType.Float32 =>
                "Enter a 32-bit floating-point value using invariant decimal notation.",
            MemoryValueType.Float64 =>
                "Enter a 64-bit floating-point value using invariant decimal notation.",
            MemoryValueType.ByteArray =>
                $"Enter hexadecimal bytes such as DE AD BE EF or DEADBEEF. Maximum length: {MaximumByteArrayLength:N0} bytes.",
            _ => "Enter a value supported by the selected value type."
        };
    }

    public static bool TryParse(
        string text,
        MemoryValueType valueType,
        TargetArchitecture architecture,
        out MemoryScanValue? value,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(architecture);

        string candidate = text?.Trim() ?? string.Empty;
        if (candidate.Length == 0)
        {
            value = null;
            error = $"Enter a value for {GetDisplayName(valueType)}.";
            return false;
        }

        if (!IsSupportedExactValueType(valueType))
        {
            value = null;
            error = $"{GetDisplayName(valueType)} is not supported by the current Exact Value scanner.";
            return false;
        }

        try
        {
            switch (valueType)
            {
                case MemoryValueType.UInt8:
                    if (!TryParseUnsigned(candidate, byte.MaxValue, out ulong uint8))
                    {
                        return Fail("Enter an unsigned 8-bit value from 0 to 255.", out value, out error);
                    }
                    return Success(valueType, new[] { (byte)uint8 }, uint8.ToString(CultureInfo.InvariantCulture), 1, out value, out error);

                case MemoryValueType.Int8:
                    if (!TryParseSigned(candidate, sbyte.MinValue, sbyte.MaxValue, byte.MaxValue, 8, out long int8))
                    {
                        return Fail("Enter a signed 8-bit value from -128 to 127, or an 8-bit 0x-prefixed hexadecimal value.", out value, out error);
                    }
                    return Success(valueType, new[] { unchecked((byte)(sbyte)int8) }, ((sbyte)int8).ToString(CultureInfo.InvariantCulture), 1, out value, out error);

                case MemoryValueType.UInt16:
                    if (!TryParseUnsigned(candidate, ushort.MaxValue, out ulong uint16))
                    {
                        return Fail("Enter an unsigned 16-bit value from 0 to 65535.", out value, out error);
                    }
                    return Success(valueType, EncodeUInt16((ushort)uint16, architecture.Endianness), uint16.ToString(CultureInfo.InvariantCulture), 2, out value, out error);

                case MemoryValueType.Int16:
                    if (!TryParseSigned(candidate, short.MinValue, short.MaxValue, ushort.MaxValue, 16, out long int16))
                    {
                        return Fail("Enter a signed 16-bit value from -32768 to 32767, or a 16-bit 0x-prefixed hexadecimal value.", out value, out error);
                    }
                    return Success(valueType, EncodeInt16((short)int16, architecture.Endianness), ((short)int16).ToString(CultureInfo.InvariantCulture), 2, out value, out error);

                case MemoryValueType.UInt32:
                    if (!TryParseUnsigned(candidate, uint.MaxValue, out ulong uint32))
                    {
                        return Fail("Enter an unsigned 32-bit value from 0 to 4294967295.", out value, out error);
                    }
                    return Success(valueType, EncodeUInt32((uint)uint32, architecture.Endianness), uint32.ToString(CultureInfo.InvariantCulture), 4, out value, out error);

                case MemoryValueType.Int32:
                    if (!TryParseSigned(candidate, int.MinValue, int.MaxValue, uint.MaxValue, 32, out long int32))
                    {
                        return Fail("Enter a signed 32-bit value, or a 32-bit 0x-prefixed hexadecimal value.", out value, out error);
                    }
                    return Success(valueType, EncodeInt32((int)int32, architecture.Endianness), ((int)int32).ToString(CultureInfo.InvariantCulture), 4, out value, out error);

                case MemoryValueType.UInt64:
                    if (!TryParseUnsigned(candidate, ulong.MaxValue, out ulong uint64))
                    {
                        return Fail("Enter an unsigned 64-bit value.", out value, out error);
                    }
                    return Success(valueType, EncodeUInt64(uint64, architecture.Endianness), uint64.ToString(CultureInfo.InvariantCulture), 8, out value, out error);

                case MemoryValueType.Int64:
                    if (!TryParseSigned64(candidate, out long int64))
                    {
                        return Fail("Enter a signed 64-bit value, or a 64-bit 0x-prefixed hexadecimal value.", out value, out error);
                    }
                    return Success(valueType, EncodeInt64(int64, architecture.Endianness), int64.ToString(CultureInfo.InvariantCulture), 8, out value, out error);

                case MemoryValueType.Float32:
                    if (!float.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out float float32))
                    {
                        return Fail("Enter a valid 32-bit floating-point value using '.' as the decimal separator.", out value, out error);
                    }
                    return Success(valueType, EncodeFloat32(float32, architecture.Endianness), float32.ToString("R", CultureInfo.InvariantCulture), 4, out value, out error);

                case MemoryValueType.Float64:
                    if (!double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double float64))
                    {
                        return Fail("Enter a valid 64-bit floating-point value using '.' as the decimal separator.", out value, out error);
                    }
                    return Success(valueType, EncodeFloat64(float64, architecture.Endianness), float64.ToString("R", CultureInfo.InvariantCulture), 8, out value, out error);

                case MemoryValueType.ByteArray:
                    if (!TryParseByteArray(candidate, out byte[]? bytes, out string byteArrayError) || bytes is null)
                    {
                        return Fail(byteArrayError, out value, out error);
                    }
                    return Success(valueType, bytes, string.Join(" ", bytes.Select(item => item.ToString("X2", CultureInfo.InvariantCulture))), 1, out value, out error);

                default:
                    return Fail($"{GetDisplayName(valueType)} is not supported by the current Exact Value scanner.", out value, out error);
            }
        }
        catch (OverflowException)
        {
            return Fail($"The entered value is outside the range supported by {GetDisplayName(valueType)}.", out value, out error);
        }
    }

    public static bool MatchesExact(
        ReadOnlySpan<byte> memoryBytes,
        MemoryScanValue targetValue,
        Endianness endianness)
    {
        ArgumentNullException.ThrowIfNull(targetValue);

        if (memoryBytes.Length != targetValue.Size)
        {
            return false;
        }

        return targetValue.ValueType switch
        {
            MemoryValueType.Float32 =>
                ReadFloat32(memoryBytes, endianness) == ReadFloat32(targetValue.Bytes.Span, endianness),
            MemoryValueType.Float64 =>
                ReadFloat64(memoryBytes, endianness) == ReadFloat64(targetValue.Bytes.Span, endianness),
            _ => memoryBytes.SequenceEqual(targetValue.Bytes.Span)
        };
    }

    private static bool Success(
        MemoryValueType valueType,
        ReadOnlySpan<byte> bytes,
        string displayText,
        int alignment,
        out MemoryScanValue? value,
        out string error)
    {
        value = new MemoryScanValue(valueType, bytes, displayText, alignment);
        error = string.Empty;
        return true;
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
        string[] tokens = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    private static float ReadFloat32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        int bits = endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(bytes)
            : BinaryPrimitives.ReadInt32LittleEndian(bytes);
        return BitConverter.Int32BitsToSingle(bits);
    }

    private static double ReadFloat64(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        long bits = endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt64BigEndian(bytes)
            : BinaryPrimitives.ReadInt64LittleEndian(bytes);
        return BitConverter.Int64BitsToDouble(bits);
    }
}
