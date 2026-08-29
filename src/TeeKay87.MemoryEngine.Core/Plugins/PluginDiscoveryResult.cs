using System.Collections.Generic;

namespace TeeKay87.MemoryEngine.Core.Plugins;

public sealed record PluginDiscoveryResult(
    IReadOnlyList<DiscoveredPlugin> Plugins,
    IReadOnlyList<PluginDiscoveryError> Errors);
