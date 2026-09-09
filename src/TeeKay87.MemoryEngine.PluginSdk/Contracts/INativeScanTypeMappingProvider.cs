using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional native-scan capability metadata. Each mapping declares semantic equivalence
/// between a Core Scan Type and one plugin-owned native Scan Type for specific stages/value types.
/// Scan Types omitted from the mapping table must use the shared Core scanner.
/// </summary>
public interface INativeScanTypeMappingProvider
{
    IReadOnlyList<NativeScanTypeMapping> NativeScanTypeMappings { get; }
}
