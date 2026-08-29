using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed record PluginMetadata(
    string Id,
    string Name,
    string Platform,
    string Backend,
    Version Version,
    string Description,
    TargetArchitecture Architecture);
