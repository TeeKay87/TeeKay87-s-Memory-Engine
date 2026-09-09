using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanner
{
    Task<IReadOnlyList<NativeValueScanResult>> ScanAsync(
        TargetProcess process,
        IReadOnlyList<MemoryRegion> memoryRegions,
        NativeValueScanRequest request,
        CancellationToken cancellationToken);
}
