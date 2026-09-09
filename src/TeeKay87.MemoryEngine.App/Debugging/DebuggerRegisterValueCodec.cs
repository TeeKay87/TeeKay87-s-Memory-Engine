using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.Debugging;

internal static class DebuggerRegisterValueCodec
{
    public static string Format(DebuggerRegister register)
    {
        ArgumentNullException.ThrowIfNull(register);

        ReadOnlySpan<byte> value = register.Value.Span;
        if (register.ValueEncoding == DebuggerRegisterValueEncoding.Bytes)
        {
            return string.Join(" ", value.ToArray().Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
        }

        BigInteger numericValue = new(
            value,
            isUnsigned: true,
            isBigEndian: register.ValueEncoding == DebuggerRegisterValueEncoding.UnsignedBigEndian);
        int hexDigits = checked((register.BitWidth + 3) / 4);
        return $"0x{numericValue.ToString($"X{hexDigits}", CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(
        DebuggerRegister register,
        string? text,
        out byte[] value,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(register);

        return register.ValueEncoding switch
        {
            DebuggerRegisterValueEncoding.Bytes => TryParseBytes(register, text, out value, out error),
            DebuggerRegisterValueEncoding.UnsignedLittleEndian => TryParseUnsigned(register, text, isBigEndian: false, out value, out error),
            DebuggerRegisterValueEncoding.UnsignedBigEndian => TryParseUnsigned(register, text, isBigEndian: true, out value, out error),
            _ => Fail("The register uses an unsupported value encoding.", out value, out error)
        };
    }

    public static bool TryGetUInt64(DebuggerRegister register, out ulong value)
    {
        ArgumentNullException.ThrowIfNull(register);

        value = 0;
        if (register.BitWidth > 64 ||
            register.ValueEncoding is not (DebuggerRegisterValueEncoding.UnsignedLittleEndian or DebuggerRegisterValueEncoding.UnsignedBigEndian))
        {
            return false;
        }

        BigInteger numericValue = new(
            register.Value.Span,
            isUnsigned: true,
            isBigEndian: register.ValueEncoding == DebuggerRegisterValueEncoding.UnsignedBigEndian);
        if (numericValue < BigInteger.Zero || numericValue > ulong.MaxValue)
        {
            return false;
        }

        value = (ulong)numericValue;
        return true;
    }

    private static bool TryParseBytes(
        DebuggerRegister register,
        string? text,
        out byte[] value,
        out string error)
    {
        string compact = string.Concat((text ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
        int expectedByteCount = register.Value.Length;
        int expectedHexCharacters = checked(expectedByteCount * 2);

        if (compact.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            compact = compact[2..];
        }

        compact = compact.Replace("_", string.Empty, StringComparison.Ordinal);
        if (compact.Length != expectedHexCharacters)
        {
            return Fail(
                $"Enter exactly {expectedByteCount:N0} bytes ({expectedHexCharacters:N0} hexadecimal characters).",
                out value,
                out error);
        }

        value = new byte[expectedByteCount];
        for (int index = 0; index < value.Length; index++)
        {
            if (!byte.TryParse(
                    compact.AsSpan(index * 2, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out value[index]))
            {
                return Fail("Use only hexadecimal byte values (00 through FF).", out value, out error);
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseUnsigned(
        DebuggerRegister register,
        string? text,
        bool isBigEndian,
        out byte[] value,
        out string error)
    {
        string compact = (text ?? string.Empty).Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        if (compact.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            compact = compact[2..];
        }

        if (compact.Length == 0 ||
            !BigInteger.TryParse(
                "0" + compact,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out BigInteger numericValue))
        {
            return Fail("Enter an unsigned hexadecimal value, optionally prefixed with 0x.", out value, out error);
        }

        BigInteger maximumValue = (BigInteger.One << register.BitWidth) - BigInteger.One;
        if (numericValue > maximumValue)
        {
            return Fail($"The value does not fit in this {register.BitWidth}-bit register.", out value, out error);
        }

        int byteCount = register.Value.Length;
        byte[] encoded = numericValue.ToByteArray(isUnsigned: true, isBigEndian: isBigEndian);
        value = new byte[byteCount];
        if (isBigEndian)
        {
            encoded.CopyTo(value, byteCount - encoded.Length);
        }
        else
        {
            encoded.CopyTo(value, 0);
        }

        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out byte[] value, out string error)
    {
        value = Array.Empty<byte>();
        error = message;
        return false;
    }
}
