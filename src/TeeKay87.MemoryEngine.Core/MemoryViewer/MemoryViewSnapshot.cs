using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public sealed record MemoryViewSnapshot(
    ulong RequestedAddress,
    ulong StartAddress,
    ReadOnlyMemory<byte> Bytes,
    MemoryRegion Region,
    int BytesPerRow)
{
    public ulong EndAddressExclusive => checked(StartAddress + (ulong)Bytes.Length);
}
