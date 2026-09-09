using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IDebuggerStepService
{
    Task StepAsync(
        DebuggerStepKind stepKind,
        ulong? threadId,
        CancellationToken cancellationToken);
}
