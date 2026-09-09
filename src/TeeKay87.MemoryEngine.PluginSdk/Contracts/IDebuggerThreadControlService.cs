using System.Threading;
using System.Threading.Tasks;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerThreadControlService
{
    Task SuspendThreadAsync(ulong threadId, CancellationToken cancellationToken);

    Task ResumeThreadAsync(ulong threadId, CancellationToken cancellationToken);
}
