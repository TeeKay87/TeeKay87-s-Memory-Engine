using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.Core.Disassembly;

public sealed class DisassemblyByteOverlay
{
    private readonly byte[] _bytes;

    public DisassemblyByteOverlay(ulong address, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("A disassembly byte overlay must contain at least one byte.", nameof(bytes));
        }

        Address = address;
        _bytes = bytes.ToArray();
    }

    public ulong Address { get; }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public ulong EndAddressExclusive => checked(Address + (ulong)_bytes.Length);
}

public sealed record DisassemblyMarker
{
    public DisassemblyMarker(ulong address, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Address = address;
        Text = text.Trim();
    }

    public ulong Address { get; }

    public string Text { get; }
}

public sealed class DisassemblyOverlay
{
    private readonly IReadOnlyList<DisassemblyByteOverlay> _byteOverlays;
    private readonly IReadOnlyList<DisassemblyMarker> _markers;

    public DisassemblyOverlay(
        IEnumerable<DisassemblyByteOverlay>? byteOverlays = null,
        IEnumerable<DisassemblyMarker>? markers = null)
    {
        _byteOverlays = Array.AsReadOnly((byteOverlays ?? Enumerable.Empty<DisassemblyByteOverlay>()).ToArray());
        _markers = Array.AsReadOnly((markers ?? Enumerable.Empty<DisassemblyMarker>()).ToArray());
    }

    public static DisassemblyOverlay Empty { get; } = new();

    public IReadOnlyList<DisassemblyByteOverlay> ByteOverlays => _byteOverlays;

    public IReadOnlyList<DisassemblyMarker> Markers => _markers;

    public bool IsEmpty => _byteOverlays.Count == 0 && _markers.Count == 0;
}
