using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.Core.Plugins;

public sealed record DiscoveredPlugin(
    ITargetPlugin Instance,
    string AssemblyPath);
