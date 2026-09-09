using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.MemoryViewer;

public static class MemoryViewerRegionNavigator
{
    public static MemoryRegion? FindPreviousReadableRegion(
        IReadOnlyList<MemoryRegion> memoryRegions,
        MemoryRegion currentRegion)
    {
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(currentRegion);

        return memoryRegions
            .Where(IsReadableRegion)
            .Where(region => region.BaseAddress < currentRegion.BaseAddress)
            .OrderByDescending(region => region.BaseAddress)
            .ThenByDescending(region => region.Size)
            .FirstOrDefault();
    }

    public static MemoryRegion? FindNextReadableRegion(
        IReadOnlyList<MemoryRegion> memoryRegions,
        MemoryRegion currentRegion)
    {
        ArgumentNullException.ThrowIfNull(memoryRegions);
        ArgumentNullException.ThrowIfNull(currentRegion);

        return memoryRegions
            .Where(IsReadableRegion)
            .Where(region => region.BaseAddress > currentRegion.BaseAddress)
            .OrderBy(region => region.BaseAddress)
            .ThenBy(region => region.Size)
            .FirstOrDefault();
    }

    public static bool IsReadableRegion(MemoryRegion region)
    {
        return region.Size > 0 &&
               region.Protection.HasFlag(MemoryProtection.Read) &&
               !region.Protection.HasFlag(MemoryProtection.Guard);
    }
}
