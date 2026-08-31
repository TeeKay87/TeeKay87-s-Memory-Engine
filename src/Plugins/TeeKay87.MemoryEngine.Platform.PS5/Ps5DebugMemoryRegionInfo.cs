namespace TeeKay87.MemoryEngine.Platform.PS5;

internal sealed record Ps5DebugMemoryRegionInfo(
    string? Name,
    ulong Start,
    ulong End,
    ulong Offset,
    ushort Protection);
