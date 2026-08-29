namespace TeeKay87.MemoryEngine.Core.Plugins;

public sealed record PluginDiscoveryError(
    string AssemblyPath,
    string Message);
