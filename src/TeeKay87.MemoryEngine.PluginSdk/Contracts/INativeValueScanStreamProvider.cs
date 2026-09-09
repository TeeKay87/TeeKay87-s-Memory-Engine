using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanStreamProvider
{
    Task<INativeValueScanResultStream> StartScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken);
}
