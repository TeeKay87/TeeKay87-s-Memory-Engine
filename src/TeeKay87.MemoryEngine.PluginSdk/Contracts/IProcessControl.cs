using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IProcessControl
{
    Task SuspendAsync(TargetProcess process, CancellationToken cancellationToken);

    Task ResumeAsync(TargetProcess process, CancellationToken cancellationToken);
}
