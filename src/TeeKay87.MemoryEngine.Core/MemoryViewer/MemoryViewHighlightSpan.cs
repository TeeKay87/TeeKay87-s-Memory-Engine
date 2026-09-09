using System;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public readonly record struct MemoryViewHighlightSpan
{
    public MemoryViewHighlightSpan(ulong address, int byteCount)
    {
        if (byteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "A Memory Viewer highlight span must contain at least one byte.");
        }

        Address = address;
        ByteCount = byteCount;
    }

    public ulong Address { get; }

    public int ByteCount { get; }

    public MemoryViewRowHighlight GetRowHighlight(ulong rowAddress, int rowByteCount)
    {
        if (rowByteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowByteCount));
        }

        ulong rowLastAddress = SaturatingAdd(rowAddress, checked((ulong)rowByteCount - 1UL));
        ulong spanLastAddress = SaturatingAdd(Address, checked((ulong)ByteCount - 1UL));

        if (spanLastAddress < rowAddress || rowLastAddress < Address)
        {
            return MemoryViewRowHighlight.Empty;
        }

        ulong overlapStart = Math.Max(Address, rowAddress);
        ulong overlapEnd = Math.Min(spanLastAddress, rowLastAddress);
        int startIndex = checked((int)(overlapStart - rowAddress));
        int overlapByteCount = checked((int)(overlapEnd - overlapStart + 1UL));
        return new MemoryViewRowHighlight(startIndex, overlapByteCount);
    }

    private static ulong SaturatingAdd(ulong value, ulong increment)
    {
        return increment > ulong.MaxValue - value
            ? ulong.MaxValue
            : value + increment;
    }
}

public readonly record struct MemoryViewRowHighlight(int StartIndex, int ByteCount)
{
    public static MemoryViewRowHighlight Empty { get; } = new(0, 0);

    public bool IsEmpty => ByteCount == 0;
}
