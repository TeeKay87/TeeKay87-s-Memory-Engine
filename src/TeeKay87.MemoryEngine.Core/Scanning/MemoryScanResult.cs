using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

public sealed record MemoryScanResult(
    ulong Address,
    MemoryScanValue CurrentValue,
    MemoryScanValue? PreviousValue,
    string? RegionName,
    string? ModuleName,
    MemoryProtection Protection)
{
    public string ValueTypeId => CurrentValue.ValueTypeId;
}
