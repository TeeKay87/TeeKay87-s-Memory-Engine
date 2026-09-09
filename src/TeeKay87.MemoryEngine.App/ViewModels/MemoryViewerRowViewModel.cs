using System;
using System.Globalization;
using System.Text;
using TeeKay87.MemoryEngine.Core.MemoryViewer;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class MemoryViewerRowViewModel
{
    public MemoryViewerRowViewModel(
        ulong address,
        ReadOnlySpan<byte> bytes,
        ulong requestedAddress,
        bool canEdit)
        : this(
            address,
            bytes,
            new MemoryViewHighlightSpan(requestedAddress, 1),
            canEdit)
    {
    }

    public MemoryViewerRowViewModel(
        ulong address,
        ReadOnlySpan<byte> bytes,
        MemoryViewHighlightSpan highlightSpan,
        bool canEdit)
    {
        byte[] rowBytes = bytes.ToArray();

        AddressValue = address;
        Address = $"0x{address:X}";
        Bytes = rowBytes;
        HexBytes = FormatHex(rowBytes);
        Ascii = FormatAscii(rowBytes);
        CanEdit = canEdit;
        IsOriginRow =
            highlightSpan.Address >= address &&
            highlightSpan.Address - address < (ulong)rowBytes.Length;

        MemoryViewRowHighlight rowHighlight = rowBytes.Length == 0
            ? MemoryViewRowHighlight.Empty
            : highlightSpan.GetRowHighlight(address, rowBytes.Length);
        HighlightStartIndex = rowHighlight.StartIndex;
        HighlightByteCount = rowHighlight.ByteCount;

        SplitHexText(
            HexBytes,
            rowHighlight,
            out string hexPrefix,
            out string highlightedHexBytes,
            out string hexSuffix);
        HexPrefix = hexPrefix;
        HighlightedHexBytes = highlightedHexBytes;
        HexSuffix = hexSuffix;

        SplitAsciiText(
            Ascii,
            rowHighlight,
            out string asciiPrefix,
            out string highlightedAscii,
            out string asciiSuffix);
        AsciiPrefix = asciiPrefix;
        HighlightedAscii = highlightedAscii;
        AsciiSuffix = asciiSuffix;
    }

    public ulong AddressValue { get; }

    public string Address { get; }

    public ReadOnlyMemory<byte> Bytes { get; }

    public int ByteCount => Bytes.Length;

    public string HexBytes { get; }

    public string Ascii { get; }

    public bool CanEdit { get; }

    public bool IsOriginRow { get; }

    public int HighlightStartIndex { get; }

    public int HighlightByteCount { get; }

    public bool HasHighlightedBytes => HighlightByteCount > 0;

    public string HexPrefix { get; }

    public string HighlightedHexBytes { get; }

    public string HexSuffix { get; }

    public string AsciiPrefix { get; }

    public string HighlightedAscii { get; }

    public string AsciiSuffix { get; }

    public string ClipboardRow => $"{Address}\t{HexBytes}\t{Ascii}";

    private static string FormatHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        StringBuilder builder = new(checked(bytes.Length * 3 - 1));
        for (int index = 0; index < bytes.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string FormatAscii(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        char[] characters = new char[bytes.Length];
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            characters[index] = value is >= 0x20 and <= 0x7E ? (char)value : '.';
        }

        return new string(characters);
    }

    private static void SplitHexText(
        string hexBytes,
        MemoryViewRowHighlight highlight,
        out string prefix,
        out string highlighted,
        out string suffix)
    {
        if (highlight.IsEmpty || string.IsNullOrEmpty(hexBytes))
        {
            prefix = hexBytes;
            highlighted = string.Empty;
            suffix = string.Empty;
            return;
        }

        int highlightStart = checked(highlight.StartIndex * 3);
        int highlightLength = checked(highlight.ByteCount * 3 - 1);
        prefix = hexBytes[..highlightStart];
        highlighted = hexBytes.Substring(highlightStart, highlightLength);
        suffix = hexBytes[(highlightStart + highlightLength)..];
    }

    private static void SplitAsciiText(
        string ascii,
        MemoryViewRowHighlight highlight,
        out string prefix,
        out string highlighted,
        out string suffix)
    {
        if (highlight.IsEmpty || string.IsNullOrEmpty(ascii))
        {
            prefix = ascii;
            highlighted = string.Empty;
            suffix = string.Empty;
            return;
        }

        prefix = ascii[..highlight.StartIndex];
        highlighted = ascii.Substring(highlight.StartIndex, highlight.ByteCount);
        suffix = ascii[(highlight.StartIndex + highlight.ByteCount)..];
    }
}
