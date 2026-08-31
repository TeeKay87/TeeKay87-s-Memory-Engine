using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanRefiner
{
    Task<IReadOnlyList<ulong>> RefineAsync(
        TargetProcess process,
        IReadOnlyList<ulong> previousAddresses,
        NativeValueScanRequest request,
        CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
