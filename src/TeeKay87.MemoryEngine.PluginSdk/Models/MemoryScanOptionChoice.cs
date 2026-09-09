using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record MemoryScanOptionChoice
{
    public MemoryScanOptionChoice(string id, string displayName, string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        DisplayName = displayName;
        Description = description ?? string.Empty;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public override string ToString() => DisplayName;
}
