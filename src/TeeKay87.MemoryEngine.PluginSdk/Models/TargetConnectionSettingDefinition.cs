namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record TargetConnectionSettingDefinition(
    string Key,
    string Label,
    string? Description = null,
    string? DefaultValue = null,
    bool IsRequired = false);
