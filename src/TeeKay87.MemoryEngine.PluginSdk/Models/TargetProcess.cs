namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record TargetProcess(
    ulong Id,
    string Name,
    string? DisplayName = null);
