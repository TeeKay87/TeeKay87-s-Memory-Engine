using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface INativeValueScanStreamRefiner
{
    Task<INativeValueScanResultStream> StartRefineAsync(
        TargetProcess process,
        INativeValueScanCandidateSource previousResults,
        NativeValueScanRequest request,
        CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
