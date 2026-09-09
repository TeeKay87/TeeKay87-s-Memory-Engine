using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerProvider
{
    Task<IDebuggerSession> AttachAsync(
        TargetProcess process,
        CancellationToken cancellationToken);
}
