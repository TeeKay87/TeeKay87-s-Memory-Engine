using System;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Scanning;

/// <summary>
/// Resolves whether a Core Scan Type may be delegated to a plugin's native scanner.
/// Plugins that publish native mappings are authoritative: only semantically equivalent
/// mappings are eligible. Older Plugin API 2.x plugins without the optional mapping
/// contract retain the legacy try-native-then-fallback behavior.
/// </summary>
public static class NativeScanTypeResolver
{
    public static bool ShouldAttemptNativeScan(
        ITargetSession session,
        IMemoryScanType scanType,
        IMemoryValueType valueType,
        MemoryScanStage stage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(scanType);
        ArgumentNullException.ThrowIfNull(valueType);

        INativeScanTypeMappingProvider? mappingProvider = session.GetService<INativeScanTypeMappingProvider>();
        if (mappingProvider is null)
        {
            return true;
        }

        return mappingProvider.NativeScanTypeMappings.Any(mapping =>
            mapping.Supports(scanType.Id, valueType.Id, stage));
    }
}
