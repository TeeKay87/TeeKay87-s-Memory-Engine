using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

[Flags]
public enum MemoryProtection
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Execute = 1 << 2,
    Guard = 1 << 3,
    Private = 1 << 4,
    Shared = 1 << 5
}

public sealed record MemoryRegion(
    ulong BaseAddress,
    ulong Size,
    MemoryProtection Protection,
    string? Name = null,
    string? ModuleName = null)
{
    public ulong EndAddressExclusive => checked(BaseAddress + Size);
}
