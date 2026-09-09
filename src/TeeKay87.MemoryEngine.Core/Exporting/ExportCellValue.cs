using System;
using System.Globalization;
using System.Text.Json;

namespace TeeKay87.MemoryEngine.Core.Exporting;

public enum ExportCellValueKind
{
    Null,
    String,
    Boolean,
    Int64,
    UInt64,
    Double
}

public readonly struct ExportCellValue
{
    private readonly string? _stringValue;
    private readonly long _int64Value;
    private readonly ulong _uint64Value;
    private readonly double _doubleValue;
    private readonly bool _booleanValue;

    private ExportCellValue(
        ExportCellValueKind kind,
        string? stringValue = null,
        long int64Value = 0,
        ulong uint64Value = 0,
        double doubleValue = 0,
        bool booleanValue = false)
    {
        Kind = kind;
        _stringValue = stringValue;
        _int64Value = int64Value;
        _uint64Value = uint64Value;
        _doubleValue = doubleValue;
        _booleanValue = booleanValue;
    }

    public ExportCellValueKind Kind { get; }

    public static ExportCellValue Null => default;

    public static ExportCellValue FromString(string? value) => value is null
        ? Null
        : new ExportCellValue(ExportCellValueKind.String, stringValue: value);

    public static ExportCellValue FromBoolean(bool value) =>
        new(ExportCellValueKind.Boolean, booleanValue: value);

    public static ExportCellValue FromInt64(long value) =>
        new(ExportCellValueKind.Int64, int64Value: value);

    public static ExportCellValue FromUInt64(ulong value) =>
        new(ExportCellValueKind.UInt64, uint64Value: value);

    public static ExportCellValue FromDouble(double value) =>
        new(ExportCellValueKind.Double, doubleValue: value);

    public string ToInvariantString()
    {
        return Kind switch
        {
            ExportCellValueKind.Null => string.Empty,
            ExportCellValueKind.String => _stringValue ?? string.Empty,
            ExportCellValueKind.Boolean => _booleanValue ? "true" : "false",
            ExportCellValueKind.Int64 => _int64Value.ToString(CultureInfo.InvariantCulture),
            ExportCellValueKind.UInt64 => _uint64Value.ToString(CultureInfo.InvariantCulture),
            ExportCellValueKind.Double => _doubleValue.ToString("R", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unsupported export cell kind '{Kind}'.")
        };
    }

    internal void WriteJson(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (Kind)
        {
            case ExportCellValueKind.Null:
                writer.WriteNullValue();
                break;
            case ExportCellValueKind.String:
                writer.WriteStringValue(_stringValue);
                break;
            case ExportCellValueKind.Boolean:
                writer.WriteBooleanValue(_booleanValue);
                break;
            case ExportCellValueKind.Int64:
                writer.WriteNumberValue(_int64Value);
                break;
            case ExportCellValueKind.UInt64:
                writer.WriteNumberValue(_uint64Value);
                break;
            case ExportCellValueKind.Double:
                writer.WriteNumberValue(_doubleValue);
                break;
            default:
                throw new InvalidOperationException($"Unsupported export cell kind '{Kind}'.");
        }
    }
}
