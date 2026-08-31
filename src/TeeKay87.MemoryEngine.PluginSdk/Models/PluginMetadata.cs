using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record PluginMetadata(
    string Id,
    string Name,
    string Platform,
    string Backend,
    Version Version,
    int Revision,
    Version ApiVersion,
    string Description,
    TargetArchitecture Architecture)
{
    public string DisplayVersion => $"{Version}.rev{Revision}";
}
